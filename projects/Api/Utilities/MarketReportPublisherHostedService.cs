using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Types;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.Utilities;

/// <summary>
/// Background service that publishes persisted <see cref="CityMarketReport"/> rows to the
/// MasterApi newsroom as <c>MARKET_REPORT</c> news entries.
/// Runs every 60 seconds so that newly-generated reports are surfaced promptly.
/// Idempotent: reports with an existing <see cref="CityMarketReport.MasterNewsEntryId"/> are skipped.
/// </summary>
public sealed class MarketReportPublisherHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<MarketReportPublisherHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan PublishInterval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Delay startup to let other services (master registration, tick engine) stabilize first.
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingReportsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in market report publisher loop.");
            }

            try
            {
                await Task.Delay(PublishInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task PublishPendingReportsAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var masterService = scope.ServiceProvider.GetRequiredService<IMasterGameAdministrationService>();

        // Fetch unpublished reports (no MasterNewsEntryId set yet).
        var pending = await db.CityMarketReports
            .Where(r => r.MasterNewsEntryId == null)
            .OrderBy(r => r.GeneratedAtUtc)
            .Take(20)
            .ToListAsync(ct);

        if (pending.Count == 0)
            return;

        foreach (var report in pending)
        {
            try
            {
                var localizations = CityMarketReportService.BuildLocalizations(report);
                if (localizations.Count == 0)
                    continue;

                var locInputs = localizations
                    .Select(l => new GameNewsLocalizationInput
                    {
                        Locale = l.Locale,
                        Title = l.Title,
                        Summary = l.Summary,
                        HtmlContent = l.HtmlContent,
                    })
                    .ToList();

                var result = await masterService.UpsertGameNewsEntryAsync(
                    requesterEmail: GameConstants.SystemRequesterEmail,
                    entryId: null,
                    entryType: "MARKET_REPORT",
                    status: "PUBLISHED",
                    localizations: locInputs,
                    cancellationToken: ct);

                report.MasterNewsEntryId = result.Id;
                await db.SaveChangesAsync(ct);

                logger.LogInformation(
                    "Published {Type} market report for city {CityId} (ticks {From}–{To}) as news entry {EntryId}.",
                    report.ReportType,
                    report.CityId,
                    report.TickFrom,
                    report.TickTo,
                    result.Id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to publish market report {Id} (city={CityId}, type={Type}). Will retry next cycle.",
                    report.Id,
                    report.CityId,
                    report.ReportType);
            }
        }
    }
}

using Api.Data.Entities;
using Api.Utilities;
using Microsoft.Extensions.Logging;

namespace Api.Engine.Phases;

/// <summary>
/// Generates weekly and monthly city market reports at their respective tick boundaries.
/// Reports are persisted as <see cref="CityMarketReport"/> rows and later published to the
/// newsroom by <see cref="MarketReportPublisherHostedService"/>.
/// Runs after all economic phases (order 1100) so reports capture complete tick data.
/// </summary>
public sealed class MarketReportPhase(ILogger<MarketReportPhase> logger) : ITickPhase
{
    public string Name => "MarketReport";
    public int Order => 1100;

    public async Task ProcessAsync(TickContext context)
    {
        var currentTick = context.GameState.CurrentTick;
        var isWeeklyBoundary = currentTick % GameConstants.TicksPerWeek == 0;
        var isMonthlyBoundary = currentTick % GameConstants.TicksPerMonth == 0;

        if (!isWeeklyBoundary && !isMonthlyBoundary)
            return;

        if (isWeeklyBoundary)
        {
            var tickFrom = currentTick - GameConstants.TicksPerWeek + 1;
            var tickTo = currentTick;
            await GenerateReportsAsync(context, MarketReportType.Weekly, tickFrom, tickTo);
        }

        // Monthly boundary is also a weekly boundary every 4.3 weeks — generate monthly separately.
        if (isMonthlyBoundary)
        {
            var tickFrom = currentTick - GameConstants.TicksPerMonth + 1;
            var tickTo = currentTick;
            await GenerateReportsAsync(context, MarketReportType.Monthly, tickFrom, tickTo);
        }
    }

    private async Task GenerateReportsAsync(
        TickContext context,
        string reportType,
        long tickFrom,
        long tickTo)
    {
        try
        {
            var reports = await CityMarketReportService.GenerateReportsAsync(
                context.Db,
                reportType,
                tickFrom,
                tickTo);

            if (reports.Count == 0)
                return;

            context.Db.CityMarketReports.AddRange(reports);
            logger.LogInformation(
                "Tick {Tick}: generated {Count} {Type} market reports (ticks {From}–{To}).",
                context.GameState.CurrentTick,
                reports.Count,
                reportType,
                tickFrom,
                tickTo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tick {Tick}: failed to generate {Type} market reports.", context.GameState.CurrentTick, reportType);
        }
    }
}

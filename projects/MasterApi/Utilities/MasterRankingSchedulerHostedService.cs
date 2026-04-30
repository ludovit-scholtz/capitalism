using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MasterApi.Utilities;

public sealed class MasterRankingSchedulerHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<MasterRankingSchedulerHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunScheduledWorkAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Master ranking scheduler iteration failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task RunScheduledWorkAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var currentHourStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
        var currentDate = now.Date;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var rankingService = scope.ServiceProvider.GetRequiredService<MasterRankingService>();

        var hourlyRunExists = await db.MasterRankingEvaluationRuns
            .AsNoTracking()
            .AnyAsync(
                run => run.RunType == RankingRunType.HourlyEvaluation
                    && run.Status == RankingRunStatus.Succeeded
                    && run.StartedAtUtc >= currentHourStart
                    && run.StartedAtUtc < currentHourStart.AddHours(1),
                cancellationToken);

        if (!hourlyRunExists)
        {
            await rankingService.EvaluateHourlyAsync(cancellationToken);
        }

        var dailyRunExists = await db.MasterRankingEvaluationRuns
            .AsNoTracking()
            .AnyAsync(
                run => run.RunType == RankingRunType.DailyDecay
                    && run.Status == RankingRunStatus.Succeeded
                    && run.StartedAtUtc >= currentDate
                    && run.StartedAtUtc < currentDate.AddDays(1),
                cancellationToken);

        if (!dailyRunExists && now.Hour == 0)
        {
            await rankingService.ApplyDailyDecayAsync(cancellationToken);
        }
    }
}

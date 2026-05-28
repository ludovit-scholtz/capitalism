using MasterApi.Configuration;
using Microsoft.Extensions.Options;

namespace MasterApi.Utilities;

public sealed class WeeklyEmailReportHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<EmailOptions> emailOptions,
    TimeProvider timeProvider,
    ILogger<WeeklyEmailReportHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = emailOptions.Value;
        var interval = TimeSpan.FromMinutes(Math.Max(5, options.SchedulerIntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunIfDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Weekly email report scheduler failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task RunIfDueAsync(CancellationToken cancellationToken)
    {
        var options = emailOptions.Value;
        if (!options.Enabled || !options.WeeklyReportsEnabled)
        {
            return;
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (nowUtc.DayOfWeek != options.WeeklyReportDayOfWeek || nowUtc.Hour != options.WeeklyReportUtcHour)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IWeeklyEmailReportService>();
        var sent = await service.SendDueWeeklyReportsAsync(nowUtc, cancellationToken);
        if (sent > 0)
        {
            logger.LogInformation("Sent {Count} weekly email reports.", sent);
        }
    }
}

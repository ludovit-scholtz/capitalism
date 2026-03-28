using Api.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.Engine;

/// <summary>
/// Background service that runs the game tick loop.
/// Each iteration creates a fresh DI scope, processes one tick via
/// <see cref="TickProcessor"/>, then waits for the configured interval.
/// </summary>
public sealed class GameTickHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<GameEngineOptions> options,
    ILogger<GameTickHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Game tick engine is disabled via configuration.");
            return;
        }

        logger.LogInformation("Game tick engine started.");

        // Small initial delay to let the host finish startup.
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var intervalSeconds = 10;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<TickProcessor>();
                intervalSeconds = await processor.ProcessTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error in game tick loop; retrying in {Interval}s.", intervalSeconds);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("Game tick engine stopped.");
    }
}

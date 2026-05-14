using MasterApi.Configuration;
using MasterApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MasterApi.Utilities;

/// <summary>
/// Background service that periodically marks stale game servers as inactive
/// when their last heartbeat is older than the configured threshold.
/// </summary>
public sealed class GameServerEvictionHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<MasterServerOptions> options,
    ILogger<GameServerEvictionHostedService> logger) : BackgroundService
{
    /// <summary>
    /// The eviction check runs at half the active-threshold interval,
    /// but no faster than this many seconds.
    /// </summary>
    private const int MinimumEvictionIntervalSeconds = 30;

    /// <summary>
    /// Divisor applied to <see cref="MasterServerOptions.ActiveThresholdSeconds"/>
    /// to derive the check interval (check twice per threshold window).
    /// </summary>
    private const int EvictionIntervalDivisor = 2;

    /// <summary>
    /// Safety lower-bound on the <see cref="MasterServerOptions.ActiveThresholdSeconds"/>
    /// value used for cutoff calculation, preventing negative or zero cutoffs on
    /// misconfigured instances.
    /// </summary>
    private const int MinimumThresholdSeconds = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var evictionIntervalSeconds = Math.Max(
            MinimumEvictionIntervalSeconds,
            options.Value.ActiveThresholdSeconds / EvictionIntervalDivisor);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvictStaleServersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Game server eviction iteration failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(evictionIntervalSeconds), stoppingToken);
        }
    }

    private async Task EvictStaleServersAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddSeconds(
            -Math.Max(MinimumThresholdSeconds, options.Value.ActiveThresholdSeconds));

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();

        var staleServers = await db.GameServers
            .Where(s => s.IsActive && s.LastHeartbeatAtUtc < cutoff)
            .ToListAsync(cancellationToken);

        if (staleServers.Count == 0)
            return;

        foreach (var server in staleServers)
        {
            server.IsActive = false;
            logger.LogInformation(
                "Game server {ServerId} ({DisplayName}) marked inactive: last heartbeat was {LastHeartbeatAtUtc:O}.",
                server.Id,
                server.DisplayName,
                server.LastHeartbeatAtUtc);
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Evicted {Count} stale game server(s).", staleServers.Count);
    }
}

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
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var evictionIntervalSeconds = Math.Max(30, options.Value.ActiveThresholdSeconds / 2);

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
        var cutoff = DateTime.UtcNow.AddSeconds(-Math.Max(5, options.Value.ActiveThresholdSeconds));

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

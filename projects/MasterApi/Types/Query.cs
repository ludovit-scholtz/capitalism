using MasterApi.Configuration;
using MasterApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MasterApi.Types;

public sealed class Query
{
    public async Task<List<GameServerSummary>> GetGameServers(
        [Service] MasterDbContext db,
        [Service] IOptions<MasterServerOptions> options)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddSeconds(-Math.Max(5, options.Value.ActiveThresholdSeconds));

        var servers = await db.GameServers
            .OrderByDescending(server => server.LastHeartbeatAtUtc)
            .ThenBy(server => server.DisplayName)
            .ToListAsync();

        return servers
            .Select(server => ToSummary(server, cutoff))
            .OrderByDescending(server => server.IsOnline)
            .ThenBy(server => server.DisplayName)
            .ToList();
    }

    internal static GameServerSummary ToSummary(Data.Entities.GameServerNode server, DateTime cutoff)
    {
        return new GameServerSummary
        {
            Id = server.Id,
            ServerKey = server.ServerKey,
            DisplayName = server.DisplayName,
            Description = server.Description,
            Region = server.Region,
            Environment = server.Environment,
            BackendUrl = server.BackendUrl,
            GraphqlUrl = server.GraphqlUrl,
            FrontendUrl = server.FrontendUrl,
            Version = server.Version,
            PlayerCount = server.PlayerCount,
            CompanyCount = server.CompanyCount,
            CurrentTick = server.CurrentTick,
            RegisteredAtUtc = server.RegisteredAtUtc,
            LastHeartbeatAtUtc = server.LastHeartbeatAtUtc,
            IsOnline = server.LastHeartbeatAtUtc >= cutoff,
        };
    }
}
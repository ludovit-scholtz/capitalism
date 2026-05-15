using Api.Configuration;
using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>Returns the configured billionaire net-worth benchmark (USD) from server settings.</summary>
    public decimal BillionaireBenchmarkUsd([Service] IOptions<GameRulesOptions> options) =>
        options.Value.BillionaireNetWorthBenchmarkUsd;

    /// <summary>Returns the current shard lifecycle state and winner information if the game has ended.</summary>
    public async Task<ShardStatusResult> ShardStatus(
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        var gameState = await db.GameStates
            .AsNoTracking()
            .FirstOrDefaultDeterministicAsync(cancellationToken);

        if (gameState is null)
        {
            return new ShardStatusResult { ShardState = "Active" };
        }

        return new ShardStatusResult
        {
            ShardState = gameState.ShardState == GameShardState.Concluded ? "CONCLUDED" : "ACTIVE",
            GameEnded = gameState.GameEnded,
            WinnerPlayerId = gameState.WinnerPlayerId,
            WinnerDisplayName = gameState.WinnerDisplayName,
            WinnerCompanyName = gameState.WinnerCompanyName,
            WinnerNetWorth = gameState.WinnerNetWorth,
            ConcludedAtUtc = gameState.GameEndedAtUtc,
        };
    }

    /// <summary>Returns the most recent victory newsletter, or null if the shard has not concluded.</summary>
    public async Task<VictoryNewsletterResult?> VictoryNewsletter(
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        var newsletter = await db.VictoryNewsletters
            .AsNoTracking()
            .OrderByDescending(v => v.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (newsletter is null)
        {
            return null;
        }

        return new VictoryNewsletterResult
        {
            Id = newsletter.Id,
            WinnerPlayerId = newsletter.WinnerPlayerId,
            WinnerDisplayName = newsletter.WinnerDisplayName,
            WinnerCompanyName = newsletter.WinnerCompanyName,
            WinnerNetWorthUsd = newsletter.WinnerNetWorthUsd,
            Top10RankingsJson = newsletter.Top10RankingsJson,
            TotalFxTradeCount = newsletter.TotalFxTradeCount,
            TotalFxVolumeUsd = newsletter.TotalFxVolumeUsd,
            TotalProductsSold = newsletter.TotalProductsSold,
            ActiveCitiesCount = newsletter.ActiveCitiesCount,
            GameDurationTicks = newsletter.GameDurationTicks,
            CreatedAtUtc = newsletter.CreatedAtUtc,
        };
    }
}

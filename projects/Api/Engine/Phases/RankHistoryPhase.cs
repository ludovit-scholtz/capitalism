using Api.Data;
using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine.Phases;

/// <summary>
/// Captures weekly leaderboard rank snapshots for all non-admin players.
/// Runs every 1,008 ticks (≈ 7 game days) — enough to produce 52 snapshots per year.
/// At most 365 snapshots per player are retained (FIFO pruning of older entries).
///
/// Wealth metric: personal bank account balances in USD (simplified; share-price
/// computation would require extra round-trips outside the tick pipeline).
///
/// Order = 1150 — runs after <see cref="EconomicReportPhase"/> (1050) and
/// <see cref="MarketReportPhase"/> (1100) so all balance changes from the current
/// tick are already committed to bank accounts.
/// </summary>
public sealed class RankHistoryPhase : ITickPhase
{
    public string Name => "RankHistory";
    public int Order => 1150;

    /// <summary>Capture one snapshot every 1,008 ticks (≈ 1 game week).</summary>
    private const long SnapshotIntervalTicks = 1_008;

    /// <summary>Maximum snapshots retained per player.</summary>
    private const int MaxSnapshotsPerPlayer = 365;

    public async Task ProcessAsync(TickContext context)
    {
        // Only run on the weekly snapshot boundary.
        if (context.CurrentTick % SnapshotIntervalTicks != 0)
            return;

        var db = context.Db;
        var ct = CancellationToken.None; // phase context has no CT; use None

        // Load all non-admin players (lightweight — only IDs and roles).
        var players = await db.Players
            .AsNoTracking()
            .Where(p => p.Role != PlayerRole.Admin
                && p.Email != GovernmentActorConstants.GovernmentEmail)
            .Select(p => new { p.Id })
            .ToListAsync(ct);

        if (players.Count == 0)
            return;

        // Load personal bank account balances.
        var playerIds = players.Select(p => p.Id).ToList();
        var personalAccounts = await db.BankAccounts
            .AsNoTracking()
            .Where(a => a.PlayerId.HasValue
                && playerIds.Contains(a.PlayerId.Value)
                && a.ClosedAtUtc == null)
            .Select(a => new { a.PlayerId, a.Balance, a.CurrencyCode })
            .ToListAsync(ct);

        // Build EUR rates lookup that also includes USD so we can convert to USD.
        var allCurrencyCodes = personalAccounts.Select(a => a.CurrencyCode).Distinct().Append("USD").ToList();
        var eurRates = await FxRateHelper.BuildEurRatesLookupAsync(db, allCurrencyCodes);

        // Compute per-player wealth in USD.
        var wealthByPlayer = new Dictionary<Guid, decimal>(players.Count);
        foreach (var pid in playerIds)
        {
            var totalUsd = personalAccounts
                .Where(a => a.PlayerId == pid)
                .Sum(a => decimal.Round(
                    FxRateHelper.ConvertToUsd(a.Balance, a.CurrencyCode, eurRates),
                    2, MidpointRounding.AwayFromZero));
            wealthByPlayer[pid] = totalUsd;
        }

        // Build ordered ranking.
        var ranked = wealthByPlayer
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)    // deterministic tie-breaking
            .Select((kv, idx) => new { PlayerId = kv.Key, WealthUsd = kv.Value, Rank = idx + 1 })
            .ToList();

        int totalPlayers = ranked.Count;

        // Load existing snapshots for these players so we can detect the previous rank.
        var existingLatest = await db.PlayerRankSnapshots
            .AsNoTracking()
            .Where(s => playerIds.Contains(s.PlayerId))
            .GroupBy(s => s.PlayerId)
            .Select(g => new { PlayerId = g.Key, LatestTick = g.Max(s => s.SnapshotTick), LatestRank = g.OrderByDescending(s => s.SnapshotTick).Select(s => s.LeaderboardRank).First() })
            .ToListAsync(ct);

        var latestByPlayer = existingLatest.ToDictionary(x => x.PlayerId);

        // Insert new snapshot for each player.
        var utcNow = DateTime.UtcNow;
        var newSnapshots = new List<PlayerRankSnapshot>(players.Count);

        foreach (var entry in ranked)
        {
            var percentile = totalPlayers > 1
                ? decimal.Round(100m * (totalPlayers - entry.Rank) / (totalPlayers - 1), 1, MidpointRounding.AwayFromZero)
                : 100m;

            int? positionChange = null;
            if (latestByPlayer.TryGetValue(entry.PlayerId, out var prev))
            {
                // Positive = improved (moved up the leaderboard).
                positionChange = prev.LatestRank - entry.Rank;
            }

            newSnapshots.Add(new PlayerRankSnapshot
            {
                Id = Guid.NewGuid(),
                PlayerId = entry.PlayerId,
                SnapshotTick = context.CurrentTick,
                SnapshotUtc = utcNow,
                LeaderboardRank = entry.Rank,
                WealthUsd = entry.WealthUsd,
                PercentileRank = percentile,
                PositionChange = positionChange,
            });
        }

        db.PlayerRankSnapshots.AddRange(newSnapshots);

        // Prune old snapshots (keep latest MaxSnapshotsPerPlayer per player).
        // Load counts first; only do the expensive query if anyone is over the limit.
        var counts = await db.PlayerRankSnapshots
            .GroupBy(s => s.PlayerId)
            .Select(g => new { PlayerId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        foreach (var c in counts.Where(c => c.Count > MaxSnapshotsPerPlayer))
        {
            // Delete oldest entries beyond the cap.
            var excess = c.Count - MaxSnapshotsPerPlayer;
            var toDelete = await db.PlayerRankSnapshots
                .Where(s => s.PlayerId == c.PlayerId)
                .OrderBy(s => s.SnapshotTick)
                .Take(excess)
                .ToListAsync(ct);

            db.PlayerRankSnapshots.RemoveRange(toDelete);
        }
        // Changes are saved by TickProcessor's SaveChangesAsync call at the end of the tick.
    }
}

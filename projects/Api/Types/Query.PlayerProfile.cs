using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

/// <summary>
/// GraphQL queries for the player achievement badge and rank history features.
/// </summary>
public sealed partial class Query
{
    private const int MaxRankHistorySnapshots = 365;

    /// <summary>
    /// Returns all achievement badges unlocked by the specified player.
    /// This query is public — no authentication required.
    /// </summary>
    public async Task<List<PlayerBadgeResult>> GetPlayerBadges(
        Guid playerId,
        [Service] AppDbContext db)
    {
        var badges = await db.PlayerAchievementBadges
            .AsNoTracking()
            .Where(b => b.PlayerId == playerId)
            .OrderBy(b => b.UnlockedAtUtc)
            .ToListAsync();

        return badges.Select(b => new PlayerBadgeResult
        {
            Id = b.Id,
            PlayerId = b.PlayerId,
            BadgeType = b.BadgeType,
            Rarity = BadgeType.GetRarity(b.BadgeType),
            UnlockCondition = BadgeType.GetUnlockCondition(b.BadgeType),
            UnlockedAtUtc = b.UnlockedAtUtc,
            UnlockedAtTick = b.UnlockedAtTick,
        }).ToList();
    }

    /// <summary>
    /// Returns leaderboard rank snapshots for the specified player.
    /// Snapshots are ordered oldest-first (chronological).
    /// The <paramref name="limit"/> parameter defaults to 365 (1 year of weekly snapshots).
    /// </summary>
    public async Task<List<PlayerRankSnapshotResult>> GetPlayerRankHistory(
        Guid playerId,
        int limit = MaxRankHistorySnapshots,
        [Service] AppDbContext? db = null)
    {
        if (db is null) return [];

        var clamped = Math.Clamp(limit, 1, MaxRankHistorySnapshots);

        var snapshots = await db.PlayerRankSnapshots
            .AsNoTracking()
            .Where(s => s.PlayerId == playerId)
            .OrderByDescending(s => s.SnapshotTick)
            .Take(clamped)
            .ToListAsync();

        // Return in chronological order (oldest first) for chart rendering.
        snapshots.Reverse();

        return snapshots.Select(s => new PlayerRankSnapshotResult
        {
            Id = s.Id,
            PlayerId = s.PlayerId,
            SnapshotTick = s.SnapshotTick,
            SnapshotUtc = s.SnapshotUtc,
            LeaderboardRank = s.LeaderboardRank,
            WealthUsd = s.WealthUsd,
            PercentileRank = s.PercentileRank,
            PositionChange = s.PositionChange,
        }).ToList();
    }

    /// <summary>
    /// Returns rank history records for the selected player and a tick window.
    /// </summary>
    public async Task<List<PlayerRankSnapshotResult>> GetRankHistory(
        Guid playerId,
        int ticksBack = MaxRankHistorySnapshots,
        [Service] AppDbContext? db = null)
    {
        if (db is null) return [];

        var clampedTicksBack = Math.Clamp(ticksBack, 1, MaxRankHistorySnapshots);
        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(gs => gs.CurrentTick)
            .FirstOrDefaultDeterministicAsync();
        var minTick = Math.Max(0, currentTick - clampedTicksBack);

        var snapshots = await db.PlayerRankSnapshots
            .AsNoTracking()
            .Where(s => s.PlayerId == playerId && s.SnapshotTick >= minTick)
            .OrderBy(s => s.SnapshotTick)
            .ToListAsync();

        return snapshots.Select(s => new PlayerRankSnapshotResult
        {
            Id = s.Id,
            PlayerId = s.PlayerId,
            SnapshotTick = s.SnapshotTick,
            SnapshotUtc = s.SnapshotUtc,
            LeaderboardRank = s.LeaderboardRank,
            WealthUsd = s.WealthUsd,
            PercentileRank = s.PercentileRank,
            PositionChange = s.PositionChange,
        }).ToList();
    }
}

// ── Result DTOs ──────────────────────────────────────────────────────────────

/// <summary>A single achievement badge record for a player.</summary>
public sealed class PlayerBadgeResult
{
    /// <summary>Database identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The player who owns this badge.</summary>
    public Guid PlayerId { get; set; }

    /// <summary>The badge type string (e.g. FIRST_MILLION, LEGENDARY_TYCOON).</summary>
    public string BadgeType { get; set; } = string.Empty;

    /// <summary>Rarity tier: COMMON, RARE, EPIC, or LEGENDARY.</summary>
    public string Rarity { get; set; } = string.Empty;

    /// <summary>Human-readable description of the unlock condition.</summary>
    public string UnlockCondition { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the badge was unlocked.</summary>
    public DateTime UnlockedAtUtc { get; set; }

    /// <summary>Game tick when the badge was unlocked.</summary>
    public long UnlockedAtTick { get; set; }
}

/// <summary>A single weekly rank snapshot for a player.</summary>
public sealed class PlayerRankSnapshotResult
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public long SnapshotTick { get; set; }
    public DateTime SnapshotUtc { get; set; }
    public int LeaderboardRank { get; set; }
    public decimal WealthUsd { get; set; }
    public decimal PercentileRank { get; set; }
    public int? PositionChange { get; set; }
}

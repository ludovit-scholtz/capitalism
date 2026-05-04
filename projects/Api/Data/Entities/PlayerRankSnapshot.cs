namespace Api.Data.Entities;

/// <summary>
/// A weekly leaderboard snapshot for a player.
/// Captured by <c>RankHistoryPhase</c> every 1,008 ticks (≈7 game days).
/// At most 365 snapshots are retained per player (≈52 weeks of history).
/// </summary>
public sealed class PlayerRankSnapshot
{
    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    /// <summary>The game tick at which this snapshot was taken.</summary>
    public long SnapshotTick { get; set; }

    /// <summary>The real-time UTC when the snapshot was captured.</summary>
    public DateTime SnapshotUtc { get; set; } = DateTime.UtcNow;

    /// <summary>The player's rank on the global leaderboard (1 = first place).</summary>
    public int LeaderboardRank { get; set; }

    /// <summary>The player's wealth in USD at the time of the snapshot.</summary>
    public decimal WealthUsd { get; set; }

    /// <summary>
    /// Percentile rank (0–100): 100 = top of leaderboard, 0 = bottom.
    /// </summary>
    public decimal PercentileRank { get; set; }

    /// <summary>
    /// Change in rank vs. the previous snapshot (+ve = improved, -ve = declined).
    /// Null for the first snapshot.
    /// </summary>
    public int? PositionChange { get; set; }
}

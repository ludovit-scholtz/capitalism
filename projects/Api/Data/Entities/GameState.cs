using System.ComponentModel.DataAnnotations.Schema;
using Api.Engine;
using Api.Utilities;

namespace Api.Data.Entities;

/// <summary>
/// Tracks the current game tick and global state.
/// Only one row should exist in this table.
/// </summary>
public sealed class GameState
{
    /// <summary>Singleton row identifier (always 1).</summary>
    public int Id { get; set; } = 1;

    /// <summary>Current game tick number. Incremented by the game engine each cycle.</summary>
    public long CurrentTick { get; set; }

    /// <summary>UTC timestamp of the last tick processing.</summary>
    public DateTime LastTickAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Interval in seconds between ticks.</summary>
    public int TickIntervalSeconds { get; set; } = 60;

    /// <summary>Ticks between tax calculation cycles.</summary>
    public int TaxCycleTicks { get; set; } = GameConstants.TicksPerYear;

    /// <summary>Global tax rate percentage (0-100).</summary>
    public decimal TaxRate { get; set; } = 15m;

    /// <summary>UTC timestamp when this game shard started.</summary>
    public DateTime GameStartedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Indicates whether this game shard has ended and is read-only.</summary>
    public bool GameEnded { get; set; }

    /// <summary>Winner player id once <see cref="GameEnded"/> is true.</summary>
    public Guid? WinnerPlayerId { get; set; }

    /// <summary>Winner display name shown in endgame banner and errors.</summary>
    public string? WinnerDisplayName { get; set; }

    /// <summary>Primary winner company name for endgame celebration text.</summary>
    public string? WinnerCompanyName { get; set; }

    /// <summary>UTC timestamp when the winner was declared.</summary>
    public DateTime? GameEndedAtUtc { get; set; }

    /// <summary>Winner net worth in USD at the time the game ended.</summary>
    public decimal? WinnerNetWorth { get; set; }

    /// <summary>Current lifecycle state of this game shard.</summary>
    public GameShardState ShardState { get; set; } = GameShardState.Active;

    [NotMapped]
    public DateTime? ConcludedAtUtc => GameEndedAtUtc;

    [NotMapped]
    public int CurrentGameYear => GameTime.GetGameYear(CurrentTick);

    [NotMapped]
    public DateTime CurrentGameTimeUtc => GameTime.GetInGameTimeUtc(CurrentTick);

    [NotMapped]
    public int TicksPerDay => GameConstants.TicksPerDay;

    [NotMapped]
    public int TicksPerYear => GameConstants.TicksPerYear;

    [NotMapped]
    public long NextTaxTick => GameTime.GetNextTaxTick(CurrentTick, TaxCycleTicks);

    [NotMapped]
    public DateTime NextTaxGameTimeUtc => GameTime.GetInGameTimeUtc(NextTaxTick);

    [NotMapped]
    public int NextTaxGameYear => GameTime.GetGameYear(NextTaxTick);

    /// <summary>
    /// Current game-year quarter index (0=Q1 Jan–Mar, 1=Q2 Apr–Jun,
    /// 2=Q3 Jul–Sep, 3=Q4 Oct–Dec). Derived from the current tick.
    /// </summary>
    [NotMapped]
    public int CurrentQuarter => (int)((CurrentTick / GameConstants.TicksPerQuarter) % 4);

    /// <summary>
    /// Human-readable label for the current quarter, e.g. "Q1" or "Q4".
    /// </summary>
    [NotMapped]
    public string CurrentQuarterLabel => $"Q{CurrentQuarter + 1}";
}

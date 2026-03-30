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
}

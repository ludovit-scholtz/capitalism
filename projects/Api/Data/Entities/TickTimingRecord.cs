using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// The timing breakdown of one processed tick (#646), kept for the last few dozen ticks so the Operations dashboard history survives
/// a restart. Written by <see cref="Engine.TickTimingStore"/> after the tick has committed; losing a row costs history, never game state.
/// </summary>
public sealed class TickTimingRecord
{
    /// <summary>The tick number; one row per tick.</summary>
    public long Tick { get; set; }

    public long TotalMs { get; set; }

    /// <summary>JSON object of phase name to milliseconds, including the context-load and save entries.</summary>
    [Required]
    public string PhaseMsJson { get; set; } = "{}";

    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}

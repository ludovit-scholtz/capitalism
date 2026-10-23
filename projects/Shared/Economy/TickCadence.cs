namespace Shared.Economy;

/// <summary>
/// How often the expensive, non-per-building tick phases run (#646). A cadence changes only when a phase is evaluated, never what
/// it decides: each phase either catches up over the ticks it skipped or only reacts to state that persists.
/// </summary>
public static class TickCadence
{
    /// <summary>City-expansion unlock scan: loads every company, lot and inventory. Unlocks are permanent, so a late scan loses nothing.</summary>
    public const int CityExpansionUnlockTicks = 6;

    /// <summary>
    /// Weather forecast rolling: appends the skipped rows in one run, so the stored forecast (and the horizon players see) is the same
    /// as when it advanced every tick.
    /// </summary>
    public const int WeatherUpdateTicks = 6;

    /// <summary>
    /// Telemetry bounty detection: queries this many ticks of sales and dividends in one run, so an event on a skipped tick is still seen.
    /// Bounties are one per player per UTC day, so a few ticks of delay changes nothing.
    /// </summary>
    public const int TelemetryBountyTicks = 6;

    /// <summary>True when a phase running every <paramref name="cadenceTicks"/> ticks is due on <paramref name="tick"/>.</summary>
    public static bool IsDue(long tick, int cadenceTicks)
        => cadenceTicks <= 1 || tick % cadenceTicks == 0;
}

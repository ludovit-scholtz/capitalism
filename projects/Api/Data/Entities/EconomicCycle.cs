namespace Api.Data.Entities;

/// <summary>
/// Global macroeconomic phase state that drives demand multipliers across the simulation.
/// </summary>
public sealed class EconomicCycle
{
    public Guid Id { get; set; }

    /// <summary>EXPANSION | PEAK | RECESSION | TROUGH</summary>
    public string Phase { get; set; } = EconomicCyclePhase.Expansion;

    /// <summary>Tick when the current phase started.</summary>
    public long PhaseStartedTick { get; set; }

    /// <summary>Expected phase duration in ticks.</summary>
    public int ExpectedDurationTicks { get; set; }

    /// <summary>Demand multiplier intensity for this phase (clamped to [0.5, 1.5]).</summary>
    public decimal IntensityFactor { get; set; } = 1.0m;

    /// <summary>
    /// Recession-start tick for which the 48-tick warning was already sent.
    /// Prevents duplicate notifications every tick.
    /// </summary>
    public long? RecessionWarningSentForTick { get; set; }
}

public static class EconomicCyclePhase
{
    public const string Expansion = "EXPANSION";
    public const string Peak = "PEAK";
    public const string Recession = "RECESSION";
    public const string Trough = "TROUGH";

    public static string Next(string phase) => phase switch
    {
        Expansion => Peak,
        Peak => Recession,
        Recession => Trough,
        _ => Expansion,
    };
}

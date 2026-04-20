namespace Api.Engine;

/// <summary>
/// Lightweight weather reading for the current tick used by <see cref="Phases.PowerDistributionPhase"/>.
/// Pre-loaded by <see cref="TickProcessor"/> into <see cref="TickContext.WeatherByCity"/>.
/// </summary>
public sealed class WeatherSnapshot
{
    /// <summary>Wind strength as a percentage [0, 100].</summary>
    public decimal WindPercent { get; init; }

    /// <summary>Solar irradiance as a percentage [0, 100].</summary>
    public decimal SolarPercent { get; init; }
}

using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Stores a single future-tick weather forecast entry for a city.
/// The rolling 50-tick window is maintained by <see cref="Api.Engine.Phases.WeatherUpdatePhase"/>.
///
/// Wind percent follows a random walk (±2–5% per tick, clamped to [0, 100]).
/// Solar percent follows a deterministic 24-tick sine-wave day/night cycle.
/// </summary>
public sealed class CityWeatherForecast
{
    /// <summary>City this forecast entry belongs to.</summary>
    public Guid CityId { get; set; }

    /// <summary>Navigation property to the city.</summary>
    public City City { get; set; } = null!;

    /// <summary>Tick number this forecast row applies to.</summary>
    public long Tick { get; set; }

    /// <summary>Wind strength as a percentage [0, 100]. Affects WIND plant output.</summary>
    [Range(0, 100)]
    public decimal WindPercent { get; set; }

    /// <summary>Solar irradiance as a percentage [0, 100]. Affects SOLAR plant output.</summary>
    [Range(0, 100)]
    public decimal SolarPercent { get; set; }
}

namespace Api.Types;

/// <summary>A single forecast tick entry for a city's weather.</summary>
public sealed class WeatherTickResult
{
    /// <summary>Game tick this forecast entry applies to.</summary>
    public long Tick { get; set; }

    /// <summary>Wind strength [0–100]. WIND power plant output = base × WindPercent / 100.</summary>
    public decimal WindPercent { get; set; }

    /// <summary>Solar irradiance [0–100]. SOLAR power plant output = base × SolarPercent / 100.</summary>
    public decimal SolarPercent { get; set; }
}

/// <summary>
/// Rolling 50-tick weather forecast for a city.
/// Returned by <c>cityWeatherForecast(cityId)</c>.
/// </summary>
public sealed class CityWeatherForecastResult
{
    /// <summary>City this forecast belongs to.</summary>
    public Guid CityId { get; set; }

    /// <summary>Current-tick wind strength [0–100].</summary>
    public decimal CurrentWindPercent { get; set; }

    /// <summary>Current-tick solar irradiance [0–100].</summary>
    public decimal CurrentSolarPercent { get; set; }

    /// <summary>Ordered list of forecast entries (up to 50 ticks).</summary>
    public List<WeatherTickResult> Forecast { get; set; } = [];
}

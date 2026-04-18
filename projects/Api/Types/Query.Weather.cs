using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Returns the rolling 50-tick weather forecast for the given city.
    /// Public query — no authentication required.
    ///
    /// SOLAR and WIND power plant output is multiplied by the corresponding
    /// percentage value divided by 100 each tick.  Players can use this forecast
    /// to evaluate renewable plant profitability and plan contract commitments.
    /// </summary>
    public async Task<CityWeatherForecastResult?> GetCityWeatherForecast(
        Guid cityId,
        [Service] AppDbContext db)
    {
        var rows = await db.CityWeatherForecasts
            .Where(f => f.CityId == cityId)
            .OrderBy(f => f.Tick)
            .Take(50)
            .AsNoTracking()
            .ToListAsync();

        if (rows.Count == 0) return null;

        var first = rows[0];
        return new CityWeatherForecastResult
        {
            CityId = cityId,
            CurrentWindPercent = first.WindPercent,
            CurrentSolarPercent = first.SolarPercent,
            Forecast = rows.Select(r => new WeatherTickResult
            {
                Tick = r.Tick,
                WindPercent = r.WindPercent,
                SolarPercent = r.SolarPercent,
            }).ToList(),
        };
    }
}

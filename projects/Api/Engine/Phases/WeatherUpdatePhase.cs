using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine.Phases;

/// <summary>
/// Advances the rolling 50-tick weather forecast for every city each tick.
///
/// Runs at order 5, before <see cref="PowerDistributionPhase"/> (order 10),
/// so that the updated <see cref="TickContext.WeatherByCity"/> snapshot is already in memory
/// when effective power output is computed for SOLAR and WIND plants.
/// </summary>
public sealed class WeatherUpdatePhase : ITickPhase
{
    public string Name => "WeatherUpdate";

    /// <summary>Must run before <see cref="PowerDistributionPhase"/> (order 10).</summary>
    public int Order => 5;

    public async Task ProcessAsync(TickContext context)
    {
        var cityIds = context.CitiesById.Keys.ToList();
        await WeatherService.AdvanceForecastsAsync(context.Db, cityIds, context.CurrentTick);

        // Refresh the in-memory snapshot after advancing so PowerDistributionPhase
        // uses the current tick's values.
        var refreshed = await WeatherService.LoadCurrentSnapshotsAsync(
            context.Db, cityIds, context.CurrentTick);

        foreach (var (cityId, snapshot) in refreshed)
            context.WeatherByCity[cityId] = snapshot;
    }
}

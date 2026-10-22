using Api.Data;
using Shared.Economy;

namespace Api.Engine.Phases;

/// <summary>
/// Advances the rolling 50-tick weather forecast for every city each tick.
///
/// Runs at order 5, before <see cref="PowerDistributionPhase"/> (order 10),
/// so that the updated <see cref="TickContext.WeatherByCity"/> snapshot is already in memory
/// when effective power output is computed for SOLAR and WIND plants.
///
/// Runs every <see cref="TickCadence.WeatherUpdateTicks"/> ticks (#646) and appends the skipped rows in one go, so the forecast table and the
/// horizon players see are the same as with a per-tick roll. <see cref="TickProcessor"/> already loads the current tick's snapshot before the
/// phases, so power output on a skipped tick reads the right weather without this phase touching the database.
/// </summary>
public sealed class WeatherUpdatePhase : ITickPhase
{
    public string Name => "WeatherUpdate";

    /// <summary>Must run before <see cref="PowerDistributionPhase"/> (order 10).</summary>
    public int Order => 5;

    public async Task ProcessAsync(TickContext context)
    {
        if (!TickCadence.IsDue(context.CurrentTick, TickCadence.WeatherUpdateTicks))
        {
            return;
        }

        var cityIds = context.CitiesById.Keys.ToList();
        await WeatherService.AdvanceForecastsAsync(context.Db, cityIds, context.CurrentTick, TickCadence.WeatherUpdateTicks);

        // Refresh the in-memory snapshot after advancing so PowerDistributionPhase
        // uses the current tick's values.
        var refreshed = await WeatherService.LoadCurrentSnapshotsAsync(
            context.Db, cityIds, context.CurrentTick);

        foreach (var (cityId, snapshot) in refreshed)
            context.WeatherByCity[cityId] = snapshot;
    }
}

using Api.Data.Entities;

namespace Api.Engine.Phases;

/// <summary>
/// Computes the city-level power balance each tick and updates each building's
/// <see cref="Building.PowerStatus"/> accordingly.
///
/// Decision rationale: power is balanced at city level (not company level) so that
/// players understand the city grid as a shared resource. A company that builds more
/// power plants benefits the whole city but must still compete for prime lots.
///
/// Balance rules (per city, applied once per tick before production phases):
///   - Supply = sum of each plant's effective output (base + unit boosts,
///             scaled by weather for SOLAR/WIND, gated on fuel reserve for COAL/GAS)
///             × dispatch target + BATTERY_STORAGE smoothing buffer.
///   - Demand = sum of PowerConsumption across all non-power-plant buildings in the city.
///   - If supply >= demand:          all consumer buildings -> POWERED.
///   - If supply >= 50% of demand:   all consumer buildings -> CONSTRAINED.
///   - If supply < 50% of demand:    all consumer buildings -> OFFLINE.
///
/// Power plants themselves are always POWERED (they produce electricity, not consume it).
///
/// This phase stores per-plant effective output in
/// <see cref="TickContext.PlantEffectiveOutputMwById"/> so that
/// <see cref="PowerGridEconomicsPhase"/> can use the authoritative values without
/// needing to re-evaluate fuel reserves (which are consumed here).
/// </summary>
public sealed class PowerDistributionPhase : ITickPhase
{
    public string Name => "PowerDistribution";

    /// Runs after <see cref="FuelProcurementPhase"/> (order 9) and before all production phases.
    public int Order => 10;

    public Task ProcessAsync(TickContext context)
    {
        var buildingsByCity = context.BuildingsById.Values
            .GroupBy(b => b.CityId);

        foreach (var cityGroup in buildingsByCity)
        {
            var buildings = cityGroup.ToList();

            var powerPlants = buildings
                .Where(b => b.Type == BuildingType.PowerPlant)
                .ToList();

            var consumers = buildings
                .Where(b => b.Type != BuildingType.PowerPlant)
                .ToList();

            if (powerPlants.Count == 0)
            {
                foreach (var consumer in consumers)
                    consumer.PowerStatus = PowerStatus.Powered;
                continue;
            }

            var cityId = cityGroup.Key;
            context.WeatherByCity.TryGetValue(cityId, out var weather);

            var totalSupplyMw = 0m;
            foreach (var plant in powerPlants)
            {
                var units = context.UnitsByBuilding.GetValueOrDefault(plant.Id);
                var outputMw = PowerPlantOutputCalculator.ComputeAndConsumeFuel(plant, units, weather);
                context.PlantEffectiveOutputMwById[plant.Id] = outputMw;
                totalSupplyMw += outputMw;
            }

            var totalDemandMw = consumers.Sum(b => b.PowerConsumption);

            foreach (var plant in powerPlants)
                plant.PowerStatus = PowerStatus.Powered;

            if (totalDemandMw <= 0m || totalSupplyMw >= totalDemandMw)
            {
                foreach (var consumer in consumers)
                    consumer.PowerStatus = PowerStatus.Powered;
                continue;
            }

            var remainingSupply = totalSupplyMw;
            var orderedConsumers = consumers
                .OrderByDescending(b => b.PowerPriority)
                .ThenByDescending(b => b.PowerConsumption)
                .ThenBy(b => b.Id)
                .ToList();

            foreach (var consumer in orderedConsumers)
            {
                if (remainingSupply <= 0m)
                {
                    consumer.PowerStatus = PowerStatus.Offline;
                    continue;
                }

                if (remainingSupply >= consumer.PowerConsumption)
                {
                    consumer.PowerStatus = PowerStatus.Powered;
                    remainingSupply -= consumer.PowerConsumption;
                    continue;
                }

                consumer.PowerStatus = PowerStatus.Constrained;
                remainingSupply = 0m;
            }
        }

        return Task.CompletedTask;
    }
}

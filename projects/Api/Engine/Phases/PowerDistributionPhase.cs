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
///   - Supply = sum of each plant's effective output (base + POWER_GENERATION unit boosts,
///             scaled by weather for SOLAR/WIND) + BATTERY_STORAGE smoothing buffer.
///   - Demand = sum of PowerConsumption across all non-power-plant buildings in the city.
///   - If supply &gt;= demand:          all consumer buildings → POWERED.
///   - If supply &gt;= 50% of demand:   all consumer buildings → CONSTRAINED.
///   - If supply &lt; 50% of demand:    all consumer buildings → OFFLINE.
///
/// Power plants themselves are always POWERED (they produce electricity, not consume it).
/// </summary>
public sealed class PowerDistributionPhase : ITickPhase
{
    public string Name => "PowerDistribution";

    /// <summary>
    /// Runs before all production phases so that manufacturing, mining, and sales
    /// can read the updated PowerStatus without needing to recalculate it.
    /// </summary>
    public int Order => 10;

    public Task ProcessAsync(TickContext context)
    {
        // Group all buildings by city.
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

            // If there are no power plants in this city yet, buildings operate on
            // municipal / legacy power and are always considered POWERED.
            // Power constraints only apply once at least one power plant exists.
            if (powerPlants.Count == 0)
            {
                foreach (var consumer in consumers)
                {
                    consumer.PowerStatus = PowerStatus.Powered;
                }
                continue;
            }

            // Total supply from all power plants in this city.
            // SOLAR and WIND plants have their output scaled by the current weather factor.
            // POWER_GENERATION units boost the rated output; BATTERY_STORAGE adds smoothing.
            var cityId = cityGroup.Key;
            context.WeatherByCity.TryGetValue(cityId, out var weather);

            var totalRawSupplyMw = powerPlants.Sum(plant =>
            {
                var baseOutput = plant.PowerOutput > 0m
                    ? plant.PowerOutput.Value
                    : GameConstants.DefaultPowerOutputMw(plant.PowerPlantType);

                // POWER_GENERATION unit boosts are tied to the plant's rated capacity and
                // are therefore weather-scaled alongside the base output for SOLAR/WIND plants.
                if (context.UnitsByBuilding.TryGetValue(plant.Id, out var plantUnits))
                {
                    baseOutput += plantUnits
                        .Where(u => u.UnitType == UnitType.PowerGeneration)
                        .Sum(u => GameConstants.PowerGenerationUnitBoostMwPerLevel * u.Level);
                }

                var factor = plant.PowerPlantType switch
                {
                    Data.Entities.PowerPlantType.Solar => weather is not null ? weather.SolarPercent / 100m : 1m,
                    Data.Entities.PowerPlantType.Wind  => weather is not null ? weather.WindPercent  / 100m : 1m,
                    _                                  => 1m,
                };

                // Weather scaling applies only to the plant's rated capacity and POWER_GENERATION boosts.
                var weatherScaledOutput = baseOutput * factor;

                if (context.UnitsByBuilding.TryGetValue(plant.Id, out var allPlantUnits))
                {
                    // FUEL_PURCHASE units expand fuel (coal/gas) procurement contracts.
                    // Thermal fuel supply is weather-independent: always adds the full MW capacity.
                    weatherScaledOutput += allPlantUnits
                        .Where(u => u.UnitType == UnitType.FuelPurchase)
                        .Sum(u => GameConstants.FuelPurchaseBoostMwPerLevel * u.Level);

                    // WATER_TURBINE units generate steady hydro-electric power independent of any
                    // plant-level weather factor — always adds the full rated hydro MW.
                    weatherScaledOutput += allPlantUnits
                        .Where(u => u.UnitType == UnitType.WaterTurbine)
                        .Sum(u => GameConstants.WaterTurbineBoostMwPerLevel * u.Level);

                    // ENERGY_PRODUCING units represent the main conversion stage (fuel/force →
                    // electricity). Their output is not driven by plant-level solar/wind factors.
                    weatherScaledOutput += allPlantUnits
                        .Where(u => u.UnitType == UnitType.EnergyProducing)
                        .Sum(u => GameConstants.EnergyProducingBoostMwPerLevel * u.Level);

                    // WIND_TURBINE units harvest wind energy and always scale by current wind
                    // percentage regardless of the plant's primary fuel type.
                    var windPercent = weather is not null ? weather.WindPercent / 100m : 0.5m;
                    weatherScaledOutput += allPlantUnits
                        .Where(u => u.UnitType == UnitType.WindTurbine)
                        .Sum(u => GameConstants.WindTurbineBoostMwPerLevel * u.Level * windPercent);
                }

                return weatherScaledOutput;
            });

            // BATTERY_STORAGE and ENERGY_STORAGE units add smoothing buffer to effective supply,
            // reducing exposure to constrained/offline transitions during partial shortages.
            var batteryBufferMw = powerPlants.Sum(plant =>
            {
                if (!context.UnitsByBuilding.TryGetValue(plant.Id, out var plantUnits))
                    return 0m;
                return plantUnits
                    .Where(u => u.UnitType == UnitType.BatteryStorage)
                    .Sum(u => GameConstants.BatterySmoothingMwPerLevel * u.Level)
                    + plantUnits
                    .Where(u => u.UnitType == UnitType.EnergyStorage)
                    .Sum(u => GameConstants.EnergyStorageSmoothingMwPerLevel * u.Level);
            });

            var totalEffectiveSupplyMw = totalRawSupplyMw + batteryBufferMw;

            // Total demand from all consuming buildings in this city.
            // PowerConsumption == 0 means the building predates the power system
            // and operates without explicit power requirements (legacy/grandfathered).
            var totalDemandMw = consumers.Sum(building => building.PowerConsumption);

            // Determine city-wide power status.
            string cityStatus;
            if (totalDemandMw == 0m || totalEffectiveSupplyMw >= totalDemandMw)
            {
                cityStatus = PowerStatus.Powered;
            }
            else if (totalEffectiveSupplyMw >= totalDemandMw * 0.5m)
            {
                cityStatus = PowerStatus.Constrained;
            }
            else
            {
                cityStatus = PowerStatus.Offline;
            }

            // Apply status: power plants are always POWERED.
            foreach (var plant in powerPlants)
            {
                plant.PowerStatus = PowerStatus.Powered;
            }

            // All consumers share the same city-level status in this first slice.
            foreach (var consumer in consumers)
            {
                consumer.PowerStatus = cityStatus;
            }
        }

        return Task.CompletedTask;
    }
}

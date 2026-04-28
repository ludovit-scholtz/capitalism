using Api.Data.Entities;

namespace Api.Engine.Phases;

/// <summary>
/// Shared helper for computing the effective MW output of a power plant in a given tick.
/// Called by both <see cref="PowerDistributionPhase"/> and consumed via
/// <see cref="TickContext.PlantEffectiveOutputMwById"/> in <see cref="PowerGridEconomicsPhase"/>.
///
/// Output chain for thermal (COAL/GAS) plants:
/// <list type="number">
///   <item>Base rated output + POWER_GENERATION unit boosts (weather-scaled).</item>
///   <item>FUEL_PURCHASE units: draw from <see cref="Building.FuelReserveMwh"/> first
///         (reserve filled by <see cref="FuelProcurementPhase"/>).</item>
///   <item>ENERGY_PRODUCING units: draw remaining reserve.</item>
///   <item>WATER_TURBINE, WIND_TURBINE, ENERGY_STORAGE: weather-independent additions.</item>
///   <item>Total scaled by <see cref="Building.DispatchTargetPercent"/>.</item>
/// </list>
///
/// For renewable/nuclear plants FUEL_PURCHASE and ENERGY_PRODUCING are flat MW boosts
/// (no fuel reserve required).
/// </summary>
public static class PowerPlantOutputCalculator
{
    /// <summary>
    /// Computes the effective output for a power plant and updates its fuel reserve.
    /// </summary>
    /// <param name="plant">The power-plant building.</param>
    /// <param name="units">All units installed in the plant (may be null if none configured).</param>
    /// <param name="weather">Current city weather (null uses safe fallback values).</param>
    /// <returns>Effective output in MW after dispatch scaling.</returns>
    public static decimal ComputeAndConsumeFuel(
        Building plant,
        IReadOnlyList<BuildingUnit>? units,
        WeatherSnapshot? weather)
    {
        var baseOutput = plant.PowerOutput > 0m
            ? plant.PowerOutput.Value
            : GameConstants.DefaultPowerOutputMw(plant.PowerPlantType);

        var powerGenBoost = units?
            .Where(u => u.UnitType == UnitType.PowerGeneration)
            .Sum(u => GameConstants.PowerGenerationUnitBoostMwPerLevel * u.Level) ?? 0m;

        var batterySmoothing = units?
            .Where(u => u.UnitType == UnitType.BatteryStorage)
            .Sum(u => GameConstants.BatterySmoothingMwPerLevel * u.Level) ?? 0m;

        // Weather factor: SOLAR and WIND scale base output + POWER_GENERATION + BATTERY boosts.
        var factor = plant.PowerPlantType switch
        {
            PowerPlantType.Solar => weather is not null ? weather.SolarPercent / 100m : 1m,
            PowerPlantType.Wind  => weather is not null ? weather.WindPercent  / 100m : 1m,
            _                    => 1m,
        };

        var weatherScaled = (baseOutput + powerGenBoost + batterySmoothing) * factor;

        // Weather-independent contributions:
        var waterTurbineMw = units?
            .Where(u => u.UnitType == UnitType.WaterTurbine)
            .Sum(u => GameConstants.WaterTurbineBoostMwPerLevel * u.Level) ?? 0m;

        var windPercent = weather is not null ? weather.WindPercent / 100m : 0.5m;
        var windTurbineMw = units?
            .Where(u => u.UnitType == UnitType.WindTurbine)
            .Sum(u => GameConstants.WindTurbineBoostMwPerLevel * u.Level * windPercent) ?? 0m;

        var energyStorageMw = units?
            .Where(u => u.UnitType == UnitType.EnergyStorage)
            .Sum(u => GameConstants.EnergyStorageSmoothingMwPerLevel * u.Level) ?? 0m;

        // Fuel-gated units:
        decimal fuelPurchaseMw;
        decimal energyProducingMw;

        if (GameConstants.IsThermalPlant(plant.PowerPlantType))
        {
            var fuelPurchaseCap = units?
                .Where(u => u.UnitType == UnitType.FuelPurchase)
                .Sum(u => GameConstants.FuelPurchaseBoostMwPerLevel * u.Level) ?? 0m;

            var energyProducingCap = units?
                .Where(u => u.UnitType == UnitType.EnergyProducing)
                .Sum(u => GameConstants.EnergyProducingBoostMwPerLevel * u.Level) ?? 0m;

            // Allocate fuel from reserve: FUEL_PURCHASE takes first, ENERGY_PRODUCING takes the rest.
            var reserveAvailable = plant.FuelReserveMwh;
            fuelPurchaseMw = Math.Min(fuelPurchaseCap, reserveAvailable);
            reserveAvailable -= fuelPurchaseMw;
            energyProducingMw = Math.Min(energyProducingCap, reserveAvailable);

            // Consume the fuel used this tick.
            plant.FuelReserveMwh = Math.Max(0m, plant.FuelReserveMwh - fuelPurchaseMw - energyProducingMw);
        }
        else
        {
            // Non-thermal: flat boosts, no fuel consumption.
            fuelPurchaseMw = units?
                .Where(u => u.UnitType == UnitType.FuelPurchase)
                .Sum(u => GameConstants.FuelPurchaseBoostMwPerLevel * u.Level) ?? 0m;

            energyProducingMw = units?
                .Where(u => u.UnitType == UnitType.EnergyProducing)
                .Sum(u => GameConstants.EnergyProducingBoostMwPerLevel * u.Level) ?? 0m;
        }

        var rawOutput = weatherScaled + waterTurbineMw + windTurbineMw
            + energyStorageMw + fuelPurchaseMw + energyProducingMw;

        // Apply dispatch target: player throttles output to save fuel or respond to market conditions.
        var dispatchFactor = Math.Clamp(plant.DispatchTargetPercent, 0, 100) / 100m;
        return rawOutput * dispatchFactor;
    }
}

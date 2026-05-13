using Api.Data.Entities;

namespace Api.Utilities;

/// <summary>
/// Pricing helpers for queued building configuration changes.
/// </summary>
public static class BuildingConfigurationEconomics
{
    public static decimal GetUnitConstructionCost(string unitType)
    {
        return unitType switch
        {
            UnitType.Mining => 9_000m,
            UnitType.Storage => 3_500m,
            UnitType.B2BSales => 5_000m,
            UnitType.Purchase => 4_500m,
            UnitType.Manufacturing => 12_000m,
            UnitType.Branding => 7_000m,
            UnitType.Marketing => 5_500m,
            UnitType.PublicSales => 6_000m,
            UnitType.ProductQuality => 8_500m,
            UnitType.BrandQuality => 8_500m,
            UnitType.PowerGeneration => 15_000m,
            UnitType.BatteryStorage => 12_000m,
            UnitType.FuelPurchase => 12_000m,
            UnitType.WindTurbine => 18_000m,
            UnitType.WaterTurbine => 25_000m,
            UnitType.EnergyStorage => 14_000m,
            UnitType.EnergyProducing => 22_000m,
            _ => 0m,
        };
    }

    public static decimal CalculateActivationCost(BuildingUnit? activeUnit, BuildingConfigurationPlanUnit pendingUnit)
    {
        if (!pendingUnit.IsChanged)
        {
            return 0m;
        }

        if (activeUnit is null)
        {
            return GetUnitConstructionCost(pendingUnit.UnitType);
        }

        return string.Equals(activeUnit.UnitType, pendingUnit.UnitType, StringComparison.Ordinal)
            ? 0m
            : GetUnitConstructionCost(pendingUnit.UnitType);
    }
}
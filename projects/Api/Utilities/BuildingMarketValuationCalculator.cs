using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Microsoft.EntityFrameworkCore;

namespace Api.Utilities;

public sealed class BuildingMarketValuation
{
    public decimal LandValue { get; init; }
    public decimal StructureValue { get; init; }
    public decimal UnitsValue { get; init; }
    public decimal TotalValue { get; init; }
    public decimal MinimumSalePrice { get; init; }
    public string CurrencyCode { get; init; } = "EUR";
}

public static class BuildingMarketValuationCalculator
{
    public const decimal MinimumSalePriceFactor = 0.70m;

    public static async Task<BuildingMarketValuation> CalculateAsync(
        AppDbContext db,
        Building building,
        CancellationToken cancellationToken = default)
    {
        var currencyCode = await db.Cities
            .AsNoTracking()
            .Where(c => c.Id == building.CityId)
            .Select(c => c.CurrencyCode)
            .FirstOrDefaultAsync(cancellationToken)
            ?? "EUR";

        var fxRates = await FxRateHelper.BuildEurRatesLookupAsync(db, [currencyCode]);
        var cityFxRate = FxRateHelper.GetEurRate(fxRates, currencyCode);

        var landValue = await GetLandValueAsync(db, building, cancellationToken);

        var structureValue = RoundCurrency(
            GetCurrentStructureReplacementCostEur(building) * cityFxRate);

        var units = await db.BuildingUnits
            .AsNoTracking()
            .Where(unit => unit.BuildingId == building.Id)
            .Select(unit => new { unit.UnitType, unit.Level })
            .ToListAsync(cancellationToken);

        var unitsValue = RoundCurrency(
            units.Sum(unit => GetCurrentUnitReplacementCostEur(unit.UnitType, unit.Level)) * cityFxRate);

        var totalValue = RoundCurrency(landValue + structureValue + unitsValue);
        var minimumSalePrice = RoundCurrency(totalValue * MinimumSalePriceFactor);

        return new BuildingMarketValuation
        {
            LandValue = landValue,
            StructureValue = structureValue,
            UnitsValue = unitsValue,
            TotalValue = totalValue,
            MinimumSalePrice = minimumSalePrice,
            CurrencyCode = currencyCode,
        };
    }

    private static async Task<decimal> GetLandValueAsync(
        AppDbContext db,
        Building building,
        CancellationToken cancellationToken)
    {
        var recordedPurchaseCost = await db.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.BuildingId == building.Id && entry.Category == LedgerCategory.PropertyPurchase)
            .OrderBy(entry => entry.RecordedAtTick)
            .ThenBy(entry => entry.RecordedAtUtc)
            .ThenBy(entry => entry.Id)
            .Select(entry => (decimal?)(entry.Amount < 0m ? -entry.Amount : entry.Amount))
            .FirstOrDefaultAsync(cancellationToken);

        if (recordedPurchaseCost is > 0m)
        {
            return RoundCurrency(recordedPurchaseCost.Value);
        }

        var fallbackLotPrice = await db.BuildingLots
            .AsNoTracking()
            .Where(lot => lot.BuildingId == building.Id)
            .Select(lot => lot.Price)
            .FirstOrDefaultAsync(cancellationToken);

        return RoundCurrency(Math.Max(fallbackLotPrice, 0m));
    }

    private static decimal GetCurrentStructureReplacementCostEur(Building building)
    {
        var currentLevel = Math.Max(1, building.Level);
        var replacementCost = GameConstants.ConstructionCost(building.Type);

        if (string.Equals(building.Type, BuildingType.MediaHouse, StringComparison.OrdinalIgnoreCase))
        {
            for (var level = 1; level < currentLevel; level++)
            {
                replacementCost += GameConstants.MediaHouseUpgradeCost(level);
            }
        }

        return replacementCost;
    }

    private static decimal GetCurrentUnitReplacementCostEur(string unitType, int level)
    {
        var currentLevel = Math.Max(1, level);
        var replacementCost = BuildingConfigurationEconomics.GetUnitConstructionCost(unitType);

        if (currentLevel > 1 && GameConstants.IsUpgradableUnitType(unitType))
        {
            for (var upgradeFromLevel = 1; upgradeFromLevel < currentLevel; upgradeFromLevel++)
            {
                replacementCost += GameConstants.UnitUpgradeCost(unitType, upgradeFromLevel);
            }
        }

        return replacementCost;
    }

    private static decimal RoundCurrency(decimal amount)
    {
        return decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }
}

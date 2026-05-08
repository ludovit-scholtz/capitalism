using Api.Data;
using Api.Data.Entities;
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
    public const decimal UnitBaseValue = 20_000m;
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

        var landValue = await db.BuildingLots
            .AsNoTracking()
            .Where(lot => lot.BuildingId == building.Id)
            .Select(lot => lot.Price)
            .FirstOrDefaultAsync(cancellationToken);
        landValue = Math.Max(landValue, 0m);

        var structureValue = decimal.Round(
            WealthCalculator.GetBuildingValue(building) * cityFxRate,
            2,
            MidpointRounding.AwayFromZero);

        var unitLevels = await db.BuildingUnits
            .AsNoTracking()
            .Where(unit => unit.BuildingId == building.Id)
            .Select(unit => unit.Level)
            .ToListAsync(cancellationToken);

        var unitValueEur = unitLevels.Sum(level => Math.Max(level, 1) * UnitBaseValue);
        var unitsValue = decimal.Round(unitValueEur * cityFxRate, 2, MidpointRounding.AwayFromZero);

        var totalValue = decimal.Round(landValue + structureValue + unitsValue, 2, MidpointRounding.AwayFromZero);
        var minimumSalePrice = decimal.Round(totalValue * MinimumSalePriceFactor, 2, MidpointRounding.AwayFromZero);

        return new BuildingMarketValuation
        {
            LandValue = decimal.Round(landValue, 2, MidpointRounding.AwayFromZero),
            StructureValue = structureValue,
            UnitsValue = unitsValue,
            TotalValue = totalValue,
            MinimumSalePrice = minimumSalePrice,
            CurrencyCode = currencyCode,
        };
    }
}

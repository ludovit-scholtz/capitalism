using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Microsoft.EntityFrameworkCore;

namespace Api.Utilities;

public sealed class GovernmentContractEligibility
{
    public bool IsEligible { get; set; }
    public string? ReasonCode { get; set; }
    public string? ReasonMessage { get; set; }
    public decimal CurrentQualityLevel { get; set; }
}

public static class GovernmentContractService
{
    public static async Task EnsureOpenContractsPerCityAsync(AppDbContext db, long currentTick, CancellationToken cancellationToken)
    {
        var cities = await db.Cities.AsNoTracking().ToListAsync(cancellationToken);
        if (cities.Count == 0)
        {
            return;
        }

        var productTypes = await db.ProductTypes
            .AsNoTracking()
            .Where(product => !product.IsProOnly)
            .ToListAsync(cancellationToken);
        if (productTypes.Count == 0)
        {
            return;
        }

        var openCountByCity = await db.GovernmentContracts
            .Where(contract => contract.Status == GovernmentContractStatus.Open)
            .GroupBy(contract => contract.CityId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);

        foreach (var city in cities)
        {
            var openCount = openCountByCity.GetValueOrDefault(city.Id);
            for (var i = openCount; i < GameConstants.MinimumOpenGovernmentContractsPerCity; i++)
            {
                db.GovernmentContracts.Add(CreateContract(city, productTypes, currentTick, sequence: i));
            }
        }
    }

    public static GovernmentContract CreateContract(City city, IReadOnlyList<ProductType> productTypes, long currentTick, int sequence = 0)
    {
        var seed = HashCode.Combine(city.Id, currentTick, sequence);
        var random = new Random(seed);
        var product = productTypes[random.Next(productTypes.Count)];
        var quantityRequired = decimal.Round(100m + random.Next(0, 16) * 50m, 0, MidpointRounding.AwayFromZero);
        var minimumQuality = decimal.Round(2m + (decimal)random.NextDouble() * 6m, 1, MidpointRounding.AwayFromZero);
        var baseUnitBudget = Math.Max(1m, product.BasePrice * (1.05m + (decimal)random.NextDouble() * 0.35m));
        var deadlineTick = currentTick + GameConstants.TicksPerDay * (2 + random.Next(0, 4));

        return new GovernmentContract
        {
            Id = Guid.NewGuid(),
            CityId = city.Id,
            Title = $"{product.Name} public procurement",
            Description = $"City of {city.Name} requests {quantityRequired:N0} {product.UnitName} of {product.Name} for public infrastructure and services.",
            ProductTypeId = product.Id,
            QuantityRequired = quantityRequired,
            MinimumQuality = Math.Clamp(minimumQuality, 0m, 10m),
            BudgetCap = decimal.Round(baseUnitBudget, 2, MidpointRounding.AwayFromZero),
            DeadlineTick = deadlineTick,
            Status = GovernmentContractStatus.Open,
            CreatedAtTick = currentTick,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    public static async Task<GovernmentContractEligibility> EvaluateCompanyEligibilityAsync(
        AppDbContext db,
        GovernmentContract contract,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var hasRequiredBuilding = await db.Buildings
            .AsNoTracking()
            .AnyAsync(
                building => building.CompanyId == companyId
                    && building.CityId == contract.CityId
                    && building.DestroyedAtUtc == null
                    && (building.Type == BuildingType.Factory || building.Type == BuildingType.SalesShop),
                cancellationToken);

        if (!hasRequiredBuilding)
        {
            return new GovernmentContractEligibility
            {
                IsEligible = false,
                ReasonCode = "MISSING_CITY_OPERATION",
                ReasonMessage = "Company must own a Factory or Sales Shop in the contract city.",
                CurrentQualityLevel = 0m,
            };
        }

        var currentQualityLevel = await ComputeCompanyProductQualityLevelAsync(db, companyId, contract.ProductTypeId, cancellationToken);
        if (currentQualityLevel < contract.MinimumQuality)
        {
            return new GovernmentContractEligibility
            {
                IsEligible = false,
                ReasonCode = "QUALITY_TOO_LOW",
                ReasonMessage = $"Company quality {currentQualityLevel:F1} is below required {contract.MinimumQuality:F1}.",
                CurrentQualityLevel = currentQualityLevel,
            };
        }

        return new GovernmentContractEligibility
        {
            IsEligible = true,
            CurrentQualityLevel = currentQualityLevel,
        };
    }

    public static async Task<decimal> ComputeCompanyProductQualityLevelAsync(
        AppDbContext db,
        Guid companyId,
        Guid productTypeId,
        CancellationToken cancellationToken)
    {
        var brand = await db.Brands
            .AsNoTracking()
            .Where(candidate =>
                candidate.CompanyId == companyId
                && candidate.Scope == BrandScope.Product
                && candidate.ProductTypeId == productTypeId)
            .FirstOrDefaultAsync(cancellationToken);

        var rdQuality = Math.Clamp(brand?.Quality ?? 0m, 0m, 1m);
        var marketingQuality = Math.Clamp(brand?.MarketingQuality ?? 0m, 0m, 1m);
        var combinedQuality = Math.Clamp(1m - (1m - rdQuality) * (1m - marketingQuality), 0m, 1m);
        return decimal.Round(combinedQuality * 10m, 1, MidpointRounding.AwayFromZero);
    }
}

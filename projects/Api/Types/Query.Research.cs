using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Returns the research profile for a specific product type for the given company.
    /// Shows the current quality level (0–1), accumulated research budget in USD,
    /// the target budget for 100% quality, and the top competitor budget.
    /// Requires auth and company ownership — returns null for foreign or missing companies.
    /// </summary>
    [Authorize]
    public async Task<ProductQualityProfile?> GetProductQualityProfile(
        Guid companyId,
        Guid productTypeId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId && c.PlayerId == userId);

        if (company is null)
            return null;

        var productType = await db.ProductTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(pt => pt.Id == productTypeId);

        if (productType is null)
            return null;

        // Load R&D brand for this company + product (PRODUCT scope)
        var brand = await db.Brands
            .AsNoTracking()
            .Where(b => b.CompanyId == companyId
                        && b.ProductTypeId == productTypeId
                        && b.Scope == BrandScope.Product)
            .FirstOrDefaultAsync();

        // Load this company's research budget for the product
        var ownBudget = await db.ProductResearchBudgets
            .AsNoTracking()
            .Where(rb => rb.CompanyId == companyId && rb.ProductTypeId == productTypeId)
            .FirstOrDefaultAsync();

        // Load the max competitor budget across all companies for competitive context
        var maxCompetitorBudget = await db.ProductResearchBudgets
            .AsNoTracking()
            .Where(rb => rb.ProductTypeId == productTypeId)
            .MaxAsync(rb => (decimal?)rb.AccumulatedBudget);

        // Load EUR→USD FX rate to display budgets in USD
        var fxRates = await Utilities.FxRateHelper.BuildEurRatesLookupAsync(db, ["USD"]);
        var usdEurRate = Utilities.FxRateHelper.GetEurRate(fxRates, "USD");

        var rdQuality = Math.Clamp(brand?.Quality ?? 0m, 0m, 1m);
        var mktQuality = Math.Clamp(brand?.MarketingQuality ?? 0m, 0m, 1m);
        var combinedQuality = Math.Clamp(1m - (1m - rdQuality) * (1m - mktQuality), 0m, 1m);

        // Display quality level on 0–10 scale (brandQuality 0–1 → qualityLevel 0–10)
        var qualityLevel = Math.Round(combinedQuality * 10m, 1);
        var rdQualityLevel = Math.Round(rdQuality * 10m, 1);

        var accBudgetUsd = ownBudget?.AccumulatedBudget;
        var baseBudgetUsd = GameConstants.ResearchBaseQualityBudget(productType.BasePrice) * usdEurRate;

        // Next level threshold: quality advances in 0.1 increments; next is the ceiling above current
        var currentFraction = rdQuality;
        var nextLevelFraction = Math.Round(Math.Ceiling(currentFraction * 10m) / 10m, 1);
        if (nextLevelFraction <= currentFraction) nextLevelFraction = Math.Min(1m, currentFraction + 0.1m);

        // Estimate ticks to next level based on recent budget accumulation (simplified heuristic)
        decimal? ticksToNextLevel = null;
        if (accBudgetUsd.HasValue && accBudgetUsd.Value > 0m && baseBudgetUsd > 0m)
        {
            var budgetFractionNeeded = nextLevelFraction - currentFraction;
            var budgetNeeded = budgetFractionNeeded * baseBudgetUsd;
            // Research budget grows by a few % per tick at level 1; approximate as constant rate
            // for display purposes. Exact rate depends on unit level and city FX.
            ticksToNextLevel = budgetNeeded > 0m ? (decimal?)Math.Round((double)budgetNeeded / (double)(accBudgetUsd.Value * 0.01m), 0) : null;
        }

        return new ProductQualityProfile
        {
            CompanyId = companyId,
            ProductTypeId = productTypeId,
            ProductName = productType.Name,
            Industry = productType.Industry,
            RdQuality = rdQuality,
            MarketingQuality = mktQuality,
            CombinedQuality = combinedQuality,
            QualityLevel = qualityLevel,
            RdQualityLevel = rdQualityLevel,
            AccumulatedResearchBudgetUsd = accBudgetUsd,
            BaseResearchBudgetUsd = baseBudgetUsd,
            MaxCompetitorBudgetUsd = maxCompetitorBudget,
            MarketingEfficiencyMultiplier = brand?.MarketingEfficiencyMultiplier ?? 1m,
            QualityPricePremiumPct = Math.Round(GameConstants.QualityPricePremiumRate * combinedQuality * 100m, 1),
            TicksToNextLevel = ticksToNextLevel,
        };
    }

    /// <summary>
    /// Returns a summary of all R&amp;D and brand research for the given company across all
    /// products and categories. This is the Company Research Dashboard data source.
    /// Requires auth and company ownership — returns empty list for foreign or missing companies.
    /// </summary>
    [Authorize]
    public async Task<BrandQualityOverview> GetBrandQualityOverview(
        Guid companyId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId && c.PlayerId == userId);

        if (company is null)
            return new BrandQualityOverview { CompanyId = companyId, Brands = [], TotalResearchBudgetUsd = 0m };

        var brands = await db.Brands
            .AsNoTracking()
            .Where(b => b.CompanyId == companyId)
            .ToListAsync();

        var productTypeIds = brands
            .Where(b => b.ProductTypeId.HasValue)
            .Select(b => b.ProductTypeId!.Value)
            .Distinct()
            .ToList();

        var productTypes = productTypeIds.Count > 0
            ? await db.ProductTypes
                .AsNoTracking()
                .Where(pt => productTypeIds.Contains(pt.Id))
                .ToDictionaryAsync(pt => pt.Id)
            : new Dictionary<Guid, ProductType>();

        var ownBudgets = await db.ProductResearchBudgets
            .AsNoTracking()
            .Where(rb => rb.CompanyId == companyId && rb.AccumulatedBudget > 0m)
            .ToListAsync();

        var ownBudgetMap = ownBudgets.ToDictionary(rb => rb.ProductTypeId);

        var allProductIds = productTypeIds
            .Concat(ownBudgets.Select(rb => rb.ProductTypeId))
            .Distinct()
            .ToList();

        var maxBudgetByProduct = allProductIds.Count > 0
            ? await db.ProductResearchBudgets
                .AsNoTracking()
                .Where(rb => allProductIds.Contains(rb.ProductTypeId))
                .GroupBy(rb => rb.ProductTypeId)
                .Select(g => new { ProductTypeId = g.Key, MaxBudget = g.Max(rb => rb.AccumulatedBudget) })
                .ToDictionaryAsync(x => x.ProductTypeId, x => x.MaxBudget)
            : new Dictionary<Guid, decimal>();

        var fxRates = await Utilities.FxRateHelper.BuildEurRatesLookupAsync(db, ["USD"]);
        var usdEurRate = Utilities.FxRateHelper.GetEurRate(fxRates, "USD");

        var brandStates = brands.Select(b =>
        {
            var pt = b.ProductTypeId.HasValue ? productTypes.GetValueOrDefault(b.ProductTypeId.Value) : null;

            decimal? accBudget = b.ProductTypeId.HasValue
                ? ownBudgetMap.TryGetValue(b.ProductTypeId.Value, out var rb) ? rb.AccumulatedBudget : null
                : null;

            decimal? baseBudget = pt is not null
                ? GameConstants.ResearchBaseQualityBudget(pt.BasePrice) * usdEurRate
                : null;

            decimal? maxBudget = b.ProductTypeId.HasValue
                ? maxBudgetByProduct.TryGetValue(b.ProductTypeId.Value, out var mb) ? mb : null
                : null;

            var rdQuality = Math.Clamp(b.Quality, 0m, 1m);
            var mktQuality = Math.Clamp(b.MarketingQuality, 0m, 1m);
            var combined = Math.Clamp(1m - (1m - rdQuality) * (1m - mktQuality), 0m, 1m);

            return new ResearchBrandState
            {
                Id = b.Id,
                CompanyId = b.CompanyId,
                Name = b.Name,
                Scope = b.Scope,
                ProductTypeId = b.ProductTypeId,
                ProductName = pt?.Name,
                IndustryCategory = b.IndustryCategory,
                Awareness = b.Awareness,
                Quality = rdQuality,
                MarketingQuality = mktQuality,
                CombinedBrandQuality = combined,
                MarketingEfficiencyMultiplier = b.MarketingEfficiencyMultiplier,
                AccumulatedResearchBudget = accBudget,
                BaseResearchBudget = baseBudget,
                MaxCompetitorBudget = maxBudget,
            };
        }).ToList();

        // Add synthetic entries for budgets without a brand (edge case: research ticked, brand delayed)
        var brandedIds = brands.Where(b => b.ProductTypeId.HasValue).Select(b => b.ProductTypeId!.Value).ToHashSet();
        foreach (var budget in ownBudgets.Where(rb => !brandedIds.Contains(rb.ProductTypeId)))
        {
            if (!productTypes.TryGetValue(budget.ProductTypeId, out var pt))
                continue;

            var baseBudget = GameConstants.ResearchBaseQualityBudget(pt.BasePrice) * usdEurRate;
            var maxBudget = maxBudgetByProduct.TryGetValue(budget.ProductTypeId, out var mb) ? mb : budget.AccumulatedBudget;
            brandStates.Add(new ResearchBrandState
            {
                Id = budget.Id,
                CompanyId = companyId,
                Name = pt.Name,
                Scope = BrandScope.Product,
                ProductTypeId = budget.ProductTypeId,
                ProductName = pt.Name,
                AccumulatedResearchBudget = budget.AccumulatedBudget,
                BaseResearchBudget = baseBudget,
                MaxCompetitorBudget = maxBudget,
            });
        }

        var totalBudget = ownBudgets.Sum(b => b.AccumulatedBudget);

        return new BrandQualityOverview
        {
            CompanyId = companyId,
            Brands = brandStates,
            TotalResearchBudgetUsd = totalBudget,
        };
    }

    /// <summary>
    /// Returns per-unit R&amp;D research progress for all PRODUCT_QUALITY and BRAND_QUALITY units
    /// in the specified building. Shows which product/brand is being researched and the current
    /// quality level. Requires auth and building ownership.
    /// </summary>
    [Authorize]
    public async Task<List<BuildingUnitResearchProgress>> GetBuildingResearchProgress(
        Guid buildingId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .AsNoTracking()
            .Include(b => b.Company)
            .FirstOrDefaultAsync(b => b.Id == buildingId);

        if (building is null || building.Company.PlayerId != userId)
            return [];

        var rdUnitTypes = new[] { UnitType.ProductQuality, UnitType.BrandQuality };
        var units = await db.BuildingUnits
            .AsNoTracking()
            .Where(u => u.BuildingId == buildingId && rdUnitTypes.Contains(u.UnitType))
            .ToListAsync();

        if (units.Count == 0)
            return [];

        // Collect all product types and brands referenced by the units
        var productTypeIds = units
            .Where(u => u.ProductTypeId.HasValue)
            .Select(u => u.ProductTypeId!.Value)
            .Distinct()
            .ToList();

        var productTypes = productTypeIds.Count > 0
            ? await db.ProductTypes
                .AsNoTracking()
                .Where(pt => productTypeIds.Contains(pt.Id))
                .ToDictionaryAsync(pt => pt.Id)
            : new Dictionary<Guid, ProductType>();

        // Find the brand(s) that match this company and product type for each unit
        var brands = productTypeIds.Count > 0
            ? await db.Brands
                .AsNoTracking()
                .Where(b => b.CompanyId == building.CompanyId
                            && productTypeIds.Contains(b.ProductTypeId ?? Guid.Empty))
                .ToDictionaryAsync(b => b.ProductTypeId ?? Guid.Empty)
            : new Dictionary<Guid, Brand>();

        // Also find company-scoped brands for BRAND_QUALITY units without a specific product
        var companyBrand = await db.Brands
            .AsNoTracking()
            .Where(b => b.CompanyId == building.CompanyId && b.Scope == BrandScope.Company)
            .FirstOrDefaultAsync();

        var results = new List<BuildingUnitResearchProgress>();
        foreach (var unit in units)
        {
            Brand? brand = null;
            ProductType? pt = null;

            if (unit.ProductTypeId.HasValue)
            {
                brands.TryGetValue(unit.ProductTypeId.Value, out brand);
                productTypes.TryGetValue(unit.ProductTypeId.Value, out pt);
            }
            else if (unit.UnitType == UnitType.BrandQuality)
            {
                brand = companyBrand;
            }

            var rdQuality = Math.Clamp(brand?.Quality ?? 0m, 0m, 1m);
            var mktQuality = Math.Clamp(brand?.MarketingQuality ?? 0m, 0m, 1m);
            var combined = Math.Clamp(1m - (1m - rdQuality) * (1m - mktQuality), 0m, 1m);

            // Progress within the current 0.1-step level (fraction toward the next 0.1 increment)
            var currentLevelInt = (int)(rdQuality * 10m);
            var currentLevelFraction = (rdQuality * 10m) - currentLevelInt;
            var progressToNextLevel = Math.Round(currentLevelFraction * 100m, 1); // 0–100%

            results.Add(new BuildingUnitResearchProgress
            {
                UnitId = unit.Id,
                UnitType = unit.UnitType,
                GridX = unit.GridX,
                GridY = unit.GridY,
                Level = unit.Level,
                ProductTypeId = unit.ProductTypeId,
                ProductName = pt?.Name,
                IndustryCategory = unit.IndustryCategory,
                CurrentQualityLevel = Math.Round(rdQuality * 10m, 1),
                CombinedQualityLevel = Math.Round(combined * 10m, 1),
                ProgressToNextLevelPct = progressToNextLevel,
                MarketingEfficiencyMultiplier = brand?.MarketingEfficiencyMultiplier ?? 1m,
                QualityPricePremiumPct = Math.Round(GameConstants.QualityPricePremiumRate * combined * 100m, 1),
            });
        }

        return results;
    }
}

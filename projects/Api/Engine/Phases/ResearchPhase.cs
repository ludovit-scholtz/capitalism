using Api.Data.Entities;
using Api.Utilities;

namespace Api.Engine.Phases;

/// <summary>
/// Processes R&amp;D building units: PRODUCT_QUALITY and BRAND_QUALITY.
///
/// PRODUCT_QUALITY — cumulative research budget model (ROADMAP):
///   Each tick a fraction of the unit's operating cost is added to an accumulated research
///   budget per (company, product).  The conversion fraction is level-dependent:
///   L1 = 50%, L2 = 66.7%, L3 = 75%, L4 = 80% (formula: 1 - 1/(level+1)).
///   All budgets decay by 0.1% per tick (GameConstants.ResearchDecayRate).
///   Product quality is derived as myBudget / max(maxCompetitorBudget, baseQualityBudget)
///   where baseQualityBudget is the spending needed for 100% quality when uncontested.
///   Units whose upgrade is in progress contribute at half-cost (ROADMAP).
///
/// BRAND_QUALITY — raises the marketing efficiency multiplier on scope-matching brands.
///   This means marketing budget becomes more effective; it does NOT directly grant awareness.
/// </summary>
public sealed class ResearchPhase : ITickPhase
{
    public string Name => "Research";
    public int Order => 800;

    public Task ProcessAsync(TickContext context)
    {
        // Step 1: accumulate research budgets from active PRODUCT_QUALITY / BRAND_QUALITY units.
        if (context.BuildingsByType.TryGetValue(BuildingType.ResearchDevelopment, out var rdBuildings))
        {
            foreach (var building in rdBuildings)
            {
                if (!context.UnitsByBuilding.TryGetValue(building.Id, out var units))
                    continue;

                foreach (var unit in units)
                {
                    switch (unit.UnitType)
                    {
                        case UnitType.ProductQuality:
                            AccumulateProductResearchBudget(context, building, unit);
                            break;
                        case UnitType.BrandQuality:
                            ProcessBrandQuality(context, building, unit);
                            break;
                    }
                }
            }
        }

        // Step 2: apply 0.1% decay to ALL accumulated research budgets (including idle ones).
        ApplyResearchDecay(context);

        // Step 3: recompute brand quality from relative budget positions across all companies.
        // This runs every tick even when no R&D buildings are active, so that existing accumulated
        // budgets continue to drive brand quality as they decay toward zero.
        UpdateProductBrandQualityFromBudgets(context);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Accumulates research budget for a PRODUCT_QUALITY unit.
    /// Budget gain = unitOperatingCost × ResearchBudgetConversionRate(level).
    /// While the unit is under upgrade the operating cost is halved (ROADMAP).
    /// </summary>
    private static void AccumulateProductResearchBudget(
        TickContext context, Building building, BuildingUnit unit)
    {
        if (!unit.ProductTypeId.HasValue) return;
        if (!context.ProductTypesById.ContainsKey(unit.ProductTypeId.Value)) return;

        if (!context.CompaniesById.TryGetValue(building.CompanyId, out var company)) return;
        if (!context.CitiesById.TryGetValue(building.CityId, out var city)) return;

        var powerEfficiency = TickContext.GetPowerEfficiency(building);
        if (powerEfficiency <= 0m) return;

        // Compute operating cost for this unit using the same formula as OperatingCostPhase.
        var salarySettings = context.CitySalarySettingsByCompany.GetValueOrDefault(company.Id, []);
        var salaryMultiplier = CompanyEconomyCalculator.GetSalaryMultiplier(salarySettings, city.Id);
        var hourlyWage = CompanyEconomyCalculator.GetEffectiveHourlyWage(city, salaryMultiplier);

        var baseLaborHours = CompanyEconomyCalculator.GetBaseUnitLaborHours(unit.UnitType, unit.Level) * powerEfficiency;
        var baseEnergyMwh = CompanyEconomyCalculator.GetBaseUnitEnergyMwh(unit.UnitType, unit.Level) * powerEfficiency;

        var laborCost = decimal.Round(baseLaborHours * hourlyWage, 2, MidpointRounding.AwayFromZero);
        var energyCost = decimal.Round(baseEnergyMwh * GameConstants.EnergyPricePerMwh, 2, MidpointRounding.AwayFromZero);
        var totalCost = laborCost + energyCost;

        // While upgrading, the unit operates at half cost (ROADMAP spec).
        if (context.UnitsUnderUpgrade.Contains(unit.Id))
            totalCost *= 0.5m;

        if (totalCost <= 0m) return;

        var conversionRate = GameConstants.ResearchBudgetConversionRate(unit.Level);
        var budgetGain = decimal.Round(totalCost * conversionRate, 4, MidpointRounding.AwayFromZero);

        var researchBudget = context.GetOrCreateResearchBudget(building.CompanyId, unit.ProductTypeId.Value);
        researchBudget.AccumulatedBudget += budgetGain;
    }

    /// <summary>
    /// Applies 0.1% decay per tick to every accumulated research budget in the game.
    /// Decayed budgets are floored at zero.
    /// </summary>
    private static void ApplyResearchDecay(TickContext context)
    {
        foreach (var budget in context.ResearchBudgetsByKey.Values)
        {
            if (budget.AccumulatedBudget <= 0m) continue;
            var decay = decimal.Round(budget.AccumulatedBudget * GameConstants.ResearchDecayRate, 4, MidpointRounding.AwayFromZero);
            budget.AccumulatedBudget = Math.Max(0m, budget.AccumulatedBudget - decay);
        }
    }

    /// <summary>
    /// Derives product brand quality from relative research budget positions.
    /// For each product: quality = myBudget / max(maxBudgetAcrossAllCompanies, baseQualityBudget).
    /// A lone researcher with budget >= baseQualityBudget gets 100% quality.
    /// The top competitor always anchors at quality 1.0 when above baseline.
    /// </summary>
    private static void UpdateProductBrandQualityFromBudgets(TickContext context)
    {
        // Group all research budgets by product.
        var budgetsByProduct = context.ResearchBudgetsByKey.Values
            .Where(rb => rb.AccumulatedBudget > 0m)
            .GroupBy(rb => rb.ProductTypeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (productTypeId, budgets) in budgetsByProduct)
        {
            if (!context.ProductTypesById.TryGetValue(productTypeId, out var productType))
                continue;

            var baseQualityBudget = GameConstants.ResearchBaseQualityBudget(productType.BasePrice);
            var maxBudget = budgets.Max(b => b.AccumulatedBudget);
            var denominator = Math.Max(maxBudget, baseQualityBudget);

            foreach (var budget in budgets)
            {
                var quality = Math.Min(1m, decimal.Round(budget.AccumulatedBudget / denominator, 4, MidpointRounding.AwayFromZero));
                var brand = context.GetOrCreateBrand(budget.CompanyId, productTypeId, productType.Name);
                brand.Quality = quality;
            }
        }
    }

    private static void ProcessBrandQuality(TickContext context, Building building, BuildingUnit unit)
    {
        var rate = GameConstants.ResearchEfficiencyRate(unit.Level);

        // Determine scope from unit configuration.
        var scope = unit.BrandScope ?? BrandScope.Company;
        var selectedProductType = unit.ProductTypeId.HasValue && context.ProductTypesById.TryGetValue(unit.ProductTypeId.Value, out var productType)
            ? productType
            : null;

        if (scope is BrandScope.Product or BrandScope.Category && selectedProductType is null)
        {
            return;
        }

        // BRAND_QUALITY research raises the marketing efficiency multiplier on scope-matching brands.
        // This means marketing budget becomes more effective — it does NOT directly grant awareness.
        var brand = scope switch
        {
            BrandScope.Product when selectedProductType is not null =>
                context.GetOrCreateBrand(building.CompanyId, selectedProductType.Id, selectedProductType.Name),
            BrandScope.Category when selectedProductType is not null =>
                context.GetOrCreateCategoryBrand(building.CompanyId, selectedProductType.Industry),
            _ =>
                context.GetOrCreateCompanyBrand(building.CompanyId)
        };

        brand.MarketingEfficiencyMultiplier = Math.Min(
            GameConstants.MaxMarketingEfficiencyMultiplier,
            brand.MarketingEfficiencyMultiplier + rate);
    }
}

using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>Gets the current player's companies with their buildings.</summary>
    [Authorize]
    public async Task<List<Company>> GetMyCompanies(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync();
        if (gameState is not null)
        {
            await BuildingConfigurationService.ApplyDuePlansAsync(db, gameState.CurrentTick);
            await db.SaveChangesAsync();
        }

        return await db.Companies
            .Include(c => c.Buildings)
            .ThenInclude(b => b.Units)
            .Include(c => c.Buildings)
            .ThenInclude(b => b.PendingConfiguration)
            .ThenInclude(plan => plan!.Units)
            .Include(c => c.Buildings)
            .ThenInclude(b => b.PendingConfiguration)
            .ThenInclude(plan => plan!.Removals)
            .AsSplitQuery()
            .Where(c => c.PlayerId == userId)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Returns the brand research state for a company — all brands accumulated by R&amp;D and marketing
    /// so the frontend can show current product-quality and brand-awareness progress.
    /// Requires auth and company ownership.
    /// </summary>
    [Authorize]
    public async Task<List<ResearchBrandState>> GetCompanyBrands(
        Guid companyId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var company = await db.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId && c.PlayerId == userId);
        if (company is null)
            return [];

        var brands = await db.Brands
            .Where(b => b.CompanyId == companyId)
            .ToListAsync();

        var productTypeIds = brands
            .Where(b => b.ProductTypeId.HasValue)
            .Select(b => b.ProductTypeId!.Value)
            .Distinct()
            .ToList();

        var productTypes = await db.ProductTypes
            .Where(pt => productTypeIds.Contains(pt.Id))
            .ToDictionaryAsync(pt => pt.Id);

        // Load research budgets for this company (for budget data in UI)
        // Also loads budgets whose brands don't yet exist (covers first-tick edge case).
        var allOwnBudgets = await db.ProductResearchBudgets
            .Where(rb => rb.CompanyId == companyId && rb.AccumulatedBudget > 0m)
            .ToListAsync();

        var ownResearchBudgets = allOwnBudgets.ToDictionary(rb => rb.ProductTypeId);

        // Add product types from budgets that don't have a brand yet (defensive)
        var extraProductTypeIds = allOwnBudgets
            .Select(rb => rb.ProductTypeId)
            .Where(id => !productTypeIds.Contains(id))
            .Distinct()
            .ToList();

        Dictionary<Guid, ProductType> allProductTypes;
        if (extraProductTypeIds.Count > 0)
        {
            var extraProductTypes = await db.ProductTypes
                .Where(pt => extraProductTypeIds.Contains(pt.Id))
                .ToDictionaryAsync(pt => pt.Id);
            allProductTypes = productTypes.Concat(extraProductTypes).ToDictionary(kv => kv.Key, kv => kv.Value);
        }
        else
        {
            allProductTypes = productTypes;
        }

        // Load max budget per product across all companies (for competitive context)
        var allBudgetProductIds = productTypeIds.Concat(extraProductTypeIds).Distinct().ToList();
        var maxBudgetPerProduct = allBudgetProductIds.Count > 0
            ? await db.ProductResearchBudgets
                .Where(rb => allBudgetProductIds.Contains(rb.ProductTypeId))
                .GroupBy(rb => rb.ProductTypeId)
                .Select(g => new { ProductTypeId = g.Key, MaxBudget = g.Max(rb => rb.AccumulatedBudget) })
                .ToDictionaryAsync(x => x.ProductTypeId, x => x.MaxBudget)
            : new Dictionary<Guid, decimal>();

        // Load EUR→USD rate to convert the EUR-denominated baseQualityBudget to USD.
        // AccumulatedBudget and MaxCompetitorBudget are already stored in USD by the tick engine.
        var fxRates = await Utilities.FxRateHelper.BuildEurRatesLookupAsync(db, ["USD"]);
        var usdEurRate = Utilities.FxRateHelper.GetEurRate(fxRates, "USD");

        var results = brands.Select(b =>
        {
            var pt = b.ProductTypeId.HasValue
                ? allProductTypes.GetValueOrDefault(b.ProductTypeId.Value)
                : null;

            decimal? accBudget = null;
            decimal? baseBudget = null;
            decimal? maxBudget = null;
            if (b.ProductTypeId.HasValue && pt is not null)
            {
                accBudget = ownResearchBudgets.TryGetValue(b.ProductTypeId.Value, out var rb) ? rb.AccumulatedBudget : null;
                // baseResearchBudget is converted from EUR to USD so it matches the USD-denominated AccumulatedBudget.
                baseBudget = Engine.GameConstants.ResearchBaseQualityBudget(pt.BasePrice) * usdEurRate;
                maxBudget = maxBudgetPerProduct.TryGetValue(b.ProductTypeId.Value, out var mb) ? mb : null;
            }

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
                Quality = b.Quality,
                MarketingQuality = mktQuality,
                CombinedBrandQuality = combined,
                MarketingEfficiencyMultiplier = b.MarketingEfficiencyMultiplier,
                AccumulatedResearchBudget = accBudget,
                BaseResearchBudget = baseBudget,
                MaxCompetitorBudget = maxBudget,
            };
        }).ToList();

        // Add synthetic entries for research budgets that accumulated but no brand exists yet.
        // This covers the edge case where research ticked but brand creation was delayed.
        var brandedProductIds = brands.Where(b => b.ProductTypeId.HasValue).Select(b => b.ProductTypeId!.Value).ToHashSet();
        foreach (var budget in allOwnBudgets.Where(rb => !brandedProductIds.Contains(rb.ProductTypeId)))
        {
            if (!allProductTypes.TryGetValue(budget.ProductTypeId, out var pt))
                continue;

            var baseBudget = Engine.GameConstants.ResearchBaseQualityBudget(pt.BasePrice) * usdEurRate;
            var maxBudget = maxBudgetPerProduct.TryGetValue(budget.ProductTypeId, out var mb) ? mb : budget.AccumulatedBudget;

            results.Add(new ResearchBrandState
            {
                Id = budget.Id,
                CompanyId = budget.CompanyId,
                Name = pt.Name,
                Scope = BrandScope.Product,
                ProductTypeId = budget.ProductTypeId,
                ProductName = pt.Name,
                IndustryCategory = null,
                Awareness = 0m,
                Quality = 0m,
                MarketingQuality = 0m,
                CombinedBrandQuality = 0m,
                MarketingEfficiencyMultiplier = 1m,
                AccumulatedResearchBudget = budget.AccumulatedBudget,
                BaseResearchBudget = baseBudget,
                MaxCompetitorBudget = maxBudget,
            });
        }

        return results;
    }

    /// <summary>Returns owner-editable company settings including salary levels per city.</summary>
    [Authorize]
    public async Task<CompanySettingsResult?> GetCompanySettings(
        Guid companyId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var company = await db.Companies
            .Include(candidate => candidate.CitySalarySettings)
            .Include(candidate => candidate.Buildings)
            .ThenInclude(building => building.City)
            .FirstOrDefaultAsync(candidate => candidate.Id == companyId && candidate.PlayerId == userId);

        if (company is null)
        {
            return null;
        }

        var cities = await db.Cities
            .OrderBy(city => city.Name)
            .ToListAsync();
        var allCompanies = await db.Companies
            .Include(candidate => candidate.BankAccounts)
            .Include(candidate => candidate.Buildings)
            .ToListAsync();
        var allOwnedLots = await db.BuildingLots
            .Where(lot => lot.OwnerCompanyId.HasValue)
            .ToListAsync();
        var companyBuildingIds = allCompanies
            .SelectMany(candidate => candidate.Buildings)
            .Select(building => building.Id)
            .ToList();
        var allInventories = await db.Inventories
            .Where(inventory => companyBuildingIds.Contains(inventory.BuildingId))
            .Include(inventory => inventory.ResourceType)
            .Include(inventory => inventory.ProductType)
            .ToListAsync();

        var companyAssetValues = allCompanies.ToDictionary(
            candidate => candidate.Id,
            candidate => ComputeCompanyAssetValue(candidate, allOwnedLots, allInventories));
        var assetValue = companyAssetValues.GetValueOrDefault(company.Id);
        var currentTick = await db.GameStates.AsNoTracking().Select(state => state.CurrentTick).FirstOrDefaultDeterministicAsync();
        var maxAssetValue = companyAssetValues.Values.DefaultIfEmpty(0m).Max();
        var overheadRate = CompanyEconomyCalculator.ComputeAdministrationOverheadRate(
            company,
            assetValue,
            maxAssetValue,
            currentTick);
        var (ageFactor, assetFactor) = CompanyEconomyCalculator.ComputeAdministrationOverheadDrivers(
            company,
            assetValue,
            maxAssetValue,
            currentTick);

        return new CompanySettingsResult
        {
            CompanyId = company.Id,
            CompanyName = company.Name,
            Cash = CompanyBankingService.GetTotalBalance(company),
            TotalSharesIssued = company.TotalSharesIssued,
            DividendPayoutRatio = company.DividendPayoutRatio,
            FoundedAtTick = company.FoundedAtTick,
            AdministrationOverheadRate = overheadRate,
            AgeFactor = ageFactor,
            AssetFactor = assetFactor,
            AssetValue = assetValue,
            CurrencyCode = ResolvePrimaryCurrencyCode(company),
            CitySalarySettings = cities
                .Select(city =>
                {
                    var multiplier = CompanyEconomyCalculator.GetSalaryMultiplier(company.CitySalarySettings, city.Id);
                    return new CompanyCitySalarySettingResult
                    {
                        CityId = city.Id,
                        CityName = city.Name,
                        CurrencyCode = city.CurrencyCode,
                        BaseSalaryPerManhour = city.BaseSalaryPerManhour,
                        SalaryMultiplier = multiplier,
                        EffectiveSalaryPerManhour = CompanyEconomyCalculator.GetEffectiveHourlyWage(city, multiplier),
                    };
                })
                .ToList(),
        };
    }

    /// <summary>Gets the current game state (tick, tax info).</summary>
    public async Task<GameState?> GetGameState([Service] AppDbContext db, [Service] IMemoryCache cache)
    {
        // Use a very short cache to reduce DB reads when multiple panels request game state
        // simultaneously or in rapid succession (e.g. dashboard + home page on navigation).
        const string key = "gameState_singleton";
        if (cache.TryGetValue(key, out GameState? cached) && cached is not null)
        {
            return cached;
        }

        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync();
        if (gameState is null)
        {
            return null;
        }

        await BuildingConfigurationService.ApplyDuePlansAsync(db, gameState.CurrentTick);
        await db.SaveChangesAsync();

        cache.Set(key, gameState, TimeSpan.FromSeconds(8));
        return gameState;
    }

    /// <summary>Gets available starter industries for onboarding.</summary>
    public StarterIndustriesPayload GetStarterIndustries()
    {
        return new StarterIndustriesPayload
        {
            Industries = Industry.StarterIndustries.ToList(),
            ProOnlyIndustries = Industry.ProOnlyStarterIndustries.ToList()
        };
    }

    /// <summary>
    /// Returns eligibility details for the "Launch Additional Company" IPO flow.
    /// Shows which prerequisites are met and which still need to be fulfilled.
    /// Requires authentication.
    /// </summary>
    [Authorize]
    public async Task<AdditionalCompanyPrerequisites> GetAdditionalCompanyPrerequisites(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var ct = httpContextAccessor.HttpContext!.RequestAborted;

        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync();
        var currentTick = gameState?.CurrentTick ?? 0L;

        var playerCompanies = await db.Companies
            .Where(c => c.PlayerId == userId)
            .ToListAsync(ct);

        var companyCount = playerCompanies.Count;
        const int maxPlayerCompanies = 5;
        var underMaxCap = companyCount < maxPlayerCompanies;
        var hasExistingCompany = companyCount > 0;

        long companyAgeTicks = 0;
        long ticksUntilAgeRequirementMet = Mutation.MinCompanyAgeTicksConst;
        bool companyAgeRequirementMet = false;
        decimal netIncomeInWindow = 0m;
        bool profitabilityRequirementMet = false;

        if (hasExistingCompany)
        {
            var firstCompany = playerCompanies.OrderBy(c => c.FoundedAtTick).First();
            companyAgeTicks = currentTick - firstCompany.FoundedAtTick;
            companyAgeRequirementMet = companyAgeTicks >= Mutation.MinCompanyAgeTicksConst;
            ticksUntilAgeRequirementMet = companyAgeRequirementMet ? 0L : Mutation.MinCompanyAgeTicksConst - companyAgeTicks;

            var windowStart = currentTick - Mutation.ProfitabilityWindowTicksConst;
            netIncomeInWindow = await db.LedgerEntries
                .Where(e => e.CompanyId == firstCompany.Id && e.RecordedAtTick >= windowStart)
                .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
            profitabilityRequirementMet = netIncomeInWindow > 0m;
        }

        var personalBalance = await PersonalBankAccountService.GetTrackedBalanceAsync(db, userId, "USD", ct);
        const decimal founderContributionRequired = 200_000m;
        var balanceRequirementMet = personalBalance >= founderContributionRequired;

        return new AdditionalCompanyPrerequisites
        {
            CompanyCount = companyCount,
            UnderMaxCap = underMaxCap,
            HasExistingCompany = hasExistingCompany,
            CompanyAgeTicks = companyAgeTicks,
            CompanyAgeRequirementMet = companyAgeRequirementMet,
            TicksUntilAgeRequirementMet = ticksUntilAgeRequirementMet,
            NetIncomeInWindow = netIncomeInWindow,
            ProfitabilityRequirementMet = profitabilityRequirementMet,
            PersonalBalanceUsd = personalBalance,
            BalanceRequirementMet = balanceRequirementMet,
            AllRequirementsMet = underMaxCap && hasExistingCompany && companyAgeRequirementMet && profitabilityRequirementMet && balanceRequirementMet,
        };
    }
}

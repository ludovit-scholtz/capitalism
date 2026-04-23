using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Api.Types;

/// <summary>
/// Player/company rankings, company management, and game-state queries.
/// Methods: GetRankings, GetCompanyRankings, GetMyCompanies, GetCompanyBrands,
///          GetCompanySettings, GetGameState, GetStarterIndustries.
/// </summary>
public sealed partial class Query
{
    /// <summary>
    /// Gets the player ranking (leaderboard) sorted by total wealth.
    ///
    /// Wealth formula for players: Personal cash + value of owned shares.
    /// Wealth formula for companies: Cash + BuildingValue + InventoryValue (unchanged).
    /// TotalWealthUsd normalizes all local-currency values to USD for cross-city fairness.
    /// </summary>
    public async Task<List<PlayerRanking>> GetRankings([Service] AppDbContext db)
    {
        var players = await db.Players
            .Where(p => p.Role != PlayerRole.Admin)
            .ToListAsync();

        // Load all companies, buildings, lots, inventories, and shareholdings for share price calculation
        var companies = await db.Companies.ToListAsync();
        var buildings = await db.Buildings
            .Include(b => b.City)
            .ToListAsync();
        var personalCashByPlayerId = await PersonalBankAccountService.GetSettlementBalancesByPlayerIdAsync(
            db,
            players.Select(player => player.Id));
        var lots = await db.BuildingLots
            .Where(l => l.OwnerCompanyId.HasValue)
            .ToListAsync();
        var inventories = await db.Inventories
            .Include(i => i.ResourceType)
            .Include(i => i.ProductType)
            .ToListAsync();
        var shareholdings = await db.Shareholdings.ToListAsync();

        var sharePriceByCompany = BuildQuotedSharePriceLookup(companies, buildings, lots, inventories, shareholdings);
        var companyCurrencyCodeById = companies.ToDictionary(
            company => company.Id,
            company => ResolvePrimaryCurrencyCode(company.Id, buildings));

        // Load FX rates once for USD normalization.
        // All stored rates are EUR-based (1 EUR = Rate units). EUR→USD = UsdRate.
        var usdRate = await GetEurToUsdRateAsync(db);
        // Company cash currency → EUR rate lookup (EUR per 1 unit of company currency = 1/EurRate)
        var companyCurrencies = companyCurrencyCodeById.Values.Distinct().ToList();
        var eurRatesByCode = await BuildEurRatesLookupAsync(db, companyCurrencies);

        // Compute per-company share price in USD (share price is denominated in company currency).
        var sharePriceUsdByCompany = companies.ToDictionary(
            c => c.Id,
            c =>
            {
                var localPrice = sharePriceByCompany.GetValueOrDefault(c.Id);
                var currencyCode = companyCurrencyCodeById.GetValueOrDefault(c.Id, "EUR");
                return ConvertToUsd(localPrice, currencyCode, eurRatesByCode, usdRate);
            });

        return players
            .Select(p =>
            {
                var personalCash = PersonalBankAccountService.GetGrossCash(p, personalCashByPlayerId);
                var sharesValue = shareholdings
                    .Where(sh => sh.OwnerPlayerId == p.Id && sh.ShareCount > 0m)
                    .Sum(sh => decimal.Round(
                        sh.ShareCount * sharePriceByCompany.GetValueOrDefault(sh.CompanyId),
                        4,
                        MidpointRounding.AwayFromZero));

                // Normalize to USD: PersonalCash is always EUR; shares use per-company USD share prices.
                var personalCashUsd = Math.Round(personalCash * usdRate, 4);
                var sharesValueUsd = shareholdings
                    .Where(sh => sh.OwnerPlayerId == p.Id && sh.ShareCount > 0m)
                    .Sum(sh => decimal.Round(
                        sh.ShareCount * sharePriceUsdByCompany.GetValueOrDefault(sh.CompanyId),
                        4,
                        MidpointRounding.AwayFromZero));

                return new PlayerRanking
                {
                    PlayerId = p.Id,
                    DisplayName = p.DisplayName,
                    PersonalCash = personalCash,
                    SharesValue = sharesValue,
                    TotalWealth = decimal.Round(personalCash + sharesValue, 4, MidpointRounding.AwayFromZero),
                    TotalWealthUsd = decimal.Round(personalCashUsd + sharesValueUsd, 4, MidpointRounding.AwayFromZero),
                    CompanyCount = companies.Count(c => c.PlayerId == p.Id)
                };
            })
            .OrderByDescending(r => r.TotalWealthUsd)
            .ToList();
    }

    /// <summary>Returns per-company wealth rankings for the leaderboard, normalized to USD.</summary>
    public async Task<List<CompanyRanking>> GetCompanyRankings([Service] AppDbContext db)
    {
        var companies = await db.Companies
            .Include(c => c.Buildings)
            .ThenInclude(b => b.Units)
            .Include(c => c.Buildings)
            .ThenInclude(b => b.City)
            .Include(c => c.Player)
            .Where(c => c.Player != null && c.Player.Role != PlayerRole.Admin)
            .AsSplitQuery()
            .ToListAsync();

        var buildingIds = companies
            .SelectMany(c => c.Buildings)
            .Select(b => b.Id)
            .ToList();

        var inventories = await db.Inventories
            .Where(i => buildingIds.Contains(i.BuildingId))
            .Include(i => i.ResourceType)
            .Include(i => i.ProductType)
            .ToListAsync();

        var inventoryByBuilding = inventories
            .GroupBy(i => i.BuildingId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Load FX rates once for USD normalization.
        var usdRate = await GetEurToUsdRateAsync(db);
        var companyCurrencyCodeById = companies.ToDictionary(
            company => company.Id,
            company => ResolvePrimaryCurrencyCode(company));
        var companyCurrencies = companyCurrencyCodeById.Values.Distinct().ToList();
        var eurRatesByCode = await BuildEurRatesLookupAsync(db, companyCurrencies);

        return companies
            .Select(c =>
            {
                var buildingValue = c.Buildings
                    .Sum(b => WealthCalculator.GetBuildingValue(b));
                var inventoryValue = c.Buildings
                    .Sum(b => inventoryByBuilding.TryGetValue(b.Id, out var inv)
                        ? inv.Sum(i => i.Quantity * WealthCalculator.GetItemBasePrice(i))
                        : 0m);
                var totalWealth = c.Cash + buildingValue + inventoryValue;
                var currencyCode = companyCurrencyCodeById.GetValueOrDefault(c.Id, "EUR");

                return new CompanyRanking
                {
                    CompanyId = c.Id,
                    CompanyName = c.Name,
                    PlayerId = c.PlayerId,
                    OwnerDisplayName = c.Player?.DisplayName ?? "Unknown",
                    Cash = c.Cash,
                    CurrencyCode = currencyCode,
                    BuildingValue = buildingValue,
                    InventoryValue = inventoryValue,
                    TotalWealth = totalWealth,
                    TotalWealthUsd = Math.Round(ConvertToUsd(totalWealth, currencyCode, eurRatesByCode, usdRate), 4),
                    BuildingCount = c.Buildings.Count
                };
            })
            .OrderByDescending(r => r.TotalWealthUsd)
            .ToList();
    }

    private static string ResolvePrimaryCurrencyCode(Company company) =>
        company.Buildings
            .Select(building => building.City?.CurrencyCode)
            .FirstOrDefault(currencyCode => !string.IsNullOrWhiteSpace(currencyCode))
        ?? "EUR";

    private static string ResolvePrimaryCurrencyCode(Guid companyId, IEnumerable<Building> buildings) =>
        buildings
            .Where(building => building.CompanyId == companyId)
            .Select(building => building.City?.CurrencyCode)
            .FirstOrDefault(currencyCode => !string.IsNullOrWhiteSpace(currencyCode))
        ?? "EUR";

    // ── FX normalization helpers ──────────────────────────────────────────────────

    /// <summary>Returns EUR→USD rate from the FX rate table, defaulting to 1.08 if unavailable.</summary>
    private static async Task<decimal> GetEurToUsdRateAsync(AppDbContext db)
    {
        var rate = await db.FxRates
            .AsNoTracking()
            .Where(r => r.BaseCurrencyCode == "EUR" && r.QuoteCurrencyCode == "USD")
            .OrderByDescending(r => r.RateDate)
            .Select(r => r.Rate)
            .FirstOrDefaultAsync();
        return rate > 0 ? rate : 1.08m; // fallback
    }

    /// <summary>
    /// Builds a lookup of EUR-based rates for each of the given currency codes.
    /// Key = currency code, Value = "units of that currency per 1 EUR".
    /// EUR itself maps to 1.0.
    /// </summary>
    private static async Task<Dictionary<string, decimal>> BuildEurRatesLookupAsync(
        AppDbContext db,
        IEnumerable<string> currencyCodes)
    {
        var codes = currencyCodes.Distinct().Where(c => c != "EUR").ToList();
        var lookup = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["EUR"] = 1m };

        if (codes.Count == 0) return lookup;

        var dbRates = await db.FxRates
            .AsNoTracking()
            .Where(r => r.BaseCurrencyCode == "EUR" && codes.Contains(r.QuoteCurrencyCode))
            .GroupBy(r => r.QuoteCurrencyCode)
            .Select(g => new
            {
                CurrencyCode = g.Key,
                Rate = g.OrderByDescending(r => r.RateDate).Select(r => r.Rate).First()
            })
            .ToListAsync();

        foreach (var row in dbRates)
        {
            lookup[row.CurrencyCode] = row.Rate;
        }

        // Fallback rates for any currency not in the database
        var fallbacks = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["CZK"] = 25.20m,
            ["USD"] = 1.08m,
            ["GBP"] = 0.86m,
            ["CNY"] = 7.84m,
            ["INR"] = 90.50m,
        };
        foreach (var code in codes.Where(c => !lookup.ContainsKey(c)))
        {
            lookup[code] = fallbacks.TryGetValue(code, out var fallback) ? fallback : 1m;
        }

        return lookup;
    }

    /// <summary>
    /// Converts an amount in <paramref name="currencyCode"/> to USD using EUR-based rates.
    /// Formula: amount → EUR via eurRatesByCode, then EUR → USD via usdRate.
    /// </summary>
    private static decimal ConvertToUsd(
        decimal amount,
        string currencyCode,
        Dictionary<string, decimal> eurRatesByCode,
        decimal usdRate)
    {
        if (amount == 0m) return 0m;
        if (string.Equals(currencyCode, "USD", StringComparison.OrdinalIgnoreCase)) return amount;

        // Convert from local currency to EUR: EUR = amount / (units of currencyCode per EUR)
        var eurUnitsPerLocal = eurRatesByCode.TryGetValue(currencyCode, out var r) && r > 0 ? r : 1m;
        var amountInEur = amount / eurUnitsPerLocal;

        // Convert EUR → USD
        return amountInEur * usdRate;
    }

    /// <summary>Gets the current player's companies with their buildings.</summary>
    [Authorize]
    public async Task<List<Company>> GetMyCompanies(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var gameState = await db.GameStates.FirstOrDefaultAsync();
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
                baseBudget = Engine.GameConstants.ResearchBaseQualityBudget(pt.BasePrice);
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

            var baseBudget = Engine.GameConstants.ResearchBaseQualityBudget(pt.BasePrice);
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
        var currentTick = await db.GameStates.AsNoTracking().Select(state => state.CurrentTick).FirstOrDefaultAsync();
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
            Cash = company.Cash,
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

        var gameState = await db.GameStates.FirstOrDefaultAsync();
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
            Industries = Industry.StarterIndustries.ToList()
        };
    }
}

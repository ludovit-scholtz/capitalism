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
            .Where(p => p.Role != PlayerRole.Admin && p.Email != GovernmentActorConstants.GovernmentEmail)
            .ToListAsync();

        // Load all companies, buildings, lots, inventories, and shareholdings for share price calculation
        var companies = await db.Companies
            .Include(company => company.BankAccounts)
            .ToListAsync();
        var buildings = await db.Buildings
            .Include(b => b.City)
            .ToListAsync();
        var playerIds = players.Select(player => player.Id).ToList();
        var personalAccounts = await db.BankAccounts
            .AsNoTracking()
            .Where(account => account.PlayerId.HasValue
                && playerIds.Contains(account.PlayerId.Value)
                && account.ClosedAtUtc == null)
            .ToListAsync();
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
        // Company and personal currencies → EUR rate lookup (EUR per 1 unit of currency = 1/EurRate)
        var companyCurrencies = companyCurrencyCodeById.Values
            .Concat(personalAccounts.Select(account => account.CurrencyCode))
            .Distinct()
            .ToList();
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
                var personalCashUsd = decimal.Round(
                    personalAccounts
                        .Where(account => account.PlayerId == p.Id)
                        .Sum(account => ConvertToUsd(account.Balance, account.CurrencyCode, eurRatesByCode, usdRate)),
                    4,
                    MidpointRounding.AwayFromZero);
                var sharesValue = shareholdings
                    .Where(sh => sh.OwnerPlayerId == p.Id && sh.ShareCount > 0m)
                    .Sum(sh => decimal.Round(
                        sh.ShareCount * sharePriceUsdByCompany.GetValueOrDefault(sh.CompanyId),
                        4,
                        MidpointRounding.AwayFromZero));

                return new PlayerRanking
                {
                    PlayerId = p.Id,
                    DisplayName = p.DisplayName,
                    PersonalCash = personalCashUsd,
                    SharesValue = sharesValue,
                    TotalWealth = decimal.Round(personalCashUsd + sharesValue, 4, MidpointRounding.AwayFromZero),
                    TotalWealthUsd = decimal.Round(personalCashUsd + sharesValue, 4, MidpointRounding.AwayFromZero),
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
            .Include(c => c.BankAccounts)
            .Include(c => c.Buildings)
            .ThenInclude(b => b.Units)
            .Include(c => c.Buildings)
            .ThenInclude(b => b.City)
            .Include(c => c.Player)
            .Where(c => c.Player != null && c.Player.Role != PlayerRole.Admin && c.Player.Email != GovernmentActorConstants.GovernmentEmail)
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
                var currencyCode = companyCurrencyCodeById.GetValueOrDefault(c.Id, "EUR");
                var companyCashUsd = decimal.Round(
                    c.BankAccounts
                        .Where(account => account.ClosedAtUtc == null)
                        .Sum(account => ConvertToUsd(account.Balance, account.CurrencyCode, eurRatesByCode, usdRate)),
                    4,
                    MidpointRounding.AwayFromZero);
                var buildingValueUsd = decimal.Round(ConvertToUsd(buildingValue, currencyCode, eurRatesByCode, usdRate), 4, MidpointRounding.AwayFromZero);
                var inventoryValueUsd = decimal.Round(ConvertToUsd(inventoryValue, currencyCode, eurRatesByCode, usdRate), 4, MidpointRounding.AwayFromZero);
                var totalWealthUsd = companyCashUsd + buildingValueUsd + inventoryValueUsd;

                return new CompanyRanking
                {
                    CompanyId = c.Id,
                    CompanyName = c.Name,
                    PlayerId = c.PlayerId,
                    OwnerDisplayName = c.Player?.DisplayName ?? "Unknown",
                    Cash = companyCashUsd,
                    CurrencyCode = "USD",
                    BuildingValue = buildingValueUsd,
                    InventoryValue = inventoryValueUsd,
                    TotalWealth = totalWealthUsd,
                    TotalWealthUsd = totalWealthUsd,
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
            .FirstOrDefaultDeterministicAsync();
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

}

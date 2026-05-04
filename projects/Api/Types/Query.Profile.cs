using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

/// <summary>
/// Player public profile query.
/// Methods: GetPlayerProfile
/// </summary>
public sealed partial class Query
{
    /// <summary>
    /// Returns the public profile for a player by ID.
    /// Includes leaderboard rank, industries active in, cities with buildings,
    /// total products sold, and hall-of-fame records.
    /// This query is public — no authentication required.
    /// </summary>
    public async Task<PlayerProfileResult?> GetPlayerProfile(
        Guid playerId,
        [Service] AppDbContext db)
    {
        // Load the player
        var player = await db.Players
            .AsNoTracking()
            .Where(p => p.Id == playerId && p.Role != PlayerRole.Admin)
            .FirstOrDefaultAsync();

        if (player is null)
            return null;

        // Load game state for tick-based calculations
        var gameState = await db.GameStates.AsNoTracking().FirstOrDefaultDeterministicAsync();
        var currentTick = gameState?.CurrentTick ?? 0L;
        var ticksPerYear = 8760L; // 1 game year = 8760 ticks

        // Load companies owned by this player
        var companies = await db.Companies
            .AsNoTracking()
            .Include(c => c.BankAccounts)
            .Include(c => c.Buildings)
                .ThenInclude(b => b.City)
            .Where(c => c.PlayerId == playerId)
            .AsSplitQuery()
            .ToListAsync();

        var companyIds = companies.Select(c => c.Id).ToList();
        var buildings = companies.SelectMany(c => c.Buildings).ToList();
        var buildingIds = buildings.Select(b => b.Id).ToList();

        // ── Wealth calculation ────────────────────────────────────────────────

        // Load FX rates
        var usdRate = await GetEurToUsdRateAsync(db);
        var allCurrencies = companies
            .SelectMany(c => c.BankAccounts.Select(a => a.CurrencyCode))
            .Distinct()
            .ToList();
        var eurRatesByCode = await BuildEurRatesLookupAsync(db, allCurrencies);

        // Personal cash (personal bank accounts)
        var personalAccounts = await db.BankAccounts
            .AsNoTracking()
            .Where(a => a.PlayerId == playerId && a.ClosedAtUtc == null)
            .ToListAsync();
        var personalCashUsd = decimal.Round(
            personalAccounts.Sum(a => ConvertToUsd(a.Balance, a.CurrencyCode, eurRatesByCode, usdRate)),
            2, MidpointRounding.AwayFromZero);

        // Shares value
        var allCompaniesForShares = await db.Companies
            .AsNoTracking()
            .Include(c => c.BankAccounts)
            .ToListAsync();
        var allBuildings = await db.Buildings
            .AsNoTracking()
            .Include(b => b.City)
            .ToListAsync();
        var lots = await db.BuildingLots
            .AsNoTracking()
            .Where(l => l.OwnerCompanyId.HasValue)
            .ToListAsync();
        var inventories = await db.Inventories
            .AsNoTracking()
            .Include(i => i.ResourceType)
            .Include(i => i.ProductType)
            .ToListAsync();
        var allShareholdings = await db.Shareholdings.AsNoTracking().ToListAsync();

        var sharePriceByCompany = BuildQuotedSharePriceLookup(allCompaniesForShares, allBuildings, lots, inventories, allShareholdings);
        var companyCurrencyCodeById = allCompaniesForShares.ToDictionary(
            c => c.Id,
            c => ResolvePrimaryCurrencyCode(c.Id, allBuildings));

        var allCurrenciesForShares = companyCurrencyCodeById.Values.Distinct().ToList();
        var eurRatesForShares = await BuildEurRatesLookupAsync(db, allCurrenciesForShares);

        var sharesValue = allShareholdings
            .Where(sh => sh.OwnerPlayerId == playerId && sh.ShareCount > 0m)
            .Sum(sh =>
            {
                var localPrice = sharePriceByCompany.GetValueOrDefault(sh.CompanyId);
                var currencyCode = companyCurrencyCodeById.GetValueOrDefault(sh.CompanyId, "EUR");
                var priceUsd = ConvertToUsd(localPrice, currencyCode, eurRatesForShares, usdRate);
                return decimal.Round(sh.ShareCount * priceUsd, 2, MidpointRounding.AwayFromZero);
            });

        var totalWealthUsd = decimal.Round(personalCashUsd + sharesValue, 2, MidpointRounding.AwayFromZero);

        // Company equity (cash + building value + inventory value) in USD
        var inventoryByBuilding = inventories.GroupBy(i => i.BuildingId).ToDictionary(g => g.Key, g => g.ToList());
        var totalCompanyEquityUsd = decimal.Round(
            companies.Sum(c =>
            {
                var currencyCode = ResolvePrimaryCurrencyCode(c.Id, allBuildings);
                var companyCashUsd = c.BankAccounts
                    .Where(a => a.ClosedAtUtc == null)
                    .Sum(a => ConvertToUsd(a.Balance, a.CurrencyCode, eurRatesByCode, usdRate));
                var buildingValueLocal = c.Buildings.Sum(b => WealthCalculator.GetBuildingValue(b));
                var inventoryValueLocal = c.Buildings.Sum(b =>
                    inventoryByBuilding.TryGetValue(b.Id, out var inv)
                        ? inv.Sum(i => i.Quantity * WealthCalculator.GetItemBasePrice(i))
                        : 0m);
                return companyCashUsd
                    + ConvertToUsd(buildingValueLocal, currencyCode, eurRatesByCode, usdRate)
                    + ConvertToUsd(inventoryValueLocal, currencyCode, eurRatesByCode, usdRate);
            }),
            2, MidpointRounding.AwayFromZero);

        // ── Leaderboard rank ──────────────────────────────────────────────────

        // Load all non-admin players for ranking
        var allPlayerIds = await db.Players
            .AsNoTracking()
            .Where(p => p.Role != PlayerRole.Admin && p.Email != GovernmentActorConstants.GovernmentEmail)
            .Select(p => p.Id)
            .ToListAsync();

        var allPersonalAccounts = await db.BankAccounts
            .AsNoTracking()
            .Where(a => a.PlayerId.HasValue && allPlayerIds.Contains(a.PlayerId!.Value) && a.ClosedAtUtc == null)
            .ToListAsync();

        var allPersonalCashUsd = allPlayerIds.ToDictionary(
            pid => pid,
            pid => allPersonalAccounts
                .Where(a => a.PlayerId == pid)
                .Sum(a => ConvertToUsd(a.Balance, a.CurrencyCode, eurRatesByCode, usdRate)));

        var allSharesValueUsd = allPlayerIds.ToDictionary(
            pid => pid,
            pid => allShareholdings
                .Where(sh => sh.OwnerPlayerId == pid && sh.ShareCount > 0m)
                .Sum(sh =>
                {
                    var localPrice = sharePriceByCompany.GetValueOrDefault(sh.CompanyId);
                    var currCode = companyCurrencyCodeById.GetValueOrDefault(sh.CompanyId, "EUR");
                    var priceUsd = ConvertToUsd(localPrice, currCode, eurRatesForShares, usdRate);
                    return sh.ShareCount * priceUsd;
                }));

        var rankedPlayers = allPlayerIds
            .Select(pid => new { PlayerId = pid, Wealth = allPersonalCashUsd.GetValueOrDefault(pid) + allSharesValueUsd.GetValueOrDefault(pid) })
            .OrderByDescending(x => x.Wealth)
            .ThenBy(x => x.PlayerId) // deterministic tie-breaking
            .ToList();

        var rank = rankedPlayers.FindIndex(x => x.PlayerId == playerId) + 1;

        // ── Active industries / building types ────────────────────────────────
        var activeBuildingTypes = buildings
            .Select(b => b.Type)
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        // ── Cities with buildings ─────────────────────────────────────────────
        var citiesWithBuildings = buildings
            .Select(b => b.CityId)
            .Distinct()
            .Count();

        // ── Total products sold ───────────────────────────────────────────────
        var totalProductsSold = companyIds.Count > 0
            ? await db.PublicSalesRecords
                .AsNoTracking()
                .Where(r => companyIds.Contains(r.CompanyId))
                .SumAsync(r => r.QuantitySold)
            : 0m;

        // ── Hall of fame ──────────────────────────────────────────────────────

        // Highest single-tick revenue
        decimal highestRevenue = 0m;
        long highestRevenueTick = 0L;
        if (companyIds.Count > 0)
        {
            var revenueByTick = await db.PublicSalesRecords
                .AsNoTracking()
                .Where(r => companyIds.Contains(r.CompanyId))
                .GroupBy(r => new { r.CompanyId, r.Tick })
                .Select(g => new { g.Key.Tick, Revenue = g.Sum(r => r.Revenue) })
                .ToListAsync();

            if (revenueByTick.Count > 0)
            {
                var best = revenueByTick.OrderByDescending(x => x.Revenue).First();
                highestRevenue = decimal.Round(best.Revenue, 2, MidpointRounding.AwayFromZero);
                highestRevenueTick = best.Tick;
            }
        }

        // Largest building acquisition (from BuildingLots purchased by this player's companies)
        var companyLots = await db.BuildingLots
            .AsNoTracking()
            .Where(l => l.OwnerCompanyId.HasValue && companyIds.Contains(l.OwnerCompanyId!.Value))
            .OrderByDescending(l => l.Price)
            .FirstOrDefaultAsync();

        var largestAcquisitionPrice = companyLots?.Price ?? 0m;
        // Find the building name for this lot
        string? largestAcquisitionName = null;
        if (companyLots != null)
        {
            var lotBuilding = buildings.FirstOrDefault(b =>
                Math.Abs(b.Latitude - companyLots.Latitude) < 0.0001 &&
                Math.Abs(b.Longitude - companyLots.Longitude) < 0.0001);
            largestAcquisitionName = lotBuilding?.Name;
        }

        // Highest brand quality across all companies
        decimal highestBrandQuality = 0m;
        string? highestBrandName = null;
        if (companyIds.Count > 0)
        {
            var brands = await db.Brands
                .AsNoTracking()
                .Where(b => companyIds.Contains(b.CompanyId))
                .ToListAsync();

            if (brands.Count > 0)
            {
                var bestBrand = brands
                    .OrderByDescending(b => 1m - (1m - b.Quality) * (1m - b.MarketingQuality))
                    .First();
                highestBrandQuality = decimal.Round(
                    1m - (1m - bestBrand.Quality) * (1m - bestBrand.MarketingQuality),
                    4, MidpointRounding.AwayFromZero);
                highestBrandName = bestBrand.Name;
            }
        }

        // Join game year is derived from account creation relative to current time.
        // Game starts at 2001 and runs at 8760 ticks/year.
        var registrationOffsetTicks = (long)((DateTime.UtcNow - player.CreatedAtUtc).TotalSeconds / 3600.0 * (ticksPerYear / 8760.0));
        var estimatedJoinTick = Math.Max(0, currentTick - registrationOffsetTicks);
        var gameJoinYear = 2001 + (int)(estimatedJoinTick / ticksPerYear);

        // Account age in ticks since the player first joined the game.
        var accountAgeTicks = Math.Max(0L, currentTick - estimatedJoinTick);

        return new PlayerProfileResult
        {
            PlayerId = player.Id,
            DisplayName = player.DisplayName,
            Bio = player.Bio,
            CreatedAtUtc = player.CreatedAtUtc,
            JoinGameYear = gameJoinYear,
            HasProSubscription = player.ProSubscriptionEndsAtUtc.HasValue && player.ProSubscriptionEndsAtUtc > DateTime.UtcNow,
            TotalWealthUsd = totalWealthUsd,
            TotalCompanyEquityUsd = totalCompanyEquityUsd,
            CompanyCount = companies.Count,
            LeaderboardRank = rank,
            ActiveBuildingTypes = activeBuildingTypes,
            CitiesWithBuildings = citiesWithBuildings,
            TotalProductsSold = decimal.Round(totalProductsSold, 2, MidpointRounding.AwayFromZero),
            HallOfFame = new PlayerHallOfFame
            {
                HighestSingleTickRevenue = highestRevenue,
                HighestSingleTickRevenueTick = highestRevenueTick,
                LargestBuildingAcquisitionPrice = largestAcquisitionPrice,
                LargestBuildingAcquisitionName = largestAcquisitionName,
                HighestBrandQuality = highestBrandQuality,
                HighestBrandQualityName = highestBrandName,
                AccountAgeTicks = accountAgeTicks,
            },
        };
    }
}

using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using HotChocolate.CostAnalysis.Types;
using HotChocolate.Authorization;
using HotChocolate.Execution.Processing;
using HotChocolate.Resolvers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Api.Types;

public sealed partial class Query
{
    private static readonly TimeSpan LedgerHistoryCacheDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan LedgerCurrentYearCacheDuration = TimeSpan.FromSeconds(10);

    /// <summary>Returns a financial ledger summary for a company (requires auth — must own company).</summary>
    [Authorize]
    [Cost(50)]
    public async Task<CompanyLedgerSummary?> GetCompanyLedger(
        Guid companyId,
        int? gameYear,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] IMemoryCache cache,
        IResolverContext resolverContext)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        // The per-year history needs an aggregate over the company's whole ledger. Most callers (e.g. the
        // mobile dashboard's Financial summary) only want the totals, so compute it only when selected.
        var includeHistory = resolverContext.IsSelected("history");

        var company = await db.Companies
            .AsNoTracking()
            .Include(c => c.BankAccounts)
            .Include(c => c.Buildings).ThenInclude(b => b.City)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == companyId && c.PlayerId == userId);

        if (company is null) return null;

        var gameState = await db.GameStates
            .AsNoTracking()
            .FirstOrDefaultDeterministicAsync();
        var currentTick = gameState?.CurrentTick ?? 0L;
        var currentGameYear = GameTime.GetGameYear(currentTick);
        var selectedGameYear = gameYear ?? currentGameYear;
        var isCurrentYear = selectedGameYear == currentGameYear;

        // Check cache — historical (non-current) years are immutable so can be cached longer.
        // A summary that includes the history is a superset of one without it, so it can serve both.
        // A history-less summary is cached under its own key and never served to a request that selects history.
        var cacheKey = $"ledger_{companyId}_{selectedGameYear}_{(includeHistory ? "h" : "t")}";
        var fullSummaryCacheKey = $"ledger_{companyId}_{selectedGameYear}_h";
        var cacheDuration = isCurrentYear ? LedgerCurrentYearCacheDuration : LedgerHistoryCacheDuration;
        if ((cache.TryGetValue(fullSummaryCacheKey, out CompanyLedgerSummary? cachedSummary)
                || cache.TryGetValue(cacheKey, out cachedSummary))
            && cachedSummary is not null)
        {
            return cachedSummary;
        }

        // Fetch only entries for the selected year — uses the (CompanyId, RecordedAtTick) index.
        var startTick = GameTime.GetStartTickForGameYear(selectedGameYear);
        var endTick = GameTime.GetEndTickForGameYear(selectedGameYear);

        var entries = await db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId && e.RecordedAtTick >= startTick && e.RecordedAtTick <= endTick)
            .ToListAsync();

        // History only needs per game-year / category / sign sums plus the first and last tick, so the
        // database aggregates it. Loading every ledger row of the company on each request made this query
        // (and the dashboard's Financial summary) slower the longer a company had been playing.
        var historyAggregates = includeHistory
            ? await HistoryAggregateQuery(db.LedgerEntries.AsNoTracking(), companyId).ToListAsync()
            : [];

        var totalRevenue = LedgerCalculator.GetTotalRevenue(entries);
        var totalGovernmentContractRevenue = entries
            .Where(entry => entry.Category == LedgerCategory.GovernmentContractRevenue && entry.Amount > 0m)
            .Sum(entry => entry.Amount);
        var totalMediaHouseIncome = LedgerCalculator.GetTotalMediaHouseIncome(entries);
        var totalRentIncome = LedgerCalculator.GetTotalRentIncome(entries);
        var totalPropertyMaintenance = LedgerCalculator.GetTotalPropertyMaintenance(entries);
        var totalPurchasingCosts = LedgerCalculator.GetTotalPurchasingCosts(entries);
        var totalShippingCosts = LedgerCalculator.GetTotalShippingCosts(entries);
        var totalLaborCosts = LedgerCalculator.GetTotalLaborCosts(entries);
        var totalEnergyCosts = LedgerCalculator.GetTotalEnergyCosts(entries);
        var totalMarketingCosts = LedgerCalculator.GetTotalMarketingCosts(entries);
        var totalTaxPaid = LedgerCalculator.GetTotalTaxPaid(entries);
        var totalOtherCosts = LedgerCalculator.GetTotalOtherCosts(entries);
        var totalPropertyPurchases = LedgerCalculator.GetTotalPropertyPurchases(entries);
        var totalStockPurchaseCashOut = LedgerCalculator.GetTotalStockPurchaseCashOut(entries);
        var totalStockSaleCashIn = LedgerCalculator.GetTotalStockSaleCashIn(entries);
        // Banking aggregates
        var totalDepositInterestReceived = LedgerCalculator.GetTotalDepositInterestReceived(entries);
        var totalDepositInterestPaid = LedgerCalculator.GetTotalDepositInterestPaid(entries);
        var totalLoanInterestIncome = LedgerCalculator.GetTotalLoanInterestIncome(entries);
        var totalLoanInterestExpense = LedgerCalculator.GetTotalLoanInterestExpense(entries);
        var totalDepositsMade = LedgerCalculator.GetTotalDepositsMade(entries);
        var totalDepositsWithdrawn = LedgerCalculator.GetTotalDepositsWithdrawn(entries);
        var totalLoanOriginations = LedgerCalculator.GetTotalLoanOriginations(entries);
        var totalLoanRepaymentPrincipal = LedgerCalculator.GetTotalLoanRepaymentPrincipal(entries);
        var taxableIncome = LedgerCalculator.ComputeTaxableIncome(entries);
        var estimatedIncomeTax = isCurrentYear
            ? GameTime.ComputeEstimatedIncomeTax(taxableIncome, gameState?.TaxRate ?? 0m)
            : totalTaxPaid;

        var ownedLots = await db.BuildingLots
            .AsNoTracking()
            .Where(lot => lot.OwnerCompanyId == companyId)
            .ToListAsync();

        var propertyValue = ownedLots.Sum(WealthCalculator.GetLandValue);
        var buildingValue = company.Buildings.Sum(b => WealthCalculator.GetBuildingValue(b));

        var buildingIds = company.Buildings.Select(b => b.Id).ToList();
        var inventories = await db.Inventories
            .AsNoTracking()
            .Where(i => buildingIds.Contains(i.BuildingId))
            .Include(i => i.ResourceType)
            .Include(i => i.ProductType)
            .ToListAsync();
        var inventoryValue = inventories.Sum(i => i.Quantity * WealthCalculator.GetItemBasePrice(i));

        // Active deposits placed by this company appear as assets on the balance sheet
        var totalDepositsPlaced = await db.BankAccounts
            .AsNoTracking()
            .Where(d => d.CompanyId == companyId && d.BankBuildingId != null && d.ClosedAtUtc == null)
            .SumAsync(d => (decimal?)d.Balance) ?? 0m;
        var currentCash = CompanyBankingService.GetTotalBalance(company);

        var buildingSummaries = entries
            .Where(e => e.BuildingId.HasValue)
            .GroupBy(e => e.BuildingId!.Value)
            .Select(g =>
            {
                var b = company.Buildings.FirstOrDefault(bld => bld.Id == g.Key);
                return new BuildingLedgerSummary
                {
                    BuildingId = g.Key,
                    BuildingName = b?.Name ?? string.Empty,
                    BuildingType = b?.Type ?? string.Empty,
                    CurrencyCode = b?.City?.CurrencyCode ?? "EUR",
                    Revenue = g.Where(e => e.Amount > 0).Sum(e => e.Amount),
                    Costs = Math.Abs(g.Where(e => e.Amount < 0).Sum(e => e.Amount)),
                };
            })
            .ToList();

        // Determine the primary currency and whether multiple currencies are present.
        var buildingCurrencies = company.Buildings
            .Select(b => b.City?.CurrencyCode ?? "EUR")
            .Distinct()
            .ToList();
        var primaryCurrencyCode = buildingCurrencies.Count == 1 ? buildingCurrencies[0] : "EUR";

        // Build history from the lightweight projection — no in-memory entity hydration needed.
        var history = historyAggregates
            .GroupBy(item => GameTime.GetGameYear(item.YearIndex * GameConstants.TicksPerYear))
            .Select(group => BuildCompanyLedgerHistoryYear(
                group.Key,
                currentGameYear,
                gameState?.TaxRate ?? 0m,
                group.ToList()))
            .ToDictionary(item => item.GameYear);

        if (includeHistory && !history.ContainsKey(currentGameYear))
        {
            history[currentGameYear] = BuildCompanyLedgerHistoryYear(currentGameYear, currentGameYear, gameState?.TaxRate ?? 0m, []);
        }

        var incomeTaxDueAtTick = GameTime.GetIncomeTaxDueTickForGameYear(selectedGameYear);

        var summary = new CompanyLedgerSummary
        {
            CompanyId = company.Id,
            CompanyName = company.Name,
            GameYear = selectedGameYear,
            IsCurrentGameYear = isCurrentYear,
            CurrentCash = currentCash,
            PrimaryCurrencyCode = primaryCurrencyCode,
            HasMixedCurrencies = buildingCurrencies.Count > 1,
            TotalRevenue = totalRevenue,
            TotalGovernmentContractRevenue = totalGovernmentContractRevenue,
            TotalMediaHouseIncome = totalMediaHouseIncome,
            TotalRentIncome = totalRentIncome,
            TotalPropertyMaintenance = totalPropertyMaintenance,
            TotalPurchasingCosts = totalPurchasingCosts,
            TotalShippingCosts = totalShippingCosts,
            TotalLaborCosts = totalLaborCosts,
            TotalEnergyCosts = totalEnergyCosts,
            TotalMarketingCosts = totalMarketingCosts,
            TotalTaxPaid = totalTaxPaid,
            TotalOtherCosts = totalOtherCosts,
            TaxableIncome = taxableIncome,
            EstimatedIncomeTax = estimatedIncomeTax,
            TotalPropertyPurchases = totalPropertyPurchases,
            TotalStockPurchaseCashOut = totalStockPurchaseCashOut,
            TotalStockSaleCashIn = totalStockSaleCashIn,
            // Banking income/expense
            TotalDepositInterestReceived = totalDepositInterestReceived,
            TotalDepositInterestPaid = totalDepositInterestPaid,
            TotalLoanInterestIncome = totalLoanInterestIncome,
            TotalLoanInterestExpense = totalLoanInterestExpense,
            NetIncome = totalRevenue + totalMediaHouseIncome + totalRentIncome + totalDepositInterestReceived + totalLoanInterestIncome
                - totalPurchasingCosts - totalShippingCosts - totalLaborCosts - totalEnergyCosts
                - totalMarketingCosts - totalTaxPaid - totalOtherCosts - totalPropertyMaintenance
                - totalDepositInterestPaid - totalLoanInterestExpense,
            PropertyValue = propertyValue,
            PropertyAppreciation = propertyValue - totalPropertyPurchases,
            BuildingValue = buildingValue,
            InventoryValue = inventoryValue,
            TotalDepositsPlaced = totalDepositsPlaced,
            TotalAssets = currentCash + propertyValue + buildingValue + inventoryValue,
            CashFromOperations = totalRevenue + totalMediaHouseIncome + totalRentIncome + totalDepositInterestReceived + totalLoanInterestIncome
                - totalPurchasingCosts - totalShippingCosts - totalLaborCosts - totalEnergyCosts
                - totalMarketingCosts - totalDepositInterestPaid - totalLoanInterestExpense - totalPropertyMaintenance,
            CashFromInvestments = totalStockSaleCashIn - totalPropertyPurchases - totalStockPurchaseCashOut,
            CashFromBanking = totalLoanOriginations + totalDepositsWithdrawn
                - totalDepositsMade - totalLoanRepaymentPrincipal,
            FirstRecordedTick = entries.Count > 0 ? entries.Min(e => e.RecordedAtTick) : 0,
            LastRecordedTick = entries.Count > 0 ? entries.Max(e => e.RecordedAtTick) : 0,
            BuildingSummaries = buildingSummaries,
            IncomeTaxDueAtTick = incomeTaxDueAtTick,
            IncomeTaxDueGameTimeUtc = GameTime.GetInGameTimeUtc(incomeTaxDueAtTick),
            IncomeTaxDueGameYear = GameTime.GetGameYear(incomeTaxDueAtTick),
            IsIncomeTaxSettled = selectedGameYear < currentGameYear,
            History = history.Values
                .OrderByDescending(item => item.GameYear)
                .ToList(),
        };

        cache.Set(cacheKey, summary, cacheDuration);
        return summary;
    }

    [Authorize]
    public async Task<List<CompanyCityFinancialSummary>> GetCompanyCityFinancialBreakdown(
        Guid companyId,
        int? gameYear,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId && c.PlayerId == userId);
        if (company is null)
        {
            return [];
        }

        var gameState = await db.GameStates
            .AsNoTracking()
            .FirstOrDefaultDeterministicAsync();
        var selectedGameYear = gameYear ?? GameTime.GetGameYear(gameState?.CurrentTick ?? 0L);
        var startTick = GameTime.GetStartTickForGameYear(selectedGameYear);
        var endTick = GameTime.GetEndTickForGameYear(selectedGameYear);

        var buildings = await db.Buildings
            .AsNoTracking()
            .Include(b => b.City)
            .Where(b => b.CompanyId == companyId)
            .ToListAsync();
        if (buildings.Count == 0)
        {
            return [];
        }

        var cityByBuildingId = buildings
            .Where(b => b.City != null)
            .GroupBy(b => b.Id)
            .ToDictionary(group => group.Key, group => group.First().City!);

        var entries = await db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId
                && e.BuildingId.HasValue
                && e.RecordedAtTick >= startTick
                && e.RecordedAtTick <= endTick)
            .Select(e => new { e.BuildingId, e.Amount, e.RecordedAtTick })
            .ToListAsync();

        var summaries = new List<CompanyCityFinancialSummary>();
        foreach (var city in buildings
                     .Where(b => b.City != null)
                     .Select(b => b.City!)
                     .GroupBy(c => c.Id)
                     .Select(group => group.First()))
        {
            var cityEntries = entries
                .Where(e => e.BuildingId.HasValue
                    && cityByBuildingId.TryGetValue(e.BuildingId.Value, out var mappedCity)
                    && mappedCity.Id == city.Id)
                .ToList();

            var revenue = cityEntries.Where(e => e.Amount > 0m).Sum(e => e.Amount);
            var costs = Math.Abs(cityEntries.Where(e => e.Amount < 0m).Sum(e => e.Amount));
            var revenueTrend = cityEntries
                .Where(e => e.Amount > 0m)
                .GroupBy(e => e.RecordedAtTick)
                .Select(group => new CityRevenueTrendPoint
                {
                    Tick = group.Key,
                    Revenue = group.Sum(entry => entry.Amount),
                })
                .OrderBy(point => point.Tick)
                .TakeLast(12)
                .ToList();

            summaries.Add(new CompanyCityFinancialSummary
            {
                CityId = city.Id,
                CityName = city.Name,
                CurrencyCode = city.CurrencyCode,
                Revenue = revenue,
                Costs = costs,
                Profit = revenue - costs,
                RevenueTrend = revenueTrend,
            });
        }

        return summaries
            .OrderByDescending(summary => summary.Profit)
            .ThenBy(summary => summary.CityName)
            .ToList();
    }
}

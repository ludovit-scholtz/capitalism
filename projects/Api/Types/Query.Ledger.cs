using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using HotChocolate.CostAnalysis.Types;
using HotChocolate.Authorization;
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
        [Service] IMemoryCache cache)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var company = await db.Companies
            .AsNoTracking()
            .Include(c => c.BankAccounts)
            .Include(c => c.Buildings).ThenInclude(b => b.City)
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
        var cacheKey = $"ledger_{companyId}_{selectedGameYear}";
        var cacheDuration = isCurrentYear ? LedgerCurrentYearCacheDuration : LedgerHistoryCacheDuration;
        if (cache.TryGetValue(cacheKey, out CompanyLedgerSummary? cachedSummary) && cachedSummary is not null)
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

        // For history years we only need lightweight aggregates: tick + category + amount.
        // Load all entries but project only the columns needed for grouping.
        var historyProjection = await db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId)
            .Select(e => new { e.RecordedAtTick, e.Category, e.Amount })
            .ToListAsync();

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
        var history = historyProjection
            .GroupBy(e => GameTime.GetGameYear(e.RecordedAtTick))
            .Select(group => BuildCompanyLedgerHistoryYearFromProjection(
                group.Key, currentGameYear, gameState?.TaxRate ?? 0m,
                group.Select(e => (e.RecordedAtTick, e.Category, e.Amount)).ToList()))
            .ToDictionary(item => item.GameYear);

        if (!history.ContainsKey(currentGameYear))
        {
            history[currentGameYear] = BuildCompanyLedgerHistoryYearFromProjection(currentGameYear, currentGameYear, gameState?.TaxRate ?? 0m, []);
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

    /// <summary>Returns drill-down entries for a specific ledger category.</summary>
    [Authorize]
    [Cost(35)]
    public async Task<List<LedgerEntryResult>> GetLedgerDrillDown(
        Guid companyId,
        string category,
        int? gameYear,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var ownsCompany = await db.Companies
            .AnyAsync(c => c.Id == companyId && c.PlayerId == userId);

        if (!ownsCompany) return [];

        // Uses the compound (CompanyId, Category, RecordedAtTick) index.
        var entriesQuery = db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId && e.Category == category);

        if (gameYear.HasValue)
        {
            var startTick = GameTime.GetStartTickForGameYear(gameYear.Value);
            var endTick = GameTime.GetEndTickForGameYear(gameYear.Value);
            entriesQuery = entriesQuery.Where(e => e.RecordedAtTick >= startTick && e.RecordedAtTick <= endTick);
        }

        var entries = await entriesQuery
            .OrderByDescending(e => e.RecordedAtTick)
            .Take(200)
            .Include(e => e.Building).ThenInclude(b => b!.City)
            .Include(e => e.ProductType)
            .Include(e => e.ResourceType)
            .ToListAsync();

        var minTick = entries.Count > 0 ? entries.Min(e => e.RecordedAtTick) : 0L;
        var maxTick = entries.Count > 0 ? entries.Max(e => e.RecordedAtTick) : 0L;

        var relevantEvents = entries.Count == 0
            ? []
            : await db.MarketEvents
                .AsNoTracking()
                .Where(me => me.ExpiresAtTick >= minTick && me.StartsAtTick <= maxTick)
                .ToListAsync();

        var cycle = await db.EconomicCycles
            .AsNoTracking()
            .OrderByDescending(c => c.PhaseStartedTick)
            .FirstOrDefaultAsync();

        return entries.Select(e => new LedgerEntryResult
        {
            Id = e.Id,
            Category = e.Category,
            Description = e.Description,
            Amount = e.Amount,
            RecordedAtTick = e.RecordedAtTick,
            RecordedAtUtc = e.RecordedAtUtc,
            BuildingId = e.BuildingId,
            BuildingName = e.Building?.Name,
            BuildingType = e.Building?.Type,
            BuildingUnitId = e.BuildingUnitId,
            ProductTypeId = e.ProductTypeId,
            ProductName = e.ProductType?.Name,
            ResourceTypeId = e.ResourceTypeId,
            ResourceName = e.ResourceType?.Name,
            CurrencyCode = e.Building?.City?.CurrencyCode ?? "EUR",
            EventTag = ResolveLedgerEventTag(e, relevantEvents, cycle),
            EventDescription = ResolveLedgerEventDescription(e, relevantEvents, cycle),
        }).ToList();
    }

    private static bool IsTickInGameYear(long tick, int gameYear)
    {
        var startTick = GameTime.GetStartTickForGameYear(gameYear);
        var endTick = GameTime.GetEndTickForGameYear(gameYear);
        return tick >= startTick && tick <= endTick;
    }

    private static CompanyLedgerHistoryYear BuildCompanyLedgerHistoryYear(
        int gameYear,
        int currentGameYear,
        decimal taxRate,
        List<LedgerEntry> entries)
    {
        var projections = entries.Select(e => (e.RecordedAtTick, e.Category, e.Amount)).ToList();
        return BuildCompanyLedgerHistoryYearFromProjection(gameYear, currentGameYear, taxRate, projections);
    }

    /// <summary>
    /// Builds a ledger history year summary from lightweight (RecordedAtTick, Category, Amount) projections,
    /// avoiding the need to hydrate full LedgerEntry entities for historical data.
    /// </summary>
    private static CompanyLedgerHistoryYear BuildCompanyLedgerHistoryYearFromProjection(
        int gameYear,
        int currentGameYear,
        decimal taxRate,
        List<(long RecordedAtTick, string Category, decimal Amount)> projections)
    {
        var totalRevenue = projections.Where(e => (e.Category == LedgerCategory.Revenue || e.Category == LedgerCategory.GovernmentContractRevenue) && e.Amount > 0).Sum(e => e.Amount);
        var totalMediaHouseIncome = projections.Where(e => e.Category == LedgerCategory.MediaHouseIncome && e.Amount > 0).Sum(e => e.Amount);
        var totalRentIncome = projections.Where(e => e.Category == LedgerCategory.RentIncome && e.Amount > 0).Sum(e => e.Amount);
        var totalPropertyMaintenance = Math.Abs(projections.Where(e => e.Category == LedgerCategory.PropertyMaintenance && e.Amount < 0).Sum(e => e.Amount));
        var totalPurchasingCosts = Math.Abs(projections.Where(e => e.Category == LedgerCategory.PurchasingCost && e.Amount < 0).Sum(e => e.Amount));
        var totalShippingCosts = Math.Abs(projections.Where(e => e.Category == LedgerCategory.ShippingCost && e.Amount < 0).Sum(e => e.Amount));
        var totalLaborCosts = Math.Abs(projections.Where(e => e.Category == LedgerCategory.LaborCost && e.Amount < 0).Sum(e => e.Amount));
        var totalEnergyCosts = Math.Abs(projections.Where(e => e.Category == LedgerCategory.EnergyCost && e.Amount < 0).Sum(e => e.Amount));
        var totalMarketingCosts = Math.Abs(projections.Where(e => e.Category == LedgerCategory.Marketing && e.Amount < 0).Sum(e => e.Amount));
        var totalTaxPaid = Math.Abs(projections.Where(e => e.Category == LedgerCategory.Tax && e.Amount < 0).Sum(e => e.Amount));
        var totalOtherCosts = Math.Abs(projections.Where(e => e.Category == LedgerCategory.Other && e.Amount < 0).Sum(e => e.Amount));
        // Taxable income = revenue + media house income + rent income minus all deductible operating costs (excluding tax itself).
        var taxableIncome = Math.Max(totalRevenue + totalMediaHouseIncome + totalRentIncome - totalPurchasingCosts - totalShippingCosts - totalLaborCosts - totalEnergyCosts - totalMarketingCosts - totalPropertyMaintenance, 0m);
        var estimatedIncomeTax = gameYear == currentGameYear
            ? GameTime.ComputeEstimatedIncomeTax(taxableIncome, taxRate)
            : totalTaxPaid;

        return new CompanyLedgerHistoryYear
        {
            GameYear = gameYear,
            IsCurrentGameYear = gameYear == currentGameYear,
            TotalRevenue = totalRevenue,
            TotalLaborCosts = totalLaborCosts,
            TotalEnergyCosts = totalEnergyCosts,
            NetIncome = totalRevenue + totalMediaHouseIncome + totalRentIncome - totalPurchasingCosts - totalShippingCosts - totalLaborCosts - totalEnergyCosts - totalMarketingCosts - totalPropertyMaintenance - totalTaxPaid - totalOtherCosts,
            TotalTaxPaid = totalTaxPaid,
            TaxableIncome = taxableIncome,
            EstimatedIncomeTax = estimatedIncomeTax,
            FirstRecordedTick = projections.Count > 0 ? projections.Min(e => e.RecordedAtTick) : 0,
            LastRecordedTick = projections.Count > 0 ? projections.Max(e => e.RecordedAtTick) : 0,
        };
    }

    private static decimal ComputeCompanyAssetValue(
        Company company,
        IReadOnlyCollection<BuildingLot> allOwnedLots,
        IReadOnlyCollection<Inventory> allInventories)
    {
        var buildingIds = company.Buildings.Select(building => building.Id).ToHashSet();
        var inventoryValue = allInventories
            .Where(inventory => buildingIds.Contains(inventory.BuildingId))
            .Sum(WealthCalculator.GetItemBasePrice);
        var lotValue = allOwnedLots
            .Where(lot => lot.OwnerCompanyId == company.Id)
            .Sum(WealthCalculator.GetLandValue);

        return CompanyBankingService.GetTotalBalance(company) + company.Buildings.Sum(WealthCalculator.GetBuildingValue) + lotValue + allInventories
            .Where(inventory => buildingIds.Contains(inventory.BuildingId))
            .Sum(inventory => inventory.Quantity * WealthCalculator.GetItemBasePrice(inventory));
    }

    private static string? ResolveLedgerEventTag(
        LedgerEntry entry,
        IReadOnlyCollection<MarketEvent> events,
        EconomicCycle? cycle)
    {
        var marketEvent = ResolveMatchingEvent(entry, events);
        if (marketEvent is not null)
        {
            return marketEvent.EventType switch
            {
                MarketEventType.CommodityShock => $"📈 Commodity shock {(marketEvent.MagnitudeMultiplier - 1m) * 100m:+0;-0;0}%",
                MarketEventType.InterestRateChange => $"🏦 Interest shift {(marketEvent.MagnitudeMultiplier - 1m) * 100m:+0;-0;0}%",
                MarketEventType.SeasonalDemandSurge => $"🛍️ Seasonal surge {(marketEvent.MagnitudeMultiplier - 1m) * 100m:+0;-0;0}%",
                _ => null,
            };
        }

        if (cycle is not null && entry.Category == LedgerCategory.Revenue)
        {
            var phase = ResolveCyclePhaseAtTick(cycle, entry.RecordedAtTick);
            if (phase == EconomicCyclePhase.Recession)
                return "📉 Recession";
        }

        return null;
    }

    private static string? ResolveLedgerEventDescription(
        LedgerEntry entry,
        IReadOnlyCollection<MarketEvent> events,
        EconomicCycle? cycle)
    {
        var marketEvent = ResolveMatchingEvent(entry, events);
        if (marketEvent is not null)
            return marketEvent.Description;

        if (cycle is not null && entry.Category == LedgerCategory.Revenue)
        {
            var phase = ResolveCyclePhaseAtTick(cycle, entry.RecordedAtTick);
            if (phase == EconomicCyclePhase.Recession)
                return "Revenue was recorded during a recession phase.";
        }

        return null;
    }

    private static MarketEvent? ResolveMatchingEvent(LedgerEntry entry, IReadOnlyCollection<MarketEvent> events)
    {
        return events.FirstOrDefault(me =>
            me.StartsAtTick <= entry.RecordedAtTick
            && me.ExpiresAtTick >= entry.RecordedAtTick
            && (me.AffectedCityId == null || me.AffectedCityId == entry.Building?.CityId)
            && (me.EventType != MarketEventType.CommodityShock || !me.AffectedResourceTypeId.HasValue || me.AffectedResourceTypeId == entry.ResourceTypeId)
            && (me.EventType != MarketEventType.InterestRateChange
                || entry.Category == LedgerCategory.LoanInterestExpense
                || entry.Category == LedgerCategory.LoanInterestIncome)
            && (me.EventType != MarketEventType.SeasonalDemandSurge || entry.Category == LedgerCategory.Revenue));
    }

    private static string ResolveCyclePhaseAtTick(EconomicCycle cycle, long tick)
    {
        if (tick >= cycle.PhaseStartedTick)
            return cycle.Phase;

        var phase = cycle.Phase;
        var phaseStart = cycle.PhaseStartedTick;
        var guard = 0;
        while (tick < phaseStart && guard++ < 32)
        {
            var previousPhase = phase switch
            {
                EconomicCyclePhase.Expansion => EconomicCyclePhase.Trough,
                EconomicCyclePhase.Peak => EconomicCyclePhase.Expansion,
                EconomicCyclePhase.Recession => EconomicCyclePhase.Peak,
                _ => EconomicCyclePhase.Recession,
            };
            phaseStart -= previousPhase switch
            {
                EconomicCyclePhase.Expansion => GameConstants.TicksPerMonth * 3,
                EconomicCyclePhase.Peak => GameConstants.TicksPerMonth,
                EconomicCyclePhase.Recession => GameConstants.TicksPerMonth * 2,
                _ => GameConstants.TicksPerMonth,
            };
            phase = previousPhase;
        }

        return phase;
    }
}

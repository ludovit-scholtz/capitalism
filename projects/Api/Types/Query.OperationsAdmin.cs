using Api.Data;
using Api.Data.Entities;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Api.Types;

public sealed partial class Query
{
    private const int OperationsWindowTicks = 100;
    private const int ProductAnalyticsMaxWindowTicks = 720;
    private static readonly TimeSpan ProductAnalyticsCacheDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Returns aggregated operations statistics for the admin Operations Dashboard:
    /// money inflow and outflow broken down by ledger category over the last 100 ticks,
    /// plus server-wide player/company/building counts.
    /// Requires admin dashboard access.
    /// </summary>
    [Authorize]
    public async Task<OperationsStatisticsResult> GetOperationsStatistics(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] GameAdminAuthorizationService gameAdminAuthorizationService)
    {
        var principal = httpContextAccessor.HttpContext!.User;
        await gameAdminAuthorizationService.RequireAdminDashboardAccessAsync(db, principal, httpContextAccessor.HttpContext!.RequestAborted);

        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(s => s.CurrentTick)
            .FirstOrDefaultAsync(httpContextAccessor.HttpContext.RequestAborted);

        var windowStart = Math.Max(0L, currentTick - OperationsWindowTicks);

        var entries = await db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.RecordedAtTick >= windowStart)
            .ToListAsync(httpContextAccessor.HttpContext.RequestAborted);

        var forexFeeAggregate = await db.ForexTradeRecords
            .AsNoTracking()
            .Where(t => t.ExecutedAtTick >= windowStart)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Sum(t => t.FeeAmount),
                Count = g.Count(),
            })
            .FirstOrDefaultAsync(httpContextAccessor.HttpContext.RequestAborted);

        var goldAmmFeeAggregate = await db.GoldAmmTradeRecords
            .AsNoTracking()
            .Where(t => t.ExecutedAtTick >= windowStart)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Sum(t => t.FeeAmount),
                Count = g.Count(),
            })
            .FirstOrDefaultAsync(httpContextAccessor.HttpContext.RequestAborted);

        var fxFeeAmount = (forexFeeAggregate?.Total ?? 0m) + (goldAmmFeeAggregate?.Total ?? 0m);
        var fxFeeEntryCount = (forexFeeAggregate?.Count ?? 0) + (goldAmmFeeAggregate?.Count ?? 0);

        var inflowItems = BuildInflowItems(entries);
        var outflowItems = BuildOutflowItems(entries, fxFeeAmount, fxFeeEntryCount);

        var totalInflow = inflowItems.Sum(i => i.Amount);
        var totalOutflow = outflowItems.Sum(i => i.Amount);

        // Server-wide counts in a single projection to reduce round-trips.
        var counts = await (
            from gs in db.GameStates.AsNoTracking()
            select new
            {
                TotalPlayers = db.Players.AsNoTracking().Count(),
                TotalCompanies = db.Companies.AsNoTracking().Count(),
                TotalBuildings = db.Buildings.AsNoTracking().Count(),
            }).FirstOrDefaultAsync(httpContextAccessor.HttpContext.RequestAborted)
            ?? new { TotalPlayers = 0, TotalCompanies = 0, TotalBuildings = 0 };

        // Compute percentages.
        foreach (var item in inflowItems)
            item.Percentage = totalInflow > 0m ? decimal.Round(item.Amount / totalInflow * 100m, 1) : 0m;
        foreach (var item in outflowItems)
            item.Percentage = totalOutflow > 0m ? decimal.Round(item.Amount / totalOutflow * 100m, 1) : 0m;

        return new OperationsStatisticsResult
        {
            CurrentTick = currentTick,
            WindowTicks = OperationsWindowTicks,
            InflowItems = inflowItems,
            OutflowItems = outflowItems,
            TotalInflow = totalInflow,
            TotalOutflow = totalOutflow,
            NetFlow = totalInflow - totalOutflow,
            TotalPlayerCount = counts.TotalPlayers,
            TotalCompanyCount = counts.TotalCompanies,
            TotalBuildingCount = counts.TotalBuildings,
        };
    }

    /// <summary>
    /// Returns per-product analytics for the admin Operations Dashboard analytics table.
    /// Aggregates production, sales, costs, and market data over the last 100 ticks.
    /// Requires admin dashboard access.
    /// </summary>
    [Authorize]
    public async Task<AdminProductAnalyticsResult> GetAdminProductAnalytics(
        AdminProductAnalyticsInput? input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] GameAdminAuthorizationService gameAdminAuthorizationService,
        [Service] IMemoryCache cache)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HTTP context is required for admin product analytics.");
        var ct = httpContext.RequestAborted;
        var principal = httpContext.User;
        await gameAdminAuthorizationService.RequireAdminDashboardAccessAsync(db, principal, ct);

        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(s => s.CurrentTick)
            .FirstOrDefaultAsync(ct);

        var windowTicks = Math.Clamp(input?.WindowTicks ?? ProductAnalyticsMaxWindowTicks, 1, ProductAnalyticsMaxWindowTicks);
        var windowStart = Math.Max(0L, currentTick - windowTicks);
        var companyId = input?.CompanyId;
        var productTypeId = input?.ProductTypeId;
        var cityId = input?.CityId;

        var cacheKey = $"admin-product-analytics:{currentTick}:{windowTicks}:{companyId}:{productTypeId}:{cityId}";
        if (cache.TryGetValue<AdminProductAnalyticsResult>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        // Load product types.
        var productTypesQuery = db.ProductTypes.AsNoTracking();
        if (productTypeId.HasValue)
        {
            productTypesQuery = productTypesQuery.Where(pt => pt.Id == productTypeId.Value);
        }

        var productTypes = await productTypesQuery
            .OrderBy(pt => pt.Industry)
            .ThenBy(pt => pt.Name)
            .ToListAsync(ct);

        var selectedCompany = companyId.HasValue
            ? await db.Companies.AsNoTracking()
                .Where(c => c.Id == companyId.Value)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(ct)
            : null;

        // Load production history per product in window.
        var productionByProduct = await (
            from h in db.BuildingUnitResourceHistories.AsNoTracking()
            join b in db.Buildings.AsNoTracking() on h.BuildingId equals b.Id
            where h.Tick >= windowStart
                  && h.ProductTypeId.HasValue
                  && (!productTypeId.HasValue || h.ProductTypeId == productTypeId.Value)
                  && (!companyId.HasValue || b.CompanyId == companyId.Value)
                  && (!cityId.HasValue || b.CityId == cityId.Value)
            group h by h.ProductTypeId!.Value
            into g
            select new
            {
                ProductTypeId = g.Key,
                TotalProduced = g.Sum(h => h.ProducedQuantity),
                ManufacturerCount = g.Select(h => h.BuildingUnitId).Distinct().Count(),
            })
            .ToListAsync(ct);

        // Load public sales per product in window.
        var salesByProduct = await db.PublicSalesRecords
            .AsNoTracking()
            .Where(r => r.Tick >= windowStart
                && r.ProductTypeId.HasValue
                && (!productTypeId.HasValue || r.ProductTypeId == productTypeId.Value)
                && (!companyId.HasValue || r.CompanyId == companyId.Value)
                && (!cityId.HasValue || r.CityId == cityId.Value))
            .GroupBy(r => r.ProductTypeId!.Value)
            .Select(g => new
            {
                ProductTypeId = g.Key,
                TotalSold = g.Sum(r => r.QuantitySold),
                TotalRevenue = g.Sum(r => r.Revenue),
                MarketSize = g.Sum(r => r.Demand),
                SellerCount = g.Select(r => r.BuildingUnitId).Distinct().Count(),
                CityCount = g.Select(r => r.CityId).Distinct().Count(),
                AvgPrice = g.Sum(r => r.QuantitySold) > 0m
                    ? g.Sum(r => r.Revenue) / g.Sum(r => r.QuantitySold)
                    : (decimal?)null,
            })
            .ToListAsync(ct);

        var filteredLedgerEntries = db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.RecordedAtTick >= windowStart && e.ProductTypeId.HasValue
                && (!productTypeId.HasValue || e.ProductTypeId == productTypeId.Value)
                && (!companyId.HasValue || e.CompanyId == companyId.Value))
            .AsQueryable();

        if (cityId.HasValue)
        {
            filteredLedgerEntries = from e in filteredLedgerEntries
                                    join b in db.Buildings.AsNoTracking() on e.BuildingId equals b.Id
                                    where b.CityId == cityId.Value
                                    select e;
        }

        // Load costs by product from ledger (using ProductTypeId linkage).
        var costsByProduct = await filteredLedgerEntries
            .Where(e => e.Category == LedgerCategory.LaborCost
                || e.Category == LedgerCategory.EnergyCost
                || e.Category == LedgerCategory.PurchasingCost)
            .GroupBy(e => new { e.ProductTypeId, e.Category })
            .Select(g => new
            {
                g.Key.ProductTypeId,
                g.Key.Category,
                Amount = g.Sum(e => Math.Abs(e.Amount)),
            })
            .ToListAsync(ct);

        // Load marketing spend per product in window.
        var marketingByProduct = await filteredLedgerEntries
            .Where(e => e.Category == LedgerCategory.Marketing)
            .GroupBy(e => e.ProductTypeId!.Value)
            .Select(g => new { ProductTypeId = g.Key, Total = g.Sum(e => Math.Abs(e.Amount)) })
            .ToListAsync(ct);

        // Load research spend per product in window.
        var researchByProduct = await filteredLedgerEntries
            .Where(e => e.Category == LedgerCategory.UnitUpgrade)
            .GroupBy(e => e.ProductTypeId!.Value)
            .Select(g => new { ProductTypeId = g.Key, Total = g.Sum(e => Math.Abs(e.Amount)) })
            .ToListAsync(ct);

        // Load active seller count for saturation under the same optional company/city/product filters.
        var activeSellersByProduct = await (
            from u in db.BuildingUnits.AsNoTracking()
            join b in db.Buildings.AsNoTracking() on u.BuildingId equals b.Id
            where u.UnitType == UnitType.PublicSales
                  && u.ProductTypeId.HasValue
                  && (!productTypeId.HasValue || u.ProductTypeId == productTypeId.Value)
                  && (!companyId.HasValue || b.CompanyId == companyId.Value)
                  && (!cityId.HasValue || b.CityId == cityId.Value)
            group u by u.ProductTypeId!.Value
            into g
            select new { ProductTypeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProductTypeId, x => x.Count, ct);

        var productionLookup = productionByProduct.ToDictionary(x => x.ProductTypeId);
        var salesLookup = salesByProduct.ToDictionary(x => x.ProductTypeId);
        var marketingLookup = marketingByProduct.ToDictionary(x => x.ProductTypeId, x => x.Total);
        var researchLookup = researchByProduct.ToDictionary(x => x.ProductTypeId, x => x.Total);

        var rows = productTypes.Select(pt =>
        {
            productionLookup.TryGetValue(pt.Id, out var prod);
            salesLookup.TryGetValue(pt.Id, out var sales);
            marketingLookup.TryGetValue(pt.Id, out var marketing);
            researchLookup.TryGetValue(pt.Id, out var research);
            activeSellersByProduct.TryGetValue(pt.Id, out var activeSellers);

            var laborCost = costsByProduct
                .Where(c => c.ProductTypeId == pt.Id && c.Category == LedgerCategory.LaborCost)
                .Sum(c => c.Amount);
            var energyCost = costsByProduct
                .Where(c => c.ProductTypeId == pt.Id && c.Category == LedgerCategory.EnergyCost)
                .Sum(c => c.Amount);
            var materialCost = costsByProduct
                .Where(c => c.ProductTypeId == pt.Id && c.Category == LedgerCategory.PurchasingCost)
                .Sum(c => c.Amount);

            var marketSize = Math.Max(0m, sales?.MarketSize ?? 0m);
            var totalSold = Math.Max(0m, sales?.TotalSold ?? 0m);
            var saturation = 0m;
            if (marketSize > 0m)
            {
                saturation = Math.Clamp(decimal.Round(totalSold / marketSize * 100m, 1), 0m, 100m);
            }

            return new AdminProductAnalyticsRow
            {
                ProductTypeId = pt.Id,
                ProductName = pt.Name,
                Industry = pt.Industry,
                CompanyId = companyId,
                CompanyName = selectedCompany,
                BasePrice = pt.BasePrice,
                TotalProduced = prod?.TotalProduced ?? 0m,
                ActiveManufacturerCount = prod?.ManufacturerCount ?? 0,
                TotalSold = totalSold,
                TotalRevenue = sales?.TotalRevenue ?? 0m,
                AvgSellingPrice = sales?.AvgPrice,
                AvgMarketPrice = sales?.AvgPrice,
                MarketSize = marketSize,
                ActiveSellerCount = activeSellers,
                ActiveCityCount = sales?.CityCount ?? 0,
                TotalMaterialCost = materialCost,
                TotalLaborCost = laborCost,
                TotalEnergyCost = energyCost,
                TotalCost = materialCost + laborCost + energyCost,
                MarketSaturation = saturation,
                TotalMarketingSpend = marketing,
                TotalResearchSpend = research,
            };
        }).ToList();

        var result = new AdminProductAnalyticsResult
        {
            WindowTicks = windowTicks,
            CurrentTick = currentTick,
            Rows = rows,
        };

        cache.Set(cacheKey, result, ProductAnalyticsCacheDuration);
        return result;
    }

    private static List<OperationsMoneyFlowItem> BuildInflowItems(List<LedgerEntry> entries)
    {
        var inflowCategories = new Dictionary<string, (string Label, string[] Categories)>
        {
            ["PUBLIC_SALES"] = ("Public Sales Revenue", [LedgerCategory.Revenue]),
            ["RENT_INCOME"] = ("Rent Income", [LedgerCategory.RentIncome]),
            ["MEDIA_HOUSE"] = ("Media House Income", [LedgerCategory.MediaHouseIncome]),
            ["IPO_RAISE"] = ("IPO Capital Raises", [LedgerCategory.IpoRaise]),
            ["FOUNDER"] = ("Founder Contributions", [LedgerCategory.FounderContribution]),
            ["GRID_INCOME"] = ("Grid Surplus Income", [LedgerCategory.GridSurplusIncome]),
            ["LOAN_INTEREST_INCOME"] = ("Loan Interest Income", [LedgerCategory.LoanInterestIncome]),
            ["DEPOSIT_INTEREST"] = ("Deposit Interest", [LedgerCategory.DepositInterestReceived]),
            ["STOCK_SALE"] = ("Stock Sales", [LedgerCategory.StockSale]),
            ["BUILDING_SALE"] = ("Building Sales", [LedgerCategory.BuildingSale]),
        };

        var result = new List<OperationsMoneyFlowItem>();
        foreach (var (key, (label, cats)) in inflowCategories)
        {
            var matching = entries
                .Where(e => cats.Contains(e.Category) && e.Amount > 0m)
                .ToList();
            if (matching.Count == 0) continue;
            result.Add(new OperationsMoneyFlowItem
            {
                Category = key,
                Label = label,
                Amount = decimal.Round(matching.Sum(e => e.Amount), 2),
                EntryCount = matching.Count,
            });
        }
        return [.. result.OrderByDescending(x => x.Amount)];
    }

    private static List<OperationsMoneyFlowItem> BuildOutflowItems(
        List<LedgerEntry> entries,
        decimal fxFeeAmount,
        int fxFeeEntryCount)
    {
        var outflowCategories = new Dictionary<string, (string Label, string[] Categories)>
        {
            ["LABOR"] = ("Labor Costs", [LedgerCategory.LaborCost]),
            ["TAX"] = ("Tax Payments", [LedgerCategory.Tax]),
            ["ENERGY"] = ("Energy Costs", [LedgerCategory.EnergyCost]),
            ["RESEARCH"] = ("Research & Upgrades", [LedgerCategory.UnitUpgrade]),
            ["PURCHASING"] = ("Raw Material Purchasing", [LedgerCategory.PurchasingCost]),
            ["MARKETING"] = ("Marketing Spend", [LedgerCategory.Marketing]),
            ["SHIPPING"] = ("Shipping & Logistics", [LedgerCategory.ShippingCost]),
            ["MEDIA_CONTENT"] = ("Media House Content", [LedgerCategory.MediaHouseContent]),
            ["LOAN_INTEREST"] = ("Loan Interest Expense", [LedgerCategory.LoanInterestExpense]),
            ["LOAN_PENALTY"] = ("Loan Penalties", [LedgerCategory.LoanPenalty]),
            ["GRID_FINE"] = ("Grid Fines", [LedgerCategory.GridFine]),
            ["FUEL"] = ("Fuel Costs", [LedgerCategory.FuelCost]),
            ["MAINTENANCE"] = ("Property Maintenance", [LedgerCategory.PropertyMaintenance]),
            ["STOCK_PURCHASE"] = ("Stock Purchases", [LedgerCategory.StockPurchase]),
        };

        var result = new List<OperationsMoneyFlowItem>();
        foreach (var (key, (label, cats)) in outflowCategories)
        {
            var matching = entries
                .Where(e => cats.Contains(e.Category) && e.Amount < 0m)
                .ToList();
            if (matching.Count == 0) continue;
            result.Add(new OperationsMoneyFlowItem
            {
                Category = key,
                Label = label,
                Amount = decimal.Round(Math.Abs(matching.Sum(e => e.Amount)), 2),
                EntryCount = matching.Count,
            });
        }

        if (fxFeeAmount > 0m && fxFeeEntryCount > 0)
        {
            result.Add(new OperationsMoneyFlowItem
            {
                Category = "FX_FEES",
                Label = "FX Fees",
                Amount = decimal.Round(fxFeeAmount, 2),
                EntryCount = fxFeeEntryCount,
            });
        }

        return [.. result.OrderByDescending(x => x.Amount)];
    }
}

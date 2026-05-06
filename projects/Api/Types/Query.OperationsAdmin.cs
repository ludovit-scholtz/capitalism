using Api.Data;
using Api.Data.Entities;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    private const int OperationsWindowTicks = 100;

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

        // Load product types.
        var productTypes = await db.ProductTypes
            .AsNoTracking()
            .OrderBy(pt => pt.Industry)
            .ThenBy(pt => pt.Name)
            .ToListAsync(httpContextAccessor.HttpContext.RequestAborted);

        // Load production history per product in window.
        var productionByProduct = await db.BuildingUnitResourceHistories
            .AsNoTracking()
            .Where(h => h.Tick >= windowStart && h.ProductTypeId.HasValue)
            .GroupBy(h => h.ProductTypeId!.Value)
            .Select(g => new
            {
                ProductTypeId = g.Key,
                TotalProduced = g.Sum(h => h.ProducedQuantity),
                ManufacturerCount = g.Select(h => h.BuildingUnitId).Distinct().Count(),
            })
            .ToListAsync(httpContextAccessor.HttpContext.RequestAborted);

        // Load public sales per product in window.
        var salesByProduct = await db.PublicSalesRecords
            .AsNoTracking()
            .Where(r => r.Tick >= windowStart && r.ProductTypeId.HasValue)
            .GroupBy(r => r.ProductTypeId!.Value)
            .Select(g => new
            {
                ProductTypeId = g.Key,
                TotalSold = g.Sum(r => r.QuantitySold),
                TotalRevenue = g.Sum(r => r.Revenue),
                SellerCount = g.Select(r => r.BuildingUnitId).Distinct().Count(),
                CityCount = g.Select(r => r.CityId).Distinct().Count(),
                AvgPrice = g.Sum(r => r.QuantitySold) > 0m
                    ? g.Sum(r => r.Revenue) / g.Sum(r => r.QuantitySold)
                    : (decimal?)null,
            })
            .ToListAsync(httpContextAccessor.HttpContext.RequestAborted);

        // Load costs by product from ledger (using ProductTypeId linkage).
        var costsByProduct = await db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.RecordedAtTick >= windowStart && e.ProductTypeId.HasValue
                && (e.Category == LedgerCategory.LaborCost
                    || e.Category == LedgerCategory.EnergyCost
                    || e.Category == LedgerCategory.PurchasingCost))
            .GroupBy(e => new { e.ProductTypeId, e.Category })
            .Select(g => new
            {
                g.Key.ProductTypeId,
                g.Key.Category,
                Amount = g.Sum(e => Math.Abs(e.Amount)),
            })
            .ToListAsync(httpContextAccessor.HttpContext.RequestAborted);

        // Load marketing spend per product in window.
        var marketingByProduct = await db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.RecordedAtTick >= windowStart && e.ProductTypeId.HasValue
                && e.Category == LedgerCategory.Marketing)
            .GroupBy(e => e.ProductTypeId!.Value)
            .Select(g => new { ProductTypeId = g.Key, Total = g.Sum(e => Math.Abs(e.Amount)) })
            .ToListAsync(httpContextAccessor.HttpContext.RequestAborted);

        // Load active seller count for saturation (current, not windowed).
        var activeSellersByProduct = await db.BuildingUnits
            .AsNoTracking()
            .Where(u => u.UnitType == UnitType.PublicSales && u.ProductTypeId.HasValue)
            .GroupBy(u => u.ProductTypeId!.Value)
            .Select(g => new { ProductTypeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProductTypeId, x => x.Count, httpContextAccessor.HttpContext.RequestAborted);

        var productionLookup = productionByProduct.ToDictionary(x => x.ProductTypeId);
        var salesLookup = salesByProduct.ToDictionary(x => x.ProductTypeId);
        var marketingLookup = marketingByProduct.ToDictionary(x => x.ProductTypeId, x => x.Total);

        var rows = productTypes.Select(pt =>
        {
            productionLookup.TryGetValue(pt.Id, out var prod);
            salesLookup.TryGetValue(pt.Id, out var sales);
            marketingLookup.TryGetValue(pt.Id, out var marketing);
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

            // Market saturation: rough heuristic — ratio of active sellers to estimated city demand.
            // Uses base price as a proxy; higher base price → lower natural demand.
            var saturation = 0m;
            if (activeSellers > 0 && pt.BasePrice > 0m)
            {
                // Rough city demand constant; each seller can serve ~20 units/tick at level 1.
                const decimal approxUnitsPerSellerPerTick = 20m;
                var estimatedSupply = activeSellers * approxUnitsPerSellerPerTick;
                // Math.Max(1, ...) ensures we do not divide by zero for zero-sales products.
                // When no sales occurred, saturation equals the estimated supply contribution alone,
                // which correctly flags the product as potentially over-supplied.
                var demandProxy = Math.Max(1m, sales?.TotalSold ?? 0m);
                saturation = Math.Clamp(decimal.Round(estimatedSupply / (demandProxy + estimatedSupply) * 100m, 1), 0m, 100m);
            }

            return new AdminProductAnalyticsRow
            {
                ProductTypeId = pt.Id,
                ProductName = pt.Name,
                Industry = pt.Industry,
                BasePrice = pt.BasePrice,
                TotalProduced = prod?.TotalProduced ?? 0m,
                ActiveManufacturerCount = prod?.ManufacturerCount ?? 0,
                TotalSold = sales?.TotalSold ?? 0m,
                TotalRevenue = sales?.TotalRevenue ?? 0m,
                AvgSellingPrice = sales?.AvgPrice,
                ActiveSellerCount = sales?.SellerCount ?? 0,
                ActiveCityCount = sales?.CityCount ?? 0,
                TotalMaterialCost = materialCost,
                TotalLaborCost = laborCost,
                TotalEnergyCost = energyCost,
                TotalCost = materialCost + laborCost + energyCost,
                MarketSaturation = saturation,
                TotalMarketingSpend = marketing,
            };
        }).ToList();

        return new AdminProductAnalyticsResult
        {
            WindowTicks = OperationsWindowTicks,
            CurrentTick = currentTick,
            Rows = rows,
        };
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

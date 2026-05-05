using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Returns product-level analytics for a MANUFACTURING unit.
    /// Shows labor/energy cost history, production quantity, and estimated economics
    /// (basePrice × produced) so players can evaluate manufacturing profitability.
    /// </summary>
    [Authorize]
    public async Task<UnitProductAnalytics?> GetUnitProductAnalytics(
        Guid unitId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var unit = await db.BuildingUnits
            .Include(u => u.Building)
            .ThenInclude(b => b.Company)
            .Include(u => u.Building)
            .ThenInclude(b => b.City)
            .FirstOrDefaultAsync(u => u.Id == unitId);

        if (unit is null || unit.Building.Company.PlayerId != userId) return null;

        // Currently only supported for MANUFACTURING units
        if (unit.UnitType != UnitType.Manufacturing) return null;

        var productTypeId = unit.ProductTypeId;
        string? productName = null;
        decimal? basePrice = null;

        if (productTypeId.HasValue)
        {
            var product = await db.ProductTypes.FindAsync(productTypeId.Value);
            if (product is not null)
            {
                productName = product.Name;
                basePrice = product.BasePrice;
            }
        }

        // Load cost ledger entries for this unit (last 100 ticks, ordered descending then ascending)
        var ledgerEntries = await db.LedgerEntries
            .Where(e => e.BuildingUnitId == unitId
                && (e.Category == LedgerCategory.LaborCost || e.Category == LedgerCategory.EnergyCost))
            .OrderByDescending(e => e.RecordedAtTick)
            .Take(100)
            .ToListAsync();

        // Load resource history for production quantities for this unit
        var resourceHistory = await db.BuildingUnitResourceHistories
            .Where(h => h.BuildingUnitId == unitId && h.ProducedQuantity > 0)
            .OrderByDescending(h => h.Tick)
            .Take(100)
            .ToListAsync();

        // Merge all ticks from both sources
        var allTicks = ledgerEntries.Select(e => e.RecordedAtTick)
            .Union(resourceHistory.Select(h => h.Tick))
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        var snapshots = allTicks.Select(tick =>
        {
            var labor = ledgerEntries
                .Where(e => e.RecordedAtTick == tick && e.Category == LedgerCategory.LaborCost)
                .Sum(e => -e.Amount);
            var energy = ledgerEntries
                .Where(e => e.RecordedAtTick == tick && e.Category == LedgerCategory.EnergyCost)
                .Sum(e => -e.Amount);
            var produced = resourceHistory
                .Where(h => h.Tick == tick)
                .Sum(h => h.ProducedQuantity);
            var totalCost = labor + energy;
            decimal? estRevenue = basePrice.HasValue ? Math.Round(produced * basePrice.Value, 2) : null;
            decimal? estProfit = estRevenue.HasValue ? Math.Round(estRevenue.Value - totalCost, 2) : null;

            return new UnitProductTickSnapshot
            {
                Tick = tick,
                LaborCost = Math.Round(labor, 2),
                EnergyCost = Math.Round(energy, 2),
                TotalCost = Math.Round(totalCost, 2),
                QuantityProduced = produced,
                EstimatedRevenue = estRevenue,
                EstimatedProfit = estProfit,
            };
        }).ToList();

        var totalCostSum = Math.Round(snapshots.Sum(s => s.TotalCost), 2);
        var totalProduced = snapshots.Sum(s => s.QuantityProduced);
        decimal? estRevSum = basePrice.HasValue ? Math.Round(totalProduced * basePrice.Value, 2) : null;
        decimal? estProfitSum = estRevSum.HasValue ? Math.Round(estRevSum.Value - totalCostSum, 2) : null;

        return new UnitProductAnalytics
        {
            BuildingUnitId = unit.Id,
            UnitType = unit.UnitType,
            ProductTypeId = productTypeId,
            ProductName = productName,
            DataFromTick = allTicks.Count > 0 ? allTicks.First() : 0,
            DataToTick = allTicks.Count > 0 ? allTicks.Last() : 0,
            TotalCost = totalCostSum,
            TotalQuantityProduced = totalProduced,
            EstimatedRevenue = estRevSum,
            EstimatedProfit = estProfitSum,
            Snapshots = snapshots,
            CityCurrencyCode = unit.Building.City?.CurrencyCode ?? "EUR",
        };
    }

    /// <summary>
    /// Returns campaign analytics for all PUBLIC_SALES units of a company.
    /// Aggregates brand quality, pricing, and sales performance so players can
    /// compare demand drivers side by side and decide where to invest.
    /// Requires authentication and company ownership.
    /// </summary>
    [Authorize]
    public async Task<CampaignAnalyticsResult?> GetCampaignAnalytics(
        Guid companyId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == companyId && c.PlayerId == userId);
        if (company is null) return null;

        // Load all PUBLIC_SALES units for the company's buildings.
        var buildings = await db.Buildings
            .Include(b => b.City)
            .Where(b => b.CompanyId == companyId)
            .ToListAsync();

        var buildingIds = buildings.Select(b => b.Id).ToList();

        var publicSalesUnits = await db.BuildingUnits
            .Where(u => buildingIds.Contains(u.BuildingId) && u.UnitType == Data.Entities.UnitType.PublicSales)
            .ToListAsync();

        if (publicSalesUnits.Count == 0)
            return new CampaignAnalyticsResult
            {
                CompanyId = companyId,
                WindowTicks = Engine.GameConstants.CampaignAnalyticsWindowTicks,
                GlobalRecommendation = "No public sales units found. Place a sales shop to start selling products.",
            };

        var unitIds = publicSalesUnits.Select(u => u.Id).ToList();

        // Determine window: current tick − window size.
        var gameState = await db.GameStates.FindAsync(1);
        var currentTick = gameState?.CurrentTick ?? 0L;
        var windowStart = currentTick - Engine.GameConstants.CampaignAnalyticsWindowTicks;

        // Load recent sales records for all units.
        var salesRecords = await db.PublicSalesRecords
            .Where(r => unitIds.Contains(r.BuildingUnitId) && r.Tick >= windowStart)
            .ToListAsync();

        // Load marketing spend ledger entries for all company buildings in window.
        var marketingLedger = await db.LedgerEntries
            .Where(e => e.CompanyId == companyId
                && e.Category == Data.Entities.LedgerCategory.Marketing
                && e.RecordedAtTick >= windowStart)
            .ToListAsync();

        // Load all product types referenced by units or sales records.
        var productTypeIds = publicSalesUnits
            .Where(u => u.ProductTypeId.HasValue).Select(u => u.ProductTypeId!.Value)
            .Concat(salesRecords
                .Where(r => r.ProductTypeId.HasValue && r.ProductTypeId.Value != Guid.Empty)
                .Select(r => r.ProductTypeId!.Value))
            .Distinct().ToList();
        var productTypes = productTypeIds.Count > 0
            ? await db.ProductTypes.Where(pt => productTypeIds.Contains(pt.Id)).ToDictionaryAsync(pt => pt.Id)
            : [];

        // Load brand data keyed by (companyId, productTypeId).
        var brands = await db.Brands
            .Where(b => b.CompanyId == companyId && b.ProductTypeId.HasValue
                && productTypeIds.Contains(b.ProductTypeId!.Value))
            .ToDictionaryAsync(b => b.ProductTypeId!.Value);

        // Load building lots for population index (optional).
        var lots = await db.BuildingLots
            .Where(l => l.BuildingId.HasValue && buildingIds.Contains(l.BuildingId!.Value))
            .ToDictionaryAsync(l => l.BuildingId!.Value);

        // Aggregate marketing spend per building unit (approximate: per building).
        var mktSpendByBuilding = marketingLedger
            .Where(e => e.BuildingId.HasValue)
            .GroupBy(e => e.BuildingId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(e => -e.Amount)); // amounts stored negative

        // Build per-unit rows.
        var rows = new List<CampaignAnalyticsRow>();
        foreach (var unit in publicSalesUnits)
        {
            var building = buildings.FirstOrDefault(b => b.Id == unit.BuildingId);
            if (building is null) continue;

            var city = building.City;

            // Resolve product type.
            var productTypeId = unit.ProductTypeId
                ?? salesRecords.Where(r => r.BuildingUnitId == unit.Id).OrderByDescending(r => r.Tick).FirstOrDefault()?.ProductTypeId;
            productTypes.TryGetValue(productTypeId ?? Guid.Empty, out var productType);

            // Sales in window.
            var unitSales = salesRecords.Where(r => r.BuildingUnitId == unit.Id).ToList();
            var revenue = unitSales.Sum(r => r.Revenue);
            var quantity = unitSales.Sum(r => r.QuantitySold);

            // Utilisation rate.
            var capacity = Engine.GameConstants.SalesCapacity(unit.Level);
            var utilRate = capacity > 0 && Engine.GameConstants.CampaignAnalyticsWindowTicks > 0
                ? Math.Clamp(quantity / (capacity * Engine.GameConstants.CampaignAnalyticsWindowTicks), 0m, 1m)
                : 0m;

            // Trend direction.
            var orderedSales = unitSales.OrderBy(r => r.Tick).ToList();
            var trendDirection = "NO_DATA";
            if (orderedSales.Count >= 2)
            {
                var half = orderedSales.Count / 2;
                var recentRevenue = orderedSales.Skip(half).Sum(r => r.Revenue);
                var priorRevenue = orderedSales.Take(half).Sum(r => r.Revenue);
                trendDirection = recentRevenue > priorRevenue * 1.05m ? "UP"
                    : recentRevenue < priorRevenue * 0.95m ? "DOWN"
                    : "FLAT";
            }

            // Latest trend factor.
            decimal? trendFactor = null;
            var latestRecord = orderedSales.LastOrDefault();
            if (latestRecord is not null)
                trendFactor = latestRecord.TrendFactor;

            // Demand signal (from latest record utilisation vs capacity).
            var demandSignal = "NO_DATA";
            if (unitSales.Count > 0)
            {
                if (utilRate >= 0.95m)
                    demandSignal = "SUPPLY_CONSTRAINED";
                else if (utilRate >= 0.6m)
                    demandSignal = "STRONG";
                else if (utilRate >= 0.3m)
                    demandSignal = "MODERATE";
                else
                    demandSignal = "WEAK";
            }

            // Brand data.
            brands.TryGetValue(productTypeId ?? Guid.Empty, out var brand);
            decimal? brandAwareness = brand?.Awareness;
            decimal? marketingQuality = brand is not null ? Math.Clamp(brand.MarketingQuality, 0m, 1m) : null;
            decimal? rdQuality = brand is not null ? Math.Clamp(brand.Quality, 0m, 1m) : null;
            decimal? brandQuality = brand is not null
                ? Math.Clamp(1m - (1m - rdQuality!.Value) * (1m - marketingQuality!.Value), 0m, 1m)
                : null;

            // Pricing.
            decimal? currentPrice = unit.MinPrice;
            decimal? basePrice = productType?.BasePrice;
            decimal? priceIndex = currentPrice.HasValue && basePrice.HasValue && basePrice > 0
                ? Engine.PublicSalesPricingModel.ComputePriceIndex(basePrice.Value, currentPrice.Value, productType!.PriceElasticity)
                : null;
            decimal? pricePremiumPct = currentPrice.HasValue && basePrice.HasValue && basePrice > 0
                ? (currentPrice.Value - basePrice.Value) / basePrice.Value * 100m
                : null;

            // Brand revenue boost estimate.
            decimal? brandRevenueBoost = brandQuality.HasValue
                ? brandQuality.Value * Engine.GameConstants.BrandQualityBoostFactor
                : null;

            // Marketing spend for this building.
            decimal? mktSpend = mktSpendByBuilding.TryGetValue(unit.BuildingId, out var spend) ? spend : null;
            if (mktSpend == 0m) mktSpend = null;

            // Campaign impact.
            var campaignImpact = "NONE";
            if (brandAwareness.HasValue && brandQuality.HasValue)
            {
                if (brandAwareness.Value >= 0.6m && brandQuality.Value >= 0.4m)
                    campaignImpact = "STRONG";
                else if (brandAwareness.Value >= 0.3m || brandQuality.Value >= 0.2m)
                    campaignImpact = "MODERATE";
                else if (brandAwareness.Value > 0m || brandQuality.Value > 0m)
                    campaignImpact = "WEAK";
            }

            // Brand vs price balance.
            var bvp = "NO_BRAND";
            if (brandQuality.HasValue && priceIndex.HasValue)
            {
                var isPremiumprice = pricePremiumPct.HasValue && pricePremiumPct.Value > 5m;
                var isDiscount = pricePremiumPct.HasValue && pricePremiumPct.Value < -5m;
                if (brandQuality.Value >= 0.5m && isPremiumprice)
                    bvp = "PREMIUM_JUSTIFIED";
                else if (brandQuality.Value < 0.3m && isPremiumprice)
                    bvp = "PREMIUM_RISKY";
                else if (brandQuality.Value >= 0.4m && isDiscount)
                    bvp = "DISCOUNT_WITH_BRAND";
                else if (brandAwareness.HasValue && brandAwareness.Value > 0m && brandQuality.Value < 0.2m)
                    bvp = "BRAND_BUILDING";
                else
                    bvp = "COMPETITIVE_BASELINE";
            }
            else if (brandQuality.HasValue)
            {
                bvp = brandAwareness > 0m ? "BRAND_BUILDING" : "COMPETITIVE_BASELINE";
            }

            // Demand drivers for top factor identification.
            var lot = lots.TryGetValue(unit.BuildingId, out var l) ? l : null;
            decimal? populationIndex = lot?.PopulationIndex;
            var inventory = await db.Inventories.Where(i => i.BuildingUnitId == unit.Id).FirstOrDefaultDeterministicAsync();
            decimal? inventoryQuality = inventory?.Quality;

            var demandDrivers = ComputeDemandDrivers(
                unit, productType, inventoryQuality, brandAwareness, populationIndex,
                [], null, city?.BaseSalaryPerManhour, 0m, city?.Population ?? 0L,
                trendFactor, brandQuality);

            var topPositive = demandDrivers
                .Where(d => d.Impact == "POSITIVE")
                .OrderByDescending(d => d.Score)
                .FirstOrDefault()?.Factor;
            var topNegative = demandDrivers
                .Where(d => d.Impact == "NEGATIVE")
                .OrderByDescending(d => d.Score)
                .FirstOrDefault()?.Factor;

            // Recommendation text.
            var rec = BuildCampaignRecommendation(bvp, campaignImpact, demandSignal, topNegative, trendDirection);

            rows.Add(new CampaignAnalyticsRow
            {
                BuildingUnitId = unit.Id,
                BuildingId = unit.BuildingId,
                BuildingName = building.Name,
                ProductName = productType?.Name,
                ProductTypeId = productTypeId,
                CityName = city?.Name ?? string.Empty,
                BrandAwareness = brandAwareness,
                BrandQuality = brandQuality,
                MarketingQuality = marketingQuality,
                CurrentPrice = currentPrice,
                BasePrice = basePrice,
                PriceIndex = priceIndex,
                PricePremiumPct = pricePremiumPct,
                RevenueLastTicks = revenue,
                QuantityLastTicks = quantity,
                UtilizationRate = utilRate,
                TrendDirection = trendDirection,
                TrendFactor = trendFactor,
                DemandSignal = demandSignal,
                TopPositiveFactor = topPositive,
                TopNegativeFactor = topNegative,
                MarketingSpendLastTicks = mktSpend,
                BrandRevenueBoost = brandRevenueBoost,
                CampaignImpact = campaignImpact,
                BrandVsPriceBalance = bvp,
                Recommendation = rec,
                CityCurrencyCode = city?.CurrencyCode ?? "EUR",
            });
        }

        // Global summary.
        var totalRevenue = rows.Sum(r => r.RevenueLastTicks);
        var totalSpend = marketingLedger.Sum(e => -e.Amount);
        var bestCity = rows.OrderByDescending(r => r.RevenueLastTicks).FirstOrDefault()?.CityName;
        var bestProduct = rows.OrderByDescending(r => r.RevenueLastTicks).FirstOrDefault()?.ProductName;
        var globalRec = BuildGlobalRecommendation(rows, totalRevenue, totalSpend);

        return new CampaignAnalyticsResult
        {
            CompanyId = companyId,
            WindowTicks = Engine.GameConstants.CampaignAnalyticsWindowTicks,
            TotalRevenue = totalRevenue,
            TotalMarketingSpend = totalSpend,
            BestPerformingCity = bestCity,
            BestPerformingProduct = bestProduct,
            GlobalRecommendation = globalRec,
            Rows = rows.OrderByDescending(r => r.RevenueLastTicks).ToList(),
        };
    }

    private static string BuildCampaignRecommendation(
        string bvp, string campaignImpact, string demandSignal, string? topNegative, string trendDirection)
    {
        if (bvp == "PREMIUM_RISKY")
            return "Price is above market but brand quality is weak — demand is suffering. Either lower the price or invest in marketing to build brand prestige before maintaining premium pricing.";
        if (bvp == "PREMIUM_JUSTIFIED")
            return "Strong brand quality is supporting premium pricing. Monitor market share and consider raising the price further if utilisation is still high.";
        if (bvp == "DISCOUNT_WITH_BRAND")
            return "Brand quality is strong but price is discounted — you may be leaving margin on the table. Consider a moderate price increase to capture the value your brand commands.";
        if (bvp == "NO_BRAND")
            return "No brand detected for this product. Creating a brand and investing in marketing will increase awareness, demand, and long-term brand quality.";
        if (demandSignal == "SUPPLY_CONSTRAINED")
            return "Sales are at capacity — demand exceeds supply. Consider expanding with additional sales units or upgrading existing ones to higher levels.";
        if (demandSignal == "WEAK" && topNegative == "PRICE")
            return "Demand is weak primarily due to pricing. Try lowering the price closer to the market baseline to attract more buyers.";
        if (demandSignal == "WEAK" && topNegative == "BRAND")
            return "Demand is weak due to low brand visibility. Increase your marketing budget to grow brand awareness and quality over time.";
        if (demandSignal == "WEAK" && topNegative == "COMPETITION")
            return "Competitors are capturing most of the market. Differentiate through lower price, higher product quality, or stronger brand investment.";
        if (campaignImpact == "STRONG" && trendDirection == "UP")
            return "Campaign performing well — strong brand with upward trend. Maintain investment and consider expanding to nearby cities.";
        if (campaignImpact == "MODERATE")
            return "Campaign is showing moderate impact. Increase marketing budget or improve brand quality to boost competitiveness.";
        return "Monitor sales trends and compare brand quality vs pricing to identify the highest-ROI lever for improvement.";
    }

    private static string BuildGlobalRecommendation(List<CampaignAnalyticsRow> rows, decimal totalRevenue, decimal totalSpend)
    {
        if (rows.Count == 0)
            return "No campaign data available. Place public sales units and connect marketing to start building your brand.";

        var weakRows = rows.Count(r => r.DemandSignal == "WEAK");
        var strongRows = rows.Count(r => r.CampaignImpact is "STRONG" or "MODERATE");
        var noBrandRows = rows.Count(r => r.BrandVsPriceBalance == "NO_BRAND");
        var premiumRiskyRows = rows.Count(r => r.BrandVsPriceBalance == "PREMIUM_RISKY");

        if (premiumRiskyRows > 0 && premiumRiskyRows >= rows.Count / 2)
            return $"{premiumRiskyRows} product line(s) are priced above market without strong brand support — reduce prices or invest heavily in brand to protect demand.";
        if (noBrandRows == rows.Count)
            return "None of your sales units have an active brand. Add MARKETING units to your shops and create brands to unlock awareness and demand multipliers.";
        if (weakRows > rows.Count / 2)
            return "Most units show weak demand. Review pricing relative to base prices and grow brand awareness through consistent marketing investment.";
        if (strongRows >= rows.Count / 2)
            return "Portfolio is performing well overall. Look for opportunities to expand to new cities or introduce new products where brand strength can be leveraged.";
        if (totalSpend > 0m && totalRevenue > 0m)
        {
            var roi = totalRevenue / totalSpend;
            if (roi > 20m)
                return "Excellent marketing ROI. Brand investment is generating strong returns — scale up campaigns in high-performing markets.";
        }
        return "Balanced portfolio. Focus brand investment on your strongest-demand cities and products to maximise returns.";
    }
}

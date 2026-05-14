using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Returns the weighted-average clearing price for a product in a city over the last N ticks.
    /// Aggregates all <see cref="PublicSalesRecord"/> rows for that product and city.
    /// Returns null when no sales have been recorded yet.
    /// </summary>
    public async Task<MarketPriceResult?> GetMarketPrice(
        Guid cityId,
        Guid productTypeId,
        int lastNTicks,
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        var clampedTicks = Math.Clamp(lastNTicks, 1, 500);

        var gameState = await db.GameStates.AsNoTracking().FirstOrDefaultDeterministicAsync(cancellationToken);
        if (gameState is null) return null;

        var fromTick = gameState.CurrentTick - clampedTicks;

        var records = await db.PublicSalesRecords
            .AsNoTracking()
            .Where(r => r.CityId == cityId
                        && r.ProductTypeId == productTypeId
                        && r.Tick > fromTick)
            .ToListAsync(cancellationToken);

        if (records.Count == 0) return null;

        var totalRevenue = records.Sum(r => r.Revenue);
        var totalQuantity = records.Sum(r => r.QuantitySold);
        var weightedAvgPrice = totalQuantity > 0 ? totalRevenue / totalQuantity : 0m;

        var productType = await db.ProductTypes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productTypeId, cancellationToken);
        var city = await db.Cities.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cityId, cancellationToken);

        return new MarketPriceResult
        {
            CityId = cityId,
            ProductTypeId = productTypeId,
            ProductName = productType?.Name ?? string.Empty,
            ClearingPrice = Math.Round(weightedAvgPrice, 4),
            TotalVolume = totalQuantity,
            TotalRevenue = totalRevenue,
            SellerCount = records.Select(r => r.CompanyId).Distinct().Count(),
            CurrencyCode = city?.CurrencyCode ?? "EUR",
            FromTick = fromTick,
            ToTick = gameState.CurrentTick,
        };
    }

    /// <summary>
    /// Returns per-tick aggregated price and volume data for a product in a city.
    /// Useful for charting price history over time.
    /// </summary>
    public async Task<List<MarketPriceHistoryPoint>> GetMarketPriceHistory(
        Guid cityId,
        Guid productTypeId,
        int lastNTicks,
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        var clampedTicks = Math.Clamp(lastNTicks, 1, 500);

        var gameState = await db.GameStates.AsNoTracking().FirstOrDefaultDeterministicAsync(cancellationToken);
        if (gameState is null) return [];

        var fromTick = gameState.CurrentTick - clampedTicks;

        var records = await db.PublicSalesRecords
            .AsNoTracking()
            .Where(r => r.CityId == cityId
                        && r.ProductTypeId == productTypeId
                        && r.Tick > fromTick)
            .ToListAsync(cancellationToken);

        if (records.Count == 0) return [];

        return records
            .GroupBy(r => r.Tick)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var revenue = g.Sum(r => r.Revenue);
                var qty = g.Sum(r => r.QuantitySold);
                return new MarketPriceHistoryPoint
                {
                    Tick = g.Key,
                    ClearingPrice = qty > 0 ? Math.Round(revenue / qty, 4) : 0m,
                    TotalVolume = qty,
                    TotalRevenue = revenue,
                    SellerCount = g.Select(r => r.CompanyId).Distinct().Count(),
                };
            })
            .ToList();
    }

    /// <summary>
    /// Returns a demand summary for a city: top products sorted by demand with satisfaction rates.
    /// Useful for the city map demand panel and the market dashboard.
    /// </summary>
    public async Task<CityDemandSummaryResult?> GetCityDemandSummary(
        Guid cityId,
        int topN,
        int lastNTicks,
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        var clampedTop = Math.Clamp(topN, 1, 20);
        var clampedTicks = Math.Clamp(lastNTicks, 1, 500);

        var gameState = await db.GameStates.AsNoTracking().FirstOrDefaultDeterministicAsync(cancellationToken);
        var city = await db.Cities.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cityId, cancellationToken);
        if (gameState is null || city is null) return null;

        var fromTick = gameState.CurrentTick - clampedTicks;

        // Fetch recent sales records for this city grouped by product type.
        var records = await db.PublicSalesRecords
            .AsNoTracking()
            .Where(r => r.CityId == cityId && r.ProductTypeId != null && r.Tick > fromTick)
            .Include(r => r.ProductType)
            .Include(r => r.Company)
            .ToListAsync(cancellationToken);

        if (records.Count == 0)
        {
            return new CityDemandSummaryResult
            {
                CityId = cityId,
                CityName = city.Name,
                CurrencyCode = city.CurrencyCode,
                FromTick = fromTick,
                ToTick = gameState.CurrentTick,
                Products = [],
            };
        }

        var grouped = records
            .GroupBy(r => r.ProductTypeId!.Value)
            .Select(g =>
            {
                var totalDemand = g.Sum(r => r.Demand);
                var totalSold = g.Sum(r => r.QuantitySold);
                var totalRevenue = g.Sum(r => r.Revenue);
                var satisfactionRate = totalDemand > 0 ? Math.Clamp(totalSold / totalDemand, 0m, 1m) : 0m;
                var avgPrice = totalSold > 0 ? totalRevenue / totalSold : 0m;
                var sellerCount = g.Select(r => r.CompanyId).Distinct().Count();
                var topCompetitor = g
                    .GroupBy(record => record.CompanyId)
                    .Select(group => new
                    {
                        CompanyName = group.Select(record => record.Company != null ? record.Company.Name : "Unknown").FirstOrDefault() ?? "Unknown",
                        Revenue = group.Sum(record => record.Revenue)
                    })
                    .OrderByDescending(item => item.Revenue)
                    .FirstOrDefault();
                var topCompetitorShare = totalRevenue > 0m && topCompetitor is not null
                    ? decimal.Round((topCompetitor.Revenue / totalRevenue) * 100m, 2, MidpointRounding.AwayFromZero)
                    : 0m;

                return new ProductDemandEntry
                {
                    ProductTypeId = g.Key,
                    ProductName = g.First().ProductType?.Name ?? string.Empty,
                    Industry = g.First().ProductType?.Industry ?? string.Empty,
                    TotalDemand = totalDemand,
                    TotalQuantitySold = totalSold,
                    SatisfactionRate = Math.Round(satisfactionRate, 4),
                    AverageClearingPrice = Math.Round(avgPrice, 4),
                    TotalRevenue = totalRevenue,
                    SellerCount = sellerCount,
                    TopCompetitorCompanyName = topCompetitor?.CompanyName,
                    TopCompetitorMarketSharePercent = topCompetitorShare,
                };
            })
            .OrderByDescending(p => p.TotalDemand)
            .Take(clampedTop)
            .ToList();

        return new CityDemandSummaryResult
        {
            CityId = cityId,
            CityName = city.Name,
            CurrencyCode = city.CurrencyCode,
            FromTick = fromTick,
            ToTick = gameState.CurrentTick,
            Products = grouped,
        };
    }

    /// <summary>
    /// Returns the market overview for all cities and their top products.
    /// Used by the Market Dashboard to give a comprehensive view of the entire economy.
    /// </summary>
    public async Task<List<CityDemandSummaryResult>> GetMarketOverview(
        int topN,
        int lastNTicks,
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        var clampedTop = Math.Clamp(topN, 1, 20);
        var clampedTicks = Math.Clamp(lastNTicks, 1, 500);

        var cities = await db.Cities.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
        var results = new List<CityDemandSummaryResult>(cities.Count);

        var gameState = await db.GameStates.AsNoTracking().FirstOrDefaultDeterministicAsync(cancellationToken);
        if (gameState is null) return results;

        var fromTick = gameState.CurrentTick - clampedTicks;

        // Load all recent sales records for all cities in one query to avoid N+1.
        var allRecords = await db.PublicSalesRecords
            .AsNoTracking()
            .Where(r => r.ProductTypeId != null && r.Tick > fromTick)
            .Include(r => r.ProductType)
            .Include(r => r.Company)
            .ToListAsync(cancellationToken);

        var recordsByCity = allRecords.GroupBy(r => r.CityId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var city in cities)
        {
            recordsByCity.TryGetValue(city.Id, out var cityRecords);
            cityRecords ??= [];

            var grouped = cityRecords
                .GroupBy(r => r.ProductTypeId!.Value)
                .Select(g =>
                {
                    var totalDemand = g.Sum(r => r.Demand);
                    var totalSold = g.Sum(r => r.QuantitySold);
                    var totalRevenue = g.Sum(r => r.Revenue);
                    var satisfactionRate = totalDemand > 0 ? Math.Clamp(totalSold / totalDemand, 0m, 1m) : 0m;
                    var avgPrice = totalSold > 0 ? totalRevenue / totalSold : 0m;
                    var topCompetitor = g
                        .GroupBy(record => record.CompanyId)
                        .Select(group => new
                        {
                            CompanyName = group.Select(record => record.Company != null ? record.Company.Name : "Unknown").FirstOrDefault() ?? "Unknown",
                            Revenue = group.Sum(record => record.Revenue)
                        })
                        .OrderByDescending(item => item.Revenue)
                        .FirstOrDefault();
                    var topCompetitorShare = totalRevenue > 0m && topCompetitor is not null
                        ? decimal.Round((topCompetitor.Revenue / totalRevenue) * 100m, 2, MidpointRounding.AwayFromZero)
                        : 0m;

                    return new ProductDemandEntry
                    {
                        ProductTypeId = g.Key,
                        ProductName = g.First().ProductType?.Name ?? string.Empty,
                        Industry = g.First().ProductType?.Industry ?? string.Empty,
                        TotalDemand = totalDemand,
                        TotalQuantitySold = totalSold,
                        SatisfactionRate = Math.Round(satisfactionRate, 4),
                        AverageClearingPrice = Math.Round(avgPrice, 4),
                        TotalRevenue = totalRevenue,
                        SellerCount = g.Select(r => r.CompanyId).Distinct().Count(),
                        TopCompetitorCompanyName = topCompetitor?.CompanyName,
                        TopCompetitorMarketSharePercent = topCompetitorShare,
                    };
                })
                .OrderByDescending(p => p.TotalDemand)
                .Take(clampedTop)
                .ToList();

            results.Add(new CityDemandSummaryResult
            {
                CityId = city.Id,
                CityName = city.Name,
                CurrencyCode = city.CurrencyCode,
                FromTick = fromTick,
                ToTick = gameState.CurrentTick,
                Products = grouped,
            });
        }

        return results;
    }
}

public sealed class MarketPriceResult
{
    public Guid CityId { get; set; }
    public Guid ProductTypeId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal ClearingPrice { get; set; }
    public decimal TotalVolume { get; set; }
    public decimal TotalRevenue { get; set; }
    public int SellerCount { get; set; }
    public string CurrencyCode { get; set; } = "EUR";
    public long FromTick { get; set; }
    public long ToTick { get; set; }
}

public sealed class MarketPriceHistoryPoint
{
    public long Tick { get; set; }
    public decimal ClearingPrice { get; set; }
    public decimal TotalVolume { get; set; }
    public decimal TotalRevenue { get; set; }
    public int SellerCount { get; set; }
}

public sealed class CityDemandSummaryResult
{
    public Guid CityId { get; set; }
    public string CityName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "EUR";
    public long FromTick { get; set; }
    public long ToTick { get; set; }
    public List<ProductDemandEntry> Products { get; set; } = [];
}

public sealed class ProductDemandEntry
{
    public Guid ProductTypeId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public decimal TotalDemand { get; set; }
    public decimal TotalQuantitySold { get; set; }
    public decimal SatisfactionRate { get; set; }
    public decimal AverageClearingPrice { get; set; }
    public decimal TotalRevenue { get; set; }
    public int SellerCount { get; set; }
    public string? TopCompetitorCompanyName { get; set; }
    public decimal TopCompetitorMarketSharePercent { get; set; }
}

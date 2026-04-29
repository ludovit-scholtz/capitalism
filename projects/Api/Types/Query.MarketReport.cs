using Api.Data;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Returns the most recent city market reports, optionally filtered by city and report type.
    /// </summary>
    public async Task<List<CityMarketReportResult>> GetCityMarketReports(
        Guid? cityId,
        string? reportType,
        int limit,
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        var clampedLimit = Math.Clamp(limit, 1, 100);

        var query = db.CityMarketReports.AsNoTracking();

        if (cityId.HasValue)
            query = query.Where(r => r.CityId == cityId.Value);

        if (!string.IsNullOrWhiteSpace(reportType))
            query = query.Where(r => r.ReportType == reportType);

        var reports = await query
            .Include(r => r.City)
            .OrderByDescending(r => r.TickTo)
            .Take(clampedLimit)
            .ToListAsync(cancellationToken);

        return reports.Select(r =>
        {
            var data = CityMarketReportService.DeserializeData(r);
            return new CityMarketReportResult
            {
                Id = r.Id,
                CityId = r.CityId,
                CityName = r.City?.Name ?? string.Empty,
                ReportType = r.ReportType,
                TickFrom = r.TickFrom,
                TickTo = r.TickTo,
                GeneratedAtUtc = r.GeneratedAtUtc,
                TotalRevenue = data?.TotalRevenue ?? 0m,
                TotalQuantitySold = data?.TotalQuantitySold ?? 0m,
                UniqueProducts = data?.UniqueProducts ?? 0,
                TopProducts = data?.TopProducts.Select(p => new ProductMarketStatResult
                {
                    ProductTypeId = p.ProductTypeId,
                    ProductName = p.ProductName,
                    Industry = p.Industry,
                    TotalRevenue = p.TotalRevenue,
                    TotalQuantitySold = p.TotalQuantitySold,
                    AveragePricePerUnit = p.AveragePricePerUnit,
                    BasePrice = p.BasePrice,
                    GrossMarginPct = p.GrossMarginPct,
                    SellerCount = p.SellerCount,
                }).ToList() ?? [],
            };
        }).ToList();
    }
}

public sealed class CityMarketReportResult
{
    public Guid Id { get; set; }
    public Guid CityId { get; set; }
    public string CityName { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public long TickFrom { get; set; }
    public long TickTo { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalQuantitySold { get; set; }
    public int UniqueProducts { get; set; }
    public List<ProductMarketStatResult> TopProducts { get; set; } = [];
}

public sealed class ProductMarketStatResult
{
    public Guid ProductTypeId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public decimal TotalRevenue { get; set; }
    public decimal TotalQuantitySold { get; set; }
    public decimal AveragePricePerUnit { get; set; }
    public decimal BasePrice { get; set; }
    public decimal GrossMarginPct { get; set; }
    public int SellerCount { get; set; }
}

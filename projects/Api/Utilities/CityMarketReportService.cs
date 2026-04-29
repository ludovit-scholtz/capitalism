using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Utilities;

/// <summary>
/// Aggregates public-sales data into weekly and monthly city market reports
/// and generates localized HTML content suitable for the newsroom feed.
/// </summary>
public static class CityMarketReportService
{
    /// <summary>Maximum number of top products to include per report.</summary>
    private const int TopProductsCount = 10;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Aggregates <see cref="PublicSalesRecord"/> rows between <paramref name="tickFrom"/> and
    /// <paramref name="tickTo"/> for every city that had any sales activity.
    /// Returns one <see cref="CityMarketReport"/> per city (unsaved; caller must persist).
    /// Skips cities that already have a report for this window (idempotent).
    /// </summary>
    public static async Task<List<CityMarketReport>> GenerateReportsAsync(
        AppDbContext db,
        string reportType,
        long tickFrom,
        long tickTo,
        CancellationToken ct = default)
    {
        // Only generate for cities that do not already have a report for this window.
        var existingCityIds = await db.CityMarketReports
            .Where(r => r.ReportType == reportType && r.TickFrom == tickFrom)
            .Select(r => r.CityId)
            .ToListAsync(ct);

        var cities = await db.Cities.ToListAsync(ct);
        var productTypes = await db.ProductTypes
            .AsNoTracking()
            .ToDictionaryAsync(p => p.Id, ct);

        // Load all relevant sales records in one query.
        var records = await db.PublicSalesRecords
            .Where(r => r.Tick >= tickFrom && r.Tick <= tickTo && r.ProductTypeId.HasValue)
            .Select(r => new
            {
                r.CityId,
                r.ProductTypeId,
                r.CompanyId,
                r.QuantitySold,
                r.Revenue,
                r.PricePerUnit,
                r.Tick,
            })
            .ToListAsync(ct);

        var result = new List<CityMarketReport>();

        foreach (var city in cities)
        {
            if (existingCityIds.Contains(city.Id))
                continue;

            var cityRecords = records.Where(r => r.CityId == city.Id).ToList();
            if (cityRecords.Count == 0)
                continue;

            // Aggregate per product.
            var byProduct = cityRecords
                .GroupBy(r => r.ProductTypeId!.Value)
                .Select(g =>
                {
                    var totalQty = g.Sum(r => r.QuantitySold);
                    var totalRev = g.Sum(r => r.Revenue);
                    productTypes.TryGetValue(g.Key, out var pt);
                    var basePrice = pt?.BasePrice ?? 0m;
                    var grossProfit = basePrice > 0m ? totalRev - (totalQty * basePrice) : 0m;
                    var grossMarginPct = totalRev > 0m && basePrice > 0m
                        ? Math.Round(grossProfit / totalRev * 100m, 1)
                        : 0m;
                    return new ProductMarketStat
                    {
                        ProductTypeId = g.Key,
                        ProductName = pt?.Name ?? "Unknown Product",
                        Industry = pt?.Industry ?? string.Empty,
                        TotalRevenue = Math.Round(totalRev, 2),
                        TotalQuantitySold = Math.Round(totalQty, 2),
                        AveragePricePerUnit = totalQty > 0m ? Math.Round(totalRev / totalQty, 2) : 0m,
                        BasePrice = Math.Round(basePrice, 2),
                        GrossMarginPct = grossMarginPct,
                        SellerCount = g.Select(r => r.CompanyId).Distinct().Count(),
                    };
                })
                .OrderByDescending(p => p.TotalRevenue)
                .Take(TopProductsCount)
                .ToList();

            var totalRevenue = cityRecords.Sum(r => r.Revenue);
            var totalQtyCity = cityRecords.Sum(r => r.QuantitySold);

            var data = new CityMarketReportData
            {
                CityName = city.Name,
                CurrencyCode = city.CurrencyCode,
                ReportType = reportType,
                TickFrom = tickFrom,
                TickTo = tickTo,
                TopProducts = byProduct,
                TotalRevenue = Math.Round(totalRevenue, 2),
                TotalQuantitySold = Math.Round(totalQtyCity, 2),
                UniqueProducts = cityRecords.Select(r => r.ProductTypeId).Distinct().Count(),
            };

            result.Add(new CityMarketReport
            {
                Id = Guid.NewGuid(),
                CityId = city.Id,
                ReportType = reportType,
                TickFrom = tickFrom,
                TickTo = tickTo,
                GeneratedAtUtc = DateTime.UtcNow,
                ReportDataJson = JsonSerializer.Serialize(data, JsonOptions),
            });
        }

        return result;
    }

    /// <summary>Deserializes the stored <see cref="CityMarketReport.ReportDataJson"/>.</summary>
    public static CityMarketReportData? DeserializeData(CityMarketReport report)
    {
        return JsonSerializer.Deserialize<CityMarketReportData>(report.ReportDataJson, JsonOptions);
    }

    /// <summary>
    /// Generates localized news entry localizations (en/sk/de) for a market report.
    /// </summary>
    public static List<(string Locale, string Title, string Summary, string HtmlContent)> BuildLocalizations(
        CityMarketReport report)
    {
        var data = DeserializeData(report);
        if (data is null)
            return [];

        return
        [
            BuildLocalization("en", data),
            BuildLocalization("sk", data),
            BuildLocalization("de", data),
        ];
    }

    private static (string Locale, string Title, string Summary, string HtmlContent) BuildLocalization(
        string locale,
        CityMarketReportData data)
    {
        var periodLabel = data.ReportType == MarketReportType.Weekly
            ? GetLabel("weekly", locale)
            : GetLabel("monthly", locale);

        var title = locale switch
        {
            "sk" => $"📊 {periodLabel} trhová správa — {data.CityName}",
            "de" => $"📊 {periodLabel} Marktbericht — {data.CityName}",
            _ => $"📊 {periodLabel} Market Report — {data.CityName}",
        };

        var summary = locale switch
        {
            "sk" => $"{data.CityName}: {data.UniqueProducts} produktov, celkové tržby {FormatRevenue(data.TotalRevenue, data.CurrencyCode)}.",
            "de" => $"{data.CityName}: {data.UniqueProducts} Produkte, Gesamtumsatz {FormatRevenue(data.TotalRevenue, data.CurrencyCode)}.",
            _ => $"{data.CityName}: {data.UniqueProducts} products tracked, total revenue {FormatRevenue(data.TotalRevenue, data.CurrencyCode)}.",
        };

        var html = BuildHtml(locale, data, periodLabel);

        return (locale, title, summary, html);
    }

    private static string BuildHtml(string locale, CityMarketReportData data, string periodLabel)
    {
        var (headingProducts, headingRank, headingProduct, headingRevenue, headingQty, headingMargin,
            headingSellers, labelTotal, labelPeriod, labelProducts, noData, tickLabel) = locale switch
        {
            "sk" => (
                "Najlepšie produkty podľa tržieb",
                "#",
                "Produkt",
                "Tržby",
                "Predané ks.",
                "Hrubá marža",
                "Predajcovia",
                "Celkové tržby mesta",
                "Obdobie",
                "Produktov",
                "V tomto meste sa v tomto období nepredávali žiadne produkty.",
                "Tiky"
            ),
            "de" => (
                "Top-Produkte nach Umsatz",
                "#",
                "Produkt",
                "Umsatz",
                "Verkaufte Menge",
                "Bruttomarge",
                "Verkäufer",
                "Gesamtumsatz der Stadt",
                "Zeitraum",
                "Produkte",
                "In diesem Zeitraum wurden in dieser Stadt keine Produkte verkauft.",
                "Ticks"
            ),
            _ => (
                "Top Products by Revenue",
                "#",
                "Product",
                "Revenue",
                "Qty Sold",
                "Gross Margin",
                "Sellers",
                "City Total Revenue",
                "Period",
                "Products",
                "No products were sold in this city during this period.",
                "Ticks"
            ),
        };

        var sb = new System.Text.StringBuilder();

        sb.Append("<div class=\"market-report\">");

        // Summary row
        sb.Append("<div class=\"mr-summary\">");
        sb.Append($"<div class=\"mr-summary-item\"><span class=\"mr-label\">{labelPeriod}</span><span class=\"mr-value\">{tickLabel} {data.TickFrom}–{data.TickTo}</span></div>");
        sb.Append($"<div class=\"mr-summary-item\"><span class=\"mr-label\">{labelTotal}</span><span class=\"mr-value mr-value-highlight\">{FormatRevenue(data.TotalRevenue, data.CurrencyCode)}</span></div>");
        sb.Append($"<div class=\"mr-summary-item\"><span class=\"mr-label\">{labelProducts}</span><span class=\"mr-value\">{data.UniqueProducts}</span></div>");
        sb.Append("</div>");

        if (data.TopProducts.Count == 0)
        {
            sb.Append($"<p class=\"mr-empty\">{noData}</p>");
        }
        else
        {
            // Rankings table
            sb.Append("<table class=\"mr-table\">");
            sb.Append($"<thead><tr><th>{headingRank}</th><th>{headingProduct}</th><th>{headingRevenue}</th><th>{headingQty}</th><th>{headingMargin}</th><th>{headingSellers}</th></tr></thead>");
            sb.Append("<tbody>");

            for (var i = 0; i < data.TopProducts.Count; i++)
            {
                var p = data.TopProducts[i];
                var rank = i + 1;
                var rankClass = rank <= 3 ? $" mr-rank-top{rank}" : string.Empty;
                var marginColor = p.GrossMarginPct >= 30m ? "mr-positive" : p.GrossMarginPct >= 10m ? "mr-neutral" : "mr-negative";

                sb.Append($"<tr>");
                sb.Append($"<td class=\"mr-rank{rankClass}\">{rank}</td>");
                sb.Append($"<td class=\"mr-product-name\"><strong>{HtmlEncode(p.ProductName)}</strong><br/><small class=\"mr-industry\">{HtmlEncode(p.Industry)}</small></td>");
                sb.Append($"<td class=\"mr-revenue\">{FormatRevenue(p.TotalRevenue, data.CurrencyCode)}</td>");
                sb.Append($"<td class=\"mr-qty\">{FormatQuantity(p.TotalQuantitySold)}</td>");
                sb.Append($"<td class=\"{marginColor}\">{p.GrossMarginPct:F1}%</td>");
                sb.Append($"<td class=\"mr-sellers\">{p.SellerCount}</td>");
                sb.Append("</tr>");
            }

            sb.Append("</tbody></table>");
        }

        sb.Append("</div>");

        return sb.ToString();
    }

    private static string GetLabel(string period, string locale) => (period, locale) switch
    {
        ("weekly", "sk") => "Týždenná",
        ("monthly", "sk") => "Mesačná",
        ("weekly", "de") => "Wöchentlicher",
        ("monthly", "de") => "Monatlicher",
        ("weekly", _) => "Weekly",
        ("monthly", _) => "Monthly",
        _ => period,
    };

    private static string FormatRevenue(decimal amount, string currencyCode)
    {
        // Format with currency code prefix for readability in HTML content.
        return $"{currencyCode} {amount:N0}";
    }

    private static string FormatQuantity(decimal qty)
    {
        return qty >= 1_000_000m
            ? $"{qty / 1_000_000m:N1}M"
            : qty >= 1_000m
                ? $"{qty / 1_000m:N1}K"
                : $"{qty:N0}";
    }

    private static string HtmlEncode(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }
}

/// <summary>Serializable market report data snapshot.</summary>
public sealed class CityMarketReportData
{
    public string CityName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public long TickFrom { get; set; }
    public long TickTo { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalQuantitySold { get; set; }
    public int UniqueProducts { get; set; }
    public List<ProductMarketStat> TopProducts { get; set; } = [];
}

/// <summary>Per-product statistics within a market report.</summary>
public sealed class ProductMarketStat
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

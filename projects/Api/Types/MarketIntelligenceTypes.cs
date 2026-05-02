namespace Api.Types;

/// <summary>
/// City-level competitive market intelligence snapshot for the last in-game week.
/// </summary>
public sealed class MarketIntelligenceResult
{
    public Guid CityId { get; set; }
    public string CityName { get; set; } = string.Empty;
    public long DataFromTick { get; set; }
    public long DataToTick { get; set; }
    public List<MarketIntelligenceProductRow> Products { get; set; } = [];
}

/// <summary>
/// Product-level seller ranking in a city.
/// </summary>
public sealed class MarketIntelligenceProductRow
{
    public Guid ProductTypeId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSlug { get; set; } = string.Empty;
    public decimal TotalWeeklySalesVolume { get; set; }
    public List<MarketIntelligenceSellerRow> Sellers { get; set; } = [];
}

/// <summary>
/// Ranked seller details for a specific product and city.
/// </summary>
public sealed class MarketIntelligenceSellerRow
{
    public int Rank { get; set; }
    public Guid CompanyId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public decimal AskingPricePerUnit { get; set; }
    /// <summary>Combined brand quality score (0.0-1.0). Null when no brand is configured.</summary>
    public decimal? BrandQuality { get; set; }
    /// <summary>Total quantity sold over the last in-game week.</summary>
    public decimal EstimatedWeeklySalesVolume { get; set; }
    /// <summary>Share of product sales volume in the selected city over the analytics window (0.0-1.0).</summary>
    public decimal MarketShare { get; set; }
}

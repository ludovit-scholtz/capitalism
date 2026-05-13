namespace Api.Types;

/// <summary>
/// Product-level analytics for a MANUFACTURING unit.
/// Shows cost history, production quantity, and estimated economics per tick.
/// </summary>
public sealed class UnitProductAnalytics
{
    public Guid BuildingUnitId { get; set; }
    /// <summary>The unit type (e.g. MANUFACTURING).</summary>
    public string UnitType { get; set; } = string.Empty;
    /// <summary>Product type being produced. Null when no product is configured.</summary>
    public Guid? ProductTypeId { get; set; }
    /// <summary>Display name of the product being produced. Null when no product data is available.</summary>
    public string? ProductName { get; set; }
    /// <summary>First tick in the analytics window (oldest data).</summary>
    public long DataFromTick { get; set; }
    /// <summary>Last tick in the analytics window (most recent data).</summary>
    public long DataToTick { get; set; }
    /// <summary>Total labor + energy cost over the analytics window.</summary>
    public decimal TotalCost { get; set; }
    /// <summary>Total units produced over the analytics window.</summary>
    public decimal TotalQuantityProduced { get; set; }
    /// <summary>
    /// Estimated total revenue = TotalQuantityProduced × product.BasePrice.
    /// Null when base price is unavailable.
    /// </summary>
    public decimal? EstimatedRevenue { get; set; }
    /// <summary>
    /// Estimated total profit = EstimatedRevenue − TotalCost.
    /// Null when base price is unavailable.
    /// </summary>
    public decimal? EstimatedProfit { get; set; }
    /// <summary>Per-tick snapshots ordered by tick ascending.</summary>
    public List<UnitProductTickSnapshot> Snapshots { get; set; } = [];
    /// <summary>ISO 4217 currency code for the city where this unit is located (e.g. "EUR", "CZK").</summary>
    public string CityCurrencyCode { get; set; } = "EUR";
}

/// <summary>
/// Per-product/per-city analytics row for the campaign analytics dashboard.
/// Combines brand, pricing, and sales data so players can compare strategies.
/// </summary>
public sealed class CampaignAnalyticsRow
{
    /// <summary>Building unit identifier (PUBLIC_SALES unit).</summary>
    public Guid BuildingUnitId { get; set; }
    /// <summary>Building identifier.</summary>
    public Guid BuildingId { get; set; }
    /// <summary>Human-readable building name.</summary>
    public string BuildingName { get; set; } = string.Empty;
    /// <summary>Display name of the product being sold in this unit. Null when no product configured.</summary>
    public string? ProductName { get; set; }
    /// <summary>Product type ID. Null when no product is configured.</summary>
    public Guid? ProductTypeId { get; set; }
    /// <summary>City where the shop is located.</summary>
    public string CityName { get; set; } = string.Empty;

    // ── Brand metrics ─────────────────────────────────────────────────────
    /// <summary>Brand awareness (0–1). Null when no brand is set.</summary>
    public decimal? BrandAwareness { get; set; }
    /// <summary>Combined brand quality blending R&amp;D and marketing prestige (0–1). Null when no brand.</summary>
    public decimal? BrandQuality { get; set; }
    /// <summary>Marketing-driven prestige component of brand quality (0–1). Null when no brand.</summary>
    public decimal? MarketingQuality { get; set; }

    // ── Pricing metrics ───────────────────────────────────────────────────
    /// <summary>Current selling price configured on the unit. Null when not configured.</summary>
    public decimal? CurrentPrice { get; set; }
    /// <summary>Product's market base price. Null when no product data.</summary>
    public decimal? BasePrice { get; set; }
    /// <summary>
    /// Price index (0–1.5). 1.0 = at base price, &lt;1.0 = premium, &gt;1.0 = discount.
    /// Null when price or base price unavailable.
    /// </summary>
    public decimal? PriceIndex { get; set; }
    /// <summary>
    /// Percentage that the current price is above the base price (positive = premium, negative = discount).
    /// Null when price or base price unavailable.
    /// </summary>
    public decimal? PricePremiumPct { get; set; }

    // ── Recent performance (last CampaignAnalyticsWindowTicks ticks) ───────
    /// <summary>Total revenue in the analytics window.</summary>
    public decimal RevenueLastTicks { get; set; }
    /// <summary>Total quantity sold in the analytics window.</summary>
    public decimal QuantityLastTicks { get; set; }
    /// <summary>Average capacity utilisation (0–1) in the analytics window.</summary>
    public decimal UtilizationRate { get; set; }
    /// <summary>Revenue trend direction: UP | FLAT | DOWN | NO_DATA.</summary>
    public string TrendDirection { get; set; } = "NO_DATA";
    /// <summary>Most-recent market trend factor (0.5–1.5). Null when no trend data.</summary>
    public decimal? TrendFactor { get; set; }
    /// <summary>Demand signal: NO_DATA | SUPPLY_CONSTRAINED | STRONG | MODERATE | WEAK.</summary>
    public string DemandSignal { get; set; } = "NO_DATA";

    // ── Demand insight ────────────────────────────────────────────────────
    /// <summary>Factor identifier with the strongest positive impact on demand. Null when no data.</summary>
    public string? TopPositiveFactor { get; set; }
    /// <summary>Factor identifier with the strongest negative impact on demand. Null when no data.</summary>
    public string? TopNegativeFactor { get; set; }

    // ── Campaign ROI ──────────────────────────────────────────────────────
    /// <summary>Total marketing spend ledger entries in the analytics window. Null when no marketing units.</summary>
    public decimal? MarketingSpendLastTicks { get; set; }
    /// <summary>
    /// Estimated brand contribution multiplier on revenue.
    /// Computed as: brandFactor − 1, where brandFactor = 1 + combinedBrandQuality × BrandQualityBoostFactor.
    /// Null when no brand data available.
    /// </summary>
    public decimal? BrandRevenueBoost { get; set; }
    /// <summary>Campaign effectiveness: STRONG | MODERATE | WEAK | NONE.</summary>
    public string CampaignImpact { get; set; } = "NONE";

    // ── Strategic insights ─────────────────────────────────────────────────
    /// <summary>
    /// Brand-versus-price balance indicator.
    /// Values: PREMIUM_JUSTIFIED | PREMIUM_RISKY | DISCOUNT_WITH_BRAND | COMPETITIVE_BASELINE | BRAND_BUILDING | NO_BRAND
    /// </summary>
    public string BrandVsPriceBalance { get; set; } = "NO_BRAND";
    /// <summary>Short player-facing recommendation based on the current brand/price/demand combination.</summary>
    public string Recommendation { get; set; } = string.Empty;
    /// <summary>ISO 4217 currency code for the city where this unit is located.</summary>
    public string CityCurrencyCode { get; set; } = "EUR";
}

/// <summary>
/// Aggregated campaign analytics result for a company.
/// Returned by the <c>campaignAnalytics</c> query.
/// </summary>
public sealed class CampaignAnalyticsResult
{
    /// <summary>Company identifier.</summary>
    public Guid CompanyId { get; set; }
    /// <summary>Number of ticks covered by the analysis window.</summary>
    public int WindowTicks { get; set; }
    /// <summary>Sum of revenue across all public sales units in the window.</summary>
    public decimal TotalRevenue { get; set; }
    /// <summary>Sum of marketing spend across all marketing units in the window.</summary>
    public decimal TotalMarketingSpend { get; set; }
    /// <summary>City with the highest revenue in the window. Null when no data.</summary>
    public string? BestPerformingCity { get; set; }
    /// <summary>Product with the highest revenue in the window. Null when no data.</summary>
    public string? BestPerformingProduct { get; set; }
    /// <summary>High-level recommendation for the entire company's campaign portfolio.</summary>
    public string GlobalRecommendation { get; set; } = string.Empty;
    /// <summary>Per-product/per-city analytics rows.</summary>
    public List<CampaignAnalyticsRow> Rows { get; set; } = [];
}

/// <summary>Per-tick cost and production snapshot for a MANUFACTURING unit.</summary>
public sealed class UnitProductTickSnapshot
{
    public long Tick { get; set; }
    /// <summary>Labor cost charged to the company for this unit on this tick.</summary>
    public decimal LaborCost { get; set; }
    /// <summary>Energy cost charged to the company for this unit on this tick.</summary>
    public decimal EnergyCost { get; set; }
    /// <summary>Total operating cost (labor + energy) for this tick.</summary>
    public decimal TotalCost { get; set; }
    /// <summary>Quantity of the product produced on this tick.</summary>
    public decimal QuantityProduced { get; set; }
    /// <summary>
    /// Estimated revenue = QuantityProduced × product.BasePrice.
    /// Null when base price is unavailable.
    /// </summary>
    public decimal? EstimatedRevenue { get; set; }
    /// <summary>
    /// Estimated profit = EstimatedRevenue − TotalCost.
    /// Null when base price is unavailable.
    /// </summary>
    public decimal? EstimatedProfit { get; set; }
}

// ── Media house analytics DTOs ────────────────────────────────────────────────

/// <summary>
/// Advertising income entry in the media house analytics history.
/// </summary>
public sealed class MediaHouseIncomeEntry
{
    public long Tick { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Shows how one advertiser's brand is being affected by campaigns through this media house.
/// </summary>
public sealed class MediaHouseBrandEffectRow
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    /// <summary>PRODUCT | CATEGORY | COMPANY</summary>
    public string BrandScope { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    /// <summary>Current brand awareness as percentage 0–100.</summary>
    public decimal BrandAwareness { get; set; }
    /// <summary>Current marketing quality (prestige) as percentage 0–100.</summary>
    public decimal MarketingQuality { get; set; }
    /// <summary>The combined channel × content-ranking multiplier applied to this advertiser's campaigns.</summary>
    public decimal EffectivenessMultiplierApplied { get; set; }
}

/// <summary>
/// Full analytics result for a MEDIA_HOUSE building.
/// </summary>
public sealed class MediaHouseAnalyticsResult
{
    public Guid BuildingId { get; set; }
    public string BuildingName { get; set; } = string.Empty;
    /// <summary>NEWSPAPER | RADIO | TV</summary>
    public string MediaType { get; set; } = string.Empty;
    public int Level { get; set; }
    public decimal ContentValue { get; set; }
    /// <summary>Content ranking as a percentage 0–100 relative to top outlet in same city+type.</summary>
    public decimal ContentRankingPct { get; set; }
    /// <summary>Raw channel multiplier based on media type (1.0/1.5/2.0).</summary>
    public decimal ChannelMultiplier { get; set; }
    /// <summary>Combined channel × content-ranking multiplier seen by advertisers.</summary>
    public decimal EffectiveMultiplier { get; set; }
    /// <summary>Current level content-conversion efficiency as integer percent.</summary>
    public int CurrentEfficiencyPct { get; set; }
    /// <summary>Next level content-conversion efficiency as integer percent. Equals CurrentEfficiencyPct when at max level.</summary>
    public int NextLevelEfficiencyPct { get; set; }
    public bool IsMaxLevel { get; set; }
    /// <summary>Upgrade cost in EUR (before FX). Null when at max level.</summary>
    public decimal? UpgradeCostEur { get; set; }
    /// <summary>Ticks required for the next upgrade. Null when at max level.</summary>
    public int? UpgradeTimeTicks { get; set; }
    public int MaxLevel { get; set; }
    public decimal TotalIncomeLast100Ticks { get; set; }
    public decimal AvgIncomePerTick { get; set; }
    public List<MediaHouseIncomeEntry> IncomeHistory { get; set; } = [];
    /// <summary>Number of companies currently advertising through this media house.</summary>
    public int AdvertiserCount { get; set; }
    /// <summary>Brand impact rows for all brands of active advertisers.</summary>
    public List<MediaHouseBrandEffectRow> BrandEffects { get; set; } = [];
    /// <summary>DOMINANT | COMPETITIVE | GROWING | EARLY_STAGE</summary>
    public string StrategyRating { get; set; } = string.Empty;
    public string StrategyTip { get; set; } = string.Empty;
}

/// <summary>
/// Lightweight public detail for a single media house.
/// </summary>
public sealed class MediaHouseDetailResult
{
    public Guid BuildingId { get; set; }
    public string BuildingName { get; set; } = string.Empty;
    public Guid CityId { get; set; }
    public string CityName { get; set; } = string.Empty;
    /// <summary>NEWSPAPER | RADIO | TV</summary>
    public string MediaType { get; set; } = string.Empty;
    /// <summary>Content quality score as percentage 0–100 relative to city+type leader.</summary>
    public decimal ContentQualityScore { get; set; }
    public decimal AccumulatedContent { get; set; }
    public int CityRank { get; set; }
    /// <summary>Current owner-configured spending level per tick. Hidden for non-owners.</summary>
    public decimal? SpendingLevelPerTick { get; set; }
    /// <summary>Advertising revenue for the latest game tick. Hidden for non-owners.</summary>
    public decimal? RevenueThisTick { get; set; }
}

/// <summary>
/// Seasonal demand outlook for a public sales unit.
/// Shows the demand multiplier for each game-year quarter and the current seasonal context.
/// </summary>
public sealed class SeasonalOutlook
{
    /// <summary>
    /// Current quarter index (0=Q1, 1=Q2, 2=Q3, 3=Q4) derived from the current game tick.
    /// </summary>
    public int CurrentQuarterIndex { get; set; }

    /// <summary>Label for the current quarter, e.g. "Q1 (Jan–Mar)".</summary>
    public string CurrentQuarterLabel { get; set; } = string.Empty;

    /// <summary>Seasonal demand multiplier active this quarter (e.g. 1.5, 0.8).</summary>
    public decimal CurrentMultiplier { get; set; }

    /// <summary>Demand level label: HIGH | MODERATE | BELOW_AVERAGE | LOW</summary>
    public string DemandLevel { get; set; } = string.Empty;

    /// <summary>Forecasted multipliers for the four quarters Q1–Q4 in game-year order.</summary>
    public List<QuarterForecast> QuarterForecasts { get; set; } = [];

    /// <summary>Short contextual callout explaining the seasonal pattern for this product.</summary>
    public string Callout { get; set; } = string.Empty;
}

/// <summary>Seasonal demand forecast for one quarter.</summary>
public sealed class QuarterForecast
{
    /// <summary>Quarter index 0=Q1, 1=Q2, 2=Q3, 3=Q4.</summary>
    public int QuarterIndex { get; set; }
    /// <summary>Label, e.g. "Q1 (Jan–Mar)".</summary>
    public string Label { get; set; } = string.Empty;
    /// <summary>Demand multiplier for this quarter (e.g. 1.5).</summary>
    public decimal Multiplier { get; set; }
    /// <summary>Whether this is the currently active quarter.</summary>
    public bool IsCurrent { get; set; }
    /// <summary>Color code: GREEN | YELLOW | ORANGE | RED</summary>
    public string ColorCode { get; set; } = string.Empty;
}

using Api.Data.Entities;

namespace Api.Types;

/// <summary>
/// Detailed R&amp;D quality profile for a specific company + product type combination.
/// Returned by the <c>productQualityProfile</c> query.
/// </summary>
public sealed class ProductQualityProfile
{
    /// <summary>The company whose research profile is returned.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>The product type being researched.</summary>
    public Guid ProductTypeId { get; set; }

    /// <summary>Human-readable product name.</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>Industry category (e.g., FURNITURE, FOOD_PROCESSING).</summary>
    public string? Industry { get; set; }

    /// <summary>
    /// R&amp;D-driven quality (0.0–1.0). Driven by PRODUCT_QUALITY units.
    /// Equivalent to <c>QualityLevel / 10</c>.
    /// </summary>
    public decimal RdQuality { get; set; }

    /// <summary>
    /// Marketing-driven prestige quality (0.0–1.0). Driven by media and marketing spend.
    /// </summary>
    public decimal MarketingQuality { get; set; }

    /// <summary>
    /// Combined effective quality (0.0–1.0): blends R&amp;D and marketing quality.
    /// Formula: <c>1 - (1 - RdQuality) × (1 - MarketingQuality)</c>.
    /// </summary>
    public decimal CombinedQuality { get; set; }

    /// <summary>
    /// Display quality level on 0–10 scale from combined quality.
    /// At level 5 (CombinedQuality = 0.5), the product earns a 25% price premium.
    /// </summary>
    public decimal QualityLevel { get; set; }

    /// <summary>R&amp;D-only quality level on 0–10 display scale.</summary>
    public decimal RdQualityLevel { get; set; }

    /// <summary>
    /// Accumulated R&amp;D research budget in USD.
    /// Grows each tick by a fraction of the PRODUCT_QUALITY unit's operating cost.
    /// Null when no R&amp;D has been started yet.
    /// </summary>
    public decimal? AccumulatedResearchBudgetUsd { get; set; }

    /// <summary>
    /// The budget in USD required to reach 100% R&amp;D quality when uncontested.
    /// Computed as <c>max(5 000, basePrice × 1 000)</c>.
    /// </summary>
    public decimal BaseResearchBudgetUsd { get; set; }

    /// <summary>
    /// Highest accumulated research budget (USD) across all companies researching this product.
    /// Null when no research exists globally.
    /// </summary>
    public decimal? MaxCompetitorBudgetUsd { get; set; }

    /// <summary>
    /// Marketing efficiency multiplier (≥ 1.0) driven by BRAND_QUALITY R&amp;D.
    /// 1.5 = 50% more brand awareness per marketing dollar.
    /// </summary>
    public decimal MarketingEfficiencyMultiplier { get; set; } = 1m;

    /// <summary>
    /// Percentage price premium this quality level unlocks.
    /// Formula: <c>QualityPricePremiumRate × CombinedQuality × 100</c>.
    /// At quality level 5: 25%. At quality level 10: 50%.
    /// </summary>
    public decimal QualityPricePremiumPct { get; set; }

    /// <summary>
    /// Estimated ticks until the next quality level is reached, based on current budget rate.
    /// Null when not enough data to estimate (no budget or at max quality).
    /// </summary>
    public decimal? TicksToNextLevel { get; set; }
}

/// <summary>
/// Overview of all brand and R&amp;D research states for a company.
/// Returned by the <c>brandQualityOverview</c> query.
/// </summary>
public sealed class BrandQualityOverview
{
    /// <summary>The company whose research overview is returned.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>
    /// All brand states for this company, across all scopes (PRODUCT, CATEGORY, COMPANY).
    /// Includes synthetic entries for budgets without a matching brand.
    /// </summary>
    public List<ResearchBrandState> Brands { get; set; } = [];

    /// <summary>
    /// Total accumulated research budget across all products this company is researching, in USD.
    /// </summary>
    public decimal TotalResearchBudgetUsd { get; set; }
}

/// <summary>
/// R&amp;D research progress for a single PRODUCT_QUALITY or BRAND_QUALITY unit in an R&amp;D building.
/// Returned by the <c>buildingResearchProgress</c> query.
/// </summary>
public sealed class BuildingUnitResearchProgress
{
    /// <summary>Building unit identifier.</summary>
    public Guid UnitId { get; set; }

    /// <summary>Unit type: ProductQuality or BrandQuality.</summary>
    public string UnitType { get; set; } = string.Empty;

    /// <summary>Grid column (0-indexed).</summary>
    public int GridX { get; set; }

    /// <summary>Grid row (0-indexed).</summary>
    public int GridY { get; set; }

    /// <summary>Unit level (1+).</summary>
    public int Level { get; set; }

    /// <summary>The product type this unit is researching (null for company-scope brand units).</summary>
    public Guid? ProductTypeId { get; set; }

    /// <summary>Human-readable product name (null when not product-scoped).</summary>
    public string? ProductName { get; set; }

    /// <summary>Industry category for CATEGORY-scoped brand units.</summary>
    public string? IndustryCategory { get; set; }

    /// <summary>
    /// Current R&amp;D quality level on 0–10 display scale.
    /// Driven exclusively by PRODUCT_QUALITY R&amp;D spend.
    /// </summary>
    public decimal CurrentQualityLevel { get; set; }

    /// <summary>
    /// Combined quality level (R&amp;D + marketing) on 0–10 display scale.
    /// This is the level that determines the quality price premium.
    /// </summary>
    public decimal CombinedQualityLevel { get; set; }

    /// <summary>
    /// Fractional progress toward the next integer quality level, 0–100%.
    /// E.g., 75% means 3/4 of the way from level 3 to level 4.
    /// </summary>
    public decimal ProgressToNextLevelPct { get; set; }

    /// <summary>
    /// Marketing efficiency multiplier (≥ 1.0) if this is a BRAND_QUALITY unit,
    /// otherwise 1.0 (not applicable).
    /// </summary>
    public decimal MarketingEfficiencyMultiplier { get; set; } = 1m;

    /// <summary>
    /// Percentage price premium this quality level unlocks for the product's demand calculation.
    /// At combined quality level 5: 25%. At level 10: 50%.
    /// </summary>
    public decimal QualityPricePremiumPct { get; set; }
}

using Api.Data.Entities;

namespace Api.Types;

/// <summary>Inventory fill information for a single building unit.</summary>
public sealed class BuildingUnitInventorySummary
{
    public Guid BuildingUnitId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Capacity { get; set; }
    public decimal FillPercent { get; set; }
    public decimal? AverageQuality { get; set; }
    public decimal TotalSourcingCost { get; set; }
    public decimal SourcingCostPerUnit { get; set; }

    /// <summary>
    /// Total quantity that entered this unit during the most recent completed tick.
    /// Null when no history exists yet (unit has never processed a tick).
    /// </summary>
    public decimal? LastTickInflow { get; set; }

    /// <summary>
    /// Total quantity that left this unit during the most recent completed tick.
    /// Null when no history exists yet (unit has never processed a tick).
    /// </summary>
    public decimal? LastTickOutflow { get; set; }
}

/// <summary>
/// City-level power balance snapshot.
/// Computed on demand from current building data.
/// </summary>
public sealed class CityPowerBalance
{
    /// <summary>The city this balance applies to.</summary>
    public Guid CityId { get; set; }

    /// <summary>Total power output in MW from all power plants in the city.</summary>
    public decimal TotalSupplyMw { get; set; }

    /// <summary>Total power demand in MW from all consuming buildings in the city.</summary>
    public decimal TotalDemandMw { get; set; }

    /// <summary>Reserve capacity in MW (supply minus demand; negative means shortage).</summary>
    public decimal ReserveMw { get; set; }

    /// <summary>Reserve as a percentage of demand (negative means shortage).</summary>
    public decimal ReservePercent { get; set; }

    /// <summary>
    /// Overall power status for the city:
    /// BALANCED = supply &gt;= demand,
    /// CONSTRAINED = supply &lt; demand but &gt;= 50%,
    /// CRITICAL = supply &lt; 50% of demand.
    /// </summary>
    public string Status { get; set; } = "BALANCED";

    /// <summary>Summary of each power plant in the city.</summary>
    public List<PowerPlantSummary> PowerPlants { get; set; } = [];

    /// <summary>Number of power plants in the city.</summary>
    public int PowerPlantCount { get; set; }

    /// <summary>Number of consuming buildings in the city.</summary>
    public int ConsumerBuildingCount { get; set; }
}

/// <summary>Summary of a single power plant building for the city power balance view.</summary>
public sealed class PowerPlantSummary
{
    /// <summary>Building identifier.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>Building display name.</summary>
    public string BuildingName { get; set; } = string.Empty;

    /// <summary>Plant type: COAL, GAS, SOLAR, WIND, or NUCLEAR.</summary>
    public string PlantType { get; set; } = string.Empty;

    /// <summary>Current power output in MW.</summary>
    public decimal OutputMw { get; set; }

    /// <summary>Power supply status of this plant (always POWERED).</summary>
    public string PowerStatus { get; set; } = Data.Entities.PowerStatus.Powered;
}

/// <summary>
/// Snapshot of a company brand accumulated by R&amp;D research and marketing spend.
/// Exposed by the companyBrands query so the frontend can render research progress.
/// </summary>
public sealed class ResearchBrandState
{
    /// <summary>Brand identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Company that owns this brand.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>Display name of the brand (product name or company name).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Scope: PRODUCT, CATEGORY, or COMPANY.</summary>
    public string Scope { get; set; } = BrandScope.Product;

    /// <summary>The specific product type this brand applies to (if scope is PRODUCT or CATEGORY).</summary>
    public Guid? ProductTypeId { get; set; }

    /// <summary>Human-readable product name for this brand (null for company-wide brands).</summary>
    public string? ProductName { get; set; }

    /// <summary>Industry category for CATEGORY-scoped brands.</summary>
    public string? IndustryCategory { get; set; }

    /// <summary>
    /// Brand awareness level (0.0–1.0). Driven by marketing unit spend.
    /// Higher awareness translates to more sales driven by brand recognition.
    /// </summary>
    public decimal Awareness { get; set; }

    /// <summary>
    /// Brand quality level (0.0–1.0). Driven by PRODUCT_QUALITY R&amp;D.
    /// Higher quality improves manufactured product output quality.
    /// </summary>
    public decimal Quality { get; set; }

    /// <summary>
    /// Marketing-driven brand prestige (0.0–1.0). Accumulated slowly from sustained marketing spend.
    /// Decays gradually when investment stops. Distinct from R&amp;D-driven <see cref="Quality"/>.
    /// </summary>
    public decimal MarketingQuality { get; set; }

    /// <summary>
    /// Combined effective brand quality (0.0–1.0): blends R&amp;D quality and marketing prestige.
    /// Formula: <c>1 - (1 - Quality) × (1 - MarketingQuality)</c>.
    /// This is the value that amplifies sales demand in the public-sales simulation.
    /// </summary>
    public decimal CombinedBrandQuality { get; set; }

    /// <summary>
    /// Accumulated R&amp;D research budget in USD for this product.
    /// Grows each tick by a fraction of the PRODUCT_QUALITY unit's operating cost converted to USD,
    /// and decays by 0.1% per tick.  Null when no research has been done for this product.
    /// All budget values on this object (AccumulatedResearchBudget, BaseResearchBudget,
    /// MaxCompetitorBudget) are in USD so the frontend can display consistent dollar amounts
    /// regardless of which city the R&amp;D building is in.
    /// </summary>
    public decimal? AccumulatedResearchBudget { get; set; }

    /// <summary>
    /// Base research budget required for 100% quality when no competitor exists,
    /// expressed in USD (computed from the product base price converted to USD).
    /// </summary>
    public decimal? BaseResearchBudget { get; set; }

    /// <summary>
    /// Highest research budget (in USD) across all companies researching this same product.
    /// Used as the denominator when computing relative quality.
    /// Null when no research has been done globally for this product.
    /// </summary>
    public decimal? MaxCompetitorBudget { get; set; }

    /// <summary>
    /// Marketing efficiency multiplier (≥ 1.0). Driven by BRAND_QUALITY R&amp;D.
    /// A value of 1.5 means each unit of marketing budget generates 50% more brand awareness than baseline.
    /// This is NOT a direct brand gain — it only amplifies the effect of marketing spend.
    /// </summary>
    public decimal MarketingEfficiencyMultiplier { get; set; } = 1m;
}

/// <summary>Upgrade info for a single building unit: cost, timing, and stat projections.</summary>
public sealed class UnitUpgradeInfo
{
    public Guid UnitId { get; set; }
    public string UnitType { get; set; } = string.Empty;
    public int CurrentLevel { get; set; }
    public int NextLevel { get; set; }
    public bool IsMaxLevel { get; set; }
    public bool IsUpgradable { get; set; }
    public decimal UpgradeCost { get; set; }
    public int UpgradeTicks { get; set; }
    public decimal CurrentStat { get; set; }
    public decimal NextStat { get; set; }
    public string StatLabel { get; set; } = string.Empty;

    // Operating cost deltas (per tick, at reference wage / energy price)
    public decimal CurrentLaborHoursPerTick { get; set; }
    public decimal NextLaborHoursPerTick { get; set; }
    public decimal CurrentEnergyMwhPerTick { get; set; }
    public decimal NextEnergyMwhPerTick { get; set; }
    public decimal CurrentLaborCostPerTick { get; set; }
    public decimal NextLaborCostPerTick { get; set; }
    public decimal CurrentEnergyCostPerTick { get; set; }
    public decimal NextEnergyCostPerTick { get; set; }

    /// <summary>
    /// Inventory holding capacity (max units storable in the unit's local buffer) at the
    /// current and next levels. Relevant for all unit types that hold inventory in transit
    /// (PUBLIC_SALES, PURCHASE, MANUFACTURING, MINING, B2B_SALES) as well as dedicated STORAGE units.
    /// Use this to show the player the concrete inventory buffer change an upgrade unlocks.
    /// </summary>
    public decimal CurrentStorageCapacity { get; set; }
    public decimal NextStorageCapacity { get; set; }
}

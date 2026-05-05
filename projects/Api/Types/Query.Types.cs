using Api.Data.Entities;

namespace Api.Types;

/// <summary>Payload for starter industries.</summary>
public sealed class StarterIndustriesPayload
{
    /// <summary>Available starter industry values.</summary>
    public List<string> Industries { get; set; } = [];

    /// <summary>Industries within the starter list that require an active Pro subscription.</summary>
    public List<string> ProOnlyIndustries { get; set; } = [];
}

/// <summary>Type values for scheduled actions visible to the player.</summary>
public static class ScheduledActionType
{
    /// <summary>A queued building configuration upgrade (layout/unit change).</summary>
    public const string BuildingUpgrade = "BUILDING_UPGRADE";
}

/// <summary>Summary of a single pending scheduled action for the player.</summary>
public sealed class ScheduledActionSummary
{
    /// <summary>Unique identifier (matches the underlying plan or entity).</summary>
    public Guid Id { get; set; }

    /// <summary>Category of the scheduled action. See <see cref="ScheduledActionType"/>.</summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>Building the action belongs to.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>Human-readable building name for display in the UI.</summary>
    public string BuildingName { get; set; } = string.Empty;

    /// <summary>Building type string (e.g. FACTORY, SALES_SHOP).</summary>
    public string BuildingType { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the action was submitted.</summary>
    public DateTime SubmittedAtUtc { get; set; }

    /// <summary>Game tick when the action was submitted.</summary>
    public long SubmittedAtTick { get; set; }

    /// <summary>Game tick when the action is scheduled to apply.</summary>
    public long AppliesAtTick { get; set; }

    /// <summary>Number of ticks remaining until the action applies.</summary>
    public long TicksRemaining { get; set; }

    /// <summary>Total ticks this action required from submission to application.</summary>
    public int TotalTicksRequired { get; set; }
}

/// <summary>Phase values for the first-sale onboarding mission.</summary>
public static class FirstSaleMissionPhase
{
    /// <summary>Onboarding is not complete or no shop building is being tracked.</summary>
    public const string NoShop = "NO_SHOP";

    /// <summary>Shop exists but has at least one configuration blocker preventing the first sale.</summary>
    public const string ConfigureShop = "CONFIGURE_SHOP";

    /// <summary>Shop is fully configured; waiting for the next simulation tick to record a sale.</summary>
    public const string AwaitingFirstSale = "AWAITING_FIRST_SALE";

    /// <summary>A real PublicSalesRecord with QuantitySold &gt; 0 exists for the onboarding shop.</summary>
    public const string FirstSaleRecorded = "FIRST_SALE_RECORDED";

    /// <summary>The player has already acknowledged the first-sale milestone (OnboardingFirstSaleCompletedAtUtc is set).</summary>
    public const string AlreadyCompleted = "ALREADY_COMPLETED";
}

/// <summary>Blocker codes returned when the first-sale mission phase is CONFIGURE_SHOP.</summary>
public static class FirstSaleMissionBlocker
{
    /// <summary>The sales shop building is still under construction and cannot operate yet.</summary>
    public const string BuildingUnderConstruction = "BUILDING_UNDER_CONSTRUCTION";

    /// <summary>No PUBLIC_SALES unit is present in the shop building.</summary>
    public const string PublicSalesUnitMissing = "PUBLIC_SALES_UNIT_MISSING";

    /// <summary>The PUBLIC_SALES unit does not have a selling price set (MinPrice is null or zero).</summary>
    public const string PriceNotSet = "PRICE_NOT_SET";

    /// <summary>The PUBLIC_SALES unit has no inventory to sell yet (factory has not produced anything).</summary>
    public const string NoInventory = "NO_INVENTORY";
}

/// <summary>
/// Mission-status view model for the post-onboarding first-sale mission.
/// Returned by the <c>firstSaleMission</c> query.
/// </summary>
public sealed class FirstSaleMissionStatus
{
    /// <summary>
    /// Current phase of the first-sale mission.
    /// One of: NO_SHOP, CONFIGURE_SHOP, AWAITING_FIRST_SALE, FIRST_SALE_RECORDED, ALREADY_COMPLETED.
    /// </summary>
    public string Phase { get; set; } = FirstSaleMissionPhase.NoShop;

    /// <summary>The onboarding sales shop building ID being tracked (null when phase is NO_SHOP).</summary>
    public Guid? ShopBuildingId { get; set; }

    /// <summary>Display name of the onboarding sales shop (null when phase is NO_SHOP).</summary>
    public string? ShopName { get; set; }

    /// <summary>
    /// List of blocker codes explaining why the shop is not yet ready.
    /// Only populated when phase is CONFIGURE_SHOP.
    /// See <see cref="FirstSaleMissionBlocker"/> for possible values.
    /// </summary>
    public List<string> Blockers { get; set; } = [];

    /// <summary>Revenue from the first recorded sale (null until phase is FIRST_SALE_RECORDED).</summary>
    public decimal? FirstSaleRevenue { get; set; }

    /// <summary>Name of the product sold in the first sale (null until phase is FIRST_SALE_RECORDED).</summary>
    public string? FirstSaleProductName { get; set; }

    /// <summary>Game tick at which the first sale occurred (null until phase is FIRST_SALE_RECORDED).</summary>
    public long? FirstSaleTick { get; set; }

    /// <summary>Quantity sold in the first sale (null until phase is FIRST_SALE_RECORDED).</summary>
    public decimal? FirstSaleQuantity { get; set; }

    /// <summary>Price per unit in the first sale (null until phase is FIRST_SALE_RECORDED).</summary>
    public decimal? FirstSalePricePerUnit { get; set; }
}

/// <summary>
/// Read model for a media house building in a city.
/// Returned by the <c>cityMediaHouses</c> query.
/// </summary>
public sealed class CityMediaHouseInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid CityId { get; set; }
    public string CityName { get; set; } = string.Empty;

    /// <summary>Channel type: NEWSPAPER, RADIO, TV. Null if not configured.</summary>
    public string? MediaType { get; set; }

    public Guid OwnerCompanyId { get; set; }
    public string OwnerCompanyName { get; set; } = string.Empty;

    /// <summary>
    /// Awareness multiplier applied when this media house is selected as the campaign channel.
    /// 1.0 = Newspaper, 1.5 = Radio, 2.0 = TV.
    /// </summary>
    public decimal EffectivenessMultiplier { get; set; }

    /// <summary>POWERED, CONSTRAINED, or OFFLINE.</summary>
    public string PowerStatus { get; set; } = Data.Entities.PowerStatus.Powered;

    public bool IsUnderConstruction { get; set; }

    /// <summary>
    /// Content ranking as a percentage (0–100) relative to the top-ranked outlet in the same
    /// city and media category.  The media house with the highest ContentValue in that slot is
    /// always 100%; all others are proportional.  Returns 0 when ContentValue is 0.
    /// </summary>
    public decimal ContentRanking { get; set; }

    /// <summary>
    /// Current accumulated content value for this media house.
    /// Grows from per-tick content spending; decays 0.5% per tick.
    /// </summary>
    public decimal ContentValue { get; set; }

    /// <summary>
    /// Per-tick content spending configured by the owner.
    /// Null means no active investment.
    /// </summary>
    public decimal? ContentBudgetPerTick { get; set; }

    /// <summary>True when the building is a government-seeded baseline media house.</summary>
    public bool IsGovernmentOwned { get; set; }
}

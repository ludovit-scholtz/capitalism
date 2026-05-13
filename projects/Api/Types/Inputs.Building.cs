using System.ComponentModel.DataAnnotations;
using Api.Data.Entities;

namespace Api.Types;

/// <summary>Input for storing a queued building configuration update.</summary>
public sealed class StoreBuildingConfigurationInput
{
    /// <summary>Building that should receive the queued configuration.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>Queued unit layout snapshot. Server-controlled fields such as level and activation tick are not accepted.</summary>
    [Required]
    public List<BuildingConfigurationUnitInput> Units { get; set; } = [];
}

/// <summary>Input for cancelling a queued building configuration plan with rollback timing.</summary>
public sealed class CancelBuildingConfigurationInput
{
    /// <summary>Building whose pending configuration should be cancelled.</summary>
    public Guid BuildingId { get; set; }
}

/// <summary>Input for setting a building's for-sale status.</summary>
public sealed class SetBuildingForSaleInput
{
    /// <summary>Building to update.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>Whether the building is listed for sale.</summary>
    public bool IsForSale { get; set; }

    /// <summary>Asking price (required when IsForSale is true).</summary>
    public decimal? AskingPrice { get; set; }
}

/// <summary>Input for permanently destroying a building owned by the authenticated player.</summary>
public sealed class DestroyBuildingInput
{
    /// <summary>Building to destroy.</summary>
    public Guid BuildingId { get; set; }
}

/// <summary>Input for setting the rent per m² on an apartment or commercial building.</summary>
public sealed class SetRentPerSqmInput
{
    /// <summary>Apartment or commercial building to configure.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>New rent per m² to apply after one in-game day (24 ticks).</summary>
    public decimal RentPerSqm { get; set; }
}

/// <summary>Input for setting the per-tick content spending budget for a media house building.</summary>
public sealed class SetMediaHouseContentBudgetInput
{
    /// <summary>MEDIA_HOUSE building to configure.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>
    /// Amount to spend on content per tick.
    /// Set to null or 0 to stop content investment.
    /// Must be non-negative.
    /// </summary>
    public decimal? ContentBudgetPerTick { get; set; }
}

/// <summary>Input for setting the per-tick spending level for a media house building.</summary>
public sealed class SetMediaHouseSpendingLevelInput
{
    /// <summary>MEDIA_HOUSE building to configure.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>
    /// Amount to spend on content per tick.
    /// Set to null or 0 to stop content investment.
    /// Must be non-negative.
    /// </summary>
    public decimal? SpendingLevelPerTick { get; set; }
}

/// <summary>Input for upgrading a media house building to the next level.</summary>
public sealed class UpgradeMediaHouseInput
{
    /// <summary>MEDIA_HOUSE building to upgrade.</summary>
    public Guid BuildingId { get; set; }
}

/// <summary>Input for creating/updating a media house campaign unit configuration.</summary>
public sealed class ConfigureMediaHouseUnitInput
{
    /// <summary>Owning MEDIA_HOUSE building.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>Optional unit ID. When omitted, a new unit is created.</summary>
    public Guid? UnitId { get; set; }

    /// <summary>Target company to boost. Must be owned by the authenticated player.</summary>
    public Guid TargetCompanyId { get; set; }

    /// <summary>Campaign media channel: NEWSPAPER, RADIO, TV.</summary>
    [Required, MaxLength(20)]
    public string MediaType { get; set; } = string.Empty;

    /// <summary>Campaign spending budget per tick. Must be non-negative.</summary>
    public decimal CampaignBudgetPerTick { get; set; }

    /// <summary>Whether this campaign unit is active.</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>Input for purchasing a building lot and placing a building on it.</summary>
public sealed class PurchaseLotInput
{
    /// <summary>Company that will purchase the lot and own the building.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>Building lot to purchase.</summary>
    public Guid LotId { get; set; }

    /// <summary>Building type to place on the lot (must be one of the lot's suitable types).</summary>
    [Required, MaxLength(30)]
    public string BuildingType { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the new building.
    /// Optional — when empty or omitted, a natural name is auto-generated
    /// from the building type and a sequential number (e.g. "Factory #2").
    /// </summary>
    [MaxLength(200)]
    public string? BuildingName { get; set; }

    /// <summary>
    /// Power plant subtype (COAL, GAS, SOLAR, WIND, NUCLEAR).
    /// Required when BuildingType is POWER_PLANT; ignored otherwise.
    /// </summary>
    [MaxLength(20)]
    public string? PowerPlantType { get; set; }

    /// <summary>
    /// Media house channel type: NEWSPAPER, RADIO, TV.
    /// Required when BuildingType is MEDIA_HOUSE; ignored otherwise.
    /// </summary>
    [MaxLength(20)]
    public string? MediaType { get; set; }
}

/// <summary>User-editable portion of a building unit configuration.</summary>
public sealed class BuildingConfigurationUnitInput
{
    /// <summary>Unit type for the grid cell.</summary>
    [Required, MaxLength(30)]
    public string UnitType { get; set; } = string.Empty;

    /// <summary>Grid column position (0-3).</summary>
    public int GridX { get; set; }

    /// <summary>Grid row position (0-3).</summary>
    public int GridY { get; set; }

    /// <summary>Whether the link to the unit above is active.</summary>
    public bool LinkUp { get; set; }

    /// <summary>Whether the link to the unit below is active.</summary>
    public bool LinkDown { get; set; }

    /// <summary>Whether the link to the unit on the left is active.</summary>
    public bool LinkLeft { get; set; }

    /// <summary>Whether the link to the unit on the right is active.</summary>
    public bool LinkRight { get; set; }

    /// <summary>Whether the diagonal link to the unit above-left is active.</summary>
    public bool LinkUpLeft { get; set; }

    /// <summary>Whether the diagonal link to the unit above-right is active.</summary>
    public bool LinkUpRight { get; set; }

    /// <summary>Whether the diagonal link to the unit below-left is active.</summary>
    public bool LinkDownLeft { get; set; }

    /// <summary>Whether the diagonal link to the unit below-right is active.</summary>
    public bool LinkDownRight { get; set; }

    // ── Unit-specific configuration ──

    /// <summary>Resource type this unit works with (optional, for Mining/Purchase/Storage/B2B Sales).</summary>
    public Guid? ResourceTypeId { get; set; }

    /// <summary>Product type this unit works with (optional, for Manufacturing/Purchase/Public Sales/Branding).</summary>
    public Guid? ProductTypeId { get; set; }

    /// <summary>Minimum selling price (for B2B Sales or Public Sales units).</summary>
    public decimal? MinPrice { get; set; }

    /// <summary>Maximum purchase price (for Purchase units).</summary>
    public decimal? MaxPrice { get; set; }

    /// <summary>Purchase source: EXCHANGE, LOCAL, OPTIMAL.</summary>
    [MaxLength(20)]
    public string? PurchaseSource { get; set; }

    /// <summary>Visibility: PUBLIC, COMPANY, GROUP.</summary>
    [MaxLength(20)]
    public string? SaleVisibility { get; set; }

    /// <summary>Marketing budget per tick (for Marketing units).</summary>
    public decimal? Budget { get; set; }

    /// <summary>Media house building ID (for Marketing units).</summary>
    public Guid? MediaHouseBuildingId { get; set; }

    /// <summary>Minimum product quality for Purchase units (0.0-1.0).</summary>
    public decimal? MinQuality { get; set; }

    /// <summary>Brand scope: PRODUCT, CATEGORY, COMPANY (for Branding units).</summary>
    [MaxLength(20)]
    public string? BrandScope { get; set; }

    /// <summary>Lock purchases to a specific vendor company ID (for Purchase units).</summary>
    public Guid? VendorLockCompanyId { get; set; }

    /// <summary>Lock exchange purchases to a specific source city ID (for Purchase units with EXCHANGE source).</summary>
    public Guid? LockedCityId { get; set; }

    /// <summary>
    /// Industry category for BRAND_QUALITY units with CATEGORY scope (e.g. "FURNITURE", "FOOD_PROCESSING").
    /// When provided, brand research targets this industry category directly.
    /// </summary>
    [MaxLength(50)]
    public string? IndustryCategory { get; set; }
}

/// <summary>Input for instantly updating the minimum sale price on a PUBLIC_SALES unit.</summary>
public sealed class UpdatePublicSalesPriceInput
{
    /// <summary>The PUBLIC_SALES building unit to update.</summary>
    public Guid UnitId { get; set; }

    /// <summary>New minimum sale price per unit. Must be greater than zero.</summary>
    public decimal NewMinPrice { get; set; }
}

/// <summary>Input for flushing inventory from a storage-capable building unit.</summary>
public sealed class FlushStorageInput
{
    /// <summary>The building unit whose inventory should be discarded.</summary>
    public Guid BuildingUnitId { get; set; }
}

/// <summary>Input for scheduling a level upgrade on a building unit.</summary>
public sealed class ScheduleUnitUpgradeInput
{
    /// <summary>The building unit to upgrade.</summary>
    public Guid UnitId { get; set; }
}

public sealed class SetPublicSalesInventoryAlertThresholdInput
{
    public Guid BuildingUnitId { get; set; }
    public decimal? MinInventoryThreshold { get; set; }
}

/// <summary>Input for a buyer to make a purchase offer on a for-sale building.</summary>
public sealed class MakeOfferOnBuildingInput
{
    /// <summary>The building to make an offer on.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>The company on behalf of which the buyer is purchasing.</summary>
    public Guid BuyerCompanyId { get; set; }

    /// <summary>The offered purchase price in the building city's currency.</summary>
    public decimal OfferedPrice { get; set; }

    /// <summary>Optional negotiation note. Only used when the listing allows negotiation.</summary>
    [MaxLength(500)]
    public string? NegotiationNote { get; set; }
}

/// <summary>Input for a seller to accept a pending offer.</summary>
public sealed class AcceptBuildingOfferInput
{
    /// <summary>The offer to accept.</summary>
    public Guid OfferId { get; set; }
    public Guid OfferVersion { get; set; }
}

/// <summary>Input for a seller to reject a pending offer.</summary>
public sealed class RejectBuildingOfferInput
{
    /// <summary>The offer to reject.</summary>
    public Guid OfferId { get; set; }
    public Guid OfferVersion { get; set; }
}

/// <summary>Input for a seller to cancel a pending offer.</summary>
public sealed class CancelBuildingOfferInput
{
    /// <summary>The offer to cancel.</summary>
    public Guid OfferId { get; set; }
    public Guid OfferVersion { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Represents a scheduled inter-city shipment of goods from a B2B sales unit
/// in one city to a purchase unit in a different city.
/// Routes progress through SCHEDULED → IN_TRANSIT → DELIVERED (or FAILED).
/// </summary>
public sealed class InterCityTradeRoute
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Company that owns this route (the seller's company).</summary>
    public Guid CompanyId { get; set; }

    /// <summary>Navigation property to the owning company.</summary>
    public Company Company { get; set; } = null!;

    /// <summary>Building where goods originate (must have a B2B sales unit).</summary>
    public Guid SourceBuildingId { get; set; }

    /// <summary>Navigation to source building.</summary>
    public Building SourceBuilding { get; set; } = null!;

    /// <summary>Specific B2B sales unit in the source building.</summary>
    public Guid SourceBuildingUnitId { get; set; }

    /// <summary>Navigation to the source B2B sales unit.</summary>
    public BuildingUnit SourceBuildingUnit { get; set; } = null!;

    /// <summary>Building where goods are delivered (must have a purchase unit).</summary>
    public Guid DestinationBuildingId { get; set; }

    /// <summary>Navigation to destination building.</summary>
    public Building DestinationBuilding { get; set; } = null!;

    /// <summary>Specific purchase unit in the destination building.</summary>
    public Guid DestinationBuildingUnitId { get; set; }

    /// <summary>Navigation to the destination purchase unit.</summary>
    public BuildingUnit DestinationBuildingUnit { get; set; } = null!;

    /// <summary>Product type being shipped (null when shipping a resource).</summary>
    public Guid? ProductTypeId { get; set; }

    /// <summary>Navigation to product type.</summary>
    public ProductType? ProductType { get; set; }

    /// <summary>Resource type being shipped (null when shipping a product).</summary>
    public Guid? ResourceTypeId { get; set; }

    /// <summary>Navigation to resource type.</summary>
    public ResourceType? ResourceType { get; set; }

    /// <summary>Quantity of goods dispatched.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Quality of the shipped goods (blended quality at dispatch time).</summary>
    public decimal Quality { get; set; } = 0.5m;

    /// <summary>Sourcing cost carried with the inventory.</summary>
    public decimal SourcingCostTotal { get; set; }

    /// <summary>Price per unit set by the source B2B unit.</summary>
    public decimal PricePerUnit { get; set; }

    /// <summary>Game tick when the route was scheduled to depart.</summary>
    public long ScheduledDepartureTick { get; set; }

    /// <summary>Game tick when the route is expected to arrive.</summary>
    public long ExpectedArrivalTick { get; set; }

    /// <summary>Tick distance = transit time in ticks.</summary>
    public long TransitTicks { get; set; }

    /// <summary>Estimated shipping cost (calculated at creation).</summary>
    public decimal ShippingCostEstimate { get; set; }

    /// <summary>Actual shipping cost settled at delivery.</summary>
    public decimal ShippingCostActual { get; set; }

    /// <summary>
    /// Route status: SCHEDULED, IN_TRANSIT, DELIVERED, or FAILED.
    /// </summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = TradeRouteStatus.Scheduled;

    /// <summary>Optional failure reason when status is FAILED.</summary>
    [MaxLength(500)]
    public string? FailureReason { get; set; }

    /// <summary>UTC timestamp when the route was created.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when the route departed (moved to IN_TRANSIT).</summary>
    public DateTime? DepartedAtUtc { get; set; }

    /// <summary>UTC timestamp when the route completed (DELIVERED or FAILED).</summary>
    public DateTime? CompletedAtUtc { get; set; }
}

/// <summary>Status codes for <see cref="InterCityTradeRoute"/>.</summary>
public static class TradeRouteStatus
{
    /// <summary>Route is queued but goods have not been dispatched yet.</summary>
    public const string Scheduled = "SCHEDULED";

    /// <summary>Goods are in transit between cities.</summary>
    public const string InTransit = "IN_TRANSIT";

    /// <summary>Goods arrived and were accepted by the destination unit.</summary>
    public const string Delivered = "DELIVERED";

    /// <summary>Delivery failed (e.g. destination unit full); goods returned to source.</summary>
    public const string Failed = "FAILED";
}

namespace Api.Data.Entities;

/// <summary>
/// Aggregated per-tick movement history for a single resource or product inside
/// a building unit.
/// </summary>
public sealed class BuildingUnitResourceHistory
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning building.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>Navigation property to the owning building.</summary>
    public Building Building { get; set; } = null!;

    /// <summary>Owning building unit.</summary>
    public Guid BuildingUnitId { get; set; }

    /// <summary>Navigation property to the owning unit.</summary>
    public BuildingUnit BuildingUnit { get; set; } = null!;

    /// <summary>Raw material tracked by this history point, when applicable.</summary>
    public Guid? ResourceTypeId { get; set; }

    /// <summary>Navigation property to the tracked resource type.</summary>
    public ResourceType? ResourceType { get; set; }

    /// <summary>Manufactured product tracked by this history point, when applicable.</summary>
    public Guid? ProductTypeId { get; set; }

    /// <summary>Navigation property to the tracked product type.</summary>
    public ProductType? ProductType { get; set; }

    /// <summary>Simulation tick this aggregate belongs to.</summary>
    public long Tick { get; set; }

    /// <summary>Total quantity that entered the unit on this tick.</summary>
    public decimal InflowQuantity { get; set; }

    /// <summary>Total quantity that left the unit on this tick.</summary>
    public decimal OutflowQuantity { get; set; }

    /// <summary>Total quantity consumed by the unit's internal processing on this tick.</summary>
    public decimal ConsumedQuantity { get; set; }

    /// <summary>Total quantity produced by the unit's internal processing on this tick.</summary>
    public decimal ProducedQuantity { get; set; }
}
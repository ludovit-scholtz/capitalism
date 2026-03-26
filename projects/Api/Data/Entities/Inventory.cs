using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Tracks inventory of resources and products stored in building storage units
/// or exchange warehouses.
/// </summary>
public sealed class Inventory
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The building that holds this inventory.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>Navigation property to the building.</summary>
    public Building Building { get; set; } = null!;

    /// <summary>Resource type (null if this is a finished product).</summary>
    public Guid? ResourceTypeId { get; set; }

    /// <summary>Navigation property to the resource type.</summary>
    public ResourceType? ResourceType { get; set; }

    /// <summary>Product type (null if this is a raw resource).</summary>
    public Guid? ProductTypeId { get; set; }

    /// <summary>Navigation property to the product type.</summary>
    public ProductType? ProductType { get; set; }

    /// <summary>Quantity in stock.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Quality rating (0.0-1.0) for manufactured products.</summary>
    public decimal Quality { get; set; } = 0.5m;

    /// <summary>Brand associated with the product (null for raw materials).</summary>
    public Guid? BrandId { get; set; }
}

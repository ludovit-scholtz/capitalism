using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Records exchange orders for buying and selling resources/products
/// through the exchange building.
/// </summary>
public sealed class ExchangeOrder
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The exchange building where this order is placed.</summary>
    public Guid ExchangeBuildingId { get; set; }

    /// <summary>Navigation property to the exchange building.</summary>
    public Building ExchangeBuilding { get; set; } = null!;

    /// <summary>The company placing the order.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>Navigation property to the company.</summary>
    public Company Company { get; set; } = null!;

    /// <summary>Order side: BUY or SELL.</summary>
    [Required, MaxLength(10)]
    public string Side { get; set; } = "BUY";

    /// <summary>Resource type being traded (null if product).</summary>
    public Guid? ResourceTypeId { get; set; }

    /// <summary>Product type being traded (null if resource).</summary>
    public Guid? ProductTypeId { get; set; }

    /// <summary>Price per unit.</summary>
    public decimal PricePerUnit { get; set; }

    /// <summary>Quantity offered or requested.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Remaining quantity to be filled.</summary>
    public decimal RemainingQuantity { get; set; }

    /// <summary>Minimum quality required (for buy orders).</summary>
    public decimal? MinQuality { get; set; }

    /// <summary>Whether the order is still active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when the order was placed.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

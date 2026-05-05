using Api.Data.Entities;

namespace Api.Types;

/// <summary>Projected supply offer at a city's global exchange.</summary>
public sealed class GlobalExchangeOffer
{
    public Guid CityId { get; set; }
    public string CityName { get; set; } = string.Empty;
    public Guid ResourceTypeId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceSlug { get; set; } = string.Empty;
    public string UnitSymbol { get; set; } = string.Empty;
    public decimal LocalAbundance { get; set; }
    public decimal ExchangePricePerUnit { get; set; }

    /// <summary>Typical (central) quality for this city/resource abundance level.</summary>
    public decimal EstimatedQuality { get; set; }

    /// <summary>
    /// Minimum quality in the variability band. Actual purchase quality varies
    /// between <see cref="QualityMin"/> and <see cref="QualityMax"/> each tick.
    /// </summary>
    public decimal QualityMin { get; set; }

    /// <summary>
    /// Maximum quality in the variability band. Actual purchase quality varies
    /// between <see cref="QualityMin"/> and <see cref="QualityMax"/> each tick.
    /// </summary>
    public decimal QualityMax { get; set; }

    public decimal TransitCostPerUnit { get; set; }
    public decimal DeliveredPricePerUnit { get; set; }
    public decimal DistanceKm { get; set; }

    /// <summary>
    /// Destination city fuel price index applied to this transit cost
    /// (1.0 = EUR baseline; values above 1.0 indicate costlier local fuel).
    /// </summary>
    public decimal FuelPriceIndex { get; set; } = 1.0m;

    /// <summary>
    /// Last 50 ticks of ask-price history for this city/resource offer.
    /// Used by the global exchange sparkline UI.
    /// </summary>
    public List<ResourceAskPricePoint> AskPriceHistory { get; set; } = [];
}

public sealed class ResourceAskPricePoint
{
    public long Tick { get; set; }
    public decimal AskPricePerUnit { get; set; }
}

/// <summary>
/// A product marketplace listing from a player-placed SELL exchange order.
/// Represents a specific offer to sell a manufactured or intermediate product.
/// </summary>
public sealed class GlobalExchangeProductListing
{
    /// <summary>The exchange order ID backing this listing.</summary>
    public Guid OrderId { get; set; }

    /// <summary>The product type being offered.</summary>
    public Guid ProductTypeId { get; set; }

    /// <summary>Human-readable product name.</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>URL-friendly product identifier.</summary>
    public string ProductSlug { get; set; } = string.Empty;

    /// <summary>Industry category: FURNITURE, FOOD_PROCESSING, HEALTHCARE, etc.</summary>
    public string ProductIndustry { get; set; } = string.Empty;

    /// <summary>Short display symbol for the produced unit (e.g. pcs).</summary>
    public string UnitSymbol { get; set; } = string.Empty;

    /// <summary>Display name for the produced unit (e.g. Piece, Crate).</summary>
    public string UnitName { get; set; } = string.Empty;

    /// <summary>Base market price per unit from the product catalogue.</summary>
    public decimal BasePrice { get; set; }

    /// <summary>Asking price per unit for this specific listing.</summary>
    public decimal PricePerUnit { get; set; }

    /// <summary>Remaining quantity available in this order.</summary>
    public decimal RemainingQuantity { get; set; }

    /// <summary>City where the selling exchange building is located.</summary>
    public Guid SellerCityId { get; set; }

    /// <summary>Name of the seller's city.</summary>
    public string SellerCityName { get; set; } = string.Empty;

    /// <summary>Company that placed this sell order.</summary>
    public Guid SellerCompanyId { get; set; }

    /// <summary>Name of the selling company.</summary>
    public string SellerCompanyName { get; set; } = string.Empty;

    /// <summary>When this order was created.</summary>
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// A single line in the shared in-game chat feed.
/// </summary>
public sealed class InGameChatMessage
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public string PlayerDisplayName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; }
    public bool IsOwnMessage { get; set; }
}

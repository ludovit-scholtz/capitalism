using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Represents a buyer's offer on a building that is listed for sale.
/// Created via the <c>makeOfferOnBuilding</c> mutation.
/// Accepted via <c>acceptBuildingOffer</c> which triggers the atomic transfer.
/// Rejected via <c>rejectBuildingOffer</c>.
/// </summary>
public sealed class BuildingSaleOffer
{
    public Guid Id { get; set; }
    public Guid OfferVersion { get; set; } = Guid.NewGuid();

    /// <summary>The building this offer targets.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>Navigation property to the building.</summary>
    public Building Building { get; set; } = null!;

    /// <summary>The player who made this offer.</summary>
    public Guid BuyerPlayerId { get; set; }

    /// <summary>Navigation property to the buyer player.</summary>
    public Player BuyerPlayer { get; set; } = null!;

    /// <summary>The company on behalf of which the buyer is purchasing.</summary>
    public Guid BuyerCompanyId { get; set; }

    /// <summary>Navigation property to the buyer company.</summary>
    public Company BuyerCompany { get; set; } = null!;

    /// <summary>Offered purchase price in the building city's currency.</summary>
    public decimal OfferedPrice { get; set; }

    /// <summary>
    /// Amount currently reserved in escrow for this offer.
    /// This is debited when the offer is placed and released when the offer is rejected/cancelled.
    /// </summary>
    public decimal EscrowAmount { get; set; }

    /// <summary>ISO 4217 currency code for <see cref="EscrowAmount"/>.</summary>
    [MaxLength(3)]
    public string EscrowCurrencyCode { get; set; } = "EUR";

    /// <summary>
    /// Optional note from the buyer (for negotiation context).
    /// Allowed when the listing has <c>AllowNegotiation = true</c>.
    /// </summary>
    [MaxLength(500)]
    public string? NegotiationNote { get; set; }

    /// <summary>Offer status: PENDING, ACCEPTED, REJECTED.</summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = BuildingSaleOfferStatus.Pending;

    /// <summary>UTC timestamp when the offer was submitted.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when the offer was resolved (accepted/rejected).</summary>
    public DateTime? ResolvedAtUtc { get; set; }
}

public static class BuildingSaleOfferStatus
{
    public const string Pending = "PENDING";
    public const string Accepted = "ACCEPTED";
    public const string Rejected = "REJECTED";
}

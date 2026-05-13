using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Represents a power plant owner's offer to sell surplus electricity capacity
/// on the intra-city energy spot market.
///
/// Active listings are auto-matched each tick by <c>EnergySpotMarketPhase</c>:
/// deficit buildings whose <see cref="Building.MaxEnergyBidPrice"/> is ≥
/// <see cref="PricePerKwhLocal"/> can purchase up to <see cref="AvailableKw"/> kW.
/// </summary>
public sealed class EnergyListing
{
    /// <summary>Unique listing identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The power-plant building offering capacity for sale.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>Navigation property to the power-plant building.</summary>
    public Building Building { get; set; } = null!;

    /// <summary>The company that owns the power plant (denormalized for query efficiency).</summary>
    public Guid CompanyId { get; set; }

    /// <summary>Navigation property to the owning company.</summary>
    public Company Company { get; set; } = null!;

    /// <summary>The city where the power plant is located (denormalized for fast city-scoped queries).</summary>
    public Guid CityId { get; set; }

    /// <summary>Navigation property to the city.</summary>
    public City City { get; set; } = null!;

    /// <summary>
    /// Asking price per kWh in the city's local currency.
    /// Buyers whose <see cref="Building.MaxEnergyBidPrice"/> is ≥ this value will be auto-matched.
    /// </summary>
    public decimal PricePerKwhLocal { get; set; }

    /// <summary>
    /// Total capacity offered for sale in kW.
    /// The listing owner commits this capacity to the spot market each tick.
    /// </summary>
    public decimal CapacityKw { get; set; }

    /// <summary>
    /// Remaining unallocated capacity in kW.
    /// Decremented each tick as deficit buildings purchase power.
    /// Reset to <see cref="CapacityKw"/> at the start of each tick before matching.
    /// </summary>
    public decimal AvailableKw { get; set; }

    /// <summary>True while the listing is accepting orders; false after <c>cancelEnergyListing</c>.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Tick at which the listing was created.</summary>
    public long CreatedAtTick { get; set; }

    /// <summary>UTC timestamp when the listing was created.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when the listing was cancelled. Null if still active.</summary>
    public DateTime? CancelledAtUtc { get; set; }
}

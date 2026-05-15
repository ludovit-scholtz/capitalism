using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Public procurement tender opened by a city government for a specific product.
/// </summary>
public sealed class GovernmentContract
{
    public Guid Id { get; set; }

    public Guid CityId { get; set; }
    public City City { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    public Guid ProductTypeId { get; set; }
    public ProductType ProductType { get; set; } = null!;

    public decimal QuantityRequired { get; set; }

    /// <summary>Minimum required quality level on a 0–10 scale.</summary>
    public decimal MinimumQuality { get; set; }

    /// <summary>Maximum allowed bid price per unit in the city currency.</summary>
    public decimal BudgetCap { get; set; }

    public long DeadlineTick { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = GovernmentContractStatus.Open;

    public Guid? WinnerCompanyId { get; set; }
    public Company? WinnerCompany { get; set; }

    public long CreatedAtTick { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Set when a deadline warning has already been sent to the winner.</summary>
    public long? DeadlineWarningSentAtTick { get; set; }

    public List<ContractBid> Bids { get; set; } = [];
    public ContractFulfillment? Fulfillment { get; set; }
}

public static class GovernmentContractStatus
{
    public const string Open = "OPEN";
    public const string Awarded = "AWARDED";
    public const string Fulfilled = "FULFILLED";
    public const string Expired = "EXPIRED";
}

/// <summary>
/// Single company bid submitted against a government contract.
/// </summary>
public sealed class ContractBid
{
    public Guid Id { get; set; }

    public Guid ContractId { get; set; }
    public GovernmentContract Contract { get; set; } = null!;

    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public decimal BidPricePerUnit { get; set; }
    public long EstimatedDeliveryTick { get; set; }
    public long SubmittedAtTick { get; set; }
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Shipment progress for an awarded contract.
/// </summary>
public sealed class ContractFulfillment
{
    public Guid Id { get; set; }

    public Guid ContractId { get; set; }
    public GovernmentContract Contract { get; set; } = null!;

    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public decimal QuantityDelivered { get; set; }
    public decimal QuantityRequired { get; set; }
    public long LastShipmentTick { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
}

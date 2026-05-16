using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Long-term B2B supply agreement between two companies.
/// </summary>
public sealed class SupplyContract
{
    public Guid Id { get; set; }

    public Guid SellerCompanyId { get; set; }
    public Company SellerCompany { get; set; } = null!;

    public Guid BuyerCompanyId { get; set; }
    public Company BuyerCompany { get; set; } = null!;

    public Guid SellerBuildingUnitId { get; set; }
    public BuildingUnit SellerBuildingUnit { get; set; } = null!;

    public Guid? ResourceTypeId { get; set; }
    public ResourceType? ResourceType { get; set; }

    public Guid? ProductTypeId { get; set; }
    public ProductType? ProductType { get; set; }

    public decimal QuantityPerTick { get; set; }
    public decimal PricePerUnit { get; set; }
    public int DurationTicks { get; set; }
    public int RemainingTicks { get; set; }

    public long StartTick { get; set; }
    public decimal PenaltyRatePercent { get; set; }

    [Required, MaxLength(8)]
    public string CurrencyCode { get; set; } = "EUR";

    [Required, MaxLength(20)]
    public string Status { get; set; } = SupplyContractStatus.Pending;

    public long CreatedAtTick { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public long? ActivatedAtTick { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    public long? CompletedAtTick { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public long? CancelledAtTick { get; set; }
    public DateTime? CancelledAtUtc { get; set; }

    public decimal TotalDeliveredQuantity { get; set; }
    public decimal TotalUndeliveredQuantity { get; set; }
    public decimal TotalPenaltyAmount { get; set; }
    public int PenaltyCount { get; set; }
    public bool FirstDeliveryNotified { get; set; }
}

public static class SupplyContractStatus
{
    public const string Pending = "PENDING";
    public const string Active = "ACTIVE";
    public const string Fulfilled = "FULFILLED";
    public const string Breached = "BREACHED";
    public const string Cancelled = "CANCELLED";
}

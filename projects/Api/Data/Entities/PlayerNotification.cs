using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// In-game notification shown to a player in the navbar bell panel.
/// </summary>
public sealed class PlayerNotification
{
    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    [Required, MaxLength(60)]
    public string Type { get; set; } = PlayerNotificationType.Generic;

    [Required, MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    [Required, MaxLength(16)]
    public string Severity { get; set; } = PlayerNotificationSeverity.Info;

    [MaxLength(200)]
    public string? TitleKey { get; set; }

    [MaxLength(200)]
    public string? BodyKey { get; set; }

    [MaxLength(4000)]
    public string? BodyParamsJson { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }

    public long CreatedAtTick { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAtUtc { get; set; }

    public Guid? CompanyId { get; set; }
    public Guid? BuildingId { get; set; }
    public Guid? BuildingUnitId { get; set; }
    public Guid? BankAccountId { get; set; }
    public Guid? LoanId { get; set; }

    [MaxLength(60)]
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
}

public static class PlayerNotificationType
{
    public const string Generic = "GENERIC";
    public const string CityExpansionUnlocked = "CITY_EXPANSION_UNLOCKED";
    public const string ShipmentArrived = "SHIPMENT_ARRIVED";
    public const string LogisticsMarginErosion = "LOGISTICS_MARGIN_EROSION";
    public const string LoanPaymentMissed = "LOAN_PAYMENT_MISSED";
    public const string BuildingConstructionCompleted = "BUILDING_CONSTRUCTION_COMPLETED";
    public const string BuildingUpgradeApplied = "BUILDING_UPGRADE_APPLIED";
    public const string LoanRepaymentDueSoon = "LOAN_REPAYMENT_DUE_SOON";
    public const string BankAccountLowBalance = "BANK_ACCOUNT_LOW_BALANCE";
    public const string PublicSalesInventoryLow = "PUBLIC_SALES_INVENTORY_LOW";
    public const string B2BSaleFulfilled = "B2B_SALE_FULFILLED";
    public const string BuildingOfferReceived = "BUILDING_OFFER_RECEIVED";
    public const string BuildingOfferAccepted = "BUILDING_OFFER_ACCEPTED";
    public const string BuildingOfferRejected = "BUILDING_OFFER_REJECTED";
    public const string BuildingSoldSuccessfully = "BUILDING_SOLD_SUCCESSFULLY";
    public const string MineLowReserveWarning = "MINE_LOW_RESERVE_WARNING";
    public const string MineCriticalReserveWarning = "MINE_CRITICAL_RESERVE_WARNING";
    public const string MineFullyDepleted = "MINE_FULLY_DEPLETED";
    public const string MineReplenished = "MINE_REPLENISHED";
    public const string BuildingDestroyedByDefault = "BUILDING_DESTROYED_BY_DEFAULT";
    public const string DividendProposalOpened = "DIVIDEND_PROPOSAL_OPENED";
    public const string DividendProposalSettled = "DIVIDEND_PROPOSAL_SETTLED";
    public const string EconomicAlert = "ECONOMIC_ALERT";
    public const string ProductionHalted = "PRODUCTION_HALTED";
    public const string LoanPaymentDue = "LOAN_PAYMENT_DUE";
    public const string LoanDefault = "LOAN_DEFAULT";
    public const string PriceSpike = "PRICE_SPIKE";
    public const string OversupplyWarning = "OVERSUPPLY_WARNING";
    public const string TakeoverAlert = "TAKEOVER_ALERT";
    public const string MineDepleting = "MINE_DEPLETING";
}

public static class PlayerNotificationSeverity
{
    public const string Info = "INFO";
    public const string Warning = "WARNING";
    public const string Critical = "CRITICAL";
}

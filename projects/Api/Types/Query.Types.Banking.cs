using Api.Data.Entities;

namespace Api.Types;

/// <summary>Read model for a bank deposit (depositor or owner view).</summary>
public sealed class BankDepositSummary
{
    public Guid Id { get; set; }
    public Guid BankBuildingId { get; set; }
    public string BankBuildingName { get; set; } = string.Empty;
    public Guid DepositorCompanyId { get; set; }
    public string DepositorCompanyName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal DepositInterestRatePercent { get; set; }
    public bool IsBaseCapital { get; set; }
    public bool IsActive { get; set; }
    public long DepositedAtTick { get; set; }
    public DateTime DepositedAtUtc { get; set; }
    public long? WithdrawnAtTick { get; set; }
    public DateTime? WithdrawnAtUtc { get; set; }
    public decimal TotalInterestPaid { get; set; }
    /// <summary>ISO 4217 currency code for the city where the bank is located (e.g. "EUR", "CZK").</summary>
    public string CityCurrencyCode { get; set; } = "EUR";
}

/// <summary>Public summary for a bank building: rates, capacity, and reserve status.</summary>
public sealed class BankInfoSummary
{
    public Guid BankBuildingId { get; set; }
    public string BankBuildingName { get; set; } = string.Empty;
    public Guid CityId { get; set; }
    public string CityName { get; set; } = string.Empty;
    /// <summary>ISO 4217 currency code for the city (e.g. "EUR", "CZK").</summary>
    public string CityCurrencyCode { get; set; } = "EUR";
    /// <summary>Display symbol for the city currency (e.g. "€", "Kč").</summary>
    public string CityCurrencySymbol { get; set; } = "€";
    /// <summary>Required base capital amount in the local city currency to open this bank.</summary>
    public decimal BaseCapitalRequirement { get; set; }
    public Guid LenderCompanyId { get; set; }
    public string LenderCompanyName { get; set; } = string.Empty;
    /// <summary>Annual rate (%) the bank pays to depositors.</summary>
    public decimal DepositInterestRatePercent { get; set; }
    /// <summary>Annual rate (%) the bank charges on loans.</summary>
    public decimal LendingInterestRatePercent { get; set; }
    /// <summary>Total active deposits in the bank.</summary>
    public decimal TotalDeposits { get; set; }
    /// <summary>90% of total deposits — the maximum lendable amount.</summary>
    public decimal LendableCapacity { get; set; }
    /// <summary>Currently outstanding loan principal from this bank.</summary>
    public decimal OutstandingLoanPrincipal { get; set; }
    /// <summary>Available capacity to issue new loans (LendableCapacity - OutstandingLoanPrincipal).</summary>
    public decimal AvailableLendingCapacity { get; set; }
    /// <summary>Whether the bank has met the base-capital deposit requirement.</summary>
    public bool BaseCapitalDeposited { get; set; }

    // ── Liquidity / Central-Bank fields (owner view) ─────────────────────────

    /// <summary>Outstanding debt owed to the central bank as emergency liquidity funding.</summary>
    public decimal CentralBankDebt { get; set; }
    /// <summary>Current variable interest rate charged by the central bank on emergency funding (2–5% p.a.).</summary>
    public decimal CentralBankInterestRatePercent { get; set; }
    /// <summary>Minimum cash the bank must hold as reserve (10% of total deposits).</summary>
    public decimal ReserveRequirement { get; set; }
    /// <summary>Bank company's actual cash balance.</summary>
    public decimal AvailableCash { get; set; }
    /// <summary>Amount by which available cash falls short of the reserve requirement (0 when healthy).</summary>
    public decimal ReserveShortfall { get; set; }
    /// <summary>Liquidity status: HEALTHY, PRESSURED, or CRITICAL.</summary>
    public string LiquidityStatus { get; set; } = BankLiquidityStatus.Healthy;
}

/// <summary>Liquidity health states for bank buildings.</summary>
public static class BankLiquidityStatus
{
    /// <summary>Bank has sufficient reserves and no central-bank debt.</summary>
    public const string Healthy = "HEALTHY";
    /// <summary>Bank has central-bank debt but cash covers the reserve requirement.</summary>
    public const string Pressured = "PRESSURED";
    /// <summary>Bank's cash is below the reserve requirement or central-bank debt is large.</summary>
    public const string Critical = "CRITICAL";
}

/// <summary>Read model for a loan offer visible to borrowers or bank owners.</summary>
public sealed class LoanOfferSummary
{
    public Guid Id { get; set; }
    public Guid BankBuildingId { get; set; }
    public string BankBuildingName { get; set; } = string.Empty;
    public Guid CityId { get; set; }
    public string CityName { get; set; } = string.Empty;
    public Guid LenderCompanyId { get; set; }
    public string LenderCompanyName { get; set; } = string.Empty;
    public decimal AnnualInterestRatePercent { get; set; }
    public decimal MaxPrincipalPerLoan { get; set; }
    public decimal TotalCapacity { get; set; }
    public decimal UsedCapacity { get; set; }
    public decimal RemainingCapacity { get; set; }
    public long DurationTicks { get; set; }
    public bool IsActive { get; set; }
    public long CreatedAtTick { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>Read model for an active or historical loan (borrower or lender view).</summary>
public sealed class LoanSummary
{
    public Guid Id { get; set; }
    public Guid LoanOfferId { get; set; }
    public Guid BorrowerCompanyId { get; set; }
    public string BorrowerCompanyName { get; set; } = string.Empty;
    public Guid LenderCompanyId { get; set; }
    public string LenderCompanyName { get; set; } = string.Empty;
    public Guid BankBuildingId { get; set; }
    public string BankBuildingName { get; set; } = string.Empty;
    public decimal OriginalPrincipal { get; set; }
    public decimal RemainingPrincipal { get; set; }
    public decimal AnnualInterestRatePercent { get; set; }
    public long DurationTicks { get; set; }
    public long StartTick { get; set; }
    public long DueTick { get; set; }
    public long NextPaymentTick { get; set; }
    public decimal PaymentAmount { get; set; }
    public int PaymentsMade { get; set; }
    public int TotalPayments { get; set; }
    public string Status { get; set; } = string.Empty;
    public int MissedPayments { get; set; }
    public decimal AccumulatedPenalty { get; set; }
    public DateTime AcceptedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }

    // ── Collateral ─────────────────────────────────────────────────────────────────
    /// <summary>ID of the building pledged as collateral, or null for unsecured loans.</summary>
    public Guid? CollateralBuildingId { get; set; }
    /// <summary>Display name of the collateral building, or null for unsecured loans.</summary>
    public string? CollateralBuildingName { get; set; }
    /// <summary>Appraised value of the collateral building at origination, or null for unsecured loans.</summary>
    public decimal? CollateralAppraisedValue { get; set; }
}

/// <summary>
/// Collateral eligibility and capacity summary for one of the player's buildings.
/// Returned by the <c>myCollateralBuildings</c> query so borrowers can compare buildings
/// before choosing which to pledge.
/// </summary>
public sealed class CollateralEligibilitySummary
{
    /// <summary>Building ID.</summary>
    public Guid BuildingId { get; set; }
    /// <summary>Display name of the building.</summary>
    public string BuildingName { get; set; } = string.Empty;
    /// <summary>Building type (e.g. FACTORY, MINE).</summary>
    public string BuildingType { get; set; } = string.Empty;
    /// <summary>Current building level.</summary>
    public int Level { get; set; }
    /// <summary>Appraised value of the building (used as the LTV base).</summary>
    public decimal AppraisedValue { get; set; }
    /// <summary>Maximum borrowable amount (70% of appraised value).</summary>
    public decimal MaxBorrowable { get; set; }
    /// <summary>Sum of remaining principal on all active secured loans against this building.</summary>
    public decimal ExistingSecuredExposure { get; set; }
    /// <summary>Remaining borrowing capacity (MaxBorrowable - ExistingSecuredExposure, floored at 0).</summary>
    public decimal RemainingBorrowingCapacity { get; set; }
    /// <summary>
    /// True when the building can currently be pledged.
    /// False when it is already pledged as collateral for another active loan.
    /// </summary>
    public bool IsEligible { get; set; }
    /// <summary>Human-readable reason the building is ineligible, or null when eligible.</summary>
    public string? IneligibilityReason { get; set; }
    /// <summary>
    /// ISO 4217 currency code for all monetary fields in this summary (AppraisedValue,
    /// MaxBorrowable, ExistingSecuredExposure, RemainingBorrowingCapacity).
    /// When a bankBuildingId was supplied to the query, this is the bank city currency.
    /// Otherwise it is the collateral building's own city currency.
    /// </summary>
    public string CurrencyCode { get; set; } = "EUR";
}

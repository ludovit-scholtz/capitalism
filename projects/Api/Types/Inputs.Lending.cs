using System.ComponentModel.DataAnnotations;
using Api.Data.Entities;

namespace Api.Types;

/// <summary>Input for publishing a new loan offer from a bank building.</summary>
public sealed class PublishLoanOfferInput
{
    /// <summary>The bank building that will publish the offer.</summary>
    public Guid BankBuildingId { get; set; }

    /// <summary>Annual interest rate as a percentage (e.g. 12.5 = 12.5%). Must be between 0.1 and 200.</summary>
    public decimal AnnualInterestRatePercent { get; set; }

    /// <summary>Maximum principal any single borrower can take (must be >= 1000).</summary>
    public decimal MaxPrincipalPerLoan { get; set; }

    /// <summary>Total capital committed to this offer across all borrowers (must be >= MaxPrincipalPerLoan).</summary>
    public decimal TotalCapacity { get; set; }

    /// <summary>Repayment duration in ticks (must be between 24 and 87600, i.e. 1 in-game day to 10 in-game years).</summary>
    public long DurationTicks { get; set; }
}

/// <summary>Input for updating an existing loan offer.</summary>
public sealed class UpdateLoanOfferInput
{
    /// <summary>ID of the loan offer to update.</summary>
    public Guid LoanOfferId { get; set; }

    /// <summary>Updated annual interest rate. Must be between 0.1 and 200.</summary>
    public decimal? AnnualInterestRatePercent { get; set; }

    /// <summary>Updated maximum principal per loan.</summary>
    public decimal? MaxPrincipalPerLoan { get; set; }

    /// <summary>Updated total capacity.</summary>
    public decimal? TotalCapacity { get; set; }

    /// <summary>Updated duration in ticks.</summary>
    public long? DurationTicks { get; set; }

    /// <summary>Whether the offer should be active (visible to borrowers).</summary>
    public bool? IsActive { get; set; }
}

/// <summary>Input for accepting a loan offer.</summary>
public sealed class AcceptLoanInput
{
    /// <summary>The loan offer to accept.</summary>
    public Guid LoanOfferId { get; set; }

    /// <summary>The company that will borrow the money.</summary>
    public Guid BorrowerCompanyId { get; set; }

    /// <summary>Principal amount to borrow (must be <= offer MaxPrincipalPerLoan and <= remaining capacity).</summary>
    public decimal PrincipalAmount { get; set; }

    /// <summary>
    /// Optional duration in ticks for direct bank borrowing.
    /// When omitted, the backend uses its default loan duration.
    /// </summary>
    public long? DurationTicks { get; set; }

    /// <summary>
    /// Optional: ID of a building owned by the borrower to pledge as collateral.
    /// When provided, the loan is secured and the principal is capped at 70% of the
    /// building's appraised value minus any existing secured exposure on the same asset.
    /// </summary>
    public Guid? CollateralBuildingId { get; set; }

    /// <summary>
    /// Optional settlement account to receive disbursement and service repayments.
    /// When provided, the account must belong to the borrower company and match the bank city currency.
    /// </summary>
    public Guid? BankAccountId { get; set; }
}

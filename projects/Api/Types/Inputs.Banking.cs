using System.ComponentModel.DataAnnotations;
using Api.Data.Entities;

namespace Api.Types;

/// <summary>Input for opening a bank account at a bank building.</summary>
public sealed class OpenBankAccountInput
{
    /// <summary>The bank building where the account is opened.</summary>
    public Guid BankBuildingId { get; set; }

    /// <summary>
    /// Optional company opening the account (must be owned by the authenticated player).
    /// When omitted, the authenticated player's personal account context is used.
    /// </summary>
    public Guid? DepositorCompanyId { get; set; }

    /// <summary>Initial account balance. Zero is allowed and is the default onboarding flow.</summary>
    public decimal Amount { get; set; }
}

/// <summary>Input for withdrawing from or fully closing a bank account.</summary>
public sealed class CloseBankAccountInput
{
    /// <summary>The account record to withdraw from.</summary>
    public Guid DepositId { get; set; }

    /// <summary>Amount to withdraw. Pass the full balance for a complete account closure.</summary>
    public decimal Amount { get; set; }
}

/// <summary>
/// Input for permanently closing a regular company bank account whose balance is exactly zero.
/// Use this for non-deposit accounts (company treasury accounts with no bank-building association).
/// Deposit accounts held at a bank building must be closed via <c>closeBankAccount</c> instead.
/// </summary>
public sealed class CloseCompanyBankAccountInput
{
    /// <summary>The company bank account to close. Must be a non-deposit account owned by one of the caller's companies.</summary>
    public Guid BankAccountId { get; set; }
}

/// <summary>Input for adding funds to an existing bank deposit (top-up).</summary>
public sealed class TopUpDepositInput
{
    /// <summary>The existing deposit to add funds to.</summary>
    public Guid DepositId { get; set; }

    /// <summary>Amount to add (must be >= 1,000).</summary>
    public decimal Amount { get; set; }
}

/// <summary>Input for configuring a bank's deposit and lending interest rates.</summary>
public sealed class SetBankRatesInput
{
    /// <summary>The bank building to configure (must be owned by the authenticated player's company).</summary>
    public Guid BankBuildingId { get; set; }

    /// <summary>Annual interest rate (%) to pay depositors. Must be between 0 and 100.</summary>
    public decimal DepositInterestRatePercent { get; set; }

    /// <summary>Annual interest rate (%) to charge borrowers. Must be between 0.1 and 200.</summary>
    public decimal LendingInterestRatePercent { get; set; }
}

/// <summary>Input for funding a building's assigned bank account from company cash.</summary>
public sealed class FundBuildingBankAccountInput
{
    /// <summary>The building whose bank account to fund.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>
    /// Amount to transfer from company cash into the building's bank account.
    /// Must be positive and not exceed the company's available cash.
    /// </summary>
    public decimal Amount { get; set; }
}

/// <summary>Input for assigning a different bank account to a building.</summary>
public sealed class AssignBuildingBankAccountInput
{
    /// <summary>The building to update.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>
    /// The bank account to assign to this building.
    /// Must be owned by the building's company and must have the same currency as the building's city.
    /// </summary>
    public Guid BankAccountId { get; set; }
}

/// <summary>Input for creating a new bank account for a company.</summary>
public sealed class CreateCompanyBankAccountInput
{
    /// <summary>The company that will own the new account.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>
    /// ISO 4217 currency code for the account (e.g. "EUR", "CZK").
    /// Must match a city currency available in this game server.
    /// </summary>
    [Required, MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;
}

/// <summary>Input for creating a new personal bank account in a supported currency.</summary>
public sealed class CreatePersonalBankAccountInput
{
    /// <summary>
    /// ISO 4217 currency code for the account (e.g. "USD", "EUR").
    /// Must match a city currency available in this game server.
    /// </summary>
    [Required, MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;
}

/// <summary>
/// Input for transferring funds between two of the authenticated player's bank accounts.
/// Both accounts must belong to the active account context (PERSON or the selected COMPANY)
/// and must use the same currency.
/// Cross-currency transfers must go through the Forex Exchange swap flow.
/// </summary>
public sealed class TransferFundsInput
{
    /// <summary>Source bank account ID. Must belong to the active account context.</summary>
    public Guid FromBankAccountId { get; set; }

    /// <summary>Destination bank account ID. Must belong to the active account context.</summary>
    public Guid ToBankAccountId { get; set; }

    /// <summary>
    /// Amount to transfer in the shared account currency. Must be positive and not exceed
    /// the source account balance.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>Optional human-readable description shown on both bank statement entries.</summary>
    [MaxLength(200)]
    public string? Description { get; set; }
}

public sealed class MarkPlayerNotificationsReadInput
{
    [Required]
    public List<Guid> NotificationIds { get; set; } = [];
}

public sealed class SetBankAccountAlertThresholdInput
{
    public Guid BankAccountId { get; set; }
    public decimal? MinBalanceThreshold { get; set; }
}

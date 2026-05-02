namespace Api.Types;

/// <summary>
/// Detailed view of the bank account assigned to a building.
/// Returned by the <c>buildingBankAccount</c> query.
/// </summary>
public sealed class BuildingBankAccountInfo
{
    /// <summary>Building ID.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>Human-readable building name.</summary>
    public string BuildingName { get; set; } = string.Empty;

    /// <summary>Name of the city where the building is located.</summary>
    public string CityName { get; set; } = string.Empty;

    /// <summary>ISO 4217 currency code for the city (and required account currency).</summary>
    public string CurrencyCode { get; set; } = "EUR";

    /// <summary>True when a bank account is assigned to this building.</summary>
    public bool HasBankAccount { get; set; }

    /// <summary>ID of the assigned bank account. Null when <see cref="HasBankAccount"/> is false.</summary>
    public Guid? BankAccountId { get; set; }

    /// <summary>16-digit account number. Null when <see cref="HasBankAccount"/> is false.</summary>
    public string? AccountNumber { get; set; }

    /// <summary>Current balance of the assigned account. Null when no account is assigned.</summary>
    public decimal? Balance { get; set; }

    /// <summary>Optional low-balance notification threshold in account currency.</summary>
    public decimal? AlertMinBalanceThreshold { get; set; }

    /// <summary>
    /// True when the building was suspended on the last tick due to insufficient bank account funds.
    /// Also true when the building has no bank account assigned (advisory state).
    /// </summary>
    public bool IsSuspendedForFunds { get; set; }

    /// <summary>
    /// Machine-readable suspension reason. Values:
    /// <list type="bullet">
    /// <item><c>null</c> – building is operating normally.</item>
    /// <item><c>MISSING_BANK_ACCOUNT</c> – no account assigned; legacy company-cash path used.</item>
    /// <item><c>INSUFFICIENT_FUNDS:&lt;amount&gt;</c> – account had less than &lt;amount&gt; needed for the tick.</item>
    /// </list>
    /// </summary>
    public string? SuspendedReason { get; set; }
}

/// <summary>Summary of a bank account owned by a company.</summary>
public sealed class CompanyBankAccountSummary
{
    /// <summary>Unique identifier of the bank account.</summary>
    public Guid Id { get; set; }

    /// <summary>16-digit account number (unique within this game server).</summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>ISO 4217 currency code.</summary>
    public string CurrencyCode { get; set; } = "EUR";

    /// <summary>Current balance of the account.</summary>
    public decimal Balance { get; set; }

    /// <summary>Optional low-balance notification threshold in account currency.</summary>
    public decimal? AlertMinBalanceThreshold { get; set; }
}

/// <summary>Result of the <c>fundBuildingBankAccount</c> mutation.</summary>
public sealed class FundBuildingBankAccountResult
{
    /// <summary>The updated bank account info after the transfer.</summary>
    public BuildingBankAccountInfo BankAccount { get; set; } = new();

    /// <summary>Remaining company cash balance after the transfer.</summary>
    public decimal RemainingCompanyCash { get; set; }
}

/// <summary>Result of the <c>assignBuildingBankAccount</c> mutation.</summary>
public sealed class AssignBuildingBankAccountResult
{
    /// <summary>The updated bank account info after the assignment.</summary>
    public BuildingBankAccountInfo BankAccount { get; set; } = new();
}

/// <summary>Result of the <c>createCompanyBankAccount</c> mutation.</summary>
public sealed class CreateCompanyBankAccountResult
{
    /// <summary>The newly created bank account.</summary>
    public CompanyBankAccountSummary Account { get; set; } = new();
}

/// <summary>Result of the <c>createPersonalBankAccount</c> mutation.</summary>
public sealed class CreatePersonalBankAccountResult
{
    /// <summary>The newly created personal bank account.</summary>
    public CompanyBankAccountSummary Account { get; set; } = new();
}

/// <summary>Result of the <c>closeCompanyBankAccount</c> mutation.</summary>
public sealed class CloseCompanyBankAccountResult
{
    /// <summary>Unique identifier of the closed bank account.</summary>
    public Guid Id { get; set; }

    /// <summary>16-digit account number of the closed account.</summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>ISO 4217 currency code of the closed account.</summary>
    public string CurrencyCode { get; set; } = "EUR";

    /// <summary>UTC timestamp when the account was closed.</summary>
    public DateTime ClosedAtUtc { get; set; }
}

/// <summary>Result of the <c>transferFunds</c> mutation.</summary>
public sealed class TransferFundsResult
{
    /// <summary>Source account after the transfer.</summary>
    public PlayerBankAccountSummary FromAccount { get; set; } = new();

    /// <summary>Destination account after the transfer.</summary>
    public PlayerBankAccountSummary ToAccount { get; set; } = new();

    /// <summary>Transferred amount in the shared account currency.</summary>
    public decimal Amount { get; set; }

    /// <summary>ISO 4217 currency code shared by both accounts.</summary>
    public string CurrencyCode { get; set; } = "EUR";
}

/// <summary>
/// A bank account owned by the authenticated player or one of their companies.
/// Returned by the <c>myBankAccounts</c> query and used to populate forex swap selectors.
/// </summary>
public sealed class PlayerBankAccountSummary
{
    /// <summary>Unique identifier of the bank account.</summary>
    public Guid Id { get; set; }

    /// <summary>16-digit account number (unique within this game server).</summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>ISO 4217 currency code for this account (e.g. "EUR", "CZK").</summary>
    public string CurrencyCode { get; set; } = "EUR";

    /// <summary>Display symbol for the currency (e.g. "€", "Kč").</summary>
    public string CurrencySymbol => Mutation.GetCurrencySymbol(CurrencyCode);

    /// <summary>Current balance of the account.</summary>
    public decimal Balance { get; set; }

    /// <summary>Optional low-balance notification threshold in account currency.</summary>
    public decimal? AlertMinBalanceThreshold { get; set; }

    /// <summary>ID of the company that owns this account. Null for personal accounts.</summary>
    public Guid? CompanyId { get; set; }

    /// <summary>Human-readable company name. Null for personal accounts.</summary>
    public string? CompanyName { get; set; }

    /// <summary>Account owner type: PERSON or COMPANY.</summary>
    public string OwnerType { get; set; } = "COMPANY";

    /// <summary>Human-readable owner display name (player display name or company name).</summary>
    public string OwnerDisplayName { get; set; } = string.Empty;

    /// <summary>ID of the bank building this account is registered at. Null for free-floating company operating accounts.</summary>
    public Guid? BankBuildingId { get; set; }

    /// <summary>ID of the city where the bank building is located. Resolved from bank building, primary city, or government bank.</summary>
    public Guid? CityId { get; set; }

    /// <summary>
    /// True when this account is a deposit account held at a specific bank building (BankBuildingId set on entity).
    /// False for regular company treasury or personal settlement accounts.
    /// Deposit accounts must be closed via the bank withdrawal flow; regular accounts via closeCompanyBankAccount.
    /// </summary>
    public bool IsDepositAccount { get; set; }
}

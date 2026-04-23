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

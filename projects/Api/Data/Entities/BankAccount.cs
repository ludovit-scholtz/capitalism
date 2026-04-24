using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Represents a bank account with a unique 16-digit account number, currency, and balance.
/// Accounts may be owned by a company, a player, or the government.
/// </summary>
public sealed class BankAccount
{
    /// <summary>Unique identifier for the bank account.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Unique 16-digit account number within this game server.
    /// Generated as 16 random decimal digits. Used as the human-readable identifier.
    /// </summary>
    [Required, MaxLength(16)]
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>ISO 4217 currency code for this account (e.g. "EUR", "CZK", "USD").</summary>
    [Required, MaxLength(3)]
    public string CurrencyCode { get; set; } = "EUR";

    /// <summary>
    /// Current balance of this account.
    /// Maintained by direct mutation when operating costs, purchases, and revenue are settled.
    /// </summary>
    public decimal Balance { get; set; }

    /// <summary>
    /// The company that owns this account.
    /// Null for player-owned or government-owned accounts.
    /// Deposit accounts held at a specific bank building also use this ownership field.
    /// </summary>
    public Guid? CompanyId { get; set; }

    /// <summary>Navigation property to the owning company.</summary>
    public Company? Company { get; set; }

    /// <summary>
    /// The bank building where this deposit account is held.
    /// Null for normal company treasury accounts, player accounts, and government accounts.
    /// </summary>
    public Guid? BankBuildingId { get; set; }

    /// <summary>Navigation property to the bank building holding this deposit account.</summary>
    public Building? BankBuilding { get; set; }

    /// <summary>
    /// The player that owns this account directly.
    /// Null for company-owned or government-owned accounts.
    /// </summary>
    public Guid? PlayerId { get; set; }

    /// <summary>Navigation property to the owning player.</summary>
    public Player? Player { get; set; }

    /// <summary>
    /// True when this is a government-controlled default account for a given currency.
    /// Government accounts are auto-created for each city currency at startup.
    /// New buildings without an assigned account are linked to the government account for their city.
    /// </summary>
    public bool IsGovernmentAccount { get; set; }

    /// <summary>
    /// Snapshotted annual deposit interest rate (%) for deposit accounts held at a bank building.
    /// Null for normal treasury, player, and government accounts.
    /// </summary>
    public decimal? DepositInterestRatePercent { get; set; }

    /// <summary>
    /// Whether this deposit account represents the bank's own base capital.
    /// </summary>
    public bool IsBaseCapitalDeposit { get; set; }

    /// <summary>
    /// Tick when this deposit account was opened.
    /// Null for non-deposit accounts.
    /// </summary>
    public long? DepositedAtTick { get; set; }

    /// <summary>
    /// Tick when this deposit account was closed.
    /// Null while the deposit is active or for non-deposit accounts.
    /// </summary>
    public long? ClosedAtTick { get; set; }

    /// <summary>
    /// UTC timestamp when this deposit account was closed.
    /// Null while the deposit is active or for non-deposit accounts.
    /// </summary>
    public DateTime? ClosedAtUtc { get; set; }

    /// <summary>
    /// Cumulative interest paid out over the life of this deposit account.
    /// Always zero for non-deposit accounts.
    /// </summary>
    public decimal TotalInterestPaid { get; set; }

    /// <summary>UTC timestamp when this account was created.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

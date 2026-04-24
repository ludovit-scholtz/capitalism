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
    /// </summary>
    public Guid? CompanyId { get; set; }

    /// <summary>Navigation property to the owning company.</summary>
    public Company? Company { get; set; }

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

    /// <summary>UTC timestamp when this account was created.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

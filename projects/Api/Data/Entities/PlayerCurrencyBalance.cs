using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Tracks a player's personal cash balance in a specific non-base currency.
/// The base currency (EUR) is tracked in the player's settlement bank account.
/// Non-EUR currencies earned through forex swaps are recorded here.
/// </summary>
public sealed class PlayerCurrencyBalance
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The player who owns this currency balance.</summary>
    public Guid PlayerId { get; set; }

    /// <summary>Navigation property to the owning player.</summary>
    public Player Player { get; set; } = null!;

    /// <summary>ISO 4217 currency code (e.g. "USD", "CZK", "GBP").</summary>
    [Required, MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>Current balance in this currency. Always >= 0.</summary>
    public decimal Balance { get; set; }

    /// <summary>When this balance record was first created.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>When this balance was last updated.</summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

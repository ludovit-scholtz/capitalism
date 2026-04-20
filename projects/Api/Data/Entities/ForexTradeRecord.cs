using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Records a completed forex currency swap executed by a player.
/// Used for the personal-account trade history and ledger visibility.
/// </summary>
public sealed class ForexTradeRecord
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The player who executed the swap.</summary>
    public Guid PlayerId { get; set; }

    /// <summary>Navigation property to the player.</summary>
    public Player Player { get; set; } = null!;

    /// <summary>Source currency code (e.g. "EUR").</summary>
    [Required, MaxLength(3)]
    public string FromCurrencyCode { get; set; } = string.Empty;

    /// <summary>Target currency code (e.g. "USD").</summary>
    [Required, MaxLength(3)]
    public string ToCurrencyCode { get; set; } = string.Empty;

    /// <summary>Amount in the source currency that was deducted from the player's balance.</summary>
    public decimal FromAmount { get; set; }

    /// <summary>Amount in the target currency that was added to the player's balance (after fee).</summary>
    public decimal ToAmount { get; set; }

    /// <summary>Fee charged for the swap, denominated in the source currency.</summary>
    public decimal FeeAmount { get; set; }

    /// <summary>
    /// Exchange rate used: how many units of ToCurrency equal 1 unit of FromCurrency.
    /// Derived from EUR-based FX rates.
    /// </summary>
    public decimal Rate { get; set; }

    /// <summary>Game tick when the trade was executed.</summary>
    public long ExecutedAtTick { get; set; }

    /// <summary>UTC timestamp when the trade was executed.</summary>
    public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;
}

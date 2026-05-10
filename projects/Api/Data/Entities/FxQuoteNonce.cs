using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Tracks a single-use quote nonce issued during a forex quote request.
/// Prevents replay attacks: each nonce can only be consumed once and expires after a TTL.
/// </summary>
public sealed class FxQuoteNonce
{
    /// <summary>Unique identifier of this nonce record.</summary>
    public Guid Id { get; set; }

    /// <summary>The player who requested the quote.</summary>
    public Guid PlayerId { get; set; }

    /// <summary>The UUID v4 nonce issued to the client. Single-use.</summary>
    public Guid Nonce { get; set; }

    /// <summary>Source currency code (e.g. "EUR").</summary>
    [Required, MaxLength(3)]
    public string FromCurrencyCode { get; set; } = string.Empty;

    /// <summary>Target currency code (e.g. "CZK").</summary>
    [Required, MaxLength(3)]
    public string ToCurrencyCode { get; set; } = string.Empty;

    /// <summary>Exchange rate at quote time (units of ToCurrency per 1 unit of FromCurrency).</summary>
    public decimal Rate { get; set; }

    /// <summary>UTC timestamp when the nonce was issued.</summary>
    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when the nonce was consumed (null = not yet used).</summary>
    public DateTime? ConsumedAtUtc { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// A snapshot of FX rates at a specific game tick, capturing the buy, mid, and sell prices
/// for a given currency pair. Used to render historical rate charts on the FX dashboard.
/// </summary>
public sealed class FxRateHistory
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>ISO 4217 base currency code (always EUR for NBS-sourced rates).</summary>
    [Required, MaxLength(3)]
    public string BaseCurrencyCode { get; set; } = "EUR";

    /// <summary>ISO 4217 quote currency code (the foreign currency).</summary>
    [Required, MaxLength(3)]
    public string QuoteCurrencyCode { get; set; } = string.Empty;

    /// <summary>
    /// Mid-market rate at time of snapshot.
    /// How many units of QuoteCurrency equal 1 unit of BaseCurrency.
    /// </summary>
    public decimal MidRate { get; set; }

    /// <summary>
    /// Buy rate (ask): rate at which the market sells the quote currency to a player,
    /// typically slightly worse than mid (e.g. mid * 1.005).
    /// </summary>
    public decimal BuyRate { get; set; }

    /// <summary>
    /// Sell rate (bid): rate at which the market buys the quote currency from a player,
    /// typically slightly better than mid (e.g. mid * 0.995).
    /// </summary>
    public decimal SellRate { get; set; }

    /// <summary>Game tick when this snapshot was captured.</summary>
    public long GameTick { get; set; }

    /// <summary>UTC timestamp when this snapshot was created.</summary>
    public DateTime CapturedAtUtc { get; set; }
}

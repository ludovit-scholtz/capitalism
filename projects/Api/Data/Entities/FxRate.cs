using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Persisted foreign exchange rate between two ISO 4217 currencies.
/// Rates are fetched from the NBS daily CSV feed and stored for reuse by the forex exchange.
/// </summary>
public sealed class FxRate
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>ISO 4217 base currency code (EUR for NBS-sourced rates).</summary>
    [Required, MaxLength(3)]
    public string BaseCurrencyCode { get; set; } = "EUR";

    /// <summary>ISO 4217 quote currency code (the foreign currency).</summary>
    [Required, MaxLength(3)]
    public string QuoteCurrencyCode { get; set; } = string.Empty;

    /// <summary>
    /// Rate: how many units of QuoteCurrency equal 1 unit of BaseCurrency.
    /// Example: if BaseCurrencyCode=EUR and QuoteCurrencyCode=CZK, Rate=25.42 means 1 EUR = 25.42 CZK.
    /// </summary>
    public decimal Rate { get; set; }

    /// <summary>Date for which this rate applies (from the source).</summary>
    public DateOnly RateDate { get; set; }

    /// <summary>UTC timestamp when this rate was fetched or seeded.</summary>
    public DateTime FetchedAtUtc { get; set; }

    /// <summary>
    /// Source of the rate: "NBS" for live data from the National Bank of Slovakia,
    /// "FALLBACK" for hardcoded approximate values used when the NBS feed is unavailable.
    /// </summary>
    [Required, MaxLength(20)]
    public string Source { get; set; } = "NBS";
}

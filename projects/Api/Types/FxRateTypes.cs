namespace Api.Types;

/// <summary>
/// Summary of a single foreign exchange rate for the fxRates GraphQL query.
/// Rates are EUR-based (1 EUR = Rate units of QuoteCurrency).
/// </summary>
public sealed class FxRateSummary
{
    /// <summary>ISO 4217 base currency code (always EUR for NBS-sourced rates).</summary>
    public string BaseCurrencyCode { get; set; } = "EUR";

    /// <summary>ISO 4217 quote currency code (the foreign currency).</summary>
    public string QuoteCurrencyCode { get; set; } = string.Empty;

    /// <summary>
    /// How many units of QuoteCurrency equal 1 unit of BaseCurrency.
    /// Example: Rate=25.20 when Base=EUR and Quote=CZK means 1 EUR = 25.20 CZK.
    /// </summary>
    public decimal Rate { get; set; }

    /// <summary>Date for which this rate applies.</summary>
    public DateOnly RateDate { get; set; }

    /// <summary>Source of the rate: "NBS" or "FALLBACK".</summary>
    public string Source { get; set; } = "NBS";

    /// <summary>Display symbol for the quote currency (e.g. "Kč" for CZK, "$" for USD).</summary>
    public string QuoteCurrencySymbol => Mutation.GetCurrencySymbol(QuoteCurrencyCode);
}

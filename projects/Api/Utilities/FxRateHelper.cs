using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Utilities;

/// <summary>
/// Shared helper for EUR-based foreign exchange rate lookups.
/// All stored FX rates use EUR as the base currency:
/// 1 EUR = Rate units of QuoteCurrency.
/// </summary>
public static class FxRateHelper
{
    /// <summary>
    /// Approximate EUR-based fallback rates used when no DB row is available.
    /// Key = ISO 4217 currency code, Value = units of that currency per 1 EUR.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, decimal> FallbackEurRates =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["EUR"] = 1m,
            ["CZK"] = 25.20m,
            ["USD"] = 1.08m,
            ["GBP"] = 0.86m,
            ["CNY"] = 7.84m,
            ["INR"] = 90.50m,
        };

    /// <summary>
    /// Returns the EUR→currencyCode rate for a given city currency code.
    /// Looks up in <paramref name="rates"/> first, then falls back to
    /// <see cref="FallbackEurRates"/>, defaulting to 1.0 if unknown.
    /// </summary>
    public static decimal GetEurRate(IReadOnlyDictionary<string, decimal> rates, string currencyCode)
    {
        if (string.IsNullOrEmpty(currencyCode) || currencyCode == "EUR") return 1m;
        if (rates.TryGetValue(currencyCode, out var rate) && rate > 0) return rate;
        if (FallbackEurRates.TryGetValue(currencyCode, out var fallback)) return fallback;
        return 1m;
    }

    /// <summary>
    /// Converts an amount in a given local currency to USD using EUR-based rates.
    /// Formula: amount → EUR via <paramref name="eurRates"/>, then EUR → USD via <paramref name="eurRates"/>.
    /// If <paramref name="fromCurrencyCode"/> is already "USD" the amount is returned unchanged.
    /// </summary>
    public static decimal ConvertToUsd(
        decimal amount,
        string fromCurrencyCode,
        IReadOnlyDictionary<string, decimal> eurRates)
    {
        if (amount == 0m) return 0m;
        if (string.Equals(fromCurrencyCode, "USD", StringComparison.OrdinalIgnoreCase)) return amount;

        // Convert from local currency to EUR: EUR = amount / (units of fromCurrencyCode per EUR)
        var localEurRate = GetEurRate(eurRates, fromCurrencyCode);
        var amountInEur = amount / localEurRate;

        // Convert EUR → USD using the EUR-per-USD rate stored in the same table.
        var usdEurRate = GetEurRate(eurRates, "USD");
        return amountInEur * usdEurRate;
    }

    /// <summary>
    /// Converts an amount in EUR to USD using the pre-loaded <paramref name="eurRates"/> table.
    /// </summary>
    public static decimal ConvertEurToUsd(decimal amountInEur, IReadOnlyDictionary<string, decimal> eurRates)
    {
        if (amountInEur == 0m) return 0m;
        var usdEurRate = GetEurRate(eurRates, "USD");
        return amountInEur * usdEurRate;
    }

    /// <summary>
    /// Builds a lookup dictionary of EUR-based FX rates for the given currency codes.
    /// Queries the database first; missing codes fall back to <see cref="FallbackEurRates"/>.
    /// EUR itself always maps to 1.0.
    /// </summary>
    public static async Task<Dictionary<string, decimal>> BuildEurRatesLookupAsync(
        AppDbContext db,
        IEnumerable<string> currencyCodes)
    {
        var codes = currencyCodes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(c => !string.Equals(c, "EUR", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var lookup = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["EUR"] = 1m
        };

        if (codes.Count == 0) return lookup;

        var dbRates = await db.FxRates
            .AsNoTracking()
            .Where(r => r.BaseCurrencyCode == "EUR" && codes.Contains(r.QuoteCurrencyCode))
            .GroupBy(r => r.QuoteCurrencyCode)
            .Select(g => new
            {
                CurrencyCode = g.Key,
                Rate = g.OrderByDescending(r => r.RateDate).Select(r => r.Rate).First()
            })
            .ToListAsync();

        foreach (var row in dbRates)
        {
            lookup[row.CurrencyCode] = row.Rate;
        }

        // Fill in fallbacks for any currency not in the database.
        foreach (var code in codes.Where(c => !lookup.ContainsKey(c)))
        {
            if (FallbackEurRates.TryGetValue(code, out var fallback))
                lookup[code] = fallback;
            else
                lookup[code] = 1m;
        }

        return lookup;
    }
}

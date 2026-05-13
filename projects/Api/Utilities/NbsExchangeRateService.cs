using Api.Data.Entities;

namespace Api.Utilities;

/// <summary>
/// Downloads exchange rates from the NBS (National Bank of Slovakia) daily CSV feed.
/// Feed URL: https://nbs.sk/export/en/exchange-rate/{date}/csv where {date} is yyyy-MM-dd.
/// Falls back to hardcoded approximate rates when the live feed is unavailable.
/// </summary>
public sealed class NbsExchangeRateService(IHttpClientFactory httpClientFactory, IWebHostEnvironment webHostEnvironment, ILogger<NbsExchangeRateService> logger)
{
    /// <summary>
    /// Fallback rates expressed as "units of quote currency per 1 EUR".
    /// Updated to approximate 2026 rates.
    /// </summary>
    private static readonly Dictionary<string, decimal> FallbackRates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CZK"] = 25.20m,
        ["PLN"] = 4.25m,
        ["USD"] = 1.08m,
        ["GBP"] = 0.86m,
        ["CNY"] = 7.84m,
        ["INR"] = 90.50m,
    };

    /// <summary>
    /// Fetches the latest exchange rates from NBS for today's date.
    /// On failure returns hardcoded fallback rates so the game can always start.
    /// </summary>
    public async Task<IReadOnlyList<FxRate>> FetchLatestRatesAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (webHostEnvironment.IsEnvironment("Testing"))
        {
            return BuildFallbackRates(today);
        }
        var dateString = today.ToString("yyyy-MM-dd");
        var url = $"https://nbs.sk/export/en/exchange-rate/{dateString}/csv";

        try
        {
            using var client = httpClientFactory.CreateClient("nbs-exchange-rate");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var csvContent = await client.GetStringAsync(url, cts.Token);
            var rates = ParseNbsCsv(csvContent, today);

            if (rates.Count > 0)
            {
                logger.LogInformation("Fetched {Count} FX rates from NBS for {Date}", rates.Count, dateString);
                return rates;
            }

            logger.LogWarning("NBS CSV at {Url} returned no parseable rates; using fallback", url);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch NBS exchange rates from {Url}; using fallback rates", url);
        }

        return BuildFallbackRates(today);
    }

    /// <summary>
    /// Parses NBS English-language CSV.
    /// Format: Date;Currency name;Amount;Currency code;Rate
    /// where Rate is EUR per [Amount] units of [Currency code].
    /// Example: 17.04.2026;Czech koruna;1;CZK;0.03970
    /// means 1 CZK = 0.03970 EUR → 1 EUR ≈ 25.19 CZK.
    /// </summary>
    private static List<FxRate> ParseNbsCsv(string csvContent, DateOnly rateDate)
    {
        var rates = new List<FxRate>();
        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            var parts = trimmed.Split(';');
            if (parts.Length < 5)
                continue;

            // Parse amount (column index 2)
            if (!decimal.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount == 0)
                continue;

            // Parse rate (column index 4) — EUR per amount units of foreign currency
            if (!decimal.TryParse(parts[4].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var eurPerAmount)
                || eurPerAmount <= 0)
                continue;

            var currencyCode = parts[3].Trim().ToUpperInvariant();
            if (currencyCode.Length != 3)
                continue;

            // Convert: rateEurPerAmount EUR = amount units of currency
            // → 1 EUR = amount/rateEurPerAmount units of currency
            var quotePerEur = Math.Round(amount / eurPerAmount, 6);

            rates.Add(new FxRate
            {
                Id = Guid.NewGuid(),
                BaseCurrencyCode = "EUR",
                QuoteCurrencyCode = currencyCode,
                Rate = quotePerEur,
                RateDate = rateDate,
                FetchedAtUtc = DateTime.UtcNow,
                Source = "NBS"
            });
        }

        return rates;
    }

    /// <summary>Returns hardcoded approximate rates for the game's currencies.</summary>
    private static List<FxRate> BuildFallbackRates(DateOnly rateDate)
    {
        return FallbackRates.Select(kv => new FxRate
        {
            Id = Guid.NewGuid(),
            BaseCurrencyCode = "EUR",
            QuoteCurrencyCode = kv.Key,
            Rate = kv.Value,
            RateDate = rateDate,
            FetchedAtUtc = DateTime.UtcNow,
            Source = "FALLBACK"
        }).ToList();
    }
}

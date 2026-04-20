using Api.Data;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    private const decimal ForexFeePercent = 1m;
    private const string EurCurrencyCode = "EUR";

    /// <summary>
    /// Returns a forex swap quote showing the exchange rate, fee, and expected output amount.
    /// Does not execute the trade. Requires authentication (personal account).
    /// </summary>
    [Authorize]
    public async Task<ForexQuoteResult> GetForexQuote(
        GetForexQuoteInput input,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] AppDbContext db)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        ValidateForexInput(input.FromCurrencyCode, input.ToCurrencyCode, input.Amount);

        var fromCode = input.FromCurrencyCode.ToUpperInvariant();
        var toCode = input.ToCurrencyCode.ToUpperInvariant();

        var rate = await ComputeForexRateAsync(db, fromCode, toCode);

        var feeAmount = Math.Round(input.Amount * (ForexFeePercent / 100m), 4);
        var netFromAmount = input.Amount - feeAmount;
        var toAmount = Math.Round(netFromAmount * rate, 4);

        var availableBalance = await GetPersonalBalanceAsync(db, playerId, fromCode);

        return new ForexQuoteResult
        {
            FromCurrencyCode = fromCode,
            ToCurrencyCode = toCode,
            FromAmount = input.Amount,
            ToAmount = toAmount,
            FeeAmount = feeAmount,
            FeePercent = ForexFeePercent,
            Rate = Math.Round(rate, 6),
            AvailableFromBalance = availableBalance
        };
    }

    /// <summary>
    /// Returns all non-zero (and known) currency balances for the authenticated player.
    /// EUR balance comes from Player.PersonalCash; other currencies from PlayerCurrencyBalance rows.
    /// </summary>
    [Authorize]
    public async Task<List<CurrencyBalanceResult>> GetPlayerCurrencyBalances(
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] AppDbContext db)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var player = await db.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == playerId)
            ?? throw new GraphQLException(new Error("Player not found.", "PLAYER_NOT_FOUND"));

        var nonEurBalances = await db.PlayerCurrencyBalances
            .AsNoTracking()
            .Where(b => b.PlayerId == playerId && b.Balance > 0)
            .OrderBy(b => b.CurrencyCode)
            .ToListAsync();

        var result = new List<CurrencyBalanceResult>
        {
            new() { CurrencyCode = EurCurrencyCode, Balance = player.PersonalCash }
        };

        result.AddRange(nonEurBalances.Select(b => new CurrencyBalanceResult
        {
            CurrencyCode = b.CurrencyCode,
            Balance = b.Balance
        }));

        return result;
    }

    /// <summary>
    /// Returns the last 50 forex trades executed by the authenticated player, newest first.
    /// </summary>
    [Authorize]
    public async Task<List<ForexTradeHistoryEntry>> GetForexTradeHistory(
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] AppDbContext db)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var trades = await db.ForexTradeRecords
            .AsNoTracking()
            .Where(t => t.PlayerId == playerId)
            .OrderByDescending(t => t.ExecutedAtTick)
            .Take(50)
            .ToListAsync();

        return trades.Select(t => new ForexTradeHistoryEntry
        {
            Id = t.Id,
            FromCurrencyCode = t.FromCurrencyCode,
            ToCurrencyCode = t.ToCurrencyCode,
            FromAmount = t.FromAmount,
            ToAmount = t.ToAmount,
            FeeAmount = t.FeeAmount,
            Rate = t.Rate,
            ExecutedAtTick = t.ExecutedAtTick,
            ExecutedAtUtc = t.ExecutedAtUtc
        }).ToList();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    internal static async Task<decimal> ComputeForexRateAsync(AppDbContext db, string fromCode, string toCode)
    {
        if (fromCode == toCode)
        {
            return 1m;
        }

        // All stored rates are EUR-based: 1 EUR = Rate units of QuoteCurrency.
        // Cross rate: FROM → TO = eurTo / eurFrom
        // Special case: if FROM is EUR, rate = eurTo
        // Special case: if TO is EUR, rate = 1 / eurFrom

        if (fromCode == EurCurrencyCode)
        {
            var eurToRate = await GetEurRateAsync(db, toCode);
            return eurToRate;
        }

        if (toCode == EurCurrencyCode)
        {
            var eurFromRate = await GetEurRateAsync(db, fromCode);
            return Math.Round(1m / eurFromRate, 6);
        }

        // Cross rate via EUR
        var rateFrom = await GetEurRateAsync(db, fromCode);
        var rateTo = await GetEurRateAsync(db, toCode);

        return Math.Round(rateTo / rateFrom, 6);
    }

    private static async Task<decimal> GetEurRateAsync(AppDbContext db, string quoteCurrencyCode)
    {
        var rate = await db.FxRates
            .AsNoTracking()
            .Where(r => r.BaseCurrencyCode == EurCurrencyCode && r.QuoteCurrencyCode == quoteCurrencyCode)
            .OrderByDescending(r => r.RateDate)
            .Select(r => r.Rate)
            .FirstOrDefaultAsync();

        if (rate == 0)
        {
            throw new GraphQLException(new Error($"No exchange rate found for currency '{quoteCurrencyCode}'.", "RATE_NOT_FOUND"));
        }

        return rate;
    }

    internal static async Task<decimal> GetPersonalBalanceAsync(AppDbContext db, Guid playerId, string currencyCode)
    {
        if (currencyCode.ToUpperInvariant() == EurCurrencyCode)
        {
            return await db.Players
                .AsNoTracking()
                .Where(p => p.Id == playerId)
                .Select(p => p.PersonalCash)
                .FirstOrDefaultAsync();
        }

        return await db.PlayerCurrencyBalances
            .AsNoTracking()
            .Where(b => b.PlayerId == playerId && b.CurrencyCode == currencyCode.ToUpperInvariant())
            .Select(b => b.Balance)
            .FirstOrDefaultAsync();
    }

    internal static void ValidateForexInput(string fromCode, string toCode, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(fromCode) || fromCode.Length != 3)
            throw new GraphQLException(new Error("Source currency code must be a 3-letter ISO code.", "INVALID_CURRENCY_CODE"));

        if (string.IsNullOrWhiteSpace(toCode) || toCode.Length != 3)
            throw new GraphQLException(new Error("Target currency code must be a 3-letter ISO code.", "INVALID_CURRENCY_CODE"));

        if (fromCode.Equals(toCode, StringComparison.OrdinalIgnoreCase))
            throw new GraphQLException(new Error("Source and target currencies must be different.", "SAME_CURRENCY"));

        if (amount <= 0)
            throw new GraphQLException(new Error("Amount must be greater than zero.", "INVALID_AMOUNT"));
    }
}

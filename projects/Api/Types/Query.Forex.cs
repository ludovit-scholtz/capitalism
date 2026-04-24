using Api.Data;
using Api.Security;
using Api.Utilities;
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

        // Determine available balance: prefer bank account when an ID is provided.
        decimal availableBalance;
        if (input.FromBankAccountId.HasValue)
        {
            var account = await GetOwnedBankAccountAsync(db, input.FromBankAccountId.Value, playerId);
            if (account is null)
                throw new GraphQLException(new Error("Source bank account not found or you do not own it.", "ACCOUNT_NOT_FOUND"));
            if (!string.Equals(account.CurrencyCode, fromCode, StringComparison.OrdinalIgnoreCase))
                throw new GraphQLException(new Error(
                    $"Source bank account currency ({account.CurrencyCode}) does not match the requested from-currency ({fromCode}).",
                    "CURRENCY_MISMATCH"));
            availableBalance = account.Balance;
        }
        else
        {
            availableBalance = await GetPersonalBalanceAsync(db, playerId, fromCode);
        }

        // Validate destination account currency when provided.
        if (input.ToBankAccountId.HasValue)
        {
            var toAccount = await GetOwnedBankAccountAsync(db, input.ToBankAccountId.Value, playerId);
            if (toAccount is null)
                throw new GraphQLException(new Error("Destination bank account not found or you do not own it.", "ACCOUNT_NOT_FOUND"));
            if (!string.Equals(toAccount.CurrencyCode, toCode, StringComparison.OrdinalIgnoreCase))
                throw new GraphQLException(new Error(
                    $"Destination bank account currency ({toAccount.CurrencyCode}) does not match the requested to-currency ({toCode}).",
                    "CURRENCY_MISMATCH"));
        }

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
    /// Returns all non-zero currency balances for the authenticated player.
    /// Player-owned bank accounts are now the canonical source of truth for every currency.
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

        var nonEurBalances = await db.BankAccounts
            .AsNoTracking()
            .Where(account => account.PlayerId == playerId
                && account.CurrencyCode != EurCurrencyCode
                && account.Balance > 0)
            .OrderBy(account => account.CurrencyCode)
            .ToListAsync();

        var eurBalance = await PersonalBankAccountService.GetGrossCashAsync(db, player.Id);

        var result = new List<CurrencyBalanceResult>
        {
            new() { CurrencyCode = EurCurrencyCode, Balance = eurBalance }
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

    /// <summary>
    /// Returns all current EUR-based FX rates (i.e. "units of quote currency per 1 EUR").
    /// Used by the frontend to scale monetary values into the city's local currency.
    /// Public query — no authentication required.
    /// </summary>
    public async Task<List<EurFxRate>> GetEurFxRates([Service] AppDbContext db)
    {
        var dbRates = await db.FxRates
            .AsNoTracking()
            .Where(r => r.BaseCurrencyCode == EurCurrencyCode)
            .GroupBy(r => r.QuoteCurrencyCode)
            .Select(g => new
            {
                QuoteCurrencyCode = g.Key,
                Rate = g.OrderByDescending(r => r.RateDate).Select(r => r.Rate).First()
            })
            .ToListAsync();

        var result = dbRates
            .Select(r => new EurFxRate { CurrencyCode = r.QuoteCurrencyCode, Rate = r.Rate })
            .ToList();

        // Always include EUR→EUR = 1 so the frontend can treat all currencies uniformly
        if (!result.Any(r => string.Equals(r.CurrencyCode, EurCurrencyCode, StringComparison.OrdinalIgnoreCase)))
        {
            result.Insert(0, new EurFxRate { CurrencyCode = EurCurrencyCode, Rate = 1m });
        }

        return result;
    }

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
        => await PersonalBankAccountService.GetTrackedBalanceAsync(db, playerId, currencyCode);

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

    /// <summary>
    /// Returns a bank account owned by one of the player's companies, or null if not found / not owned.
    /// </summary>
    internal static async Task<Api.Data.Entities.BankAccount?> GetOwnedBankAccountAsync(
        AppDbContext db,
        Guid bankAccountId,
        Guid playerId)
    {
        return await db.BankAccounts
            .Include(a => a.Company)
            .FirstOrDefaultAsync(a => a.Id == bankAccountId && a.Company != null && a.Company.PlayerId == playerId);
    }
}

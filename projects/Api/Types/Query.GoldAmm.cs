using Api.Data;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    private const string GoldTokenCode = "XAU";
    private const decimal GoldAmmFeePercent = 1m;

    /// <summary>
    /// Returns all active gold AMM pools. Public query.
    /// If authenticated, includes the caller's position in each pool.
    /// </summary>
    public async Task<List<GoldAmmPoolInfo>> GoldAmmPools(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        Guid? playerId = null;
        try { playerId = httpContextAccessor.HttpContext?.User.GetRequiredUserId(); }
        catch { }

        var pools = await db.GoldAmmPools
            .AsNoTracking()
            .Include(p => p.Positions)
            .OrderBy(p => p.CurrencyCode)
            .ToListAsync();

        return pools.Select(p =>
        {
            var myPos = playerId.HasValue
                ? p.Positions.FirstOrDefault(pos => pos.PlayerId == playerId.Value)
                : null;

            GoldAmmPositionInfo? posInfo = null;
            if (myPos != null)
            {
                var (claimFiat, claimGold, pct) = ComputePositionClaims(myPos, p);
                posInfo = new GoldAmmPositionInfo
                {
                    Id = myPos.Id,
                    PoolId = p.Id,
                    CurrencyCode = p.CurrencyCode,
                    LiquidityShares = myPos.LiquidityShares,
                    SharePercent = pct,
                    ClaimableFiat = claimFiat,
                    ClaimableGold = claimGold,
                    FiatProvided = myPos.FiatProvided,
                    GoldProvided = myPos.GoldProvided
                };
            }

            return new GoldAmmPoolInfo
            {
                Id = p.Id,
                CurrencyCode = p.CurrencyCode,
                FiatReserve = p.FiatReserve,
                GoldReserve = p.GoldReserve,
                TotalLiquidityShares = p.TotalLiquidityShares,
                MyPosition = posInfo
            };
        }).ToList();
    }

    /// <summary>
    /// Returns a quote for a gold AMM swap. Requires authentication.
    /// </summary>
    [Authorize]
    public async Task<GoldAmmSwapQuote> GoldAmmSwapQuote(
        GetGoldAmmSwapQuoteInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var direction = input.Direction?.ToUpperInvariant() ?? string.Empty;
        var currencyCode = input.CurrencyCode?.ToUpperInvariant() ?? string.Empty;
        ValidateGoldAmmInput(direction, currencyCode, input.Amount);

        var pool = await db.GoldAmmPools.AsNoTracking()
            .FirstOrDefaultAsync(p => p.CurrencyCode == currencyCode)
            ?? throw new GraphQLException(new Error($"No liquidity pool found for {currencyCode}/XAU.", "POOL_NOT_FOUND"));

        if (pool.FiatReserve <= 0 || pool.GoldReserve <= 0)
            throw new GraphQLException(new Error("Pool has no liquidity.", "INSUFFICIENT_LIQUIDITY"));

        var (outputAmount, feeAmount) = ComputeAmmOutput(direction, input.Amount, pool.FiatReserve, pool.GoldReserve);

        // Slippage: compare implied price after swap vs current mid price
        var midPrice = pool.FiatReserve / pool.GoldReserve; // fiat per gold
        decimal impliedPrice;
        if (direction == "FIAT_TO_GOLD")
            impliedPrice = outputAmount > 0 ? Math.Round(input.Amount / outputAmount, 4) : 0m;
        else
            impliedPrice = outputAmount > 0 ? Math.Round(outputAmount / input.Amount, 4) : 0m;

        var slippage = midPrice > 0
            ? Math.Abs(Math.Round((impliedPrice - midPrice) / midPrice * 100m, 2))
            : 0m;

        var availableBalance = direction == "FIAT_TO_GOLD"
            ? await GetAvailableFiatAsync(db, playerId, currencyCode)
            : await GetAvailableGoldAsync(db, playerId);

        return new GoldAmmSwapQuote
        {
            Direction = direction,
            CurrencyCode = currencyCode,
            InputAmount = input.Amount,
            OutputAmount = outputAmount,
            FeeAmount = feeAmount,
            FeePercent = GoldAmmFeePercent,
            ImpliedPrice = impliedPrice,
            SlippagePercent = slippage,
            PoolFiatReserve = pool.FiatReserve,
            PoolGoldReserve = pool.GoldReserve,
            AvailableInputBalance = availableBalance
        };
    }

    /// <summary>
    /// Returns all of the authenticated player's gold AMM liquidity positions.
    /// </summary>
    [Authorize]
    public async Task<List<GoldAmmPositionInfo>> MyGoldAmmPositions(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var positions = await db.GoldAmmPositions
            .AsNoTracking()
            .Include(pos => pos.Pool)
            .Where(pos => pos.PlayerId == playerId)
            .OrderBy(pos => pos.Pool.CurrencyCode)
            .ToListAsync();

        return positions.Select(pos =>
        {
            var (claimFiat, claimGold, pct) = ComputePositionClaims(pos, pos.Pool);
            return new GoldAmmPositionInfo
            {
                Id = pos.Id,
                PoolId = pos.PoolId,
                CurrencyCode = pos.Pool.CurrencyCode,
                LiquidityShares = pos.LiquidityShares,
                SharePercent = pct,
                ClaimableFiat = claimFiat,
                ClaimableGold = claimGold,
                FiatProvided = pos.FiatProvided,
                GoldProvided = pos.GoldProvided
            };
        }).ToList();
    }

    /// <summary>
    /// Returns the authenticated player's gold balance (total, blocked in pools, available).
    /// </summary>
    [Authorize]
    public async Task<GoldBalanceInfo> MyGoldBalance(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var balance = await GetTotalGoldAsync(db, playerId);
        var blocked = await GetBlockedGoldAsync(db, playerId);
        return new GoldBalanceInfo { Balance = balance, BlockedInPools = blocked };
    }

    // ── AMM helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Computes constant-product AMM output for a given input and pool reserves.
    /// Fee (1%) is charged on the input; the net input is used in the AMM formula.
    /// Returns (outputAmount, feeAmount).
    /// </summary>
    public static (decimal outputAmount, decimal feeAmount) ComputeAmmOutput(
        string direction, decimal inputAmount, decimal fiatReserve, decimal goldReserve)
    {
        var feeAmount = Math.Round(inputAmount * (GoldAmmFeePercent / 100m), 8);
        var netInput = inputAmount - feeAmount;

        decimal outputAmount;
        if (direction == "FIAT_TO_GOLD")
        {
            // AMM: (fiat + netInput) * (gold - out) = fiat * gold
            var newFiatReserve = fiatReserve + netInput;
            outputAmount = goldReserve - (fiatReserve * goldReserve / newFiatReserve);
        }
        else // GOLD_TO_FIAT
        {
            var newGoldReserve = goldReserve + netInput;
            outputAmount = fiatReserve - (fiatReserve * goldReserve / newGoldReserve);
        }

        return (Math.Round(outputAmount, 8), feeAmount);
    }

    /// <summary>Validates gold AMM input parameters.</summary>
    public static void ValidateGoldAmmInput(string direction, string currencyCode, decimal amount)
    {
        if (direction != "FIAT_TO_GOLD" && direction != "GOLD_TO_FIAT")
            throw new GraphQLException(new Error("Direction must be 'FIAT_TO_GOLD' or 'GOLD_TO_FIAT'.", "INVALID_DIRECTION"));

        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3 ||
            currencyCode.Equals(GoldTokenCode, StringComparison.OrdinalIgnoreCase))
            throw new GraphQLException(new Error("CurrencyCode must be a 3-letter fiat ISO code (not XAU).", "INVALID_CURRENCY_CODE"));

        if (amount <= 0)
            throw new GraphQLException(new Error("Amount must be greater than zero.", "INVALID_AMOUNT"));
    }

    // ── Balance helpers ──────────────────────────────────────────────────────────

    internal static async Task<decimal> GetTotalGoldAsync(AppDbContext db, Guid playerId)
        => await db.PlayerGoldBalances
            .AsNoTracking()
            .Where(g => g.PlayerId == playerId)
            .Select(g => g.Balance)
            .FirstOrDefaultAsync();

    internal static async Task<decimal> GetBlockedGoldAsync(AppDbContext db, Guid playerId)
        => await db.GoldAmmPositions
            .AsNoTracking()
            .Where(p => p.PlayerId == playerId)
            .SumAsync(p => p.GoldProvided);

    /// <summary>
    /// Returns the player's available gold for spending/swapping.
    /// Pool-committed gold is already deducted from PlayerGoldBalance.Balance at deposit time,
    /// so the wallet balance IS the available amount — no additional "blocked" subtraction needed.
    /// </summary>
    internal static Task<decimal> GetAvailableGoldAsync(AppDbContext db, Guid playerId)
        => GetTotalGoldAsync(db, playerId);

    /// <summary>
    /// Returns the player's available fiat for spending/swapping.
    /// Pool-committed fiat is already deducted from PersonalCash/PlayerCurrencyBalance at deposit time,
    /// so the wallet balance IS the available amount — no additional "blocked" subtraction needed.
    /// </summary>
    internal static Task<decimal> GetAvailableFiatAsync(AppDbContext db, Guid playerId, string currencyCode)
        => GetPersonalBalanceAsync(db, playerId, currencyCode);

    // ── Position claims helper ──────────────────────────────────────────────────

    private static (decimal claimFiat, decimal claimGold, decimal sharePercent) ComputePositionClaims(
        Data.Entities.GoldAmmPosition pos, Data.Entities.GoldAmmPool pool)
    {
        if (pool.TotalLiquidityShares <= 0)
            return (0m, 0m, 0m);

        var sharePercent = Math.Round(pos.LiquidityShares / pool.TotalLiquidityShares * 100m, 4);
        var claimFiat = Math.Round(pos.LiquidityShares / pool.TotalLiquidityShares * pool.FiatReserve, 4);
        var claimGold = Math.Round(pos.LiquidityShares / pool.TotalLiquidityShares * pool.GoldReserve, 8);
        return (claimFiat, claimGold, sharePercent);
    }
}

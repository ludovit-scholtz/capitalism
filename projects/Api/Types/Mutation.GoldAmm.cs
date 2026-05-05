using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    private const decimal GoldAmmFee = 1m;

    /// <summary>
    /// Creates a new gold AMM liquidity pool for the given fiat/XAU pair and seeds it.
    /// Requires sufficient available (non-blocked) fiat and gold.
    /// </summary>
    [Authorize]
    public async Task<GoldAmmLiquidityResult> CreateGoldAmmPool(
        CreateGoldAmmPoolInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var currencyCode = input.CurrencyCode?.ToUpperInvariant() ?? string.Empty;
        ValidateCreatePoolInput(currencyCode, input.FiatAmount, input.GoldAmount);

        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync()
            : null;

        var existing = await db.GoldAmmPools.FirstOrDefaultAsync(p => p.CurrencyCode == currencyCode);
        if (existing != null)
            throw new GraphQLException(new Error(
                $"A pool for {currencyCode}/XAU already exists. Use addGoldAmmLiquidity to contribute.",
                "POOL_ALREADY_EXISTS"));

        await AssertSufficientFiat(db, playerId, currencyCode, input.FiatAmount);
        await AssertSufficientGold(db, playerId, input.GoldAmount);

        // Initial LP shares = sqrt(fiat * gold) — Uniswap v2 formula
        var initialShares = (decimal)Math.Sqrt((double)(input.FiatAmount * input.GoldAmount));

        var pool = new GoldAmmPool
        {
            Id = Guid.NewGuid(),
            CurrencyCode = currencyCode,
            FiatReserve = input.FiatAmount,
            GoldReserve = input.GoldAmount,
            TotalLiquidityShares = initialShares,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.GoldAmmPools.Add(pool);

        var position = new GoldAmmPosition
        {
            Id = Guid.NewGuid(),
            PoolId = pool.Id,
            PlayerId = playerId,
            LiquidityShares = initialShares,
            FiatProvided = input.FiatAmount,
            GoldProvided = input.GoldAmount,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.GoldAmmPositions.Add(position);

        // Deduct fiat
        await DeductFiat(db, playerId, currencyCode, input.FiatAmount);
        // Deduct gold
        await DeductGold(db, playerId, input.GoldAmount);

        await db.SaveChangesAsync();
        if (tx is not null)
        {
            await tx.CommitAsync();
        }

        var newFiatBalance = await Query.GetPersonalBalanceAsync(db, playerId, currencyCode);
        var newGoldBalance = await Query.GetTotalGoldAsync(db, playerId);

        return new GoldAmmLiquidityResult
        {
            PoolId = pool.Id,
            PositionId = position.Id,
            CurrencyCode = currencyCode,
            LiquidityShares = initialShares,
            FiatProvided = input.FiatAmount,
            GoldProvided = input.GoldAmount,
            PoolFiatReserve = pool.FiatReserve,
            PoolGoldReserve = pool.GoldReserve,
            NewFiatBalance = newFiatBalance,
            NewGoldBalance = newGoldBalance
        };
    }

    /// <summary>
    /// Adds liquidity to an existing gold AMM pool.
    /// Fiat amount is specified; gold is determined proportionally by the current pool ratio.
    /// </summary>
    [Authorize]
    public async Task<GoldAmmLiquidityResult> AddGoldAmmLiquidity(
        AddGoldAmmLiquidityInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        if (input.FiatAmount <= 0)
            throw new GraphQLException(new Error("FiatAmount must be > 0.", "INVALID_AMOUNT"));

        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync()
            : null;

        var pool = await db.GoldAmmPools
            .Include(p => p.Positions)
            .FirstOrDefaultAsync(p => p.Id == input.PoolId)
            ?? throw new GraphQLException(new Error("Pool not found.", "POOL_NOT_FOUND"));

        if (pool.FiatReserve <= 0 || pool.GoldReserve <= 0)
            throw new GraphQLException(new Error("Pool has no liquidity.", "INSUFFICIENT_LIQUIDITY"));

        // Gold required proportional to fiat ratio
        var goldRequired = Math.Round(input.FiatAmount / pool.FiatReserve * pool.GoldReserve, 8);

        if (input.MaxGoldAmount > 0 && goldRequired > input.MaxGoldAmount)
            throw new GraphQLException(new Error(
                $"Required gold ({goldRequired:F8} XAU) exceeds MaxGoldAmount ({input.MaxGoldAmount:F8} XAU).",
                "SLIPPAGE_EXCEEDED"));

        await AssertSufficientFiat(db, playerId, pool.CurrencyCode, input.FiatAmount);
        await AssertSufficientGold(db, playerId, goldRequired);

        // LP shares = (fiatAmount / poolFiatReserve) * totalShares
        var newShares = Math.Round(input.FiatAmount / pool.FiatReserve * pool.TotalLiquidityShares, 8);

        // Update pool
        pool.FiatReserve += input.FiatAmount;
        pool.GoldReserve += goldRequired;
        pool.TotalLiquidityShares += newShares;
        pool.UpdatedAtUtc = DateTime.UtcNow;

        // Update or create position
        var position = pool.Positions.FirstOrDefault(p => p.PlayerId == playerId);
        if (position == null)
        {
            position = new GoldAmmPosition
            {
                Id = Guid.NewGuid(),
                PoolId = pool.Id,
                PlayerId = playerId,
                LiquidityShares = newShares,
                FiatProvided = input.FiatAmount,
                GoldProvided = goldRequired,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.GoldAmmPositions.Add(position);
        }
        else
        {
            position.LiquidityShares += newShares;
            position.FiatProvided += input.FiatAmount;
            position.GoldProvided += goldRequired;
            position.UpdatedAtUtc = DateTime.UtcNow;
        }

        await DeductFiat(db, playerId, pool.CurrencyCode, input.FiatAmount);
        await DeductGold(db, playerId, goldRequired);

        await db.SaveChangesAsync();
        if (tx is not null)
        {
            await tx.CommitAsync();
        }

        var newFiatBalance = await Query.GetPersonalBalanceAsync(db, playerId, pool.CurrencyCode);
        var newGoldBalance = await Query.GetTotalGoldAsync(db, playerId);

        return new GoldAmmLiquidityResult
        {
            PoolId = pool.Id,
            PositionId = position.Id,
            CurrencyCode = pool.CurrencyCode,
            LiquidityShares = newShares,
            FiatProvided = input.FiatAmount,
            GoldProvided = goldRequired,
            PoolFiatReserve = pool.FiatReserve,
            PoolGoldReserve = pool.GoldReserve,
            NewFiatBalance = newFiatBalance,
            NewGoldBalance = newGoldBalance
        };
    }

    /// <summary>
    /// Removes a fraction of the player's liquidity position from a gold AMM pool.
    /// Returns the proportional share of fiat and gold reserves.
    /// </summary>
    [Authorize]
    public async Task<GoldAmmRemoveLiquidityResult> RemoveGoldAmmLiquidity(
        RemoveGoldAmmLiquidityInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        if (input.ShareFraction <= 0 || input.ShareFraction > 1)
            throw new GraphQLException(new Error("ShareFraction must be between 0 (exclusive) and 1 (inclusive).", "INVALID_FRACTION"));

        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync()
            : null;

        var position = await db.GoldAmmPositions
            .Include(p => p.Pool)
            .FirstOrDefaultAsync(p => p.Id == input.PositionId)
            ?? throw new GraphQLException(new Error("Position not found.", "POSITION_NOT_FOUND"));

        if (position.PlayerId != playerId)
            throw new GraphQLException(new Error("You do not own this position.", "UNAUTHORIZED"));

        var pool = position.Pool;
        var sharesToRemove = Math.Round(position.LiquidityShares * input.ShareFraction, 8);

        var fiatReturn = pool.TotalLiquidityShares > 0
            ? Math.Round(sharesToRemove / pool.TotalLiquidityShares * pool.FiatReserve, 4)
            : 0m;
        var goldReturn = pool.TotalLiquidityShares > 0
            ? Math.Round(sharesToRemove / pool.TotalLiquidityShares * pool.GoldReserve, 8)
            : 0m;

        // Update pool reserves
        pool.FiatReserve -= fiatReturn;
        pool.GoldReserve -= goldReturn;
        pool.TotalLiquidityShares -= sharesToRemove;
        pool.UpdatedAtUtc = DateTime.UtcNow;

        // Reduce blocked amounts proportionally
        var fiatProvideReduction = Math.Min(fiatReturn, position.FiatProvided);
        var goldProvideReduction = Math.Min(goldReturn, position.GoldProvided);
        position.FiatProvided -= fiatProvideReduction;
        position.GoldProvided -= goldProvideReduction;
        position.LiquidityShares -= sharesToRemove;
        position.UpdatedAtUtc = DateTime.UtcNow;

        // If position is effectively empty, remove it
        if (position.LiquidityShares <= 0.000000001m)
        {
            position.LiquidityShares = 0;
            position.FiatProvided = 0;
            position.GoldProvided = 0;
        }

        // Credit fiat back to player
        await CreditFiat(db, playerId, pool.CurrencyCode, fiatReturn);
        // Credit gold back
        await CreditGold(db, playerId, goldReturn);

        await db.SaveChangesAsync();
        if (tx is not null)
        {
            await tx.CommitAsync();
        }

        var newFiatBalance = await Query.GetPersonalBalanceAsync(db, playerId, pool.CurrencyCode);
        var newGoldBalance = await Query.GetTotalGoldAsync(db, playerId);

        return new GoldAmmRemoveLiquidityResult
        {
            PositionId = position.Id,
            CurrencyCode = pool.CurrencyCode,
            FiatReturned = fiatReturn,
            GoldReturned = goldReturn,
            RemainingShares = position.LiquidityShares,
            NewFiatBalance = newFiatBalance,
            NewGoldBalance = newGoldBalance
        };
    }

}

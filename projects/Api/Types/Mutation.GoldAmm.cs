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

        await using var tx = await db.Database.BeginTransactionAsync();

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
        await tx.CommitAsync();

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

        await using var tx = await db.Database.BeginTransactionAsync();

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
        await tx.CommitAsync();

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

        await using var tx = await db.Database.BeginTransactionAsync();

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
        await tx.CommitAsync();

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

    /// <summary>
    /// Executes a gold AMM swap (fiat→gold or gold→fiat).
    /// The 1% fee stays in the pool, accruing to liquidity providers.
    /// </summary>
    [Authorize]
    public async Task<GoldAmmSwapResult> ExecuteGoldAmmSwap(
        ExecuteGoldAmmSwapInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var direction = input.Direction?.ToUpperInvariant() ?? string.Empty;
        var currencyCode = input.CurrencyCode?.ToUpperInvariant() ?? string.Empty;
        Query.ValidateGoldAmmInput(direction, currencyCode, input.Amount);

        await using var tx = await db.Database.BeginTransactionAsync();

        var pool = await db.GoldAmmPools.FirstOrDefaultAsync(p => p.CurrencyCode == currencyCode)
            ?? throw new GraphQLException(new Error($"No pool for {currencyCode}/XAU.", "POOL_NOT_FOUND"));

        if (pool.FiatReserve <= 0 || pool.GoldReserve <= 0)
            throw new GraphQLException(new Error("Pool has no liquidity.", "INSUFFICIENT_LIQUIDITY"));

        var (outputAmount, feeAmount) = Query.ComputeAmmOutput(direction, input.Amount, pool.FiatReserve, pool.GoldReserve);

        if (input.MinOutputAmount > 0 && outputAmount < input.MinOutputAmount)
            throw new GraphQLException(new Error(
                $"Output {outputAmount:F8} is below your minimum {input.MinOutputAmount:F8}.",
                "SLIPPAGE_EXCEEDED"));

        if (direction == "FIAT_TO_GOLD")
        {
            await AssertSufficientFiat(db, playerId, currencyCode, input.Amount);
            // Pool: fiat increases by full input amount (fee stays in pool), gold decreases
            await DeductFiat(db, playerId, currencyCode, input.Amount);
            await CreditGold(db, playerId, outputAmount);
            pool.FiatReserve += input.Amount;   // full input (fee stays in pool reserves)
            pool.GoldReserve -= outputAmount;
        }
        else // GOLD_TO_FIAT
        {
            await AssertSufficientGold(db, playerId, input.Amount);
            await DeductGold(db, playerId, input.Amount);
            await CreditFiat(db, playerId, currencyCode, outputAmount);
            pool.GoldReserve += input.Amount;
            pool.FiatReserve -= outputAmount;
        }

        pool.UpdatedAtUtc = DateTime.UtcNow;

        // Compute implied price
        var impliedPrice = direction == "FIAT_TO_GOLD" && outputAmount > 0
            ? Math.Round(input.Amount / outputAmount, 4)
            : outputAmount > 0
                ? Math.Round(outputAmount / input.Amount, 4)
                : 0m;

        var currentTick = await db.GameStates.AsNoTracking().Select(gs => gs.CurrentTick).FirstOrDefaultAsync();
        var tradeRecord = new GoldAmmTradeRecord
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            PoolId = pool.Id,
            Direction = direction,
            CurrencyCode = currencyCode,
            InputAmount = input.Amount,
            OutputAmount = outputAmount,
            FeeAmount = feeAmount,
            ImpliedPrice = impliedPrice,
            ExecutedAtTick = currentTick,
            ExecutedAtUtc = DateTime.UtcNow
        };
        db.GoldAmmTradeRecords.Add(tradeRecord);

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        var newFiatBalance = await Query.GetPersonalBalanceAsync(db, playerId, currencyCode);
        var newGoldBalance = await Query.GetTotalGoldAsync(db, playerId);

        return new GoldAmmSwapResult
        {
            TradeId = tradeRecord.Id,
            Direction = direction,
            CurrencyCode = currencyCode,
            InputAmount = input.Amount,
            OutputAmount = outputAmount,
            FeeAmount = feeAmount,
            ImpliedPrice = impliedPrice,
            NewFiatBalance = newFiatBalance,
            NewGoldBalance = newGoldBalance
        };
    }

    /// <summary>
    /// Admin-only: sets a player's gold balance. Creates or updates the PlayerGoldBalance row.
    /// </summary>
    [Authorize]
    public async Task<GoldBalanceInfo> AdminSetPlayerGoldBalance(
        AdminSetPlayerGoldBalanceInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var adminId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var admin = await db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id == adminId)
            ?? throw new GraphQLException(new Error("Admin not found.", "PLAYER_NOT_FOUND"));

        if (admin.Role != "ADMIN")
            throw new GraphQLException(new Error("Admin role required.", "ADMIN_REQUIRED"));

        if (input.Balance < 0)
            throw new GraphQLException(new Error("Balance cannot be negative.", "INVALID_AMOUNT"));

        var target = await db.Players.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Email == input.PlayerEmail)
            ?? throw new GraphQLException(new Error($"Player '{input.PlayerEmail}' not found.", "PLAYER_NOT_FOUND"));

        var goldRow = await db.PlayerGoldBalances.FirstOrDefaultAsync(g => g.PlayerId == target.Id);
        if (goldRow == null)
        {
            goldRow = new PlayerGoldBalance
            {
                Id = Guid.NewGuid(),
                PlayerId = target.Id,
                Balance = input.Balance,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.PlayerGoldBalances.Add(goldRow);
        }
        else
        {
            goldRow.Balance = input.Balance;
            goldRow.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        var blocked = await Query.GetBlockedGoldAsync(db, target.Id);
        return new GoldBalanceInfo { Balance = goldRow.Balance, BlockedInPools = blocked };
    }

    // ── Private helpers ──────────────────────────────────────────────────────────

    private static void ValidateCreatePoolInput(string currencyCode, decimal fiatAmount, decimal goldAmount)
    {
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3 ||
            currencyCode.Equals("XAU", StringComparison.OrdinalIgnoreCase))
            throw new GraphQLException(new Error("CurrencyCode must be a 3-letter fiat ISO code (not XAU).", "INVALID_CURRENCY_CODE"));

        if (fiatAmount <= 0)
            throw new GraphQLException(new Error("FiatAmount must be > 0.", "INVALID_AMOUNT"));

        if (goldAmount <= 0)
            throw new GraphQLException(new Error("GoldAmount must be > 0.", "INVALID_AMOUNT"));
    }

    private static async Task AssertSufficientFiat(AppDbContext db, Guid playerId, string currencyCode, decimal required)
    {
        var available = await Query.GetAvailableFiatAsync(db, playerId, currencyCode);
        if (available < required)
            throw new GraphQLException(new Error(
                $"Insufficient available {currencyCode}. Available: {available:F4}, required: {required:F4}.",
                "INSUFFICIENT_FUNDS"));
    }

    private static async Task AssertSufficientGold(AppDbContext db, Guid playerId, decimal required)
    {
        var available = await Query.GetAvailableGoldAsync(db, playerId);
        if (available < required)
            throw new GraphQLException(new Error(
                $"Insufficient available gold. Available: {available:F8} XAU, required: {required:F8} XAU.",
                "INSUFFICIENT_GOLD"));
    }

    private static async Task DeductFiat(AppDbContext db, Guid playerId, string currencyCode, decimal amount)
    {
        if (currencyCode == "EUR")
        {
            var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId)
                ?? throw new GraphQLException(new Error("Player not found.", "PLAYER_NOT_FOUND"));
            await PersonalBankAccountService.DebitTrackedGrossCashAsync(db, player, amount);
        }
        else
        {
            var bal = await db.PlayerCurrencyBalances.FirstOrDefaultAsync(b => b.PlayerId == playerId && b.CurrencyCode == currencyCode)
                ?? throw new GraphQLException(new Error($"No {currencyCode} balance found.", "INSUFFICIENT_FUNDS"));
            bal.Balance -= amount;
            bal.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    private static async Task CreditFiat(AppDbContext db, Guid playerId, string currencyCode, decimal amount)
    {
        if (currencyCode == "EUR")
        {
            var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId)
                ?? throw new GraphQLException(new Error("Player not found.", "PLAYER_NOT_FOUND"));
            await PersonalBankAccountService.CreditTrackedGrossCashAsync(db, player, amount);
        }
        else
        {
            var bal = await db.PlayerCurrencyBalances.FirstOrDefaultAsync(b => b.PlayerId == playerId && b.CurrencyCode == currencyCode);
            if (bal == null)
            {
                bal = new PlayerCurrencyBalance
                {
                    Id = Guid.NewGuid(), PlayerId = playerId, CurrencyCode = currencyCode,
                    Balance = amount, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
                };
                db.PlayerCurrencyBalances.Add(bal);
            }
            else
            {
                bal.Balance += amount;
                bal.UpdatedAtUtc = DateTime.UtcNow;
            }
        }
    }

    private static async Task DeductGold(AppDbContext db, Guid playerId, decimal amount)
    {
        var gold = await db.PlayerGoldBalances.FirstOrDefaultAsync(g => g.PlayerId == playerId)
            ?? throw new GraphQLException(new Error("No gold balance found.", "INSUFFICIENT_GOLD"));
        gold.Balance -= amount;
        gold.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static async Task CreditGold(AppDbContext db, Guid playerId, decimal amount)
    {
        var gold = await db.PlayerGoldBalances.FirstOrDefaultAsync(g => g.PlayerId == playerId);
        if (gold == null)
        {
            gold = new PlayerGoldBalance
            {
                Id = Guid.NewGuid(), PlayerId = playerId, Balance = amount,
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            };
            db.PlayerGoldBalances.Add(gold);
        }
        else
        {
            gold.Balance += amount;
            gold.UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}

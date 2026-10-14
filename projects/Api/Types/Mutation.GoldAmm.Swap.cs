using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
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

        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync()
            : null;

        var pool = await db.GoldAmmPools.FirstOrDefaultAsync(p => p.CurrencyCode == currencyCode)
            ?? throw new GraphQLException(ErrorBuilder.New().SetMessage($"No pool for {currencyCode}/XAU.").SetCode("POOL_NOT_FOUND").Build());

        if (pool.FiatReserve <= 0 || pool.GoldReserve <= 0)
            throw new GraphQLException(ErrorBuilder.New().SetMessage("Pool has no liquidity.").SetCode("INSUFFICIENT_LIQUIDITY").Build());

        var (outputAmount, feeAmount) = Query.ComputeAmmOutput(direction, input.Amount, pool.FiatReserve, pool.GoldReserve);

        if (input.MinOutputAmount > 0 && outputAmount < input.MinOutputAmount)
            throw new GraphQLException(ErrorBuilder.New().SetMessage($"Output {outputAmount:F8} is below your minimum {input.MinOutputAmount:F8}.").SetCode("SLIPPAGE_EXCEEDED").Build());

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

        var currentTick = await db.GameStates.AsNoTracking().Select(gs => gs.CurrentTick).FirstOrDefaultDeterministicAsync();
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
        if (tx is not null)
        {
            await tx.CommitAsync();
        }

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
            ?? throw new GraphQLException(ErrorBuilder.New().SetMessage("Admin not found.").SetCode("PLAYER_NOT_FOUND").Build());

        if (admin.Role != "ADMIN")
            throw new GraphQLException(ErrorBuilder.New().SetMessage("Admin role required.").SetCode("ADMIN_REQUIRED").Build());

        if (input.Balance < 0)
            throw new GraphQLException(ErrorBuilder.New().SetMessage("Balance cannot be negative.").SetCode("INVALID_AMOUNT").Build());

        var target = await db.Players.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Email == input.PlayerEmail)
            ?? throw new GraphQLException(ErrorBuilder.New().SetMessage($"Player '{input.PlayerEmail}' not found.").SetCode("PLAYER_NOT_FOUND").Build());

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
            throw new GraphQLException(ErrorBuilder.New().SetMessage("CurrencyCode must be a 3-letter fiat ISO code (not XAU).").SetCode("INVALID_CURRENCY_CODE").Build());

        if (fiatAmount <= 0)
            throw new GraphQLException(ErrorBuilder.New().SetMessage("FiatAmount must be > 0.").SetCode("INVALID_AMOUNT").Build());

        if (goldAmount <= 0)
            throw new GraphQLException(ErrorBuilder.New().SetMessage("GoldAmount must be > 0.").SetCode("INVALID_AMOUNT").Build());
    }

    private static async Task AssertSufficientFiat(AppDbContext db, Guid playerId, string currencyCode, decimal required)
    {
        var available = await Query.GetAvailableFiatAsync(db, playerId, currencyCode);
        if (available < required)
            throw new GraphQLException(ErrorBuilder.New().SetMessage($"Insufficient available {currencyCode}. Available: {available:F4}, required: {required:F4}.").SetCode("INSUFFICIENT_FUNDS").Build());
    }

    private static async Task AssertSufficientGold(AppDbContext db, Guid playerId, decimal required)
    {
        var available = await Query.GetAvailableGoldAsync(db, playerId);
        if (available < required)
            throw new GraphQLException(ErrorBuilder.New().SetMessage($"Insufficient available gold. Available: {available:F8} XAU, required: {required:F8} XAU.").SetCode("INSUFFICIENT_GOLD").Build());
    }

    private static async Task DeductFiat(AppDbContext db, Guid playerId, string currencyCode, decimal amount)
    {
        var bal = await PersonalBankAccountService.GetTrackedAccountAsync(db, playerId, currencyCode)
            ?? throw new GraphQLException(ErrorBuilder.New().SetMessage($"No {currencyCode} balance found.").SetCode("INSUFFICIENT_FUNDS").Build());

        bal.Balance -= amount;
    }

    private static async Task CreditFiat(AppDbContext db, Guid playerId, string currencyCode, decimal amount)
    {
        await PersonalBankAccountService.CreditTrackedBalanceAsync(db, playerId, currencyCode, amount);
    }

    private static async Task DeductGold(AppDbContext db, Guid playerId, decimal amount)
    {
        var gold = await db.PlayerGoldBalances.FirstOrDefaultAsync(g => g.PlayerId == playerId)
            ?? throw new GraphQLException(ErrorBuilder.New().SetMessage("No gold balance found.").SetCode("INSUFFICIENT_GOLD").Build());
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

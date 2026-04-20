using Api.Data;
using Api.Data.Entities;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    /// <summary>
    /// Executes a forex currency swap on the player's personal account.
    /// Deducts the source amount (including 1% fee) from the player's balance in the source
    /// currency and credits the net amount to the player's balance in the target currency.
    /// A ForexTradeRecord is persisted for auditing.
    ///
    /// Concurrency safety: the swap runs inside a Serializable transaction so that two
    /// concurrent requests cannot both read the same balance and both pass the
    /// insufficient-funds check.  The player's ConcurrencyToken is refreshed on every
    /// save so that optimistic concurrency at the EF layer provides a second layer of
    /// protection: if two overlapping transactions both read the same token value, the
    /// second SaveChangesAsync will throw DbUpdateConcurrencyException.
    /// </summary>
    [Authorize]
    public async Task<ForexTradeResult> ExecuteForexSwap(
        ExecuteForexSwapInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        Query.ValidateForexInput(input.FromCurrencyCode, input.ToCurrencyCode, input.Amount);

        var fromCode = input.FromCurrencyCode.ToUpperInvariant();
        var toCode = input.ToCurrencyCode.ToUpperInvariant();

        // Pre-read the current tick outside the transaction (read-only, cheap).
        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(gs => gs.CurrentTick)
            .FirstOrDefaultAsync();

        ForexTradeRecord tradeRecord;
        try
        {
            // Wrap the read-check-write sequence in a transaction to keep the balance updates
            // for both currencies atomic.  Concurrency protection comes from the Player's
            // ConcurrencyToken (refreshed below), which causes SaveChangesAsync to throw
            // DbUpdateConcurrencyException if another transaction committed a swap for the
            // same player between our read and our write.
            await using var tx = await db.Database.BeginTransactionAsync();

            var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId)
                ?? throw new GraphQLException(new Error("Player not found.", "PLAYER_NOT_FOUND"));

            // Re-read balance inside the transaction to get the authoritative snapshot.
            var currentBalance = await Query.GetPersonalBalanceAsync(db, playerId, fromCode);

            if (currentBalance < input.Amount)
            {
                throw new GraphQLException(new Error(
                    string.Format(
                        "Insufficient balance. You have {0:F2} {1} but tried to swap {2:F2} {1}.",
                        currentBalance, fromCode, input.Amount),
                    "INSUFFICIENT_FUNDS"));
            }

            var rate = await Query.ComputeForexRateAsync(db, fromCode, toCode);
            var feeAmount = Math.Round(input.Amount * (1m / 100m), 4);
            var netFromAmount = input.Amount - feeAmount;
            var toAmount = Math.Round(netFromAmount * rate, 4);

            // Deduct from source balance.
            if (fromCode == "EUR")
            {
                player.PersonalCash -= input.Amount;
            }
            else
            {
                var fromBalance = await db.PlayerCurrencyBalances
                    .FirstOrDefaultAsync(b => b.PlayerId == playerId && b.CurrencyCode == fromCode);

                if (fromBalance is null)
                    throw new GraphQLException(new Error("No " + fromCode + " balance found.", "INSUFFICIENT_FUNDS"));

                fromBalance.Balance -= input.Amount;
                fromBalance.UpdatedAtUtc = DateTime.UtcNow;
            }

            // Credit to target balance.
            if (toCode == "EUR")
            {
                player.PersonalCash += toAmount;
            }
            else
            {
                var toBalance = await db.PlayerCurrencyBalances
                    .FirstOrDefaultAsync(b => b.PlayerId == playerId && b.CurrencyCode == toCode);

                if (toBalance is null)
                {
                    toBalance = new PlayerCurrencyBalance
                    {
                        Id = Guid.NewGuid(),
                        PlayerId = playerId,
                        CurrencyCode = toCode,
                        Balance = 0m,
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    };
                    db.PlayerCurrencyBalances.Add(toBalance);
                }

                toBalance.Balance += toAmount;
                toBalance.UpdatedAtUtc = DateTime.UtcNow;
            }

            // Refresh the player's ConcurrencyToken so EF's optimistic-concurrency check
            // fires if two transactions race through the balance validation concurrently.
            player.ConcurrencyToken = Guid.NewGuid();

            tradeRecord = new ForexTradeRecord
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                FromCurrencyCode = fromCode,
                ToCurrencyCode = toCode,
                FromAmount = input.Amount,
                ToAmount = toAmount,
                FeeAmount = feeAmount,
                Rate = Math.Round(rate, 6),
                ExecutedAtTick = currentTick,
                ExecutedAtUtc = DateTime.UtcNow
            };
            db.ForexTradeRecords.Add(tradeRecord);

            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Two concurrent swaps raced past the balance check; the second one loses.
            throw new GraphQLException(new Error(
                "A concurrent swap was in progress. Please retry your trade.",
                "CONCURRENT_SWAP_CONFLICT"));
        }

        var newFromBalance = await Query.GetPersonalBalanceAsync(db, playerId, fromCode);
        var newToBalance = await Query.GetPersonalBalanceAsync(db, playerId, toCode);

        return new ForexTradeResult
        {
            TradeId = tradeRecord.Id,
            FromCurrencyCode = fromCode,
            ToCurrencyCode = toCode,
            FromAmount = input.Amount,
            ToAmount = tradeRecord.ToAmount,
            FeeAmount = tradeRecord.FeeAmount,
            Rate = tradeRecord.Rate,
            NewFromBalance = newFromBalance,
            NewToBalance = newToBalance
        };
    }
}

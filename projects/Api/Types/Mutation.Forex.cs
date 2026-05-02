using Api.Configuration;
using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Types;

public sealed partial class Mutation
{
    /// <summary>
    /// Executes a forex currency swap.
    ///
    /// When <c>fromBankAccountId</c> is provided the source funds are drawn from that company bank account;
    /// otherwise the player's personal currency wallet is used.
    /// When <c>toBankAccountId</c> is provided the proceeds are deposited into that bank account;
    /// otherwise the player's personal currency wallet is credited.
    ///
    /// A <see cref="ForexTradeRecord"/> is persisted for auditing.
    ///
    /// Concurrency safety: runs inside a serializable transaction; the player's ConcurrencyToken
    /// is refreshed on save so that EF's optimistic-concurrency check fires on racing requests.
    /// </summary>
    [Authorize]
    public async Task<ForexTradeResult> ExecuteForexSwap(
        ExecuteForexSwapInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] IMasterRankingTelemetryService rankingTelemetry,
        [Service] IOptions<MasterServerRegistrationOptions> masterOptions)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        Query.ValidateForexInput(input.FromCurrencyCode, input.ToCurrencyCode, input.Amount);

        var fromCode = input.FromCurrencyCode.ToUpperInvariant();
        var toCode = input.ToCurrencyCode.ToUpperInvariant();

        // Pre-read the current tick outside the transaction (read-only, cheap).
        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(gs => gs.CurrentTick)
            .FirstOrDefaultDeterministicAsync();

        ForexTradeRecord tradeRecord;
        try
        {
            await using var tx = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync()
                : null;

            var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId)
                ?? throw new GraphQLException(new Error("Player not found.", "PLAYER_NOT_FOUND"));

            var rate = await Query.ComputeForexRateAsync(db, fromCode, toCode);
            var feeAmount = Math.Round(input.Amount * (1m / 100m), 4);
            var netFromAmount = input.Amount - feeAmount;
            var toAmount = Math.Round(netFromAmount * rate, 4);
            BankAccount? fromCompanyAccount = null;
            BankAccount? toCompanyAccount = null;

            if (input.FromBankAccountId.HasValue)
            {
                // ── Bank-account path (source) ─────────────────────────────
                var fromAccount = await db.BankAccounts
                    .Include(a => a.Company)
                    .FirstOrDefaultAsync(a => a.Id == input.FromBankAccountId.Value && a.Company != null && a.Company.PlayerId == playerId)
                    ?? throw new GraphQLException(new Error("Source bank account not found or you do not own it.", "ACCOUNT_NOT_FOUND"));

                if (!string.Equals(fromAccount.CurrencyCode, fromCode, StringComparison.OrdinalIgnoreCase))
                    throw new GraphQLException(new Error(
                        $"Source bank account currency ({fromAccount.CurrencyCode}) does not match the requested from-currency ({fromCode}).",
                        "CURRENCY_MISMATCH"));

                if (fromAccount.Balance < input.Amount)
                    throw new GraphQLException(new Error(
                        string.Format(
                            "Insufficient balance. Account has {0:F2} {1} but tried to swap {2:F2} {1}.",
                            fromAccount.Balance, fromCode, input.Amount),
                        "INSUFFICIENT_FUNDS"));

                fromAccount.Balance -= input.Amount;
                fromCompanyAccount = fromAccount.CompanyId.HasValue ? fromAccount : null;
            }
            else
            {
                // ── Personal wallet path (source) ──────────────────────────
                var currentBalance = await Query.GetPersonalBalanceAsync(db, playerId, fromCode);

                if (currentBalance < input.Amount)
                    throw new GraphQLException(new Error(
                        string.Format(
                            "Insufficient balance. You have {0:F2} {1} but tried to swap {2:F2} {1}.",
                            currentBalance, fromCode, input.Amount),
                        "INSUFFICIENT_FUNDS"));

                if (fromCode == "EUR")
                {
                    await PersonalBankAccountService.DebitTrackedGrossCashAsync(db, player, input.Amount);
                }
                else
                {
                    var fromBalance = await PersonalBankAccountService.GetTrackedAccountAsync(db, playerId, fromCode)
                        ?? throw new GraphQLException(new Error("No " + fromCode + " balance found.", "INSUFFICIENT_FUNDS"));

                    fromBalance.Balance -= input.Amount;
                }
            }

            if (input.ToBankAccountId.HasValue)
            {
                // ── Bank-account path (destination) ───────────────────────
                var toAccount = await db.BankAccounts
                    .Include(a => a.Company)
                    .FirstOrDefaultAsync(a => a.Id == input.ToBankAccountId.Value && a.Company != null && a.Company.PlayerId == playerId)
                    ?? throw new GraphQLException(new Error("Destination bank account not found or you do not own it.", "ACCOUNT_NOT_FOUND"));

                if (!string.Equals(toAccount.CurrencyCode, toCode, StringComparison.OrdinalIgnoreCase))
                    throw new GraphQLException(new Error(
                        $"Destination bank account currency ({toAccount.CurrencyCode}) does not match the requested to-currency ({toCode}).",
                        "CURRENCY_MISMATCH"));

                toAccount.Balance += toAmount;
                toCompanyAccount = toAccount.CompanyId.HasValue ? toAccount : null;
            }
            else
            {
                // ── Personal wallet path (destination) ────────────────────
                if (toCode == "EUR")
                {
                    await PersonalBankAccountService.CreditTrackedGrossCashAsync(db, player, toAmount);
                }
                else
                {
                    var toBalance = await PersonalBankAccountService.EnsureTrackedAccountAsync(db, playerId, toCode);
                    toBalance.Balance += toAmount;
                }
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

            if (fromCompanyAccount?.CompanyId is Guid fromCompanyId)
            {
                db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = fromCompanyId,
                    BankAccountId = fromCompanyAccount.Id,
                    Category = LedgerCategory.ForexSwapOut,
                    Description = $"Forex swap out {input.Amount:F2} {fromCode} to {toCode}",
                    Amount = -input.Amount,
                    RecordedAtTick = currentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                });
            }

            if (toCompanyAccount?.CompanyId is Guid toCompanyId)
            {
                db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = toCompanyId,
                    BankAccountId = toCompanyAccount.Id,
                    Category = LedgerCategory.ForexSwapIn,
                    Description = $"Forex swap in {toAmount:F2} {toCode} from {fromCode}",
                    Amount = toAmount,
                    RecordedAtTick = currentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                });
            }

            await db.SaveChangesAsync();
            if (tx is not null)
            {
                await tx.CommitAsync();
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new GraphQLException(new Error(
                "A concurrent swap was in progress. Please retry your trade.",
                "CONCURRENT_SWAP_CONFLICT"));
        }

        // Compute post-swap balances for the result.
        decimal newFromBalance;
        decimal newToBalance;

        if (input.FromBankAccountId.HasValue)
        {
            newFromBalance = await db.BankAccounts
                .Where(a => a.Id == input.FromBankAccountId.Value)
                .Select(a => a.Balance)
                .FirstOrDefaultDeterministicAsync();
        }
        else
        {
            newFromBalance = await Query.GetPersonalBalanceAsync(db, playerId, fromCode);
        }

        if (input.ToBankAccountId.HasValue)
        {
            newToBalance = await db.BankAccounts
                .Where(a => a.Id == input.ToBankAccountId.Value)
                .Select(a => a.Balance)
                .FirstOrDefaultDeterministicAsync();
        }
        else
        {
            newToBalance = await Query.GetPersonalBalanceAsync(db, playerId, toCode);
        }

        // Fire FX_TRADER telemetry (fire-and-forget).
        {
            var playerEmail = await db.Players
                .AsNoTracking()
                .Where(p => p.Id == playerId)
                .Select(p => p.Email)
                .FirstOrDefaultAsync();
            if (playerEmail is not null)
            {
                var today = DateTime.UtcNow.ToString("yyyyMMdd");
                var serverKey = masterOptions.Value.ServerKey ?? string.Empty;
                _ = rankingTelemetry.ReportEventAsync(
                    MasterRankingBountyCodes.FxTrader,
                    playerEmail,
                    uniqueScopeKey: $"{MasterRankingBountyCodes.FxTrader}:{playerEmail}:{today}:{serverKey}");
            }
        }

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

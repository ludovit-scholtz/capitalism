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
    /// When <c>quoteNonce</c> is provided the server validates that the nonce is fresh (issued within
    /// the 30-second TTL) and has not already been consumed, preventing replay attacks.
    /// When <c>acceptedSlippageBps</c> is provided (basis points, 1 BPS = 0.01%) the settlement is
    /// rejected with <c>SLIPPAGE_EXCEEDED</c> if the current rate deviates from the quoted rate by more.
    ///
    /// A <see cref="ForexTradeRecord"/> is persisted for auditing.
    ///
    /// Concurrency safety: runs inside a serializable transaction; the player's ConcurrencyToken
    /// is refreshed on save so that EF's optimistic-concurrency check fires on racing requests.
    /// API-key callers can swap only through accounts they own; server-side ownership is resolved
    /// from the authenticated principal and foreign account identifiers are rejected.
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

        // ── Quote nonce validation ─────────────────────────────────────────────
        if (input.QuoteNonce.HasValue)
        {
            await ValidateAndConsumeQuoteNonceAsync(db, playerId, input.QuoteNonce.Value);
        }

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
            var isCompanyContext = player.ActiveAccountType == AccountContextType.Company && player.ActiveCompanyId.HasValue;

            if (isCompanyContext && (!input.FromBankAccountId.HasValue || !input.ToBankAccountId.HasValue))
            {
                throw new GraphQLException(new Error(
                    "In company context, both source and destination bank accounts must be selected.",
                    "ACCOUNT_CONTEXT_REQUIRED"));
            }

            var rate = await Query.ComputeForexRateAsync(db, fromCode, toCode);

            // ── Slippage guard ─────────────────────────────────────────────────
            if ((input.AcceptedSlippageBps ?? 0) > 0 && input.QuoteNonce.HasValue)
            {
                // Re-read the consumed nonce record to get the quoted rate.
                var nonceRecord = await db.FxQuoteNonces
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Nonce == input.QuoteNonce.Value && n.PlayerId == playerId);
                if (nonceRecord is not null && nonceRecord.Rate > 0)
                {
                    ValidateSlippage(nonceRecord.Rate, rate, input.AcceptedSlippageBps!.Value, playerId, fromCode, toCode);
                }
            }

            var feeAmount = Math.Round(input.Amount * (1m / 100m), 4);
            var netFromAmount = input.Amount - feeAmount;
            var toAmount = Math.Round(netFromAmount * rate, 4);
            BankAccount? fromCompanyAccount = null;
            BankAccount? toCompanyAccount = null;

            if (input.FromBankAccountId.HasValue)
            {
                // ── Bank-account path (source) ─────────────────────────────
                var fromAccount = await GetAccountInActiveContextAsync(db, input.FromBankAccountId.Value, player)
                    ?? throw new GraphQLException(new Error(
                        ObjectAuthorizationService.FriendlyMessage,
                        ObjectAuthorizationService.NotFoundOrNotOwnedCode));

                if (!string.Equals(fromAccount.CurrencyCode, fromCode, StringComparison.OrdinalIgnoreCase))
                    throw new GraphQLException(new Error(
                        $"Source bank account currency ({fromAccount.CurrencyCode}) does not match the requested from-currency ({fromCode}).",
                        "CURRENCY_MISMATCH"));

                if (fromAccount.Balance < input.Amount)
                    throw new GraphQLException(new Error("Insufficient funds.", "INSUFFICIENT_FUNDS"));

                fromAccount.Balance -= input.Amount;
                fromCompanyAccount = fromAccount.CompanyId.HasValue ? fromAccount : null;
            }
            else
            {
                if (isCompanyContext)
                {
                    throw new GraphQLException(new Error(
                        "In company context, swaps must use selected company bank accounts.",
                        "ACCOUNT_CONTEXT_REQUIRED"));
                }

                // ── Personal wallet path (source) ──────────────────────────
                var currentBalance = await Query.GetPersonalBalanceAsync(db, playerId, fromCode);

                if (currentBalance < input.Amount)
                    throw new GraphQLException(new Error("Insufficient funds.", "INSUFFICIENT_FUNDS"));

                if (fromCode == PersonalBankAccountService.SettlementCurrencyCode)
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
                var toAccount = await GetAccountInActiveContextAsync(db, input.ToBankAccountId.Value, player)
                    ?? throw new GraphQLException(new Error(
                        ObjectAuthorizationService.FriendlyMessage,
                        ObjectAuthorizationService.NotFoundOrNotOwnedCode));

                if (!string.Equals(toAccount.CurrencyCode, toCode, StringComparison.OrdinalIgnoreCase))
                    throw new GraphQLException(new Error(
                        $"Destination bank account currency ({toAccount.CurrencyCode}) does not match the requested to-currency ({toCode}).",
                        "CURRENCY_MISMATCH"));

                toAccount.Balance += toAmount;
                toCompanyAccount = toAccount.CompanyId.HasValue ? toAccount : null;
            }
            else
            {
                if (isCompanyContext)
                {
                    throw new GraphQLException(new Error(
                        "In company context, swaps must use selected company bank accounts.",
                        "ACCOUNT_CONTEXT_REQUIRED"));
                }

                // ── Personal wallet path (destination) ────────────────────
                if (toCode == PersonalBankAccountService.SettlementCurrencyCode)
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

    // ── Quote nonce & slippage helpers ────────────────────────────────────────

    /// <summary>
    /// Validates that the given quote nonce: (1) exists in the database for this player,
    /// (2) has not already been consumed (replay protection), and (3) was issued within
    /// the configurable TTL window (stale-quote protection).
    /// On success, atomically marks the nonce as consumed.
    /// </summary>
    private static async Task ValidateAndConsumeQuoteNonceAsync(AppDbContext db, Guid playerId, Guid quoteNonce)
    {
        var nonceRecord = await db.FxQuoteNonces
            .FirstOrDefaultAsync(n => n.Nonce == quoteNonce && n.PlayerId == playerId);

        if (nonceRecord is null)
        {
            // Log the rejection before throwing.
            await LogFxSecurityRejectionAsync(db, playerId, quoteNonce, "QUOTE_NONCE_NOT_FOUND");
            throw new GraphQLException(new Error(
                "Quote nonce not found. Please request a new quote.",
                "QUOTE_NONCE_NOT_FOUND"));
        }

        if (nonceRecord.ConsumedAtUtc.HasValue)
        {
            await LogFxSecurityRejectionAsync(db, playerId, quoteNonce, "QUOTE_ALREADY_USED");
            throw new GraphQLException(new Error(
                "This quote has already been used. Please request a new quote.",
                "QUOTE_ALREADY_USED"));
        }

        var age = DateTime.UtcNow - nonceRecord.IssuedAtUtc;
        if (age.TotalSeconds > Query.QuoteTtlSeconds)
        {
            await LogFxSecurityRejectionAsync(db, playerId, quoteNonce, "QUOTE_EXPIRED");
            throw new GraphQLException(new Error(
                $"Quote expired ({age.TotalSeconds:F0}s old). Please request a fresh quote.",
                "QUOTE_EXPIRED"));
        }

        // Mark consumed.
        nonceRecord.ConsumedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Validates that the current settlement rate does not deviate from the quoted rate
    /// by more than the accepted slippage expressed in basis points (1 BPS = 0.01%).
    /// Throws <c>SLIPPAGE_EXCEEDED</c> if the deviation is too large.
    /// </summary>
    private static void ValidateSlippage(
        decimal quotedRate,
        decimal currentRate,
        int acceptedSlippageBps,
        Guid playerId,
        string fromCode,
        string toCode)
    {
        if (quotedRate <= 0 || currentRate <= 0) return;

        var deviationBps = Math.Abs((currentRate - quotedRate) / quotedRate) * 10_000m;
        if (deviationBps > acceptedSlippageBps)
        {
            throw new GraphQLException(new Error(
                $"Market moved too far. Rate deviation {deviationBps:F1} BPS exceeds your accepted slippage of {acceptedSlippageBps} BPS. " +
                $"Quoted: {quotedRate:F6} {toCode}/{fromCode}, current: {currentRate:F6}. " +
                "Please request a new quote or increase your slippage tolerance.",
                "SLIPPAGE_EXCEEDED"));
        }
    }

    /// <summary>Appends a structured security-rejection record to the audit log.</summary>
    private static async Task LogFxSecurityRejectionAsync(AppDbContext db, Guid playerId, Guid nonce, string reason)
    {
        db.FxSecurityAuditLogs.Add(new FxSecurityAuditLog
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            NonceHash = nonce.ToString("N"),
            RejectionReason = reason,
            OccurredAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task<BankAccount?> GetAccountInActiveContextAsync(AppDbContext db, Guid bankAccountId, Player player)
    {
        if (player.ActiveAccountType == AccountContextType.Company && player.ActiveCompanyId.HasValue)
        {
            return await db.BankAccounts
                .Include(account => account.Company)
                .FirstOrDefaultAsync(account => account.Id == bankAccountId && account.CompanyId == player.ActiveCompanyId.Value);
        }

        return await db.BankAccounts
            .FirstOrDefaultAsync(account => account.Id == bankAccountId && account.PlayerId == player.Id && account.CompanyId == null);
    }
}

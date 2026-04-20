using System.Security.Claims;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace MasterApi.Types;

public sealed partial class Mutation
{
    /// <summary>
    /// Adjusts a player's gold token balance (positive = top-up, negative = deduction).
    /// Requires global admin or root administrator access.
    /// Records an immutable audit transaction for every adjustment.
    ///
    /// Concurrency safety: the adjust runs inside a database transaction so that the balance
    /// update and the audit record are committed atomically.  The PlayerAccount's
    /// ConcurrencyToken is refreshed on every successful write (set to a new Guid).  EF Core
    /// adds a WHERE ConcurrencyToken = &lt;original&gt; predicate to the UPDATE statement; if a
    /// concurrent write already committed and changed the token, the WHERE matches 0 rows and
    /// EF throws DbUpdateConcurrencyException.  The caller receives CONCURRENT_ADJUSTMENT_CONFLICT
    /// and should reload the current balance before retrying.
    ///
    /// The transaction is only started for relational databases; the in-memory EF provider used
    /// in integration tests does not support Begin/Commit, but still enforces the optimistic
    /// concurrency check through EF's in-memory concurrency detection.
    /// </summary>
    [HotChocolate.Authorization.Authorize]
    public async Task<GoldTokenBalanceInfo> AdjustGoldTokenBalance(
        AdjustGoldTokenInput input,
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions)
    {
        var callerEmail = Query.GetEmailFromClaims(claimsPrincipal);
        var access = await Query.BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, callerEmail);
        if (!access.CanAccessEveryGameDashboard)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Gold token administration requires global admin access.")
                    .SetCode("GLOBAL_ADMIN_REQUIRED")
                    .Build());
        }

        if (input.Amount == 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Amount must be non-zero.")
                    .SetCode("INVALID_AMOUNT")
                    .Build());
        }

        if (string.IsNullOrWhiteSpace(input.Note))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("An audit note is required for every gold balance adjustment.")
                    .SetCode("NOTE_REQUIRED")
                    .Build());
        }

        var targetEmail = Query.NormalizeEmail(input.TargetEmail, "INVALID_TARGET_EMAIL");

        // Only relational databases (PostgreSQL in production) support explicit transactions.
        // The in-memory EF provider used in integration tests does not, but it does enforce
        // the IsConcurrencyToken() check inside SaveChangesAsync.
        IDbContextTransaction? tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync()
            : null;

        try
        {
            var target = await db.PlayerAccounts.FirstOrDefaultAsync(p => p.Email == targetEmail)
                ?? throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Target player not found.")
                        .SetCode("PLAYER_NOT_FOUND")
                        .Build());

            var balanceBefore = target.GoldTokenBalance;
            var newBalance = balanceBefore + input.Amount;

            if (newBalance < 0m)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage($"Deduction would result in a negative balance. Current balance: {balanceBefore}, deduction: {Math.Abs(input.Amount)}.")
                        .SetCode("INSUFFICIENT_BALANCE")
                        .Build());
            }

            target.GoldTokenBalance = newBalance;

            // Set a fresh token on each write so EF Core's WHERE ConcurrencyToken = <original>
            // predicate causes a DbUpdateConcurrencyException for any concurrent request that
            // loaded the same original token value.
            target.ConcurrencyToken = Guid.NewGuid();

            db.GoldTokenTransactions.Add(new GoldTokenTransaction
            {
                Id = Guid.NewGuid(),
                PlayerAccountId = target.Id,
                PlayerEmail = target.Email,
                Amount = input.Amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = newBalance,
                AdminEmail = callerEmail,
                Note = input.Note?.Trim(),
                CreatedAtUtc = DateTime.UtcNow,
            });

            await db.SaveChangesAsync();

            if (tx is not null)
                await tx.CommitAsync();

            return new GoldTokenBalanceInfo
            {
                PlayerId = target.Id,
                Email = target.Email,
                DisplayName = target.DisplayName,
                GoldTokenBalance = target.GoldTokenBalance,
            };
        }
        catch (DbUpdateConcurrencyException)
        {
            // Two concurrent adjustments raced past the non-negative check for the same player.
            // The second one loses — the admin should reload and retry.
            if (tx is not null)
                await tx.RollbackAsync();

            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("A concurrent adjustment was in progress for this player. Please reload the balance and retry.")
                    .SetCode("CONCURRENT_ADJUSTMENT_CONFLICT")
                    .Build());
        }
        finally
        {
            if (tx is not null)
                await tx.DisposeAsync();
        }
    }
}

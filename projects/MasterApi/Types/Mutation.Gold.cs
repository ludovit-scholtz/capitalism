using System.Security.Claims;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MasterApi.Types;

public sealed partial class Mutation
{
    /// <summary>
    /// Adjusts a player's gold token balance (positive = top-up, negative = deduction).
    /// Requires global admin or root administrator access.
    /// Records an immutable audit transaction for every adjustment.
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

        var targetEmail = Query.NormalizeEmail(input.TargetEmail, "INVALID_TARGET_EMAIL");

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

        return new GoldTokenBalanceInfo
        {
            PlayerId = target.Id,
            Email = target.Email,
            DisplayName = target.DisplayName,
            GoldTokenBalance = target.GoldTokenBalance,
        };
    }
}

using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Security;

/// <summary>
/// Picks the audit reason for a bank account that could not be resolved in the caller's active account context.
/// The client always receives the same uniform error; this only decides what the security log records.
/// </summary>
public static class BankAccountDenialClassifier
{
    public static async Task<string> ClassifyAsync(AppDbContext db, Guid bankAccountId, Guid playerId)
    {
        var owner = await db.BankAccounts
            .AsNoTracking()
            .Where(account => account.Id == bankAccountId)
            .Select(account => new { account.PlayerId, CompanyPlayerId = account.Company == null ? (Guid?)null : account.Company.PlayerId })
            .FirstOrDefaultAsync();

        if (owner is null)
        {
            return ObjectAuthorizationService.NotFoundReason;
        }

        return owner.PlayerId == playerId || owner.CompanyPlayerId == playerId
            ? ObjectAuthorizationService.WrongContextReason
            : ObjectAuthorizationService.NotOwnedReason;
    }
}

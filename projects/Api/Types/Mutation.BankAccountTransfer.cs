using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

/// <summary>
/// Mutation for transferring funds between two bank accounts owned by the authenticated player.
/// Both accounts must use the same currency; cross-currency moves go through the Forex Exchange.
/// </summary>
public sealed partial class Mutation
{
    /// <summary>
    /// Transfers <paramref name="input.Amount"/> from one of the player's bank accounts to another.
    /// Records two ledger entries (BANK_ACCOUNT_TRANSFER_OUT on the source company,
    /// BANK_ACCOUNT_TRANSFER_IN on the destination company) so both bank statements show the move.
    /// </summary>
    [Authorize]
    public async Task<TransferFundsResult> TransferFunds(
        TransferFundsInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var player = await db.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == userId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        if (input.FromBankAccountId == input.ToBankAccountId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Source and destination accounts must be different.")
                    .SetCode("SAME_ACCOUNT")
                    .Build());
        }

        if (input.Amount <= 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Transfer amount must be positive.")
                    .SetCode("INVALID_AMOUNT")
                    .Build());
        }

        var fromAccount = await db.BankAccounts
            .Include(a => a.Company)
            .Include(a => a.Player)
            .FirstOrDefaultAsync(a => a.Id == input.FromBankAccountId);

        if (fromAccount is null
            || ((fromAccount.Company is null || fromAccount.Company.PlayerId != userId)
                && fromAccount.PlayerId != userId))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(ObjectAuthorizationService.FriendlyMessage)
                    .SetCode(ObjectAuthorizationService.NotFoundOrNotOwnedCode)
                    .Build());
        }

        var toAccount = await db.BankAccounts
            .Include(a => a.Company)
            .Include(a => a.Player)
            .FirstOrDefaultAsync(a => a.Id == input.ToBankAccountId);

        if (toAccount is null
            || ((toAccount.Company is null || toAccount.Company.PlayerId != userId)
                && toAccount.PlayerId != userId))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(ObjectAuthorizationService.FriendlyMessage)
                    .SetCode(ObjectAuthorizationService.NotFoundOrNotOwnedCode)
                    .Build());
        }

        var companyContext = player.ActiveAccountType == AccountContextType.Company && player.ActiveCompanyId.HasValue;
        if (companyContext)
        {
            if (fromAccount.CompanyId != player.ActiveCompanyId || toAccount.CompanyId != player.ActiveCompanyId)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Transfers in company context are allowed only between accounts of the active company.")
                        .SetCode("ACCOUNT_CONTEXT_MISMATCH")
                        .Build());
            }
        }
        else
        {
            if (fromAccount.PlayerId != userId || fromAccount.CompanyId is not null || toAccount.PlayerId != userId || toAccount.CompanyId is not null)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Transfers in personal context are allowed only between personal accounts.")
                        .SetCode("ACCOUNT_CONTEXT_MISMATCH")
                        .Build());
            }
        }

        if (!string.Equals(fromAccount.CurrencyCode, toAccount.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(
                        $"Currency mismatch: {fromAccount.CurrencyCode} vs {toAccount.CurrencyCode}. "
                        + "Use the Forex Exchange to swap between currencies.")
                    .SetCode("CURRENCY_MISMATCH")
                    .Build());
        }

        if (fromAccount.Balance < input.Amount)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(
                        $"Insufficient funds. Available: {fromAccount.Balance:F2} {fromAccount.CurrencyCode}.")
                    .SetCode("INSUFFICIENT_FUNDS")
                    .Build());
        }

        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(gs => gs.CurrentTick)
            .FirstOrDefaultDeterministicAsync();

        // Move the money.
        fromAccount.Balance -= input.Amount;
        toAccount.Balance += input.Amount;

        var description = string.IsNullOrWhiteSpace(input.Description)
            ? "Transfer between own accounts"
            : input.Description!.Trim();

        var nowUtc = DateTime.UtcNow;

        if (fromAccount.CompanyId.HasValue)
        {
            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = fromAccount.CompanyId.Value,
                BankAccountId = fromAccount.Id,
                Category = LedgerCategory.BankAccountTransferOut,
                Description = $"{description} → {(toAccount.Company?.Name ?? toAccount.Player?.DisplayName ?? "Personal")} ({toAccount.AccountNumber})",
                Amount = -input.Amount,
                RecordedAtTick = currentTick,
                RecordedAtUtc = nowUtc,
            });
        }

        if (toAccount.CompanyId.HasValue)
        {
            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = toAccount.CompanyId.Value,
                BankAccountId = toAccount.Id,
                Category = LedgerCategory.BankAccountTransferIn,
                Description = $"{description} ← {(fromAccount.Company?.Name ?? fromAccount.Player?.DisplayName ?? "Personal")} ({fromAccount.AccountNumber})",
                Amount = input.Amount,
                RecordedAtTick = currentTick,
                RecordedAtUtc = nowUtc,
            });
        }

        await db.SaveChangesAsync();

        return new TransferFundsResult
        {
            Amount = input.Amount,
            CurrencyCode = fromAccount.CurrencyCode,
            FromAccount = new PlayerBankAccountSummary
            {
                Id = fromAccount.Id,
                AccountNumber = fromAccount.AccountNumber,
                CurrencyCode = fromAccount.CurrencyCode,
                Balance = fromAccount.Balance,
                CompanyId = fromAccount.CompanyId,
                CompanyName = fromAccount.Company?.Name,
                OwnerType = fromAccount.CompanyId.HasValue ? "COMPANY" : "PERSON",
                OwnerDisplayName = fromAccount.Company?.Name ?? PublicPlayerDisplayName.Resolve(fromAccount.Player),
            },
            ToAccount = new PlayerBankAccountSummary
            {
                Id = toAccount.Id,
                AccountNumber = toAccount.AccountNumber,
                CurrencyCode = toAccount.CurrencyCode,
                Balance = toAccount.Balance,
                CompanyId = toAccount.CompanyId,
                CompanyName = toAccount.Company?.Name,
                OwnerType = toAccount.CompanyId.HasValue ? "COMPANY" : "PERSON",
                OwnerDisplayName = toAccount.Company?.Name ?? PublicPlayerDisplayName.Resolve(toAccount.Player),
            },
        };
    }
}

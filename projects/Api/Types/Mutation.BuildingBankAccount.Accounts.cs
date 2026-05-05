using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Api.Types;

public sealed partial class Mutation
{
    /// <summary>
    /// Creates a new personal bank account for the authenticated player in the specified currency.
    /// The currency must match a city currency available in this game server.
    /// </summary>
    [Authorize]
    public async Task<CreatePersonalBankAccountResult> CreatePersonalBankAccount(
        CreatePersonalBankAccountInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var currencyCode = input.CurrencyCode.ToUpperInvariant();

        var validCurrency = await db.Cities.AnyAsync(city => city.CurrencyCode == currencyCode);
        if (!validCurrency)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Currency '{currencyCode}' is not used by any city in this game server.")
                    .SetCode("INVALID_CURRENCY")
                    .Build());
        }

        var existing = await db.BankAccounts
            .AnyAsync(account => account.PlayerId == userId && account.CompanyId == null && account.CurrencyCode == currencyCode && account.ClosedAtUtc == null);
        if (existing)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"A personal bank account in {currencyCode} already exists.")
                    .SetCode("DUPLICATE_BANK_ACCOUNT")
                    .Build());
        }

        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = GenerateRandomAccountNumber(),
            CurrencyCode = currencyCode,
            Balance = 0m,
            PlayerId = userId,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.BankAccounts.Add(account);
        await db.SaveChangesAsync();

        return new CreatePersonalBankAccountResult
        {
            Account = new CompanyBankAccountSummary
            {
                Id = account.Id,
                AccountNumber = account.AccountNumber,
                CurrencyCode = account.CurrencyCode,
                Balance = account.Balance,
                AlertMinBalanceThreshold = account.AlertMinBalanceThreshold,
            },
        };
    }

    /// <summary>
    /// Permanently closes a company bank account whose balance is exactly zero.
    /// This mutation handles regular (non-deposit) company treasury accounts only.
    /// Deposit accounts held at a bank building must be closed via <c>closeBankAccount</c>.
    /// </summary>
    /// <remarks>
    /// Rejected with <c>ACCOUNT_NOT_FOUND</c> if the account does not exist or is already closed.
    /// Rejected with <c>UNAUTHORIZED</c> if the caller does not own the account.
    /// Rejected with <c>NON_ZERO_BALANCE</c> if the balance is not exactly zero.
    /// Rejected with <c>GOVERNMENT_ACCOUNT</c> if the account is a government system account.
    /// Rejected with <c>ACCOUNT_IN_USE</c> if the account is still assigned as the active bank account for one or more buildings.
    /// Rejected with <c>DEPOSIT_ACCOUNT</c> if the account is a deposit account held at a bank building (use <c>closeBankAccount</c> instead).
    /// </remarks>
    [Authorize]
    public async Task<CloseCompanyBankAccountResult> CloseCompanyBankAccount(
        CloseCompanyBankAccountInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var account = await db.BankAccounts
            .Include(a => a.Company)
            .FirstOrDefaultAsync(a => a.Id == input.BankAccountId && a.ClosedAtUtc == null);

        if (account is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Bank account not found or already closed.")
                    .SetCode("ACCOUNT_NOT_FOUND")
                    .Build());
        }

        // Only company-owned accounts can be closed via this mutation.
        if (account.Company?.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You do not own this bank account.")
                    .SetCode("UNAUTHORIZED")
                    .Build());
        }

        // Deposit accounts (held at a bank building) are managed by closeBankAccount.
        if (account.BankBuildingId.HasValue)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("This account is a bank deposit and must be closed via the bank building's withdrawal flow.")
                    .SetCode("DEPOSIT_ACCOUNT")
                    .Build());
        }

        // Government system accounts cannot be closed by players.
        if (account.IsGovernmentAccount)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Government system accounts cannot be closed.")
                    .SetCode("GOVERNMENT_ACCOUNT")
                    .Build());
        }

        // Exact zero balance is required — any residual amount blocks closure.
        if (account.Balance != 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"The account balance must be exactly zero before it can be closed. Current balance: {account.Balance:F2} {account.CurrencyCode}. Transfer all remaining funds to another account first.")
                    .SetCode("NON_ZERO_BALANCE")
                    .Build());
        }

        // Block closure if any building still relies on this account.
        var assignedBuildingName = await db.Buildings
            .Where(b => b.BankAccountId == account.Id)
            .Select(b => b.Name)
            .FirstOrDefaultAsync();

        if (assignedBuildingName is not null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"This account is still assigned as the bank account for '{assignedBuildingName}'. Reassign or unlink the building first.")
                    .SetCode("ACCOUNT_IN_USE")
                    .Build());
        }

        // Block closure if any active or overdue loan still uses this account for scheduled repayments.
        var hasActiveLoan = await db.Loans
            .AnyAsync(l => l.BorrowerBankAccountId == account.Id
                && (l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue));

        if (hasActiveLoan)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("This account is still the scheduled repayment account for an active loan. Reassign the repayment account or fully repay the loan before closing this account.")
                    .SetCode("ACTIVE_LOAN_REPAYMENT_ACCOUNT")
                    .Build());
        }

        var closedAtUtc = DateTime.UtcNow;
        account.ClosedAtUtc = closedAtUtc;

        await db.SaveChangesAsync();

        return new CloseCompanyBankAccountResult
        {
            Id = account.Id,
            AccountNumber = account.AccountNumber,
            CurrencyCode = account.CurrencyCode,
            ClosedAtUtc = closedAtUtc,
        };
    }

    // ── Private helpers ──

    private static BuildingBankAccountInfo BuildingBankAccountInfoFromEntity(
        Data.Entities.Building building,
        BankAccount? account)
    {
        var currencyCode = building.City?.CurrencyCode ?? "EUR";
        return new BuildingBankAccountInfo
        {
            BuildingId = building.Id,
            BuildingName = building.Name,
            CityName = building.City?.Name ?? string.Empty,
            CurrencyCode = currencyCode,
            HasBankAccount = account is not null,
            BankAccountId = account?.Id,
            AccountNumber = account?.AccountNumber,
            Balance = account?.Balance,
            AlertMinBalanceThreshold = account?.AlertMinBalanceThreshold,
            IsSuspendedForFunds = building.IsSuspendedForFunds,
            SuspendedReason = building.SuspendedReason,
        };
    }

    /// <summary>
    /// Generates a cryptographically random 16-digit decimal account number.
    /// Guaranteed to be unique with overwhelming probability.
    /// </summary>
    private static string GenerateRandomAccountNumber()
    {
        var bytes = RandomNumberGenerator.GetBytes(8);
        var value = BitConverter.ToUInt64(bytes, 0);
        return (value % 10_000_000_000_000_000UL).ToString("D16");
    }

    private static async Task<List<BankAccount>> LoadActiveCompanyBankAccountsAsync(
        AppDbContext db,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        return await db.BankAccounts
            .Where(account => account.CompanyId == companyId && account.ClosedAtUtc == null)
            .ToListAsync(cancellationToken);
    }

    private static async Task<BankAccount> ResolveCompanyTransferAccountAsync(
        AppDbContext db,
        Guid companyId,
        string currencyCode,
        Guid? excludeAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var account = await CompanyBankingService.FindPreferredAccountAsync(
            db,
            companyId,
            currencyCode,
            excludeAccountId,
            cancellationToken);

        if (account is not null)
        {
            return account;
        }

        var newAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = GenerateRandomAccountNumber(),
            CurrencyCode = currencyCode.ToUpperInvariant(),
            Balance = 0m,
            CompanyId = companyId,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.BankAccounts.Add(newAccount);
        return newAccount;
    }
}

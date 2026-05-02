using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Api.Types;

/// <summary>
/// Mutations for building bank account management:
/// funding, assigning, and creating accounts.
/// </summary>
public sealed partial class Mutation
{
    /// <summary>
    /// Transfers money from one company-owned bank account into the building's assigned bank account.
    /// If the building has no bank account yet, it is assigned a company account in
    /// the building city's currency before the transfer.
    /// </summary>
    [Authorize]
    public async Task<FundBuildingBankAccountResult> FundBuildingBankAccount(
        FundBuildingBankAccountInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        if (input.Amount <= 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Amount must be positive.")
                    .SetCode("INVALID_AMOUNT")
                    .Build());
        }

        var building = await db.Buildings
            .Include(b => b.BankAccount)
            .Include(b => b.City)
            .Include(b => b.Company)
            .FirstOrDefaultAsync(b => b.Id == input.BuildingId && b.Company.PlayerId == userId);

        if (building is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Building not found or you do not own it.")
                    .SetCode("BUILDING_NOT_FOUND")
                    .Build());
        }

        var company = building.Company;
        var cityCurrencyCode = building.City?.CurrencyCode ?? "EUR";
        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(gs => gs.CurrentTick)
            .FirstOrDefaultDeterministicAsync();

        var bankAccount = building.BankAccount
            ?? await BuildingBankAccountProvisioning.EnsureBuildingAssignedAccountAsync(
                db,
                building,
                cityCurrencyCode,
                httpContextAccessor.HttpContext!.RequestAborted);
        var sourceAccount = await ResolveCompanyTransferAccountAsync(
            db,
            company.Id,
            cityCurrencyCode,
            bankAccount.Id,
            httpContextAccessor.HttpContext!.RequestAborted);

        if (sourceAccount.Id != bankAccount.Id && sourceAccount.Balance < input.Amount)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Insufficient company funds. Source account {sourceAccount.AccountNumber} has {sourceAccount.Balance:F2} {cityCurrencyCode} available.")
                    .SetCode("INSUFFICIENT_COMPANY_CASH")
                    .Build());
        }

        if (sourceAccount.Id != bankAccount.Id)
        {
            sourceAccount.Balance -= input.Amount;
            bankAccount.Balance += input.Amount;

            var nowUtc = DateTime.UtcNow;
            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                BuildingId = building.Id,
                BankAccountId = sourceAccount.Id,
                Category = LedgerCategory.BankAccountTransferOut,
                Description = $"Funding transfer to building account {bankAccount.AccountNumber} ({building.Name})",
                Amount = -input.Amount,
                RecordedAtTick = currentTick,
                RecordedAtUtc = nowUtc,
            });

            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                BuildingId = building.Id,
                BankAccountId = bankAccount.Id,
                Category = LedgerCategory.BankAccountTransferIn,
                Description = $"Funding transfer from company account {sourceAccount.AccountNumber} to {building.Name}",
                Amount = input.Amount,
                RecordedAtTick = currentTick,
                RecordedAtUtc = nowUtc,
            });
        }

        // Clear any suspension that was due to insufficient funds.
        if (building.SuspendedReason?.StartsWith("INSUFFICIENT_FUNDS", StringComparison.Ordinal) == true)
        {
            building.IsSuspendedForFunds = false;
            building.SuspendedReason = null;
        }

        await db.SaveChangesAsync();

        return new FundBuildingBankAccountResult
        {
            BankAccount = BuildingBankAccountInfoFromEntity(building, building.BankAccount),
            RemainingCompanyCash = await CompanyBankingService.GetTotalBalanceAsync(
                db,
                company.Id,
                httpContextAccessor.HttpContext!.RequestAborted),
        };
    }

    /// <summary>
    /// Assigns an existing company bank account to a building.
    /// The account must be owned by the same company as the building and must
    /// have the same currency as the building's city.
    /// </summary>
    [Authorize]
    public async Task<AssignBuildingBankAccountResult> AssignBuildingBankAccount(
        AssignBuildingBankAccountInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .Include(b => b.City)
            .Include(b => b.Company)
            .FirstOrDefaultAsync(b => b.Id == input.BuildingId && b.Company.PlayerId == userId);

        if (building is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Building not found or you do not own it.")
                    .SetCode("BUILDING_NOT_FOUND")
                    .Build());
        }

        var account = await db.BankAccounts
            .FirstOrDefaultAsync(a => a.Id == input.BankAccountId && a.CompanyId == building.CompanyId);

        if (account is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Bank account not found or it does not belong to this building's company.")
                    .SetCode("BANK_ACCOUNT_NOT_FOUND")
                    .Build());
        }

        var cityCurrency = building.City?.CurrencyCode ?? "EUR";
        if (!string.Equals(account.CurrencyCode, cityCurrency, StringComparison.OrdinalIgnoreCase))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Account currency {account.CurrencyCode} does not match city currency {cityCurrency}.")
                    .SetCode("CURRENCY_MISMATCH")
                    .Build());
        }

        building.BankAccountId = account.Id;
        building.BankAccount = account;

        await db.SaveChangesAsync();

        return new AssignBuildingBankAccountResult
        {
            BankAccount = BuildingBankAccountInfoFromEntity(building, account),
        };
    }

    /// <summary>
    /// Creates a new bank account for the given company in the specified currency.
    /// The company must be owned by the caller.
    /// The currency must match a city currency available in this game server.
    /// </summary>
    [Authorize]
    public async Task<CreateCompanyBankAccountResult> CreateCompanyBankAccount(
        CreateCompanyBankAccountInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var company = await db.Companies
            .FirstOrDefaultAsync(c => c.Id == input.CompanyId && c.PlayerId == userId);

        if (company is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Company not found or you do not own it.")
                    .SetCode("COMPANY_NOT_FOUND")
                    .Build());
        }

        // Validate currency is available in this game server.
        var currencyCode = input.CurrencyCode.ToUpperInvariant();
        var validCurrency = await db.Cities.AnyAsync(c => c.CurrencyCode == currencyCode);
        if (!validCurrency)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Currency '{currencyCode}' is not used by any city in this game server.")
                    .SetCode("INVALID_CURRENCY")
                    .Build());
        }

        // Prevent duplicate accounts for the same company + currency.
        var existing = await db.BankAccounts
            .AnyAsync(a => a.CompanyId == input.CompanyId && a.CurrencyCode == currencyCode);

        if (existing)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"A bank account in {currencyCode} already exists for this company.")
                    .SetCode("DUPLICATE_BANK_ACCOUNT")
                    .Build());
        }

        var newAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = GenerateRandomAccountNumber(),
            CurrencyCode = currencyCode,
            Balance = 0m,
            CompanyId = company.Id,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(newAccount);
        await db.SaveChangesAsync();

        return new CreateCompanyBankAccountResult
        {
            Account = new CompanyBankAccountSummary
            {
                Id = newAccount.Id,
                AccountNumber = newAccount.AccountNumber,
                CurrencyCode = newAccount.CurrencyCode,
                Balance = newAccount.Balance,
                AlertMinBalanceThreshold = newAccount.AlertMinBalanceThreshold,
            },
        };
    }

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

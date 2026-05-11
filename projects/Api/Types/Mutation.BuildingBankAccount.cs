using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

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
                    .SetMessage(ObjectAuthorizationService.FriendlyMessage)
                    .SetCode(ObjectAuthorizationService.NotFoundOrNotOwnedCode)
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
                    .SetMessage("Insufficient company funds in the source account.")
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
                    .SetMessage(ObjectAuthorizationService.FriendlyMessage)
                    .SetCode(ObjectAuthorizationService.NotFoundOrNotOwnedCode)
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

}


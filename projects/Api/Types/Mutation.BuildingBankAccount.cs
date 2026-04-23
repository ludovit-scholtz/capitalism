using Api.Data;
using Api.Data.Entities;
using Api.Security;
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
    /// Transfers money from the company's cash into the building's assigned bank account.
    /// If the building has no bank account yet, one is automatically created for the
    /// company in the city's currency before the transfer.
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

        if (company.Cash < input.Amount)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Insufficient company cash. Available: {company.Cash:F2} {building.City?.CurrencyCode}.")
                    .SetCode("INSUFFICIENT_COMPANY_CASH")
                    .Build());
        }

        // Auto-create a bank account if the building doesn't have one yet.
        if (building.BankAccount is null)
        {
            var currencyCode = building.City?.CurrencyCode ?? "EUR";
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
            building.BankAccountId = newAccount.Id;
            building.BankAccount = newAccount;
        }

        // Transfer money.
        company.Cash -= input.Amount;
        building.BankAccount.Balance += input.Amount;

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
            RemainingCompanyCash = company.Cash,
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
            },
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
}

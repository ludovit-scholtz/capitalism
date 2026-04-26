using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Api.Utilities;

/// <summary>
/// Provisions company-owned bank accounts for buildings in their city currency.
/// Reuses an existing company account for that currency when available and only
/// creates a new one when the company has none yet.
/// </summary>
public static class BuildingBankAccountProvisioning
{
    private static bool UsePostgresCompatPath(AppDbContext db)
        => db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

    public static async Task<BankAccount> EnsureBuildingAssignedAccountAsync(
        AppDbContext db,
        Building building,
        string? currencyCode = null,
        CancellationToken cancellationToken = default)
    {
        if (building.BankAccountId.HasValue)
        {
            var trackedAssignedAccount = db.BankAccounts.Local
                .FirstOrDefault(account => account.Id == building.BankAccountId.Value);

            if (trackedAssignedAccount is not null)
            {
                building.BankAccount = trackedAssignedAccount;
                return trackedAssignedAccount;
            }

            BankAccount? persistedAssignedAccount;

            if (UsePostgresCompatPath(db))
            {
                var allAccounts = await db.BankAccounts
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);
                persistedAssignedAccount = allAccounts.FirstOrDefault(account => account.Id == building.BankAccountId.Value);
            }
            else
            {
                persistedAssignedAccount = await db.BankAccounts
                    .FirstOrDefaultAsync(account => account.Id == building.BankAccountId.Value, cancellationToken);
            }

            if (persistedAssignedAccount is not null)
            {
                building.BankAccount = persistedAssignedAccount;
                return persistedAssignedAccount;
            }
        }

        var resolvedCurrencyCode = (currencyCode ?? building.City?.CurrencyCode ?? "EUR").ToUpperInvariant();
        var companyAccount = await EnsureCompanyCurrencyAccountAsync(
            db,
            building.CompanyId,
            resolvedCurrencyCode,
            cancellationToken);

        building.BankAccountId = companyAccount.Id;
        building.BankAccount = companyAccount;
        return companyAccount;
    }

    public static async Task<BankAccount> EnsureCompanyCurrencyAccountAsync(
        AppDbContext db,
        Guid companyId,
        string currencyCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCurrencyCode = currencyCode.ToUpperInvariant();

        var trackedAccount = db.BankAccounts.Local.FirstOrDefault(
            account => account.CompanyId == companyId
                && string.Equals(account.CurrencyCode, normalizedCurrencyCode, StringComparison.OrdinalIgnoreCase));

        if (trackedAccount is not null)
        {
            return trackedAccount;
        }

        BankAccount? existingAccount;

        if (UsePostgresCompatPath(db))
        {
            var companyAccountsInCurrency = await db.BankAccounts
                .AsNoTracking()
                .Where(account => account.CompanyId.HasValue
                    && account.CurrencyCode != null
                    && account.CurrencyCode.ToUpper() == normalizedCurrencyCode)
                .OrderBy(account => account.CreatedAtUtc)
                .ToListAsync(cancellationToken);
            existingAccount = companyAccountsInCurrency.FirstOrDefault(account => account.CompanyId == companyId);
        }
        else
        {
            existingAccount = await db.BankAccounts.FirstOrDefaultAsync(
                account => account.CompanyId == companyId && account.CurrencyCode == normalizedCurrencyCode,
                cancellationToken);
        }

        if (existingAccount is not null)
        {
            return existingAccount;
        }

        var resolvedBankBuildingId = await ResolveGovernmentBankBuildingIdAsync(
            db,
            companyId,
            normalizedCurrencyCode,
            cancellationToken);

        var newAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = GenerateRandomAccountNumber(),
            CurrencyCode = normalizedCurrencyCode,
            Balance = 0m,
            CompanyId = companyId,
            BankBuildingId = resolvedBankBuildingId,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.BankAccounts.Add(newAccount);
        return newAccount;
    }

    private static async Task<Guid?> ResolveGovernmentBankBuildingIdAsync(
        AppDbContext db,
        Guid companyId,
        string normalizedCurrencyCode,
        CancellationToken cancellationToken)
    {
        // Prefer government bank in the same city currency where the company already operates.
        var companyCityIdsInCurrency = await db.Buildings
            .AsNoTracking()
            .Where(building => building.CompanyId == companyId)
            .Join(
                db.Cities.AsNoTracking(),
                building => building.CityId,
                city => city.Id,
                (building, city) => new
                {
                    building.CityId,
                    CurrencyCode = city.CurrencyCode,
                    building.BuiltAtUtc,
                    building.Id,
                })
            .Where(x => x.CurrencyCode != null && x.CurrencyCode.ToUpper() == normalizedCurrencyCode)
            .OrderBy(x => x.BuiltAtUtc)
            .ThenBy(x => x.Id)
            .Select(x => x.CityId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (companyCityIdsInCurrency.Count > 0)
        {
            var governmentBankInCompanyCity = await db.Buildings
                .AsNoTracking()
                .Where(building => building.IsGovernmentOwned
                    && building.Type == BuildingType.Bank
                    && companyCityIdsInCurrency.Contains(building.CityId))
                .OrderBy(building => building.CityId)
                .Select(building => (Guid?)building.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (governmentBankInCompanyCity.HasValue)
            {
                return governmentBankInCompanyCity.Value;
            }
        }

        // Fallback: pick any government bank that serves this currency.
        return await db.Buildings
            .AsNoTracking()
            .Join(
                db.Cities.AsNoTracking(),
                building => building.CityId,
                city => city.Id,
                (building, city) => new
                {
                    BuildingId = building.Id,
                    building.IsGovernmentOwned,
                    building.Type,
                    CurrencyCode = city.CurrencyCode,
                    city.Name,
                })
            .Where(x => x.IsGovernmentOwned
                && x.Type == BuildingType.Bank
                && x.CurrencyCode != null
                && x.CurrencyCode.ToUpper() == normalizedCurrencyCode)
            .OrderBy(x => x.Name)
            .Select(x => (Guid?)x.BuildingId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string GenerateRandomAccountNumber()
    {
        var bytes = RandomNumberGenerator.GetBytes(8);
        var value = BitConverter.ToUInt64(bytes, 0);
        return (value % 10_000_000_000_000_000UL).ToString("D16");
    }
}
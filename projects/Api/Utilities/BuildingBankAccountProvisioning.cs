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
                var bankAccountIdText = building.BankAccountId.Value.ToString("D");
                persistedAssignedAccount = await db.BankAccounts
                    .FromSqlInterpolated(
                        $"""
                        SELECT *
                        FROM "BankAccounts"
                        WHERE "Id"::text = {bankAccountIdText}
                        LIMIT 1
                        """)
                    .FirstOrDefaultAsync(cancellationToken);
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
            var companyIdText = companyId.ToString("D");
            existingAccount = await db.BankAccounts
                .FromSqlInterpolated(
                    $"""
                    SELECT *
                    FROM "BankAccounts"
                    WHERE "CompanyId" IS NOT NULL
                      AND "CompanyId"::text = {companyIdText}
                      AND UPPER("CurrencyCode") = {normalizedCurrencyCode}
                    ORDER BY "CreatedAtUtc" ASC
                    LIMIT 1
                    """)
                .FirstOrDefaultAsync(cancellationToken);
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

        var newAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = GenerateRandomAccountNumber(),
            CurrencyCode = normalizedCurrencyCode,
            Balance = 0m,
            CompanyId = companyId,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.BankAccounts.Add(newAccount);
        return newAccount;
    }

    private static string GenerateRandomAccountNumber()
    {
        var bytes = RandomNumberGenerator.GetBytes(8);
        var value = BitConverter.ToUInt64(bytes, 0);
        return (value % 10_000_000_000_000_000UL).ToString("D16");
    }
}
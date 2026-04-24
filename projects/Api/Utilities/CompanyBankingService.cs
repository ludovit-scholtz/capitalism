using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Utilities;

public static class CompanyBankingService
{
    public static decimal GetTotalBalance(IEnumerable<BankAccount> accounts)
        => GetActiveAccounts(accounts).Sum(account => account.Balance);

    public static decimal GetAvailableBalance(
        IEnumerable<BankAccount> accounts,
        string? currencyCode = null,
        Guid? excludeAccountId = null)
        => GetCandidateAccounts(accounts, currencyCode, excludeAccountId).Sum(account => account.Balance);

    public static decimal GetCurrencyBalance(IEnumerable<BankAccount> accounts, string currencyCode)
        => GetActiveAccounts(accounts)
            .Where(account => string.Equals(account.CurrencyCode, currencyCode, StringComparison.OrdinalIgnoreCase))
            .Sum(account => account.Balance);

    public static decimal GetTotalBalance(Company company)
        => GetTotalBalance(company.BankAccounts);

    public static BankAccount? FindPreferredAccount(
        IEnumerable<BankAccount> accounts,
        string currencyCode,
        Guid? excludeAccountId = null)
    {
        return GetCandidateAccounts(accounts, currencyCode, excludeAccountId).FirstOrDefault();
    }

    public static BankAccount? FindAnyPreferredAccount(
        IEnumerable<BankAccount> accounts,
        Guid? excludeAccountId = null)
    {
        return GetCandidateAccounts(accounts, null, excludeAccountId).FirstOrDefault();
    }

    public static bool TryDebit(
        IEnumerable<BankAccount> accounts,
        decimal amount,
        string? currencyCode = null,
        Guid? excludeAccountId = null)
    {
        if (amount <= 0m)
        {
            return true;
        }

        if (GetAvailableBalance(accounts, currencyCode, excludeAccountId) < amount)
        {
            return false;
        }

        var remaining = amount;
        foreach (var account in GetCandidateAccounts(accounts, currencyCode, excludeAccountId))
        {
            if (remaining <= 0m)
            {
                break;
            }

            var debit = Math.Min(account.Balance, remaining);
            if (debit <= 0m)
            {
                continue;
            }

            account.Balance -= debit;
            remaining -= debit;
        }

        return remaining <= 0m;
    }

    public static bool TryCredit(
        IEnumerable<BankAccount> accounts,
        decimal amount,
        string? currencyCode,
        out BankAccount? creditedAccount,
        Guid? excludeAccountId = null)
    {
        if (amount <= 0m)
        {
            creditedAccount = null;
            return true;
        }

        creditedAccount = currencyCode is not null
            ? FindPreferredAccount(accounts, currencyCode, excludeAccountId)
            : FindAnyPreferredAccount(accounts, excludeAccountId);

        if (creditedAccount is null)
        {
            return false;
        }

        creditedAccount.Balance += amount;
        return true;
    }

    public static async Task<decimal> GetTotalBalanceAsync(
        AppDbContext db,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        return await db.BankAccounts
            .Where(account => account.CompanyId == companyId && account.ClosedAtUtc == null)
            .SumAsync(account => account.Balance, cancellationToken);
    }

    public static async Task<decimal> GetCurrencyBalanceAsync(
        AppDbContext db,
        Guid companyId,
        string currencyCode,
        CancellationToken cancellationToken = default)
    {
        return await db.BankAccounts
            .Where(account => account.CompanyId == companyId
                && account.ClosedAtUtc == null
                && account.CurrencyCode == currencyCode)
            .SumAsync(account => account.Balance, cancellationToken);
    }

    public static async Task<BankAccount?> FindPreferredAccountAsync(
        AppDbContext db,
        Guid companyId,
        string currencyCode,
        Guid? excludeAccountId = null,
        CancellationToken cancellationToken = default)
    {
        return await db.BankAccounts
            .Where(account => account.CompanyId == companyId
                && account.ClosedAtUtc == null
                && account.CurrencyCode == currencyCode
                && (!excludeAccountId.HasValue || account.Id != excludeAccountId.Value))
            .OrderBy(account => account.IsBaseCapitalDeposit)
            .ThenBy(account => account.BankBuildingId.HasValue)
            .ThenByDescending(account => account.Balance)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public static async Task<BankAccount> EnsurePreferredAccountAsync(
        AppDbContext db,
        Guid companyId,
        string currencyCode,
        CancellationToken cancellationToken = default)
    {
        var account = await FindPreferredAccountAsync(db, companyId, currencyCode, cancellationToken: cancellationToken);
        if (account is not null)
        {
            return account;
        }

        return await BuildingBankAccountProvisioning.EnsureCompanyCurrencyAccountAsync(
            db,
            companyId,
            currencyCode,
            cancellationToken);
    }

    private static IEnumerable<BankAccount> GetActiveAccounts(IEnumerable<BankAccount> accounts)
        => accounts.Where(account => account.ClosedAtUtc == null);

    private static IEnumerable<BankAccount> GetCandidateAccounts(
        IEnumerable<BankAccount> accounts,
        string? currencyCode,
        Guid? excludeAccountId)
    {
        var activeAccounts = GetActiveAccounts(accounts)
            .Where(account => !excludeAccountId.HasValue || account.Id != excludeAccountId.Value);

        if (!string.IsNullOrWhiteSpace(currencyCode))
        {
            activeAccounts = activeAccounts.Where(account => string.Equals(account.CurrencyCode, currencyCode, StringComparison.OrdinalIgnoreCase));
        }

        return activeAccounts
            .OrderBy(account => account.IsBaseCapitalDeposit)
            .ThenBy(account => account.BankBuildingId.HasValue)
            .ThenByDescending(account => account.Balance);
    }
}
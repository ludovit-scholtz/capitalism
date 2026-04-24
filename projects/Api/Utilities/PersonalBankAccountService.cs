using System.Security.Cryptography;
using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Utilities;

public static class PersonalBankAccountService
{
    public const string SettlementCurrencyCode = "EUR";

    public static Task<BankAccount> EnsureTrackedSettlementAccountAsync(
        AppDbContext db,
        Player player,
        CancellationToken cancellationToken = default)
        => EnsureTrackedSettlementAccountAsync(db, player, 0m, cancellationToken);

    public static async Task<BankAccount> EnsureTrackedSettlementAccountAsync(
        AppDbContext db,
        Player player,
        decimal openingBalance,
        CancellationToken cancellationToken = default)
    {
        var existingAccount = await db.BankAccounts
            .FirstOrDefaultAsync(
                account => account.PlayerId == player.Id && account.CurrencyCode == SettlementCurrencyCode,
                cancellationToken);

        if (existingAccount is not null)
        {
            return existingAccount;
        }

        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = GenerateRandomAccountNumber(),
            CurrencyCode = SettlementCurrencyCode,
            Balance = openingBalance,
            PlayerId = player.Id,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.BankAccounts.Add(account);
        return account;
    }

    public static async Task<decimal> GetGrossCashAsync(
        AppDbContext db,
        Guid playerId,
        CancellationToken cancellationToken = default)
        => await db.BankAccounts
            .AsNoTracking()
            .Where(account => account.PlayerId == playerId && account.CurrencyCode == SettlementCurrencyCode)
            .Select(account => (decimal?)account.Balance)
            .FirstOrDefaultAsync(cancellationToken) ?? 0m;

    public static async Task<decimal> GetGrossCashAsync(
        AppDbContext db,
        Player player,
        CancellationToken cancellationToken = default)
    {
        var settlementAccount = await db.BankAccounts
            .FirstOrDefaultAsync(
                account => account.PlayerId == player.Id && account.CurrencyCode == SettlementCurrencyCode,
                cancellationToken);

        return settlementAccount?.Balance ?? 0m;
    }

    public static decimal GetGrossCash(Player player, IReadOnlyDictionary<Guid, decimal> settlementBalancesByPlayerId)
        => settlementBalancesByPlayerId.TryGetValue(player.Id, out var balance) ? balance : 0m;

    public static async Task<decimal> GetAvailableCashAsync(
        AppDbContext db,
        Player player,
        CancellationToken cancellationToken = default)
        => await GetGrossCashAsync(db, player, cancellationToken) - player.PersonalTaxReserve;

    public static async Task<decimal> DebitTrackedGrossCashAsync(
        AppDbContext db,
        Player player,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var settlementAccount = await EnsureTrackedSettlementAccountAsync(db, player, cancellationToken);
        settlementAccount.Balance -= amount;
        return settlementAccount.Balance;
    }

    public static async Task<decimal> CreditTrackedGrossCashAsync(
        AppDbContext db,
        Player player,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var settlementAccount = await EnsureTrackedSettlementAccountAsync(db, player, cancellationToken);
        settlementAccount.Balance += amount;
        return settlementAccount.Balance;
    }

    public static async Task<decimal> SetTrackedGrossCashAsync(
        AppDbContext db,
        Player player,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var settlementAccount = await EnsureTrackedSettlementAccountAsync(db, player, cancellationToken);
        settlementAccount.Balance = amount;
        return settlementAccount.Balance;
    }

    public static async Task<Dictionary<Guid, decimal>> GetSettlementBalancesByPlayerIdAsync(
        AppDbContext db,
        IEnumerable<Guid> playerIds,
        CancellationToken cancellationToken = default)
    {
        var distinctPlayerIds = playerIds.Distinct().ToList();
        if (distinctPlayerIds.Count == 0)
        {
            return [];
        }

        var balances = distinctPlayerIds.ToDictionary(playerId => playerId, _ => 0m);

        var settlementAccounts = await db.BankAccounts
            .AsNoTracking()
            .Where(account => account.PlayerId.HasValue
                && distinctPlayerIds.Contains(account.PlayerId.Value)
                && account.CurrencyCode == SettlementCurrencyCode)
            .Select(account => new { PlayerId = account.PlayerId!.Value, account.Balance })
            .ToListAsync(cancellationToken);

        foreach (var account in settlementAccounts)
        {
            balances[account.PlayerId] = account.Balance;
        }

        return balances;
    }

    private static string GenerateRandomAccountNumber()
    {
        var bytes = RandomNumberGenerator.GetBytes(8);
        var value = BitConverter.ToUInt64(bytes, 0);
        return (value % 10_000_000_000_000_000UL).ToString("D16");
    }
}
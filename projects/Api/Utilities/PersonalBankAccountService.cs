using System.Security.Cryptography;
using Api.Data;
using Api.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Api.Utilities;

public static class PersonalBankAccountService
{
    public const string SettlementCurrencyCode = "EUR";

    public static async Task<BankAccount?> GetTrackedAccountAsync(
        AppDbContext db,
        Guid playerId,
        string currencyCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCurrencyCode = currencyCode.ToUpperInvariant();

        var tracked = db.BankAccounts.Local.FirstOrDefault(
            account => account.PlayerId == playerId
                && string.Equals(account.CurrencyCode, normalizedCurrencyCode, StringComparison.OrdinalIgnoreCase));

        if (tracked is not null)
        {
            return tracked;
        }

        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            if (command.Connection!.State != System.Data.ConnectionState.Open)
            {
                await command.Connection.OpenAsync(cancellationToken);
            }

            command.CommandText =
                """
                SELECT "Id", "AccountNumber", "CurrencyCode", "Balance", "PlayerId", "IsGovernmentAccount", "CreatedAtUtc"
                FROM "BankAccounts"
                WHERE "PlayerId" = @playerId AND "CurrencyCode" = @currencyCode
                LIMIT 1
                """;

            var playerIdParameter = command.CreateParameter();
            playerIdParameter.ParameterName = "@playerId";
            playerIdParameter.Value = playerId;
            command.Parameters.Add(playerIdParameter);

            var currencyCodeParameter = command.CreateParameter();
            currencyCodeParameter.ParameterName = "@currencyCode";
            currencyCodeParameter.Value = normalizedCurrencyCode;
            command.Parameters.Add(currencyCodeParameter);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            var account = new BankAccount
            {
                Id = reader.GetGuid(0),
                AccountNumber = reader.GetString(1),
                CurrencyCode = reader.GetString(2),
                Balance = reader.GetDecimal(3),
                PlayerId = reader.IsDBNull(4) ? null : reader.GetGuid(4),
                IsGovernmentAccount = !reader.IsDBNull(5) && reader.GetBoolean(5),
                CreatedAtUtc = reader.IsDBNull(6) ? DateTime.UtcNow : reader.GetDateTime(6),
            };

            db.Attach(account);
            return account;
        }
        catch (SqliteException)
        {
            // Legacy bootstrap databases may have older BankAccounts shape or no table yet.
            return null;
        }
    }

    public static Task<BankAccount> EnsureTrackedAccountAsync(
        AppDbContext db,
        Guid playerId,
        string currencyCode,
        CancellationToken cancellationToken = default)
        => EnsureTrackedAccountAsync(db, playerId, currencyCode, 0m, cancellationToken);

    public static async Task<BankAccount> EnsureTrackedAccountAsync(
        AppDbContext db,
        Guid playerId,
        string currencyCode,
        decimal openingBalance,
        CancellationToken cancellationToken = default)
    {
        var normalizedCurrencyCode = currencyCode.ToUpperInvariant();
        var existingAccount = await GetTrackedAccountAsync(db, playerId, normalizedCurrencyCode, cancellationToken);

        if (existingAccount is not null)
        {
            return existingAccount;
        }

        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = GenerateRandomAccountNumber(),
            CurrencyCode = normalizedCurrencyCode,
            Balance = openingBalance,
            PlayerId = playerId,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.BankAccounts.Add(account);
        return account;
    }

    public static async Task<decimal> GetTrackedBalanceAsync(
        AppDbContext db,
        Guid playerId,
        string currencyCode,
        CancellationToken cancellationToken = default)
        => await db.BankAccounts
            .AsNoTracking()
            .Where(account => account.PlayerId == playerId && account.CurrencyCode == currencyCode.ToUpperInvariant())
            .Select(account => (decimal?)account.Balance)
            .FirstOrDefaultAsync(cancellationToken) ?? 0m;

    public static async Task<decimal> DebitTrackedBalanceAsync(
        AppDbContext db,
        Guid playerId,
        string currencyCode,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var account = await EnsureTrackedAccountAsync(db, playerId, currencyCode, cancellationToken);
        account.Balance -= amount;
        return account.Balance;
    }

    public static async Task<decimal> CreditTrackedBalanceAsync(
        AppDbContext db,
        Guid playerId,
        string currencyCode,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var account = await EnsureTrackedAccountAsync(db, playerId, currencyCode, cancellationToken);
        account.Balance += amount;
        return account.Balance;
    }

    public static Task<BankAccount> EnsureTrackedSettlementAccountAsync(
        AppDbContext db,
        Player player,
        CancellationToken cancellationToken = default)
        => EnsureTrackedSettlementAccountAsync(db, player, 0m, cancellationToken);

    public static Task<BankAccount> EnsureTrackedSettlementAccountAsync(
        AppDbContext db,
        Player player,
        decimal openingBalance,
        CancellationToken cancellationToken = default)
        => EnsureTrackedAccountAsync(db, player.Id, SettlementCurrencyCode, openingBalance, cancellationToken);

    public static async Task<decimal> GetGrossCashAsync(
        AppDbContext db,
        Guid playerId,
        CancellationToken cancellationToken = default)
        => await GetTrackedBalanceAsync(db, playerId, SettlementCurrencyCode, cancellationToken);

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
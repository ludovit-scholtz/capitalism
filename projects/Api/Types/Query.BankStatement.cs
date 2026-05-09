using Api.Data;
using Api.Data.Entities;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

/// <summary>
/// Bank statement query — returns company ledger entries formatted as a bank statement
/// with running balance for display in the Bank Statement Review UI.
/// </summary>
public sealed partial class Query
{
    private const int BankStatementDefaultLimit = 50;
    private const int BankStatementMaxLimit = 200;

    /// <summary>
    /// Returns a bank statement for a company owned by the authenticated player
    /// or for the authenticated player's personal account when accountId points to a personal account.
    /// Entries are ordered newest-first with a computed running balance column.
    /// </summary>
    [Authorize]
    public async Task<BankStatementResult> GetBankStatement(
        Guid? companyId,
        int? limit,
        int? offset,
        Guid? accountId,
        long? fromTick,
        long? toTick,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var pageSize = Math.Clamp(limit ?? BankStatementDefaultLimit, 1, BankStatementMaxLimit);
        var pageOffset = Math.Max(offset ?? 0, 0);

        if (accountId.HasValue)
        {
            var personalAccount = await db.BankAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(account => account.Id == accountId.Value && account.PlayerId == userId && account.CompanyId == null);

            if (personalAccount is not null)
            {
                return await BuildPersonalBankStatementAsync(
                    db,
                    userId,
                    personalAccount,
                    pageSize,
                    pageOffset,
                    fromTick,
                    toTick);
            }
        }

        if (!companyId.HasValue)
        {
            throw new GraphQLException(new Error("Company not found or you do not own it.", "COMPANY_NOT_FOUND"));
        }

        var company = await db.Companies
            .AsNoTracking()
            .Include(c => c.Buildings).ThenInclude(b => b.City)
            .FirstOrDefaultAsync(c => c.Id == companyId.Value && c.PlayerId == userId)
            ?? throw new GraphQLException(new Error("Company not found or you do not own it.", "COMPANY_NOT_FOUND"));

        string? filterCurrencyCode = null;
        BankAccount? selectedAccount = null;
        if (accountId.HasValue)
        {
            selectedAccount = await db.BankAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == accountId.Value && a.CompanyId == companyId.Value);
            if (selectedAccount is null)
            {
                throw new GraphQLException(new Error("Selected bank account not found for this company.", "ACCOUNT_NOT_FOUND"));
            }

            filterCurrencyCode = selectedAccount.CurrencyCode;
        }

        // Load entries for the company or for one specific bank account.
        var entriesQuery = db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId.Value);
        if (accountId.HasValue)
            entriesQuery = entriesQuery.Where(e => e.BankAccountId == accountId.Value);
        if (fromTick.HasValue)
            entriesQuery = entriesQuery.Where(e => e.RecordedAtTick >= fromTick.Value);
        if (toTick.HasValue)
            entriesQuery = entriesQuery.Where(e => e.RecordedAtTick <= toTick.Value);

        var allEntries = await entriesQuery
            .Include(e => e.Building)
            .OrderBy(e => e.RecordedAtTick)
            .ThenBy(e => e.RecordedAtUtc)
            .ToListAsync();

        var ledgerNetBalance = allEntries.Sum(e => e.Amount);
        var hasLegacyUnscopedEntries = !accountId.HasValue && allEntries.Any(e => e.BankAccountId is null);

        // Use authoritative account balances when accounts exist.
        // Legacy/test data may not seed company bank accounts yet, so fall back to
        // the ledger-derived net balance to keep statements stable for those cases.
        var activeCompanyAccounts = selectedAccount is null
            ? await db.BankAccounts
                .AsNoTracking()
                .Where(a => a.CompanyId == companyId.Value && a.ClosedAtUtc == null)
                .Select(a => a.Balance)
                .ToListAsync()
            : null;

        var currentBalance = selectedAccount is not null
            ? selectedAccount.Balance
            : hasLegacyUnscopedEntries
                ? ledgerNetBalance
            : activeCompanyAccounts!.Count > 0
                ? activeCompanyAccounts.Sum()
                : ledgerNetBalance;

        // Compute running balances in chronological order, anchored to current balance.
        var openingBalance = currentBalance - ledgerNetBalance;
        decimal runningBalance = openingBalance;
        var balanceMap = new Dictionary<Guid, decimal>(allEntries.Count);
        foreach (var entry in allEntries)
        {
            runningBalance += entry.Amount;
            balanceMap[entry.Id] = runningBalance;
        }

        // Take the requested page (newest first for display) and map to result rows.
        var pagedEntries = allEntries
            .OrderByDescending(e => e.RecordedAtTick)
            .ThenByDescending(e => e.RecordedAtUtc)
            .Skip(pageOffset)
            .Take(pageSize)
            .ToList();

        var rows = pagedEntries.Select(e => new BankStatementRow
        {
            Id = e.Id,
            RecordedAtTick = e.RecordedAtTick,
            RecordedAtUtc = e.RecordedAtUtc,
            Description = e.Description,
            Category = e.Category,
            Amount = e.Amount,
            RunningBalance = balanceMap.TryGetValue(e.Id, out var bal) ? bal : 0m,
            BuildingId = e.BuildingId,
            BuildingName = e.Building?.Name,
        }).ToList();

        // Primary currency: prefer the filtered account's currency, then first building city, then EUR.
        var primaryCurrencyCode = filterCurrencyCode
            ?? company.Buildings
                .Select(b => b.City?.CurrencyCode)
                .FirstOrDefault(c => !string.IsNullOrEmpty(c))
            ?? "EUR";

        return new BankStatementResult
        {
            CompanyId = company.Id,
            CompanyName = company.Name,
            CurrencyCode = primaryCurrencyCode,
            CurrencySymbol = Mutation.GetCurrencySymbol(primaryCurrencyCode),
            CurrentBalance = currentBalance,
            TotalEntries = allEntries.Count,
            Rows = rows,
        };
    }

    private static async Task<BankStatementResult> BuildPersonalBankStatementAsync(
        AppDbContext db,
        Guid playerId,
        BankAccount personalAccount,
        int pageSize,
        int pageOffset,
        long? fromTick,
        long? toTick)
    {
        var founderEntries = await db.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.Category == LedgerCategory.FounderContribution && entry.Company.PlayerId == playerId)
            .OrderBy(entry => entry.RecordedAtTick)
            .ThenBy(entry => entry.RecordedAtUtc)
            .ToListAsync();
        var dividendPayments = await db.DividendPayments
            .AsNoTracking()
            .Where(payment => payment.RecipientPlayerId == playerId)
            .OrderBy(payment => payment.RecordedAtTick)
            .ThenBy(payment => payment.RecordedAtUtc)
            .ToListAsync();

        var movementEntries = new List<BankStatementRow>();

        if (string.Equals(personalAccount.CurrencyCode, "USD", StringComparison.OrdinalIgnoreCase))
        {
            movementEntries.Add(new BankStatementRow
            {
                Id = Guid.NewGuid(),
                RecordedAtTick = 0,
                RecordedAtUtc = personalAccount.CreatedAtUtc,
                Description = "Government starter funding deposit",
                Category = LedgerCategory.BankAccountTransferIn,
                Amount = 200_000m,
                RunningBalance = 0m,
            });
        }

        foreach (var founderEntry in founderEntries)
        {
            movementEntries.Add(new BankStatementRow
            {
                Id = Guid.NewGuid(),
                RecordedAtTick = founderEntry.RecordedAtTick,
                RecordedAtUtc = founderEntry.RecordedAtUtc,
                Description = string.IsNullOrWhiteSpace(founderEntry.Description)
                    ? "Founder contribution to starter company (USD converted to city currency)"
                    : founderEntry.Description,
                Category = LedgerCategory.FounderContribution,
                Amount = -200_000m,
                RunningBalance = 0m,
            });
        }

        foreach (var payment in dividendPayments)
        {
            movementEntries.Add(new BankStatementRow
            {
                Id = Guid.NewGuid(),
                RecordedAtTick = payment.RecordedAtTick,
                RecordedAtUtc = payment.RecordedAtUtc,
                Description = string.IsNullOrWhiteSpace(payment.Description)
                    ? "Dividend payout"
                    : payment.Description,
                Category = LedgerCategory.Dividend,
                Amount = payment.TotalAmount,
                RunningBalance = 0m,
            });
        }

        var filteredEntries = movementEntries
            .Where(entry => !fromTick.HasValue || entry.RecordedAtTick >= fromTick.Value)
            .Where(entry => !toTick.HasValue || entry.RecordedAtTick <= toTick.Value)
            .OrderBy(entry => entry.RecordedAtTick)
            .ThenBy(entry => entry.RecordedAtUtc)
            .ToList();

        var movementNetBalance = filteredEntries.Sum(entry => entry.Amount);
        var openingBalance = personalAccount.Balance - movementNetBalance;
        var runningBalance = openingBalance;

        foreach (var entry in filteredEntries)
        {
            runningBalance += entry.Amount;
            entry.RunningBalance = runningBalance;
        }

        var rows = filteredEntries
            .OrderByDescending(entry => entry.RecordedAtTick)
            .ThenByDescending(entry => entry.RecordedAtUtc)
            .Skip(pageOffset)
            .Take(pageSize)
            .ToList();

        return new BankStatementResult
        {
            CompanyId = Guid.Empty,
            CompanyName = "Personal Account",
            CurrencyCode = personalAccount.CurrencyCode,
            CurrencySymbol = Mutation.GetCurrencySymbol(personalAccount.CurrencyCode),
            CurrentBalance = personalAccount.Balance,
            TotalEntries = filteredEntries.Count,
            Rows = rows,
        };
    }
}

// ── Result types ─────────────────────────────────────────────────────────────

/// <summary>Top-level bank statement result for one company.</summary>
public sealed class BankStatementResult
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    /// <summary>ISO 4217 currency code inferred from the company's city.</summary>
    public string CurrencyCode { get; set; } = "EUR";
    /// <summary>Display symbol for the currency (e.g. "€", "Kč").</summary>
    public string CurrencySymbol { get; set; } = "€";
    /// <summary>Current net balance computed from all ledger entries.</summary>
    public decimal CurrentBalance { get; set; }
    /// <summary>Total number of ledger entries for this company (before page limit).</summary>
    public int TotalEntries { get; set; }
    public List<BankStatementRow> Rows { get; set; } = [];
}

/// <summary>A single row in the bank statement (corresponds to one LedgerEntry).</summary>
public sealed class BankStatementRow
{
    public Guid Id { get; set; }
    public long RecordedAtTick { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    /// <summary>Positive = credit (income), negative = debit (expense).</summary>
    public decimal Amount { get; set; }
    /// <summary>Running account balance after this entry was applied.</summary>
    public decimal RunningBalance { get; set; }
    public Guid? BuildingId { get; set; }
    public string? BuildingName { get; set; }
}

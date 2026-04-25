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
    /// Returns a bank statement for a company owned by the authenticated player.
    /// Entries are ordered newest-first with a computed running balance column.
    /// </summary>
    [Authorize]
    public async Task<BankStatementResult> GetBankStatement(
        Guid companyId,
        int? limit,
        int? offset,
        Guid? accountId,
        long? fromTick,
        long? toTick,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var company = await db.Companies
            .AsNoTracking()
            .Include(c => c.Buildings).ThenInclude(b => b.City)
            .FirstOrDefaultAsync(c => c.Id == companyId && c.PlayerId == userId)
            ?? throw new GraphQLException(new Error("Company not found or you do not own it.", "COMPANY_NOT_FOUND"));

        var pageSize = Math.Clamp(limit ?? BankStatementDefaultLimit, 1, BankStatementMaxLimit);
        var pageOffset = Math.Max(offset ?? 0, 0);

        // Resolve the account filter: find the building (if any) linked to this account.
        Guid? filterBuildingId = null;
        string? filterCurrencyCode = null;
        if (accountId.HasValue)
        {
            // Look up the account to get its currency and owning building.
            var acct = await db.BankAccounts
                .AsNoTracking()
                .Include(a => a.BankBuilding).ThenInclude(b => b!.City)
                .FirstOrDefaultAsync(a => a.Id == accountId.Value && a.CompanyId == companyId);
            if (acct != null)
            {
                filterCurrencyCode = acct.CurrencyCode;
                // If the account is the primary account for a specific building, scope to that building.
                var owningBuilding = company.Buildings.FirstOrDefault(b => b.BankAccountId == accountId.Value);
                if (owningBuilding != null)
                    filterBuildingId = owningBuilding.Id;
            }
        }

        // Load entries: filter by building when available, otherwise by company.
        var entriesQuery = db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId);
        if (filterBuildingId.HasValue)
            entriesQuery = entriesQuery.Where(e => e.BuildingId == filterBuildingId.Value);
        if (fromTick.HasValue)
            entriesQuery = entriesQuery.Where(e => e.RecordedAtTick >= fromTick.Value);
        if (toTick.HasValue)
            entriesQuery = entriesQuery.Where(e => e.RecordedAtTick <= toTick.Value);

        var allEntries = await entriesQuery
            .Include(e => e.Building)
            .OrderBy(e => e.RecordedAtTick)
            .ThenBy(e => e.RecordedAtUtc)
            .ToListAsync();

        // Compute running balance for each entry (cumulative sum in chronological order).
        decimal runningBalance = 0m;
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

        var currentBalance = allEntries.Sum(e => e.Amount);

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

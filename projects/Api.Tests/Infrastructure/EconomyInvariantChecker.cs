using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Point-in-time view of each company's total bank balance and the ledger rows that already existed,
/// used to check conservation between checkpoints.
/// </summary>
internal sealed record EconomySnapshot(
    IReadOnlyDictionary<Guid, decimal> CompanyBalances,
    HashSet<Guid> KnownLedgerEntryIds,
    IReadOnlyDictionary<Guid, decimal> PersonalAccountBalances);

/// <summary>
/// Economy-wide invariants asserted by <see cref="EconomyInvariantTests"/>. Each check returns
/// human-readable violations (offending entity ids included) so a failing seed is diagnosable.
/// </summary>
internal static class EconomyInvariantChecker
{
    public static async Task<EconomySnapshot> TakeSnapshotAsync(AppDbContext db)
    {
        var balances = await db.BankAccounts.AsNoTracking()
            .Where(a => a.CompanyId != null)
            .GroupBy(a => a.CompanyId!.Value)
            .Select(g => new { g.Key, Total = g.Sum(a => a.Balance) })
            .ToListAsync();
        var ledgerIds = await db.LedgerEntries.AsNoTracking().Select(e => e.Id).ToListAsync();
        var personal = await db.BankAccounts.AsNoTracking()
            .Where(a => a.CompanyId == null && a.PlayerId != null && !a.IsGovernmentAccount)
            .Select(a => new { a.Id, a.Balance })
            .ToListAsync();
        return new EconomySnapshot(balances.ToDictionary(t => t.Key, t => t.Total), ledgerIds.ToHashSet(), personal.ToDictionary(a => a.Id, a => a.Balance));
    }

    /// <summary>Runs every invariant; <paramref name="baseline"/> enables the conservation check.</summary>
    public static async Task<List<string>> CheckAllAsync(AppDbContext db, EconomySnapshot? baseline = null)
    {
        var violations = new List<string>();
        violations.AddRange(await CheckNoNegativeBalancesAsync(db));
        violations.AddRange(await CheckBuildingOwnershipAsync(db));
        violations.AddRange(await CheckLedgerMatchedAsync(db));
        if (baseline is not null)
        {
            violations.AddRange(await CheckMoneyConservationAsync(db, baseline));
            violations.AddRange(await CheckPersonalAccountsUntouchedAsync(db, baseline));
        }

        return violations;
    }

    public static async Task<List<string>> CheckNoNegativeBalancesAsync(AppDbContext db)
    {
        var negative = await db.BankAccounts.AsNoTracking()
            .Where(a => !a.IsGovernmentAccount && a.Balance < 0m)
            .Select(a => new { a.Id, a.Balance, a.CurrencyCode })
            .ToListAsync();
        return negative.Select(a => $"NEGATIVE_BALANCE account={a.Id} balance={a.Balance} {a.CurrencyCode}").ToList();
    }

    /// <summary>Every building has exactly one existing owning company (no orphan / ownerless buildings).</summary>
    public static async Task<List<string>> CheckBuildingOwnershipAsync(AppDbContext db)
    {
        var companyIds = (await db.Companies.AsNoTracking().Select(c => c.Id).ToListAsync()).ToHashSet();
        var buildings = await db.Buildings.AsNoTracking().Select(b => new { b.Id, b.CompanyId }).ToListAsync();
        var violations = buildings
            .Where(b => !companyIds.Contains(b.CompanyId))
            .Select(b => $"ORPHAN_BUILDING building={b.Id} company={b.CompanyId}")
            .ToList();
        // The owning company recorded on the lot must be the building's company, and no building may sit on two lots.
        var buildingOwners = buildings.ToDictionary(b => b.Id, b => b.CompanyId);
        var lots = await db.BuildingLots.AsNoTracking()
            .Where(l => l.BuildingId != null)
            .Select(l => new { l.Id, BuildingId = l.BuildingId!.Value, l.OwnerCompanyId })
            .ToListAsync();
        violations.AddRange(lots
            .Where(l => buildingOwners.TryGetValue(l.BuildingId, out var owner) && l.OwnerCompanyId != owner)
            .Select(l => $"MULTI_OWNER_BUILDING building={l.BuildingId} lot={l.Id} lotOwner={l.OwnerCompanyId} buildingOwner={buildingOwners[l.BuildingId]}"));
        violations.AddRange(lots
            .GroupBy(l => l.BuildingId)
            .Where(g => g.Count() > 1)
            .Select(g => $"MULTI_OWNER_BUILDING building={g.Key} sits on {g.Count()} lots"));
        return violations;
    }

    /// <summary>Every ledger entry that names a bank account points at an existing account of the same company.</summary>
    public static async Task<List<string>> CheckLedgerMatchedAsync(AppDbContext db)
    {
        var accounts = await db.BankAccounts.AsNoTracking()
            .Select(a => new { a.Id, a.CompanyId })
            .ToDictionaryAsync(a => a.Id, a => a.CompanyId);
        var entries = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.BankAccountId != null)
            .Select(e => new { e.Id, e.CompanyId, BankAccountId = e.BankAccountId!.Value })
            .ToListAsync();
        var violations = new List<string>();
        foreach (var entry in entries)
        {
            if (!accounts.TryGetValue(entry.BankAccountId, out var accountCompany))
            {
                violations.Add($"UNMATCHED_LEDGER entry={entry.Id} missing account={entry.BankAccountId}");
            }
            else if (accountCompany.HasValue && accountCompany.Value != entry.CompanyId)
            {
                violations.Add($"UNMATCHED_LEDGER entry={entry.Id} company={entry.CompanyId} account={entry.BankAccountId} accountCompany={accountCompany}");
            }
        }

        return violations;
    }

    /// <summary>
    /// Global leak detector for the money the per-company ledger check cannot see: personal (player-owned, non-government) accounts. A personal account that changes, disappears or appears during a run is a violation.
    /// The generated actions only move company money, so no personal account may change; a change means a company flow minted or
    /// burned money into a player's pocket, or an account was created or removed behind the ledger.
    /// </summary>
    public static async Task<List<string>> CheckPersonalAccountsUntouchedAsync(AppDbContext db, EconomySnapshot baseline)
    {
        var current = await db.BankAccounts.AsNoTracking()
            .Where(a => a.CompanyId == null && a.PlayerId != null && !a.IsGovernmentAccount)
            .Select(a => new { a.Id, a.Balance })
            .ToDictionaryAsync(a => a.Id, a => a.Balance);
        var violations = new List<string>();
        violations.AddRange(current.Keys
            .Where(id => !baseline.PersonalAccountBalances.ContainsKey(id))
            .Select(id => $"PERSONAL_ACCOUNT_CREATED account={id} balance={current[id]}"));
        foreach (var (accountId, balanceBefore) in baseline.PersonalAccountBalances)
        {
            if (!current.TryGetValue(accountId, out var balanceNow))
            {
                violations.Add($"PERSONAL_ACCOUNT_REMOVED account={accountId} balanceBefore={balanceBefore}");
            }
            else if (balanceNow != balanceBefore)
            {
                violations.Add($"PERSONAL_ACCOUNT_CHANGED account={accountId} before={balanceBefore} after={balanceNow}");
            }
        }

        return violations;
    }

    /// <summary>
    /// For every company, the change in the total of its bank balances since <paramref name="baseline"/>
    /// equals the sum of its ledger entries recorded since then: no company gains or loses money
    /// without a visible bank-statement row. Only company-owned accounts are covered: player-owned and government accounts have no ledger and are not part of this check.
    /// </summary>
    public static async Task<List<string>> CheckMoneyConservationAsync(AppDbContext db, EconomySnapshot baseline)
    {
        var current = await TakeSnapshotAsync(db);
        var newEntries = await db.LedgerEntries.AsNoTracking()
            .Where(e => !baseline.KnownLedgerEntryIds.Contains(e.Id))
            .Select(e => new { e.CompanyId, e.Amount })
            .ToListAsync();
        var ledgerDelta = newEntries.GroupBy(e => e.CompanyId).ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

        var violations = new List<string>();
        foreach (var companyId in current.CompanyBalances.Keys.Union(baseline.CompanyBalances.Keys))
        {
            var before = baseline.CompanyBalances.GetValueOrDefault(companyId);
            var after = current.CompanyBalances.GetValueOrDefault(companyId);
            var expected = ledgerDelta.GetValueOrDefault(companyId);
            if (after - before != expected)
            {
                violations.Add($"MONEY_NOT_CONSERVED company={companyId} balanceDelta={after - before} ledgerDelta={expected}");
            }
        }

        return violations;
    }
}

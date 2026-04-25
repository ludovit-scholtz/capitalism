using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Api.Data;

public sealed partial class AppDbContext
{
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureLedgerEntryBankAccountIdsAsync(CancellationToken.None).GetAwaiter().GetResult();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => SaveChangesAsync(true, cancellationToken);

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await EnsureLedgerEntryBankAccountIdsAsync(cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private async Task EnsureLedgerEntryBankAccountIdsAsync(CancellationToken cancellationToken)
    {
        if (!Database.IsRelational())
        {
            return;
        }

        var pendingEntries = ChangeTracker
            .Entries<LedgerEntry>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .ToList();

        if (pendingEntries.Count == 0)
        {
            return;
        }

        // First pass: fill from the building's assigned bank account when available.
        var unresolved = pendingEntries
            .Where(entry => !entry.Entity.BankAccountId.HasValue)
            .ToList();

        if (unresolved.Count > 0)
        {
            var buildingIds = unresolved
                .Select(entry => entry.Entity.BuildingId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            if (buildingIds.Count > 0)
            {
                var buildingAccountLookup = await Buildings
                    .AsNoTracking()
                    .Where(building => buildingIds.Contains(building.Id) && building.BankAccountId.HasValue)
                    .Select(building => new { building.Id, building.BankAccountId })
                    .ToDictionaryAsync(item => item.Id, item => item.BankAccountId!.Value, cancellationToken);

                foreach (var entry in unresolved)
                {
                    var buildingId = entry.Entity.BuildingId;
                    if (buildingId.HasValue && buildingAccountLookup.TryGetValue(buildingId.Value, out var accountId))
                    {
                        entry.Entity.BankAccountId = accountId;
                    }
                }
            }
        }

        // Second pass: for company-level entries without building context, bind to a preferred active company account.
        unresolved = pendingEntries
            .Where(entry => !entry.Entity.BankAccountId.HasValue)
            .ToList();

        if (unresolved.Count > 0)
        {
            var companyIds = unresolved
                .Select(entry => entry.Entity.CompanyId)
                .Distinct()
                .ToList();

            var preferredAccountsByCompany = await BankAccounts
                .Where(account => account.ClosedAtUtc == null
                    && account.CompanyId.HasValue
                    && companyIds.Contains(account.CompanyId.Value))
                .OrderBy(account => account.IsBaseCapitalDeposit)
                .ThenBy(account => account.BankBuildingId.HasValue)
                .ThenBy(account => account.CreatedAtUtc)
                .Select(account => new { account.Id, CompanyId = account.CompanyId!.Value })
                .ToListAsync(cancellationToken);

            var preferredLookup = preferredAccountsByCompany
                .GroupBy(account => account.CompanyId)
                .ToDictionary(group => group.Key, group => group.First().Id);

            foreach (var entry in unresolved)
            {
                if (preferredLookup.TryGetValue(entry.Entity.CompanyId, out var accountId))
                {
                    entry.Entity.BankAccountId = accountId;
                    continue;
                }

                // Absolute fallback: provision an EUR company account when no active account exists.
                var fallbackAccount = await BuildingBankAccountProvisioning.EnsureCompanyCurrencyAccountAsync(
                    this,
                    entry.Entity.CompanyId,
                    "EUR",
                    cancellationToken);

                entry.Entity.BankAccountId = fallbackAccount.Id;
            }
        }

        // Validate that explicit assignment always belongs to the same company as the ledger row.
        var assignedAccountIds = pendingEntries
            .Select(entry => entry.Entity.BankAccountId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (assignedAccountIds.Count == 0)
        {
            return;
        }

        var accountCompanyLookup = await BankAccounts
            .Where(account => assignedAccountIds.Contains(account.Id))
            .Select(account => new { account.Id, account.CompanyId })
            .ToDictionaryAsync(item => item.Id, item => item.CompanyId, cancellationToken);

        var trackedBankAccounts = ChangeTracker
            .Entries<BankAccount>()
            .Select(entry => entry.Entity)
            .Where(account => assignedAccountIds.Contains(account.Id));

        foreach (var trackedAccount in trackedBankAccounts)
        {
            accountCompanyLookup[trackedAccount.Id] = trackedAccount.CompanyId;
        }

        foreach (var entry in pendingEntries)
        {
            if (!entry.Entity.BankAccountId.HasValue)
            {
                throw new InvalidOperationException($"LedgerEntry {entry.Entity.Id} is missing BankAccountId.");
            }

            if (!accountCompanyLookup.TryGetValue(entry.Entity.BankAccountId.Value, out var accountCompanyId)
                || accountCompanyId != entry.Entity.CompanyId)
            {
                throw new InvalidOperationException(
                    $"LedgerEntry {entry.Entity.Id} has BankAccountId {entry.Entity.BankAccountId} not owned by CompanyId {entry.Entity.CompanyId}.");
            }
        }
    }
}

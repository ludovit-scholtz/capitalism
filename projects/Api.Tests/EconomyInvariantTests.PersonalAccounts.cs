using Api.Data;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed partial class EconomyInvariantTests
{
    [Fact]
    public async Task Checker_DetectsMoneyMintedOrBurnedOnAPersonalAccount()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var personal = NewAccount(null, "EUR", 500m);
        personal.PlayerId = Guid.NewGuid();
        var gone = NewAccount(null, "EUR", 10m);
        gone.PlayerId = Guid.NewGuid();
        db.BankAccounts.AddRange(personal, gone);
        await db.SaveChangesAsync();
        var baseline = await EconomyInvariantChecker.TakeSnapshotAsync(db);
        Assert.Empty(await EconomyInvariantChecker.CheckPersonalAccountsUntouchedAsync(db, baseline));

        personal.Balance += 1m; // money appears in a player's pocket without any company ledger row
        db.BankAccounts.Remove(gone);
        await db.SaveChangesAsync();

        var violations = await EconomyInvariantChecker.CheckPersonalAccountsUntouchedAsync(db, baseline);
        Assert.Contains(violations, v => v.StartsWith("PERSONAL_ACCOUNT_CHANGED") && v.Contains(personal.Id.ToString()));
        Assert.Contains(violations, v => v.StartsWith("PERSONAL_ACCOUNT_REMOVED") && v.Contains(gone.Id.ToString()));

        var created = NewAccount(null, "EUR", 5m);
        created.PlayerId = Guid.NewGuid();
        db.BankAccounts.Add(created);
        await db.SaveChangesAsync();
        // The full invariant run (not only the single check) reports it.
        var all = await EconomyInvariantChecker.CheckAllAsync(db, baseline);
        Assert.Contains(all, v => v.StartsWith("PERSONAL_ACCOUNT_CREATED") && v.Contains(created.Id.ToString()));
    }
}

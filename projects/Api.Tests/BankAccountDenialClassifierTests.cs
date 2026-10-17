using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

public sealed class BankAccountDenialClassifierTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"bank-denial-classifier-{Guid.NewGuid():N}")
            .Options);

    private static BankAccount Account(Guid? companyId, Guid? playerId) => new()
    {
        Id = Guid.NewGuid(), CompanyId = companyId, PlayerId = playerId, CurrencyCode = "EUR", Balance = 1m,
        AccountNumber = Guid.NewGuid().ToString("N")[..16], CreatedAtUtc = DateTime.UtcNow,
    };

    [Fact]
    public async Task MissingAccount_IsNotFound()
    {
        await using var db = CreateDb();

        var reason = await BankAccountDenialClassifier.ClassifyAsync(db, Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(ObjectAuthorizationService.NotFoundReason, reason);
    }

    [Fact]
    public async Task AccountOfAnotherPlayer_IsNotOwned()
    {
        await using var db = CreateDb();
        var foreignCompany = new Company { Id = Guid.NewGuid(), PlayerId = Guid.NewGuid(), Name = "Other", FoundedAtUtc = DateTime.UtcNow };
        var foreign = Account(foreignCompany.Id, null);
        db.Companies.Add(foreignCompany);
        db.BankAccounts.Add(foreign);
        await db.SaveChangesAsync();

        var reason = await BankAccountDenialClassifier.ClassifyAsync(db, foreign.Id, Guid.NewGuid());

        Assert.Equal(ObjectAuthorizationService.NotOwnedReason, reason);
    }

    [Fact]
    public async Task OwnAccountInAnotherContext_IsWrongContextNotAProbe()
    {
        await using var db = CreateDb();
        var me = Guid.NewGuid();
        var personal = Account(null, me);
        var company = new Company { Id = Guid.NewGuid(), PlayerId = me, Name = "Mine", FoundedAtUtc = DateTime.UtcNow };
        var companyAccount = Account(company.Id, null);
        db.Companies.Add(company);
        db.BankAccounts.AddRange(personal, companyAccount);
        await db.SaveChangesAsync();

        Assert.Equal(ObjectAuthorizationService.WrongContextReason, await BankAccountDenialClassifier.ClassifyAsync(db, personal.Id, me));
        Assert.Equal(ObjectAuthorizationService.WrongContextReason, await BankAccountDenialClassifier.ClassifyAsync(db, companyAccount.Id, me));
    }
}

using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Security;
using HotChocolate;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

public sealed class BotOwnershipGuardTests
{
    [Fact]
    public async Task EnsureCompanyOwnedAsync_OwnedCompany_Succeeds()
    {
        await using var db = CreateDb(nameof(EnsureCompanyOwnedAsync_OwnedCompany_Succeeds));
        var playerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        db.Players.Add(new Player
        {
            Id = playerId,
            Email = "owner@test.com",
            DisplayName = "Owner",
            PasswordHash = "hash",
        });
        db.Companies.Add(new Company
        {
            Id = companyId,
            PlayerId = playerId,
            Name = "Owned Co",
            FoundedAtTick = 0,
        });
        await db.SaveChangesAsync();

        var guard = new BotOwnershipGuard(db);
        await guard.EnsureCompanyOwnedAsync(playerId, companyId, CancellationToken.None);
    }

    [Fact]
    public async Task EnsureCompanyOwnedAsync_ForeignCompany_ThrowsNotOwnedOrNotFound()
    {
        await using var db = CreateDb(nameof(EnsureCompanyOwnedAsync_ForeignCompany_ThrowsNotOwnedOrNotFound));
        var ownerPlayerId = Guid.NewGuid();
        var foreignPlayerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        db.Players.AddRange(
            new Player
            {
                Id = ownerPlayerId,
                Email = "owner@test.com",
                DisplayName = "Owner",
                PasswordHash = "hash",
            },
            new Player
            {
                Id = foreignPlayerId,
                Email = "foreign@test.com",
                DisplayName = "Foreign",
                PasswordHash = "hash",
            });
        db.Companies.Add(new Company
        {
            Id = companyId,
            PlayerId = foreignPlayerId,
            Name = "Foreign Co",
            FoundedAtTick = 0,
        });
        await db.SaveChangesAsync();

        var guard = new BotOwnershipGuard(db);
        var ex = await Assert.ThrowsAsync<GraphQLException>(() => guard.EnsureCompanyOwnedAsync(ownerPlayerId, companyId, CancellationToken.None));
        Assert.Equal(BotOwnershipGuard.NotOwnedOrNotFoundCode, ex.Errors.Single().Code);
    }

    [Fact]
    public async Task EnsureCompanyOwnedAsync_MissingCompany_ThrowsNotOwnedOrNotFound()
    {
        await using var db = CreateDb(nameof(EnsureCompanyOwnedAsync_MissingCompany_ThrowsNotOwnedOrNotFound));
        var playerId = Guid.NewGuid();
        db.Players.Add(new Player
        {
            Id = playerId,
            Email = "owner@test.com",
            DisplayName = "Owner",
            PasswordHash = "hash",
        });
        await db.SaveChangesAsync();

        var guard = new BotOwnershipGuard(db);
        var ex = await Assert.ThrowsAsync<GraphQLException>(() => guard.EnsureCompanyOwnedAsync(playerId, null, CancellationToken.None));
        Assert.Equal(BotOwnershipGuard.NotOwnedOrNotFoundCode, ex.Errors.Single().Code);
    }

    [Fact]
    public async Task EnsureMutationOwnershipAsync_ForeignForexAccount_ThrowsNotOwnedOrNotFound()
    {
        await using var db = CreateDb(nameof(EnsureMutationOwnershipAsync_ForeignForexAccount_ThrowsNotOwnedOrNotFound));
        var ownerPlayerId = Guid.NewGuid();
        var foreignPlayerId = Guid.NewGuid();
        var foreignAccountId = Guid.NewGuid();

        db.Players.AddRange(
            new Player
            {
                Id = ownerPlayerId,
                Email = "owner@test.com",
                DisplayName = "Owner",
                PasswordHash = "hash",
            },
            new Player
            {
                Id = foreignPlayerId,
                Email = "foreign@test.com",
                DisplayName = "Foreign",
                PasswordHash = "hash",
            });
        db.BankAccounts.Add(new BankAccount
        {
            Id = foreignAccountId,
            PlayerId = foreignPlayerId,
            AccountNumber = "1234567890123456",
            CurrencyCode = "EUR",
            Balance = 100m,
        });
        await db.SaveChangesAsync();

        using var document = JsonDocument.Parse($$"""
        {
          "input": {
            "fromBankAccountId": "{{foreignAccountId}}"
          }
        }
        """);

        var guard = new BotOwnershipGuard(db);
        var ex = await Assert.ThrowsAsync<GraphQLException>(() => guard.EnsureMutationOwnershipAsync("executeForexSwap", document.RootElement, ownerPlayerId, CancellationToken.None));
        Assert.Equal(BotOwnershipGuard.NotOwnedOrNotFoundCode, ex.Errors.Single().Code);
    }

    private static AppDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"bot-ownership-guard-tests-{name}")
            .Options;

        return new AppDbContext(options);
    }
}

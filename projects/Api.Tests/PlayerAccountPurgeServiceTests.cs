using Api.Data;
using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

public sealed class PlayerAccountPurgeServiceTests
{
    private static AppDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"player-purge-tests-{name}-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<(Guid GovPlayerId, Guid GovCompanyId)> SeedGovernmentAsync(AppDbContext db)
    {
        var govPlayerId = Guid.NewGuid();
        var govCompanyId = Guid.NewGuid();
        db.Players.Add(new Player
        {
            Id = govPlayerId,
            Email = GovernmentActorConstants.GovernmentEmail,
            DisplayName = "Government",
            PasswordHash = "hash",
        });
        db.Companies.Add(new Company
        {
            Id = govCompanyId,
            PlayerId = govPlayerId,
            Name = "Government",
            FoundedAtTick = 0,
        });
        await db.SaveChangesAsync();
        return (govPlayerId, govCompanyId);
    }

    [Fact]
    public async Task PurgeAsync_DestroysNonBankBuildings_TransfersBanksToGovernment_AndRemovesPlayer()
    {
        await using var db = CreateDb(nameof(PurgeAsync_DestroysNonBankBuildings_TransfersBanksToGovernment_AndRemovesPlayer));
        var (_, govCompanyId) = await SeedGovernmentAsync(db);

        var playerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        db.Players.Add(new Player { Id = playerId, Email = "victim@test.com", DisplayName = "Victim", PasswordHash = "hash" });
        db.Companies.Add(new Company { Id = companyId, PlayerId = playerId, Name = "Victim Co", FoundedAtTick = 0 });

        var cityId = Guid.NewGuid();
        db.Cities.Add(new City { Id = cityId, Name = "Testville", CurrencyCode = "EUR" });

        var bankId = Guid.NewGuid();
        db.Buildings.Add(new Building
        {
            Id = bankId,
            CompanyId = companyId,
            CityId = cityId,
            Type = BuildingType.Bank,
            Name = "Victim Bank",
            DepositInterestRatePercent = 8m,
            LendingInterestRatePercent = 5m,
            PendingDepositInterestRatePercent = 9m,
            PendingDepositRateEffectiveTick = 100,
            IsGovernmentOwned = false,
        });

        var factoryId = Guid.NewGuid();
        db.Buildings.Add(new Building
        {
            Id = factoryId,
            CompanyId = companyId,
            CityId = cityId,
            Type = BuildingType.Factory,
            Name = "Victim Factory",
        });
        db.BuildingUnits.Add(new BuildingUnit { Id = Guid.NewGuid(), BuildingId = factoryId, GridX = 0, GridY = 0, UnitType = "MANUFACTURING" });
        db.Inventories.Add(new Inventory { Id = Guid.NewGuid(), BuildingId = factoryId, Quantity = 10m });

        // The bank's own operating account.
        db.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = "1111111111111111",
            CurrencyCode = "EUR",
            CompanyId = companyId,
            BankBuildingId = bankId,
            Balance = 1000m,
        });

        // A deposit held at the victim's bank by a different player; must survive.
        var depositorAccountId = Guid.NewGuid();
        db.BankAccounts.Add(new BankAccount
        {
            Id = depositorAccountId,
            AccountNumber = "2222222222222222",
            CurrencyCode = "EUR",
            CompanyId = govCompanyId,
            BankBuildingId = bankId,
            Balance = 500m,
        });
        await db.SaveChangesAsync();

        var service = new PlayerAccountPurgeService(db);
        var result = await service.PurgeAsync("victim@test.com", CancellationToken.None);

        Assert.True(result.PlayerFound);
        Assert.Equal(1, result.CompaniesRemoved);
        Assert.Equal(1, result.BuildingsDestroyed);
        Assert.Equal(1, result.BanksTransferredToGovernment);

        Assert.False(await db.Players.AnyAsync(p => p.Id == playerId));
        Assert.False(await db.Companies.AnyAsync(c => c.Id == companyId));
        Assert.False(await db.Buildings.AnyAsync(b => b.Id == factoryId));
        Assert.False(await db.BuildingUnits.AnyAsync(u => u.BuildingId == factoryId));
        Assert.False(await db.Inventories.AnyAsync(i => i.BuildingId == factoryId));

        var bank = await db.Buildings.SingleAsync(b => b.Id == bankId);
        Assert.Equal(govCompanyId, bank.CompanyId);
        Assert.True(bank.IsGovernmentOwned);
        Assert.Equal(0m, bank.DepositInterestRatePercent);
        Assert.Equal(20m, bank.LendingInterestRatePercent);
        Assert.Null(bank.PendingDepositInterestRatePercent);
        Assert.Null(bank.PendingDepositRateEffectiveTick);

        // Other player's deposit at the (now government) bank survives.
        Assert.True(await db.BankAccounts.AnyAsync(a => a.Id == depositorAccountId));
    }

    [Fact]
    public async Task PurgeAsync_UnknownPlayer_ReturnsNotFound()
    {
        await using var db = CreateDb(nameof(PurgeAsync_UnknownPlayer_ReturnsNotFound));
        await SeedGovernmentAsync(db);

        var service = new PlayerAccountPurgeService(db);
        var result = await service.PurgeAsync("nobody@test.com", CancellationToken.None);

        Assert.False(result.PlayerFound);
        Assert.Equal(0, result.CompaniesRemoved);
    }

    [Fact]
    public async Task PurgeAsync_GovernmentActor_IsNeverPurged()
    {
        await using var db = CreateDb(nameof(PurgeAsync_GovernmentActor_IsNeverPurged));
        var (govPlayerId, _) = await SeedGovernmentAsync(db);

        var service = new PlayerAccountPurgeService(db);
        var result = await service.PurgeAsync(GovernmentActorConstants.GovernmentEmail, CancellationToken.None);

        Assert.False(result.PlayerFound);
        Assert.True(await db.Players.AnyAsync(p => p.Id == govPlayerId));
    }
}

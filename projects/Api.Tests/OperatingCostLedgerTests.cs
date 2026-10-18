using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Api.Tests.Infrastructure;

namespace Api.Tests;

/// <summary>
/// #630: the operating-cost phase books one labor and one energy ledger row per building per tick (R&amp;D units stay per unit),
/// and the ledger always sums to exactly what the building's account was debited, also while a global cost event is active.
/// </summary>
public sealed class OperatingCostLedgerTests
{
    private const decimal StartBalance = 1_000_000m;

    private sealed record Seeded(Guid CompanyId, Guid BuildingId, Guid AccountId, Guid ResearchUnitId);

    private static async Task TickAsync(ApiWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetRequiredService<IEnumerable<ITickPhase>>();
        await new TickProcessor(db, phases, new NullLogger<TickProcessor>()).ProcessTickAsync();
    }

    private static async Task<Seeded> SeedAsync(ApiWebApplicationFactory factory, decimal? costMultiplier = null, decimal startBalance = StartBalance)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var buildings = await db.Buildings.Include(b => b.Units).ToListAsync();
        foreach (var existing in buildings)
        {
            db.BuildingUnits.RemoveRange(existing.Units);
        }

        db.Buildings.RemoveRange(buildings);
        await db.SaveChangesAsync();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var player = new Player
        {
            Id = Guid.NewGuid(), Email = $"opcost-{Guid.NewGuid():N}@example.com", DisplayName = "OpCost", PasswordHash = "x",
            OnboardingCompletedAtUtc = DateTime.UtcNow,
        };
        var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "OpCost Co", FoundedAtUtc = DateTime.UtcNow, FoundedAtTick = 1 };
        var account = new BankAccount
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CurrencyCode = city.CurrencyCode, Balance = startBalance,
            AccountNumber = "1234567890123499", CreatedAtUtc = DateTime.UtcNow,
        };
        var building = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, Type = BuildingType.Factory, Name = "OpCost Factory",
            Latitude = city.Latitude, Longitude = city.Longitude, PowerConsumption = 10, PowerStatus = PowerStatus.Powered,
            BankAccountId = account.Id, BuiltAtUtc = DateTime.UtcNow,
        };
        db.Players.Add(player);
        db.Companies.Add(company);
        db.BankAccounts.Add(account);
        db.Buildings.Add(building);
        for (var i = 0; i < 6; i++)
        {
            db.BuildingUnits.Add(new BuildingUnit { Id = Guid.NewGuid(), BuildingId = building.Id, UnitType = UnitType.Manufacturing, GridX = i, GridY = 0, Level = 1 });
        }

        for (var i = 0; i < 2; i++)
        {
            db.BuildingUnits.Add(new BuildingUnit { Id = Guid.NewGuid(), BuildingId = building.Id, UnitType = UnitType.Storage, GridX = i, GridY = 1, Level = 1 });
        }

        var research = new BuildingUnit { Id = Guid.NewGuid(), BuildingId = building.Id, UnitType = UnitType.ProductQuality, GridX = 0, GridY = 2, Level = 1 };
        db.BuildingUnits.Add(research);
        if (costMultiplier.HasValue)
        {
            var tick = (await db.GameStates.FirstAsync()).CurrentTick;
            db.GlobalEvents.Add(new GlobalEvent
            {
                Id = Guid.NewGuid(), EventType = GlobalEventType.EnergyCrisis, Severity = GlobalEventSeverity.Major, Title = "Cost spike",
                Description = "Cost test.", IsActive = true, StartTick = tick, DurationTicks = 100, OperatingCostMultiplier = costMultiplier.Value,
                TradeRouteMultiplier = 1.0m, RdMultiplier = 1.0m, MineEfficiencyMultiplier = 1.0m, CreatedAtUtc = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
        return new Seeded(company.Id, building.Id, account.Id, research.Id);
    }

    private static async Task<(List<LedgerEntry> Entries, decimal BalanceDelta)> RunAsync(ApiWebApplicationFactory factory, Seeded seeded)
    {
        await TickAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var balance = (await db.BankAccounts.AsNoTracking().SingleAsync(a => a.Id == seeded.AccountId)).Balance;
        var entries = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.CompanyId == seeded.CompanyId && (e.Category == LedgerCategory.LaborCost || e.Category == LedgerCategory.EnergyCost))
            .ToListAsync();
        return (entries, balance - StartBalance);
    }

    [Fact]
    public async Task OperatingCosts_AreBookedAsOneLaborAndOneEnergyRowPerBuilding_PlusPerUnitResearchRows()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var seeded = await SeedAsync(factory);

        var (entries, _) = await RunAsync(factory, seeded);

        var operating = entries.Where(e => e.BuildingUnitId != seeded.ResearchUnitId).ToList();
        Assert.Single(operating, e => e.Category == LedgerCategory.LaborCost);
        Assert.Single(operating, e => e.Category == LedgerCategory.EnergyCost);
        Assert.All(operating, e => Assert.Null(e.BuildingUnitId));
        Assert.All(operating, e => Assert.Equal(seeded.BuildingId, e.BuildingId));
        // Research units keep their own labelled rows.
        Assert.Contains(entries, e => e.BuildingUnitId == seeded.ResearchUnitId && e.Category == LedgerCategory.LaborCost);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1.5)]
    public async Task OperatingCostLedger_SumsToTheAccountDebit(double? multiplier)
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var seeded = await SeedAsync(factory, multiplier.HasValue ? (decimal)multiplier.Value : null);

        var (entries, balanceDelta) = await RunAsync(factory, seeded);

        Assert.NotEmpty(entries);
        Assert.Equal(balanceDelta, entries.Sum(e => e.Amount));
    }

    [Fact]
    public async Task UnfundedBuilding_IsSuspended_WithNoCostRowsAndNoDebit()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var seeded = await SeedAsync(factory, startBalance: 0m);

        await TickAsync(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0m, (await db.BankAccounts.AsNoTracking().SingleAsync(a => a.Id == seeded.AccountId)).Balance);
        Assert.True((await db.Buildings.AsNoTracking().SingleAsync(b => b.Id == seeded.BuildingId)).IsSuspendedForFunds);
        Assert.Empty(await db.LedgerEntries.AsNoTracking()
            .Where(e => e.CompanyId == seeded.CompanyId && (e.Category == LedgerCategory.LaborCost || e.Category == LedgerCategory.EnergyCost))
            .ToListAsync());
    }

    [Fact]
    public async Task AStillUnfundedBuilding_WritesTheSuspensionAuditRowOnce_NotEveryTick()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var seeded = await SeedAsync(factory, startBalance: 0m);

        await TickAsync(factory);
        await TickAsync(factory);
        await TickAsync(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.BuildingId == seeded.BuildingId && e.Category == LedgerCategory.Other && e.Amount == 0m && e.Description.StartsWith("Building suspended"))
            .ToListAsync();
        Assert.Single(audit); // the transition into suspension is recorded; staying suspended is not repeated every tick
        Assert.True((await db.Buildings.AsNoTracking().SingleAsync(b => b.Id == seeded.BuildingId)).IsSuspendedForFunds);
    }

    [Fact]
    public async Task ABuildingThatIsFundedAgainAndRunsDryAgain_RecordsANewSuspensionRow()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var seeded = await SeedAsync(factory, startBalance: 0m);
        await TickAsync(factory); // suspended: first row
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.BankAccounts.SingleAsync(a => a.Id == seeded.AccountId)).Balance = StartBalance;
            await db.SaveChangesAsync();
        }

        await TickAsync(factory); // funded: resumes and pays
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False((await db.Buildings.AsNoTracking().SingleAsync(b => b.Id == seeded.BuildingId)).IsSuspendedForFunds);
            (await db.BankAccounts.SingleAsync(a => a.Id == seeded.AccountId)).Balance = 0m;
            await db.SaveChangesAsync();
        }

        await TickAsync(factory); // dry again: a new transition, a new row

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = await verifyDb.LedgerEntries.AsNoTracking()
            .Where(e => e.BuildingId == seeded.BuildingId && e.Category == LedgerCategory.Other && e.Amount == 0m && e.Description.StartsWith("Building suspended"))
            .ToListAsync();
        Assert.Equal(2, audit.Count);
    }
}

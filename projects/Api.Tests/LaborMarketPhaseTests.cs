using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Economy;

namespace Api.Tests;

public sealed class LaborMarketPhaseTests
{
    private static async Task TickAsync(ApiWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetRequiredService<IEnumerable<ITickPhase>>();
        await new TickProcessor(db, phases, new NullLogger<TickProcessor>()).ProcessTickAsync();
    }

    /// <summary>Removes the buildings of the seeded NPC companies so a test controls all labor demand.</summary>
    private static async Task ClearSeededBuildingsAsync(AppDbContext db)
    {
        var buildings = await db.Buildings.Include(b => b.Units).ToListAsync();
        foreach (var building in buildings)
        {
            db.BuildingUnits.RemoveRange(building.Units);
        }

        db.Buildings.RemoveRange(buildings);
        await db.SaveChangesAsync();
    }

    private sealed record Seeded(Guid CityId, Guid CompanyId, Guid BuildingId, Guid AccountId);

    /// <summary>One powered factory with <paramref name="manufacturingUnits"/> level-1 manufacturing units in Bratislava.</summary>
    private static async Task<Seeded> SeedAsync(
        ApiWebApplicationFactory factory,
        int manufacturingUnits,
        int? population = null,
        decimal? salaryMultiplier = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ClearSeededBuildingsAsync(db);
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        if (population.HasValue)
        {
            city.Population = population.Value;
        }

        var player = new Player
        {
            Id = Guid.NewGuid(), Email = $"labor-{Guid.NewGuid():N}@example.com", DisplayName = "Labor", PasswordHash = "x",
            OnboardingCompletedAtUtc = DateTime.UtcNow,
        };
        var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "Labor Co", FoundedAtUtc = DateTime.UtcNow, FoundedAtTick = 1 };
        var account = new BankAccount
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CurrencyCode = city.CurrencyCode, Balance = 1_000_000m,
            AccountNumber = "1234567890123456", CreatedAtUtc = DateTime.UtcNow,
        };
        var building = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, Type = BuildingType.Factory, Name = "Labor Factory",
            Latitude = city.Latitude, Longitude = city.Longitude, PowerConsumption = 10, PowerStatus = PowerStatus.Powered,
            BankAccountId = account.Id, BuiltAtUtc = DateTime.UtcNow,
        };
        db.Players.Add(player);
        db.Companies.Add(company);
        db.BankAccounts.Add(account);
        db.Buildings.Add(building);
        for (var i = 0; i < manufacturingUnits; i++)
        {
            db.BuildingUnits.Add(new BuildingUnit
            {
                Id = Guid.NewGuid(), BuildingId = building.Id, UnitType = UnitType.Manufacturing, GridX = i % 5, GridY = i / 5, Level = 1,
            });
        }

        if (salaryMultiplier.HasValue)
        {
            db.CompanyCitySalarySettings.Add(new CompanyCitySalarySetting
            {
                Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, SalaryMultiplier = salaryMultiplier.Value,
            });
        }

        await db.SaveChangesAsync();
        return new Seeded(city.Id, company.Id, building.Id, account.Id);
    }

    private static async Task<CityLaborMarket?> GetMarketAsync(ApiWebApplicationFactory factory, Guid cityId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.CityLaborMarkets.AsNoTracking().FirstOrDefaultAsync(m => m.CityId == cityId);
    }

    [Fact]
    public async Task EveryTick_WritesLaborMarketRowWithPoolDemandAndAverageSalary()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var seeded = await SeedAsync(factory, manufacturingUnits: 4, population: 1_000_000, salaryMultiplier: 1.2m);

        await TickAsync(factory);

        var market = await GetMarketAsync(factory, seeded.CityId);
        Assert.NotNull(market);
        var expectedDemand = 4 * CompanyEconomyCalculator.GetBaseUnitLaborHours(UnitType.Manufacturing, 1);
        Assert.Equal(LaborMarketCalculator.ComputeLaborPool(1_000_000), market.LaborPool);
        Assert.Equal(expectedDemand, market.LaborDemand);
        Assert.Equal(1.2m, market.AverageSalaryMultiplier);
        Assert.True(market.UnemploymentRate > 0.9m);
        Assert.Equal(1L, market.UpdatedAtTick);
    }

    [Fact]
    public async Task EveryCityGetsARow_EvenWithoutBuildings()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        await using (var clearScope = factory.Services.CreateAsyncScope())
        {
            await ClearSeededBuildingsAsync(clearScope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        await TickAsync(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cityCount = await db.Cities.CountAsync();
        var rows = await db.CityLaborMarkets.AsNoTracking().ToListAsync();
        Assert.Equal(cityCount, rows.Count);
        Assert.All(rows, row => Assert.Equal(0m, row.LaborDemand));
        Assert.All(rows, row => Assert.Equal(1m, row.StaffingFillRate));
    }

    [Fact]
    public async Task ZeroPopulationCity_DoesNotCrashAndCannotStaffAnything()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var seeded = await SeedAsync(factory, manufacturingUnits: 2, population: 0);

        await TickAsync(factory);

        var market = await GetMarketAsync(factory, seeded.CityId);
        Assert.NotNull(market);
        Assert.Equal(0m, market.LaborPool);
        Assert.Equal(0m, market.StaffingFillRate);
        Assert.Equal(LaborMarketCalculator.MaxWageIndex, market.WageIndex);
    }

    [Fact]
    public async Task MoreDemandOverSeveralTicks_RaisesTheWageIndex()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        // pool = 40,000 x 0.45 x 0.005 = 90 worker-hours; 10 units use 8.5 hours (9% utilization).
        var seeded = await SeedAsync(factory, manufacturingUnits: 10, population: 40_000);
        await TickAsync(factory);
        var calm = await GetMarketAsync(factory, seeded.CityId);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < 120; i++)
            {
                db.BuildingUnits.Add(new BuildingUnit
                {
                    Id = Guid.NewGuid(), BuildingId = seeded.BuildingId, UnitType = UnitType.Manufacturing, GridX = i % 10, GridY = 10 + (i / 10), Level = 1,
                });
            }

            await db.SaveChangesAsync();
        }

        await TickAsync(factory);
        await TickAsync(factory);
        var tight = await GetMarketAsync(factory, seeded.CityId);

        Assert.NotNull(calm);
        Assert.NotNull(tight);
        Assert.True(calm.WageIndex < 1m, $"idle city should soften wages, was {calm.WageIndex}");
        Assert.True(tight.WageIndex > calm.WageIndex, $"{tight.WageIndex} should exceed {calm.WageIndex}");
        Assert.True(tight.Utilization > 1m);
        Assert.True(tight.StaffingFillRate < 1m);
        Assert.Equal(0m, tight.UnemploymentRate);
    }

    [Fact]
    public async Task BuildingSuspendedForFunds_StillCountsAsLaborDemand_SoWagesDoNotOscillate()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var seeded = await SeedAsync(factory, manufacturingUnits: 4, population: 1_000_000);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Empty account: OperatingCostPhase suspends the building for insufficient funds this tick.
            (await db.BankAccounts.FirstAsync(a => a.Id == seeded.AccountId)).Balance = 0m;
            await db.SaveChangesAsync();
        }

        await TickAsync(factory);

        var market = await GetMarketAsync(factory, seeded.CityId);
        Assert.NotNull(market);
        Assert.Equal(4 * CompanyEconomyCalculator.GetBaseUnitLaborHours(UnitType.Manufacturing, 1), market.LaborDemand);
    }

    [Fact]
    public async Task WageIndexFromPreviousTick_ScalesLaborCostsWhileLedgerStaysBalanced()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var seeded = await SeedAsync(factory, manufacturingUnits: 3, population: 5_000_000);

        // Baseline tick with a neutral index (no stored row yet).
        decimal baselineLabor;
        decimal balanceBeforeSecondTick;
        await TickAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            baselineLabor = await LaborSpentAsync(db, seeded.CompanyId);
            balanceBeforeSecondTick = (await db.BankAccounts.AsNoTracking().FirstAsync(a => a.Id == seeded.AccountId)).Balance;
            // Force a 1.5x wage index for the next tick and wipe the ledger so only tick 2 is measured.
            var row = await db.CityLaborMarkets.FirstAsync(m => m.CityId == seeded.CityId);
            row.WageIndex = 1.5m;
            row.UpdatedAtTick = 1;
            db.LedgerEntries.RemoveRange(db.LedgerEntries.Where(e => e.CompanyId == seeded.CompanyId));
            await db.SaveChangesAsync();
        }

        await TickAsync(factory);

        await using var verify = factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var scaledLabor = await LaborSpentAsync(verifyDb, seeded.CompanyId);
        Assert.True(baselineLabor > 0m);
        Assert.Equal(decimal.Round(baselineLabor * 1.5m, 2), decimal.Round(scaledLabor, 2));

        // Money conservation: the account moved by exactly what the ledger recorded for the tick.
        var account = await verifyDb.BankAccounts.AsNoTracking().FirstAsync(a => a.Id == seeded.AccountId);
        var ledgerSum = await verifyDb.LedgerEntries.Where(e => e.CompanyId == seeded.CompanyId).SumAsync(e => e.Amount);
        Assert.True(ledgerSum < 0m);
        Assert.Equal(balanceBeforeSecondTick + ledgerSum, account.Balance);
    }

    private static async Task<decimal> LaborSpentAsync(AppDbContext db, Guid companyId)
        => -await db.LedgerEntries
            .Where(e => e.CompanyId == companyId && e.Category == LedgerCategory.LaborCost)
            .SumAsync(e => e.Amount);
}

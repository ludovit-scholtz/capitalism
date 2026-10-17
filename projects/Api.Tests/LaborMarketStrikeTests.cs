using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Economy;

namespace Api.Tests;

/// <summary>Staffing fill rate and deterministic strikes of the labor market (#616).</summary>
public sealed class LaborMarketStrikeTests
{
    // Tiny city so a handful of units makes the labor market tight: pool = 40,000 x 0.45 x 0.005 = 90 worker-hours.
    private const int TightCityPopulation = 40_000;

    private static async Task TickAsync(ApiWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetRequiredService<IEnumerable<ITickPhase>>();
        await new TickProcessor(db, phases, new NullLogger<TickProcessor>()).ProcessTickAsync();
    }

    private sealed record Employer(Guid CompanyId, Guid BuildingId, Guid AccountId, Guid CityId);

    private static async Task<List<Employer>> SeedEmployersAsync(
        ApiWebApplicationFactory factory,
        IReadOnlyList<(decimal Salary, int Units, Guid? BuildingId)> employers,
        int population = TightCityPopulation)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var buildings = await db.Buildings.Include(b => b.Units).ToListAsync();
        foreach (var existing in buildings)
        {
            db.BuildingUnits.RemoveRange(existing.Units);
        }

        db.Buildings.RemoveRange(buildings);
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        city.Population = population;

        var result = new List<Employer>();
        foreach (var (salary, units, buildingId) in employers)
        {
            var player = new Player
            {
                Id = Guid.NewGuid(), Email = $"strike-{Guid.NewGuid():N}@example.com", DisplayName = "Strike", PasswordHash = "x",
                OnboardingCompletedAtUtc = DateTime.UtcNow,
            };
            var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = $"Employer {salary}", FoundedAtUtc = DateTime.UtcNow, FoundedAtTick = 1 };
            var account = new BankAccount
            {
                Id = Guid.NewGuid(), CompanyId = company.Id, CurrencyCode = city.CurrencyCode, Balance = 5_000_000m,
                AccountNumber = Random.Shared.NextInt64(1_000_000_000_000_000, 9_000_000_000_000_000).ToString(), CreatedAtUtc = DateTime.UtcNow,
            };
            var building = new Building
            {
                Id = buildingId ?? Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, Type = BuildingType.Factory, Name = "Plant",
                Latitude = city.Latitude, Longitude = city.Longitude, PowerConsumption = 10, PowerStatus = PowerStatus.Powered,
                BankAccountId = account.Id, BuiltAtUtc = DateTime.UtcNow,
            };
            db.Players.Add(player);
            db.Companies.Add(company);
            db.BankAccounts.Add(account);
            db.Buildings.Add(building);
            for (var i = 0; i < units; i++)
            {
                db.BuildingUnits.Add(new BuildingUnit
                {
                    Id = Guid.NewGuid(), BuildingId = building.Id, UnitType = UnitType.Manufacturing, GridX = i % 10, GridY = i / 10, Level = 1,
                });
            }

            db.CompanyCitySalarySettings.Add(new CompanyCitySalarySetting
            {
                Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, SalaryMultiplier = salary,
            });
            result.Add(new Employer(company.Id, building.Id, account.Id, city.Id));
        }

        await db.SaveChangesAsync();
        return result;
    }

    private static async Task<Building> GetBuildingAsync(ApiWebApplicationFactory factory, Guid buildingId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Buildings.AsNoTracking().FirstAsync(b => b.Id == buildingId);
    }

    /// <summary>Finds a building id whose deterministic tick-1 strike roll falls below <paramref name="probability"/>.</summary>
    private static Guid FindIdThatStrikesAtTick(long tick, decimal probability, bool shouldStrike)
    {
        for (var i = 0; i < 100_000; i++)
        {
            var id = Guid.NewGuid();
            var rolled = LaborMarketCalculator.Roll(tick, id, salt: 1) < (double)probability;
            if (rolled == shouldStrike)
            {
                return id;
            }
        }

        throw new InvalidOperationException("No suitable building id found.");
    }

    [Fact]
    public async Task UnderpayingEmployer_GetsFewerStaffThanTheBetterPayingOneInATightMarket()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        // 120 + 120 units x 0.85 h = 204 worker-hours against a pool of 90: tight and over-demanded.
        var employers = await SeedEmployersAsync(factory, [(1.5m, 120, null), (0.5m, 120, null)]);

        await TickAsync(factory);

        var generous = await GetBuildingAsync(factory, employers[0].BuildingId);
        var stingy = await GetBuildingAsync(factory, employers[1].BuildingId);
        Assert.InRange(generous.StaffingFactor, 0.01m, 1m);
        Assert.True(stingy.StaffingFactor < generous.StaffingFactor, $"stingy {stingy.StaffingFactor} should be below generous {generous.StaffingFactor}");
    }

    [Fact]
    public async Task IdleMarket_LeavesEveryoneFullyStaffedRegardlessOfPay()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var employers = await SeedEmployersAsync(factory, [(1.5m, 2, null), (0.5m, 2, null)], population: 5_000_000);

        await TickAsync(factory);

        var stingy = await GetBuildingAsync(factory, employers[1].BuildingId);
        Assert.True(stingy.StaffingFactor > 0.99m, $"idle market should barely penalise low pay, was {stingy.StaffingFactor}");
    }

    [Fact]
    public async Task StaffingFactorFromPreviousTick_ScalesLaborCost()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var employers = await SeedEmployersAsync(factory, [(1.0m, 4, null)], population: 5_000_000);
        await TickAsync(factory);
        decimal fullLabor;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            fullLabor = -await db.LedgerEntries.Where(e => e.CompanyId == employers[0].CompanyId && e.Category == LedgerCategory.LaborCost).SumAsync(e => e.Amount);
            db.LedgerEntries.RemoveRange(db.LedgerEntries.Where(e => e.CompanyId == employers[0].CompanyId));
            (await db.Buildings.FirstAsync(b => b.Id == employers[0].BuildingId)).StaffingFactor = 0.5m;
            // Neutralise wage pressure so only the staffing factor changes the bill.
            foreach (var row in await db.CityLaborMarkets.ToListAsync()) row.WageIndex = 1m;
            await db.SaveChangesAsync();
        }

        await TickAsync(factory);

        await using var verify = factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var halfLabor = -await verifyDb.LedgerEntries.Where(e => e.CompanyId == employers[0].CompanyId && e.Category == LedgerCategory.LaborCost).SumAsync(e => e.Amount);
        Assert.True(fullLabor > 0m);
        Assert.Equal(decimal.Round(fullLabor * 0.5m, 2), decimal.Round(halfLabor, 2), 1);
    }

    [Fact]
    public async Task PayWellBelowCityAverage_StartsAStrikeDeterministically()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var payRatio = 0.5m / ((0.5m + 1.5m) / 2m); // equal demand => city average 1.0
        var strikingId = FindIdThatStrikesAtTick(1, LaborMarketCalculator.ComputeStrikeProbability(payRatio), shouldStrike: true);
        var employers = await SeedEmployersAsync(factory, [(0.5m, 6, strikingId), (1.5m, 6, null)], population: 5_000_000);

        await TickAsync(factory);

        var building = await GetBuildingAsync(factory, strikingId);
        Assert.NotNull(building.StrikeStartedAtTick);
        Assert.Equal(1L, building.StrikeStartedAtTick);
        Assert.InRange(building.StrikeUntilTick!.Value - building.StrikeStartedAtTick!.Value, 6, 11);
        var other = await GetBuildingAsync(factory, employers[1].BuildingId);
        Assert.Null(other.StrikeUntilTick);
    }

    [Fact]
    public async Task FairlyPaidEmployer_NeverStrikes()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var employers = await SeedEmployersAsync(factory, [(1.0m, 4, null), (1.0m, 4, null)], population: 5_000_000);

        for (var i = 0; i < 25; i++)
        {
            await TickAsync(factory);
        }

        Assert.All(employers, e => Assert.Null(GetBuildingAsync(factory, e.BuildingId).Result.StrikeStartedAtTick));
    }

    [Fact]
    public async Task StrikingBuilding_ProducesNothingAndPaysNoWages_WithoutCreatingOrDestroyingMoney()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var employers = await SeedEmployersAsync(factory, [(1.0m, 4, null)], population: 5_000_000);
        await TickAsync(factory);
        decimal balanceBefore;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var building = await db.Buildings.FirstAsync(b => b.Id == employers[0].BuildingId);
            building.StrikeStartedAtTick = 1;
            building.StrikeUntilTick = 50;
            balanceBefore = (await db.BankAccounts.FirstAsync(a => a.Id == employers[0].AccountId)).Balance;
            db.LedgerEntries.RemoveRange(db.LedgerEntries.Where(e => e.CompanyId == employers[0].CompanyId));
            await db.SaveChangesAsync();
        }

        await TickAsync(factory);

        await using var verify = factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = await verifyDb.BankAccounts.AsNoTracking().FirstAsync(a => a.Id == employers[0].AccountId);
        var ledgerSum = await verifyDb.LedgerEntries.Where(e => e.CompanyId == employers[0].CompanyId).SumAsync(e => e.Amount);
        Assert.Equal(balanceBefore, account.Balance);
        Assert.Equal(0m, ledgerSum);
    }

    [Fact]
    public async Task Strike_EndsWhenItsTimeIsUp()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var employers = await SeedEmployersAsync(factory, [(0.5m, 4, null), (1.5m, 4, null)], population: 5_000_000);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var building = await db.Buildings.FirstAsync(b => b.Id == employers[0].BuildingId);
            building.StrikeStartedAtTick = 0;
            building.StrikeUntilTick = 1; // ends at the end of tick 1
            await db.SaveChangesAsync();
        }

        await TickAsync(factory);

        var after = await GetBuildingAsync(factory, employers[0].BuildingId);
        Assert.Null(after.StrikeUntilTick);
        Assert.Null(after.StrikeStartedAtTick);
    }

    [Fact]
    public async Task Strike_EndsEarlyWhenThePayIsFixed()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var employers = await SeedEmployersAsync(factory, [(1.0m, 4, null), (1.0m, 4, null)], population: 5_000_000);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var building = await db.Buildings.FirstAsync(b => b.Id == employers[0].BuildingId);
            building.StrikeStartedAtTick = 0;
            building.StrikeUntilTick = 40; // long strike, but pay is already at the city average
            await db.SaveChangesAsync();
        }

        await TickAsync(factory);

        Assert.Null((await GetBuildingAsync(factory, employers[0].BuildingId)).StrikeUntilTick);
    }
}

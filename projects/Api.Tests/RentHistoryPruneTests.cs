using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// The rent phase keeps 100 ticks of income history per building for the sparkline. It used to look for old rows through
/// <c>DbSet.Local</c> once per building (a full change-detection pass each, and it only ever saw rows added in the same tick, so
/// nothing was ever deleted). The prune is now one delete per in-game day (#646).
/// </summary>
public sealed class RentHistoryPruneTests
{
    private const long PruneTick = 720; // a multiple of 24: the first tick of an in-game day
    private const long RetainedTicks = 100;

    private static async Task<Building> SeedBuildingAsync(AppDbContext db, string name)
    {
        var city = await db.Cities.FirstDeterministicAsync();
        city.AverageRentPerSqm = 10m;
        var player = new Player { Id = Guid.NewGuid(), Email = $"rent-prune-{Guid.NewGuid():N}@test.com", DisplayName = name, PasswordHash = "hash", Role = PlayerRole.Player };
        var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = name, FoundedAtUtc = DateTime.UtcNow, FoundedAtTick = 1 };
        var account = new BankAccount
        {
            Id = Guid.NewGuid(), AccountNumber = Random.Shared.NextInt64(7_200_000_000_000_000, 7_299_999_999_999_999).ToString(),
            CurrencyCode = city.CurrencyCode, Balance = 50_000m, CompanyId = company.Id, CreatedAtUtc = DateTime.UtcNow,
        };
        var building = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, Type = BuildingType.Apartment, Name = name, Latitude = city.Latitude,
            Longitude = city.Longitude, Level = 1, PricePerSqm = 10m, TotalAreaSqm = 100m, OccupancyPercent = 80m, BankAccountId = account.Id,
            BuiltAtUtc = DateTime.UtcNow,
        };
        db.Players.Add(player);
        db.Companies.Add(company);
        db.BankAccounts.Add(account);
        db.Buildings.Add(building);
        await db.SaveChangesAsync();
        return building;
    }

    private static void AddRecord(AppDbContext db, Guid buildingId, long tick)
        => db.RentalIncomeRecords.Add(new RentalIncomeRecord
        {
            Id = Guid.NewGuid(), BuildingId = buildingId, Tick = tick, Revenue = 1m, Costs = 1m, OccupancyPercent = 80m, RentPerSqm = 10m, CurrencyCode = "EUR",
        });

    private static async Task<List<long>> TicksOfAsync(ApiWebApplicationFactory factory, Guid buildingId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().RentalIncomeRecords.AsNoTracking()
            .Where(r => r.BuildingId == buildingId).OrderBy(r => r.Tick).Select(r => r.Tick).ToListAsync();
    }

    private static async Task RunTickAsync(ApiWebApplicationFactory factory, long tickBefore)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var state = await db.GameStates.FirstDeterministicAsync();
        state!.CurrentTick = tickBefore;
        await db.SaveChangesAsync();
        _ = await scope.ServiceProvider.GetRequiredService<TickProcessor>().ProcessTickAsync();
    }

    private static async Task<(Building First, Building Second)> ArrangeAsync(ApiWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = await SeedBuildingAsync(db, "Rent Prune A");
        var second = await SeedBuildingAsync(db, "Rent Prune B");
        foreach (var building in new[] { first, second })
        {
            AddRecord(db, building.Id, 10); // far older than the window
            AddRecord(db, building.Id, PruneTick - RetainedTicks - 1); // one tick outside the window
            AddRecord(db, building.Id, PruneTick - RetainedTicks); // exactly at the edge: kept
            AddRecord(db, building.Id, PruneTick - 5);
        }

        await db.SaveChangesAsync();
        return (first, second);
    }

    [Fact]
    public async Task OnTheFirstTickOfAnInGameDay_RowsOlderThanTheWindowAreDeletedForEveryBuilding()
    {
        await using var factory = new ApiWebApplicationFactory();
        var (first, second) = await ArrangeAsync(factory);

        await RunTickAsync(factory, tickBefore: PruneTick - 1);

        foreach (var building in new[] { first, second })
        {
            // Kept: the edge of the window, a recent row, and the row the phase wrote this tick.
            Assert.Equal([PruneTick - RetainedTicks, PruneTick - 5, PruneTick], await TicksOfAsync(factory, building.Id));
        }
    }

    [Fact]
    public async Task OnOtherTicks_NothingIsDeleted()
    {
        await using var factory = new ApiWebApplicationFactory();
        var (first, _) = await ArrangeAsync(factory);

        await RunTickAsync(factory, tickBefore: PruneTick); // runs tick 721, not a multiple of 24

        Assert.Equal([10L, PruneTick - RetainedTicks - 1, PruneTick - RetainedTicks, PruneTick - 5, PruneTick + 1], await TicksOfAsync(factory, first.Id));
    }

    [Fact]
    public async Task EveryRentalBuildingStillGetsExactlyOneRecordPerTick()
    {
        await using var factory = new ApiWebApplicationFactory();
        var (first, second) = await ArrangeAsync(factory);

        await RunTickAsync(factory, tickBefore: PruneTick - 1);

        Assert.Single((await TicksOfAsync(factory, first.Id)).Where(tick => tick == PruneTick));
        Assert.Single((await TicksOfAsync(factory, second.Id)).Where(tick => tick == PruneTick));
    }
}

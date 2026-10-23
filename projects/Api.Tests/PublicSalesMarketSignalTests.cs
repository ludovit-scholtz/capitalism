using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Pins the market-signal notifications of <c>PublicSalesPhase</c> (price spike against the previous tick, unmet demand) so the
/// previous-tick lookup can be one query for all (city, product) groups instead of one per group (#646).
/// </summary>
public sealed class PublicSalesMarketSignalTests
{
    private const long CurrentTick = 700;

    private sealed record Seller(Guid PlayerId, Guid CompanyId, Guid CityId, Guid ProductId, decimal Price);

    private static async Task<City> CreateCityAsync(AppDbContext db, string name, int population)
    {
        var city = new City
        {
            Id = Guid.NewGuid(), Name = name, CountryCode = "TS", CurrencyCode = "EUR", Latitude = 48.3, Longitude = 17.3,
            Population = population, AverageRentPerSqm = 20m, BaseSalaryPerManhour = 10m,
        };
        db.Cities.Add(city);
        await db.SaveChangesAsync();
        return city;
    }

    /// <summary>A player with one company and one shop selling <paramref name="stock"/> units of a product at its base price.</summary>
    private static async Task<Seller> CreateSellerAsync(AppDbContext db, City city, ProductType product, decimal stock)
    {
        var player = new Player
        {
            Id = Guid.NewGuid(), Email = $"signal-{Guid.NewGuid():N}@test.local", DisplayName = "Signal", PasswordHash = "mock",
            Role = PlayerRole.Player, ActiveAccountType = AccountContextType.Company, CreatedAtUtc = DateTime.UtcNow,
            OnboardingCompletedAtUtc = DateTime.UtcNow,
        };
        var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = $"Signal Co {Guid.NewGuid():N}", FoundedAtUtc = DateTime.UtcNow, FoundedAtTick = 1 };
        player.ActiveCompanyId = company.Id;
        db.Players.Add(player);
        db.Companies.Add(company);
        var account = await BuildingBankAccountProvisioning.EnsureCompanyCurrencyAccountAsync(db, company.Id, city.CurrencyCode);
        account.Balance = 1_000_000m;

        var building = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, Type = BuildingType.SalesShop, Name = "Signal Shop",
            Latitude = city.Latitude, Longitude = city.Longitude, BuiltAtUtc = DateTime.UtcNow, Level = 1,
        };
        db.Buildings.Add(building);
        var unit = new BuildingUnit
        {
            Id = Guid.NewGuid(), BuildingId = building.Id, UnitType = UnitType.PublicSales, GridX = 0, GridY = 0, Level = 1,
            ProductTypeId = product.Id, MinPrice = product.BasePrice,
        };
        db.BuildingUnits.Add(unit);
        db.Inventories.Add(new Inventory { Id = Guid.NewGuid(), BuildingId = building.Id, BuildingUnitId = unit.Id, ProductTypeId = product.Id, Quantity = stock, Quality = 0.5m });
        await db.SaveChangesAsync();
        return new Seller(player.Id, company.Id, city.Id, product.Id, product.BasePrice);
    }

    private static async Task SetTickAsync(AppDbContext db)
    {
        var state = await db.GameStates.OrderBy(s => s.Id).FirstAsync();
        state.CurrentTick = CurrentTick - 1; // the processor advances it before running the phases
        await db.SaveChangesAsync();
    }

    private static void AddRecord(AppDbContext db, Guid cityId, Guid productId, long tick, decimal quantity, decimal pricePerUnit)
        => db.PublicSalesRecords.Add(new PublicSalesRecord
        {
            Id = Guid.NewGuid(), BuildingUnitId = Guid.NewGuid(), BuildingId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), CityId = cityId,
            ProductTypeId = productId, Tick = tick, QuantitySold = quantity, PricePerUnit = pricePerUnit, Revenue = quantity * pricePerUnit,
        });

    private static async Task<ProductType> ProductAsync(AppDbContext db, int skip = 0)
        => await db.ProductTypes.AsNoTracking().OrderBy(p => p.Name).Skip(skip).FirstAsync();

    private static async Task<List<PlayerNotification>> NotificationsAsync(ApiWebApplicationFactory factory, Guid playerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PlayerNotifications.AsNoTracking().Where(n => n.PlayerId == playerId).ToListAsync();
    }

    private static async Task RunTickAsync(ApiWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        _ = await scope.ServiceProvider.GetRequiredService<TickProcessor>().ProcessTickAsync();
    }

    private async Task<Seller> ArrangeAsync(
        ApiWebApplicationFactory factory,
        int population,
        decimal stock,
        Action<AppDbContext, Seller, ProductType, ProductType, City>? seedRecords = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await CreateCityAsync(db, $"Signal City {Guid.NewGuid():N}", population);
        var product = await ProductAsync(db);
        var otherProduct = await ProductAsync(db, skip: 1);
        var seller = await CreateSellerAsync(db, city, product, stock);
        await SetTickAsync(db);
        seedRecords?.Invoke(db, seller, product, otherProduct, city);
        await db.SaveChangesAsync();
        return seller;
    }

    [Fact]
    public async Task PriceSpike_IsReportedWhenThePriceJumpedMoreThanThirtyPercentSinceThePreviousTick()
    {
        await using var factory = new ApiWebApplicationFactory();
        var seller = await ArrangeAsync(factory, population: 400_000, stock: 5_000m,
            (db, s, product, _, city) => AddRecord(db, city.Id, product.Id, CurrentTick - 1, quantity: 100m, pricePerUnit: s.Price * 0.5m));

        await RunTickAsync(factory);

        var spike = Assert.Single(await NotificationsAsync(factory, seller.PlayerId), n => n.Type == PlayerNotificationType.PriceSpike);
        Assert.Equal(seller.CompanyId, spike.CompanyId);
        Assert.Equal(seller.ProductId, spike.RelatedEntityId);
    }

    [Fact]
    public async Task NoPriceSpike_WhenThePreviousTickPaidTheSamePrice()
    {
        await using var factory = new ApiWebApplicationFactory();
        var seller = await ArrangeAsync(factory, population: 400_000, stock: 5_000m,
            (db, s, product, _, city) => AddRecord(db, city.Id, product.Id, CurrentTick - 1, quantity: 100m, pricePerUnit: s.Price));

        await RunTickAsync(factory);

        Assert.DoesNotContain(await NotificationsAsync(factory, seller.PlayerId), n => n.Type == PlayerNotificationType.PriceSpike);
    }

    [Fact]
    public async Task OnlyThePreviousTickCounts_AndOnlyForTheSameCityAndProduct()
    {
        await using var factory = new ApiWebApplicationFactory();
        var seller = await ArrangeAsync(factory, population: 400_000, stock: 5_000m, (db, s, product, otherProduct, city) =>
        {
            AddRecord(db, city.Id, product.Id, CurrentTick - 2, quantity: 100m, pricePerUnit: s.Price * 0.1m); // two ticks ago
            AddRecord(db, city.Id, otherProduct.Id, CurrentTick - 1, quantity: 100m, pricePerUnit: s.Price * 0.1m); // another product
            AddRecord(db, Guid.NewGuid(), product.Id, CurrentTick - 1, quantity: 100m, pricePerUnit: s.Price * 0.1m); // another city
        });

        await RunTickAsync(factory);

        Assert.DoesNotContain(await NotificationsAsync(factory, seller.PlayerId), n => n.Type == PlayerNotificationType.PriceSpike);
    }

    [Fact]
    public async Task PreviousTickAverage_IsQuantityWeightedAcrossAllRecordsOfThatTick()
    {
        await using var factory = new ApiWebApplicationFactory();
        // Previous average = (100 x 0.4p + 900 x 1.0p) / 1000 = 0.94p -> no spike. A plain mean of the two prices (0.7p) would spike.
        var seller = await ArrangeAsync(factory, population: 400_000, stock: 5_000m, (db, s, product, _, city) =>
        {
            AddRecord(db, city.Id, product.Id, CurrentTick - 1, quantity: 100m, pricePerUnit: s.Price * 0.4m);
            AddRecord(db, city.Id, product.Id, CurrentTick - 1, quantity: 900m, pricePerUnit: s.Price);
        });

        await RunTickAsync(factory);

        Assert.DoesNotContain(await NotificationsAsync(factory, seller.PlayerId), n => n.Type == PlayerNotificationType.PriceSpike);
    }

    [Fact]
    public async Task UnmetDemand_IsReportedWhenTheShopCanServeLessThanAThirdOfTheCityDemand()
    {
        await using var factory = new ApiWebApplicationFactory();
        var seller = await ArrangeAsync(factory, population: 5_000_000, stock: 2m);

        await RunTickAsync(factory);

        var warning = Assert.Single(await NotificationsAsync(factory, seller.PlayerId), n => n.Type == PlayerNotificationType.OversupplyWarning);
        Assert.Equal(seller.ProductId, warning.RelatedEntityId);
    }

    [Fact]
    public async Task NoUnmetDemandWarning_WhenTheShopServesTheWholeSmallCity()
    {
        await using var factory = new ApiWebApplicationFactory();
        var seller = await ArrangeAsync(factory, population: 1_000, stock: 5_000m);

        await RunTickAsync(factory);

        Assert.DoesNotContain(await NotificationsAsync(factory, seller.PlayerId), n => n.Type == PlayerNotificationType.OversupplyWarning);
    }
}

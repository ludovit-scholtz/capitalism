using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Pins what <c>NpcCompanyPhase</c> does to an NPC's sales shops (starter units, starter stock, market-following price), so the
/// phase can read the tick context and aggregate its market data in one query without changing a decision (#646).
/// </summary>
public sealed class NpcShopBehaviourTests
{
    private const long CurrentTick = 500;
    private const decimal RetailerPriceModifier = -0.08m;

    private sealed record NpcFixture(NpcCompany Npc, Company Company, City City);

    private static async Task<City> CreateCityAsync(AppDbContext db, string name)
    {
        var city = new City
        {
            Id = Guid.NewGuid(), Name = name, CountryCode = "TS", CurrencyCode = "EUR", Latitude = 48.3, Longitude = 17.3,
            Population = 50_000, AverageRentPerSqm = 20m, BaseSalaryPerManhour = 10m,
        };
        db.Cities.Add(city);
        await db.SaveChangesAsync();
        return city;
    }

    private static async Task<NpcFixture> CreateRetailerAsync(AppDbContext db, City city, int shopCount)
    {
        var player = new Player
        {
            Id = Guid.NewGuid(), Email = $"npc-shop-{Guid.NewGuid():N}@test.local", DisplayName = "NPC Shop", PasswordHash = "mock",
            Role = PlayerRole.Player, ActiveAccountType = AccountContextType.Company, CreatedAtUtc = DateTime.UtcNow,
            OnboardingCompletedAtUtc = DateTime.UtcNow,
        };
        var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = $"NPC Retailer {Guid.NewGuid():N}", FoundedAtUtc = DateTime.UtcNow, FoundedAtTick = 1 };
        player.ActiveCompanyId = company.Id;
        db.Players.Add(player);
        db.Companies.Add(company);
        // Below the expansion threshold's building cap logic is irrelevant here: keep the NPC poor so it never expands.
        var account = await BuildingBankAccountProvisioning.EnsureCompanyCurrencyAccountAsync(db, company.Id, city.CurrencyCode);
        account.Balance = 1_000m;

        var npc = new NpcCompany
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, HomeCityId = city.Id, Name = company.Name, Archetype = NpcArchetype.Retailer,
            DifficultyLevel = 2, IsActive = true, CreatedAtUtc = DateTime.UtcNow,
        };
        db.NpcCompanies.Add(npc);

        for (var i = 0; i < shopCount; i++)
        {
            db.Buildings.Add(new Building
            {
                Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, Type = BuildingType.SalesShop, Name = $"Shop {i}",
                Latitude = city.Latitude, Longitude = city.Longitude, BuiltAtUtc = DateTime.UtcNow, Level = 1,
            });
        }

        await db.SaveChangesAsync();
        return new NpcFixture(npc, company, city);
    }

    private static async Task SetTickAsync(AppDbContext db)
    {
        var state = await db.GameStates.OrderBy(s => s.Id).FirstAsync();
        state.CurrentTick = CurrentTick - 1; // the processor advances it before running the phases
        await db.SaveChangesAsync();
    }

    private static async Task<ProductType> StarterProductAsync(AppDbContext db)
        => await db.ProductTypes.AsNoTracking().OrderBy(p => p.Name).FirstAsync();

    private static void AddRecord(AppDbContext db, Guid cityId, Guid productId, long tick, decimal quantity, decimal revenue)
        => db.PublicSalesRecords.Add(new PublicSalesRecord
        {
            Id = Guid.NewGuid(), BuildingUnitId = Guid.NewGuid(), BuildingId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), CityId = cityId,
            ProductTypeId = productId, Tick = tick, QuantitySold = quantity, PricePerUnit = quantity == 0 ? 0 : revenue / quantity, Revenue = revenue,
        });

    private static async Task RunTickAsync(ApiWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var tick = scope.ServiceProvider.GetRequiredService<TickProcessor>();
        _ = await tick.ProcessTickAsync();
    }

    [Fact]
    public async Task NewShop_GetsStarterUnitsStarterStockAndAPriceNearTheProductBasePrice()
    {
        await using var factory = new ApiWebApplicationFactory();
        int shopCount;
        NpcFixture npc;
        ProductType starter;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            npc = await CreateRetailerAsync(db, await CreateCityAsync(db, "Npc Shop City A"), shopCount: 2);
            starter = await StarterProductAsync(db);
            await SetTickAsync(db);
            shopCount = 2;
        }

        await RunTickAsync(factory);

        await using var verify = factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var shops = await verifyDb.Buildings.AsNoTracking().Include(b => b.Units).Where(b => b.CompanyId == npc.Company.Id).ToListAsync();
        Assert.Equal(shopCount, shops.Count);
        foreach (var shop in shops)
        {
            var purchase = Assert.Single(shop.Units, u => u.UnitType == UnitType.Purchase);
            var sales = Assert.Single(shop.Units, u => u.UnitType == UnitType.PublicSales);
            Assert.Equal(starter.Id, purchase.ProductTypeId);
            Assert.Equal("OPTIMAL", purchase.PurchaseSource);
            Assert.True(purchase.LinkRight);
            Assert.Equal(starter.Id, sales.ProductTypeId);
            Assert.Equal(decimal.Round(starter.BasePrice * (1m + RetailerPriceModifier), 2, MidpointRounding.AwayFromZero), sales.MinPrice);

            var stock = await verifyDb.Inventories.AsNoTracking().SingleAsync(i => i.BuildingUnitId == sales.Id);
            Assert.Equal(starter.Id, stock.ProductTypeId);
            Assert.Equal(80m, stock.Quantity);
            Assert.Equal(0.55m, stock.Quality);
        }

        var priceLogs = await verifyDb.NpcDecisionLogs.CountAsync(l => l.NpcCompanyId == npc.Npc.Id && l.ActionType == "PRICE_SET");
        Assert.Equal(shopCount, priceLogs);
    }

    [Fact]
    public async Task Price_FollowsTheCityMarketAverageOfTheLastHundredTicks()
    {
        await using var factory = new ApiWebApplicationFactory();
        NpcFixture npc;
        ProductType starter;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var city = await CreateCityAsync(db, "Npc Shop City B");
            var otherCity = await CreateCityAsync(db, "Npc Shop City B2");
            npc = await CreateRetailerAsync(db, city, shopCount: 1);
            starter = await StarterProductAsync(db);
            var otherProduct = await db.ProductTypes.AsNoTracking().Where(p => p.Id != starter.Id).OrderBy(p => p.Name).FirstAsync();
            await SetTickAsync(db);

            AddRecord(db, city.Id, starter.Id, CurrentTick - 1, quantity: 10m, revenue: 120m); // counts
            AddRecord(db, city.Id, starter.Id, CurrentTick - 50, quantity: 30m, revenue: 400m); // counts
            AddRecord(db, city.Id, starter.Id, CurrentTick - 100, quantity: 1_000m, revenue: 1m); // exactly 100 ticks back: outside the window
            AddRecord(db, city.Id, starter.Id, 5, quantity: 1_000m, revenue: 1m); // long ago
            AddRecord(db, otherCity.Id, starter.Id, CurrentTick - 1, quantity: 1_000m, revenue: 9_000_000m); // another city
            AddRecord(db, city.Id, otherProduct.Id, CurrentTick - 1, quantity: 1_000m, revenue: 9_000_000m); // another product
            await db.SaveChangesAsync();
        }

        await RunTickAsync(factory);

        await using var verify = factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var shop = await verifyDb.Buildings.AsNoTracking().Include(b => b.Units).SingleAsync(b => b.CompanyId == npc.Company.Id);
        var sales = Assert.Single(shop.Units, u => u.UnitType == UnitType.PublicSales);
        var marketAverage = (120m + 400m) / (10m + 30m);
        Assert.Equal(decimal.Round(marketAverage * (1m + RetailerPriceModifier), 2, MidpointRounding.AwayFromZero), sales.MinPrice);
    }

    [Fact]
    public async Task ExistingShopStock_IsToppedUpOnlyBelowTheThreshold()
    {
        await using var factory = new ApiWebApplicationFactory();
        NpcFixture npc;
        Guid lowUnitId;
        Guid fullUnitId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            npc = await CreateRetailerAsync(db, await CreateCityAsync(db, "Npc Shop City C"), shopCount: 2);
            var starter = await StarterProductAsync(db);
            await SetTickAsync(db);
            var shops = await db.Buildings.Where(b => b.CompanyId == npc.Company.Id).OrderBy(b => b.Name).ToListAsync();
            lowUnitId = Guid.NewGuid();
            fullUnitId = Guid.NewGuid();
            var units = new[] { (shops[0], lowUnitId, 10m), (shops[1], fullUnitId, 60m) };
            foreach (var (shop, unitId, quantity) in units)
            {
                // An extreme price keeps public sales (earlier in the tick) from touching the stock; no purchase unit exists yet, so
                // nothing buys either. The NPC phase, which runs last, creates the purchase unit and sets the real price.
                db.BuildingUnits.Add(new BuildingUnit { Id = unitId, BuildingId = shop.Id, UnitType = UnitType.PublicSales, GridX = 1, GridY = 0, Level = 1, ProductTypeId = starter.Id, MinPrice = 1_000_000m });
                db.Inventories.Add(new Inventory { Id = Guid.NewGuid(), BuildingId = shop.Id, BuildingUnitId = unitId, ProductTypeId = starter.Id, Quantity = quantity, Quality = 0.5m });
            }

            await db.SaveChangesAsync();
        }

        await RunTickAsync(factory);

        await using var verify = factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(50m, (await verifyDb.Inventories.AsNoTracking().SingleAsync(i => i.BuildingUnitId == lowUnitId)).Quantity);
        Assert.Equal(60m, (await verifyDb.Inventories.AsNoTracking().SingleAsync(i => i.BuildingUnitId == fullUnitId)).Quantity);
        var unitCounts = await verifyDb.BuildingUnits.AsNoTracking().Where(u => u.Building.CompanyId == npc.Company.Id).GroupBy(u => u.UnitType).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        Assert.Equal(2, unitCounts.Count);
        Assert.All(unitCounts, g => Assert.Equal(2, g.Count)); // one purchase and one sales unit per shop: the existing sales unit is reused, not duplicated
    }

    [Fact]
    public async Task SeveralNpcs_EachConfigureOnlyTheirOwnShops()
    {
        await using var factory = new ApiWebApplicationFactory();
        NpcFixture first;
        NpcFixture second;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var city = await CreateCityAsync(db, "Npc Shop City D");
            first = await CreateRetailerAsync(db, city, shopCount: 2);
            second = await CreateRetailerAsync(db, city, shopCount: 3);
            await SetTickAsync(db);
        }

        await RunTickAsync(factory);

        await using var verify = factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var (npc, expectedShops) in new[] { (first, 2), (second, 3) })
        {
            var shops = await verifyDb.Buildings.AsNoTracking().Include(b => b.Units).Where(b => b.CompanyId == npc.Company.Id).ToListAsync();
            Assert.Equal(expectedShops, shops.Count);
            Assert.All(shops, shop =>
            {
                Assert.Single(shop.Units, u => u.UnitType == UnitType.Purchase);
                Assert.Single(shop.Units, u => u.UnitType == UnitType.PublicSales);
            });
            Assert.Equal(expectedShops, await verifyDb.NpcDecisionLogs.CountAsync(l => l.NpcCompanyId == npc.Npc.Id && l.ActionType == "PRICE_SET"));
        }
    }
}

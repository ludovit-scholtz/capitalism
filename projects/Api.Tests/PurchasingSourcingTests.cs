using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Pins where a purchase unit buys from (same-company B2B supply in its city, a vendor-locked company's supply, player exchange
/// orders) so the lookups can use indexes instead of scanning every unit and order in the world for every purchase unit (#646: at
/// 20,000 buildings the phase took 50 s per tick).
/// </summary>
public sealed class PurchasingSourcingTests
{
    private const decimal Capacity = 50m; // level-1 purchase unit

    private sealed class World
    {
        public required Guid CityId { get; init; }
        public required Guid OtherCityId { get; init; }
        public required Guid ResourceId { get; init; }
        public required Guid OtherResourceId { get; init; }
    }

    private sealed record Party(Guid PlayerId, Guid CompanyId, Guid AccountId);

    private sealed record Buyer(Building Building, BuildingUnit Unit);

    private sealed record Seller(Building Building, BuildingUnit Unit, Inventory Stock);

    private static async Task<World> CreateWorldAsync(AppDbContext db)
    {
        City NewCity(string name, double lat) => new()
        {
            Id = Guid.NewGuid(), Name = $"{name} {Guid.NewGuid():N}", CountryCode = "TS", CurrencyCode = "EUR", Latitude = lat, Longitude = 17.3,
            Population = 50_000, AverageRentPerSqm = 20m, BaseSalaryPerManhour = 10m,
        };

        var city = NewCity("Sourcing", 48.3);
        var other = NewCity("Elsewhere", 49.3);
        db.Cities.AddRange(city, other);
        var resources = await db.ResourceTypes.AsNoTracking().OrderBy(r => r.Name).Take(2).ToListAsync();
        await db.SaveChangesAsync();
        return new World { CityId = city.Id, OtherCityId = other.Id, ResourceId = resources[0].Id, OtherResourceId = resources[1].Id };
    }

    private static Party NewParty(AppDbContext db, string name)
    {
        var player = new Player { Id = Guid.NewGuid(), Email = $"sourcing-{Guid.NewGuid():N}@test.local", DisplayName = name, PasswordHash = "x", Role = PlayerRole.Player, OnboardingCompletedAtUtc = DateTime.UtcNow };
        var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = name, FoundedAtUtc = DateTime.UtcNow, FoundedAtTick = 1 };
        var account = new BankAccount
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CurrencyCode = "EUR", Balance = 5_000_000m, CreatedAtUtc = DateTime.UtcNow,
            AccountNumber = Random.Shared.NextInt64(7_400_000_000_000_000, 7_499_999_999_999_999).ToString(),
        };
        db.Players.Add(player);
        db.Companies.Add(company);
        db.BankAccounts.Add(account);
        return new Party(player.Id, company.Id, account.Id);
    }

    private static Building NewBuilding(AppDbContext db, Party owner, Guid cityId, string name, double latitude)
    {
        var building = new Building
        {
            Id = Guid.NewGuid(), CompanyId = owner.CompanyId, CityId = cityId, Type = BuildingType.Factory, Name = name, Latitude = latitude, Longitude = 17.3,
            BuiltAtUtc = DateTime.UtcNow, Level = 1, BankAccountId = owner.AccountId,
        };
        db.Buildings.Add(building);
        return building;
    }

    private static Buyer AddBuyer(AppDbContext db, Party owner, World world, string source = "LOCAL", decimal? minQuality = null, Guid? vendorLock = null)
    {
        var building = NewBuilding(db, owner, world.CityId, "Buyer", 48.3);
        var unit = new BuildingUnit
        {
            Id = Guid.NewGuid(), BuildingId = building.Id, UnitType = UnitType.Purchase, GridX = 0, GridY = 0, Level = 1, ResourceTypeId = world.ResourceId,
            PurchaseSource = source, MinQuality = minQuality, VendorLockCompanyId = vendorLock,
        };
        db.BuildingUnits.Add(unit);
        return new Buyer(building, unit);
    }

    private static Seller AddSeller(AppDbContext db, Party owner, Guid cityId, Guid resourceId, decimal price, decimal quality = 0.8m, decimal stock = 500m, double latitude = 48.3)
    {
        var building = NewBuilding(db, owner, cityId, "Seller", latitude);
        var unit = new BuildingUnit
        {
            Id = Guid.NewGuid(), BuildingId = building.Id, UnitType = UnitType.B2BSales, GridX = 0, GridY = 0, Level = 1, ResourceTypeId = resourceId, MinPrice = price,
        };
        var inventory = new Inventory { Id = Guid.NewGuid(), BuildingId = building.Id, BuildingUnitId = unit.Id, ResourceTypeId = resourceId, Quantity = stock, Quality = quality };
        db.BuildingUnits.Add(unit);
        db.Inventories.Add(inventory);
        return new Seller(building, unit, inventory);
    }

    private static async Task RunAsync(ApiWebApplicationFactory factory, Func<AppDbContext, World, Task> arrange, Func<AppDbContext, World, Task> assert)
    {
        World world;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            world = await CreateWorldAsync(db);
            await arrange(db, world);
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            _ = await scope.ServiceProvider.GetRequiredService<TickProcessor>().ProcessTickAsync();
        }

        await using var verify = factory.Services.CreateAsyncScope();
        await assert(verify.ServiceProvider.GetRequiredService<AppDbContext>(), world);
    }

    private static async Task<decimal> StockOfAsync(AppDbContext db, Guid unitId)
        => await db.Inventories.AsNoTracking().Where(i => i.BuildingUnitId == unitId).SumAsync(i => (decimal?)i.Quantity) ?? 0m;

    // ── Local B2B supply ─────────────────────────────────────────────────────

    [Fact]
    public async Task ASellerOfTheSameCompanyInTheSameCity_IsBoughtFromUpToThePurchaseCapacity()
    {
        await using var factory = new ApiWebApplicationFactory();
        Buyer buyer = null!;
        Seller seller = null!;
        await RunAsync(factory,
            (db, world) =>
            {
                var company = NewParty(db, "Own Co");
                buyer = AddBuyer(db, company, world);
                seller = AddSeller(db, company, world.CityId, world.ResourceId, price: 4m);
                return Task.CompletedTask;
            },
            async (db, _) =>
            {
                Assert.Equal(Capacity, await StockOfAsync(db, buyer.Unit.Id));
                Assert.Equal(500m - Capacity, await StockOfAsync(db, seller.Unit.Id));
                Assert.True(await db.LedgerEntries.AnyAsync(e => e.BuildingUnitId == buyer.Unit.Id && e.Category == LedgerCategory.PurchasingCost));
            });
    }

    [Fact]
    public async Task ASellerInAnotherCity_IsIgnored()
    {
        await using var factory = new ApiWebApplicationFactory();
        Buyer buyer = null!;
        Seller seller = null!;
        await RunAsync(factory,
            (db, world) =>
            {
                var company = NewParty(db, "Own Co");
                buyer = AddBuyer(db, company, world);
                seller = AddSeller(db, company, world.OtherCityId, world.ResourceId, price: 4m, latitude: 49.3);
                return Task.CompletedTask;
            },
            async (db, _) =>
            {
                Assert.Equal(0m, await StockOfAsync(db, buyer.Unit.Id));
                Assert.Equal(500m, await StockOfAsync(db, seller.Unit.Id));
            });
    }

    [Fact]
    public async Task ASellerOfAnotherCompany_IsIgnoredWithoutAVendorLock()
    {
        await using var factory = new ApiWebApplicationFactory();
        Buyer buyer = null!;
        Seller seller = null!;
        await RunAsync(factory,
            (db, world) =>
            {
                var buyerCompany = NewParty(db, "Buyer Co");
                var sellerCompany = NewParty(db, "Seller Co");
                buyer = AddBuyer(db, buyerCompany, world);
                seller = AddSeller(db, sellerCompany, world.CityId, world.ResourceId, price: 4m);
                return Task.CompletedTask;
            },
            async (db, _) =>
            {
                Assert.Equal(0m, await StockOfAsync(db, buyer.Unit.Id));
                Assert.Equal(500m, await StockOfAsync(db, seller.Unit.Id));
            });
    }

    [Fact]
    public async Task AVendorLock_BuysOnlyFromThatCompany_NotFromTheBuyersOwnSellers()
    {
        await using var factory = new ApiWebApplicationFactory();
        Buyer buyer = null!;
        Seller own = null!;
        Seller locked = null!;
        await RunAsync(factory,
            (db, world) =>
            {
                var buyerCompany = NewParty(db, "Buyer Co");
                var vendor = NewParty(db, "Vendor Co");
                buyer = AddBuyer(db, buyerCompany, world, vendorLock: vendor.CompanyId);
                own = AddSeller(db, buyerCompany, world.CityId, world.ResourceId, price: 1m);
                locked = AddSeller(db, vendor, world.CityId, world.ResourceId, price: 9m);
                return Task.CompletedTask;
            },
            async (db, _) =>
            {
                Assert.Equal(Capacity, await StockOfAsync(db, buyer.Unit.Id));
                Assert.Equal(500m - Capacity, await StockOfAsync(db, locked.Unit.Id));
                Assert.Equal(500m, await StockOfAsync(db, own.Unit.Id));
            });
    }

    [Fact]
    public async Task TheCheapestDeliveredPriceIsBoughtFirst()
    {
        await using var factory = new ApiWebApplicationFactory();
        Buyer buyer = null!;
        Seller dear = null!;
        Seller cheap = null!;
        await RunAsync(factory,
            (db, world) =>
            {
                var company = NewParty(db, "Own Co");
                buyer = AddBuyer(db, company, world);
                dear = AddSeller(db, company, world.CityId, world.ResourceId, price: 9m);
                cheap = AddSeller(db, company, world.CityId, world.ResourceId, price: 5m);
                return Task.CompletedTask;
            },
            async (db, _) =>
            {
                Assert.Equal(Capacity, await StockOfAsync(db, buyer.Unit.Id));
                Assert.Equal(500m - Capacity, await StockOfAsync(db, cheap.Unit.Id));
                Assert.Equal(500m, await StockOfAsync(db, dear.Unit.Id));
            });
    }

    [Fact]
    public async Task SuppliesBelowTheMinimumQuality_AreSkipped()
    {
        await using var factory = new ApiWebApplicationFactory();
        Buyer buyer = null!;
        Seller poor = null!;
        Seller good = null!;
        await RunAsync(factory,
            (db, world) =>
            {
                var company = NewParty(db, "Own Co");
                buyer = AddBuyer(db, company, world, minQuality: 0.9m);
                poor = AddSeller(db, company, world.CityId, world.ResourceId, price: 1m, quality: 0.8m);
                good = AddSeller(db, company, world.CityId, world.ResourceId, price: 8m, quality: 0.95m);
                return Task.CompletedTask;
            },
            async (db, _) =>
            {
                Assert.Equal(500m, await StockOfAsync(db, poor.Unit.Id));
                Assert.Equal(500m - Capacity, await StockOfAsync(db, good.Unit.Id));
            });
    }

    [Fact]
    public async Task ASellerOfADifferentResource_IsIgnored()
    {
        await using var factory = new ApiWebApplicationFactory();
        Buyer buyer = null!;
        Seller other = null!;
        await RunAsync(factory,
            (db, world) =>
            {
                var company = NewParty(db, "Own Co");
                buyer = AddBuyer(db, company, world);
                other = AddSeller(db, company, world.CityId, world.OtherResourceId, price: 1m);
                return Task.CompletedTask;
            },
            async (db, _) =>
            {
                Assert.Equal(0m, await StockOfAsync(db, buyer.Unit.Id));
                Assert.Equal(500m, await StockOfAsync(db, other.Unit.Id));
            });
    }

    // ── Player exchange orders ───────────────────────────────────────────────

    private static ExchangeOrder SellOrder(Building exchange, Party seller, Guid resourceId, decimal price, decimal quantity, bool active = true)
        => new()
        {
            Id = Guid.NewGuid(), ExchangeBuildingId = exchange.Id, CompanyId = seller.CompanyId, Side = "SELL", ResourceTypeId = resourceId, PricePerUnit = price,
            Quantity = quantity, RemainingQuantity = quantity, IsActive = active,
        };

    [Fact]
    public async Task ExchangeSellOrders_AreFilledCheapestFirst_AndInactiveOrOtherResourcesAreIgnored()
    {
        await using var factory = new ApiWebApplicationFactory();
        Buyer buyer = null!;
        ExchangeOrder dear = null!;
        ExchangeOrder cheap = null!;
        ExchangeOrder inactive = null!;
        ExchangeOrder otherResource = null!;
        await RunAsync(factory,
            (db, world) =>
            {
                var buyerCompany = NewParty(db, "Buyer Co");
                var sellerCompany = NewParty(db, "Order Co");
                buyer = AddBuyer(db, buyerCompany, world);
                var exchange = NewBuilding(db, sellerCompany, world.CityId, "Exchange", 48.3);
                dear = SellOrder(exchange, sellerCompany, world.ResourceId, price: 9m, quantity: 100m);
                cheap = SellOrder(exchange, sellerCompany, world.ResourceId, price: 5m, quantity: 30m);
                inactive = SellOrder(exchange, sellerCompany, world.ResourceId, price: 1m, quantity: 100m, active: false);
                otherResource = SellOrder(exchange, sellerCompany, world.OtherResourceId, price: 1m, quantity: 100m);
                db.ExchangeOrders.AddRange(dear, cheap, inactive, otherResource);
                return Task.CompletedTask;
            },
            async (db, _) =>
            {
                var orders = await db.ExchangeOrders.AsNoTracking().ToDictionaryAsync(o => o.Id);
                Assert.Equal(0m, orders[cheap.Id].RemainingQuantity); // 30 filled completely
                Assert.False(orders[cheap.Id].IsActive);
                Assert.Equal(100m - (Capacity - 30m), orders[dear.Id].RemainingQuantity); // the remaining 20 of the 50 came from the dearer order
                Assert.Equal(100m, orders[inactive.Id].RemainingQuantity);
                Assert.Equal(100m, orders[otherResource.Id].RemainingQuantity);
                Assert.Equal(Capacity, await StockOfAsync(db, buyer.Unit.Id));
            });
    }

    [Fact]
    public async Task AVendorLock_RestrictsExchangeOrdersToThatCompany()
    {
        await using var factory = new ApiWebApplicationFactory();
        Buyer buyer = null!;
        ExchangeOrder lockedVendor = null!;
        ExchangeOrder stranger = null!;
        await RunAsync(factory,
            (db, world) =>
            {
                var buyerCompany = NewParty(db, "Buyer Co");
                var vendor = NewParty(db, "Vendor Co");
                var strangerCompany = NewParty(db, "Stranger Co");
                buyer = AddBuyer(db, buyerCompany, world, vendorLock: vendor.CompanyId);
                var exchange = NewBuilding(db, vendor, world.CityId, "Exchange", 48.3);
                lockedVendor = SellOrder(exchange, vendor, world.ResourceId, price: 9m, quantity: 100m);
                stranger = SellOrder(exchange, strangerCompany, world.ResourceId, price: 1m, quantity: 100m);
                db.ExchangeOrders.AddRange(lockedVendor, stranger);
                return Task.CompletedTask;
            },
            async (db, _) =>
            {
                var orders = await db.ExchangeOrders.AsNoTracking().ToDictionaryAsync(o => o.Id);
                Assert.Equal(100m - Capacity, orders[lockedVendor.Id].RemainingQuantity);
                Assert.Equal(100m, orders[stranger.Id].RemainingQuantity);
            });
    }
}

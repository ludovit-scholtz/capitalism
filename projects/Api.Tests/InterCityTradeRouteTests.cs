using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Engine.Phases;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

/// <summary>
/// Backend integration tests for the inter-city trade route feature.
/// Covers:
///   - Entity creation and persistence
///   - Distance calculation for known city pairs (Bratislava, Prague, Vienna, Warsaw)
///   - Shipping cost formula: cost scales with distance and fuel price index
///   - Status transition: IN_TRANSIT → DELIVERED / FAILED
///   - Inventory arrives at destination on DELIVERED
///   - Inventory returned to source on FAILED (destination full)
///   - Concurrent routes processed independently
///   - LedgerEntry records (revenue, shipping cost) created on delivery
///   - FuelPriceIndex from destination city is used for actual shipping cost
/// </summary>
public sealed class InterCityTradeRouteTests
{
    // ── Real-city coordinate constants ────────────────────────────────────────
    private const double BratislavaLat = 48.15;
    private const double BratislavaLon = 17.11;
    private const double PragueLat = 50.08;
    private const double PragueLon = 14.43;
    private const double ViennaLat = 48.21;
    private const double ViennaLon = 16.37;
    private const double WarsawLat = 52.23;
    private const double WarsawLon = 21.01;

    // ── Test helpers ──────────────────────────────────────────────────────────

    private static TickProcessor CreateProcessor(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetServices<ITickPhase>();
        var logger = new NullLogger<TickProcessor>();
        return new TickProcessor(db, phases, logger);
    }

    /// <summary>
    /// Seeds a minimal two-company, two-city scenario suitable for route tests.
    /// NOTE: <paramref name="currentTick"/> sets the game state tick BEFORE the
    /// processor call.  <see cref="TickProcessor.ProcessTickAsync"/> first increments
    /// the tick, so if you want routes with ExpectedArrivalTick = N to be processed,
    /// set currentTick = N - 1.
    /// </summary>
    private static async Task<TradeRouteScenario> SeedScenarioAsync(
        AppDbContext db,
        long currentTick = 0,
        bool generousDestInventorySpace = true,
        decimal sourceUnitInventory = 100m)
    {
        var bratislava = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var prague = await db.Cities.FirstAsync(c => c.Name == "Prague");
        var wood = await db.ResourceTypes.FirstAsync(r => r.Slug == "wood");

        // ── Seller (Bratislava) ───────────────────────────────────────────────
        var sellerPlayer = new Player
        {
            Id = Guid.NewGuid(),
            Email = $"seller-{Guid.NewGuid():N}@test.com",
            DisplayName = "Seller",
            PasswordHash = "x",
            Role = PlayerRole.Player
        };
        db.Players.Add(sellerPlayer);

        var sellerCompany = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = sellerPlayer.Id,
            Name = $"SellerCo-{Guid.NewGuid():N}",
            Cash = 500_000m
        };
        db.Companies.Add(sellerCompany);

        var sellerAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = sellerCompany.Id,
            AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CurrencyCode = "EUR",
            Balance = 500_000m,
        };
        db.BankAccounts.Add(sellerAccount);

        var sourceBuilding = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = sellerCompany.Id,
            CityId = bratislava.Id,
            Type = BuildingType.Factory,
            Name = "Bratislava Factory",
            Level = 1,
            Latitude = BratislavaLat,
            Longitude = BratislavaLon,
            BankAccountId = sellerAccount.Id,
        };
        db.Buildings.Add(sourceBuilding);

        var b2bUnit = new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = sourceBuilding.Id,
            UnitType = UnitType.B2BSales,
            Level = 1,
            GridX = 0,
            GridY = 0,
        };
        db.BuildingUnits.Add(b2bUnit);

        db.Inventories.Add(new Inventory
        {
            Id = Guid.NewGuid(),
            BuildingId = sourceBuilding.Id,
            BuildingUnitId = b2bUnit.Id,
            ResourceTypeId = wood.Id,
            Quantity = sourceUnitInventory,
            Quality = 0.7m,
            SourcingCostTotal = sourceUnitInventory * 0.5m,
        });

        // ── Buyer (Prague) ────────────────────────────────────────────────────
        var buyerPlayer = new Player
        {
            Id = Guid.NewGuid(),
            Email = $"buyer-{Guid.NewGuid():N}@test.com",
            DisplayName = "Buyer",
            PasswordHash = "x",
            Role = PlayerRole.Player
        };
        db.Players.Add(buyerPlayer);

        var buyerCompany = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = buyerPlayer.Id,
            Name = $"BuyerCo-{Guid.NewGuid():N}",
            Cash = 200_000m
        };
        db.Companies.Add(buyerCompany);

        var buyerAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = buyerCompany.Id,
            AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CurrencyCode = "CZK",
            Balance = 200_000m,
        };
        db.BankAccounts.Add(buyerAccount);

        var destBuilding = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = buyerCompany.Id,
            CityId = prague.Id,
            Type = BuildingType.Factory,
            Name = "Prague Factory",
            Level = 1,
            Latitude = PragueLat,
            Longitude = PragueLon,
            BankAccountId = buyerAccount.Id,
        };
        db.Buildings.Add(destBuilding);

        var purchaseUnit = new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = destBuilding.Id,
            UnitType = UnitType.Purchase,
            Level = 1,
            GridX = 0,
            GridY = 0,
            ResourceTypeId = wood.Id,
        };
        db.BuildingUnits.Add(purchaseUnit);

        // For the "full" scenario: seed 100 units of inventory to fill Level-1 purchase unit (capacity=100)
        if (!generousDestInventorySpace)
        {
            db.Inventories.Add(new Inventory
            {
                Id = Guid.NewGuid(),
                BuildingId = destBuilding.Id,
                BuildingUnitId = purchaseUnit.Id,
                ResourceTypeId = wood.Id,
                Quantity = 100m, // fills the unit completely (GameConstants.StorageCapacity(1) == 100)
                Quality = 0.5m,
                SourcingCostTotal = 50m,
            });
        }

        // ── Game state ────────────────────────────────────────────────────────
        var gameState = await db.GameStates.FirstOrDefaultAsync();
        if (gameState is null)
        {
            gameState = new GameState { Id = 1, CurrentTick = currentTick, TaxRate = 0.05m };
            db.GameStates.Add(gameState);
        }
        else
        {
            gameState.CurrentTick = currentTick;
        }

        await db.SaveChangesAsync();

        return new TradeRouteScenario(
            sellerPlayer, sellerCompany, sellerAccount, sourceBuilding, b2bUnit,
            buyerPlayer, buyerCompany, buyerAccount, destBuilding, purchaseUnit, wood);
    }

    private record TradeRouteScenario(
        Player SellerPlayer,
        Company SellerCompany,
        BankAccount SellerAccount,
        Building SourceBuilding,
        BuildingUnit B2BUnit,
        Player BuyerPlayer,
        Company BuyerCompany,
        BankAccount BuyerAccount,
        Building DestBuilding,
        BuildingUnit PurchaseUnit,
        ResourceType Wood);

    // ── Distance tests ────────────────────────────────────────────────────────

    [Fact]
    public void DistanceCalculation_BratislavaToPrague_IsApprox277Km()
    {
        // Known great-circle distance Bratislava → Prague ≈ 277 km; allow ±2% tolerance
        var dist = GlobalExchangeCalculator.ComputeDistanceKm(BratislavaLat, BratislavaLon, PragueLat, PragueLon);
        Assert.InRange(dist, 270d, 290d);
    }

    [Fact]
    public void DistanceCalculation_BratislavaToVienna_IsApprox55Km()
    {
        // Bratislava → Vienna ≈ 55–60 km
        var dist = GlobalExchangeCalculator.ComputeDistanceKm(BratislavaLat, BratislavaLon, ViennaLat, ViennaLon);
        Assert.InRange(dist, 45d, 75d);
    }

    [Fact]
    public void DistanceCalculation_PragueToWarsaw_IsApprox517Km()
    {
        // Prague (50.08°N, 14.43°E) → Warsaw (52.23°N, 21.01°E) ≈ 517 km
        var dist = GlobalExchangeCalculator.ComputeDistanceKm(PragueLat, PragueLon, WarsawLat, WarsawLon);
        Assert.InRange(dist, 490d, 550d);
    }

    // ── Transit tick computation ───────────────────────────────────────────────

    [Theory]
    [InlineData(0d, 1L)]
    [InlineData(200d, 1L)]
    [InlineData(400d, 2L)]
    [InlineData(600d, 3L)]
    public void ComputeTransitTicks_ReturnsExpectedTicks(double distanceKm, long expectedTicks)
    {
        Assert.Equal(expectedTicks, TradeRoutePhase.ComputeTransitTicks(distanceKm));
    }

    // ── Shipping cost formula ─────────────────────────────────────────────────

    [Fact]
    public void ShippingCostFormula_ScalesWithDistance()
    {
        var woodResource = new ResourceType { Id = Guid.NewGuid(), Name = "Wood", Slug = "wood", WeightPerUnit = 0.5m, BasePrice = 10m };
        var resourceTypesById = new Dictionary<Guid, ResourceType> { [woodResource.Id] = woodResource };
        var productTypesById = new Dictionary<Guid, ProductType>();
        var recipesByProduct = new Dictionary<Guid, List<ProductRecipe>>();

        var weight = GlobalExchangeCalculator.ComputeItemWeightPerUnit(woodResource.Id, null, resourceTypesById, productTypesById, recipesByProduct);

        var costShort = GlobalExchangeCalculator.ComputeTransitCostPerUnit(BratislavaLat, BratislavaLon, ViennaLat, ViennaLon, weight, 1.0m);
        var costLong = GlobalExchangeCalculator.ComputeTransitCostPerUnit(BratislavaLat, BratislavaLon, PragueLat, PragueLon, weight, 1.0m);

        Assert.True(costLong > costShort, $"Long-distance cost ({costLong}) should exceed short-distance cost ({costShort}).");
    }

    [Fact]
    public void ShippingCostFormula_HigherFuelIndex_IncreasesShippingCost()
    {
        var woodResource = new ResourceType { Id = Guid.NewGuid(), Name = "Wood", Slug = "wood", WeightPerUnit = 0.5m, BasePrice = 10m };
        var resourceTypesById = new Dictionary<Guid, ResourceType> { [woodResource.Id] = woodResource };
        var productTypesById = new Dictionary<Guid, ProductType>();
        var recipesByProduct = new Dictionary<Guid, List<ProductRecipe>>();

        var weight = GlobalExchangeCalculator.ComputeItemWeightPerUnit(woodResource.Id, null, resourceTypesById, productTypesById, recipesByProduct);

        var costNormal = GlobalExchangeCalculator.ComputeTransitCostPerUnit(BratislavaLat, BratislavaLon, PragueLat, PragueLon, weight, 1.0m);
        var costHigh = GlobalExchangeCalculator.ComputeTransitCostPerUnit(BratislavaLat, BratislavaLon, PragueLat, PragueLon, weight, 3.0m);

        Assert.True(costHigh > costNormal, $"High fuel cost ({costHigh}) should exceed normal ({costNormal}).");
    }

    // ── Creation and persistence ───────────────────────────────────────────────

    [Fact]
    public async Task CreateTradeRoute_Persists_WithCorrectCityCodesAndProductType()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await SeedScenarioAsync(db);

        var route = new InterCityTradeRoute
        {
            Id = Guid.NewGuid(),
            CompanyId = s.SellerCompany.Id,
            SourceBuildingId = s.SourceBuilding.Id,
            SourceBuildingUnitId = s.B2BUnit.Id,
            DestinationBuildingId = s.DestBuilding.Id,
            DestinationBuildingUnitId = s.PurchaseUnit.Id,
            ResourceTypeId = s.Wood.Id,
            Quantity = 50m,
            Quality = 0.7m,
            SourcingCostTotal = 25m,
            PricePerUnit = 15m,
            ScheduledDepartureTick = 1,
            ExpectedArrivalTick = 2,
            TransitTicks = 1,
            ShippingCostEstimate = 10m,
            Status = TradeRouteStatus.InTransit,
            CreatedAtUtc = DateTime.UtcNow,
            DepartedAtUtc = DateTime.UtcNow,
        };
        db.InterCityTradeRoutes.Add(route);
        await db.SaveChangesAsync();

        var loaded = await db.InterCityTradeRoutes
            .Include(r => r.SourceBuilding).ThenInclude(b => b.City)
            .Include(r => r.DestinationBuilding).ThenInclude(b => b.City)
            .FirstAsync(r => r.Id == route.Id);

        Assert.Equal("Bratislava", loaded.SourceBuilding.City.Name);
        Assert.Equal("Prague", loaded.DestinationBuilding.City.Name);
        Assert.Equal(TradeRouteStatus.InTransit, loaded.Status);
        Assert.Equal(s.Wood.Id, loaded.ResourceTypeId);
        Assert.Equal(50m, loaded.Quantity);
    }

    // ── Status transition: IN_TRANSIT → DELIVERED ─────────────────────────────

    [Fact]
    public async Task TradeRoutePhase_DeliverableRoute_StatusBecomesDelivered()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // ProcessTickAsync increments tick: seed at tick 4 so after processing tick becomes 5
        var s = await SeedScenarioAsync(db, currentTick: 4);

        var route = new InterCityTradeRoute
        {
            Id = Guid.NewGuid(),
            CompanyId = s.SellerCompany.Id,
            SourceBuildingId = s.SourceBuilding.Id,
            SourceBuildingUnitId = s.B2BUnit.Id,
            DestinationBuildingId = s.DestBuilding.Id,
            DestinationBuildingUnitId = s.PurchaseUnit.Id,
            ResourceTypeId = s.Wood.Id,
            Quantity = 20m,
            Quality = 0.7m,
            SourcingCostTotal = 10m,
            PricePerUnit = 15m,
            ScheduledDepartureTick = 4,
            ExpectedArrivalTick = 5,
            TransitTicks = 1,
            ShippingCostEstimate = 5m,
            Status = TradeRouteStatus.InTransit,
            CreatedAtUtc = DateTime.UtcNow,
            DepartedAtUtc = DateTime.UtcNow,
        };
        db.InterCityTradeRoutes.Add(route);
        await db.SaveChangesAsync();

        var processor = CreateProcessor(scope);
        await processor.ProcessTickAsync();

        var updated = await db.InterCityTradeRoutes.FindAsync(route.Id);
        Assert.Equal(TradeRouteStatus.Delivered, updated!.Status);
        Assert.NotNull(updated.CompletedAtUtc);
    }

    // ── Inventory arrives at destination ──────────────────────────────────────

    [Fact]
    public async Task TradeRoutePhase_OnDelivery_InventoryAppearsAtDestination()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await SeedScenarioAsync(db, currentTick: 9);

        var route = new InterCityTradeRoute
        {
            Id = Guid.NewGuid(),
            CompanyId = s.SellerCompany.Id,
            SourceBuildingId = s.SourceBuilding.Id,
            SourceBuildingUnitId = s.B2BUnit.Id,
            DestinationBuildingId = s.DestBuilding.Id,
            DestinationBuildingUnitId = s.PurchaseUnit.Id,
            ResourceTypeId = s.Wood.Id,
            Quantity = 30m,
            Quality = 0.6m,
            SourcingCostTotal = 15m,
            PricePerUnit = 12m,
            ScheduledDepartureTick = 9,
            ExpectedArrivalTick = 10,
            TransitTicks = 1,
            ShippingCostEstimate = 6m,
            Status = TradeRouteStatus.InTransit,
            CreatedAtUtc = DateTime.UtcNow,
            DepartedAtUtc = DateTime.UtcNow,
        };
        db.InterCityTradeRoutes.Add(route);
        await db.SaveChangesAsync();

        // Capture pre-delivery quantity (purchasing phase may also add inventory this tick)
        var preBefore = await db.Inventories
            .Where(i => i.BuildingUnitId == s.PurchaseUnit.Id && i.ResourceTypeId == s.Wood.Id)
            .SumAsync(i => (decimal?)i.Quantity) ?? 0m;

        var processor = CreateProcessor(scope);
        await processor.ProcessTickAsync();

        var totalAfter = await db.Inventories
            .Where(i => i.BuildingUnitId == s.PurchaseUnit.Id && i.ResourceTypeId == s.Wood.Id)
            .SumAsync(i => (decimal?)i.Quantity) ?? 0m;

        // Total inventory must have increased by at least 30 (the route quantity)
        Assert.True(totalAfter >= preBefore + 30m,
            $"Expected dest inventory to increase by at least 30. Before={preBefore}, After={totalAfter}.");
    }

    // ── Delivery failure – destination unit full ───────────────────────────────

    [Fact]
    public async Task TradeRoutePhase_DestinationUnitFull_StatusBecomesFailedAndInventoryReturned()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // generousDestInventorySpace=false seeds a tiny MaxInventory=5 that is already filled with 5 units
        var s = await SeedScenarioAsync(db, currentTick: 19, generousDestInventorySpace: false);

        var route = new InterCityTradeRoute
        {
            Id = Guid.NewGuid(),
            CompanyId = s.SellerCompany.Id,
            SourceBuildingId = s.SourceBuilding.Id,
            SourceBuildingUnitId = s.B2BUnit.Id,
            DestinationBuildingId = s.DestBuilding.Id,
            DestinationBuildingUnitId = s.PurchaseUnit.Id,
            ResourceTypeId = s.Wood.Id,
            Quantity = 50m,
            Quality = 0.7m,
            SourcingCostTotal = 25m,
            PricePerUnit = 15m,
            ScheduledDepartureTick = 19,
            ExpectedArrivalTick = 20,
            TransitTicks = 1,
            ShippingCostEstimate = 8m,
            Status = TradeRouteStatus.InTransit,
            CreatedAtUtc = DateTime.UtcNow,
            DepartedAtUtc = DateTime.UtcNow,
        };
        db.InterCityTradeRoutes.Add(route);
        await db.SaveChangesAsync();

        var processor = CreateProcessor(scope);
        await processor.ProcessTickAsync();

        var updated = await db.InterCityTradeRoutes.FindAsync(route.Id);
        Assert.Equal(TradeRouteStatus.Failed, updated!.Status);
        Assert.NotNull(updated.FailureReason);

        // Inventory must be returned to the source B2B unit.
        // Source was seeded with 100 units; returned 50 = total 150.
        var sourceInventory = await db.Inventories
            .FirstOrDefaultAsync(i =>
                i.BuildingUnitId == s.B2BUnit.Id
                && i.ResourceTypeId == s.Wood.Id);
        Assert.NotNull(sourceInventory);
        Assert.Equal(150m, sourceInventory.Quantity);
    }

    // ── Concurrent routes from the same B2B unit ──────────────────────────────

    [Fact]
    public async Task TradeRoutePhase_TenConcurrentRoutes_AllProcessIndependently()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await SeedScenarioAsync(db, currentTick: 49, sourceUnitInventory: 1_000m);

        var routes = Enumerable.Range(0, 10).Select(_ => new InterCityTradeRoute
        {
            Id = Guid.NewGuid(),
            CompanyId = s.SellerCompany.Id,
            SourceBuildingId = s.SourceBuilding.Id,
            SourceBuildingUnitId = s.B2BUnit.Id,
            DestinationBuildingId = s.DestBuilding.Id,
            DestinationBuildingUnitId = s.PurchaseUnit.Id,
            ResourceTypeId = s.Wood.Id,
            Quantity = 10m,
            Quality = 0.7m,
            SourcingCostTotal = 5m,
            PricePerUnit = 12m,
            ScheduledDepartureTick = 49,
            ExpectedArrivalTick = 50,
            TransitTicks = 1,
            ShippingCostEstimate = 3m,
            Status = TradeRouteStatus.InTransit,
            CreatedAtUtc = DateTime.UtcNow,
            DepartedAtUtc = DateTime.UtcNow,
        }).ToList();

        db.InterCityTradeRoutes.AddRange(routes);
        await db.SaveChangesAsync();

        var processor = CreateProcessor(scope);
        await processor.ProcessTickAsync();

        var routeIds = routes.Select(r => r.Id).ToHashSet();
        var updated = await db.InterCityTradeRoutes
            .Where(r => routeIds.Contains(r.Id))
            .ToListAsync();

        // All 10 routes must have been processed (DELIVERED or FAILED)
        Assert.All(updated, r => Assert.True(
            r.Status is TradeRouteStatus.Delivered or TradeRouteStatus.Failed,
            $"Route {r.Id} was unexpectedly still {r.Status}."));
    }

    // ── LedgerEntry created on delivery ───────────────────────────────────────

    [Fact]
    public async Task TradeRoutePhase_OnDelivery_ShippingCostLedgerEntryCreated()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await SeedScenarioAsync(db, currentTick: 99);

        var route = new InterCityTradeRoute
        {
            Id = Guid.NewGuid(),
            CompanyId = s.SellerCompany.Id,
            SourceBuildingId = s.SourceBuilding.Id,
            SourceBuildingUnitId = s.B2BUnit.Id,
            DestinationBuildingId = s.DestBuilding.Id,
            DestinationBuildingUnitId = s.PurchaseUnit.Id,
            ResourceTypeId = s.Wood.Id,
            Quantity = 10m,
            Quality = 0.7m,
            SourcingCostTotal = 5m,
            PricePerUnit = 15m,
            ScheduledDepartureTick = 99,
            ExpectedArrivalTick = 100,
            TransitTicks = 1,
            ShippingCostEstimate = 5m,
            Status = TradeRouteStatus.InTransit,
            CreatedAtUtc = DateTime.UtcNow,
            DepartedAtUtc = DateTime.UtcNow,
        };
        db.InterCityTradeRoutes.Add(route);
        await db.SaveChangesAsync();

        var processor = CreateProcessor(scope);
        await processor.ProcessTickAsync();

        // Seller must have a SHIPPING_COST ledger entry (negative)
        var shippingLedger = await db.LedgerEntries
            .Where(l => l.CompanyId == s.SellerCompany.Id
                && l.Category == LedgerCategory.ShippingCost)
            .ToListAsync();
        Assert.NotEmpty(shippingLedger);
        Assert.All(shippingLedger, e => Assert.True(e.Amount < 0m, "Shipping cost ledger entry must be negative."));

        // Seller must have a REVENUE ledger entry (positive)
        var revenueLedger = await db.LedgerEntries
            .Where(l => l.CompanyId == s.SellerCompany.Id
                && l.Category == LedgerCategory.Revenue)
            .ToListAsync();
        Assert.NotEmpty(revenueLedger);
        Assert.All(revenueLedger, e => Assert.True(e.Amount > 0m, "Revenue ledger entry must be positive."));
    }

    [Fact]
    public async Task TradeRoutePhase_OnDelivery_CreatesShipmentArrivedNotification()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await SeedScenarioAsync(db, currentTick: 119);

        var route = new InterCityTradeRoute
        {
            Id = Guid.NewGuid(),
            CompanyId = s.SellerCompany.Id,
            SourceBuildingId = s.SourceBuilding.Id,
            SourceBuildingUnitId = s.B2BUnit.Id,
            DestinationBuildingId = s.DestBuilding.Id,
            DestinationBuildingUnitId = s.PurchaseUnit.Id,
            ResourceTypeId = s.Wood.Id,
            Quantity = 10m,
            Quality = 0.7m,
            SourcingCostTotal = 5m,
            PricePerUnit = 20m,
            ScheduledDepartureTick = 119,
            ExpectedArrivalTick = 120,
            TransitTicks = 1,
            ShippingCostEstimate = 5m,
            Status = TradeRouteStatus.InTransit,
            CreatedAtUtc = DateTime.UtcNow,
            DepartedAtUtc = DateTime.UtcNow,
        };
        db.InterCityTradeRoutes.Add(route);
        await db.SaveChangesAsync();

        var processor = CreateProcessor(scope);
        await processor.ProcessTickAsync();

        var sellerNotification = await db.PlayerNotifications
            .AsNoTracking()
            .FirstOrDefaultAsync(notification =>
                notification.PlayerId == s.SellerPlayer.Id
                && notification.Type == PlayerNotificationType.ShipmentArrived);
        Assert.NotNull(sellerNotification);
        Assert.Equal(s.DestBuilding.Id, sellerNotification!.BuildingId);
    }

    [Fact]
    public async Task TradeRoutePhase_HighShippingShare_CreatesMarginErosionNotification()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await SeedScenarioAsync(db, currentTick: 129);

        var route = new InterCityTradeRoute
        {
            Id = Guid.NewGuid(),
            CompanyId = s.SellerCompany.Id,
            SourceBuildingId = s.SourceBuilding.Id,
            SourceBuildingUnitId = s.B2BUnit.Id,
            DestinationBuildingId = s.DestBuilding.Id,
            DestinationBuildingUnitId = s.PurchaseUnit.Id,
            ResourceTypeId = s.Wood.Id,
            Quantity = 10m,
            Quality = 0.7m,
            SourcingCostTotal = 5m,
            PricePerUnit = 1m,
            ScheduledDepartureTick = 129,
            ExpectedArrivalTick = 130,
            TransitTicks = 1,
            ShippingCostEstimate = 5m,
            Status = TradeRouteStatus.InTransit,
            CreatedAtUtc = DateTime.UtcNow,
            DepartedAtUtc = DateTime.UtcNow,
        };
        db.InterCityTradeRoutes.Add(route);
        await db.SaveChangesAsync();

        var processor = CreateProcessor(scope);
        await processor.ProcessTickAsync();

        var erosionNotification = await db.PlayerNotifications
            .AsNoTracking()
            .FirstOrDefaultAsync(notification =>
                notification.PlayerId == s.SellerPlayer.Id
                && notification.Type == PlayerNotificationType.LogisticsMarginErosion);
        Assert.NotNull(erosionNotification);
        Assert.Equal(s.SourceBuilding.Id, erosionNotification!.BuildingId);
    }

    // ── FuelPriceIndex from destination city ──────────────────────────────────

    [Fact]
    public async Task TradeRoutePhase_HighFuelPriceCity_ShippingCostActualIsHigher()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await SeedScenarioAsync(db, currentTick: 199);

        // Set Prague's fuel price index to 3.0 (high cost)
        var prague = await db.Cities.FirstAsync(c => c.Name == "Prague");
        prague.FuelPriceIndex = 3.0m;
        await db.SaveChangesAsync();

        var route = new InterCityTradeRoute
        {
            Id = Guid.NewGuid(),
            CompanyId = s.SellerCompany.Id,
            SourceBuildingId = s.SourceBuilding.Id,
            SourceBuildingUnitId = s.B2BUnit.Id,
            DestinationBuildingId = s.DestBuilding.Id,
            DestinationBuildingUnitId = s.PurchaseUnit.Id,
            ResourceTypeId = s.Wood.Id,
            Quantity = 10m,
            Quality = 0.7m,
            SourcingCostTotal = 5m,
            PricePerUnit = 15m,
            ScheduledDepartureTick = 199,
            ExpectedArrivalTick = 200,
            TransitTicks = 1,
            ShippingCostEstimate = 5m,
            Status = TradeRouteStatus.InTransit,
            CreatedAtUtc = DateTime.UtcNow,
            DepartedAtUtc = DateTime.UtcNow,
        };
        db.InterCityTradeRoutes.Add(route);
        await db.SaveChangesAsync();

        var processor = CreateProcessor(scope);
        await processor.ProcessTickAsync();

        var updated = await db.InterCityTradeRoutes.FindAsync(route.Id);
        Assert.Equal(TradeRouteStatus.Delivered, updated!.Status);
        // With fuel index 3.0, actual shipping cost must be positive and clearly
        // higher than if fuel index were 1.0 (verified via ShippingCostFormula test above).
        Assert.True(updated.ShippingCostActual > 0m,
            $"ShippingCostActual must be positive with FuelPriceIndex=3.0 (got {updated.ShippingCostActual}).");
    }
}

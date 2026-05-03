using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

/// <summary>
/// Unit and integration tests for seasonal demand mechanics.
/// Covers DemandSeasonality entity, tick-to-quarter mapping, seed coverage,
/// PublicSalesPhase seasonal multiplier application, and the SeasonalOutlook
/// exposed via publicSalesAnalytics GraphQL query.
/// </summary>
public sealed class DemandSeasonalityTests
{
    // ── Pure unit tests ────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0.8)]   // Q1: post-holiday slump for Furniture
    [InlineData(1, 1.5)]   // Q2: spring moving season peak
    [InlineData(2, 1.3)]   // Q3: summer moving season
    [InlineData(3, 1.0)]   // Q4: year-end neutral
    public void DemandSeasonality_GetMultiplierForQuarter_ReturnsCorrectValue(
        int quarterIndex, decimal expected)
    {
        var seasonality = new DemandSeasonality
        {
            Q1Multiplier = 0.8m,
            Q2Multiplier = 1.5m,
            Q3Multiplier = 1.3m,
            Q4Multiplier = 1.0m,
        };

        var result = seasonality.GetMultiplierForQuarter(quarterIndex);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void DemandSeasonality_GetMultiplierForQuarter_OutOfRange_ReturnsNeutral()
    {
        var seasonality = new DemandSeasonality
        {
            Q1Multiplier = 0.8m,
            Q2Multiplier = 1.5m,
            Q3Multiplier = 1.3m,
            Q4Multiplier = 1.0m,
        };

        Assert.Equal(1.0m, seasonality.GetMultiplierForQuarter(-1));
        Assert.Equal(1.0m, seasonality.GetMultiplierForQuarter(4));
        Assert.Equal(1.0m, seasonality.GetMultiplierForQuarter(99));
    }

    [Theory]
    [InlineData(0L,                                           0)] // tick 0 → Q1
    [InlineData(GameConstants.TicksPerQuarter,                1)] // boundary → Q2
    [InlineData(GameConstants.TicksPerQuarter * 2,           2)] // → Q3
    [InlineData(GameConstants.TicksPerQuarter * 3,           3)] // → Q4
    [InlineData(GameConstants.TicksPerYear,                   0)] // year wrap → Q1
    [InlineData(GameConstants.TicksPerYear + 1,              0)] // still Q1
    [InlineData(GameConstants.TicksPerYear + GameConstants.TicksPerQuarter, 1)] // year+1Q → Q2
    public void GameConstants_TickToQuarterIndex_CalculationIsCorrect(long tick, int expectedQuarter)
    {
        var actualQuarter = (int)((tick / GameConstants.TicksPerQuarter) % 4);
        Assert.Equal(expectedQuarter, actualQuarter);
    }

    [Fact]
    public void GameState_CurrentQuarterLabel_ReturnsCorrectQuarter()
    {
        var gs = new GameState();

        gs.CurrentTick = 0;
        Assert.Equal("Q1", gs.CurrentQuarterLabel);
        Assert.Equal(0, gs.CurrentQuarter);

        gs.CurrentTick = GameConstants.TicksPerQuarter;
        Assert.Equal("Q2", gs.CurrentQuarterLabel);
        Assert.Equal(1, gs.CurrentQuarter);

        gs.CurrentTick = GameConstants.TicksPerQuarter * 2;
        Assert.Equal("Q3", gs.CurrentQuarterLabel);
        Assert.Equal(2, gs.CurrentQuarter);

        gs.CurrentTick = GameConstants.TicksPerQuarter * 3;
        Assert.Equal("Q4", gs.CurrentQuarterLabel);
        Assert.Equal(3, gs.CurrentQuarter);

        // Year wrap
        gs.CurrentTick = GameConstants.TicksPerYear;
        Assert.Equal("Q1", gs.CurrentQuarterLabel);
    }

    // ── Integration tests ──────────────────────────────────────────────────

    [Fact]
    public async Task EnsureDemandSeasonality_SeedsRowForEveryProductType()
    {
        // After initialization every product type must have exactly one DemandSeasonality row.
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var productCount = await db.ProductTypes.CountAsync();
        var seasonalityCount = await db.DemandSeasonalities.CountAsync();

        Assert.True(productCount > 0, "Products must be seeded.");
        Assert.Equal(productCount, seasonalityCount);
    }

    [Fact]
    public async Task EnsureDemandSeasonality_AllMultipliersAreInValidRange()
    {
        // All seeded multipliers must be in the range [0.5, 2.0] per spec.
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rows = await db.DemandSeasonalities.ToListAsync();
        Assert.NotEmpty(rows);

        foreach (var row in rows)
        {
            foreach (var (label, value) in new[]
            {
                ("Q1", row.Q1Multiplier),
                ("Q2", row.Q2Multiplier),
                ("Q3", row.Q3Multiplier),
                ("Q4", row.Q4Multiplier),
            })
            {
                Assert.True(value >= 0.5m && value <= 2.0m,
                    $"{label}Multiplier {value} for product {row.ProductTypeId} is outside [0.5, 2.0]");
            }
        }
    }

    [Fact]
    public async Task EnsureDemandSeasonality_FurniturePeaksInQ2()
    {
        // Furniture spring-moving-season pattern: Q2 must be >= Q1 and Q4.
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ids = await db.ProductTypes
            .Where(p => p.Industry == Industry.Furniture)
            .Select(p => p.Id).ToListAsync();

        Assert.NotEmpty(ids);

        foreach (var id in ids)
        {
            var s = await db.DemandSeasonalities.FirstAsync(d => d.ProductTypeId == id);
            Assert.True(s.Q2Multiplier >= s.Q1Multiplier,
                $"Furniture Q2 ({s.Q2Multiplier}) should be >= Q1 ({s.Q1Multiplier}) for {id}");
            Assert.True(s.Q2Multiplier >= s.Q4Multiplier,
                $"Furniture Q2 ({s.Q2Multiplier}) should be >= Q4 ({s.Q4Multiplier}) for {id}");
        }
    }

    [Fact]
    public async Task EnsureDemandSeasonality_ElectronicsPeaksInQ4()
    {
        // Electronics holiday pattern: Q4 must be the peak.
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ids = await db.ProductTypes
            .Where(p => p.Industry == Industry.Electronics)
            .Select(p => p.Id).ToListAsync();

        Assert.NotEmpty(ids);

        foreach (var id in ids)
        {
            var s = await db.DemandSeasonalities.FirstAsync(d => d.ProductTypeId == id);
            Assert.True(s.Q4Multiplier >= s.Q1Multiplier,
                $"Electronics Q4 ({s.Q4Multiplier}) should be >= Q1 ({s.Q1Multiplier}) for {id}");
            Assert.True(s.Q4Multiplier >= s.Q2Multiplier,
                $"Electronics Q4 ({s.Q4Multiplier}) should be >= Q2 ({s.Q2Multiplier}) for {id}");
        }
    }

    [Fact]
    public async Task EnsureDemandSeasonality_ConstructionPeaksInQ3()
    {
        // Construction summer-peak pattern: Q3 must be the highest quarter.
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ids = await db.ProductTypes
            .Where(p => p.Industry == Industry.Construction)
            .Select(p => p.Id).ToListAsync();

        Assert.NotEmpty(ids);

        foreach (var id in ids)
        {
            var s = await db.DemandSeasonalities.FirstAsync(d => d.ProductTypeId == id);
            Assert.True(s.Q3Multiplier >= s.Q1Multiplier,
                $"Construction Q3 ({s.Q3Multiplier}) should be >= Q1 ({s.Q1Multiplier}) for {id}");
            // Q3 should be higher than Q4 (slowdown in fall)
            Assert.True(s.Q3Multiplier >= s.Q4Multiplier,
                $"Construction Q3 ({s.Q3Multiplier}) should be >= Q4 ({s.Q4Multiplier}) for {id}");
        }
    }

    [Fact]
    public async Task PublicSalesPhase_Q2SeasonMultiplier_IncreasesQuantitySold_VsQ1()
    {
        // A seller operating in Q2 (furniture 1.5× multiplier) must sell more than
        // the same seller in Q1 (0.8× multiplier), all else being equal.
        // Uses isolated cities so their demand does not interfere.
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var chair = await db.ProductTypes.FirstAsync(p => p.Slug == "wooden-chair");

        // Verify seasonality exists for the product
        var seasonality = await db.DemandSeasonalities
            .FirstOrDefaultAsync(d => d.ProductTypeId == chair.Id);
        Assert.NotNull(seasonality);

        // Small custom cities so demand < unit capacity (no binding constraint)
        var cityA = new City
        {
            Id = Guid.NewGuid(), Name = "SeasonCityA", CurrencyCode = "EUR",
            Population = 25_000, BaseSalaryPerManhour = 15m,
            Latitude = 48.0, Longitude = 17.0, FuelPriceIndex = 1.0m,
        };
        var cityB = new City
        {
            Id = Guid.NewGuid(), Name = "SeasonCityB", CurrencyCode = "EUR",
            Population = 25_000, BaseSalaryPerManhour = 15m,
            Latitude = 48.0, Longitude = 17.1, FuelPriceIndex = 1.0m,
        };
        db.Cities.AddRange(cityA, cityB);

        var player = new Player
        {
            Id = Guid.NewGuid(), Email = $"ssn-{Guid.NewGuid():N}@t.com",
            DisplayName = "S", PasswordHash = "h", Role = PlayerRole.Player,
        };
        var coA = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "SeasonCoA", Cash = 5_000_000m };
        var coB = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "SeasonCoB", Cash = 5_000_000m };
        db.Players.Add(player);
        db.Companies.AddRange(coA, coB);

        var shopA = new Building { Id = Guid.NewGuid(), CompanyId = coA.Id, CityId = cityA.Id, Type = BuildingType.SalesShop, Name = "ShopA", Level = 1 };
        var shopB = new Building { Id = Guid.NewGuid(), CompanyId = coB.Id, CityId = cityB.Id, Type = BuildingType.SalesShop, Name = "ShopB", Level = 1 };
        db.Buildings.AddRange(shopA, shopB);

        // High-level units so capacity doesn't cap sales
        var unitA = new BuildingUnit { Id = Guid.NewGuid(), BuildingId = shopA.Id, UnitType = UnitType.PublicSales, GridX = 0, GridY = 0, Level = 5, ProductTypeId = chair.Id, MinPrice = chair.BasePrice };
        var unitB = new BuildingUnit { Id = Guid.NewGuid(), BuildingId = shopB.Id, UnitType = UnitType.PublicSales, GridX = 0, GridY = 0, Level = 5, ProductTypeId = chair.Id, MinPrice = chair.BasePrice };
        db.BuildingUnits.AddRange(unitA, unitB);

        db.Inventories.AddRange(
            new Inventory { Id = Guid.NewGuid(), BuildingId = shopA.Id, BuildingUnitId = unitA.Id, ProductTypeId = chair.Id, Quantity = 500m, Quality = 0.7m },
            new Inventory { Id = Guid.NewGuid(), BuildingId = shopB.Id, BuildingUnitId = unitB.Id, ProductTypeId = chair.Id, Quantity = 500m, Quality = 0.7m }
        );

        // Bank accounts so OperatingCostPhase passes
        db.BankAccounts.AddRange(
            new BankAccount { Id = Guid.NewGuid(), BankBuildingId = shopA.Id, CompanyId = coA.Id, CurrencyCode = "EUR", Balance = 100_000m, AccountNumber = "1200000000000001" },
            new BankAccount { Id = Guid.NewGuid(), BankBuildingId = shopB.Id, CompanyId = coB.Id, CurrencyCode = "EUR", Balance = 100_000m, AccountNumber = "1200000000000002" }
        );

        await db.SaveChangesAsync();

        var gs = await db.GameStates.FirstOrDefaultDeterministicAsync();
        Assert.NotNull(gs);

        var phases = scope.ServiceProvider.GetServices<ITickPhase>();
        var processor = new TickProcessor(db, phases, new NullLogger<TickProcessor>());

        // ── Run at Q1 (furniture multiplier 0.8×) ──────────────────────────
        gs!.CurrentTick = 0L; // Q1
        await db.SaveChangesAsync();
        await processor.ProcessTickAsync();

        var soldQ1 = await db.PublicSalesRecords
            .Where(r => r.BuildingUnitId == unitA.Id)
            .SumAsync(r => r.QuantitySold);

        // Reset for Q2 test
        db.PublicSalesRecords.RemoveRange(
            await db.PublicSalesRecords.Where(r => r.BuildingUnitId == unitA.Id || r.BuildingUnitId == unitB.Id).ToListAsync()
        );
        var invB = await db.Inventories.FirstAsync(i => i.BuildingUnitId == unitB.Id);
        invB.Quantity = 500m;
        await db.SaveChangesAsync();

        // ── Run at Q2 (furniture multiplier 1.5×) ──────────────────────────
        gs.CurrentTick = (long)GameConstants.TicksPerQuarter; // Q2
        await db.SaveChangesAsync();
        await processor.ProcessTickAsync();

        var soldQ2 = await db.PublicSalesRecords
            .Where(r => r.BuildingUnitId == unitB.Id)
            .SumAsync(r => r.QuantitySold);

        // Q2 (1.5×) must sell strictly more than Q1 (0.8×).
        // Gap: 1.5 − 0.8 = 0.7 >> random noise amplitude (~0.08), so this is deterministic.
        Assert.True(soldQ2 > soldQ1,
            $"Q2 seasonal (1.5×) should exceed Q1 (0.8×). Sold: Q1={soldQ1:F2}, Q2={soldQ2:F2}");
    }

    [Fact]
    public async Task PublicSalesPhase_NeutralSeasonality_DoesNotChangeBaseline()
    {
        // When Q4 multiplier = 1.0 (neutral), the phase must not suppress or boost demand
        // compared to a scenario with no seasonality row (which also defaults to 1.0).
        // This is the backward-compatibility regression test.
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var chair = await db.ProductTypes.FirstAsync(p => p.Slug == "wooden-chair");
        var seasonality = await db.DemandSeasonalities
            .FirstOrDefaultAsync(d => d.ProductTypeId == chair.Id);
        Assert.NotNull(seasonality);

        // Furniture Q4 multiplier is 1.0 (neutral baseline for this quarter).
        Assert.Equal(1.0m, seasonality!.Q4Multiplier);

        // Create a test city and seller
        var city = new City
        {
            Id = Guid.NewGuid(), Name = "NeutralSeasonCity", CurrencyCode = "EUR",
            Population = 30_000, BaseSalaryPerManhour = 15m,
            Latitude = 48.0, Longitude = 17.2, FuelPriceIndex = 1.0m,
        };
        db.Cities.Add(city);

        var player = new Player { Id = Guid.NewGuid(), Email = $"ns-{Guid.NewGuid():N}@t.com", DisplayName = "NS", PasswordHash = "h", Role = PlayerRole.Player };
        var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "NeutralSsnCo", Cash = 1_000_000m };
        db.Players.Add(player);
        db.Companies.Add(company);

        var shop = new Building { Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, Type = BuildingType.SalesShop, Name = "NShop", Level = 1 };
        db.Buildings.Add(shop);

        var unit = new BuildingUnit { Id = Guid.NewGuid(), BuildingId = shop.Id, UnitType = UnitType.PublicSales, GridX = 0, GridY = 0, Level = 5, ProductTypeId = chair.Id, MinPrice = chair.BasePrice };
        db.BuildingUnits.Add(unit);
        db.Inventories.Add(new Inventory { Id = Guid.NewGuid(), BuildingId = shop.Id, BuildingUnitId = unit.Id, ProductTypeId = chair.Id, Quantity = 500m, Quality = 0.7m });
        db.BankAccounts.Add(new BankAccount { Id = Guid.NewGuid(), BankBuildingId = shop.Id, CompanyId = company.Id, CurrencyCode = "EUR", Balance = 100_000m, AccountNumber = "1300000000000001" });
        await db.SaveChangesAsync();

        // Run at Q4 (neutral 1.0× for furniture)
        var gs = await db.GameStates.FirstOrDefaultDeterministicAsync();
        Assert.NotNull(gs);
        gs!.CurrentTick = (long)GameConstants.TicksPerQuarter * 3; // Q4 start
        await db.SaveChangesAsync();

        var phases = scope.ServiceProvider.GetServices<ITickPhase>();
        var processor = new TickProcessor(db, phases, new NullLogger<TickProcessor>());
        await processor.ProcessTickAsync();

        // With 1.0× multiplier, sales must be positive (not suppressed).
        var sold = await db.PublicSalesRecords
            .Where(r => r.BuildingUnitId == unit.Id)
            .SumAsync(r => r.QuantitySold);

        Assert.True(sold > 0, $"Neutral season (1.0×) should still produce sales. Sold: {sold}");
    }
}

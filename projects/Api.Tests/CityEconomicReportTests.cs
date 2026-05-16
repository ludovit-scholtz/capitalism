using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Engine.Phases;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

/// <summary>
/// Integration tests for <see cref="EconomicReportPhase"/> and the
/// city economic health indicators feature.
/// Uses isolated factories for each test to avoid shared-state contamination.
/// </summary>
public sealed class CityEconomicReportTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>Creates a basic player + company + building in a given city.</summary>
    private static async Task<(Guid CompanyId, Guid BuildingId)> SeedCompanyBuildingAsync(
        AppDbContext db,
        Guid cityId,
        string buildingType = BuildingType.SalesShop)
    {
        var player = new Player
        {
            Id = Guid.NewGuid(),
            Email = $"eco-test-{Guid.NewGuid():N}@test.com",
            DisplayName = "EcoTester",
            PasswordHash = "hash",
            Role = PlayerRole.Player
        };
        db.Players.Add(player);

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Name = $"EcoCompany {Guid.NewGuid():N}",
            Cash = 0m
        };
        db.Companies.Add(company);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CityId = cityId,
            CompanyId = company.Id,
            Type = buildingType,
            Latitude = 48.1,
            Longitude = 17.1,
            Name = "Test Building",
            PowerStatus = PowerStatus.Powered,
            PowerConsumption = 2m,
        };
        db.Buildings.Add(building);
        await db.SaveChangesAsync();

        return (company.Id, building.Id);
    }

    /// <summary>Adds a labor-cost ledger entry for a company building.</summary>
    private static async Task SeedLaborCostAsync(
        AppDbContext db, Guid companyId, Guid buildingId, decimal amount, long tick)
    {
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            BuildingId = buildingId,
            Category = LedgerCategory.LaborCost,
            Description = "Test salary",
            Amount = -amount, // negative = expense
            RecordedAtTick = tick,
            RecordedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Adds a revenue ledger entry for a company building.</summary>
    private static async Task SeedRevenueAsync(
        AppDbContext db, Guid companyId, Guid buildingId, decimal amount, long tick)
    {
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            BuildingId = buildingId,
            Category = LedgerCategory.Revenue,
            Description = "Test revenue",
            Amount = amount,
            RecordedAtTick = tick,
            RecordedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Builds a minimal <see cref="TickContext"/> for the given database and game state.</summary>
    private static TickContext BuildContext(AppDbContext db, GameState gs, IEnumerable<Building> buildings)
    {
        var buildingsById = buildings.ToDictionary(b => b.Id);
        var buildingsByType = buildings
            .GroupBy(b => b.Type)
            .ToDictionary(g => g.Key, g => g.ToList());

        return new TickContext
        {
            Db = db,
            GameState = gs,
            BuildingsById = buildingsById,
            BuildingsByType = buildingsByType,
            CitiesById = db.Cities.ToDictionary(c => c.Id),
            CompaniesById = db.Companies.ToDictionary(c => c.Id),
            EurFxRates = new Dictionary<string, decimal> { ["EUR"] = 1m },
        };
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EconomicReportPhase_EmptyCity_CreatesZeroIndexReport()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Use Prague (CZK city) to get a clean slate without admin seeded buildings
        var city = await db.Cities.FirstAsync(c => c.Name == "Prague");

        var gs = await db.GameStates.FirstAsync();
        gs.TaxCycleTicks = 10;
        gs.CurrentTick = 10; // on tax boundary
        await db.SaveChangesAsync();

        // No buildings, no companies → only power score contributes (100 when no demand)
        var phase = new EconomicReportPhase(NullLogger<EconomicReportPhase>.Instance);
        var context = BuildContext(db, gs, []);

        await phase.ProcessAsync(context);
        await db.SaveChangesAsync();

        var report = await db.CityEconomicReports.FirstOrDefaultAsync(r => r.CityId == city.Id);
        Assert.NotNull(report);
        Assert.Equal(0m, report.TotalSalaries);
        Assert.Equal(0m, report.TotalPublicRevenue);
        Assert.Equal(0, report.ActiveCompanies);
        // Power score = 100 (no demand), but weighted only 15%
        // Salary score = 0, revenue score = 0, quality score = 0
        // Index = 0.15 * 100 = 15
        Assert.Equal(15m, report.EconomicIndex);
    }

    [Fact]
    public async Task EconomicReportPhase_SingleCompanyWithSalaryAndRevenue_AggregatesCorrectly()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Use Prague to avoid admin-seeded buildings in Bratislava
        var city = await db.Cities.FirstAsync(c => c.Name == "Prague");
        var (companyId, buildingId) = await SeedCompanyBuildingAsync(db, city.Id);

        var gs = await db.GameStates.FirstAsync();
        gs.TaxCycleTicks = 10;
        gs.CurrentTick = 10;
        await db.SaveChangesAsync();

        await SeedLaborCostAsync(db, companyId, buildingId, 50_000m, 5L);
        await SeedRevenueAsync(db, companyId, buildingId, 100_000m, 5L);

        var buildings = await db.Buildings.Where(b => b.CityId == city.Id).ToListAsync();
        var phase = new EconomicReportPhase(NullLogger<EconomicReportPhase>.Instance);
        var context = BuildContext(db, gs, buildings);

        await phase.ProcessAsync(context);
        await db.SaveChangesAsync();

        var report = await db.CityEconomicReports.FirstOrDefaultAsync(r => r.CityId == city.Id);
        Assert.NotNull(report);
        Assert.Equal(50_000m, report.TotalSalaries);
        Assert.Equal(100_000m, report.TotalPublicRevenue);
        // Government company buildings are also seeded in every city, so expect >= 2
        Assert.True(report.ActiveCompanies >= 2, $"Expected at least 2 active companies but got {report.ActiveCompanies}");
        Assert.True(report.EconomicIndex > 0m, "Economic index should be > 0 with salary and revenue");
    }

    [Fact]
    public async Task EconomicReportPhase_MultipleCompanies_SumsMetrics()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Use Prague to avoid admin-seeded buildings in Bratislava
        var city = await db.Cities.FirstAsync(c => c.Name == "Prague");
        var (companyId1, buildingId1) = await SeedCompanyBuildingAsync(db, city.Id);
        var (companyId2, buildingId2) = await SeedCompanyBuildingAsync(db, city.Id);

        var gs = await db.GameStates.FirstAsync();
        gs.TaxCycleTicks = 10;
        gs.CurrentTick = 10;
        await db.SaveChangesAsync();

        await SeedLaborCostAsync(db, companyId1, buildingId1, 30_000m, 5L);
        await SeedLaborCostAsync(db, companyId2, buildingId2, 20_000m, 5L);
        await SeedRevenueAsync(db, companyId1, buildingId1, 60_000m, 5L);
        await SeedRevenueAsync(db, companyId2, buildingId2, 40_000m, 5L);

        var buildings = await db.Buildings.Where(b => b.CityId == city.Id).ToListAsync();
        var phase = new EconomicReportPhase(NullLogger<EconomicReportPhase>.Instance);
        var context = BuildContext(db, gs, buildings);

        await phase.ProcessAsync(context);
        await db.SaveChangesAsync();

        var report = await db.CityEconomicReports.FirstOrDefaultAsync(r => r.CityId == city.Id);
        Assert.NotNull(report);
        Assert.Equal(50_000m, report.TotalSalaries);
        Assert.Equal(100_000m, report.TotalPublicRevenue);
        // Government company buildings are also seeded → expect >= 3 (gov + 2 test companies)
        Assert.True(report.ActiveCompanies >= 3, $"Expected at least 3 active companies but got {report.ActiveCompanies}");
    }

    [Fact]
    public async Task EconomicReportPhase_PowerImbalance_PowerScoreReflectsDeficit()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Use Prague (no pre-seeded buildings)
        var city = await db.Cities.FirstAsync(c => c.Name == "Prague");
        var (_, _) = await SeedCompanyBuildingAsync(db, city.Id); // building with PowerConsumption=2

        var gs = await db.GameStates.FirstAsync();
        gs.TaxCycleTicks = 10;
        gs.CurrentTick = 10;
        await db.SaveChangesAsync();

        var buildings = await db.Buildings.Where(b => b.CityId == city.Id).ToListAsync();
        var phase = new EconomicReportPhase(NullLogger<EconomicReportPhase>.Instance);
        var context = BuildContext(db, gs, buildings);
        // No power plants → supply=0, demand=2 → power score=0

        await phase.ProcessAsync(context);
        await db.SaveChangesAsync();

        var report = await db.CityEconomicReports.FirstOrDefaultAsync(r => r.CityId == city.Id);
        Assert.NotNull(report);
        Assert.Equal(2m, report.TotalPowerConsumption);
        Assert.Equal(0m, report.TotalPowerSupply);
    }

    [Fact]
    public async Task EconomicReportPhase_PhaseSkippedWhenNotOnTaxBoundary()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var gs = await db.GameStates.FirstAsync();
        gs.TaxCycleTicks = 10;
        gs.CurrentTick = 7; // NOT on boundary
        await db.SaveChangesAsync();

        var phase = new EconomicReportPhase(NullLogger<EconomicReportPhase>.Instance);
        var context = BuildContext(db, gs, []);

        await phase.ProcessAsync(context);
        await db.SaveChangesAsync();

        var count = await db.CityEconomicReports.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task EconomicReportPhase_ConsecutiveCycles_PrunesOldReportsToMax10()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var gs = await db.GameStates.FirstAsync();
        gs.TaxCycleTicks = 10;

        var phase = new EconomicReportPhase(NullLogger<EconomicReportPhase>.Instance);

        // Run 12 cycles — only 10 should be retained
        for (var cycle = 1; cycle <= 12; cycle++)
        {
            gs.CurrentTick = cycle * 10L;
            await db.SaveChangesAsync();

            var context = BuildContext(db, gs, []);
            await phase.ProcessAsync(context);
            await db.SaveChangesAsync();
        }

        var count = await db.CityEconomicReports.CountAsync(r => r.CityId == city.Id);
        Assert.Equal(EconomicReportPhase.MaxHistoricalReports, count);
    }

    [Fact]
    public async Task EconomicReportPhase_ThrivingCity_IncreasesPopulation()
    {
        // Use a mock city with a known population
        var city = new City
        {
            Id = Guid.NewGuid(),
            Name = "TestThriving",
            CountryCode = "SK",
            Population = 100_000,
            AverageRentPerSqm = 10m,
            BaseSalaryPerManhour = 10m,
            CurrencyCode = "EUR",
        };
        var initialPop = city.Population;

        // Score = 80 (≥70 → thriving)
        EconomicReportPhase.ApplyPopulationImpact(city, 80m);

        Assert.True(city.Population > initialPop, "Thriving city should gain population");
        Assert.Equal((int)Math.Round(initialPop * 1.005m), city.Population);
    }

    [Fact]
    public async Task EconomicReportPhase_DecliningCity_DecreasesPopulation()
    {
        var city = new City
        {
            Id = Guid.NewGuid(),
            Name = "TestDeclining",
            CountryCode = "SK",
            Population = 100_000,
            AverageRentPerSqm = 10m,
            BaseSalaryPerManhour = 10m,
            CurrencyCode = "EUR",
        };
        var initialPop = city.Population;

        // Score = 30 (<40 → declining)
        EconomicReportPhase.ApplyPopulationImpact(city, 30m);

        Assert.True(city.Population < initialPop, "Declining city should lose population");
        Assert.Equal((int)Math.Round(initialPop * 0.998m), city.Population);
    }

    [Fact]
    public async Task EconomicReportPhase_NeutralCity_PopulationUnchanged()
    {
        var city = new City
        {
            Id = Guid.NewGuid(),
            Name = "TestNeutral",
            CountryCode = "SK",
            Population = 100_000,
            AverageRentPerSqm = 10m,
            BaseSalaryPerManhour = 10m,
            CurrencyCode = "EUR",
        };
        var initialPop = city.Population;

        // Score = 55 (40-69 → neutral)
        EconomicReportPhase.ApplyPopulationImpact(city, 55m);

        Assert.Equal(initialPop, city.Population);
    }

    [Fact]
    public void ComputeEconomicIndex_AllZeros_Returns0()
    {
        // When there IS power demand but no supply, powerScore=0 → all weights give 0
        var index = EconomicReportPhase.ComputeEconomicIndex(
            totalSalaries: 0m,
            totalRevenue: 0m,
            totalConsumption: 10m, // has demand
            totalSupply: 0m,       // but no supply → powerScore=0
            avgQuality: 0m,
            population: 100_000);
        Assert.Equal(0m, index);
    }

    [Fact]
    public void ComputeEconomicIndex_NoPowerDemand_ReturnsPowerScore15()
    {
        // No power demand = perfect power balance (score=100), weighted 15% → index=15
        var index = EconomicReportPhase.ComputeEconomicIndex(0m, 0m, 0m, 0m, 0m, 100_000);
        Assert.Equal(15m, index);
    }

    [Fact]
    public void ComputeEconomicIndex_FullMetrics_Returns100()
    {
        // Supply very high salary+revenue per capita, full power balance, perfect quality
        var index = EconomicReportPhase.ComputeEconomicIndex(
            totalSalaries: 1_000_000m,
            totalRevenue: 1_000_000m,
            totalConsumption: 100m,
            totalSupply: 100m,
            avgQuality: 1m,
            population: 1);  // tiny population → high per-capita scores

        Assert.Equal(100m, index);
    }

    [Fact]
    public void ComputeEconomicIndex_PowerDeficit_ReducesScore()
    {
        var scoreWithPower = EconomicReportPhase.ComputeEconomicIndex(0m, 0m, 0m, 0m, 0m, 100);
        var scoreWithPowerDeficit = EconomicReportPhase.ComputeEconomicIndex(0m, 0m, 10m, 0m, 0m, 100);

        // With no demand, powerScore=100 (full). With demand but no supply, powerScore=0.
        // powerScore affects 15% of the index.
        Assert.True(scoreWithPower >= scoreWithPowerDeficit,
            $"Power deficit should reduce or maintain score. With={scoreWithPower}, Without={scoreWithPowerDeficit}");
    }

    [Fact]
    public async Task EconomicReportPhase_AllThreeCities_AllGetReports()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cities = await db.Cities.ToListAsync();
        Assert.True(cities.Count >= 3, "Should have at least 3 seeded cities");

        var gs = await db.GameStates.FirstAsync();
        gs.TaxCycleTicks = 10;
        gs.CurrentTick = 10;
        await db.SaveChangesAsync();

        var phase = new EconomicReportPhase(NullLogger<EconomicReportPhase>.Instance);
        var context = BuildContext(db, gs, []);

        await phase.ProcessAsync(context);
        await db.SaveChangesAsync();

        // Each city should have exactly one report
        foreach (var city in cities)
        {
            var report = await db.CityEconomicReports.FirstOrDefaultAsync(r => r.CityId == city.Id);
            Assert.NotNull(report);
            Assert.Equal(10L, report.TaxCycleEnd);
        }
    }

    [Fact]
    public async Task EconomicReportPhase_LedgerEntriesOutsideCycle_NotAggregated()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Use Prague to avoid admin-seeded buildings
        var city = await db.Cities.FirstAsync(c => c.Name == "Prague");
        var (companyId, buildingId) = await SeedCompanyBuildingAsync(db, city.Id);

        var gs = await db.GameStates.FirstAsync();
        gs.TaxCycleTicks = 10;
        gs.CurrentTick = 10;
        await db.SaveChangesAsync();

        // Salary at tick 15 is OUTSIDE the cycle [1..10]
        await SeedLaborCostAsync(db, companyId, buildingId, 99_999m, 15L);

        var buildings = await db.Buildings.Where(b => b.CityId == city.Id).ToListAsync();
        var phase = new EconomicReportPhase(NullLogger<EconomicReportPhase>.Instance);
        var context = BuildContext(db, gs, buildings);

        await phase.ProcessAsync(context);
        await db.SaveChangesAsync();

        var report = await db.CityEconomicReports.FirstOrDefaultAsync(r => r.CityId == city.Id);
        Assert.NotNull(report);
        Assert.Equal(0m, report.TotalSalaries); // outside cycle → not counted
    }

    [Fact]
    public async Task EconomicReportPhase_PublicSalesInventoryQuality_IsAggregatedPerCity()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var existingCitySalesInventory = await db.Inventories
            .Include(inv => inv.BuildingUnit)
            .ThenInclude(unit => unit!.Building)
            .Where(inv => inv.BuildingUnit != null
                && inv.BuildingUnit.UnitType == UnitType.PublicSales
                && inv.BuildingUnit.Building != null
                && inv.BuildingUnit.Building.CityId == city.Id)
            .ToListAsync();

        db.Inventories.RemoveRange(existingCitySalesInventory);
        await db.SaveChangesAsync();

        var (companyId, buildingId) = await SeedCompanyBuildingAsync(db, city.Id);

        // Add a PUBLIC_SALES unit to the building
        var unit = new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = buildingId,
            UnitType = UnitType.PublicSales,
            GridX = 0,
            GridY = 0,
            Level = 1,
        };
        db.BuildingUnits.Add(unit);

        // Seed inventory with quality 0.8 and positive quantity
        var product = await db.ProductTypes.FirstAsync();
        var inv = new Inventory
        {
            Id = Guid.NewGuid(),
            BuildingId = buildingId,
            BuildingUnitId = unit.Id,
            ProductTypeId = product.Id,
            Quantity = 100m,
            Quality = 0.8m,
        };
        db.Inventories.Add(inv);

        var gs = await db.GameStates.FirstAsync();
        gs.TaxCycleTicks = 10;
        gs.CurrentTick = 10;
        await db.SaveChangesAsync();

        var buildings = await db.Buildings.Where(b => b.CityId == city.Id).ToListAsync();
        var phase = new EconomicReportPhase(NullLogger<EconomicReportPhase>.Instance);
        var context = BuildContext(db, gs, buildings);
        await phase.ProcessAsync(context);
        await db.SaveChangesAsync();

        var report = await db.CityEconomicReports.FirstOrDefaultAsync(r => r.CityId == city.Id);
        Assert.NotNull(report);
        // avgQuality drawn from inventory: one item at 0.8 → AverageProductQuality = 0.8
        Assert.Equal(0.8m, report.AverageProductQuality);
        // qualityScore = quality * 100 = 80; weighted 15% → contributes 12 to the index
        Assert.True(report.EconomicIndex > 0, "Quality contribution should push index above 0");
    }
}

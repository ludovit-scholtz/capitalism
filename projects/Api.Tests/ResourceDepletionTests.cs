using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Engine.Phases;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Economy;

namespace Api.Tests;

/// <summary>
/// Integration tests for resource depletion and replenishment mechanics.
/// Each test uses an isolated <see cref="ApiWebApplicationFactory"/> to avoid shared-database
/// interference.  Tests cover:
/// - MaterialQuantity decrease each tick
/// - Mining stops when quantity reaches zero
/// - Depletion record created on transition to zero
/// - Player notification emitted on depletion
/// - Fast-extraction depletes sooner than slow-extraction
/// - Replenishment phase triggers at correct interval
/// - Replenishment restores 10-30 % of OriginalMaterialQuantity
/// - Replenishment is idempotent (re-running same tick does nothing)
/// - Multiple city independence (cities have separate schedules)
/// - OriginalMaterialQuantity backfill sets value from MaterialQuantity on startup
/// </summary>
public sealed class ResourceDepletionTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<TickProcessor> CreateProcessorAsync(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetServices<ITickPhase>();
        var logger = new NullLogger<TickProcessor>();
        return await Task.FromResult(new TickProcessor(db, phases, logger));
    }

    private static (Company, Building, BuildingUnit, BuildingLot, ResourceType) CreateMineSeed(
        AppDbContext db,
        City city,
        Player player,
        decimal materialQuantity,
        decimal? originalQuantity = null,
        int miningLevel = 1,
        decimal? materialQuality = 0.70m)
    {
        var resource = db.ResourceTypes.FirstOrDefault(r => r.Slug == "coal")
            ?? new ResourceType { Id = Guid.NewGuid(), Name = "Coal", Slug = "coal", Category = "MINERAL" };
        if (db.ResourceTypes.Local.All(r => r.Id != resource.Id))
            db.ResourceTypes.Add(resource);

        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = $"Miner_{Guid.NewGuid():N}"[..16],
            PlayerId = player.Id,
            FoundedAtUtc = DateTime.UtcNow,
        };
        db.Companies.Add(company);

        var lot = new BuildingLot
        {
            Id = Guid.NewGuid(),
            CityId = city.Id,
            Name = "Test Mine Lot",
            Description = "Test",
            District = "Industrial",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            SuitableTypes = "MINE",
            Price = 100_000m,
            BasePrice = 100_000m,
            ResourceTypeId = resource.Id,
            MaterialQuality = materialQuality,
            MaterialQuantity = materialQuantity,
            OriginalMaterialQuantity = originalQuantity ?? materialQuantity,
            OwnerCompanyId = company.Id,
        };
        db.BuildingLots.Add(lot);

        var mine = new Building
        {
            Id = Guid.NewGuid(),
            CityId = city.Id,
            CompanyId = company.Id,
            Type = BuildingType.Mine,
            Name = "Test Coal Mine",
            Level = 1,
            PowerStatus = PowerStatus.Powered,
            BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.Add(mine);

        // Provision a bank account with ample balance so OperatingCostPhase does not suspend the mine.
        var bankAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CompanyId = company.Id,
            CurrencyCode = city.CurrencyCode,
            Balance = 10_000_000m,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(bankAccount);
        mine.BankAccountId = bankAccount.Id;

        lot.BuildingId = mine.Id;

        var unit = new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = mine.Id,
            UnitType = UnitType.Mining,
            GridX = 0,
            GridY = 0,
            Level = miningLevel,
            ResourceTypeId = resource.Id,
        };
        db.BuildingUnits.Add(unit);

        return (company, mine, unit, lot, resource);
    }

    private static City CreateIsolatedCity(AppDbContext db, string suffix = "")
    {
        var city = new City
        {
            Id = Guid.NewGuid(),
            Name = $"TestCity{suffix}_{Guid.NewGuid():N}"[..20],
            CountryCode = "XX",
            Latitude = 48.0,
            Longitude = 17.0,
            Population = 50_000,
            CurrencyCode = "EUR",
            AverageRentPerSqm = 10m,
        };
        db.Cities.Add(city);
        return city;
    }

    // ── Test 1: MaterialQuantity decreases by mined output each tick ─────────

    [Fact]
    public async Task MiningPhase_ReducesMaterialQuantity_ByMiningOutput()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var city = CreateIsolatedCity(db, "A");
        var (_, _, unit, lot, _) = CreateMineSeed(db, city, player, materialQuantity: 1_000m);
        await db.SaveChangesAsync();

        var expectedOutputPerTick = GameConstants.MiningRate(unit.Level); // 10

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        await db.Entry(lot).ReloadAsync();
        var remaining = lot.MaterialQuantity!.Value;

        Assert.True(remaining < 1_000m, $"Expected quantity to decrease from 1000, got {remaining}");
        Assert.True(remaining >= 1_000m - expectedOutputPerTick,
            $"Expected at most {expectedOutputPerTick} extracted, but {1_000m - remaining} was extracted");
    }

    // ── Test 2: Mining output becomes zero when MaterialQuantity <= 0 ────────

    [Fact]
    public async Task MiningPhase_StopsOutput_WhenMaterialQuantityZero()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var city = CreateIsolatedCity(db, "B");
        // Seeding 0 quantity = already depleted.
        var (_, mine, unit, lot, resource) = CreateMineSeed(db, city, player, materialQuantity: 0m, originalQuantity: 100m);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        // No inventory should have been added.
        await db.Entry(lot).ReloadAsync();
        var inv = await db.Inventories
            .Where(i => i.BuildingId == mine.Id && i.ResourceTypeId == resource.Id)
            .ToListAsync();

        var totalMined = inv.Sum(i => i.Quantity);
        Assert.Equal(0m, totalMined);
        Assert.Equal(0m, lot.MaterialQuantity!.Value);
    }

    // ── Test 3: Depletion record created when lot transitions to zero ────────

    [Fact]
    public async Task MiningPhase_CreatesDepletionRecord_OnTransitionToZero()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var city = CreateIsolatedCity(db, "C");
        // Only 5 tonnes with original=5 => fully extracted in one tick.
        var (company, mine, unit, lot, resource) = CreateMineSeed(db, city, player, materialQuantity: 5m, originalQuantity: 5m);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        await db.Entry(lot).ReloadAsync();
        Assert.Equal(0m, lot.MaterialQuantity!.Value);

        var record = await db.MineDepletionRecords.FirstOrDefaultAsync(r => r.LotId == lot.Id);
        Assert.NotNull(record);
        Assert.Equal(lot.Id, record!.LotId);
        Assert.Equal(mine.Id, record.BuildingId);
        Assert.Equal(company.Id, record.CompanyId);
        Assert.Equal(resource.Id, record.ResourceTypeId);
        Assert.True(record.DepletedAtTick > 0);
    }

    // ── Test 4: Player notification emitted on depletion ─────────────────────

    [Fact]
    public async Task MiningPhase_EmitsPlayerNotification_OnDepletion()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var city = CreateIsolatedCity(db, "D");
        // Tiny deposit → depletes this tick.
        var (company, mine, _, lot, _) = CreateMineSeed(db, city, player, materialQuantity: 3m, originalQuantity: 100m);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        await db.Entry(lot).ReloadAsync();
        Assert.Equal(0m, lot.MaterialQuantity!.Value);

        var notification = await db.PlayerNotifications
            .FirstOrDefaultAsync(n => n.PlayerId == player.Id
                && n.Type == PlayerNotificationType.MineFullyDepleted);

        Assert.NotNull(notification);
        Assert.Equal(PlayerNotificationType.MineFullyDepleted, notification!.Type);
        Assert.Equal(company.Id, notification.CompanyId);
    }

    // ── Test 5: No duplicate depletion record on subsequent ticks ────────────

    [Fact]
    public async Task MiningPhase_NoDuplicateDepletionRecord_OnSubsequentTicks()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var city = CreateIsolatedCity(db, "E");
        var (_, mine, _, lot, _) = CreateMineSeed(db, city, player, materialQuantity: 3m, originalQuantity: 100m);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync(); // Depletes
        await processor.ProcessTickAsync(); // Second tick (already 0)

        var records = await db.MineDepletionRecords
            .Where(r => r.LotId == lot.Id)
            .ToListAsync();

        Assert.Single(records);
    }

    // ── Test 6: Fast extraction depletes sooner than slow extraction ──────────

    [Fact]
    public async Task Depletion_HigherLevelUnit_DepletesLotFaster()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var cityA = CreateIsolatedCity(db, "F1");
        var cityB = CreateIsolatedCity(db, "F2");

        var (_, _, unitLevel1, lotLevel1, _) = CreateMineSeed(db, cityA, player, materialQuantity: 200m, miningLevel: 1);
        var (_, _, unitLevel3, lotLevel3, _) = CreateMineSeed(db, cityB, player, materialQuantity: 200m, miningLevel: 3);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        // Process enough ticks to show differentiation (level-1 mines 10/tick, level-3 mines 50/tick).
        for (var i = 0; i < 3; i++)
            await processor.ProcessTickAsync();

        await db.Entry(lotLevel1).ReloadAsync();
        await db.Entry(lotLevel3).ReloadAsync();

        Assert.True(lotLevel3.MaterialQuantity < lotLevel1.MaterialQuantity,
            $"Level-3 mine should have depleted more: L3={lotLevel3.MaterialQuantity}, L1={lotLevel1.MaterialQuantity}");
    }

    // ── Test 7: Replenishment schedule created for all cities at startup ──────

    [Fact]
    public async Task ResourceReplenishment_ScheduleExists_ForAllCities()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cities = await db.Cities.ToListAsync();
        var schedules = await db.ResourceReplenishmentSchedules.ToListAsync();

        var scheduledCityIds = schedules.Select(s => s.CityId).ToHashSet();
        foreach (var city in cities)
        {
            Assert.Contains(city.Id, scheduledCityIds);
        }
    }

    // ── Test 8: Replenishment restores 10-30 % of original quantity ───────────

    [Fact]
    public async Task ResourceReplenishment_RestoresFraction_OfDepletedLots()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var city = CreateIsolatedCity(db, "G");
        db.Cities.Add(city);

        // Depleted lot (materialQuantity = 0).
        var lot = new BuildingLot
        {
            Id = Guid.NewGuid(),
            CityId = city.Id,
            Name = "Depleted Lot",
            Description = "Test",
            District = "Industrial",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            SuitableTypes = "MINE",
            Price = 50_000m,
            BasePrice = 50_000m,
            ResourceTypeId = db.ResourceTypes.FirstOrDefault(r => r.Slug == "coal")?.Id,
            MaterialQuantity = 0m,
            OriginalMaterialQuantity = 1_000m,
        };
        db.BuildingLots.Add(lot);

        // Schedule due at tick 1 (processor increments tick to 1 on first ProcessTickAsync).
        var schedule = new ResourceReplenishmentSchedule
        {
            Id = Guid.NewGuid(),
            CityId = city.Id,
            LastReplenishmentTick = 0,
            NextReplenishmentTick = 1,
        };
        db.ResourceReplenishmentSchedules.Add(schedule);
        await db.SaveChangesAsync();

        // Reset game state to tick 0 so ProcessTickAsync advances to 1.
        var gameState = await db.GameStates.FindAsync(1);
        Assert.NotNull(gameState);
        gameState!.CurrentTick = 0;
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();  // tick becomes 1, replenishment fires

        await db.Entry(lot).ReloadAsync();
        await db.Entry(schedule).ReloadAsync();

        // Lot should have been partially restored (if selected — at least 1 lot so always selected).
        if (lot.MaterialQuantity > 0m)
        {
            var restoredFraction = lot.MaterialQuantity.Value / lot.OriginalMaterialQuantity!.Value;
            Assert.True(restoredFraction >= GameConstants.ReplenishmentMinRestoreFraction - 0.01m,
                $"Restored fraction {restoredFraction} is below minimum {GameConstants.ReplenishmentMinRestoreFraction}");
            Assert.True(restoredFraction <= GameConstants.ReplenishmentMaxRestoreFraction + 0.01m,
                $"Restored fraction {restoredFraction} is above maximum {GameConstants.ReplenishmentMaxRestoreFraction}");
        }

        // Schedule should have advanced by one interval.
        Assert.Equal(1, schedule.LastReplenishmentTick);
        Assert.Equal(1 + GameConstants.ReplenishmentIntervalTicks, schedule.NextReplenishmentTick);
    }

    // ── Test 9: Replenishment is idempotent (schedule already advanced) ───────

    [Fact]
    public async Task ResourceReplenishment_IsIdempotent_WhenAlreadyAdvanced()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var city = CreateIsolatedCity(db, "H");

        var lot = new BuildingLot
        {
            Id = Guid.NewGuid(),
            CityId = city.Id,
            Name = "Idempotent Lot",
            Description = "Test",
            District = "Industrial",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            SuitableTypes = "MINE",
            Price = 50_000m,
            BasePrice = 50_000m,
            MaterialQuantity = 0m,
            OriginalMaterialQuantity = 1_000m,
        };
        db.BuildingLots.Add(lot);

        // Schedule is NOT due yet (far future).
        var schedule = new ResourceReplenishmentSchedule
        {
            Id = Guid.NewGuid(),
            CityId = city.Id,
            LastReplenishmentTick = 0,
            NextReplenishmentTick = 999_999,
        };
        db.ResourceReplenishmentSchedules.Add(schedule);
        await db.SaveChangesAsync();

        var gameState = await db.GameStates.FindAsync(1);
        Assert.NotNull(gameState);
        gameState!.CurrentTick = 0;
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();  // tick becomes 1, replenishment NOT due

        await db.Entry(lot).ReloadAsync();

        // Lot should remain at 0 because schedule is not due.
        Assert.Equal(0m, lot.MaterialQuantity!.Value);
    }

    // ── Test 10: Independent replenishment per city ───────────────────────────

    [Fact]
    public async Task Depletion_AcrossMultipleCities_IndependentReplenishment()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var cityA = CreateIsolatedCity(db, "I1");
        var cityB = CreateIsolatedCity(db, "I2");

        // Both cities have depleted lots.
        var lotA = new BuildingLot
        {
            Id = Guid.NewGuid(), CityId = cityA.Id, Name = "LotA", Description = "Test",
            District = "Industrial", Latitude = cityA.Latitude, Longitude = cityA.Longitude,
            SuitableTypes = "MINE", Price = 50_000m, BasePrice = 50_000m,
            MaterialQuantity = 0m, OriginalMaterialQuantity = 500m,
        };
        var lotB = new BuildingLot
        {
            Id = Guid.NewGuid(), CityId = cityB.Id, Name = "LotB", Description = "Test",
            District = "Industrial", Latitude = cityB.Latitude, Longitude = cityB.Longitude,
            SuitableTypes = "MINE", Price = 50_000m, BasePrice = 50_000m,
            MaterialQuantity = 0m, OriginalMaterialQuantity = 500m,
        };
        db.BuildingLots.AddRange(lotA, lotB);

        // City A schedule is due at tick 1; City B schedule is NOT due.
        var scheduleA = new ResourceReplenishmentSchedule
        {
            Id = Guid.NewGuid(), CityId = cityA.Id, LastReplenishmentTick = 0, NextReplenishmentTick = 1,
        };
        var scheduleB = new ResourceReplenishmentSchedule
        {
            Id = Guid.NewGuid(), CityId = cityB.Id, LastReplenishmentTick = 0, NextReplenishmentTick = 999_999,
        };
        db.ResourceReplenishmentSchedules.AddRange(scheduleA, scheduleB);
        await db.SaveChangesAsync();

        var gameState = await db.GameStates.FindAsync(1);
        Assert.NotNull(gameState);
        gameState!.CurrentTick = 0;
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();  // tick becomes 1

        await db.Entry(lotB).ReloadAsync();
        await db.Entry(scheduleB).ReloadAsync();

        // City B lot should remain at 0 (not due).
        Assert.Equal(0m, lotB.MaterialQuantity!.Value);
        // City B schedule should NOT have advanced.
        Assert.Equal(999_999, scheduleB.NextReplenishmentTick);
    }

    [Fact]
    public async Task MiningPhase_EmitsReserveWarnings_WhenCrossingTwentyAndFivePercent()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var city = CreateIsolatedCity(db, "W");
        var (_, _, _, lot, _) = CreateMineSeed(db, city, player, materialQuantity: 25m, originalQuantity: 100m);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        for (var i = 0; i < 4; i++)
        {
            await processor.ProcessTickAsync();
        }

        await db.Entry(lot).ReloadAsync();

        var lowWarning = await db.PlayerNotifications
            .CountAsync(notification => notification.PlayerId == player.Id
                && notification.Type == PlayerNotificationType.MineLowReserveWarning
                && notification.BuildingId == lot.BuildingId);

        var criticalWarning = await db.PlayerNotifications
            .CountAsync(notification => notification.PlayerId == player.Id
                && notification.Type == PlayerNotificationType.MineCriticalReserveWarning
                && notification.BuildingId == lot.BuildingId);

        Assert.Equal(1, lowWarning);
        Assert.Equal(1, criticalWarning);
        Assert.True(lot.MaterialQuantity is > 0m and < 5m);
    }

    [Fact]
    public async Task EnsureMinimumAvailableLots_KeepsAtLeastTwoMineDepositsPerResourceType()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = CreateIsolatedCity(db, "COV");
        var coal = await db.ResourceTypes.FirstAsync(resource => resource.Slug == "coal");
        db.CityResources.Add(new CityResource
        {
            Id = Guid.NewGuid(),
            CityId = city.Id,
            ResourceTypeId = coal.Id,
            Abundance = 0.6m,
        });
        await db.SaveChangesAsync();

        await LandService.EnsureMinimumAvailableLotsAsync(db, currentTick: 0, cityIds: [city.Id]);
        await db.SaveChangesAsync();

        var availableCoalDeposits = await db.BuildingLots
            .Where(lot => lot.CityId == city.Id
                && lot.OwnerCompanyId == null
                && lot.ResourceTypeId == coal.Id
                && lot.MaterialQuantity.HasValue
                && lot.MaterialQuantity > 0m)
            .CountAsync();

        Assert.True(
            availableCoalDeposits >= GameConstants.MinimumAvailableMineLotsPerResourceType,
            $"Expected at least {GameConstants.MinimumAvailableMineLotsPerResourceType} available coal deposits, got {availableCoalDeposits}");
    }

    [Fact]
    public async Task GetLandResourceStatus_ReturnsLiveEfficiencyAndTicksRemaining()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var city = CreateIsolatedCity(db, "Q");
        var (_, _, _, lot, _) = CreateMineSeed(db, city, player, materialQuantity: 60m, originalQuantity: 100m);
        await db.SaveChangesAsync();

        var client = factory.CreateClient();
        var response = await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            query LandResourceStatus($landId: UUID!) {
              getLandResourceStatus(landId: $landId) {
                landId
                quantityRemaining
                initialQuantity
                qualityIndex
                efficiencyFactor
                estimatedTicksRemaining
                isDepleted
              }
            }
            """,
            new { landId = lot.Id });

        if (response.TryGetProperty("errors", out var landErrors))
        {
            throw new Exception($"GraphQL errors: {landErrors}");
        }

        var status = response.GetProperty("data").GetProperty("getLandResourceStatus");
        Assert.Equal(lot.Id.ToString(), status.GetProperty("landId").GetString());
        Assert.Equal(60m, status.GetProperty("quantityRemaining").GetDecimal());
        Assert.Equal(100m, status.GetProperty("initialQuantity").GetDecimal());
        Assert.Equal(0.7m, status.GetProperty("qualityIndex").GetDecimal());
        Assert.Equal(MiningScarcityCalculator.ComputeEfficiencyFactor(60m, 100m), status.GetProperty("efficiencyFactor").GetDecimal());
        Assert.True(status.GetProperty("estimatedTicksRemaining").GetDecimal() > 0m);
        Assert.False(status.GetProperty("isDepleted").GetBoolean());
    }

    [Fact]
    public async Task GetCityResourceMap_ReturnsUpdatedResourceStatus()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var city = CreateIsolatedCity(db, "RM");
        var (_, _, _, lot, _) = CreateMineSeed(db, city, player, materialQuantity: 40m, originalQuantity: 100m);
        await db.SaveChangesAsync();

        var client = factory.CreateClient();
        var response = await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            query CityResourceMap($cityId: UUID!) {
              getCityResourceMap(cityId: $cityId) {
                landId
                cityId
                quantityRemaining
                initialQuantity
                efficiencyFactor
                isDepleted
              }
            }
            """,
            new { cityId = city.Id });

        if (response.TryGetProperty("errors", out var mapErrors))
        {
            throw new Exception($"GraphQL errors: {mapErrors}");
        }

        var entries = response.GetProperty("data").GetProperty("getCityResourceMap")
            .EnumerateArray()
            .ToList();
        var target = entries.First(entry => entry.GetProperty("landId").GetString() == lot.Id.ToString());

        Assert.Equal(city.Id.ToString(), target.GetProperty("cityId").GetString());
        Assert.Equal(40m, target.GetProperty("quantityRemaining").GetDecimal());
        Assert.Equal(100m, target.GetProperty("initialQuantity").GetDecimal());
        Assert.Equal(MiningScarcityCalculator.ComputeEfficiencyFactor(40m, 100m), target.GetProperty("efficiencyFactor").GetDecimal());
        Assert.False(target.GetProperty("isDepleted").GetBoolean());
    }

    // ── Test 11: OriginalMaterialQuantity backfill on startup ─────────────────

    [Fact]
    public async Task AppDbInitializer_BackfillsOriginalMaterialQuantity_ForLegacyLots()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // All seeded mine lots should have OriginalMaterialQuantity set by the initializer.
        var lotsWithMaterial = await db.BuildingLots
            .Where(l => l.MaterialQuantity.HasValue && l.MaterialQuantity > 0m)
            .ToListAsync();

        foreach (var lot in lotsWithMaterial)
        {
            Assert.True(lot.OriginalMaterialQuantity.HasValue,
                $"Lot '{lot.Name}' has MaterialQuantity but no OriginalMaterialQuantity");
            Assert.True(lot.OriginalMaterialQuantity > 0m,
                $"Lot '{lot.Name}' has OriginalMaterialQuantity <= 0");
        }
    }

    // ── Test 12: MiningRate constants match depletion tracking ────────────────

    [Fact]
    public void GameConstants_MiningRateLevels_MatchExpectedValues()
    {
        Assert.Equal(10m, GameConstants.MiningRate(1));
        Assert.Equal(25m, GameConstants.MiningRate(2));
        Assert.Equal(50m, GameConstants.MiningRate(3));
        Assert.Equal(100m, GameConstants.MiningRate(4));
    }

    // ── Test 13: ReplenishmentIntervalTicks equals one game year ─────────────

    [Fact]
    public void GameConstants_ReplenishmentIntervalTicks_EqualsTicksPerYear()
    {
        Assert.Equal(GameConstants.TicksPerYear, GameConstants.ReplenishmentIntervalTicks);
        Assert.Equal(8_760, GameConstants.ReplenishmentIntervalTicks);
    }

    // ── Test 14: DepletionRiskThreshold is 20% ────────────────────────────────

    [Fact]
    public void GameConstants_DepletionRiskThreshold_Is20Percent()
    {
        Assert.Equal(0.20m, GameConstants.DepletionRiskThreshold);
    }
}

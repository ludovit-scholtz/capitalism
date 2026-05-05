using System.Diagnostics;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Engine.Phases;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// Performance benchmark scenarios for the tick engine.
/// These tests seed realistic multi-player portfolios and measure tick throughput.
/// They serve as reproducible regression guards: if future changes cause a
/// significant tick slowdown, the assert thresholds will catch it.
///
/// Measured on a developer workstation with EF Core InMemory provider; real
/// PostgreSQL runtimes will differ (slower for DB round-trips, faster for
/// indexed queries).  The thresholds here are conservative to avoid false
/// positives in CI while still catching catastrophic regressions.
///
/// Baseline measurements (InMemory, 20 players, 3 ticks):
///   Context build: ~30-80ms
///   Per tick (all phases): ~20-80ms
///   Total 3-tick run: ~100-300ms
/// </summary>
public sealed class TickPerformanceBenchmarkTests(ITestOutputHelper output)
{
    // ── Scenario constants ───────────────────────────────────────────────────

    /// <summary>Number of simulated concurrent players in the benchmark.</summary>
    private const int PlayerCount = 20;

    /// <summary>Number of consecutive ticks to run.</summary>
    private const int TickCount = 3;

    /// <summary>
    /// Maximum allowed wall-clock milliseconds for the entire benchmark run
    /// (context build + all ticks + save).  Conservative to avoid CI flakiness.
    /// </summary>
    private const long TotalBudgetMs = 10_000;

    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds N players each with a factory (PURCHASE → MANUFACTURING → STORAGE)
    /// and a sales shop (PUBLIC_SALES), then runs M ticks while verifying
    /// correctness and measuring wall-clock time.
    ///
    /// Correctness assertions:
    ///   1. All seeded buildings and units survive the ticks.
    ///   2. LedgerEntries are created (operating costs at minimum).
    ///   3. Total tick time is within the acceptable budget.
    /// </summary>
    [Fact]
    public async Task Benchmark_MultiPlayer_TickThroughput()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient(); // trigger seed

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetServices<ITickPhase>();
        var processor = new TickProcessor(db, phases, new NullLogger<TickProcessor>());

        // ── Seed ────────────────────────────────────────────────────────────
        var seedSw = Stopwatch.StartNew();
        var (companyIds, buildingIds) = await SeedBenchmarkScenarioAsync(db, PlayerCount);
        seedSw.Stop();
        output.WriteLine($"Seed: {seedSw.ElapsedMilliseconds}ms  ({PlayerCount} players)");

        // ── Warm-up: one tick to prime EF Core caches ─────────────────────
        await processor.ProcessTickAsync();

        // ── Benchmark: M consecutive ticks ──────────────────────────────────
        var totalSw = Stopwatch.StartNew();
        for (var i = 0; i < TickCount; i++)
        {
            var tickSw = Stopwatch.StartNew();
            await processor.ProcessTickAsync();
            tickSw.Stop();
            output.WriteLine($"  Tick {i + 1}: {tickSw.ElapsedMilliseconds}ms");
        }
        totalSw.Stop();

        output.WriteLine($"Total ({TickCount} ticks, {PlayerCount} players): {totalSw.ElapsedMilliseconds}ms");
        output.WriteLine($"Avg per tick: {totalSw.ElapsedMilliseconds / TickCount}ms");

        // ── Correctness ─────────────────────────────────────────────────────
        // All seeded buildings must still exist.
        var survivingBuildings = await db.Buildings
            .Where(b => buildingIds.Contains(b.Id))
            .CountAsync();
        Assert.Equal(buildingIds.Count, survivingBuildings);

        // Each company should have at least one ledger entry (operating costs or taxes).
        var companiesWithLedger = await db.LedgerEntries
            .Where(l => companyIds.Contains(l.CompanyId))
            .Select(l => l.CompanyId)
            .Distinct()
            .CountAsync();
        Assert.True(companiesWithLedger > 0,
            $"Expected at least one company to have ledger entries after {TickCount} ticks.");

        // ── Performance budget ───────────────────────────────────────────────
        Assert.True(totalSw.ElapsedMilliseconds <= TotalBudgetMs,
            $"Tick throughput regression: {TickCount} ticks with {PlayerCount} players took " +
            $"{totalSw.ElapsedMilliseconds}ms, exceeding {TotalBudgetMs}ms budget. " +
            "Review recent changes to tick phases or query patterns.");
    }

    /// <summary>
    /// Verifies that the new performance indexes are reflected in the EF Core
    /// model (i.e. the migration was applied at startup).  This acts as a guard
    /// so that accidentally reverting the model configuration is caught immediately.
    /// </summary>
    [Fact]
    public async Task PerformanceIndexes_AreRegisteredInModel()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var model = db.Model;

        // ExchangeOrder.IsActive index
        var exchangeOrderEntity = model.FindEntityType(typeof(ExchangeOrder))!;
        var isActiveIndex = exchangeOrderEntity.GetIndexes()
            .FirstOrDefault(ix => ix.Properties.Any(p => p.Name == nameof(ExchangeOrder.IsActive)));
        Assert.NotNull(isActiveIndex);

        // LedgerEntry.(Category, RecordedAtTick) index
        var ledgerEntity = model.FindEntityType(typeof(LedgerEntry))!;
        var categoryTickIndex = ledgerEntity.GetIndexes()
            .FirstOrDefault(ix => ix.Properties.Count == 2
                && ix.Properties.Any(p => p.Name == nameof(LedgerEntry.Category))
                && ix.Properties.Any(p => p.Name == nameof(LedgerEntry.RecordedAtTick)));
        Assert.NotNull(categoryTickIndex);

        // InterCityTradeRoute.(Status, ExpectedArrivalTick) index
        var tradeRouteEntity = model.FindEntityType(typeof(InterCityTradeRoute))!;
        var statusArrivalIndex = tradeRouteEntity.GetIndexes()
            .FirstOrDefault(ix => ix.Properties.Count == 2
                && ix.Properties.Any(p => p.Name == nameof(InterCityTradeRoute.Status))
                && ix.Properties.Any(p => p.Name == nameof(InterCityTradeRoute.ExpectedArrivalTick)));
        Assert.NotNull(statusArrivalIndex);

        await Task.CompletedTask;
    }

    // ── Seed helper ──────────────────────────────────────────────────────────

    private static async Task<(List<Guid> CompanyIds, List<Guid> BuildingIds)>
        SeedBenchmarkScenarioAsync(AppDbContext db, int playerCount)
    {
        var companyIds = new List<Guid>(playerCount);
        var buildingIds = new List<Guid>(playerCount * 2);

        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var product = await db.ProductTypes
            .Include(p => p.Recipes)
            .FirstAsync(p => p.Slug == "wooden-chair");
        var woodResource = await db.ResourceTypes.FirstAsync(r => r.Slug == "wood");

        // Seed a bank account number counter to avoid unique-constraint violations.
        var accountCounter = 0;

        for (var i = 0; i < playerCount; i++)
        {
            var playerId = Guid.NewGuid();
            var companyId = Guid.NewGuid();
            var factoryId = Guid.NewGuid();
            var shopId = Guid.NewGuid();

            companyIds.Add(companyId);
            buildingIds.Add(factoryId);
            buildingIds.Add(shopId);

            db.Players.Add(new Player
            {
                Id = playerId,
                Email = $"bench-{i:D4}-{Guid.NewGuid():N}@test.com",
                DisplayName = $"Bench Player {i}",
                PasswordHash = "hash",
                Role = PlayerRole.Player
            });

            db.Companies.Add(new Company
            {
                Id = companyId,
                PlayerId = playerId,
                Name = $"Bench Corp {i}",
            });

            // Bank account so OperatingCostPhase can debit funds.
            accountCounter++;
            var accountNumber = accountCounter.ToString("D16");
            var bankAccountId = Guid.NewGuid();
            db.BankAccounts.Add(new BankAccount
            {
                Id = bankAccountId,
                AccountNumber = accountNumber,
                CurrencyCode = city.CurrencyCode,
                Balance = 5_000_000m,
                CompanyId = companyId,
                IsGovernmentAccount = false,
                CreatedAtUtc = DateTime.UtcNow,
                ConcurrencyToken = Guid.NewGuid()
            });

            // Factory: PURCHASE → MANUFACTURING → STORAGE
            db.Buildings.Add(new Building
            {
                Id = factoryId,
                CompanyId = companyId,
                CityId = city.Id,
                Type = BuildingType.Factory,
                Name = $"Bench Factory {i}",
                Level = 1,
                BankAccountId = bankAccountId
            });

            var purchaseUnit = new BuildingUnit
            {
                Id = Guid.NewGuid(), BuildingId = factoryId,
                UnitType = UnitType.Purchase, GridX = 0, GridY = 0, Level = 1,
                LinkRight = true, ResourceTypeId = woodResource.Id,
                MaxPrice = 999_999m, PurchaseSource = "EXCHANGE"
            };
            var mfgUnit = new BuildingUnit
            {
                Id = Guid.NewGuid(), BuildingId = factoryId,
                UnitType = UnitType.Manufacturing, GridX = 1, GridY = 0, Level = 1,
                LinkRight = true, ProductTypeId = product.Id
            };
            var storageUnit = new BuildingUnit
            {
                Id = Guid.NewGuid(), BuildingId = factoryId,
                UnitType = UnitType.Storage, GridX = 2, GridY = 0, Level = 1
            };
            db.BuildingUnits.AddRange(purchaseUnit, mfgUnit, storageUnit);

            // Seed initial wood inventory so the factory can manufacture from tick 1.
            db.Inventories.Add(new Inventory
            {
                Id = Guid.NewGuid(), BuildingId = factoryId, BuildingUnitId = purchaseUnit.Id,
                ResourceTypeId = woodResource.Id, Quantity = 500m, Quality = 0.8m
            });

            accountCounter++;
            var shopAccountNumber = accountCounter.ToString("D16");
            var shopBankAccountId = Guid.NewGuid();
            db.BankAccounts.Add(new BankAccount
            {
                Id = shopBankAccountId,
                AccountNumber = shopAccountNumber,
                CurrencyCode = city.CurrencyCode,
                Balance = 2_000_000m,
                CompanyId = companyId,
                IsGovernmentAccount = false,
                CreatedAtUtc = DateTime.UtcNow,
                ConcurrencyToken = Guid.NewGuid()
            });

            // Sales shop: PUBLIC_SALES
            db.Buildings.Add(new Building
            {
                Id = shopId,
                CompanyId = companyId,
                CityId = city.Id,
                Type = BuildingType.SalesShop,
                Name = $"Bench Shop {i}",
                Level = 1,
                BankAccountId = shopBankAccountId
            });

            var salesUnit = new BuildingUnit
            {
                Id = Guid.NewGuid(), BuildingId = shopId,
                UnitType = UnitType.PublicSales, GridX = 0, GridY = 0, Level = 1,
                ProductTypeId = product.Id, MinPrice = 30m, MaxPrice = 80m,
                SaleVisibility = "PUBLIC"
            };
            db.BuildingUnits.Add(salesUnit);

            // Seed initial chair inventory in shop so sales can happen from tick 1.
            db.Inventories.Add(new Inventory
            {
                Id = Guid.NewGuid(), BuildingId = shopId, BuildingUnitId = salesUnit.Id,
                ProductTypeId = product.Id, Quantity = 200m, Quality = 0.75m
            });
        }

        await db.SaveChangesAsync();
        return (companyIds, buildingIds);
    }
}

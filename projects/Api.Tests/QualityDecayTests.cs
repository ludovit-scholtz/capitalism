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
/// Integration tests for <see cref="QualityDecayPhase"/>.
///
/// Coverage:
/// - Perishable product quality decays each tick
/// - Non-perishable product quality is unaffected
/// - When quality reaches zero the inventory row is removed
/// - When quality reaches zero an InventorySpoilageRecord is created
/// </summary>
public sealed class QualityDecayTests
{
    private static Task<TickProcessor> CreateProcessorAsync(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetServices<ITickPhase>();
        return Task.FromResult(new TickProcessor(db, phases, new NullLogger<TickProcessor>()));
    }

    /// <summary>
    /// Seeds a factory building with a STORAGE unit containing a single inventory item of the given product.
    /// </summary>
    private static async Task<(Guid CompanyId, Guid BuildingId, Guid StorageUnitId, Guid InventoryId)> SeedStorageInventoryAsync(
        AppDbContext db,
        Guid productTypeId,
        decimal quality = 0.5m,
        decimal quantity = 100m)
    {
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var player = new Player
        {
            Id = Guid.NewGuid(),
            Email = $"qdecay-{Guid.NewGuid():N}@test.com",
            DisplayName = "QDecay Player",
            PasswordHash = "hash",
            Role = PlayerRole.Player,
        };
        db.Players.Add(player);

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Name = "QDecay Corp",
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
            TotalSharesIssued = 10_000m,
            DividendPayoutRatio = 0.2m,
        };
        db.Companies.Add(company);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "QDecay Factory",
            Level = 1,
        };
        db.Buildings.Add(building);

        var storageUnit = new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            UnitType = UnitType.Storage,
            GridX = 0, GridY = 0,
            Level = 1,
        };
        db.BuildingUnits.Add(storageUnit);

        var inventory = new Inventory
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            BuildingUnitId = storageUnit.Id,
            ProductTypeId = productTypeId,
            Quantity = quantity,
            Quality = quality,
            SourcingCostTotal = quantity * 10m, // 10 per unit for loss calculation
        };
        db.Inventories.Add(inventory);

        await db.SaveChangesAsync();

        return (company.Id, building.Id, storageUnit.Id, inventory.Id);
    }

    [Fact]
    public async Task QualityDecay_PerishableProduct_DecaysEachTick()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient(); // Ensure DB is seeded

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Get a FoodProcessing product (should be marked perishable by initializer)
        var perishableProduct = await db.ProductTypes
            .FirstOrDefaultAsync(p => p.Industry == Industry.FoodProcessing);

        if (perishableProduct is null)
        {
            // If not seeded yet, create one manually
            perishableProduct = new ProductType
            {
                Id = Guid.NewGuid(),
                Name = "Test Bread",
                Slug = $"test-bread-{Guid.NewGuid():N}",
                Industry = Industry.FoodProcessing,
                BasePrice = 2m,
                IsPerishable = true,
            };
            db.ProductTypes.Add(perishableProduct);
            await db.SaveChangesAsync();
        }
        else if (!perishableProduct.IsPerishable)
        {
            perishableProduct.IsPerishable = true;
            await db.SaveChangesAsync();
        }

        const decimal initialQuality = 0.5m;
        var (_, _, _, inventoryId) = await SeedStorageInventoryAsync(db, perishableProduct.Id, quality: initialQuality);

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        // Reload inventory
        var updatedInventory = await db.Inventories.FindAsync(inventoryId);
        Assert.NotNull(updatedInventory);
        Assert.True(updatedInventory.Quality < initialQuality,
            $"Quality should have decreased from {initialQuality} but was {updatedInventory.Quality}.");
        Assert.True(updatedInventory.Quality >= initialQuality - GameConstants.QualityDecayRatePerTick - 0.001m,
            $"Quality should have decreased by approximately {GameConstants.QualityDecayRatePerTick}.");
    }

    [Fact]
    public async Task QualityDecay_NonPerishableProduct_NotDecayed()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Get a Furniture product (should NOT be perishable)
        var nonPerishableProduct = await db.ProductTypes
            .FirstOrDefaultAsync(p => p.Industry == Industry.Furniture && !p.IsPerishable);

        if (nonPerishableProduct is null)
        {
            nonPerishableProduct = new ProductType
            {
                Id = Guid.NewGuid(),
                Name = "Test Chair",
                Slug = $"test-chair-{Guid.NewGuid():N}",
                Industry = Industry.Furniture,
                BasePrice = 50m,
                IsPerishable = false,
            };
            db.ProductTypes.Add(nonPerishableProduct);
            await db.SaveChangesAsync();
        }

        const decimal initialQuality = 0.7m;
        var (_, _, _, inventoryId) = await SeedStorageInventoryAsync(db, nonPerishableProduct.Id, quality: initialQuality);

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        var updatedInventory = await db.Inventories.FindAsync(inventoryId);
        Assert.NotNull(updatedInventory);
        Assert.True(updatedInventory.Quality == initialQuality,
            $"Non-perishable product quality should not change. Expected {initialQuality}, got {updatedInventory.Quality}.");
    }

    [Fact]
    public async Task QualityDecay_ZeroQuality_InventoryRemoved()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Create a perishable product at near-zero quality so one tick pushes it to 0
        var perishableProduct = new ProductType
        {
            Id = Guid.NewGuid(),
            Name = "Spoiling Food",
            Slug = $"spoiling-food-{Guid.NewGuid():N}",
            Industry = Industry.FoodProcessing,
            BasePrice = 2m,
            IsPerishable = true,
        };
        db.ProductTypes.Add(perishableProduct);
        await db.SaveChangesAsync();

        // Set quality to exactly the decay rate so one tick brings it to 0
        var nearZeroQuality = GameConstants.QualityDecayRatePerTick;
        var (_, _, _, inventoryId) = await SeedStorageInventoryAsync(db, perishableProduct.Id, quality: nearZeroQuality);

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        // Inventory should be removed
        var removed = await db.Inventories.FindAsync(inventoryId);
        Assert.Null(removed);
    }

    [Fact]
    public async Task QualityDecay_ZeroQuality_SpoilageRecordCreated()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var perishableProduct = new ProductType
        {
            Id = Guid.NewGuid(),
            Name = "Spoiling Meds",
            Slug = $"spoiling-meds-{Guid.NewGuid():N}",
            Industry = Industry.Healthcare,
            BasePrice = 5m,
            IsPerishable = true,
        };
        db.ProductTypes.Add(perishableProduct);
        await db.SaveChangesAsync();

        var nearZeroQuality = GameConstants.QualityDecayRatePerTick;
        var (companyId, buildingId, _, _) = await SeedStorageInventoryAsync(db, perishableProduct.Id, quality: nearZeroQuality, quantity: 50m);

        var tickBefore = (await db.GameStates.FirstAsync()).CurrentTick;

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        var tickAfter = tickBefore + 1;

        // Spoilage record should be created
        var spoilageRecord = await db.InventorySpoilageRecords
            .FirstOrDefaultAsync(r => r.CompanyId == companyId
                                   && r.BuildingId == buildingId
                                   && r.ProductTypeId == perishableProduct.Id);

        Assert.NotNull(spoilageRecord);
        Assert.Equal(50m, spoilageRecord.QuantitySpoiled);
        Assert.Equal(tickAfter, spoilageRecord.RecordedAtTick);

        // Ledger entry for spoilage should also be created
        var ledgerEntry = await db.LedgerEntries
            .FirstOrDefaultAsync(l => l.CompanyId == companyId
                                   && l.Category == LedgerCategory.SpoilageLoss
                                   && l.ProductTypeId == perishableProduct.Id);

        Assert.NotNull(ledgerEntry);
        Assert.True(ledgerEntry.Amount <= 0m, "Spoilage loss should be a negative (expense) ledger entry.");
    }
}

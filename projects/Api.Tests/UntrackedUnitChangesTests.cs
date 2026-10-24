using System.Reflection;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

/// <summary>The mechanism behind untracked unit loading (#646); the end-to-end equivalence is in <see cref="UntrackedUnitsDifferentialTests"/>.</summary>
public sealed class UntrackedUnitChangesTests
{
    private static AppDbContext NewDb()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"untracked-units-{Guid.NewGuid():N}").Options);

    private static IEnumerable<PropertyInfo> ScalarProperties(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property =>
        {
            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            return property.CanRead && property.CanWrite
                && (propertyType.IsPrimitive || propertyType == typeof(string) || propertyType == typeof(decimal) || propertyType == typeof(Guid));
        });

    private static BuildingUnit NewUnit(Guid? buildingId = null, int gridX = 0)
        => new()
        {
            Id = Guid.NewGuid(), BuildingId = buildingId ?? Guid.NewGuid(), UnitType = UnitType.PublicSales, GridX = gridX, GridY = 0, Level = 1,
            ProductTypeId = Guid.NewGuid(), MinPrice = 5m,
        };

    [Fact]
    public void TheSnapshotCoversEveryColumnOfAUnit()
    {
        var unitColumns = ScalarProperties(typeof(BuildingUnit)).Select(property => property.Name).Where(name => name != nameof(BuildingUnit.Id)).Order();
        var snapshotColumns = typeof(BuildingUnitSnapshot).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0).Select(property => property.Name).Order();

        // A column missing here would be changed in memory by a phase but never saved. Add it to BuildingUnitSnapshot.
        Assert.Equal(unitColumns, snapshotColumns);
    }

    [Fact]
    public void ChangingAnyColumnMakesTheUnitCountAsChanged()
    {
        foreach (var property in ScalarProperties(typeof(BuildingUnit)).Where(property => property.Name != nameof(BuildingUnit.Id)))
        {
            var unit = NewUnit();
            var changes = UntrackedUnitChanges.Capture([unit]);
            Assert.Empty(changes.ChangedUnits());

            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            var current = property.GetValue(unit);
            object next = type == typeof(bool) ? !(current is true)
                : type == typeof(int) ? ((current as int?) ?? 0) + 1
                : type == typeof(decimal) ? ((current as decimal?) ?? 0m) + 1.25m
                : type == typeof(Guid) ? Guid.NewGuid()
                : type == typeof(string) ? ((current as string) ?? string.Empty) + "x"
                : throw new InvalidOperationException($"Unsupported column type {type} of {property.Name}.");
            property.SetValue(unit, next);

            Assert.True(changes.ChangedUnits().Contains(unit), $"A change to {property.Name} was not noticed.");
        }
    }

    [Fact]
    public async Task OnlyChangedUnitsAreHandedToTheContext_AndTheirChangesAreSaved()
    {
        await using var db = NewDb();
        var changed = NewUnit();
        var untouched = NewUnit(gridX: 1);
        db.BuildingUnits.AddRange(changed, untouched);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var loaded = await db.BuildingUnits.AsNoTracking().ToListAsync();
        var changes = UntrackedUnitChanges.Capture(loaded);
        loaded.Single(unit => unit.Id == changed.Id).MinPrice = 9.5m;
        loaded.Single(unit => unit.Id == changed.Id).IsLowInventoryAlertActive = true;

        changes.MarkChangedUnits(db);

        var tracked = Assert.Single(db.ChangeTracker.Entries<BuildingUnit>());
        Assert.Equal(changed.Id, tracked.Entity.Id);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var saved = await db.BuildingUnits.AsNoTracking().ToDictionaryAsync(unit => unit.Id);
        Assert.Equal(9.5m, saved[changed.Id].MinPrice);
        Assert.True(saved[changed.Id].IsLowInventoryAlertActive);
        Assert.Equal(5m, saved[untouched.Id].MinPrice);
    }

    [Fact]
    public async Task ADifferentInstanceThatTheContextAlreadyTracks_GetsTheChangedValuesInsteadOfAConflict()
    {
        await using var db = NewDb();
        var original = NewUnit();
        db.BuildingUnits.Add(original);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var untracked = (await db.BuildingUnits.AsNoTracking().ToListAsync()).Single();
        var changes = UntrackedUnitChanges.Capture([untracked]);
        var trackedCopy = await db.BuildingUnits.SingleAsync(unit => unit.Id == original.Id); // e.g. loaded by a phase's own query
        untracked.MinPrice = 12m;

        changes.MarkChangedUnits(db);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        Assert.Equal(12m, (await db.BuildingUnits.AsNoTracking().SingleAsync()).MinPrice);
        Assert.Equal(12m, trackedCopy.MinPrice);
    }

    [Fact]
    public async Task UnitsTheContextRemovedOrAddedAreLeftToItsOwnTracking()
    {
        await using var db = NewDb();
        var removed = NewUnit();
        db.BuildingUnits.Add(removed);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var untracked = (await db.BuildingUnits.AsNoTracking().ToListAsync()).Single();
        var changes = UntrackedUnitChanges.Capture([untracked]);
        untracked.MinPrice = 99m;
        db.BuildingUnits.Remove(untracked); // a service deleted it in the same tick

        changes.MarkChangedUnits(db);
        await db.SaveChangesAsync();

        Assert.False(await db.BuildingUnits.AnyAsync());
    }

    // ── Context loading ──────────────────────────────────────────────────────

    private static async Task<(int TrackedUnits, int UnitsInContext)> LoadContextAsync(bool trackBuildingUnits)
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await TickBenchmarkWorld.SeedAsync(db, new TickBenchmarkScale(2, 10, 6), seed: 5);
        db.ChangeTracker.Clear();

        var processor = new TickProcessor(db, [], NullLogger<TickProcessor>.Instance, trackBuildingUnits: trackBuildingUnits);
        var context = await processor.BuildContextForTestAsync();

        return (db.ChangeTracker.Entries<BuildingUnit>().Count(), context.UnitsByBuilding.Values.Sum(units => units.Count));
    }

    [Fact]
    public async Task TrackedLoading_TracksEveryUnit()
    {
        var (tracked, inContext) = await LoadContextAsync(trackBuildingUnits: true);

        Assert.True(inContext > 0);
        Assert.Equal(inContext, tracked);
    }

    [Fact]
    public async Task UntrackedLoading_HasTheSameUnitsInTheContextButTracksNone()
    {
        var (trackedByTracked, inContextTracked) = await LoadContextAsync(trackBuildingUnits: true);
        var (tracked, inContext) = await LoadContextAsync(trackBuildingUnits: false);

        Assert.Equal(0, tracked);
        Assert.Equal(inContextTracked, inContext);
        Assert.True(trackedByTracked > 0);
    }
}

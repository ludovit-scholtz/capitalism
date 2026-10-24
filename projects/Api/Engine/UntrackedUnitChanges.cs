using Api.Data;
using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine;

/// <summary>
/// Every column of a <see cref="BuildingUnit"/> that a phase can change, copied when the tick loads the units. Comparing it with the
/// unit at the end of the tick finds the units that need saving without asking Entity Framework to track all of them. Adding a column
/// to <see cref="BuildingUnit"/> without adding it here is caught by a test, because a change to it would otherwise never be saved.
/// </summary>
public readonly record struct BuildingUnitSnapshot(
    Guid BuildingId,
    string UnitType,
    int GridX,
    int GridY,
    int Level,
    bool LinkUp,
    bool LinkDown,
    bool LinkLeft,
    bool LinkRight,
    bool LinkUpLeft,
    bool LinkUpRight,
    bool LinkDownLeft,
    bool LinkDownRight,
    Guid? ResourceTypeId,
    Guid? ProductTypeId,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? PurchaseSource,
    string? SaleVisibility,
    decimal? Budget,
    Guid? MediaHouseBuildingId,
    decimal? MinQuality,
    string? BrandScope,
    Guid? VendorLockCompanyId,
    Guid? LockedCityId,
    string? IndustryCategory,
    decimal? LowInventoryAlertThreshold,
    bool IsLowInventoryAlertActive)
{
    public static BuildingUnitSnapshot From(BuildingUnit unit) => new(
        unit.BuildingId,
        unit.UnitType,
        unit.GridX,
        unit.GridY,
        unit.Level,
        unit.LinkUp,
        unit.LinkDown,
        unit.LinkLeft,
        unit.LinkRight,
        unit.LinkUpLeft,
        unit.LinkUpRight,
        unit.LinkDownLeft,
        unit.LinkDownRight,
        unit.ResourceTypeId,
        unit.ProductTypeId,
        unit.MinPrice,
        unit.MaxPrice,
        unit.PurchaseSource,
        unit.SaleVisibility,
        unit.Budget,
        unit.MediaHouseBuildingId,
        unit.MinQuality,
        unit.BrandScope,
        unit.VendorLockCompanyId,
        unit.LockedCityId,
        unit.IndustryCategory,
        unit.LowInventoryAlertThreshold,
        unit.IsLowInventoryAlertActive);
}

/// <summary>
/// The tick loads building units without change tracking: tracking 50,000 units cost about 0.7 s of context load and 0.2 s of change
/// detection per tick at 2,000 buildings, and 8 s at 20,000 (#646). Phases still change units in memory; this finds the changed
/// ones by comparing against the snapshot taken at load and tells the context to save them.
/// </summary>
public sealed class UntrackedUnitChanges
{
    private readonly List<(BuildingUnit Unit, BuildingUnitSnapshot Original)> _units;

    private UntrackedUnitChanges(List<(BuildingUnit, BuildingUnitSnapshot)> units) => _units = units;

    public int Count => _units.Count;

    public static UntrackedUnitChanges Capture(IReadOnlyCollection<BuildingUnit> units)
    {
        var captured = new List<(BuildingUnit, BuildingUnitSnapshot)>(units.Count);
        foreach (var unit in units)
        {
            captured.Add((unit, BuildingUnitSnapshot.From(unit)));
        }

        return new UntrackedUnitChanges(captured);
    }

    /// <summary>The loaded units that differ from the moment they were loaded.</summary>
    public IEnumerable<BuildingUnit> ChangedUnits()
    {
        foreach (var (unit, original) in _units)
        {
            if (original != BuildingUnitSnapshot.From(unit))
            {
                yield return unit;
            }
        }
    }

    /// <summary>
    /// Makes the context save the units that changed. Units the context already tracks (attached for a due configuration plan, added or
    /// removed) are left to its own change detection. If a different instance of the same unit is tracked (a query that loaded it
    /// tracked), the changed values are copied onto that instance instead of attaching a second one.
    /// </summary>
    public void MarkChangedUnits(AppDbContext db)
    {
        var changed = ChangedUnits().ToList();
        if (changed.Count == 0)
        {
            return;
        }

        var trackedById = db.ChangeTracker.TrackedEntriesWithoutDetection<BuildingUnit>().ToDictionary(entry => entry.Entity.Id, entry => entry);
        foreach (var unit in changed)
        {
            if (trackedById.TryGetValue(unit.Id, out var tracked))
            {
                if (!ReferenceEquals(tracked.Entity, unit))
                {
                    tracked.CurrentValues.SetValues(unit);
                }

                continue;
            }

            db.BuildingUnits.Attach(unit);
            db.Entry(unit).State = EntityState.Modified;
        }
    }
}

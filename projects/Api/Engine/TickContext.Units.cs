using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine;

public sealed partial class TickContext
{
    /// <summary>
    /// When the units were loaded without tracking, attaches the units of every building that has a pending configuration plan, so that
    /// a service which loads those buildings and their units from the database gets these instances back instead of second copies.
    /// Pending plans are few, so this touches few units. Does nothing when the context already tracks the units.
    /// </summary>
    public void AttachUnitsOfBuildingsWithPendingPlans()
    {
        if (UnitChanges is null)
        {
            return;
        }

        foreach (var building in BuildingsById.Values)
        {
            if (building.PendingConfiguration is null || !UnitsByBuilding.TryGetValue(building.Id, out var units))
            {
                continue;
            }

            foreach (var unit in units)
            {
                if (Db.Entry(unit).State == EntityState.Detached)
                {
                    Db.BuildingUnits.Attach(unit);
                }
            }
        }
    }
}

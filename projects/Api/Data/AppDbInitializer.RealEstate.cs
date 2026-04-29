using Api.Data.Entities;
using Api.Engine;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public sealed partial class AppDbInitializer
{
    /// <summary>
    /// Backfills missing apartment/commercial defaults on existing databases.
    /// Ensures occupancy is always numeric and total area is never empty.
    /// </summary>
    private async Task EnsurePropertyBuildingDefaultsAsync()
    {
        var properties = await dbContext.Buildings
            .Where(b => b.Type == BuildingType.Apartment || b.Type == BuildingType.Commercial)
            .ToListAsync();

        var changed = false;
        foreach (var building in properties)
        {
            if (!building.OccupancyPercent.HasValue)
            {
                building.OccupancyPercent = GameConstants.PropertyInitialOccupancyPercent;
                changed = true;
            }

            if (!building.TotalAreaSqm.HasValue || building.TotalAreaSqm.Value <= 0m)
            {
                var defaultArea = GameConstants.DefaultPropertyAreaSqm(building.Type);
                if (defaultArea.HasValue)
                {
                    building.TotalAreaSqm = defaultArea.Value;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync();
        }
    }
}

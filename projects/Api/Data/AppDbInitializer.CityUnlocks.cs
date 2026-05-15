using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public sealed partial class AppDbInitializer
{
    private async Task EnsureCityUnlockRequirementsAsync()
    {
        var cities = await dbContext.Cities
            .AsNoTracking()
            .Select(city => new { city.Id, city.Name })
            .ToListAsync();

        var existingCityIds = await dbContext.CityUnlockRequirements
            .AsNoTracking()
            .Select(requirement => requirement.CityId)
            .ToListAsync();
        var existingCityIdSet = existingCityIds.ToHashSet();

        foreach (var city in cities)
        {
            if (existingCityIdSet.Contains(city.Id))
            {
                continue;
            }

            dbContext.CityUnlockRequirements.Add(new CityUnlockRequirement
            {
                Id = CreateDeterministicGuid($"city-unlock:{city.Name}"),
                CityId = city.Id,
                RequiredNetWorthUsd = city.Name switch
                {
                    "Berlin" => 500_000m,
                    "Warsaw" => 300_000m,
                    _ => 0m,
                },
            });
        }
    }
}

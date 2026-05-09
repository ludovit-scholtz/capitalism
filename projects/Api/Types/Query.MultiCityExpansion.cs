using Api.Data;
using Api.Security;
using HotChocolate;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Multi-city overview for expansion UX with player unlock status.
    /// </summary>
    [GraphQLName("getCities")]
    public async Task<List<CityExpansionOverview>> GetCitiesForExpansion(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        HashSet<Guid> unlockedCityIds = [];

        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var playerId = user.GetRequiredUserId();
            unlockedCityIds = await db.Buildings
                .AsNoTracking()
                .Where(b => b.Company.PlayerId == playerId && b.DestroyedAtUtc == null)
                .Select(b => b.CityId)
                .Distinct()
                .ToHashSetAsync();
        }

        var cities = await db.Cities
            .AsNoTracking()
            .Include(c => c.Resources)
                .ThenInclude(r => r.ResourceType)
            .Include(c => c.Lots)
            .Include(c => c.Buildings)
            .AsSplitQuery()
            .OrderBy(c => c.Name)
            .ToListAsync();

        return cities.Select(city =>
        {
            var topResource = city.Resources
                .OrderByDescending(r => r.Abundance)
                .ThenBy(r => r.ResourceType.Name)
                .FirstOrDefault();

            return new CityExpansionOverview(
                Id: city.Id,
                Name: city.Name,
                CountryCode: city.CountryCode,
                CurrencyCode: city.CurrencyCode,
                Latitude: city.Latitude,
                Longitude: city.Longitude,
                Population: city.Population,
                IsUnlocked: unlockedCityIds.Contains(city.Id),
                AvailableLandPlots: city.Lots.Count(l => l.OwnerCompanyId == null && l.BuildingId == null),
                ActiveCompanyCount: city.Buildings
                    .Where(b => b.DestroyedAtUtc == null)
                    .Select(b => b.CompanyId)
                    .Distinct()
                    .Count(),
                TopResourceName: topResource?.ResourceType.Name,
                TopResourceSlug: topResource?.ResourceType.Slug);
        }).ToList();
    }
}

public record CityExpansionOverview(
    Guid Id,
    string Name,
    string CountryCode,
    string CurrencyCode,
    double Latitude,
    double Longitude,
    int Population,
    bool IsUnlocked,
    int AvailableLandPlots,
    int ActiveCompanyCount,
    string? TopResourceName,
    string? TopResourceSlug);

using Api.Data;
using Api.Security;
using Api.Utilities;
using HotChocolate;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    [GraphQLName("getCities")]
    public async Task<List<CityExpansionOverview>> GetCitiesForExpansion(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;
        Guid? activeCompanyId = null;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var playerId = user.GetRequiredUserId();
            activeCompanyId = await CityUnlockService.ResolvePlayerActiveCompanyIdAsync(db, playerId);
        }

        var unlockStatuses = await CityUnlockService.GetStatusesAsync(db, activeCompanyId);
        var unlockStatusByCityId = unlockStatuses.ToDictionary(status => status.CityId);

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
                IsUnlocked: unlockStatusByCityId.GetValueOrDefault(city.Id)?.IsUnlocked ?? true,
                AvailableLandPlots: city.Lots.Count(l => l.OwnerCompanyId == null && l.BuildingId == null),
                ActiveCompanyCount: city.Buildings
                    .Where(b => b.DestroyedAtUtc == null)
                    .Select(b => b.CompanyId)
                    .Distinct()
                    .Count(),
                TopResourceName: topResource?.ResourceType.Name,
                TopResourceSlug: topResource?.ResourceType.Slug,
                RequiredNetWorth: unlockStatusByCityId.GetValueOrDefault(city.Id)?.RequiredNetWorth ?? 0m,
                CurrentNetWorth: unlockStatusByCityId.GetValueOrDefault(city.Id)?.CurrentNetWorth ?? 0m,
                ProgressPercent: unlockStatusByCityId.GetValueOrDefault(city.Id)?.ProgressPercent ?? 100,
                EstimatedTicksToUnlock: unlockStatusByCityId.GetValueOrDefault(city.Id)?.EstimatedTicksToUnlock);
        }).ToList();
    }

    [Authorize]
    public async Task<CityUnlockStatusResult?> CityUnlockStatus(
        Guid cityId,
        Guid? companyId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ObjectAuthorizationService objectAuthorization)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var resolvedCompanyId = await ResolveOwnedCompanyIdAsync(db, playerId, companyId, objectAuthorization);
        var status = await CityUnlockService.GetStatusForCityAsync(db, cityId, resolvedCompanyId);
        return status is null ? null : CityUnlockStatusResult.FromStatus(status);
    }

    [Authorize]
    public async Task<List<CityUnlockStatusResult>> CityUnlockStatuses(
        Guid? companyId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ObjectAuthorizationService objectAuthorization)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var resolvedCompanyId = await ResolveOwnedCompanyIdAsync(db, playerId, companyId, objectAuthorization);
        var statuses = await CityUnlockService.GetStatusesAsync(db, resolvedCompanyId);
        return statuses.Select(CityUnlockStatusResult.FromStatus).ToList();
    }

    private static async Task<Guid?> ResolveOwnedCompanyIdAsync(
        AppDbContext db,
        Guid playerId,
        Guid? companyId,
        ObjectAuthorizationService objectAuthorization)
    {
        if (!companyId.HasValue)
        {
            return await CityUnlockService.ResolvePlayerActiveCompanyIdAsync(db, playerId);
        }

        var ownerPlayerId = await db.Companies
            .AsNoTracking()
            .Where(company => company.Id == companyId.Value)
            .Select(company => (Guid?)company.PlayerId)
            .FirstOrDefaultAsync();
        if (ownerPlayerId != playerId)
        {
            throw objectAuthorization.Deny(
                playerId,
                "company",
                companyId.Value,
                ownerPlayerId is null ? ObjectAuthorizationService.NotFoundReason : ObjectAuthorizationService.NotOwnedReason);
        }

        return companyId.Value;
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
    string? TopResourceSlug,
    decimal RequiredNetWorth,
    decimal CurrentNetWorth,
    int ProgressPercent,
    long? EstimatedTicksToUnlock);

public sealed record CityUnlockStatusResult(
    Guid CityId,
    string CityName,
    string CountryCode,
    bool IsUnlocked,
    decimal RequiredNetWorth,
    decimal CurrentNetWorth,
    string Currency,
    int ProgressPercent,
    long? EstimatedTicksToUnlock,
    Guid? CompanyId)
{
    public static CityUnlockStatusResult FromStatus(CityUnlockService.CompanyCityUnlockStatus status)
        => new(
            status.CityId,
            status.CityName,
            status.CountryCode,
            status.IsUnlocked,
            status.RequiredNetWorth,
            status.CurrentNetWorth,
            status.Currency,
            status.ProgressPercent,
            status.EstimatedTicksToUnlock,
            status.CompanyId);
}

using Api.Data;
using Api.Security;
using HotChocolate;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    /// <summary>
    /// Marks/returns whether the city is already unlocked by owning at least one building there.
    /// </summary>
    [Authorize]
    [GraphQLName("unlockCity")]
    public async Task<UnlockCityPayload> UnlockCity(
        Guid cityId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var city = await db.Cities
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cityId);

        if (city is null)
        {
            return new UnlockCityPayload(
                IsSuccess: false,
                ErrorCode: "CITY_NOT_FOUND",
                ErrorMessage: "City not found.",
                CityId: cityId,
                IsUnlocked: false,
                AvailableLandPlots: 0);
        }

        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var isUnlocked = await db.Buildings
            .AsNoTracking()
            .AnyAsync(b => b.Company.PlayerId == playerId && b.CityId == cityId && b.DestroyedAtUtc == null);

        var availableLandPlots = await db.BuildingLots
            .AsNoTracking()
            .CountAsync(l => l.CityId == cityId && l.OwnerCompanyId == null && l.BuildingId == null);

        return new UnlockCityPayload(
            IsSuccess: true,
            ErrorCode: null,
            ErrorMessage: null,
            CityId: cityId,
            IsUnlocked: isUnlocked,
            AvailableLandPlots: availableLandPlots);
    }
}

public record UnlockCityPayload(
    bool IsSuccess,
    string? ErrorCode,
    string? ErrorMessage,
    Guid CityId,
    bool IsUnlocked,
    int AvailableLandPlots);

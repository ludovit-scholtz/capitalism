using Api.Data;
using Api.Security;
using HotChocolate;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    [Authorize]
    [GraphQLName("unlockCity")]
    public async Task<UnlockCityPayload> UnlockCity(
        Guid cityId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var activeCompanyId = await CityUnlockService.ResolvePlayerActiveCompanyIdAsync(db, playerId);
        var status = await CityUnlockService.GetStatusForCityAsync(db, cityId, activeCompanyId);
        if (status is null)
        {
            return new UnlockCityPayload(
                IsSuccess: false,
                ErrorCode: "CITY_NOT_FOUND",
                ErrorMessage: "City not found.",
                CityId: cityId,
                IsUnlocked: false,
                AvailableLandPlots: 0,
                RequiredNetWorth: 0m,
                CurrentNetWorth: 0m,
                Currency: "EUR",
                ProgressPercent: 0,
                EstimatedTicksToUnlock: null);
        }

        var availableLandPlots = await db.BuildingLots
            .AsNoTracking()
            .CountAsync(l => l.CityId == cityId && l.OwnerCompanyId == null && l.BuildingId == null);

        return new UnlockCityPayload(
            IsSuccess: true,
            ErrorCode: null,
            ErrorMessage: null,
            CityId: cityId,
            IsUnlocked: status.IsUnlocked,
            AvailableLandPlots: availableLandPlots,
            RequiredNetWorth: status.RequiredNetWorth,
            CurrentNetWorth: status.CurrentNetWorth,
            Currency: status.Currency,
            ProgressPercent: status.ProgressPercent,
            EstimatedTicksToUnlock: status.EstimatedTicksToUnlock);
    }
}

public record UnlockCityPayload(
    bool IsSuccess,
    string? ErrorCode,
    string? ErrorMessage,
    Guid CityId,
    bool IsUnlocked,
    int AvailableLandPlots,
    decimal RequiredNetWorth,
    decimal CurrentNetWorth,
    string Currency,
    int ProgressPercent,
    long? EstimatedTicksToUnlock);

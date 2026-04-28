using Api.Data;
using Api.Data.Entities;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    /// <summary>
    /// Sets the dispatch target for a player-owned power plant, controlling how much of
    /// the plant's rated capacity it actually generates each tick.
    ///
    /// - 100 = full output (default): maximises generation, income, and fuel consumption.
    /// - 50  = half output: reduces fuel cost by 50% but also halves surplus income and
    ///         increases shortage risk if the city demand is not met.
    /// - 0   = offline: no generation, no fuel cost (useful for maintenance or when fuel
    ///         is expensive and the city has ample alternative supply).
    ///
    /// The dispatch target is applied in <c>PowerDistributionPhase</c> before any economics
    /// or production phases run. Changing it takes effect on the next tick.
    /// </summary>
    [Authorize]
    public async Task<Building> SetPlantDispatch(
        SetPlantDispatchInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .Include(b => b.Company)
            .FirstOrDefaultAsync(b => b.Id == input.BuildingId);

        if (building is null || building.Company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Building not found or you do not own it.")
                    .SetCode("BUILDING_NOT_FOUND")
                    .Build());
        }

        if (building.Type != BuildingType.PowerPlant)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Dispatch targets can only be set on power plant buildings.")
                    .SetCode("INVALID_BUILDING_TYPE")
                    .Build());
        }

        if (input.DispatchTargetPercent < 0 || input.DispatchTargetPercent > 100)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Dispatch target must be between 0 and 100 (percent).")
                    .SetCode("INVALID_DISPATCH_TARGET")
                    .Build());
        }

        building.DispatchTargetPercent = input.DispatchTargetPercent;
        await db.SaveChangesAsync();
        return building;
    }
}

/// <summary>Input for <see cref="Mutation.SetPlantDispatch"/>.</summary>
public sealed class SetPlantDispatchInput
{
    /// <summary>ID of the power-plant building to configure.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>
    /// Target dispatch percentage (0–100).
    /// 100 = full output (default), 0 = effectively offline.
    /// </summary>
    public int DispatchTargetPercent { get; set; }
}

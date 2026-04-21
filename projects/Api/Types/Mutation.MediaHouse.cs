using Api.Data;
using Api.Data.Entities;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    /// <summary>
    /// Sets the per-tick content spending budget for a player-owned MEDIA_HOUSE building.
    /// Each tick the specified amount is deducted from the company cash and converted to
    /// accumulated content using the level-based efficiency formula
    /// (efficiency = 1 – 1/(level+1); 50% at level 1, 66% at level 2, …).
    /// Pass null or 0 to stop content investment.
    /// </summary>
    [Authorize]
    public async Task<Building> SetMediaHouseContentBudget(
        SetMediaHouseContentBudgetInput input,
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
                    .SetMessage("Building not found or you don't own it.")
                    .SetCode("BUILDING_NOT_FOUND")
                    .Build());
        }

        if (building.Type != BuildingType.MediaHouse)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Only media house buildings support content budget configuration.")
                    .SetCode("INVALID_BUILDING_TYPE")
                    .Build());
        }

        if (building.IsGovernmentOwned)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Government-owned media houses cannot be configured by players.")
                    .SetCode("GOVERNMENT_OWNED")
                    .Build());
        }

        if (input.ContentBudgetPerTick.HasValue && input.ContentBudgetPerTick.Value < 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Content budget per tick must be non-negative.")
                    .SetCode("INVALID_BUDGET")
                    .Build());
        }

        building.ContentBudgetPerTick = (input.ContentBudgetPerTick is null or <= 0m)
            ? null
            : input.ContentBudgetPerTick;

        await db.SaveChangesAsync();
        return building;
    }
}

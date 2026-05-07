using Api.Data;
using Api.Data.Entities;
using Api.Utilities;
using HotChocolate;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

[ExtendObjectType<BuildingConfigurationPlan>]
public sealed class BuildingConfigurationPlanTypeExtensions
{
    public async Task<string?> GetBlockReason(
        [Parent] BuildingConfigurationPlan plan,
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(gameState => (long?)gameState.CurrentTick)
            .FirstOrDefaultAsync(cancellationToken)
            ?? 0L;

        return await BuildingConfigurationService.GetUpgradeBlockReasonAsync(db, plan, currentTick, cancellationToken);
    }
}
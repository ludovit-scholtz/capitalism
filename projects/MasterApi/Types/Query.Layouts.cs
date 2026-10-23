using System.Security.Claims;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using MasterApi.Utilities;
using Capitalism.Shared.Security;
using HotChocolate.CostAnalysis.Types;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MasterApi.Types;

public sealed partial class Query
{

    [HotChocolate.Authorization.Authorize]
    public async Task<List<BuildingLayoutTemplateInfo>> GetMyBuildingLayouts(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db)
    {
        var player = await GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());
        var userId = player.Id;

        return await db.BuildingLayoutTemplates
            .AsNoTracking()
            .Where(l => l.PlayerAccountId == userId)
            .OrderByDescending(l => l.UpdatedAtUtc)
            .Select(l => new BuildingLayoutTemplateInfo
            {
                Id = l.Id,
                Name = l.Name,
                Description = l.Description,
                BuildingType = l.BuildingType,
                UnitsJson = l.UnitsJson,
                CreatedAtUtc = l.CreatedAtUtc,
                UpdatedAtUtc = l.UpdatedAtUtc,
            })
            .ToListAsync();
    }
}

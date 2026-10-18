using Api.Data;
using Api.Engine.Campaign;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// The signed-in player's guided first-week campaign: day number and the seven goals with progress computed from existing data
    /// (tutorial milestones, owned buildings). Auth decision: [Authorize], always the caller's own campaign (no id argument, so there is
    /// no foreign-owner case). Read-only.
    /// </summary>
    [Authorize]
    public async Task<FirstWeekCampaignResult> GetFirstWeekCampaign(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var ct = httpContextAccessor.HttpContext.RequestAborted;
        var player = await db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id == userId, ct)
            ?? throw new GraphQLException(
                ErrorBuilder.New().SetMessage("Player not found.").SetCode("PLAYER_NOT_FOUND").Build());

        return FirstWeekCampaignResult.From(await FirstWeekCampaignLoader.LoadAsync(db, player, DateTime.UtcNow, ct));
    }
}

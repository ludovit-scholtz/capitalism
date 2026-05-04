using Api.Data;
using Api.Data.Entities;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Returns the tutorial progress for the authenticated player,
    /// listing all 5 milestones with their completion status.
    /// </summary>
    [Authorize]
    public async Task<IReadOnlyList<TutorialMilestoneStatus>> GetTutorialProgress(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var rows = await db.TutorialProgresses
            .AsNoTracking()
            .Where(tp => tp.PlayerId == userId)
            .ToListAsync(httpContextAccessor.HttpContext.RequestAborted);

        return TutorialMilestone.All
            .Select(milestone =>
            {
                var row = rows.FirstOrDefault(r => r.Milestone == milestone);
                return new TutorialMilestoneStatus
                {
                    Milestone = milestone,
                    IsCompleted = row?.IsCompleted ?? false,
                    CompletedAtUtc = row?.CompletedAtUtc,
                };
            })
            .ToList();
    }
}

/// <summary>Tutorial milestone status returned by the getTutorialProgress query.</summary>
public sealed class TutorialMilestoneStatus
{
    /// <summary>Milestone identifier constant from <see cref="TutorialMilestone"/>.</summary>
    public string Milestone { get; set; } = string.Empty;

    /// <summary>Whether the player has completed this milestone.</summary>
    public bool IsCompleted { get; set; }

    /// <summary>UTC timestamp when this milestone was completed. Null if not yet achieved.</summary>
    public DateTime? CompletedAtUtc { get; set; }
}

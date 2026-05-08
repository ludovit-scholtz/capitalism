using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Returns the tutorial progress for the authenticated player,
    /// listing all tracked milestones with their completion and bounty-award status.
    /// </summary>
    [Authorize]
    public async Task<IReadOnlyList<TutorialMilestoneStatus>> GetTutorialProgress(
        [Service] AppDbContext db,
        [Service] IMasterGameAdministrationService masterGameAdministrationService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var cancellationToken = httpContextAccessor.HttpContext.RequestAborted;
        var playerEmail = await db.Players
            .AsNoTracking()
            .Where(player => player.Id == userId)
            .Select(player => player.Email)
            .FirstOrDefaultAsync(cancellationToken);

        var rows = await db.TutorialProgresses
            .AsNoTracking()
            .Where(tp => tp.PlayerId == userId)
            .ToListAsync(cancellationToken);
        var normalizedRows = rows
            .GroupBy(row => TutorialMilestone.Normalize(row.Milestone), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.CompletedAtUtc ?? DateTime.MinValue).First(),
                StringComparer.Ordinal);

        var bountyStatuses = string.IsNullOrWhiteSpace(playerEmail)
            ? []
            : await masterGameAdministrationService.GetTutorialBountyStatusesAsync(playerEmail, cancellationToken);
        var bountyByMilestone = bountyStatuses
            .GroupBy(status => status.Milestone, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.AwardedAtUtc ?? DateTime.MinValue).First(),
                StringComparer.Ordinal);

        return TutorialMilestone.All
            .Select(milestone =>
            {
                normalizedRows.TryGetValue(milestone, out var row);
                bountyByMilestone.TryGetValue(milestone, out var bounty);
                var bountyAwarded = bounty?.IsAwarded ?? false;
                return new TutorialMilestoneStatus
                {
                    Milestone = milestone,
                    IsCompleted = bountyAwarded || (row?.IsCompleted ?? false),
                    CompletedAtUtc = bounty?.AwardedAtUtc ?? row?.CompletedAtUtc,
                    BountyAwarded = bountyAwarded,
                    BountyAwardedAtUtc = bounty?.AwardedAtUtc,
                    BountyPoints = bounty?.RewardPoints ?? TutorialMilestone.GetTutorialBountyPoints(milestone),
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

    /// <summary>Whether the associated tutorial bounty has already been awarded.</summary>
    public bool BountyAwarded { get; set; }

    /// <summary>UTC timestamp when the tutorial bounty was awarded. Null if never awarded.</summary>
    public DateTime? BountyAwardedAtUtc { get; set; }

    /// <summary>Configured tutorial bounty points for this milestone (if any).</summary>
    public decimal? BountyPoints { get; set; }
}

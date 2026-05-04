using Api.Data;
using Api.Data.Entities;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    /// <summary>
    /// Marks a specific tutorial milestone as completed for the authenticated player.
    /// Idempotent: calling this multiple times for the same milestone is safe and
    /// returns the existing record if already completed.
    /// </summary>
    [Authorize]
    public async Task<TutorialMilestoneStatus> MarkTutorialMilestoneComplete(
        MarkTutorialMilestoneCompleteInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        if (!TutorialMilestone.All.Contains(input.Milestone))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Unknown tutorial milestone: {input.Milestone}")
                    .SetCode("UNKNOWN_MILESTONE")
                    .Build());
        }

        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var existing = await db.TutorialProgresses
            .FirstOrDefaultAsync(tp => tp.PlayerId == userId && tp.Milestone == input.Milestone,
                httpContextAccessor.HttpContext.RequestAborted);

        if (existing is not null)
        {
            // Idempotent: always ensure IsCompleted=true regardless of current state.
            if (!existing.IsCompleted)
            {
                existing.IsCompleted = true;
                existing.CompletedAtUtc ??= DateTime.UtcNow;
                await db.SaveChangesAsync(httpContextAccessor.HttpContext.RequestAborted);
            }
            return new TutorialMilestoneStatus
            {
                Milestone = existing.Milestone,
                IsCompleted = existing.IsCompleted,
                CompletedAtUtc = existing.CompletedAtUtc,
            };
        }

        var now = DateTime.UtcNow;
        var progress = new TutorialProgress
        {
            Id = Guid.NewGuid(),
            PlayerId = userId,
            Milestone = input.Milestone,
            IsCompleted = true,
            CompletedAtUtc = now,
            CreatedAtUtc = now,
        };
        db.TutorialProgresses.Add(progress);
        await db.SaveChangesAsync(httpContextAccessor.HttpContext.RequestAborted);

        return new TutorialMilestoneStatus
        {
            Milestone = progress.Milestone,
            IsCompleted = true,
            CompletedAtUtc = now,
        };
    }
}

/// <summary>Input for the markTutorialMilestoneComplete mutation.</summary>
public sealed class MarkTutorialMilestoneCompleteInput
{
    /// <summary>
    /// The milestone identifier to mark as completed.
    /// Must be one of the values defined in <see cref="TutorialMilestone"/>.
    /// </summary>
    public string Milestone { get; set; } = string.Empty;
}

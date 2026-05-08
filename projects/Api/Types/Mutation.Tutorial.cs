using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using Capitalism.Shared.Ranking;
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
        [Service] IMasterRankingTelemetryService rankingTelemetry,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var normalizedMilestone = TutorialMilestone.Normalize(input.Milestone?.Trim() ?? string.Empty);
        if (!TutorialMilestone.IsKnown(normalizedMilestone))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Unknown tutorial milestone: {input.Milestone}")
                    .SetCode("UNKNOWN_MILESTONE")
                    .Build());
        }

        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var cancellationToken = httpContextAccessor.HttpContext.RequestAborted;
        var playerEmail = await db.Players
            .AsNoTracking()
            .Where(player => player.Id == userId)
            .Select(player => player.Email)
            .FirstOrDefaultAsync(cancellationToken);
        var acceptedKeys = TutorialMilestone.GetAcceptedKeys(normalizedMilestone);

        var existing = await db.TutorialProgresses
            .FirstOrDefaultAsync(tp => tp.PlayerId == userId && acceptedKeys.Contains(tp.Milestone),
                cancellationToken);

        if (existing is not null)
        {
            var requiresSave = !string.Equals(existing.Milestone, normalizedMilestone, StringComparison.Ordinal);
            existing.Milestone = normalizedMilestone;
            // Idempotent: always ensure IsCompleted=true regardless of current state.
            if (!existing.IsCompleted)
            {
                existing.IsCompleted = true;
                existing.CompletedAtUtc ??= DateTime.UtcNow;
                requiresSave = true;
            }

            if (requiresSave)
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            TryAwardTutorialBounty(rankingTelemetry, normalizedMilestone, playerEmail);
            return new TutorialMilestoneStatus
            {
                Milestone = existing.Milestone,
                IsCompleted = existing.IsCompleted,
                CompletedAtUtc = existing.CompletedAtUtc,
                BountyAwarded = false,
                BountyAwardedAtUtc = null,
                BountyPoints = TutorialMilestone.GetTutorialBountyPoints(existing.Milestone),
            };
        }

        var now = DateTime.UtcNow;
        var progress = new TutorialProgress
        {
            Id = Guid.NewGuid(),
            PlayerId = userId,
            Milestone = normalizedMilestone,
            IsCompleted = true,
            CompletedAtUtc = now,
            CreatedAtUtc = now,
        };
        db.TutorialProgresses.Add(progress);
        await db.SaveChangesAsync(cancellationToken);

        TryAwardTutorialBounty(rankingTelemetry, normalizedMilestone, playerEmail);

        return new TutorialMilestoneStatus
        {
            Milestone = progress.Milestone,
            IsCompleted = true,
            CompletedAtUtc = now,
            BountyAwarded = false,
            BountyAwardedAtUtc = null,
            BountyPoints = TutorialMilestone.GetTutorialBountyPoints(progress.Milestone),
        };
    }

    private static void TryAwardTutorialBounty(
        IMasterRankingTelemetryService rankingTelemetry,
        string milestone,
        string? playerEmail)
    {
        if (string.IsNullOrWhiteSpace(playerEmail))
        {
            return;
        }

        if (!TutorialRankingBountyCatalog.ByMilestone.TryGetValue(milestone, out var bounty))
        {
            return;
        }

        _ = rankingTelemetry.ReportEventAsync(
            bounty.BountyCode,
            playerEmail.Trim().ToLowerInvariant(),
            uniqueScopeKey: $"{bounty.BountyCode}:{playerEmail.Trim().ToLowerInvariant()}:tutorial");
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

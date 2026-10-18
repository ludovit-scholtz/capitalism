using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine.Campaign;

/// <summary>Reads the facts for one player and evaluates the first-week campaign with <see cref="FirstWeekCampaignCalculator"/>. Read-only.</summary>
public static class FirstWeekCampaignLoader
{
    public static async Task<FirstWeekCampaignState> LoadAsync(AppDbContext db, Player player, DateTime nowUtc, CancellationToken ct)
    {
        var buildingCount = await db.Buildings.AsNoTracking()
            .CountAsync(b => b.Company.PlayerId == player.Id && b.DestroyedAtUtc == null, ct);
        var milestones = await db.TutorialProgresses.AsNoTracking()
            .Where(t => t.PlayerId == player.Id && t.IsCompleted)
            .Select(t => t.Milestone)
            .ToListAsync(ct);
        var claimed = await db.PlayerCampaignClaims.AsNoTracking()
            .Where(c => c.PlayerId == player.Id)
            .Select(c => c.GoalCode)
            .ToListAsync(ct);

        return FirstWeekCampaignCalculator.Evaluate(
            player.OnboardingCompletedAtUtc,
            nowUtc,
            new FirstWeekFacts(buildingCount, milestones.ToHashSet()),
            claimed.ToHashSet());
    }
}

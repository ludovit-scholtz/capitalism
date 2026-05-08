using System.ComponentModel.DataAnnotations;
using Capitalism.Shared.Ranking;
using Capitalism.Shared.Tutorial;

namespace Api.Data.Entities;

/// <summary>
/// Tracks per-player completion of guided tutorial milestones.
/// Each row records whether a specific milestone has been achieved and when.
/// </summary>
public sealed class TutorialProgress
{
    public Guid Id { get; set; }

    /// <summary>The player this progress record belongs to.</summary>
    public Guid PlayerId { get; set; }

    /// <summary>Navigation property to the owning player.</summary>
    public Player Player { get; set; } = null!;

    /// <summary>Identifies which tutorial milestone this row tracks. See <see cref="TutorialMilestone"/>.</summary>
    [Required, MaxLength(60)]
    public string Milestone { get; set; } = string.Empty;

    /// <summary>Whether the player has completed this milestone.</summary>
    public bool IsCompleted { get; set; }

    /// <summary>UTC timestamp when the player completed this milestone. Null when not yet achieved.</summary>
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>UTC timestamp when this progress row was first created.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Defines the available tutorial milestone identifiers.</summary>
public static class TutorialMilestone
{
    /// <summary>Player completes their first public sale (product sold to consumers).</summary>
    public const string FirstResourceSold = TutorialMilestoneCodes.FirstResourceSold;

    /// <summary>Player sets up and completes their first B2B trade route.</summary>
    public const string FirstB2BTrade = TutorialMilestoneCodes.FirstB2BTrade;

    /// <summary>Player takes out their first loan from a bank building.</summary>
    public const string FirstLoanTaken = TutorialMilestoneCodes.FirstLoanTaken;

    /// <summary>Player observes a competitor in market intelligence for the first time.</summary>
    public const string FirstCompetitorObserved = TutorialMilestoneCodes.FirstCompetitorObserved;

    /// <summary>Player establishes their first brand.</summary>
    public const string FirstBrandEstablished = TutorialMilestoneCodes.FirstBrandEstablished;

    /// <summary>Player visits the building detail view for the first time.</summary>
    public const string FirstBuildingDetailVisit = TutorialMilestoneCodes.FirstBuildingDetailVisit;

    /// <summary>Player opens the building grid editor for the first time.</summary>
    public const string FirstGridEditorOpen = TutorialMilestoneCodes.FirstGridEditorOpen;

    /// <summary>Player dismissed the dashboard contextual tooltip overlay on first visit.</summary>
    public const string TooltipDashboardShown = TutorialMilestoneCodes.TooltipDashboardShown;

    /// <summary>Legacy milestone key used before FIRST_BUILDING_DETAIL_VISIT was introduced.</summary>
    public const string LegacyTooltipBuildingDetailShown = "TOOLTIP_BUILDING_DETAIL_SHOWN";

    /// <summary>Legacy milestone key used before FIRST_GRID_EDITOR_OPEN was introduced.</summary>
    public const string LegacyTooltipGridEditorShown = "TOOLTIP_GRID_EDITOR_SHOWN";

    /// <summary>All milestone identifiers in display order.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        FirstResourceSold,
        FirstB2BTrade,
        FirstLoanTaken,
        FirstCompetitorObserved,
        FirstBrandEstablished,
        FirstBuildingDetailVisit,
        FirstGridEditorOpen,
        TooltipDashboardShown,
    ];

    public static bool IsKnown(string milestone)
    {
        var normalized = Normalize(milestone);
        return All.Contains(normalized);
    }

    public static IReadOnlyList<string> GetAcceptedKeys(string milestone)
    {
        var normalized = Normalize(milestone);
        return normalized switch
        {
            FirstBuildingDetailVisit => [FirstBuildingDetailVisit, LegacyTooltipBuildingDetailShown],
            FirstGridEditorOpen => [FirstGridEditorOpen, LegacyTooltipGridEditorShown],
            _ => [normalized],
        };
    }

    public static string Normalize(string milestone)
    {
        return milestone switch
        {
            LegacyTooltipBuildingDetailShown => FirstBuildingDetailVisit,
            LegacyTooltipGridEditorShown => FirstGridEditorOpen,
            _ => milestone,
        };
    }

    public static bool HasTutorialBounty(string milestone)
    {
        return TutorialRankingBountyCatalog.ByMilestone.ContainsKey(Normalize(milestone));
    }

    public static decimal? GetTutorialBountyPoints(string milestone)
    {
        return TutorialRankingBountyCatalog.ByMilestone.TryGetValue(Normalize(milestone), out var entry)
            ? entry.RewardPoints
            : null;
    }
}

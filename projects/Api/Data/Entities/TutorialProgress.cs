using System.ComponentModel.DataAnnotations;

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
    public const string FirstResourceSold = "FIRST_RESOURCE_SOLD";

    /// <summary>Player sets up and completes their first B2B trade route.</summary>
    public const string FirstB2BTrade = "FIRST_B2B_TRADE";

    /// <summary>Player takes out their first loan from a bank building.</summary>
    public const string FirstLoanTaken = "FIRST_LOAN_TAKEN";

    /// <summary>Player observes a competitor in market intelligence for the first time.</summary>
    public const string FirstCompetitorObserved = "FIRST_COMPETITOR_OBSERVED";

    /// <summary>Player establishes their first brand.</summary>
    public const string FirstBrandEstablished = "FIRST_BRAND_ESTABLISHED";

    /// <summary>Player dismissed the dashboard contextual tooltip overlay on first visit.</summary>
    public const string TooltipDashboardShown = "TOOLTIP_DASHBOARD_SHOWN";

    /// <summary>Player dismissed the building detail contextual tooltip overlay on first visit.</summary>
    public const string TooltipBuildingDetailShown = "TOOLTIP_BUILDING_DETAIL_SHOWN";

    /// <summary>Player dismissed the grid editor contextual tooltip overlay on first use.</summary>
    public const string TooltipGridEditorShown = "TOOLTIP_GRID_EDITOR_SHOWN";

    /// <summary>All milestone identifiers in display order.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        FirstResourceSold,
        FirstB2BTrade,
        FirstLoanTaken,
        FirstCompetitorObserved,
        FirstBrandEstablished,
        TooltipDashboardShown,
        TooltipBuildingDetailShown,
        TooltipGridEditorShown,
    ];
}

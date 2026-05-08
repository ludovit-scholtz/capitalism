using Capitalism.Shared.Tutorial;

namespace Capitalism.Shared.Ranking;

public static class TutorialRankingBountyCatalog
{
    public sealed record Entry(
        string Milestone,
        string BountyCode,
        string DisplayName,
        string Description,
        decimal RewardPoints);

    public static readonly IReadOnlyList<Entry> All =
    [
        new(
            TutorialMilestoneCodes.FirstResourceSold,
            MasterRankingBountyCodes.TutorialFirstResourceSold,
            "Tutorial: First resource sold",
            "Complete the tutorial milestone for first public sale.",
            50m),
        new(
            TutorialMilestoneCodes.FirstB2BTrade,
            MasterRankingBountyCodes.TutorialFirstB2BTrade,
            "Tutorial: First B2B trade",
            "Complete the tutorial milestone for first B2B trade.",
            75m),
        new(
            TutorialMilestoneCodes.FirstLoanTaken,
            MasterRankingBountyCodes.TutorialFirstLoanTaken,
            "Tutorial: First loan taken",
            "Complete the tutorial milestone for first loan.",
            60m),
        new(
            TutorialMilestoneCodes.FirstCompetitorObserved,
            MasterRankingBountyCodes.TutorialFirstCompetitorObserved,
            "Tutorial: First competitor observed",
            "Complete the tutorial milestone for first competitor observation.",
            40m),
        new(
            TutorialMilestoneCodes.FirstBrandEstablished,
            MasterRankingBountyCodes.TutorialFirstBrandEstablished,
            "Tutorial: First brand established",
            "Complete the tutorial milestone for first brand establishment.",
            80m),
        new(
            TutorialMilestoneCodes.FirstBuildingDetailVisit,
            MasterRankingBountyCodes.TutorialFirstBuildingDetailVisit,
            "Tutorial: First building detail visit",
            "Complete the tutorial milestone for first building detail visit.",
            30m),
        new(
            TutorialMilestoneCodes.FirstGridEditorOpen,
            MasterRankingBountyCodes.TutorialFirstGridEditorOpen,
            "Tutorial: First grid editor open",
            "Complete the tutorial milestone for first grid editor open.",
            30m),
    ];

    public static readonly IReadOnlyDictionary<string, Entry> ByMilestone =
        All.ToDictionary(item => item.Milestone, StringComparer.Ordinal);

    public static readonly IReadOnlyDictionary<string, Entry> ByBountyCode =
        All.ToDictionary(item => item.BountyCode, StringComparer.Ordinal);
}

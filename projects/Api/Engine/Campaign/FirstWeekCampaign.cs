using Capitalism.Shared.Tutorial;

namespace Api.Engine.Campaign;

/// <summary>One goal of the guided first-week campaign: unlocks on <see cref="Day"/>, pays <see cref="RewardAmount"/> once.</summary>
public sealed record FirstWeekGoal(string Code, int Day, string? Milestone, int BuildingTarget, decimal RewardAmount)
{
    /// <summary>How much progress completes the goal (1 for milestone goals, the building count for building goals).</summary>
    public int Target => Milestone is not null ? 1 : BuildingTarget;
}

/// <summary>The facts about a player the goals are measured against; everything comes from existing server state.</summary>
public sealed record FirstWeekFacts(int BuildingCount, IReadOnlySet<string> CompletedMilestones);

public sealed record FirstWeekGoalState(
    FirstWeekGoal Goal,
    int Progress,
    bool IsUnlocked,
    bool IsComplete,
    bool IsClaimed)
{
    public string Code => Goal.Code;
    public int Target => Goal.Target;

    /// <summary>Done, its day has come and the reward was not paid yet.</summary>
    public bool CanClaim => IsUnlocked && IsComplete && !IsClaimed;
}

public sealed record FirstWeekCampaignState(
    bool Started,
    int DayNumber,
    DateTime? StartedAtUtc,
    IReadOnlyList<FirstWeekGoalState> Goals,
    int ClaimedCount,
    bool AllClaimed);

/// <summary>The seven goals, one per day (#602). Rewards are paid in <see cref="RewardCurrency"/> to the claiming company's account.</summary>
public static class FirstWeekCampaignCatalog
{
    public const int Days = 7;
    public const string RewardCurrency = "USD";

    public const string FirstSale = "FIRST_SALE";
    public const string SecondBuilding = "SECOND_BUILDING";
    public const string TakeALoan = "TAKE_A_LOAN";
    public const string FirstB2BTrade = "FIRST_B2B_TRADE";
    public const string BuildABrand = "BUILD_A_BRAND";
    public const string ScoutCompetitors = "SCOUT_COMPETITORS";
    public const string GrowToThreeBuildings = "GROW_TO_THREE_BUILDINGS";

    /// <summary>Day number times $2,000: small enough to teach rather than fund the game (the whole week pays $56,000 against $200,000 of starting capital).</summary>
    private static decimal Reward(int day) => day * 2_000m;

    public static IReadOnlyList<FirstWeekGoal> Goals { get; } =
    [
        new(FirstSale, 1, TutorialMilestoneCodes.FirstResourceSold, 0, Reward(1)),
        new(SecondBuilding, 2, null, 2, Reward(2)),
        new(TakeALoan, 3, TutorialMilestoneCodes.FirstLoanTaken, 0, Reward(3)),
        new(FirstB2BTrade, 4, TutorialMilestoneCodes.FirstB2BTrade, 0, Reward(4)),
        new(BuildABrand, 5, TutorialMilestoneCodes.FirstBrandEstablished, 0, Reward(5)),
        new(ScoutCompetitors, 6, TutorialMilestoneCodes.FirstCompetitorObserved, 0, Reward(6)),
        new(GrowToThreeBuildings, 7, null, 3, Reward(7)),
    ];

    public static FirstWeekGoal? Find(string code) => Goals.FirstOrDefault(g => string.Equals(g.Code, code, StringComparison.Ordinal));
}

/// <summary>Pure goal/day evaluation for the first-week campaign: no database, no clock, so every rule is unit-testable.</summary>
public static class FirstWeekCampaignCalculator
{
    public static FirstWeekCampaignState Evaluate(
        DateTime? onboardingCompletedAtUtc,
        DateTime nowUtc,
        FirstWeekFacts facts,
        IReadOnlySet<string> claimedGoalCodes)
    {
        var started = onboardingCompletedAtUtc.HasValue;
        var dayNumber = 0;
        if (started)
        {
            var elapsedDays = (int)Math.Floor(Math.Max(0d, (nowUtc - onboardingCompletedAtUtc!.Value).TotalDays));
            dayNumber = Math.Min(FirstWeekCampaignCatalog.Days, elapsedDays + 1);
        }

        var goals = FirstWeekCampaignCatalog.Goals
            .Select(goal =>
            {
                var progress = goal.Milestone is not null
                    ? (facts.CompletedMilestones.Contains(goal.Milestone) ? 1 : 0)
                    : Math.Min(facts.BuildingCount, goal.BuildingTarget);
                return new FirstWeekGoalState(
                    goal,
                    progress,
                    IsUnlocked: started && goal.Day <= dayNumber,
                    IsComplete: progress >= goal.Target,
                    IsClaimed: claimedGoalCodes.Contains(goal.Code));
            })
            .ToList();

        var claimedCount = goals.Count(g => g.IsClaimed);
        return new FirstWeekCampaignState(
            started,
            dayNumber,
            onboardingCompletedAtUtc,
            goals,
            claimedCount,
            AllClaimed: claimedCount == goals.Count);
    }
}

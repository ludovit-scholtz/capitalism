using Api.Data.Entities;
using Api.Engine.Campaign;
using Capitalism.Shared.Tutorial;

namespace Api.Tests;

public sealed class FirstWeekCampaignCalculatorTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly IReadOnlySet<string> None = new HashSet<string>();

    private static FirstWeekFacts Facts(int buildings = 1, params string[] milestones)
        => new(buildings, milestones.ToHashSet());

    [Fact]
    public void BeforeOnboardingIsCompleted_TheCampaignHasNotStarted()
    {
        var state = FirstWeekCampaignCalculator.Evaluate(null, Start, Facts(), None);

        Assert.False(state.Started);
        Assert.Equal(0, state.DayNumber);
        Assert.All(state.Goals, g => Assert.False(g.IsUnlocked));
        Assert.All(state.Goals, g => Assert.False(g.CanClaim));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(23, 1)]   // still the first 24 hours
    [InlineData(24, 2)]
    [InlineData(24 * 3 + 5, 4)]
    [InlineData(24 * 6, 7)]
    [InlineData(24 * 30, 7)] // the strip stops at day 7
    public void DayNumberCountsWholeDaysSinceOnboarding_CappedAtSeven(int hoursAfterStart, int expectedDay)
    {
        var state = FirstWeekCampaignCalculator.Evaluate(Start, Start.AddHours(hoursAfterStart), Facts(), None);

        Assert.True(state.Started);
        Assert.Equal(expectedDay, state.DayNumber);
    }

    [Fact]
    public void TheCatalogHasOneGoalPerDay_WithGrowingRewardsAndUniqueCodes()
    {
        var goals = FirstWeekCampaignCatalog.Goals;

        Assert.Equal(FirstWeekCampaignCatalog.Days, goals.Count);
        Assert.Equal(Enumerable.Range(1, FirstWeekCampaignCatalog.Days), goals.Select(g => g.Day));
        Assert.Equal(goals.Count, goals.Select(g => g.Code).Distinct().Count());
        Assert.True(goals.Zip(goals.Skip(1), (a, b) => b.RewardAmount > a.RewardAmount).All(x => x));
    }

    [Fact]
    public void GoalsUnlockDayByDay_AndFutureGoalsCannotBeClaimedEvenIfAlreadyDone()
    {
        var done = Facts(5, TutorialMilestoneCodes.FirstResourceSold, TutorialMilestoneCodes.FirstLoanTaken, TutorialMilestoneCodes.FirstB2BTrade);

        var dayTwo = FirstWeekCampaignCalculator.Evaluate(Start, Start.AddHours(30), done, None);

        Assert.Equal(2, dayTwo.DayNumber);
        Assert.True(dayTwo.Goals[0].CanClaim);                  // day 1 goal: done and unlocked
        Assert.True(dayTwo.Goals[1].CanClaim);                  // day 2 goal: 2 buildings, unlocked
        Assert.True(dayTwo.Goals[2].IsComplete);                // day 3 goal is already done...
        Assert.False(dayTwo.Goals[2].IsUnlocked);               // ...but its day has not come
        Assert.False(dayTwo.Goals[2].CanClaim);
    }

    [Fact]
    public void ProgressIsCappedAtTheTarget_AndAClaimedGoalCannotBeClaimedAgain()
    {
        var facts = Facts(10, TutorialMilestoneCodes.FirstResourceSold);
        var claimed = new HashSet<string> { FirstWeekCampaignCatalog.Goals[0].Code };

        var state = FirstWeekCampaignCalculator.Evaluate(Start, Start.AddHours(1), facts, claimed);

        Assert.True(state.Goals[0].IsClaimed);
        Assert.False(state.Goals[0].CanClaim);
        Assert.Equal(1, state.ClaimedCount);
        Assert.All(state.Goals, g => Assert.True(g.Progress <= g.Target));
    }

    [Fact]
    public void BuildingGoalsReportPartialProgress()
    {
        var state = FirstWeekCampaignCalculator.Evaluate(Start, Start.AddDays(10), Facts(2), None);

        var growGoal = state.Goals.Single(g => g.Code == FirstWeekCampaignCatalog.GrowToThreeBuildings);
        Assert.Equal((2, 3, false), (growGoal.Progress, growGoal.Target, growGoal.IsComplete));
        var secondBuilding = state.Goals.Single(g => g.Code == FirstWeekCampaignCatalog.SecondBuilding);
        Assert.True(secondBuilding.IsComplete);
    }

    [Fact]
    public void AllClaimed_IsTrueOnlyWhenEveryGoalIsClaimed()
    {
        var every = FirstWeekCampaignCatalog.Goals.Select(g => g.Code).ToHashSet();

        Assert.True(FirstWeekCampaignCalculator.Evaluate(Start, Start.AddDays(8), Facts(), every).AllClaimed);
        every.Remove(FirstWeekCampaignCatalog.Goals[3].Code);
        Assert.False(FirstWeekCampaignCalculator.Evaluate(Start, Start.AddDays(8), Facts(), every).AllClaimed);
    }

    [Fact]
    public void ANowEarlierThanTheStart_IsTreatedAsDayOne()
    {
        var state = FirstWeekCampaignCalculator.Evaluate(Start, Start.AddHours(-3), Facts(), None);

        Assert.Equal(1, state.DayNumber);
    }
}

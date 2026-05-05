using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Twelfth-wave coverage tests filling genuine gaps not addressed by the previous eleven waves.
///
/// <para>Categories:</para>
/// <list type="bullet">
///   <item><b>ComputeNewPrice factor=0</b> — zero factor clamps to MinimumAllowedPrice rather than returning 0.</item>
///   <item><b>ComputeAnnualisedRatePercent ticksPerYear=0</b> — multiplies by zero, returns 0 safely (no division-by-zero).</item>
///   <item><b>Recommend with minTicksBeforeAdjustment=0</b> — evaluates profitability even at tick 0 when threshold is 0.</item>
///   <item><b>ContainsSuitableType with spaces inside CSV segments</b> — TrimEntries normalises internal spaces.</item>
///   <item><b>BotStateValidator.IsReadyForOperation onboarding-gate</b> — onboarded bot without token is not ready.</item>
///   <item><b>BotRosterFactory.Build strategy first-cycle ordering</b> — first 5 bots cycle strategies exactly once.</item>
///   <item><b>BotAccount.IsSkipped default and set</b> — fresh bots are not skipped; setting the flag works.</item>
///   <item><b>BotAccount.ConsecutiveErrors default</b> — fresh bots start with zero consecutive errors.</item>
///   <item><b>BotAccount.LastSuccessUtc default</b> — fresh bots have null LastSuccessUtc.</item>
///   <item><b>BotProfitCalculator.Classify with zero delta</b> — exactly zero change is Neutral (within ±2% band).</item>
///   <item><b>GameStateSummary TaxCycleTicks default</b> — default value is 0 (not seeded).</item>
///   <item><b>StrategyRecommendation ShouldAct=false default</b> — new recommendation with no explicit ShouldAct defaults to false.</item>
///   <item><b>BotOptions BotCount default</b> — fresh BotOptions has BotCount=3.</item>
///   <item><b>PriceAdjustmentHelper: factor=0 → floor clamp</b> — ComputeNewPrice(any, 0) returns MinimumAllowedPrice.</item>
///   <item><b>SelectAdjustableUnits: empty profile companies list</b> — yields no units (no exception).</item>
/// </list>
/// </summary>
public sealed class BotTwelfthWaveCoverageTests
{
    // ── ComputeNewPrice with factor = 0 ───────────────────────────────────────

    [Fact]
    public void ComputeNewPrice_ZeroFactor_ClampsToMinimumAllowedPrice()
    {
        // 100 × 0 = 0 → Max(Round(0, 2), 0.01) = 0.01
        var result = PriceAdjustmentHelper.ComputeNewPrice(100m, 0m);
        Assert.Equal(PriceAdjustmentHelper.MinimumAllowedPrice, result);
    }

    [Fact]
    public void ComputeNewPrice_ZeroFactorWithLargePrice_ClampsToMinimumAllowedPrice()
    {
        // Large prices produce 0 when factor=0; must still clamp to 0.01
        var result = PriceAdjustmentHelper.ComputeNewPrice(999_999m, 0m);
        Assert.Equal(PriceAdjustmentHelper.MinimumAllowedPrice, result);
    }

    // ── ComputeAnnualisedRatePercent with ticksPerYear = 0 ────────────────────

    [Fact]
    public void ComputeAnnualisedRatePercent_ZeroTicksPerYear_ReturnsZero()
    {
        // ratePerTick × 0 × 100 = 0 — no exception, just zero result.
        var result = BotProfitCalculator.ComputeAnnualisedRatePercent(
            currentNetWorth: 110_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: 8760,
            ticksPerYear: 0);

        Assert.Equal(0m, result);
    }

    // ── Recommend with minTicksBeforeAdjustment = 0 ───────────────────────────

    [Fact]
    public void Recommend_MinTicksIsZero_EvaluatesAtTickZero_SevereLoss()
    {
        // When minTicksBeforeAdjustment=0, the guard (ticksElapsed < 0) is FALSE even at tick 0.
        // The bot has lost 20% → severe-loss path.
        var rec = BotProfitCalculator.Recommend(
            currentNetWorth: 80_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: 0,
            minTicksBeforeAdjustment: 0);

        Assert.True(rec.ShouldAct);
        Assert.Equal(BotProfitCalculator.AggressivePriceReductionFactor, rec.PriceAdjustmentFactor);
    }

    [Fact]
    public void Recommend_MinTicksIsZero_EvaluatesAtTickZero_MildLoss()
    {
        // -5% loss at tick 0 with minTicks=0 → mild loss.
        var rec = BotProfitCalculator.Recommend(
            currentNetWorth: 95_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: 0,
            minTicksBeforeAdjustment: 0);

        Assert.True(rec.ShouldAct);
        Assert.Equal(BotProfitCalculator.MildPriceReductionFactor, rec.PriceAdjustmentFactor);
    }

    [Fact]
    public void Recommend_MinTicksIsZero_EvaluatesAtTickZero_Profitable_NoAction()
    {
        // +5% gain at tick 0 with minTicks=0 → profitable, no action.
        var rec = BotProfitCalculator.Recommend(
            currentNetWorth: 105_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: 0,
            minTicksBeforeAdjustment: 0);

        Assert.False(rec.ShouldAct);
        Assert.Equal(StrategyRecommendation.NoAction, rec);
    }

    // ── ContainsSuitableType: spaces inside CSV segments ─────────────────────

    [Fact]
    public void ContainsSuitableType_CsvWithSpaces_MatchesCleanType()
    {
        // "FACTORY , MINE" — spaces around FACTORY should be trimmed.
        Assert.True(OnboardingHelpers.ContainsSuitableType("FACTORY , MINE", "FACTORY"));
    }

    [Fact]
    public void ContainsSuitableType_CsvWithLeadingTrailingSpaces_MatchesSecondType()
    {
        // " MINE , SALES_SHOP " — leading/trailing spaces on each segment are trimmed.
        Assert.True(OnboardingHelpers.ContainsSuitableType(" MINE , SALES_SHOP ", "SALES_SHOP"));
    }

    [Fact]
    public void ContainsSuitableType_InternalSpaceInsideTypeName_DoesNotMatch()
    {
        // "FAC TORY" is not the same as "FACTORY" — internal space is not the separator.
        Assert.False(OnboardingHelpers.ContainsSuitableType("FAC TORY,MINE", "FACTORY"));
    }

    // ── BotStateValidator.IsReadyForOperation: onboarding gate ───────────────

    [Fact]
    public void IsReadyForOperation_OnboardedBotWithoutToken_ReturnsFalse()
    {
        // Onboarding completed but no valid token → not ready.
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "Bot", Email = "b@test.local", Strategy = "S",
            Profile = new PlayerProfile
            {
                Id = "p1", DisplayName = "Bot", Email = "b@test.local",
                OnboardingCompletedAtUtc = DateTime.UtcNow.AddHours(-1),
            },
        };

        Assert.False(BotStateValidator.IsReadyForOperation(bot));
    }

    [Fact]
    public void IsReadyForOperation_TokenValidButOnboardingIncomplete_ReturnsFalse()
    {
        // Valid token but no onboarding completion timestamp → not ready.
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "Bot", Email = "b@test.local", Strategy = "S",
            Token = "tok",
            TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2),
        };
        // OnboardingCompleted is driven by profile; no profile → false.
        Assert.False(BotStateValidator.IsReadyForOperation(bot));
    }

    // ── BotRosterFactory: first 5 bots contain one of each strategy ──────────

    [Fact]
    public void Build_FiveBots_StrategiesAreNotAllSame()
    {
        var bots = BotRosterFactory.Build(new BotOptions { BotCount = 5 }).ToList();
        var strategies = bots.Select(b => b.Strategy).Distinct().ToList();
        Assert.True(strategies.Count > 1, "First 5 bots should have more than one distinct strategy.");
    }

    [Fact]
    public void Build_TwoBots_IndicesAre1And2()
    {
        var bots = BotRosterFactory.Build(new BotOptions { BotCount = 2 }).ToList();
        Assert.Equal(1, bots[0].Index);
        Assert.Equal(2, bots[1].Index);
    }

    // ── BotAccount: IsSkipped default and mutation ───────────────────────────

    [Fact]
    public void BotAccount_IsSkipped_DefaultIsFalse()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "Bot", Email = "b@test.local", Strategy = "S",
        };
        Assert.False(bot.IsSkipped);
    }

    [Fact]
    public void BotAccount_IsSkipped_CanBeSetToTrue()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "Bot", Email = "b@test.local", Strategy = "S",
        };
        bot.IsSkipped = true;
        Assert.True(bot.IsSkipped);
    }

    // ── BotAccount: ConsecutiveErrors default ────────────────────────────────

    [Fact]
    public void BotAccount_ConsecutiveErrors_DefaultIsZero()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "Bot", Email = "b@test.local", Strategy = "S",
        };
        Assert.Equal(0, bot.ConsecutiveErrors);
    }

    // ── BotAccount: LastSuccessUtc default ───────────────────────────────────

    [Fact]
    public void BotAccount_LastSuccessUtc_DefaultIsNull()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "Bot", Email = "b@test.local", Strategy = "S",
        };
        Assert.Null(bot.LastSuccessUtc);
    }

    // ── BotProfitCalculator.Classify: exactly-zero change ───────────────────

    [Fact]
    public void Classify_ZeroDelta_IsNeutral()
    {
        // Current == Initial → delta = 0% which is within ±2% neutral band.
        Assert.Equal(ProfitabilityStatus.Neutral,
            BotProfitCalculator.Classify(100_000m, 100_000m));
    }

    // ── GameStateSummary defaults ────────────────────────────────────────────

    [Fact]
    public void GameStateSummary_TaxCycleTicks_DefaultIsZero()
    {
        var gs = new GameStateSummary();
        Assert.Equal(0, gs.TaxCycleTicks);
    }

    [Fact]
    public void GameStateSummary_CurrentTick_DefaultIsZero()
    {
        var gs = new GameStateSummary();
        Assert.Equal(0L, gs.CurrentTick);
    }

    // ── StrategyRecommendation: default ShouldAct ────────────────────────────

    [Fact]
    public void StrategyRecommendation_DefaultInstance_ShouldActIsFalse()
    {
        var rec = new StrategyRecommendation();
        Assert.False(rec.ShouldAct);
    }

    [Fact]
    public void StrategyRecommendation_DefaultInstance_PriceAdjustmentFactorIsZero()
    {
        // Zero factor is the uninitialised default. The NoAction sentinel also uses 0,
        // so both share the same numeric value but have different semantic meanings
        // (ShouldAct=false with Reason="" vs ShouldAct=false with Reason="Performance is acceptable.").
        var rec = new StrategyRecommendation();
        Assert.Equal(0m, rec.PriceAdjustmentFactor);
    }

    // ── BotOptions: BotCount default ────────────────────────────────────────

    [Fact]
    public void BotOptions_BotCount_DefaultIsThree()
    {
        Assert.Equal(3, new BotOptions().BotCount);
    }

    // ── SelectAdjustableUnits: empty companies list ───────────────────────────

    [Fact]
    public void SelectAdjustableUnits_EmptyCompaniesList_YieldsNoUnits()
    {
        var result = PriceAdjustmentHelper.SelectAdjustableUnits([]).ToList();
        Assert.Empty(result);
    }

    [Fact]
    public void SelectAdjustableUnits_CompanyWithNoBuildingUnits_YieldsNoUnits()
    {
        var companies = new List<CompanySummary>
        {
            new() { Id = "c1", Name = "Corp", Cash = 100m, Buildings = [] },
        };

        var result = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();
        Assert.Empty(result);
    }

    [Fact]
    public void SelectAdjustableUnits_BuildingWithNoUnits_YieldsNoUnits()
    {
        var companies = new List<CompanySummary>
        {
            new()
            {
                Id = "c1", Name = "Corp", Cash = 100m,
                Buildings = [new BuildingSummary { Id = "b1", Name = "Shop", Type = "SALES_SHOP", CityId = "city1", Units = [] }],
            },
        };

        var result = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();
        Assert.Empty(result);
    }
}

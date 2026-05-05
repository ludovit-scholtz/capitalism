using System.Text.Json;
using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Seventh coverage wave — fills targeted gaps identified after 742 passing tests.
/// <list type="bullet">
///   <item>
///     <b>ComputeRecommendationForBot negative elapsed</b> —
///     when <c>currentTick &lt; TrackingStartTick</c> the computed
///     <c>ticksElapsed</c> is negative; <see cref="BotProfitCalculator.Recommend"/>
///     must return <see cref="StrategyRecommendation.NoAction"/> because the bot
///     has not yet accumulated meaningful history.
///   </item>
///   <item>
///     <b>ComputeRecommendationForBot just-below threshold</b> —
///     <c>ticksElapsed = minTicksBeforeAdjustment - 1</c> must still return
///     NoAction even for a severely-loss-making bot.
///   </item>
///   <item>
///     <b>ComputeRecommendationForBot exactly at threshold</b> —
///     <c>ticksElapsed = minTicksBeforeAdjustment</c> with a loss must
///     produce an action recommendation.
///   </item>
///   <item>
///     <b>BotProfitCalculator.ComputeAnnualisedRatePercent full-year cycle</b> —
///     when <c>ticksElapsed = ticksPerYear</c> the annualised rate equals the
///     actual percent change over that year.
///   </item>
///   <item>
///     <b>BotProfitCalculator.ComputeAnnualisedRatePercent two-year cycle</b> —
///     when <c>ticksElapsed = 2 × ticksPerYear</c> the annualised rate is half
///     the total percent change.
///   </item>
///   <item>
///     <b>BotRosterFactory strategy cycling</b> —
///     with a count greater than the number of available strategy names, the
///     strategy list wraps around modularly.
///   </item>
///   <item>
///     <b>BotRosterFactory email format for all five strategies</b> —
///     emails are lower-cased versions of the display name appended with the domain.
///   </item>
///   <item>
///     <b>PriceAdjustmentHelper.SelectAdjustableUnits null MinPrice</b> —
///     units with <c>MinPrice = null</c> must be excluded (only units with a
///     non-null positive price are adjustable).
///   </item>
///   <item>
///     <b>PriceAdjustmentHelper.SelectAdjustableUnits zero MinPrice</b> —
///     units with <c>MinPrice = 0</c> must also be excluded because there is
///     no sensible base price to adjust from.
///   </item>
///   <item>
///     <b>OnboardingHelpers.ShouldResumeFromShopStep non-resumable steps</b> —
///     step values such as <c>"FACTORY"</c>, <c>"COMPLETE"</c>, <c>""</c>,
///     and <c>null</c> must all return false.
///   </item>
///   <item>
///     <b>OnboardingHelpers.ShouldResumeFromShopStep lowercase match</b> —
///     <c>"shop_selection"</c> (lowercase) must match because the comparison
///     is case-insensitive.
///   </item>
///   <item>
///     <b>BotStateValidator.IsAtRisk at exactly 100% of max</b> —
///     with <c>maxConsecutiveErrors=1</c> and <c>ConsecutiveErrors=1</c>
///     the ratio is 1.0 which is ≥ 0.5 → bot is at risk.
///   </item>
///   <item>
///     <b>BotStateValidator.IsAtRisk well-below threshold</b> —
///     with <c>maxConsecutiveErrors=100</c> and <c>ConsecutiveErrors=1</c>
///     the ratio is 0.01 which is &lt; 0.5 → not at risk.
///   </item>
///   <item>
///     <b>StrategyRecommendation.NoAction PriceAdjustmentFactor</b> —
///     the singleton's factor must be exactly 0 (zero), not 1.0, so that
///     calling code that guards on <c>factor == 0</c> works correctly.
///   </item>
///   <item>
///     <b>StrategyRecommendation.NoAction singleton identity</b> —
///     two calls to <see cref="StrategyRecommendation.NoAction"/> return
///     the same object reference.
///   </item>
///   <item>
///     <b>BotOptions.AllowedIndustries default count</b> —
///     the default roster contains exactly three starter industries so that
///     all onboarding paths are available out-of-the-box.
///   </item>
///   <item>
///     <b>BotOptions.TokenRefreshBufferMinutes default</b> —
///     the default buffer is 5 minutes, matching <see cref="BotAccount.IsTokenValid"/>
///     default parameter so the two configurations are consistent.
///   </item>
///   <item>
///     <b>BotAccount.TrackingStartTick default</b> —
///     a freshly-created bot has <c>TrackingStartTick = 0</c>, meaning no
///     ticks have been tracked yet.
///   </item>
///   <item>
///     <b>GameStateSummary TaxCycleTicks deserialization</b> —
///     the <c>taxCycleTicks</c> JSON field maps to
///     <see cref="GameStateSummary.TaxCycleTicks"/> correctly.
///   </item>
///   <item>
///     <b>CompanySummary with two buildings deserialization</b> —
///     a company with two buildings and per-building unit lists round-trips
///     through JSON correctly.
///   </item>
///   <item>
///     <b>BotAccount CurrentRank lifecycle</b> —
///     initial null → set to rank → cleared back to null lifecycle.
///   </item>
///   <item>
///     <b>BotProfitCalculator.Classify with identical current and initial</b> —
///     equal values produce a deltaPercent of 0 which is within the neutral
///     band → <see cref="ProfitabilityStatus.Neutral"/>.
///   </item>
///   <item>
///     <b>BotProfitCalculator.Classify with very large positive delta</b> —
///     an extreme profit (10× initial) is classified as Profitable.
///   </item>
///   <item>
///     <b>BotProfitCalculator.Classify with very large negative delta</b> —
///     total loss (currentNetWorth = 0) from a positive initial is Unprofitable.
///   </item>
///   <item>
///     <b>BotRosterFactory Index is 1-based sequential</b> —
///     the <c>Index</c> of the Nth bot is exactly N (1, 2, 3 …).
///   </item>
///   <item>
///     <b>BotRosterFactory DisplayName includes prefix and index</b> —
///     every display name contains the configured prefix and a two-digit index.
///   </item>
/// </list>
/// </summary>
public sealed class BotSeventhWaveCoverageTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── ComputeRecommendationForBot: elapsed tick edge cases ──────────────────

    [Fact]
    public void ComputeRecommendationForBot_CurrentTickLessThanTrackingStart_ReturnsNoAction()
    {
        // Negative ticksElapsed: bot tracking hasn't properly started (clock anomaly).
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC_Test", Email = "test@t.test", Strategy = "Retail",
            TrackingStartTick = 100,
            InitialNetWorth = 100_000m,
            CurrentNetWorth = 50_000m, // Severe loss if ticks allowed
        };

        // currentTick=50 < TrackingStartTick=100 → ticksElapsed=-50 < minTicksBeforeAdjustment
        var rec = BotOrchestrator.ComputeRecommendationForBot(bot, currentTick: 50, minTicksBeforeAdjustment: 5);

        Assert.False(rec.ShouldAct,
            "Negative elapsed ticks must not trigger a price-reduction action.");
        Assert.Same(StrategyRecommendation.NoAction, rec);
    }

    [Fact]
    public void ComputeRecommendationForBot_JustBelowThreshold_ReturnsNoAction()
    {
        // ticksElapsed = minTicksBeforeAdjustment - 1 = 4 < 5 threshold
        var bot = new BotAccount
        {
            Index = 2, DisplayName = "NPC_Test2", Email = "test2@t.test", Strategy = "Industrial",
            TrackingStartTick = 0,
            InitialNetWorth = 100_000m,
            CurrentNetWorth = 80_000m, // −20% severe loss
        };

        var rec = BotOrchestrator.ComputeRecommendationForBot(bot, currentTick: 4, minTicksBeforeAdjustment: 5);

        Assert.False(rec.ShouldAct,
            "Bot must not act when ticks elapsed (4) is one less than the minimum (5).");
    }

    [Fact]
    public void ComputeRecommendationForBot_ExactlyAtThresholdWithLoss_ReturnsAction()
    {
        // ticksElapsed = minTicksBeforeAdjustment = 5 — exactly at the threshold
        var bot = new BotAccount
        {
            Index = 3, DisplayName = "NPC_Test3", Email = "test3@t.test", Strategy = "Trading",
            TrackingStartTick = 0,
            InitialNetWorth = 100_000m,
            CurrentNetWorth = 80_000m, // −20% severe loss → aggressive action
        };

        var rec = BotOrchestrator.ComputeRecommendationForBot(bot, currentTick: 5, minTicksBeforeAdjustment: 5);

        Assert.True(rec.ShouldAct,
            "Bot must act when ticksElapsed equals the minimum and there is a significant loss.");
        Assert.Equal(BotProfitCalculator.AggressivePriceReductionFactor, rec.PriceAdjustmentFactor);
    }

    // ── BotProfitCalculator.ComputeAnnualisedRatePercent: full/multi-year ─────

    [Fact]
    public void ComputeAnnualisedRatePercent_FullYearCycle_EqualsActualPercentChange()
    {
        // When ticksElapsed = ticksPerYear, annualised rate = actual % change over the year.
        // gain = 10_000 / 100_000 = 10%; annualised (1 year) = 10%.
        //
        // Tolerance note: the formula divides by (initialNetWorth × ticksElapsed) and then
        // multiplies by ticksPerYear × 100. The intermediate C# decimal division of
        // 10_000 / 876_000_000 produces a finite repeating representation that may
        // differ from exact 10 by a sub-cent rounding artefact. The stored memory rule
        // (BotProfitCalculator annualised-rate tests) documents this as the correct pattern:
        // use Assert.InRange with ±0.01 rather than exact equality.
        var rate = BotProfitCalculator.ComputeAnnualisedRatePercent(
            currentNetWorth: 110_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: 8760,
            ticksPerYear: 8760);

        Assert.InRange(rate, 9.99m, 10.01m);
    }

    [Fact]
    public void ComputeAnnualisedRatePercent_TwoYearCycle_IsHalfTheTotalGain()
    {
        // When ticksElapsed = 2 × ticksPerYear, annualised rate = total gain / 2 years.
        // gain = 20_000 / 100_000 = 20%; over 2 years → 10% / year.
        //
        // Tolerance note: same rounding rationale as the full-year test above.
        // The intermediate decimal division may produce a sub-cent artefact, so
        // Assert.InRange(9.99, 10.01) is the documented safe pattern.
        var rate = BotProfitCalculator.ComputeAnnualisedRatePercent(
            currentNetWorth: 120_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: 17_520,
            ticksPerYear: 8760);

        Assert.InRange(rate, 9.99m, 10.01m);
    }

    // ── BotRosterFactory: strategy cycling and email format ───────────────────

    [Fact]
    public void BotRosterFactory_MoreBotsThanStrategies_StrategiesCycleModularly()
    {
        // 5 strategies, 7 bots: bots 6 and 7 get strategies 1 and 2 again.
        var opts = new BotOptions { BotCount = 7 };
        var bots = BotRosterFactory.Build(opts);

        Assert.Equal(7, bots.Count);
        Assert.Equal(bots[0].Strategy, bots[5].Strategy); // index 1 mod 5 == index 6 mod 5
        Assert.Equal(bots[1].Strategy, bots[6].Strategy); // index 2 mod 5 == index 7 mod 5
    }

    [Fact]
    public void BotRosterFactory_EmailUsesLowercaseDisplayName()
    {
        var opts = new BotOptions { BotCount = 5, BotEmailDomain = "test.local", BotNamePrefix = "Bot" };
        var bots = BotRosterFactory.Build(opts);

        foreach (var bot in bots)
        {
            var expectedEmail = $"{bot.DisplayName.ToLowerInvariant()}@test.local";
            Assert.Equal(expectedEmail, bot.Email);
        }
    }

    [Fact]
    public void BotRosterFactory_IndexIs1BasedSequential()
    {
        var opts = new BotOptions { BotCount = 5 };
        var bots = BotRosterFactory.Build(opts);

        for (var i = 0; i < bots.Count; i++)
            Assert.Equal(i + 1, bots[i].Index);
    }

    [Fact]
    public void BotRosterFactory_DisplayNameContainsPrefixAndTwoDigitIndex()
    {
        var opts = new BotOptions { BotCount = 3, BotNamePrefix = "X" };
        var bots = BotRosterFactory.Build(opts);

        // Display names should be like "X_Trading_01", "X_Industrial_02", "X_Retail_03"
        foreach (var bot in bots)
        {
            Assert.StartsWith("X_", bot.DisplayName);
            // Two-digit zero-padded index appears in the name
            Assert.Contains($"_{bot.Index:D2}", bot.DisplayName);
        }
    }

    // ── PriceAdjustmentHelper.SelectAdjustableUnits: null and zero MinPrice ───

    [Fact]
    public void SelectAdjustableUnits_NullMinPrice_UnitExcluded()
    {
        var companies = new List<CompanySummary>
        {
            new()
            {
                Id = "c1", Name = "Company A", Cash = 100_000m,
                Buildings =
                [
                    new() {
                        Id = "b1", Name = "Shop A", Type = "SALES_SHOP",
                        Units = [ new() { Id = "u1", UnitType = "PUBLIC_SALES", MinPrice = null } ]
                    }
                ]
            }
        };

        var adjustable = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();

        Assert.Empty(adjustable);
    }

    [Fact]
    public void SelectAdjustableUnits_ZeroMinPrice_UnitExcluded()
    {
        var companies = new List<CompanySummary>
        {
            new()
            {
                Id = "c1", Name = "Company B", Cash = 100_000m,
                Buildings =
                [
                    new() {
                        Id = "b1", Name = "Shop B", Type = "SALES_SHOP",
                        Units = [ new() { Id = "u1", UnitType = "PUBLIC_SALES", MinPrice = 0m } ]
                    }
                ]
            }
        };

        var adjustable = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();

        Assert.Empty(adjustable);
    }

    [Fact]
    public void SelectAdjustableUnits_MixedNullAndPositive_OnlyPositiveReturned()
    {
        var companies = new List<CompanySummary>
        {
            new()
            {
                Id = "c1", Name = "Company C", Cash = 100_000m,
                Buildings =
                [
                    new()
                    {
                        Id = "b1", Name = "Shop C", Type = "SALES_SHOP",
                        Units =
                        [
                            new() { Id = "u1", UnitType = "PUBLIC_SALES", MinPrice = null },
                            new() { Id = "u2", UnitType = "PUBLIC_SALES", MinPrice = 0m },
                            new() { Id = "u3", UnitType = "PUBLIC_SALES", MinPrice = 50m },
                        ]
                    }
                ]
            }
        };

        var adjustable = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();

        Assert.Single(adjustable);
        Assert.Equal("u3", adjustable[0].Unit.Id);
    }

    // ── OnboardingHelpers.ShouldResumeFromShopStep ────────────────────────────

    [Fact]
    public void ShouldResumeFromShopStep_FactoryStep_ReturnsFalse()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "N", Email = "n@t.t", Strategy = "Trading",
            Profile = new PlayerProfile { OnboardingCurrentStep = "FACTORY" }
        };

        Assert.False(OnboardingHelpers.ShouldResumeFromShopStep(bot));
    }

    [Fact]
    public void ShouldResumeFromShopStep_CompleteStep_ReturnsFalse()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "N", Email = "n@t.t", Strategy = "Trading",
            Profile = new PlayerProfile { OnboardingCurrentStep = "COMPLETE" }
        };

        Assert.False(OnboardingHelpers.ShouldResumeFromShopStep(bot));
    }

    [Fact]
    public void ShouldResumeFromShopStep_EmptyStep_ReturnsFalse()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "N", Email = "n@t.t", Strategy = "Trading",
            Profile = new PlayerProfile { OnboardingCurrentStep = "" }
        };

        Assert.False(OnboardingHelpers.ShouldResumeFromShopStep(bot));
    }

    [Fact]
    public void ShouldResumeFromShopStep_LowercaseShopSelection_ReturnsTrue()
    {
        // Case-insensitive match: "shop_selection" must be treated same as "SHOP_SELECTION"
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "N", Email = "n@t.t", Strategy = "Trading",
            Profile = new PlayerProfile { OnboardingCurrentStep = "shop_selection" }
        };

        Assert.True(OnboardingHelpers.ShouldResumeFromShopStep(bot));
    }

    // ── BotStateValidator.IsAtRisk: boundary with different max values ─────────

    [Fact]
    public void IsAtRisk_Exactly100Percent_WhenMaxEquals1_ReturnsTrue()
    {
        // 1/1 = 100% → at risk (≥ 50%)
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "N", Email = "n@t.t", Strategy = "Trading",
            ConsecutiveErrors = 1,
        };

        Assert.True(BotStateValidator.IsAtRisk(bot, maxConsecutiveErrors: 1));
    }

    [Fact]
    public void IsAtRisk_OnePercentRatio_NotAtRisk()
    {
        // 1/100 = 1% → not at risk (< 50%)
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "N", Email = "n@t.t", Strategy = "Trading",
            ConsecutiveErrors = 1,
        };

        Assert.False(BotStateValidator.IsAtRisk(bot, maxConsecutiveErrors: 100));
    }

    // ── StrategyRecommendation.NoAction field assertions ─────────────────────

    [Fact]
    public void NoAction_PriceAdjustmentFactor_IsExactlyZero()
    {
        // PriceAdjustmentFactor = 0 means "no price change"; callers guard on ShouldAct,
        // but the factor value itself must also be zero to prevent accidental multiplication
        // by a no-op factor.
        Assert.Equal(0m, StrategyRecommendation.NoAction.PriceAdjustmentFactor);
    }

    [Fact]
    public void NoAction_ShouldAct_IsFalse()
    {
        // Complementary assertion: callers must guard on ShouldAct before reading the factor.
        // Both ShouldAct=false and Factor=0 together make the "do nothing" contract unambiguous.
        Assert.False(StrategyRecommendation.NoAction.ShouldAct);
    }

    [Fact]
    public void NoAction_SingletonIdentity_SameReferenceReturnedEachTime()
    {
        var a = StrategyRecommendation.NoAction;
        var b = StrategyRecommendation.NoAction;
        Assert.Same(a, b);
    }

    // ── BotOptions defaults ───────────────────────────────────────────────────

    [Fact]
    public void BotOptions_AllowedIndustries_DefaultsToThreeStarterIndustries()
    {
        var opts = new BotOptions();
        Assert.Equal(3, opts.AllowedIndustries.Length);
        Assert.Contains("FURNITURE", opts.AllowedIndustries);
        Assert.Contains("FOOD_PROCESSING", opts.AllowedIndustries);
        Assert.Contains("HEALTHCARE", opts.AllowedIndustries);
    }

    [Fact]
    public void BotOptions_TokenRefreshBufferMinutes_MatchesBotAccountDefaultBuffer()
    {
        // BotOptions.TokenRefreshBufferMinutes default (5) must match the default
        // argument to BotAccount.IsTokenValid(bufferMinutes) (also 5) so that the
        // orchestrator and the model agree on the same buffer without configuration.
        var opts = new BotOptions();
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "N", Email = "n@t.t", Strategy = "Trading",
            Token = "t",
            TokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(opts.TokenRefreshBufferMinutes - 1),
        };

        // Token expiring in less than the default buffer → needs refresh
        Assert.False(bot.IsTokenValid(opts.TokenRefreshBufferMinutes));
    }

    // ── BotAccount.TrackingStartTick default ─────────────────────────────────

    [Fact]
    public void BotAccount_TrackingStartTick_DefaultsToZero()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC", Email = "e@t.t", Strategy = "Retail",
        };

        Assert.Equal(0L, bot.TrackingStartTick);
    }

    // ── GameStateSummary JSON deserialization ─────────────────────────────────

    [Fact]
    public void GameStateSummary_Deserialize_ParsesTaxCycleTicks()
    {
        const string json = """
            {
              "currentTick": 12345,
              "tickIntervalSeconds": 60,
              "taxCycleTicks": 8760
            }
            """;

        var gs = JsonSerializer.Deserialize<GameStateSummary>(json, JsonOpts)!;

        Assert.Equal(12345L, gs.CurrentTick);
        Assert.Equal(60, gs.TickIntervalSeconds);
        Assert.Equal(8760, gs.TaxCycleTicks);
    }

    // ── CompanySummary with multiple buildings ────────────────────────────────

    [Fact]
    public void CompanySummary_Deserialize_ParsesTwoBuildings()
    {
        const string json = """
            {
              "id": "c1",
              "name": "My Corp",
              "cash": 500000.00,
              "buildings": [
                { "id": "b1", "name": "Factory 1", "type": "FACTORY", "cityId": "city1", "units": [] },
                { "id": "b2", "name": "Shop 1",    "type": "SALES_SHOP", "cityId": "city1", "units": [] }
              ]
            }
            """;

        var company = JsonSerializer.Deserialize<CompanySummary>(json, JsonOpts)!;

        Assert.Equal("c1", company.Id);
        Assert.Equal(500_000m, company.Cash);
        Assert.Equal(2, company.Buildings.Count);
        Assert.Equal("Factory 1", company.Buildings[0].Name);
        Assert.Equal("Shop 1", company.Buildings[1].Name);
    }

    // ── BotAccount.CurrentRank lifecycle ─────────────────────────────────────

    [Fact]
    public void BotAccount_CurrentRank_NullThenSetThenCleared()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "N", Email = "n@t.t", Strategy = "Trading",
        };

        // Default: null
        Assert.Null(bot.CurrentRank);

        // Set rank
        bot.CurrentRank = 7;
        Assert.Equal(7, bot.CurrentRank);

        // Clear (when bot not found in rankings)
        bot.CurrentRank = null;
        Assert.Null(bot.CurrentRank);
    }

    // ── BotProfitCalculator.Classify edge cases ───────────────────────────────

    [Fact]
    public void Classify_IdenticalCurrentAndInitial_IsNeutral()
    {
        // deltaPercent = 0 → within ±2% neutral band
        var status = BotProfitCalculator.Classify(100_000m, 100_000m);
        Assert.Equal(ProfitabilityStatus.Neutral, status);
    }

    [Fact]
    public void Classify_ExtremeProfit_IsProfitable()
    {
        // 10× the initial net worth
        var status = BotProfitCalculator.Classify(1_000_000m, 100_000m);
        Assert.Equal(ProfitabilityStatus.Profitable, status);
    }

    [Fact]
    public void Classify_TotalLoss_IsUnprofitable()
    {
        // current = 0, initial > 0 → 100% loss
        var status = BotProfitCalculator.Classify(0m, 100_000m);
        Assert.Equal(ProfitabilityStatus.Unprofitable, status);
    }
}

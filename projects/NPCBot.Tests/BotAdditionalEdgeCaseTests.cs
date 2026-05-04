using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Additional edge-case tests covering corner cases across all helpers:
/// zero/negative tick-elapsed guards, exact neutral-band boundaries, clock-skew
/// protection, price-increase scenarios, and model default verification.
/// All tests are pure (no I/O) and exercise deterministic pure-function paths.
/// </summary>
public sealed class BotAdditionalEdgeCaseTests
{
    // ── BotProfitCalculator: tick-elapsed edge cases ──────────────────────────

    [Fact]
    public void Recommend_ZeroTicksElapsed_ReturnsNoAction()
    {
        // ticksElapsed == 0 → guard fires (0 < minTicksBeforeAdjustment=5)
        var rec = BotProfitCalculator.Recommend(
            currentNetWorth: 50_000m,
            initialNetWorth: 100_000m,  // −50% loss — would be aggressive otherwise
            ticksElapsed: 0,
            minTicksBeforeAdjustment: 5);
        Assert.False(rec.ShouldAct, "No action should be recommended at zero ticks.");
        Assert.Equal(StrategyRecommendation.NoAction.PriceAdjustmentFactor, rec.PriceAdjustmentFactor);
    }

    [Fact]
    public void ComputeAnnualisedRatePercent_NegativeTicksElapsed_ReturnsZero()
    {
        // Guard: ticksElapsed <= 0 → return 0m (clock-skew or resync protection).
        var rate = BotProfitCalculator.ComputeAnnualisedRatePercent(
            currentNetWorth: 200_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: -10);
        Assert.Equal(0m, rate);
    }

    [Fact]
    public void Recommend_NegativeTicksElapsed_ReturnsNoAction()
    {
        // Negative elapsed ticks (clock skew) must never trigger an action.
        var rec = BotProfitCalculator.Recommend(
            currentNetWorth: 1m,         // extreme loss
            initialNetWorth: 100_000m,
            ticksElapsed: -1);
        Assert.False(rec.ShouldAct, "Negative ticks elapsed must produce NoAction.");
    }

    // ── BotProfitCalculator: exact neutral-band boundaries ───────────────────

    [Fact]
    public void Classify_ExactlyAtPositiveTwoPercent_IsNeutral()
    {
        // deltaPercent == NeutralBandPercent (0.02) uses strict >, so equals → Neutral.
        // initial=100 000, current=102 000 → delta=2 000 → 2 000/100 000 = 0.02 exactly
        Assert.Equal(ProfitabilityStatus.Neutral,
            BotProfitCalculator.Classify(102_000m, 100_000m));
    }

    [Fact]
    public void Classify_JustAbovePositiveTwoPercent_IsProfitable()
    {
        // 102 001 → delta = 2 001 → deltaPercent = 0.02001 > 0.02 → Profitable
        Assert.Equal(ProfitabilityStatus.Profitable,
            BotProfitCalculator.Classify(102_001m, 100_000m));
    }

    [Fact]
    public void Classify_ExactlyAtNegativeTwoPercent_IsNeutral()
    {
        // deltaPercent == −NeutralBandPercent (−0.02) uses strict <, so equals → Neutral.
        // initial=100 000, current=98 000 → delta=−2 000 → −0.02 exactly
        Assert.Equal(ProfitabilityStatus.Neutral,
            BotProfitCalculator.Classify(98_000m, 100_000m));
    }

    [Fact]
    public void Classify_JustBelowNegativeTwoPercent_IsUnprofitable()
    {
        // 97 999 → delta = −2 001 → deltaPercent = −0.02001 < −0.02 → Unprofitable
        Assert.Equal(ProfitabilityStatus.Unprofitable,
            BotProfitCalculator.Classify(97_999m, 100_000m));
    }

    // ── BotOrchestrator: clock-skew (TrackingStartTick > currentTick) ─────────

    [Fact]
    public void ComputeRecommendationForBot_TrackingStartAheadOfCurrentTick_ReturnsNoAction()
    {
        // If a game server resets or the tick counter rolls back, TrackingStartTick
        // can be higher than currentTick → negative elapsed ticks.  Must not recommend.
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC_Trading_01", Email = "npc@test.example",
            Strategy = "Trading",
            InitialNetWorth = 100_000m,
            CurrentNetWorth = 1m,        // worst case: near-total loss
            TrackingStartTick = 500,
        };

        var rec = BotOrchestrator.ComputeRecommendationForBot(
            bot, currentTick: 100, minTicksBeforeAdjustment: 5);

        Assert.False(rec.ShouldAct,
            "Negative elapsed ticks from clock skew must produce NoAction.");
    }

    // ── PriceAdjustmentHelper: additional selectability edge cases ────────────

    [Fact]
    public void SelectAdjustableUnits_UnitWithNegativeMinPrice_IsExcluded()
    {
        // The `is > 0m` pattern in SelectAdjustableUnits must reject negative MinPrice.
        var companies = new List<CompanySummary>
        {
            new()
            {
                Id = "c1", Name = "Negative Corp",
                Buildings = [
                    new BuildingSummary
                    {
                        Id = "b1", Name = "Shop A", Type = "SALES_SHOP", CityId = "ba",
                        Units = [
                            new UnitSummary { Id = "u1", UnitType = "PUBLIC_SALES", MinPrice = -5.00m },
                        ]
                    }
                ]
            }
        };

        var units = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();
        Assert.Empty(units);
    }

    [Fact]
    public void SelectAdjustableUnits_AllUnitsPublicSales_ReturnsAllEligible()
    {
        // A building where EVERY unit is PUBLIC_SALES with valid prices — all must be returned.
        var companies = new List<CompanySummary>
        {
            new()
            {
                Id = "c1", Name = "Corp",
                Buildings = [
                    new BuildingSummary
                    {
                        Id = "b1", Name = "Multi-shop", Type = "SALES_SHOP", CityId = "ba",
                        Units = [
                            new UnitSummary { Id = "u1", UnitType = "PUBLIC_SALES", MinPrice = 10m },
                            new UnitSummary { Id = "u2", UnitType = "PUBLIC_SALES", MinPrice = 20m },
                            new UnitSummary { Id = "u3", UnitType = "PUBLIC_SALES", MinPrice = 30m },
                        ]
                    }
                ]
            }
        };

        var units = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();
        Assert.Equal(3, units.Count);
    }

    [Fact]
    public void ComputeNewPrice_FactorGreaterThanOne_IncreasesPrice()
    {
        // A factor > 1.0 (unusual but valid) must apply correctly and not be clamped to minimum.
        var result = PriceAdjustmentHelper.ComputeNewPrice(100m, 1.10m);
        Assert.Equal(110m, result); // 100 × 1.1 = 110.00
    }

    // ── BotAccount: token validity with varied buffer ─────────────────────────

    [Fact]
    public void IsTokenValid_LargeBuffer_ExpiresTokenThatIsWithinBuffer()
    {
        // Token expires in 60 minutes; buffer = 120 minutes → token needs refresh.
        // UtcNow < TokenExpiresAtUtc.AddMinutes(-120)
        // = UtcNow < (UtcNow + 60min - 120min)
        // = UtcNow < (UtcNow - 60min) → false → not valid
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "Test", Email = "t@t.example", Strategy = "Trading",
            Token = "valid-token",
            TokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(60),
        };

        Assert.False(bot.IsTokenValid(bufferMinutes: 120),
            "Token expiring in 60 min should be considered stale with a 120-min buffer.");
    }

    [Fact]
    public void IsTokenValid_SmallBuffer_ValidatesTokenThatExpiresInDistantFuture()
    {
        // Token expires in 2 hours; buffer = 1 minute → should be valid.
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "Test", Email = "t@t.example", Strategy = "Trading",
            Token = "valid-token",
            TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2),
        };

        Assert.True(bot.IsTokenValid(bufferMinutes: 1),
            "Token expiring in 2 hours should be valid with only a 1-minute buffer.");
    }

    // ── BotAccount: edge cases in onboarding state ────────────────────────────

    [Fact]
    public void OnboardingCompleted_ProfileWithCompaniesButNoCompletionDate_ReturnsFalse()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC", Email = "npc@t.example", Strategy = "Trading",
            Profile = new PlayerProfile
            {
                Id = "p1",
                OnboardingCompletedAtUtc = null,   // not done
                Companies = [new CompanySummary { Id = "co1", Name = "Corp" }],
            }
        };

        Assert.False(bot.OnboardingCompleted,
            "Having companies does not mean onboarding is complete; the completion timestamp must be set.");
    }

    // ── BotStateValidator: IsAtRisk with skipped bot having many errors ───────

    [Fact]
    public void IsAtRisk_SkippedBotWithExtremlyHighErrors_ReturnsFalse()
    {
        // A skipped bot is permanently excluded from operation — IsAtRisk must return false
        // even if ConsecutiveErrors is orders of magnitude above the limit.
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC", Email = "npc@t.example", Strategy = "Trading",
            IsSkipped = true,
            ConsecutiveErrors = 10_000,
        };

        Assert.False(BotStateValidator.IsAtRisk(bot, maxConsecutiveErrors: 5),
            "A skipped bot must never be reported as at-risk — it is already removed from the pool.");
    }

    [Fact]
    public void IsAtRisk_ConsecutiveErrorsOfOne_WithMaxFive_ReturnsFalse()
    {
        // 1 error out of max 5 = 20% which is below the 50% threshold → not at risk.
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC", Email = "npc@t.example", Strategy = "Trading",
            ConsecutiveErrors = 1,
        };
        Assert.False(BotStateValidator.IsAtRisk(bot, maxConsecutiveErrors: 5));
    }

    [Fact]
    public void Validate_SingleIssue_SummaryEqualsIssueText()
    {
        // When only one issue is present, the summary must equal that issue's text verbatim.
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC", Email = "npc@t.example", Strategy = "Trading",
            Token = "tok", TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2),
            Profile = new PlayerProfile { OnboardingCompletedAtUtc = DateTime.UtcNow.AddDays(-1) },
            IsSkipped = true,
            LastSuccessUtc = DateTime.UtcNow,  // recent success — not stale
        };

        var result = BotStateValidator.Validate(bot);
        Assert.False(result.IsValid);
        Assert.Single(result.Issues);
        Assert.Equal(result.Issues[0], result.Summary);
    }

    // ── GameModels: default field verification ────────────────────────────────

    [Fact]
    public void GameStateSummary_TickIntervalSeconds_DefaultsToZero()
    {
        var gs = new GameStateSummary();
        Assert.Equal(0, gs.TickIntervalSeconds);
    }

    [Fact]
    public void GameStateSummary_TaxCycleTicks_DefaultsToZero()
    {
        var gs = new GameStateSummary();
        Assert.Equal(0, gs.TaxCycleTicks);
    }

    [Fact]
    public void BuildingLotSummary_SuitableTypes_DefaultsToEmptyString()
    {
        var lot = new BuildingLotSummary();
        Assert.Equal(string.Empty, lot.SuitableTypes);
    }

    [Fact]
    public void BuildingLotSummary_Price_DefaultsToZero()
    {
        var lot = new BuildingLotSummary();
        Assert.Equal(0m, lot.Price);
    }

    [Fact]
    public void UnitSummary_MinPrice_DefaultsToNull()
    {
        var unit = new UnitSummary();
        Assert.Null(unit.MinPrice);
    }

    [Fact]
    public void CompanySummary_Buildings_DefaultsToEmptyList()
    {
        var company = new CompanySummary();
        Assert.NotNull(company.Buildings);
        Assert.Empty(company.Buildings);
    }

    [Fact]
    public void BuildingSummary_Units_DefaultsToEmptyList()
    {
        var building = new BuildingSummary();
        Assert.NotNull(building.Units);
        Assert.Empty(building.Units);
    }

    // ── BotOptions: configuration defaults that matter at runtime ────────────

    [Fact]
    public void BotOptions_GraphqlUrl_StartsWithHttps()
    {
        // The default URL must use HTTPS for production security.
        var opts = new BotOptions();
        Assert.StartsWith("https://", opts.GraphqlUrl);
    }

    [Fact]
    public void BotOptions_Enabled_DefaultsToTrue()
    {
        // Runner must be enabled by default; disabling is an explicit opt-out.
        Assert.True(new BotOptions().Enabled);
    }

    // ── OnboardingHelpers: whitespace handling in ContainsSuitableType ────────

    [Fact]
    public void ContainsSuitableType_WhitespaceOnlyField_ReturnsFalse()
    {
        // A field that is only whitespace (e.g. a blank API response) must not match.
        Assert.False(OnboardingHelpers.ContainsSuitableType("   ", "FACTORY"));
    }

    [Fact]
    public void ContainsSuitableType_WhitespaceOnlyType_ReturnsFalse()
    {
        // A blank suitableType parameter must never match anything.
        Assert.False(OnboardingHelpers.ContainsSuitableType("FACTORY,MINE", "   "));
    }

    [Fact]
    public void ContainsSuitableType_SpacePaddedCsvEntries_StillMatches()
    {
        // "FACTORY , MINE" has spaces around "MINE" — TrimEntries should handle this.
        Assert.True(OnboardingHelpers.ContainsSuitableType("FACTORY , MINE", "MINE"));
    }

    [Fact]
    public void PickCheapestAvailableLot_SuitableTypesIsNull_NotMatchedAndNoException()
    {
        // A lot whose SuitableTypes is null (network default) must not throw and must not match.
        // Note: BuildingLotSummary.SuitableTypes is `string` with default empty, so we simulate
        // a whitespace-only value that behaves similarly to null via IsNullOrWhiteSpace.
        var lot = new BuildingLotSummary
        {
            Id = "lot1", District = "D1", Price = 100m,
            SuitableTypes = string.Empty,   // no types — should not match "FACTORY"
            BuildingId = null,
        };

        var result = OnboardingHelpers.PickCheapestAvailableLot([lot], "FACTORY");
        Assert.Null(result);
    }

    // ── BotProfitCalculator: net-worth zero-company combinations ─────────────

    [Fact]
    public void ComputeNetWorth_AllCompaniesZeroCash_ReturnsZero()
    {
        var profile = new PlayerProfile
        {
            Companies =
            [
                new CompanySummary { Cash = 0m },
                new CompanySummary { Cash = 0m },
            ]
        };
        Assert.Equal(0m, BotProfitCalculator.ComputeNetWorth(profile));
    }

    [Fact]
    public void Classify_BothCurrentAndInitialNegative_AndCurrentWorse_IsUnprofitable()
    {
        // Initial = −50 000, Current = −70 000 → delta = −20 000, ratio = −0.4 → Unprofitable.
        Assert.Equal(ProfitabilityStatus.Unprofitable,
            BotProfitCalculator.Classify(-70_000m, -50_000m));
    }

    [Fact]
    public void Classify_BothCurrentAndInitialNegative_AndCurrentBetter_IsProfitable()
    {
        // Initial = −100 000, Current = −50 000 → delta = +50 000, ratio = 0.5 → Profitable.
        Assert.Equal(ProfitabilityStatus.Profitable,
            BotProfitCalculator.Classify(-50_000m, -100_000m));
    }

    // ── Strategy recommendation does not increase prices ──────────────────────

    [Fact]
    public void Recommend_AllActionFactors_AreLessThanOne()
    {
        // All price-adjustment factors must reduce prices (factor < 1.0).
        // This proves the bot never inflates prices during a loss phase.
        Assert.True(BotProfitCalculator.MildPriceReductionFactor < 1m,
            "Mild reduction factor must be less than 1.");
        Assert.True(BotProfitCalculator.AggressivePriceReductionFactor < 1m,
            "Aggressive reduction factor must be less than 1.");
        Assert.True(BotProfitCalculator.AggressivePriceReductionFactor < BotProfitCalculator.MildPriceReductionFactor,
            "Aggressive factor must be more severe (smaller) than mild factor.");
    }

    // ── BotRosterFactory: email format verification ───────────────────────────

    [Fact]
    public void Build_AllEmails_ContainAtSign()
    {
        var opts = new BotOptions { BotCount = 5 };
        var roster = BotRosterFactory.Build(opts);
        Assert.All(roster, b => Assert.Contains("@", b.Email));
    }

    [Fact]
    public void Build_AllEmails_ContainStrategyInLowercase()
    {
        // The email includes the strategy name in lowercase so it's parseable for diagnostics.
        var opts = new BotOptions { BotCount = 5 };
        var roster = BotRosterFactory.Build(opts);
        Assert.All(roster, b =>
            Assert.Contains(b.Strategy.ToLowerInvariant(), b.Email));
    }
}

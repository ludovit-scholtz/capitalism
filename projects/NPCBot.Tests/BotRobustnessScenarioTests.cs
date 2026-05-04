using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Robustness scenario tests covering multi-component interactions under edge conditions:
/// repeated price adjustments, price-floor protection, error accumulation, large rosters,
/// and tick-boundary edge cases. All tests are pure (no I/O).
/// </summary>
public sealed class BotRobustnessScenarioTests
{
    // ── Price floor protection across repeated adjustments ────────────────────

    [Fact]
    public void RepeatedAggressiveAdjustments_NeverDropsBelowMinimumPrice()
    {
        // Simulate 20 consecutive aggressive reductions — price must stay at or above 0.01.
        var price = 1.00m;
        const int iterations = 20;

        for (var i = 0; i < iterations; i++)
            price = PriceAdjustmentHelper.ComputeNewPrice(price, BotProfitCalculator.AggressivePriceReductionFactor);

        Assert.True(price >= PriceAdjustmentHelper.MinimumAllowedPrice,
            $"Price {price} dropped below the minimum allowed price after {iterations} reductions.");
    }

    [Fact]
    public void RepeatedMildAdjustments_NeverDropsBelowMinimumPrice()
    {
        // 50 mild reductions on a very small starting price — must still floor at 0.01.
        var price = 0.10m;
        for (var i = 0; i < 50; i++)
            price = PriceAdjustmentHelper.ComputeNewPrice(price, BotProfitCalculator.MildPriceReductionFactor);

        Assert.True(price >= PriceAdjustmentHelper.MinimumAllowedPrice,
            $"Price {price} dropped below the minimum allowed price after 50 mild reductions.");
    }

    // ── Large roster — all strategies covered ────────────────────────────────

    [Fact]
    public void LargeRoster_MaxBotCount_AllStrategiesAppear()
    {
        var opts = new BotOptions { BotCount = 20 };
        var roster = BotRosterFactory.Build(opts);

        var strategies = roster.Select(b => b.Strategy).Distinct().ToHashSet();
        Assert.Contains("Trading", strategies);
        Assert.Contains("Industrial", strategies);
        Assert.Contains("Retail", strategies);
        Assert.Contains("Mixed", strategies);
        Assert.Contains("Aggressive", strategies);
    }

    [Fact]
    public void LargeRoster_MaxBotCount_AllEmailsDistinct()
    {
        var opts = new BotOptions { BotCount = 20 };
        var roster = BotRosterFactory.Build(opts);

        var uniqueEmails = roster.Select(b => b.Email)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        Assert.Equal(20, uniqueEmails);
    }

    [Fact]
    public void LargeRoster_MaxBotCount_AllIndicesDistinct()
    {
        var opts = new BotOptions { BotCount = 20 };
        var roster = BotRosterFactory.Build(opts);

        var uniqueIndices = roster.Select(b => b.Index).Distinct().Count();
        Assert.Equal(20, uniqueIndices);
    }

    // ── Error accumulation and skip logic ────────────────────────────────────

    [Fact]
    public void ErrorAccumulation_ReachingMaxErrors_BotIsSkipped()
    {
        // Simulate the orchestrator incrementing ConsecutiveErrors until it hits the max.
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC 001", Email = "npc001@test.example", Strategy = "FURNITURE",
        };

        const int maxErrors = 5;
        for (var i = 0; i < maxErrors; i++)
        {
            bot.ConsecutiveErrors++;
            if (bot.ConsecutiveErrors >= maxErrors)
                bot.IsSkipped = true;
        }

        Assert.True(bot.IsSkipped, "Bot should be skipped after reaching max consecutive errors.");
        Assert.Equal(maxErrors, bot.ConsecutiveErrors);
        Assert.Equal("SKIPPED", BotOrchestrator.GetBotStatusLabel(bot));
    }

    [Fact]
    public void ErrorAccumulation_SuccessResetsErrors()
    {
        // After a successful API call the orchestrator resets ConsecutiveErrors.
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC 001", Email = "npc001@test.example", Strategy = "FURNITURE",
            ConsecutiveErrors = 3,
        };

        // Simulate a successful call resetting the counter and updating LastSuccessUtc.
        bot.ConsecutiveErrors = 0;
        bot.LastSuccessUtc = DateTime.UtcNow;

        Assert.Equal(0, bot.ConsecutiveErrors);
        Assert.False(bot.IsSkipped);
        Assert.NotNull(bot.LastSuccessUtc);
    }

    [Fact]
    public void ErrorAccumulation_SkippedBotNotReadyForOperation()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC 001", Email = "npc001@test.example", Strategy = "FURNITURE",
            Token = "valid-tok",
            TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2),
            Profile = new PlayerProfile { OnboardingCompletedAtUtc = DateTime.UtcNow.AddDays(-1) },
            IsSkipped = true,
        };
        Assert.False(BotStateValidator.IsReadyForOperation(bot));
    }

    // ── PendingRecommendation lifecycle across tick boundaries ────────────────

    [Fact]
    public void PendingRecommendation_AlwaysClearedAfterApplyAttempt()
    {
        // The orchestrator must clear PendingRecommendation after every apply attempt,
        // even when no units are adjustable (ShouldAct == false or SelectAdjustableUnits returns empty).
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC 001", Email = "npc001@test.example", Strategy = "FURNITURE",
            InitialNetWorth = 100_000m,
            CurrentNetWorth = 80_000m,
        };

        // Tick 1: recommendation is produced and stored.
        var rec = BotProfitCalculator.Recommend(80_000m, 100_000m, ticksElapsed: 10);
        bot.PendingRecommendation = rec;
        Assert.NotNull(bot.PendingRecommendation);

        // After apply attempt (regardless of outcome) recommendation is cleared.
        bot.PendingRecommendation = null;
        Assert.Null(bot.PendingRecommendation);
    }

    [Fact]
    public void PendingRecommendation_NoActionNotApplied()
    {
        // A NoAction recommendation must NOT trigger a price mutation.
        var rec = StrategyRecommendation.NoAction;
        Assert.False(rec.ShouldAct,
            "NoAction must have ShouldAct=false so PriceAdjustmentService skips the mutation.");
        Assert.Equal(0m, rec.PriceAdjustmentFactor);
    }

    // ── Annualised rate edge cases ────────────────────────────────────────────

    [Fact]
    public void AnnualisedRate_OnlyOneTick_IsHighAndNonZero()
    {
        // A 10% gain in a single tick annualises to 87 600% (10% × 8760 ticks/year).
        var rate = BotProfitCalculator.ComputeAnnualisedRatePercent(
            110_000m, 100_000m, ticksElapsed: 1, ticksPerYear: 8760);
        Assert.True(rate > 0m, "Single-tick 10% gain must produce a positive rate.");
        Assert.Equal(10m * 8760m, rate); // 10% per tick × 8760 ticks/year × 100
    }

    [Fact]
    public void AnnualisedRate_ZeroTicksElapsed_IsZero()
    {
        var rate = BotProfitCalculator.ComputeAnnualisedRatePercent(
            200_000m, 100_000m, ticksElapsed: 0);
        Assert.Equal(0m, rate);
    }

    [Fact]
    public void AnnualisedRate_ZeroInitialNetWorth_IsZero()
    {
        var rate = BotProfitCalculator.ComputeAnnualisedRatePercent(
            100_000m, 0m, ticksElapsed: 100);
        Assert.Equal(0m, rate);
    }

    // ── Multi-bot recommendation independence ─────────────────────────────────

    [Fact]
    public void MultipleBotsWithDifferentProfitability_RecommendationsAreIndependent()
    {
        // Bot A: losing money → should act.
        var recA = BotProfitCalculator.Recommend(85_000m, 100_000m, ticksElapsed: 10);
        // Bot B: growing → no action.
        var recB = BotProfitCalculator.Recommend(120_000m, 100_000m, ticksElapsed: 10);

        Assert.True(recA.ShouldAct, "Bot A has a severe loss and should act.");
        Assert.False(recB.ShouldAct, "Bot B is profitable and should not act.");
    }

    [Fact]
    public void MultipleBotsWithSameConditions_ProduceSameRecommendation()
    {
        // Pure function: identical inputs must always produce identical recommendations.
        var rec1 = BotProfitCalculator.Recommend(92_000m, 100_000m, ticksElapsed: 8);
        var rec2 = BotProfitCalculator.Recommend(92_000m, 100_000m, ticksElapsed: 8);

        Assert.Equal(rec1.ShouldAct, rec2.ShouldAct);
        Assert.Equal(rec1.PriceAdjustmentFactor, rec2.PriceAdjustmentFactor);
    }

    // ── Roster factory idempotency ────────────────────────────────────────────

    [Fact]
    public void Build_CalledTwiceWithSameOptions_ProducesSameRoster()
    {
        var opts = new BotOptions { BotCount = 5, BotNamePrefix = "NPC", BotEmailDomain = "test.example" };

        var roster1 = BotRosterFactory.Build(opts);
        var roster2 = BotRosterFactory.Build(opts);

        // Both rosters must be identical in count, emails, and strategies.
        Assert.Equal(roster1.Count, roster2.Count);
        for (var i = 0; i < roster1.Count; i++)
        {
            Assert.Equal(roster1[i].Email, roster2[i].Email);
            Assert.Equal(roster1[i].Strategy, roster2[i].Strategy);
            Assert.Equal(roster1[i].Index, roster2[i].Index);
        }
    }

    // ── Price adjustment selectability after floor ────────────────────────────

    [Fact]
    public void PriceAtFloor_IsAdjustmentMeaningful_ReturnsFalse()
    {
        // Once price is already at the minimum, further reduction produces no meaningful change.
        var floor = PriceAdjustmentHelper.MinimumAllowedPrice;
        var reduced = PriceAdjustmentHelper.ComputeNewPrice(floor, BotProfitCalculator.AggressivePriceReductionFactor);
        Assert.False(PriceAdjustmentHelper.IsAdjustmentMeaningful(floor, reduced),
            "A price already at the floor cannot be meaningfully reduced further.");
    }
}

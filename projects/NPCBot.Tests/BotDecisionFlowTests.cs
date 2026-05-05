using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// End-to-end decision-flow tests that exercise the complete pipeline:
///   BotProfitCalculator.Recommend → StrategyRecommendation → PriceAdjustmentHelper.ComputeNewPrice
///
/// These tests prove that all components work together correctly from a profitability
/// signal all the way through to a specific adjusted price value.
/// </summary>
public sealed class BotDecisionFlowTests
{
    // ── Mild-loss pipeline ────────────────────────────────────────────────────

    [Fact]
    public void MildLoss_ProducesCorrectPriceReduction()
    {
        // −5 % loss (beyond ±2 % neutral band) → mild reduction factor 0.95
        var recommendation = BotProfitCalculator.Recommend(
            currentNetWorth: 95_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: 10,
            minTicksBeforeAdjustment: 5);

        Assert.True(recommendation.ShouldAct);
        Assert.Equal(BotProfitCalculator.MildPriceReductionFactor, recommendation.PriceAdjustmentFactor);

        // Apply the factor to a $100 unit price → should reduce to $95.00
        var newPrice = PriceAdjustmentHelper.ComputeNewPrice(100m, recommendation.PriceAdjustmentFactor);
        Assert.Equal(95.00m, newPrice);
    }

    [Fact]
    public void SevereLoss_ProducesAggressivePriceReduction()
    {
        // −15 % loss (≤ −10 % threshold) → aggressive factor 0.85
        var recommendation = BotProfitCalculator.Recommend(
            currentNetWorth: 85_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: 10,
            minTicksBeforeAdjustment: 5);

        Assert.True(recommendation.ShouldAct);
        Assert.Equal(BotProfitCalculator.AggressivePriceReductionFactor, recommendation.PriceAdjustmentFactor);

        // Apply the factor to a $100 unit price → should reduce to $85.00
        var newPrice = PriceAdjustmentHelper.ComputeNewPrice(100m, recommendation.PriceAdjustmentFactor);
        Assert.Equal(85.00m, newPrice);
    }

    [Fact]
    public void Profitable_ProducesNoAction_PriceUnchanged()
    {
        // +5 % growth → no action
        var recommendation = BotProfitCalculator.Recommend(
            currentNetWorth: 105_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: 10);

        Assert.False(recommendation.ShouldAct);

        // Even if ComputeNewPrice were called with factor 1.0, price is unchanged
        var newPrice = PriceAdjustmentHelper.ComputeNewPrice(100m, 1.0m);
        Assert.Equal(100.00m, newPrice);
    }

    [Fact]
    public void Neutral_ProducesNoAction_PriceUnchanged()
    {
        // +1 % growth within ±2 % band → neutral → no action
        var recommendation = BotProfitCalculator.Recommend(
            currentNetWorth: 101_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: 10);

        Assert.False(recommendation.ShouldAct);
    }

    // ── Early-ticks guard ─────────────────────────────────────────────────────

    [Fact]
    public void EarlyTicks_Unprofitable_NoActionUntilMinTicksReached()
    {
        // −20 % loss but only 4 ticks (min is 5) → no action despite severity
        var recommendation = BotProfitCalculator.Recommend(
            currentNetWorth: 80_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: 4,
            minTicksBeforeAdjustment: 5);

        Assert.False(recommendation.ShouldAct,
            "Bot should not act until MinTicksBeforeAdjustment ticks have elapsed.");
    }

    [Fact]
    public void ExactlyAtMinTicks_Unprofitable_ProducesRecommendation()
    {
        // Exactly at the minimum tick threshold → should produce a recommendation
        var recommendation = BotProfitCalculator.Recommend(
            currentNetWorth: 90_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: 5,
            minTicksBeforeAdjustment: 5);

        Assert.True(recommendation.ShouldAct,
            "Bot at exactly MinTicksBeforeAdjustment with a loss should receive a recommendation.");
    }

    // ── Price floor ───────────────────────────────────────────────────────────

    [Fact]
    public void SevereLoss_VeryLowCurrentPrice_PriceClampedToFloor()
    {
        // Aggressive −15 % on a tiny price that would go sub-cent
        var recommendation = BotProfitCalculator.Recommend(
            currentNetWorth: 80_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: 10);

        Assert.True(recommendation.ShouldAct);

        // 0.001 × 0.85 = 0.00085 → rounds to 0.00 → clamped to MinimumAllowedPrice (0.01)
        var newPrice = PriceAdjustmentHelper.ComputeNewPrice(0.001m, recommendation.PriceAdjustmentFactor);
        Assert.Equal(PriceAdjustmentHelper.MinimumAllowedPrice, newPrice);
    }

    [Fact]
    public void MildLoss_PriceAlreadyAtFloor_NewPriceStaysAtFloor()
    {
        // Current price already at the floor — mild factor cannot reduce further
        var newPrice = PriceAdjustmentHelper.ComputeNewPrice(
            PriceAdjustmentHelper.MinimumAllowedPrice,
            BotProfitCalculator.MildPriceReductionFactor);

        Assert.Equal(PriceAdjustmentHelper.MinimumAllowedPrice, newPrice);
    }

    // ── Adjustment meaningfulness ─────────────────────────────────────────────

    [Fact]
    public void MildLoss_HighPriceUnit_AdjustmentIsMeaningful()
    {
        // $200 × 0.95 = $190 — a $10 difference is always meaningful
        var currentPrice = 200m;
        var factor = BotProfitCalculator.MildPriceReductionFactor;
        var newPrice = PriceAdjustmentHelper.ComputeNewPrice(currentPrice, factor);

        Assert.True(PriceAdjustmentHelper.IsAdjustmentMeaningful(currentPrice, newPrice));
    }

    [Fact]
    public void SevereLoss_VeryHighPriceUnit_AdjustmentIsMeaningful()
    {
        // $50 000 × 0.85 = $42 500 — difference is $7 500, clearly meaningful
        var currentPrice = 50_000m;
        var factor = BotProfitCalculator.AggressivePriceReductionFactor;
        var newPrice = PriceAdjustmentHelper.ComputeNewPrice(currentPrice, factor);

        Assert.True(PriceAdjustmentHelper.IsAdjustmentMeaningful(currentPrice, newPrice));
    }

    // ── Multi-company / multi-unit selection ──────────────────────────────────

    [Fact]
    public void MultiCompanyAndBuildings_AllPublicSalesUnitsSelected()
    {
        // Three PUBLIC_SALES units across two companies should all be selected for adjustment.
        var companies = new List<CompanySummary>
        {
            new()
            {
                Id = "c1", Name = "Furniture Co",
                Buildings =
                [
                    new()
                    {
                        Id = "b1", Name = "Chair Shop",
                        Units = [new() { Id = "u1", UnitType = "PUBLIC_SALES", MinPrice = 50m }],
                    },
                    new()
                    {
                        Id = "b2", Name = "Table Shop",
                        Units = [new() { Id = "u2", UnitType = "PUBLIC_SALES", MinPrice = 80m }],
                    },
                ],
            },
            new()
            {
                Id = "c2", Name = "Food Co",
                Buildings =
                [
                    new()
                    {
                        Id = "b3", Name = "Bread Shop",
                        Units = [new() { Id = "u3", UnitType = "PUBLIC_SALES", MinPrice = 5m }],
                    },
                ],
            },
        };

        var adjustable = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();
        Assert.Equal(3, adjustable.Count);
    }

    [Fact]
    public void MultiCompanyAndBuildings_MildLoss_AllNewPricesReducedByFivePercent()
    {
        // Three units at different prices; all should have mild factor applied.
        var prices = new[] { 50m, 80m, 5m };
        var factor = BotProfitCalculator.MildPriceReductionFactor; // 0.95

        foreach (var price in prices)
        {
            var newPrice = PriceAdjustmentHelper.ComputeNewPrice(price, factor);
            Assert.Equal(Math.Round(price * factor, 2, MidpointRounding.AwayFromZero), newPrice);
        }
    }

    // ── Classify + Recommend coherence ───────────────────────────────────────

    [Fact]
    public void Classify_Unprofitable_Recommend_ShouldAct_AreCoherent()
    {
        // When Classify says Unprofitable, Recommend should say ShouldAct.
        var current = 95_000m;
        var initial = 100_000m;
        long ticks = 10;

        var status = BotProfitCalculator.Classify(current, initial);
        var recommendation = BotProfitCalculator.Recommend(current, initial, ticks);

        Assert.Equal(ProfitabilityStatus.Unprofitable, status);
        Assert.True(recommendation.ShouldAct);
    }

    [Fact]
    public void Classify_Profitable_Recommend_NoAction_AreCoherent()
    {
        var current = 105_000m;
        var initial = 100_000m;
        long ticks = 10;

        var status = BotProfitCalculator.Classify(current, initial);
        var recommendation = BotProfitCalculator.Recommend(current, initial, ticks);

        Assert.Equal(ProfitabilityStatus.Profitable, status);
        Assert.False(recommendation.ShouldAct);
    }

    [Fact]
    public void Classify_Neutral_Recommend_NoAction_AreCoherent()
    {
        // Within ±2 % band
        var current = 101_000m;
        var initial = 100_000m;
        long ticks = 10;

        var status = BotProfitCalculator.Classify(current, initial);
        var recommendation = BotProfitCalculator.Recommend(current, initial, ticks);

        Assert.Equal(ProfitabilityStatus.Neutral, status);
        Assert.False(recommendation.ShouldAct);
    }

    // ── Severe vs mild threshold boundary ────────────────────────────────────

    [Fact]
    public void JustBelowSevereThreshold_ProducesMildNotAggressiveReduction()
    {
        // −9.9 % — just above the severe −10 % threshold → mild action
        var recommendation = BotProfitCalculator.Recommend(
            currentNetWorth: 90_100m,
            initialNetWorth: 100_000m,
            ticksElapsed: 10);

        Assert.True(recommendation.ShouldAct);
        Assert.Equal(BotProfitCalculator.MildPriceReductionFactor, recommendation.PriceAdjustmentFactor);
    }

    [Fact]
    public void ExactlyAtSevereThreshold_ProducesAggressiveReduction()
    {
        // −10.0 % exactly at the severe threshold → aggressive action
        var recommendation = BotProfitCalculator.Recommend(
            currentNetWorth: 90_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: 10);

        Assert.True(recommendation.ShouldAct);
        Assert.Equal(BotProfitCalculator.AggressivePriceReductionFactor, recommendation.PriceAdjustmentFactor);
    }

    // ── Bot account PendingRecommendation lifecycle ───────────────────────────

    [Fact]
    public void PendingRecommendation_SetThenCleared_LifecycleIsCorrect()
    {
        var bot = new BotAccount { Index = 1, DisplayName = "Bot", Email = "b@b.com", Strategy = "S" };

        Assert.Null(bot.PendingRecommendation);

        // Simulate orchestrator setting the recommendation
        var recommendation = BotProfitCalculator.Recommend(95_000m, 100_000m, 10);
        bot.PendingRecommendation = recommendation;

        Assert.NotNull(bot.PendingRecommendation);
        Assert.True(bot.PendingRecommendation.ShouldAct);

        // Simulate orchestrator clearing it after apply attempt
        bot.PendingRecommendation = null;
        Assert.Null(bot.PendingRecommendation);
    }

    [Fact]
    public void PendingRecommendation_NoActionSentinel_CanBeAssignedAndCleared()
    {
        var bot = new BotAccount { Index = 1, DisplayName = "Bot", Email = "b@b.com", Strategy = "S" };

        // Even a NoAction recommendation can be stored (orchestrator always sets it)
        bot.PendingRecommendation = StrategyRecommendation.NoAction;
        Assert.NotNull(bot.PendingRecommendation);
        Assert.False(bot.PendingRecommendation.ShouldAct);

        bot.PendingRecommendation = null;
        Assert.Null(bot.PendingRecommendation);
    }
}

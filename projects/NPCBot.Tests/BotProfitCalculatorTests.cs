using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Unit tests for <see cref="BotProfitCalculator"/> — pure functions with no I/O.
/// </summary>
public sealed class BotProfitCalculatorTests
{
    // ── ComputeNetWorth ───────────────────────────────────────────────────────

    [Fact]
    public void ComputeNetWorth_EmptyCompanies_ReturnsZero()
    {
        var profile = new PlayerProfile();
        Assert.Equal(0m, BotProfitCalculator.ComputeNetWorth(profile));
    }

    [Fact]
    public void ComputeNetWorth_SingleCompany_ReturnsCash()
    {
        var profile = new PlayerProfile
        {
            Companies = [new CompanySummary { Cash = 500_000m }],
        };
        Assert.Equal(500_000m, BotProfitCalculator.ComputeNetWorth(profile));
    }

    [Fact]
    public void ComputeNetWorth_MultipleCompanies_SumsAllCash()
    {
        var profile = new PlayerProfile
        {
            Companies =
            [
                new CompanySummary { Cash = 100_000m },
                new CompanySummary { Cash = 250_000m },
                new CompanySummary { Cash = 75_000m },
            ],
        };
        Assert.Equal(425_000m, BotProfitCalculator.ComputeNetWorth(profile));
    }

    [Fact]
    public void ComputeNetWorth_NegativeCash_SumsCorrectly()
    {
        var profile = new PlayerProfile
        {
            Companies =
            [
                new CompanySummary { Cash = 200_000m },
                new CompanySummary { Cash = -50_000m },
            ],
        };
        Assert.Equal(150_000m, BotProfitCalculator.ComputeNetWorth(profile));
    }

    // ── Classify ─────────────────────────────────────────────────────────────

    [Fact]
    public void Classify_ZeroInitialNetWorth_ReturnsUnknown()
    {
        Assert.Equal(ProfitabilityStatus.Unknown, BotProfitCalculator.Classify(10m, 0m));
    }

    [Fact]
    public void Classify_GrowthBeyondNeutralBand_ReturnsProfitable()
    {
        // 3% growth > 2% neutral band → Profitable
        Assert.Equal(ProfitabilityStatus.Profitable,
            BotProfitCalculator.Classify(103_000m, 100_000m));
    }

    [Fact]
    public void Classify_ExactlyOnNeutralBandPositive_ReturnsNeutral()
    {
        // 2% growth is exactly at the boundary → Neutral (not strictly greater)
        Assert.Equal(ProfitabilityStatus.Neutral,
            BotProfitCalculator.Classify(102_000m, 100_000m));
    }

    [Fact]
    public void Classify_WithinNeutralBand_ReturnsNeutral()
    {
        // 1% growth is within ±2% band → Neutral
        Assert.Equal(ProfitabilityStatus.Neutral,
            BotProfitCalculator.Classify(101_000m, 100_000m));
    }

    [Fact]
    public void Classify_ExactlyOnNeutralBandNegative_ReturnsNeutral()
    {
        // −2% loss is exactly at the lower boundary → Neutral
        Assert.Equal(ProfitabilityStatus.Neutral,
            BotProfitCalculator.Classify(98_000m, 100_000m));
    }

    [Fact]
    public void Classify_LossBeyondNeutralBand_ReturnsUnprofitable()
    {
        // −3% loss > 2% neutral band → Unprofitable
        Assert.Equal(ProfitabilityStatus.Unprofitable,
            BotProfitCalculator.Classify(97_000m, 100_000m));
    }

    [Fact]
    public void Classify_ZeroNetWorthCurrentAndInitial_ReturnsUnknown()
    {
        Assert.Equal(ProfitabilityStatus.Unknown, BotProfitCalculator.Classify(0m, 0m));
    }

    [Fact]
    public void Classify_NegativeInitialNetWorth_HandlesCorrectly()
    {
        // InitialNetWorth = −10 000, current = −8 000 → delta = +2 000.
        // Abs(−10 000) = 10 000 so deltaPercent = 0.2 → Profitable.
        Assert.Equal(ProfitabilityStatus.Profitable,
            BotProfitCalculator.Classify(-8_000m, -10_000m));
    }

    // ── ComputeAnnualisedRatePercent ──────────────────────────────────────────

    [Fact]
    public void ComputeAnnualisedRatePercent_ZeroTicksElapsed_ReturnsZero()
    {
        Assert.Equal(0m, BotProfitCalculator.ComputeAnnualisedRatePercent(110_000m, 100_000m, 0));
    }

    [Fact]
    public void ComputeAnnualisedRatePercent_ZeroInitialNetWorth_ReturnsZero()
    {
        Assert.Equal(0m, BotProfitCalculator.ComputeAnnualisedRatePercent(110_000m, 0m, 100));
    }

    [Fact]
    public void ComputeAnnualisedRatePercent_FullYearAt10PercentGrowth_Returns10()
    {
        // 10% growth over one full year (8760 ticks) → 10%/yr
        var rate = BotProfitCalculator.ComputeAnnualisedRatePercent(
            110_000m, 100_000m, 8760, ticksPerYear: 8760);
        Assert.Equal(10m, Math.Round(rate, 2));
    }

    [Fact]
    public void ComputeAnnualisedRatePercent_HalfYearAt10PercentGrowth_Returns20()
    {
        // 10% growth in half a year → annualised 20%/yr
        var rate = BotProfitCalculator.ComputeAnnualisedRatePercent(
            110_000m, 100_000m, 4380, ticksPerYear: 8760);
        Assert.Equal(20m, Math.Round(rate, 2));
    }

    [Fact]
    public void ComputeAnnualisedRatePercent_NegativeGrowth_ReturnsNegative()
    {
        var rate = BotProfitCalculator.ComputeAnnualisedRatePercent(
            90_000m, 100_000m, 8760, ticksPerYear: 8760);
        Assert.True(rate < 0m, "Loss should yield a negative rate.");
    }

    // ── Recommend ────────────────────────────────────────────────────────────

    [Fact]
    public void Recommend_BeforeMinTicks_ReturnsNoAction()
    {
        // Only 3 ticks elapsed, threshold is 5 → no action
        var rec = BotProfitCalculator.Recommend(50_000m, 100_000m, ticksElapsed: 3, minTicksBeforeAdjustment: 5);
        Assert.False(rec.ShouldAct);
    }

    [Fact]
    public void Recommend_ZeroInitialNetWorth_ReturnsNoAction()
    {
        var rec = BotProfitCalculator.Recommend(100_000m, 0m, ticksElapsed: 10);
        Assert.False(rec.ShouldAct);
    }

    [Fact]
    public void Recommend_Profitable_ReturnsNoAction()
    {
        // 5% growth — no action needed
        var rec = BotProfitCalculator.Recommend(105_000m, 100_000m, ticksElapsed: 10);
        Assert.False(rec.ShouldAct);
    }

    [Fact]
    public void Recommend_Neutral_ReturnsNoAction()
    {
        // 1% growth — within neutral band
        var rec = BotProfitCalculator.Recommend(101_000m, 100_000m, ticksElapsed: 10);
        Assert.False(rec.ShouldAct);
    }

    [Fact]
    public void Recommend_MildLoss_ReturnsMildPriceReduction()
    {
        // −5% loss — mild reduction
        var rec = BotProfitCalculator.Recommend(95_000m, 100_000m, ticksElapsed: 10);
        Assert.True(rec.ShouldAct);
        Assert.Equal(BotProfitCalculator.MildPriceReductionFactor, rec.PriceAdjustmentFactor);
    }

    [Fact]
    public void Recommend_SevereLoss_ReturnsAggressivePriceReduction()
    {
        // −15% loss — well beyond the severe threshold of −10%
        var rec = BotProfitCalculator.Recommend(85_000m, 100_000m, ticksElapsed: 10);
        Assert.True(rec.ShouldAct);
        Assert.Equal(BotProfitCalculator.AggressivePriceReductionFactor, rec.PriceAdjustmentFactor);
    }

    [Fact]
    public void Recommend_ExactlyAtSevereThreshold_ReturnsAggressiveAction()
    {
        // Exactly −10% → severe threshold is inclusive (<=)
        var rec = BotProfitCalculator.Recommend(90_000m, 100_000m, ticksElapsed: 10);
        Assert.True(rec.ShouldAct);
        Assert.Equal(BotProfitCalculator.AggressivePriceReductionFactor, rec.PriceAdjustmentFactor);
    }

    [Fact]
    public void Recommend_NoActionResult_HasZeroPriceAdjustmentFactor()
    {
        Assert.Equal(0m, StrategyRecommendation.NoAction.PriceAdjustmentFactor);
    }

    [Fact]
    public void Recommend_ActionResult_HasNonEmptyReason()
    {
        var rec = BotProfitCalculator.Recommend(85_000m, 100_000m, ticksElapsed: 10);
        Assert.False(string.IsNullOrWhiteSpace(rec.Reason));
    }

    // ── Constants sanity ──────────────────────────────────────────────────────

    [Fact]
    public void MildPriceReductionFactor_IsLessThanOne()
    {
        Assert.True(BotProfitCalculator.MildPriceReductionFactor < 1m);
        Assert.True(BotProfitCalculator.MildPriceReductionFactor > 0m);
    }

    [Fact]
    public void AggressivePriceReductionFactor_IsLessThanMild()
    {
        Assert.True(BotProfitCalculator.AggressivePriceReductionFactor
            < BotProfitCalculator.MildPriceReductionFactor);
    }

    [Fact]
    public void NeutralBandPercent_IsPositiveSmallFraction()
    {
        Assert.True(BotProfitCalculator.NeutralBandPercent > 0m);
        Assert.True(BotProfitCalculator.NeutralBandPercent < 0.10m);
    }

    // ── Recommend boundary cases ──────────────────────────────────────────────

    [Fact]
    public void Recommend_OneTickBelowMinimum_ReturnsNoAction()
    {
        // ticksElapsed = minTicksBeforeAdjustment - 1 must still return NoAction.
        var rec = BotProfitCalculator.Recommend(
            50_000m, 100_000m,
            ticksElapsed: 4, minTicksBeforeAdjustment: 5);
        Assert.False(rec.ShouldAct);
    }

    [Fact]
    public void Recommend_ExactlyAtMinTicksBoundary_EvaluatesNormally()
    {
        // ticksElapsed == minTicksBeforeAdjustment should NOT be blocked by the guard.
        // With a −50% loss at exactly the threshold, an action must be recommended.
        var rec = BotProfitCalculator.Recommend(
            50_000m, 100_000m,
            ticksElapsed: 5, minTicksBeforeAdjustment: 5);
        Assert.True(rec.ShouldAct);
    }

    [Fact]
    public void Recommend_LargePositiveGrowth_ReturnsNoAction()
    {
        // Even with 200% growth, no corrective action is needed.
        var rec = BotProfitCalculator.Recommend(300_000m, 100_000m, ticksElapsed: 100);
        Assert.False(rec.ShouldAct);
    }

    [Fact]
    public void Recommend_ExactlyAtNeutralBandNegative_ReturnsNoAction()
    {
        // Exactly −2% (the band boundary itself) → Neutral → no action.
        var rec = BotProfitCalculator.Recommend(98_000m, 100_000m, ticksElapsed: 10);
        Assert.False(rec.ShouldAct);
    }

    [Fact]
    public void Recommend_JustBeyondNeutralBand_ReturnsMildAction()
    {
        // −3% is beyond the ±2% neutral band but above the −10% severe threshold.
        var rec = BotProfitCalculator.Recommend(97_000m, 100_000m, ticksElapsed: 10);
        Assert.True(rec.ShouldAct);
        Assert.Equal(BotProfitCalculator.MildPriceReductionFactor, rec.PriceAdjustmentFactor);
    }
}

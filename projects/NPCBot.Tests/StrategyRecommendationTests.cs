using Capitalism.NPCBot.Models;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Unit tests for <see cref="StrategyRecommendation"/> model and the
/// <see cref="StrategyRecommendation.NoAction"/> sentinel.
/// </summary>
public sealed class StrategyRecommendationTests
{
    // ── NoAction sentinel ─────────────────────────────────────────────────────

    [Fact]
    public void NoAction_ShouldActIsFalse()
    {
        Assert.False(StrategyRecommendation.NoAction.ShouldAct);
    }

    [Fact]
    public void NoAction_PriceAdjustmentFactorIsZero()
    {
        Assert.Equal(0m, StrategyRecommendation.NoAction.PriceAdjustmentFactor);
    }

    [Fact]
    public void NoAction_ReasonIsNonEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(StrategyRecommendation.NoAction.Reason));
    }

    [Fact]
    public void NoAction_IsSameInstanceOnMultipleAccesses()
    {
        // The sentinel is a static property — both accesses should return the same object.
        Assert.Same(StrategyRecommendation.NoAction, StrategyRecommendation.NoAction);
    }

    // ── Custom recommendation ─────────────────────────────────────────────────

    [Fact]
    public void CustomRecommendation_ShouldActIsTrue()
    {
        var rec = new StrategyRecommendation
        {
            ShouldAct = true,
            Reason = "Test reason.",
            PriceAdjustmentFactor = 0.95m,
        };
        Assert.True(rec.ShouldAct);
    }

    [Fact]
    public void CustomRecommendation_PriceAdjustmentFactorStored()
    {
        var rec = new StrategyRecommendation { PriceAdjustmentFactor = 0.85m };
        Assert.Equal(0.85m, rec.PriceAdjustmentFactor);
    }

    [Fact]
    public void CustomRecommendation_ReasonIsStored()
    {
        const string reason = "Severe loss — cut prices aggressively.";
        var rec = new StrategyRecommendation { Reason = reason };
        Assert.Equal(reason, rec.Reason);
    }

    [Fact]
    public void DefaultRecommendation_ShouldActIsFalse()
    {
        // A default-constructed instance should be safe (no action).
        var rec = new StrategyRecommendation();
        Assert.False(rec.ShouldAct);
    }

    [Fact]
    public void DefaultRecommendation_PriceAdjustmentFactorIsZero()
    {
        var rec = new StrategyRecommendation();
        Assert.Equal(0m, rec.PriceAdjustmentFactor);
    }
}

using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Scenario tests that model multi-tick economic progressions for NPC bots:
/// mild-loss escalation to aggressive action, recovery from losses, multi-bot
/// independence, and recommendation lifecycle across several poll cycles.
///
/// These tests use only pure helpers (no I/O) and are deterministic.
/// </summary>
public sealed class BotTickProgressionTests
{
    // ── Multi-tick loss escalation ────────────────────────────────────────────

    [Fact]
    public void MildLossThenSevereLoss_RecommendationEscalatesFromMildToAggressive()
    {
        // Tick 5: −5% loss → mild recommendation
        var mildRec = BotProfitCalculator.Recommend(
            currentNetWorth: 95_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: 5);

        Assert.True(mildRec.ShouldAct);
        Assert.Equal(BotProfitCalculator.MildPriceReductionFactor, mildRec.PriceAdjustmentFactor);

        // Tick 15: −15% loss → aggressive recommendation
        var aggressiveRec = BotProfitCalculator.Recommend(
            currentNetWorth: 85_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: 15);

        Assert.True(aggressiveRec.ShouldAct);
        Assert.Equal(BotProfitCalculator.AggressivePriceReductionFactor, aggressiveRec.PriceAdjustmentFactor);

        // The aggressive factor is strictly more severe (smaller multiplier)
        Assert.True(aggressiveRec.PriceAdjustmentFactor < mildRec.PriceAdjustmentFactor,
            "Aggressive reduction must be more severe than mild reduction.");
    }

    [Fact]
    public void SevereLossThenRecovery_RecommendationBecomesNoAction()
    {
        // Tick 6: −15% loss → aggressive action
        var actionRec = BotProfitCalculator.Recommend(
            currentNetWorth: 85_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: 6);
        Assert.True(actionRec.ShouldAct);

        // Tick 20: bot has recovered above neutral band (+5%) → no action needed
        var noActionRec = BotProfitCalculator.Recommend(
            currentNetWorth: 105_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: 20);
        Assert.False(noActionRec.ShouldAct, "Profitable bot must not receive a price-reduction recommendation.");
    }

    [Fact]
    public void NetWorthDelta_TracksAccuratelyAcrossMultipleUpdates()
    {
        // Simulates BotAccount fields being updated across ticks.
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC_Retail_01",
            Email = "npc_retail_01@npcbot.capitalism.local",
            Strategy = "Retail",
            InitialNetWorth = 100_000m,
        };

        // Tick 5: slight decline
        bot.CurrentNetWorth = 98_000m;
        Assert.Equal(-2_000m, bot.ProfitDelta);
        Assert.Equal(ProfitabilityStatus.Neutral, BotProfitCalculator.Classify(bot.CurrentNetWorth, bot.InitialNetWorth));

        // Tick 10: deeper loss
        bot.CurrentNetWorth = 90_000m;
        Assert.Equal(-10_000m, bot.ProfitDelta);
        Assert.Equal(ProfitabilityStatus.Unprofitable, BotProfitCalculator.Classify(bot.CurrentNetWorth, bot.InitialNetWorth));

        // Tick 20: recovered to profitable
        bot.CurrentNetWorth = 115_000m;
        Assert.Equal(15_000m, bot.ProfitDelta);
        Assert.Equal(ProfitabilityStatus.Profitable, BotProfitCalculator.Classify(bot.CurrentNetWorth, bot.InitialNetWorth));
    }

    // ── Multi-bot independence ────────────────────────────────────────────────

    [Fact]
    public void ThreeBots_DifferentEconomicStates_IndependentClassifications()
    {
        // Bot A: profitable
        var botA = new BotAccount
        {
            Index = 1, DisplayName = "NPC_Trading_01",
            Email = "npc_trading_01@npcbot.capitalism.local",
            Strategy = "Trading",
            InitialNetWorth = 100_000m, CurrentNetWorth = 110_000m, TrackingStartTick = 0,
        };

        // Bot B: neutral
        var botB = new BotAccount
        {
            Index = 2, DisplayName = "NPC_Industrial_02",
            Email = "npc_industrial_02@npcbot.capitalism.local",
            Strategy = "Industrial",
            InitialNetWorth = 100_000m, CurrentNetWorth = 101_000m, TrackingStartTick = 0,
        };

        // Bot C: unprofitable
        var botC = new BotAccount
        {
            Index = 3, DisplayName = "NPC_Retail_03",
            Email = "npc_retail_03@npcbot.capitalism.local",
            Strategy = "Retail",
            InitialNetWorth = 100_000m, CurrentNetWorth = 88_000m, TrackingStartTick = 0,
        };

        var statusA = BotProfitCalculator.Classify(botA.CurrentNetWorth, botA.InitialNetWorth);
        var statusB = BotProfitCalculator.Classify(botB.CurrentNetWorth, botB.InitialNetWorth);
        var statusC = BotProfitCalculator.Classify(botC.CurrentNetWorth, botC.InitialNetWorth);

        Assert.Equal(ProfitabilityStatus.Profitable, statusA);
        Assert.Equal(ProfitabilityStatus.Neutral, statusB);
        Assert.Equal(ProfitabilityStatus.Unprofitable, statusC);

        // Only Bot C should receive a recommendation at tick 10
        var recA = BotOrchestrator.ComputeRecommendationForBot(botA, currentTick: 10);
        var recB = BotOrchestrator.ComputeRecommendationForBot(botB, currentTick: 10);
        var recC = BotOrchestrator.ComputeRecommendationForBot(botC, currentTick: 10);

        Assert.False(recA.ShouldAct, "Profitable bot must not receive action.");
        Assert.False(recB.ShouldAct, "Neutral bot must not receive action.");
        Assert.True(recC.ShouldAct, "Unprofitable bot must receive an action recommendation.");
    }

    [Fact]
    public void ThreeBots_OneSkipped_SkippedBotDoesNotInfluenceOthers()
    {
        var botSkipped = new BotAccount
        {
            Index = 1, DisplayName = "NPC_Aggressive_01",
            Email = "npc_aggressive_01@npcbot.capitalism.local",
            Strategy = "Aggressive",
            IsSkipped = true, ConsecutiveErrors = 5,
            InitialNetWorth = 100_000m, CurrentNetWorth = 50_000m,
        };

        var botActive1 = new BotAccount
        {
            Index = 2, DisplayName = "NPC_Trading_02",
            Email = "npc_trading_02@npcbot.capitalism.local",
            Strategy = "Trading",
            Token = "tok-2", TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2),
            Profile = new PlayerProfile { OnboardingCompletedAtUtc = DateTime.UtcNow.AddDays(-1) },
            InitialNetWorth = 100_000m, CurrentNetWorth = 98_000m,
        };

        var botActive2 = new BotAccount
        {
            Index = 3, DisplayName = "NPC_Mixed_03",
            Email = "npc_mixed_03@npcbot.capitalism.local",
            Strategy = "Mixed",
            Token = "tok-3", TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2),
            Profile = new PlayerProfile { OnboardingCompletedAtUtc = DateTime.UtcNow.AddDays(-1) },
            InitialNetWorth = 100_000m, CurrentNetWorth = 92_000m,
        };

        // Skipped bot must not be ready for operation
        Assert.False(BotStateValidator.IsReadyForOperation(botSkipped));

        // Active bots remain ready regardless of skipped bot
        Assert.True(BotStateValidator.IsReadyForOperation(botActive1));
        Assert.True(BotStateValidator.IsReadyForOperation(botActive2));

        // Status labels are independent
        Assert.Equal("SKIPPED", BotOrchestrator.GetBotStatusLabel(botSkipped));
        Assert.Equal("ACTIVE", BotOrchestrator.GetBotStatusLabel(botActive1));
        Assert.Equal("ACTIVE", BotOrchestrator.GetBotStatusLabel(botActive2));
    }

    // ── Recommendation reason messages ────────────────────────────────────────

    [Fact]
    public void MildLossRecommendation_ReasonContainsMildKeyword()
    {
        var rec = BotProfitCalculator.Recommend(95_000m, 100_000m, ticksElapsed: 10);
        Assert.True(rec.ShouldAct);
        Assert.NotEmpty(rec.Reason);
        // The reason message should indicate a mild (not severe) loss scenario.
        Assert.Contains("ild", rec.Reason, StringComparison.OrdinalIgnoreCase); // "mild" or "Mild"
    }

    [Fact]
    public void SevereLossRecommendation_ReasonContainsSevereKeyword()
    {
        var rec = BotProfitCalculator.Recommend(85_000m, 100_000m, ticksElapsed: 10);
        Assert.True(rec.ShouldAct);
        Assert.NotEmpty(rec.Reason);
        // The reason message should indicate a severe loss scenario.
        Assert.Contains("evere", rec.Reason, StringComparison.OrdinalIgnoreCase); // "severe" or "Severe"
    }

    [Fact]
    public void NoActionRecommendation_ReasonIsNonEmpty()
    {
        // The NoAction sentinel has a non-empty reason string that explains the status.
        Assert.NotEmpty(StrategyRecommendation.NoAction.Reason);
    }

    // ── Price computation determinism ─────────────────────────────────────────

    [Fact]
    public void ComputeNewPrice_SameInputAlwaysProducesSameOutput()
    {
        // Prices must be deterministic (no randomness).
        const decimal price = 49.99m;
        const decimal factor = BotProfitCalculator.MildPriceReductionFactor;

        var result1 = PriceAdjustmentHelper.ComputeNewPrice(price, factor);
        var result2 = PriceAdjustmentHelper.ComputeNewPrice(price, factor);
        var result3 = PriceAdjustmentHelper.ComputeNewPrice(price, factor);

        Assert.Equal(result1, result2);
        Assert.Equal(result1, result3);
    }

    [Fact]
    public void ComputeNewPrice_RepeatedApplications_EventuallyStabilizes()
    {
        // Applying the aggressive factor repeatedly must eventually produce a stable price
        // where no further meaningful adjustment is possible (price at floor or rounding fixed point).
        var price = 0.50m;
        const int maxIterations = 50;
        decimal prevPrice;

        for (var i = 0; i < maxIterations; i++)
        {
            prevPrice = price;
            price = PriceAdjustmentHelper.ComputeNewPrice(price, BotProfitCalculator.AggressivePriceReductionFactor);
            // Stabilization: price no longer changes meaningfully
            if (!PriceAdjustmentHelper.IsAdjustmentMeaningful(prevPrice, price))
                return; // stable — test passes
        }

        Assert.Fail("Price did not stabilize within 50 applications of the aggressive reduction factor.");
    }

    // ── BotOptions AllowedIndustries vs product selection ────────────────────

    [Fact]
    public void PickCheapestFreeProduct_FurnitureIndustry_ReturnsAffordableProduct()
    {
        // Simulates picking from a FURNITURE industry product list.
        var products = new List<ProductTypeSummary>
        {
            new() { Id = "p1", Slug = "wooden-chair", Name = "Wooden Chair",
                    Industry = "FURNITURE", BasePrice = 45m, IsProOnly = false },
            new() { Id = "p2", Slug = "wooden-table", Name = "Wooden Table",
                    Industry = "FURNITURE", BasePrice = 75m, IsProOnly = false },
        };

        var picked = OnboardingHelpers.PickCheapestFreeProduct(products);
        Assert.NotNull(picked);
        Assert.Equal("wooden-chair", picked.Slug);  // cheaper item picked
    }

    [Fact]
    public void PickCheapestFreeProduct_HealthcareIndustry_SkipsProOnlyAndPicksFree()
    {
        // Healthcare has both free and Pro-only products; bots should only pick free ones.
        var products = new List<ProductTypeSummary>
        {
            new() { Id = "p1", Slug = "advanced-medication", Name = "Advanced Medication",
                    Industry = "HEALTHCARE", BasePrice = 30m, IsProOnly = true },
            new() { Id = "p2", Slug = "basic-medicine", Name = "Basic Medicine",
                    Industry = "HEALTHCARE", BasePrice = 50m, IsProOnly = false },
            new() { Id = "p3", Slug = "bandages", Name = "Bandages",
                    Industry = "HEALTHCARE", BasePrice = 20m, IsProOnly = false },
        };

        var picked = OnboardingHelpers.PickCheapestFreeProduct(products);
        Assert.NotNull(picked);
        Assert.False(picked.IsProOnly, "Picked product must not be Pro-only.");
        Assert.Equal("bandages", picked.Slug);  // cheapest free product
    }

    [Fact]
    public void PickCheapestFreeProduct_FoodProcessingIndustry_PicksBread()
    {
        // Food Processing: Bread (base price 3) should be picked over Flour (higher).
        var products = new List<ProductTypeSummary>
        {
            new() { Id = "p1", Slug = "bread", Name = "Bread",
                    Industry = "FOOD_PROCESSING", BasePrice = 3m, IsProOnly = false },
            new() { Id = "p2", Slug = "flour", Name = "Flour",
                    Industry = "FOOD_PROCESSING", BasePrice = 5m, IsProOnly = false },
        };

        var picked = OnboardingHelpers.PickCheapestFreeProduct(products);
        Assert.NotNull(picked);
        Assert.Equal("bread", picked.Slug);
    }
}

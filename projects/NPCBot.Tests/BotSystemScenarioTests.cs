using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// End-to-end scenario tests that combine multiple NPC-bot components
/// (BotRosterFactory, BotProfitCalculator, BotStateValidator, PriceAdjustmentHelper)
/// without any HTTP dependency.
///
/// Each scenario represents a realistic phase of the NPC bot lifecycle and proves
/// that the components integrate correctly at a model level.
/// </summary>
public sealed class BotSystemScenarioTests
{
    private static BotOptions DefaultOptions() => new() { BotNamePrefix = "NPC" };

    // ── Phase 1: fresh bot just created from factory ──────────────────────────

    [Fact]
    public void FreshBotFromFactory_HasZeroInitialNetWorth()
    {
        var roster = BotRosterFactory.Build(DefaultOptions());
        Assert.All(roster, b => Assert.Equal(0m, b.InitialNetWorth));
    }

    [Fact]
    public void FreshBotFromFactory_ProfitabilityClassificationIsUnknown()
    {
        // A bot with InitialNetWorth=0 always returns Unknown — no tracking baseline yet.
        var roster = BotRosterFactory.Build(DefaultOptions());
        Assert.All(roster, b =>
        {
            var status = BotProfitCalculator.Classify(b.CurrentNetWorth, b.InitialNetWorth);
            Assert.Equal(ProfitabilityStatus.Unknown, status);
        });
    }

    [Fact]
    public void FreshBotFromFactory_RecommendationIsNoAction()
    {
        // Cannot recommend strategy change before baseline is established.
        var roster = BotRosterFactory.Build(DefaultOptions());
        Assert.All(roster, b =>
        {
            var rec = BotProfitCalculator.Recommend(b.CurrentNetWorth, b.InitialNetWorth, ticksElapsed: 100);
            Assert.False(rec.ShouldAct, "Fresh bot with zero initial NW should never trigger a price action.");
        });
    }

    [Fact]
    public void FreshBotFromFactory_IsNotReadyForOperation_NoToken()
    {
        // A brand-new bot has no token yet — the validator must report it as invalid.
        var roster = BotRosterFactory.Build(DefaultOptions());
        var bot = roster[0];
        Assert.Null(bot.Token);

        var result = BotStateValidator.Validate(bot);
        Assert.False(result.IsValid);
    }

    // ── Phase 2: bot has received a token and completed onboarding ────────────

    [Fact]
    public void OnboardedBotWithToken_IsReadyForOperation()
    {
        // Once the bot has a valid token and a completed onboarding timestamp, the
        // validator should report it as fully ready for the orchestration loop.
        var opts = DefaultOptions();
        var bot = BotRosterFactory.Build(opts)[0];
        bot.Token = "valid-jwt-token";
        bot.TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2);
        bot.Profile = new PlayerProfile
        {
            OnboardingCompletedAtUtc = DateTime.UtcNow.AddMinutes(-30),
        };

        var result = BotStateValidator.Validate(bot);
        Assert.True(result.IsValid, result.Summary);
    }

    // ── Phase 3: profitability tracking after baseline is set ─────────────────

    [Fact]
    public void BotWithNetWorthIncrease_GivesProfitableStatus()
    {
        var bot = BotRosterFactory.Build(DefaultOptions())[0];
        bot.InitialNetWorth = 500_000m;
        bot.CurrentNetWorth = 550_000m; // +10%

        var status = BotProfitCalculator.Classify(bot.CurrentNetWorth, bot.InitialNetWorth);
        Assert.Equal(ProfitabilityStatus.Profitable, status);
    }

    [Fact]
    public void BotWithNetWorthDecrease_GivesUnprofitableStatus()
    {
        var bot = BotRosterFactory.Build(DefaultOptions())[0];
        bot.InitialNetWorth = 500_000m;
        bot.CurrentNetWorth = 450_000m; // −10%

        var status = BotProfitCalculator.Classify(bot.CurrentNetWorth, bot.InitialNetWorth);
        Assert.Equal(ProfitabilityStatus.Unprofitable, status);
    }

    [Fact]
    public void BotWithMildLoss_FullPipeline_PriceIsLowerAfterAdjustment()
    {
        // Complete pipeline: mild loss → recommendation → price adjusted downward.
        var bot = BotRosterFactory.Build(DefaultOptions())[0];
        bot.InitialNetWorth = 100_000m;
        bot.CurrentNetWorth = 95_000m; // −5% mild loss
        bot.TrackingStartTick = 0;

        var rec = BotOrchestrator.ComputeRecommendationForBot(bot, currentTick: 10, minTicksBeforeAdjustment: 5);
        Assert.True(rec.ShouldAct);

        var originalPrice = 50m;
        var newPrice = PriceAdjustmentHelper.ComputeNewPrice(originalPrice, rec.PriceAdjustmentFactor);
        Assert.True(newPrice < originalPrice, "Price must be lower after a mild-loss recommendation.");
        Assert.True(PriceAdjustmentHelper.IsAdjustmentMeaningful(originalPrice, newPrice));
    }

    [Fact]
    public void BotWithProfit_FullPipeline_PriceIsUnchanged()
    {
        // Complete pipeline: profitable → no action → price stays the same.
        var bot = BotRosterFactory.Build(DefaultOptions())[0];
        bot.InitialNetWorth = 100_000m;
        bot.CurrentNetWorth = 110_000m; // +10% profitable
        bot.TrackingStartTick = 0;

        var rec = BotOrchestrator.ComputeRecommendationForBot(bot, currentTick: 10, minTicksBeforeAdjustment: 5);
        Assert.False(rec.ShouldAct);

        // With NoAction factor = 0, ComputeNewPrice should not be called in production,
        // but if it were, it must not reduce the price (factor 0 → would clamp to 0.01).
        // We simply verify the recommendation does not want adjustment.
        Assert.Equal(0m, rec.PriceAdjustmentFactor);
    }

    // ── Phase 4: roster-level independence ───────────────────────────────────

    [Fact]
    public void TwoBots_IndependentProfitabilityStatus_DoNotAffectEachOther()
    {
        var opts = DefaultOptions();
        opts.BotCount = 2;
        var roster = BotRosterFactory.Build(opts);

        // Bot A is profitable, bot B has a severe loss.
        var botA = roster[0];
        botA.InitialNetWorth = 100_000m;
        botA.CurrentNetWorth = 115_000m; // +15% profitable

        var botB = roster[1];
        botB.InitialNetWorth = 100_000m;
        botB.CurrentNetWorth = 80_000m; // −20% severe loss

        var statusA = BotProfitCalculator.Classify(botA.CurrentNetWorth, botA.InitialNetWorth);
        var statusB = BotProfitCalculator.Classify(botB.CurrentNetWorth, botB.InitialNetWorth);

        Assert.Equal(ProfitabilityStatus.Profitable, statusA);
        Assert.Equal(ProfitabilityStatus.Unprofitable, statusB);

        // Independence: error count on B must not affect A
        botB.ConsecutiveErrors = 4;
        botB.IsSkipped = true;
        Assert.Equal(0, botA.ConsecutiveErrors);
        Assert.False(botA.IsSkipped);
    }

    // ── Phase 5: allowed industries alignment ─────────────────────────────────

    [Fact]
    public void AllowedIndustries_NoneOfTheDefaultsIsProOnly()
    {
        // Pro-only industries must never appear in the default allowed list.
        // This prevents bots from accidentally onboarding into gated industries.
        var proOnly = new[] { "ELECTRONICS", "CONSTRUCTION", "PHARMACEUTICALS", "ENERGY", "LOGISTICS" };
        var opts = DefaultOptions();

        foreach (var industry in opts.AllowedIndustries)
            Assert.True(!proOnly.Contains(industry),
                $"Industry '{industry}' is Pro-only and must not be in the default AllowedIndustries.");
    }

    [Fact]
    public void AllowedIndustries_ContainsAllThreeFreeStarterIndustries()
    {
        // Every free-tier starter industry must be reachable by default NPC bots.
        var opts = DefaultOptions();
        Assert.Contains("FURNITURE", opts.AllowedIndustries);
        Assert.Contains("FOOD_PROCESSING", opts.AllowedIndustries);
        Assert.Contains("HEALTHCARE", opts.AllowedIndustries);
    }
}

using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Unit tests for the pure-function helpers on <see cref="BotOrchestrator"/>
/// that determine bot status labels and pending-recommendation lifecycle.
///
/// Async orchestration methods that require live HTTP are not tested here;
/// they are covered by integration/smoke tests run against the deployed server.
/// </summary>
public sealed class BotOrchestratorTests
{
    // ── Helper: make a fresh bot with sensible defaults ───────────────────────

    private static BotAccount MakeBot() => new()
    {
        Index = 1,
        DisplayName = "NPC 001",
        Email = "npc001@test.example",
        Strategy = "FURNITURE",
        Token = $"tok-{Guid.NewGuid()}",
        TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2),
        Profile = new PlayerProfile
        {
            OnboardingCompletedAtUtc = DateTime.UtcNow.AddMinutes(-10),
        },
    };

    // ── GetBotStatusLabel: ACTIVE ─────────────────────────────────────────────

    [Fact]
    public void StatusLabel_FullyReady_IsActive()
    {
        var bot = MakeBot(); // valid token + onboarding completed
        Assert.Equal("ACTIVE", BotOrchestrator.GetBotStatusLabel(bot));
    }

    [Fact]
    public void StatusLabel_ActiveBot_IsNotSkippedOrNoToken()
    {
        var bot = MakeBot();
        var label = BotOrchestrator.GetBotStatusLabel(bot);
        Assert.NotEqual("SKIPPED", label);
        Assert.NotEqual("NO_TOKEN", label);
        Assert.NotEqual("ONBOARDING", label);
    }

    // ── GetBotStatusLabel: SKIPPED ────────────────────────────────────────────

    [Fact]
    public void StatusLabel_IsSkipped_IsSkipped()
    {
        var bot = MakeBot();
        bot.IsSkipped = true;
        Assert.Equal("SKIPPED", BotOrchestrator.GetBotStatusLabel(bot));
    }

    [Fact]
    public void StatusLabel_SkippedTakesPrecedenceOverNoToken()
    {
        // SKIPPED beats NO_TOKEN if both are true
        var bot = MakeBot();
        bot.IsSkipped = true;
        bot.Token = null;
        Assert.Equal("SKIPPED", BotOrchestrator.GetBotStatusLabel(bot));
    }

    [Fact]
    public void StatusLabel_SkippedTakesPrecedenceOverOnboarding()
    {
        // SKIPPED beats ONBOARDING if both are true
        var bot = new BotAccount
        {
            Index = 2,
            DisplayName = "NPC 002",
            Email = "npc002@test.example",
            Strategy = "FURNITURE",
            IsSkipped = true,
            Token = $"tok-{Guid.NewGuid()}",
            TokenExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            Profile = null, // no profile → onboarding not complete
        };
        Assert.Equal("SKIPPED", BotOrchestrator.GetBotStatusLabel(bot));
    }

    // ── GetBotStatusLabel: NO_TOKEN ───────────────────────────────────────────

    [Fact]
    public void StatusLabel_NullToken_IsNoToken()
    {
        var bot = MakeBot();
        bot.Token = null;
        Assert.Equal("NO_TOKEN", BotOrchestrator.GetBotStatusLabel(bot));
    }

    [Fact]
    public void StatusLabel_ExpiredToken_IsNoToken()
    {
        var bot = MakeBot();
        bot.Token = "expired-tok";
        bot.TokenExpiresAtUtc = DateTime.UtcNow.AddHours(-1); // past expiry
        Assert.Equal("NO_TOKEN", BotOrchestrator.GetBotStatusLabel(bot));
    }

    [Fact]
    public void StatusLabel_NoTokenTakesPrecedenceOverOnboarding()
    {
        // NO_TOKEN beats ONBOARDING if both are true
        var bot = new BotAccount
        {
            Index = 3,
            DisplayName = "NPC 003",
            Email = "npc003@test.example",
            Strategy = "FURNITURE",
            Token = null,
            Profile = null,
        };
        Assert.Equal("NO_TOKEN", BotOrchestrator.GetBotStatusLabel(bot));
    }

    // ── GetBotStatusLabel: ONBOARDING ─────────────────────────────────────────

    [Fact]
    public void StatusLabel_OnboardingNotComplete_IsOnboarding()
    {
        var bot = MakeBot();
        bot.Profile = new PlayerProfile
        {
            OnboardingCompletedAtUtc = null, // not completed
        };
        Assert.Equal("ONBOARDING", BotOrchestrator.GetBotStatusLabel(bot));
    }

    [Fact]
    public void StatusLabel_NullProfile_IsOnboarding()
    {
        // No profile at all means onboarding is not complete
        var bot = MakeBot();
        bot.Profile = null;
        Assert.Equal("ONBOARDING", BotOrchestrator.GetBotStatusLabel(bot));
    }

    // ── Status label covers all four states exhaustively ─────────────────────

    [Fact]
    public void StatusLabel_AllFourStatesProduceKnownStrings()
    {
        var knownLabels = new HashSet<string> { "ACTIVE", "SKIPPED", "NO_TOKEN", "ONBOARDING" };

        // ACTIVE
        var active = MakeBot();
        Assert.Contains(BotOrchestrator.GetBotStatusLabel(active), knownLabels);

        // SKIPPED
        var skipped = MakeBot(); skipped.IsSkipped = true;
        Assert.Contains(BotOrchestrator.GetBotStatusLabel(skipped), knownLabels);

        // NO_TOKEN
        var noToken = MakeBot(); noToken.Token = null;
        Assert.Contains(BotOrchestrator.GetBotStatusLabel(noToken), knownLabels);

        // ONBOARDING
        var onboarding = MakeBot(); onboarding.Profile = null;
        Assert.Contains(BotOrchestrator.GetBotStatusLabel(onboarding), knownLabels);
    }

    // ── PendingRecommendation lifecycle (pure state machine) ──────────────────

    [Fact]
    public void PendingRecommendation_DefaultIsNull()
    {
        var bot = new BotAccount
        {
            Index = 5,
            DisplayName = "NPC 005",
            Email = "npc005@test.example",
            Strategy = "HEALTHCARE",
        };
        Assert.Null(bot.PendingRecommendation);
    }

    [Fact]
    public void PendingRecommendation_SetByOrchestratorThenCleared_CycleIsCorrect()
    {
        var bot = new BotAccount
        {
            Index = 6,
            DisplayName = "NPC 006",
            Email = "npc006@test.example",
            Strategy = "FOOD_PROCESSING",
        };

        // Simulate orchestrator EvaluateAndLogProfitability setting a recommendation
        var rec = BotProfitCalculator.Recommend(90_000m, 100_000m, 10);
        bot.PendingRecommendation = rec;

        Assert.NotNull(bot.PendingRecommendation);
        Assert.True(bot.PendingRecommendation!.ShouldAct);

        // Simulate orchestrator clearing after apply attempt
        bot.PendingRecommendation = null;
        Assert.Null(bot.PendingRecommendation);
    }

    [Fact]
    public void PendingRecommendation_NoActionSentinel_SetAndCleared()
    {
        var bot = new BotAccount
        {
            Index = 7,
            DisplayName = "NPC 007",
            Email = "npc007@test.example",
            Strategy = "FURNITURE",
        };

        // Orchestrator stores even NoAction recommendations (to log them)
        bot.PendingRecommendation = StrategyRecommendation.NoAction;
        Assert.NotNull(bot.PendingRecommendation);
        Assert.False(bot.PendingRecommendation!.ShouldAct);

        bot.PendingRecommendation = null;
        Assert.Null(bot.PendingRecommendation);
    }

    // ── Error isolation ───────────────────────────────────────────────────────

    [Fact]
    public void ErrorIsolation_EachBotTracksConsecutiveErrorsIndependently()
    {
        // Two bots: one encounters errors, the other should be unaffected.
        var bot1 = new BotAccount { Index = 8, DisplayName = "NPC 008", Email = "npc008@t.example", Strategy = "FURNITURE" };
        var bot2 = new BotAccount { Index = 9, DisplayName = "NPC 009", Email = "npc009@t.example", Strategy = "HEALTHCARE" };

        bot1.ConsecutiveErrors = 3;
        bot1.IsSkipped = true;

        // bot2's error counter is unaffected
        Assert.Equal(0, bot2.ConsecutiveErrors);
        Assert.False(bot2.IsSkipped);
    }

    [Fact]
    public void ErrorIsolation_SkippedBotStatusLabel_DoesNotAffectSiblings()
    {
        var bot1 = MakeBot(); bot1.IsSkipped = true;
        var bot2 = MakeBot();

        Assert.Equal("SKIPPED", BotOrchestrator.GetBotStatusLabel(bot1));
        Assert.Equal("ACTIVE", BotOrchestrator.GetBotStatusLabel(bot2));
    }
}

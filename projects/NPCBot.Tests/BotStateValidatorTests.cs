using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Unit tests for <see cref="BotStateValidator"/> — pure validation with no I/O.
/// </summary>
public sealed class BotStateValidatorTests
{
    // ── IsReadyForOperation ───────────────────────────────────────────────────

    [Fact]
    public void IsReadyForOperation_FullyActiveBot_ReturnsTrue()
    {
        var bot = MakeReadyBot();
        Assert.True(BotStateValidator.IsReadyForOperation(bot));
    }

    [Fact]
    public void IsReadyForOperation_SkippedBot_ReturnsFalse()
    {
        var bot = MakeReadyBot();
        bot.IsSkipped = true;
        Assert.False(BotStateValidator.IsReadyForOperation(bot));
    }

    [Fact]
    public void IsReadyForOperation_NoToken_ReturnsFalse()
    {
        var bot = MakeReadyBot();
        bot.Token = null;
        Assert.False(BotStateValidator.IsReadyForOperation(bot));
    }

    [Fact]
    public void IsReadyForOperation_TokenExpired_ReturnsFalse()
    {
        var bot = MakeReadyBot();
        bot.TokenExpiresAtUtc = DateTime.UtcNow.AddHours(-1);
        Assert.False(BotStateValidator.IsReadyForOperation(bot));
    }

    [Fact]
    public void IsReadyForOperation_OnboardingNotComplete_ReturnsFalse()
    {
        var bot = MakeReadyBot();
        bot.Profile = new PlayerProfile { OnboardingCompletedAtUtc = null };
        Assert.False(BotStateValidator.IsReadyForOperation(bot));
    }

    [Fact]
    public void IsReadyForOperation_NullProfile_ReturnsFalse()
    {
        var bot = MakeReadyBot();
        bot.Profile = null;
        Assert.False(BotStateValidator.IsReadyForOperation(bot));
    }

    // ── IsStale ───────────────────────────────────────────────────────────────

    [Fact]
    public void IsStale_NullLastSuccess_ReturnsFalse()
    {
        var bot = new BotAccount { Index = 1, DisplayName = "Test", Email = "t@t.com", Strategy = "S" };
        Assert.False(BotStateValidator.IsStale(bot, staleAfterMinutes: 10));
    }

    [Fact]
    public void IsStale_RecentSuccess_ReturnsFalse()
    {
        var bot = MakeReadyBot();
        bot.LastSuccessUtc = DateTime.UtcNow.AddMinutes(-2);
        Assert.False(BotStateValidator.IsStale(bot, staleAfterMinutes: 10));
    }

    [Fact]
    public void IsStale_OldSuccess_ReturnsTrue()
    {
        var bot = MakeReadyBot();
        bot.LastSuccessUtc = DateTime.UtcNow.AddMinutes(-30);
        Assert.True(BotStateValidator.IsStale(bot, staleAfterMinutes: 10));
    }

    [Fact]
    public void IsStale_ExactlyAtThreshold_ReturnsFalse()
    {
        // Exactly 10 minutes ago — should NOT be considered stale yet.
        var bot = MakeReadyBot();
        bot.LastSuccessUtc = DateTime.UtcNow.AddMinutes(-10).AddSeconds(1);
        Assert.False(BotStateValidator.IsStale(bot, staleAfterMinutes: 10));
    }

    // ── IsAtRisk ──────────────────────────────────────────────────────────────

    [Fact]
    public void IsAtRisk_NoErrors_ReturnsFalse()
    {
        var bot = MakeReadyBot();
        bot.ConsecutiveErrors = 0;
        Assert.False(BotStateValidator.IsAtRisk(bot, maxConsecutiveErrors: 5));
    }

    [Fact]
    public void IsAtRisk_AtHalfLimit_ReturnsTrue()
    {
        var bot = MakeReadyBot();
        bot.ConsecutiveErrors = 3; // 60 % of 5
        Assert.True(BotStateValidator.IsAtRisk(bot, maxConsecutiveErrors: 5));
    }

    [Fact]
    public void IsAtRisk_BelowHalfLimit_ReturnsFalse()
    {
        var bot = MakeReadyBot();
        bot.ConsecutiveErrors = 2; // 40 % of 5
        Assert.False(BotStateValidator.IsAtRisk(bot, maxConsecutiveErrors: 5));
    }

    [Fact]
    public void IsAtRisk_SkippedBot_ReturnsFalse()
    {
        var bot = MakeReadyBot();
        bot.IsSkipped = true;
        bot.ConsecutiveErrors = 5;
        // Already skipped — at-risk is irrelevant
        Assert.False(BotStateValidator.IsAtRisk(bot, maxConsecutiveErrors: 5));
    }

    // ── Validate ─────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_FullyReadyBot_IsValidWithNoIssues()
    {
        var bot = MakeReadyBot();
        bot.LastSuccessUtc = DateTime.UtcNow;

        var result = BotStateValidator.Validate(bot);
        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
        Assert.Contains("ready", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_SkippedBot_HasIssue()
    {
        var bot = MakeReadyBot();
        bot.IsSkipped = true;

        var result = BotStateValidator.Validate(bot);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Contains("skipped", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ExpiredToken_HasIssue()
    {
        var bot = MakeReadyBot();
        bot.TokenExpiresAtUtc = DateTime.UtcNow.AddHours(-2);

        var result = BotStateValidator.Validate(bot);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Contains("expired", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_NotOnboarded_HasIssue()
    {
        var bot = MakeReadyBot();
        bot.Profile = new PlayerProfile { OnboardingCompletedAtUtc = null };

        var result = BotStateValidator.Validate(bot);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Contains("onboarding", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_StaleBot_HasIssue()
    {
        var bot = MakeReadyBot();
        bot.LastSuccessUtc = DateTime.UtcNow.AddHours(-1);

        var result = BotStateValidator.Validate(bot, staleAfterMinutes: 10);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Contains("10 minutes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_MultipleProblems_ReportsAllIssues()
    {
        var bot = MakeReadyBot();
        bot.IsSkipped = true;
        bot.Token = null;

        var result = BotStateValidator.Validate(bot);
        Assert.False(result.IsValid);
        Assert.True(result.Issues.Count >= 2);
    }

    [Fact]
    public void Validate_Summary_ContainsIssueTextWhenInvalid()
    {
        var bot = MakeReadyBot();
        bot.IsSkipped = true;

        var result = BotStateValidator.Validate(bot);
        Assert.False(string.IsNullOrWhiteSpace(result.Summary));
        // Summary must aggregate the issue text
        Assert.Contains("skipped", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_StaleAndNotOnboarded_ReportsBothIssues()
    {
        var bot = MakeReadyBot();
        bot.Profile = new PlayerProfile { OnboardingCompletedAtUtc = null };
        bot.LastSuccessUtc = DateTime.UtcNow.AddHours(-1);

        var result = BotStateValidator.Validate(bot, staleAfterMinutes: 10);
        Assert.False(result.IsValid);
        Assert.True(result.Issues.Count >= 2);
        Assert.Contains(result.Issues, i => i.Contains("onboarding", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Issues, i => i.Contains("minutes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_AllProblems_ReportsAllFourIssues()
    {
        // All four possible issues: skipped, no token, not onboarded, stale.
        var bot = MakeReadyBot();
        bot.IsSkipped = true;
        bot.Token = null;
        bot.Profile = new PlayerProfile { OnboardingCompletedAtUtc = null };
        bot.LastSuccessUtc = DateTime.UtcNow.AddHours(-2);

        var result = BotStateValidator.Validate(bot, staleAfterMinutes: 10);
        Assert.False(result.IsValid);
        Assert.Equal(4, result.Issues.Count);
    }

    // ── IsAtRisk boundary ─────────────────────────────────────────────────────

    [Fact]
    public void IsAtRisk_ExactlyAtHalfLimit_ReturnsTrue()
    {
        // 50% of 4 = 2 errors — exactly at the half-limit threshold.
        var bot = MakeReadyBot();
        bot.ConsecutiveErrors = 2;
        Assert.True(BotStateValidator.IsAtRisk(bot, maxConsecutiveErrors: 4));
    }

    [Fact]
    public void IsAtRisk_OneBeforeHalfLimit_ReturnsFalse()
    {
        // 1/4 = 25% — below the 50% threshold.
        var bot = MakeReadyBot();
        bot.ConsecutiveErrors = 1;
        Assert.False(BotStateValidator.IsAtRisk(bot, maxConsecutiveErrors: 4));
    }

    // ── Additional Validate edge cases ────────────────────────────────────────

    [Fact]
    public void Validate_NullLastSuccessUtc_IsNotStale()
    {
        // A bot that has never run is NOT considered stale — it is uninitialised.
        var bot = MakeReadyBot();
        bot.LastSuccessUtc = null;

        var result = BotStateValidator.Validate(bot, staleAfterMinutes: 10);
        Assert.True(result.IsValid, "A bot with null LastSuccessUtc should not be flagged as stale.");
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Validate_CustomStaleThreshold_IsRespected()
    {
        // Bot had a successful operation 5 minutes ago.
        // With staleAfterMinutes=10 it is fine; with staleAfterMinutes=2 it is stale.
        var bot = MakeReadyBot();
        bot.LastSuccessUtc = DateTime.UtcNow.AddMinutes(-5);

        var notStale = BotStateValidator.Validate(bot, staleAfterMinutes: 10);
        var stale = BotStateValidator.Validate(bot, staleAfterMinutes: 2);

        Assert.True(notStale.IsValid);
        Assert.False(stale.IsValid);
    }

    [Fact]
    public void Validate_Summary_IsReadyWhenValid()
    {
        var bot = MakeReadyBot();
        bot.LastSuccessUtc = DateTime.UtcNow;

        var result = BotStateValidator.Validate(bot);
        Assert.Contains("ready", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Summary_ContainsAllIssuesText()
    {
        var bot = MakeReadyBot();
        bot.IsSkipped = true;
        bot.Token = null;

        var result = BotStateValidator.Validate(bot);
        // Summary should be a join of all individual issue strings
        foreach (var issue in result.Issues)
            Assert.Contains(issue, result.Summary);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BotAccount MakeReadyBot() => new()
    {
        Index = 1,
        DisplayName = "NPC_Test_1",
        Email = "npc_test_1@example.com",
        Strategy = "Industrial",
        Token = "valid-token",
        TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2),
        Profile = new PlayerProfile
        {
            OnboardingCompletedAtUtc = DateTime.UtcNow.AddDays(-1),
        },
        LastSuccessUtc = null, // null means not yet started — IsStale returns false
    };
}

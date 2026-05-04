using Capitalism.NPCBot.Models;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Unit tests for <see cref="BotAccount"/> model logic:
/// token validity, onboarding state, and profitability tracking.
/// </summary>
public sealed class BotAccountTests
{
    // ── IsTokenValid ──────────────────────────────────────────────────────────

    [Fact]
    public void IsTokenValid_WhenNoToken_ReturnsFalse()
    {
        var bot = new BotAccount { Token = null };
        Assert.False(bot.IsTokenValid());
    }

    [Fact]
    public void IsTokenValid_WhenNoExpiry_ReturnsFalse()
    {
        var bot = new BotAccount { Token = "abc", TokenExpiresAtUtc = null };
        Assert.False(bot.IsTokenValid());
    }

    [Fact]
    public void IsTokenValid_WhenTokenAlreadyExpired_ReturnsFalse()
    {
        var bot = new BotAccount
        {
            Token = "abc",
            TokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1),
        };
        Assert.False(bot.IsTokenValid());
    }

    [Fact]
    public void IsTokenValid_WhenTokenExpiredWithinBuffer_ReturnsFalse()
    {
        // Token expires in 3 minutes but the default buffer is 5 minutes.
        var bot = new BotAccount
        {
            Token = "abc",
            TokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(3),
        };
        Assert.False(bot.IsTokenValid(bufferMinutes: 5));
    }

    [Fact]
    public void IsTokenValid_WhenTokenFreshAndWithinBuffer_ReturnsTrue()
    {
        // Token expires in 30 minutes, well beyond the 5-minute buffer.
        var bot = new BotAccount
        {
            Token = "abc",
            TokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(30),
        };
        Assert.True(bot.IsTokenValid(bufferMinutes: 5));
    }

    [Fact]
    public void IsTokenValid_WithZeroBuffer_ReturnsTrueForNonExpiredToken()
    {
        var bot = new BotAccount
        {
            Token = "abc",
            TokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(10),
        };
        Assert.True(bot.IsTokenValid(bufferMinutes: 0));
    }

    // ── HasValidToken ─────────────────────────────────────────────────────────

    [Fact]
    public void HasValidToken_MatchesIsTokenValidWithDefaultBuffer()
    {
        var botValid = new BotAccount
        {
            Token = "t",
            TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2),
        };
        Assert.Equal(botValid.IsTokenValid(5), botValid.HasValidToken);

        var botExpired = new BotAccount
        {
            Token = "t",
            TokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(3), // within buffer
        };
        Assert.Equal(botExpired.IsTokenValid(5), botExpired.HasValidToken);
    }

    // ── OnboardingCompleted ───────────────────────────────────────────────────

    [Fact]
    public void OnboardingCompleted_WhenProfileNull_ReturnsFalse()
    {
        var bot = new BotAccount { Profile = null };
        Assert.False(bot.OnboardingCompleted);
    }

    [Fact]
    public void OnboardingCompleted_WhenCompletedAtUtcNull_ReturnsFalse()
    {
        var bot = new BotAccount
        {
            Profile = new PlayerProfile { OnboardingCompletedAtUtc = null },
        };
        Assert.False(bot.OnboardingCompleted);
    }

    [Fact]
    public void OnboardingCompleted_WhenCompletedAtUtcSet_ReturnsTrue()
    {
        var bot = new BotAccount
        {
            Profile = new PlayerProfile
            {
                OnboardingCompletedAtUtc = DateTime.UtcNow.AddHours(-1),
            },
        };
        Assert.True(bot.OnboardingCompleted);
    }

    // ── ProfitDelta ───────────────────────────────────────────────────────────

    [Fact]
    public void ProfitDelta_AtBaseline_IsZero()
    {
        var bot = new BotAccount { InitialNetWorth = 100_000m, CurrentNetWorth = 100_000m };
        Assert.Equal(0m, bot.ProfitDelta);
    }

    [Fact]
    public void ProfitDelta_WhenProfitable_IsPositive()
    {
        var bot = new BotAccount { InitialNetWorth = 100_000m, CurrentNetWorth = 150_000m };
        Assert.Equal(50_000m, bot.ProfitDelta);
    }

    [Fact]
    public void ProfitDelta_WhenLossMaking_IsNegative()
    {
        var bot = new BotAccount { InitialNetWorth = 100_000m, CurrentNetWorth = 70_000m };
        Assert.Equal(-30_000m, bot.ProfitDelta);
    }

    // ── ToString / display ────────────────────────────────────────────────────

    [Fact]
    public void ToString_IncludesIndexDisplayNameAndStrategy()
    {
        var bot = new BotAccount
        {
            Index = 3,
            DisplayName = "NPC_Trading_03",
            Strategy = "Trading",
        };
        var str = bot.ToString();
        Assert.Contains("3", str);
        Assert.Contains("NPC_Trading_03", str);
        Assert.Contains("Trading", str);
    }

    // ── Defaults ──────────────────────────────────────────────────────────────

    [Fact]
    public void Defaults_ConsecutiveErrorsIsZero()
    {
        var bot = new BotAccount();
        Assert.Equal(0, bot.ConsecutiveErrors);
    }

    [Fact]
    public void Defaults_IsSkippedIsFalse()
    {
        var bot = new BotAccount();
        Assert.False(bot.IsSkipped);
    }

    [Fact]
    public void Defaults_LastSuccessUtcIsNull()
    {
        var bot = new BotAccount();
        Assert.Null(bot.LastSuccessUtc);
    }

    [Fact]
    public void Defaults_InitialNetWorthIsZero()
    {
        var bot = new BotAccount();
        Assert.Equal(0m, bot.InitialNetWorth);
    }

    [Fact]
    public void Defaults_CurrentNetWorthIsZero()
    {
        var bot = new BotAccount();
        Assert.Equal(0m, bot.CurrentNetWorth);
    }

    [Fact]
    public void Defaults_TrackingStartTickIsZero()
    {
        var bot = new BotAccount();
        Assert.Equal(0L, bot.TrackingStartTick);
    }

    [Fact]
    public void Defaults_PendingRecommendationIsNull()
    {
        var bot = new BotAccount();
        Assert.Null(bot.PendingRecommendation);
    }

    [Fact]
    public void PendingRecommendation_CanBeSet()
    {
        var bot = new BotAccount();
        var rec = new StrategyRecommendation { ShouldAct = true, Reason = "Test", PriceAdjustmentFactor = 0.95m };
        bot.PendingRecommendation = rec;
        Assert.NotNull(bot.PendingRecommendation);
        Assert.True(bot.PendingRecommendation.ShouldAct);
    }

    [Fact]
    public void PendingRecommendation_CanBeCleared()
    {
        var bot = new BotAccount
        {
            PendingRecommendation = new StrategyRecommendation { ShouldAct = true, Reason = "x", PriceAdjustmentFactor = 0.9m },
        };
        bot.PendingRecommendation = null;
        Assert.Null(bot.PendingRecommendation);
    }
}

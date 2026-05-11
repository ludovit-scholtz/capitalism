using Capitalism.NPCBot.Configuration;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Unit tests for <see cref="BotOptions"/> defaults and constraints.
/// </summary>
public sealed class BotOptionsTests
{
    [Fact]
    public void Defaults_GraphqlUrlPointsAtLiveServer()
    {
        var opts = new BotOptions();
        Assert.Equal("https://capitalism.de-4.biatec.io/graphql", opts.GraphqlUrl);
    }

    [Fact]
    public void Defaults_BotCountIsThree()
    {
        var opts = new BotOptions();
        Assert.Equal(3, opts.BotCount);
    }

    [Fact]
    public void Defaults_IsEnabled()
    {
        var opts = new BotOptions();
        Assert.True(opts.Enabled);
    }

    [Fact]
    public void Defaults_PollIntervalIsAtLeastTenSeconds()
    {
        // Bots should not poll more aggressively than once every 10 s to avoid
        // hammering a shared live server.
        var opts = new BotOptions();
        Assert.True(opts.PollIntervalSeconds >= 10,
            $"PollIntervalSeconds should be >= 10 but was {opts.PollIntervalSeconds}");
    }

    [Fact]
    public void Defaults_TokenRefreshBufferIsPositive()
    {
        var opts = new BotOptions();
        Assert.True(opts.TokenRefreshBufferMinutes > 0);
    }

    [Fact]
    public void Defaults_MaxConsecutiveErrorsIsPositive()
    {
        var opts = new BotOptions();
        Assert.True(opts.MaxConsecutiveErrors > 0);
    }

    [Fact]
    public void Defaults_AllowedIndustriesContainsThreeFreeStarterIndustries()
    {
        // Only the three free-tier industries (FURNITURE, FOOD_PROCESSING, HEALTHCARE) are
        // pre-configured so bots never accidentally try to join a Pro-only industry.
        var opts = new BotOptions();
        Assert.Contains("FURNITURE", opts.AllowedIndustries);
        Assert.Contains("FOOD_PROCESSING", opts.AllowedIndustries);
        Assert.Contains("HEALTHCARE", opts.AllowedIndustries);
    }

    [Fact]
    public void Defaults_AllowedIndustriesDoesNotContainProOnlyIndustries()
    {
        // Pro-only industries that bots must not use by default.
        var proOnly = new[] { "ELECTRONICS", "CONSTRUCTION", "PHARMACEUTICALS", "ENERGY", "LOGISTICS" };
        var opts = new BotOptions();
        foreach (var industry in proOnly)
            Assert.DoesNotContain(industry, opts.AllowedIndustries);
    }

    [Fact]
    public void Defaults_BotEmailDomainIsNonEmpty()
    {
        var opts = new BotOptions();
        Assert.NotEmpty(opts.BotEmailDomain);
    }

    [Fact]
    public void Defaults_BotNamePrefixIsNonEmpty()
    {
        var opts = new BotOptions();
        Assert.NotEmpty(opts.BotNamePrefix);
    }

    [Fact]
    public void Defaults_BotNamePrefixIsNpc()
    {
        var opts = new BotOptions();
        Assert.Equal("NPC", opts.BotNamePrefix);
    }

    [Fact]
    public void Defaults_MinTicksBeforeAdjustmentIsPositive()
    {
        // Bots must wait at least one tick before recommending strategy changes
        // to avoid reacting to start-up noise.
        var opts = new BotOptions();
        Assert.True(opts.MinTicksBeforeAdjustment > 0,
            $"MinTicksBeforeAdjustment should be > 0 but was {opts.MinTicksBeforeAdjustment}");
    }

    [Fact]
    public void Defaults_AllowedIndustriesHasExactlyThreeEntries()
    {
        // Only the three starter free-tier industries should be pre-configured.
        var opts = new BotOptions();
        Assert.Equal(3, opts.AllowedIndustries.Length);
    }

    [Fact]
    public void Defaults_MaxConsecutiveErrorsIsAtLeastTwo()
    {
        // A limit of 1 would skip a bot after the very first transient error.
        // Require at least 2 retries before giving up.
        var opts = new BotOptions();
        Assert.True(opts.MaxConsecutiveErrors >= 2,
            $"MaxConsecutiveErrors should be >= 2 but was {opts.MaxConsecutiveErrors}");
    }

    [Fact]
    public void SectionName_Constant_IsNpcBot()
    {
        // The SectionName constant is used for .NET configuration DI binding
        // (services.Configure<BotOptions>(config.GetSection(BotOptions.SectionName))).
        // If it changes, the environment-variable overrides and appsettings.json binding break silently.
        Assert.Equal("NpcBot", BotOptions.SectionName);
    }

    [Fact]
    public void Defaults_BotPassword_IsEmpty()
    {
        // The default password is intentionally empty so the bot never ships a committed
        // credential. Operators must supply NpcBot__BotPassword (or NpcBot__ApiKey) via
        // environment variables before running outside the Development environment.
        var opts = new BotOptions();
        Assert.Equal("", opts.BotPassword);
    }

    [Fact]
    public void Defaults_AllowedIndustries_AllEntriesAreUppercase()
    {
        // Industry names are compared with SCREAMING_SNAKE_CASE GraphQL enum values.
        // A lowercase entry would fail the backend INVALID_INDUSTRY validation silently.
        var opts = new BotOptions();
        foreach (var industry in opts.AllowedIndustries)
            Assert.True(industry == industry.ToUpperInvariant(),
                $"Industry '{industry}' must be uppercase to match GraphQL enum values.");
    }

    [Fact]
    public void Defaults_PollIntervalIsExactlySixtySeconds()
    {
        // The default poll interval should be exactly 60 seconds so that one bot tick
        // aligns with approximately one game simulation tick (60 s interval in production).
        var opts = new BotOptions();
        Assert.Equal(60, opts.PollIntervalSeconds);
    }

    [Fact]
    public void Defaults_TokenRefreshBufferIsExactlyFiveMinutes()
    {
        // Proactive re-authentication fires 5 minutes before token expiry by default.
        // A smaller buffer risks sending requests with an expired token on slow networks.
        var opts = new BotOptions();
        Assert.Equal(5, opts.TokenRefreshBufferMinutes);
    }

    [Fact]
    public void Defaults_MaxConsecutiveErrorsIsExactlyFive()
    {
        // The skip threshold should be exactly 5 so that transient network blips (up to 4
        // consecutive) do not permanently disable a bot account.
        var opts = new BotOptions();
        Assert.Equal(5, opts.MaxConsecutiveErrors);
    }

    [Fact]
    public void Defaults_BotEmailDomain_ContainsDotSeparator()
    {
        // A valid email domain must have at least one dot so registration does not
        // fail immediately with a malformed email address error.
        var opts = new BotOptions();
        Assert.Contains(".", opts.BotEmailDomain);
    }

    [Fact]
    public void Defaults_MinTicksBeforeAdjustmentIsExactlyFive()
    {
        // The default minimum is 5 ticks so a bot waits for meaningful profitability
        // data before making price adjustments.
        var opts = new BotOptions();
        Assert.Equal(5, opts.MinTicksBeforeAdjustment);
    }
}

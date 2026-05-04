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
}

using Capitalism.NPCBot.Configuration;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Unit tests for <see cref="BotRosterFactory"/>: bot naming conventions,
/// email generation, count clamping, and strategy cycling.
/// </summary>
public sealed class BotRosterFactoryTests
{
    private static BotOptions DefaultOptions() => new();

    // ── Count clamping ────────────────────────────────────────────────────────

    [Fact]
    public void Build_DefaultOptions_ProducesThreeBots()
    {
        var roster = BotRosterFactory.Build(DefaultOptions());
        Assert.Equal(3, roster.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void Build_WithinValidRange_ProducesExactCount(int count)
    {
        var opts = DefaultOptions();
        opts.BotCount = count;
        var roster = BotRosterFactory.Build(opts);
        Assert.Equal(count, roster.Count);
    }

    [Fact]
    public void Build_CountBelowOne_ClampedToOne()
    {
        var opts = DefaultOptions();
        opts.BotCount = 0;
        var roster = BotRosterFactory.Build(opts);
        Assert.Single(roster);
    }

    [Fact]
    public void Build_CountAboveTwenty_ClampedToTwenty()
    {
        var opts = DefaultOptions();
        opts.BotCount = 100;
        var roster = BotRosterFactory.Build(opts);
        Assert.Equal(20, roster.Count);
    }

    // ── Index sequencing ──────────────────────────────────────────────────────

    [Fact]
    public void Build_IndexStartsAtOne()
    {
        var roster = BotRosterFactory.Build(DefaultOptions());
        Assert.Equal(1, roster[0].Index);
    }

    [Fact]
    public void Build_IndicesAreContiguousAndOneBased()
    {
        var opts = DefaultOptions();
        opts.BotCount = 5;
        var roster = BotRosterFactory.Build(opts);
        for (var i = 0; i < 5; i++)
            Assert.Equal(i + 1, roster[i].Index);
    }

    // ── Naming conventions ────────────────────────────────────────────────────

    [Fact]
    public void Build_DisplayNamesContainPrefix()
    {
        var opts = DefaultOptions();
        opts.BotNamePrefix = "MYBOT";
        var roster = BotRosterFactory.Build(opts);
        Assert.All(roster, b => Assert.StartsWith("MYBOT_", b.DisplayName));
    }

    [Fact]
    public void Build_EmailsUseDomain()
    {
        var opts = DefaultOptions();
        opts.BotEmailDomain = "test.example.com";
        var roster = BotRosterFactory.Build(opts);
        Assert.All(roster, b => Assert.EndsWith("@test.example.com", b.Email));
    }

    [Fact]
    public void Build_EmailsAreLowercase()
    {
        var opts = DefaultOptions();
        opts.BotNamePrefix = "NPC";
        var roster = BotRosterFactory.Build(opts);
        Assert.All(roster, b => Assert.Equal(b.Email.ToLowerInvariant(), b.Email));
    }

    [Fact]
    public void Build_AllEmailsAreUnique()
    {
        var opts = DefaultOptions();
        opts.BotCount = 20;
        var roster = BotRosterFactory.Build(opts);
        var emails = roster.Select(b => b.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Equal(20, emails.Count);
    }

    // ── Strategy cycling ─────────────────────────────────────────────────────

    [Fact]
    public void Build_StrategyIsNotEmpty()
    {
        var roster = BotRosterFactory.Build(DefaultOptions());
        Assert.All(roster, b => Assert.NotEmpty(b.Strategy));
    }

    [Fact]
    public void Build_StrategiesCycleWhenMoreThanFiveBots()
    {
        // There are 5 strategy names. Bot 1 and bot 6 should share the same strategy.
        var opts = DefaultOptions();
        opts.BotCount = 10;
        var roster = BotRosterFactory.Build(opts);
        Assert.Equal(roster[0].Strategy, roster[5].Strategy);
        Assert.Equal(roster[1].Strategy, roster[6].Strategy);
    }

    [Fact]
    public void Build_StrategyAppearsInDisplayName()
    {
        var opts = DefaultOptions();
        opts.BotCount = 5;
        var roster = BotRosterFactory.Build(opts);
        Assert.All(roster, b => Assert.Contains(b.Strategy, b.DisplayName));
    }

    // ── Token is null on creation ─────────────────────────────────────────────

    [Fact]
    public void Build_NewBotsHaveNoToken()
    {
        var roster = BotRosterFactory.Build(DefaultOptions());
        Assert.All(roster, b => Assert.Null(b.Token));
    }

    [Fact]
    public void Build_FirstBot_StrategyIsTrading()
    {
        // The Strategies array is ["Trading", "Industrial", "Retail", "Mixed", "Aggressive"].
        // Bot index 1 uses index 0 → "Trading". Locking this prevents silent reordering
        // that would change the profile of existing live bots on restart.
        var opts = DefaultOptions();
        opts.BotCount = 1;
        var roster = BotRosterFactory.Build(opts);
        Assert.Equal("Trading", roster[0].Strategy);
    }

    [Fact]
    public void Build_FiveBots_AllFiveStrategiesPresent()
    {
        // Exactly 5 bots should produce all 5 distinct strategy names.
        var opts = DefaultOptions();
        opts.BotCount = 5;
        var roster = BotRosterFactory.Build(opts);
        var strategies = roster.Select(b => b.Strategy).Distinct().ToList();
        Assert.Equal(5, strategies.Count);
        Assert.Contains("Trading", strategies);
        Assert.Contains("Industrial", strategies);
        Assert.Contains("Retail", strategies);
        Assert.Contains("Mixed", strategies);
        Assert.Contains("Aggressive", strategies);
    }

    [Fact]
    public void Build_BotIndex10_DisplayName_ContainsTwoDigitIndex()
    {
        // Format string is {i:D2}, so index 10 → "10" (2 chars), not "010".
        // Index 1 → "01". The display name for bot 10 must contain "10" verbatim.
        var opts = DefaultOptions();
        opts.BotCount = 10;
        var roster = BotRosterFactory.Build(opts);
        var bot10 = roster[9]; // index 1-based = 10, list 0-based = 9
        Assert.Contains("_10", bot10.DisplayName);
    }
}

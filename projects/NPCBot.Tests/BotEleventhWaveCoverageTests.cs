using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Eleventh coverage-wave tests targeting paths confirmed absent from all prior waves:
/// <list type="bullet">
///   <item><b>OnboardingHelpers.PickCheapestAvailableLot</b> — whitespace-string BuildingId
///     is NOT null, so the lot is treated as occupied (mentioned in BotEighthWaveCoverageTests
///     doc comment as covered but never actually implemented).</item>
///   <item><b>OnboardingHelpers.ContainsSuitableType</b> — trailing comma, leading comma,
///     consecutive-comma, and single-comma edge cases all exercised to prove
///     <c>StringSplitOptions.RemoveEmptyEntries</c> is respected.</item>
///   <item><b>BotRosterFactory.Build</b> — off-by-one boundary at max+1 (21 → 20) and
///     negative count (−1 → 1, −5 → 1) complement the existing 0 → 1 and 100 → 20 tests.</item>
///   <item><b>BotStateValidator.Validate</b> — exact "No successful operation in the last
///     N minutes." issue-text format, including custom threshold values and the
///     one-issue/invalid state for a stale but otherwise valid bot.</item>
/// </list>
/// </summary>
public sealed class BotEleventhWaveCoverageTests
{
    // ── PickCheapestAvailableLot: whitespace BuildingId ───────────────────────

    [Fact]
    public void PickCheapestAvailableLot_WhitespaceBuildingId_TreatedAsOccupied()
    {
        // A BuildingId of "   " (whitespace only) is NOT null.
        // PickCheapestAvailableLot filters with `l.BuildingId is null`, so the whitespace
        // string is evaluated as non-null → the lot is excluded as if already occupied.
        var lots = new[]
        {
            new BuildingLotSummary
            {
                Id = "lot-ws", SuitableTypes = "FACTORY",
                BuildingId = "   ", Price = 50_000m,
            },
        };

        var result = OnboardingHelpers.PickCheapestAvailableLot(lots, "FACTORY");

        Assert.Null(result);
    }

    [Fact]
    public void PickCheapestAvailableLot_MixedWhitespaceAndNullBuildingIds_OnlyNullLotReturned()
    {
        // Two lots with matching SuitableTypes: one has a whitespace BuildingId (occupied)
        // and one has null BuildingId (available). Only the null one must be returned.
        var lots = new[]
        {
            new BuildingLotSummary
            {
                Id = "lot-ws", SuitableTypes = "FACTORY",
                BuildingId = "\t", Price = 10_000m, // whitespace → occupied
            },
            new BuildingLotSummary
            {
                Id = "lot-avail", SuitableTypes = "FACTORY",
                BuildingId = null, Price = 20_000m, // null → available
            },
        };

        var result = OnboardingHelpers.PickCheapestAvailableLot(lots, "FACTORY");

        Assert.NotNull(result);
        Assert.Equal("lot-avail", result.Id);
    }

    // ── ContainsSuitableType: comma edge cases ────────────────────────────────

    [Fact]
    public void ContainsSuitableType_TrailingComma_ReturnsTrue()
    {
        // "FACTORY," — the empty segment produced by the trailing comma is removed by
        // StringSplitOptions.RemoveEmptyEntries, leaving only "FACTORY" which matches.
        Assert.True(OnboardingHelpers.ContainsSuitableType("FACTORY,", "FACTORY"));
    }

    [Fact]
    public void ContainsSuitableType_LeadingComma_ReturnsTrue()
    {
        // ",FACTORY" — the empty segment before the comma is removed; "FACTORY" remains.
        Assert.True(OnboardingHelpers.ContainsSuitableType(",FACTORY", "FACTORY"));
    }

    [Fact]
    public void ContainsSuitableType_ConsecutiveCommas_StillFindsType()
    {
        // "MINE,,FACTORY" — the empty segment between the two commas is removed.
        // Both "MINE" and "FACTORY" are present after splitting.
        Assert.True(OnboardingHelpers.ContainsSuitableType("MINE,,FACTORY", "FACTORY"));
    }

    [Fact]
    public void ContainsSuitableType_SingleComma_ReturnsFalse()
    {
        // "," is split into two empty strings; both are removed by RemoveEmptyEntries.
        // The resulting sequence is empty → no match → returns false.
        // (Complements the existing ",,," test in BotFifthWaveCoverageTests.)
        Assert.False(OnboardingHelpers.ContainsSuitableType(",", "FACTORY"));
    }

    [Fact]
    public void ContainsSuitableType_TrailingAndLeadingCommas_StillFindsType()
    {
        // ",FACTORY,MINE," — both edge empty segments are removed; types are found normally.
        Assert.True(OnboardingHelpers.ContainsSuitableType(",FACTORY,MINE,", "MINE"));
    }

    // ── BotRosterFactory: off-by-one and negative count clamping ─────────────

    [Fact]
    public void BotRosterFactory_BotCountTwentyOne_ClampsToTwenty()
    {
        // Max allowed count is 20. BotCount = 21 must be clamped down to 20.
        // This is the off-by-one boundary immediately above the maximum.
        var opts = new BotOptions { BotCount = 21 };

        var bots = BotRosterFactory.Build(opts);

        Assert.Equal(20, bots.Count);
    }

    [Fact]
    public void BotRosterFactory_BotCountNegativeOne_ClampsToOne()
    {
        // Negative counts must be clamped to the minimum of 1.
        var opts = new BotOptions { BotCount = -1 };

        var bots = BotRosterFactory.Build(opts);

        Assert.Single(bots);
    }

    [Fact]
    public void BotRosterFactory_BotCountNegativeFive_ClampsToOne()
    {
        // Any negative value — not just −1 — must clamp to 1.
        var opts = new BotOptions { BotCount = -5 };

        var bots = BotRosterFactory.Build(opts);

        Assert.Single(bots);
        Assert.Equal(1, bots[0].Index);
    }

    [Fact]
    public void BotRosterFactory_BotCountNegative_FirstBotHasIndexOne()
    {
        // When clamped to 1 bot, that single bot must have Index = 1 (1-based sequential rule).
        var opts = new BotOptions { BotCount = -100 };

        var bots = BotRosterFactory.Build(opts);

        Assert.Equal(1, bots[0].Index);
    }

    // ── BotStateValidator.Validate: stale-bot issue text format ──────────────

    [Fact]
    public void BotStateValidator_Validate_StaleBot_ExactIssueText()
    {
        // When a bot is stale (no recent success), Validate must report the exact string
        // "No successful operation in the last 10 minutes." (using the default threshold).
        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "TestBot",
            Token = "tok",
            TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2),
            Profile = new PlayerProfile { OnboardingCompletedAtUtc = DateTime.UtcNow },
            LastSuccessUtc = DateTime.UtcNow.AddHours(-1), // well past 10-minute threshold
        };

        var result = BotStateValidator.Validate(bot, staleAfterMinutes: 10);

        Assert.False(result.IsValid);
        Assert.Single(result.Issues);
        Assert.Equal("No successful operation in the last 10 minutes.", result.Issues[0]);
    }

    [Fact]
    public void BotStateValidator_Validate_StaleBot_IsInvalid_ExactlyOneIssue()
    {
        // A bot that is only stale (valid token + onboarding) must be invalid
        // with exactly one issue (the staleness message).
        var bot = new BotAccount
        {
            Index = 2,
            DisplayName = "Stale",
            Token = "valid-token",
            TokenExpiresAtUtc = DateTime.UtcNow.AddHours(3),
            Profile = new PlayerProfile { OnboardingCompletedAtUtc = DateTime.UtcNow },
            LastSuccessUtc = DateTime.UtcNow.AddMinutes(-30),
        };

        var result = BotStateValidator.Validate(bot, staleAfterMinutes: 10);

        Assert.False(result.IsValid);
        Assert.Single(result.Issues);
    }

    [Fact]
    public void BotStateValidator_Validate_StaleBot_CustomThreshold_IssueTextContainsThreshold()
    {
        // When staleAfterMinutes = 5, the issue text must say "5 minutes", not "10 minutes".
        var bot = new BotAccount
        {
            Index = 3,
            DisplayName = "StaleBot5",
            Token = "tok",
            TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2),
            Profile = new PlayerProfile { OnboardingCompletedAtUtc = DateTime.UtcNow },
            LastSuccessUtc = DateTime.UtcNow.AddMinutes(-10), // past 5-minute threshold
        };

        var result = BotStateValidator.Validate(bot, staleAfterMinutes: 5);

        Assert.False(result.IsValid);
        Assert.Contains("5 minutes", result.Issues[0], StringComparison.Ordinal);
        Assert.DoesNotContain("10 minutes", result.Issues[0], StringComparison.Ordinal);
    }

    [Fact]
    public void BotStateValidator_Validate_StaleBot_SummaryMatchesIssueJoined()
    {
        // When IsValid=false, Summary is the space-joined Issues list.
        // For a single-issue (stale) bot, Summary must equal the one issue string.
        var bot = new BotAccount
        {
            Index = 4,
            DisplayName = "SummaryBot",
            Token = "tok",
            TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2),
            Profile = new PlayerProfile { OnboardingCompletedAtUtc = DateTime.UtcNow },
            LastSuccessUtc = DateTime.UtcNow.AddHours(-2),
        };

        var result = BotStateValidator.Validate(bot, staleAfterMinutes: 10);

        Assert.Equal(result.Issues[0], result.Summary);
    }
}

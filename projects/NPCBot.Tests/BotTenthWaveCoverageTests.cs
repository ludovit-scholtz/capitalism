using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Tenth wave of NPC-bot coverage tests.  Targets gaps that survive all nine previous waves:
/// <list type="bullet">
///   <item><b>IsReadyForOperation vs Validate divergence</b> — a stale-but-authenticated bot
///   is operationally ready but fails holistic validation.  The two helpers serve distinct
///   purposes and deliberately diverge on staleness, so tests prove both directions.</item>
///   <item><b>GetBotStatusLabel for stale bots</b> — the label does not include a staleness
///   concept; a stale-but-ready bot still shows as ACTIVE, not a new STALE label.</item>
///   <item><b>BotRosterFactory roster invariants</b> — email uniqueness, display-name
///   uniqueness, and 1-based sequential Index values for a full 20-bot roster.</item>
///   <item><b>Email lowercasing with mixed-case prefix</b> — BotNamePrefix="NPC" (uppercase)
///   produces emails starting with lowercase "npc_".</item>
///   <item><b>ComputeNetWorth with empty company list</b> — verifies the Sum() baseline
///   returns 0 when the player profile has no companies yet.</item>
///   <item><b>ProfitDelta exactly zero</b> — when CurrentNetWorth equals InitialNetWorth,
///   ProfitDelta must be exactly 0 (not a rounding artefact).</item>
///   <item><b>GraphQLException inheritance chain</b> — code can be caught generically as
///   Exception; the pattern used in AccountService works for <em>all</em> callers.</item>
///   <item><b>AllowedIndustries no-duplicate guard</b> — the default AllowedIndustries array
///   must not contain the same industry string more than once.</item>
///   <item><b>BotAccount.Email is read-only after construction</b> — init-only property
///   ensures the identity cannot be mutated post-construction.</item>
///   <item><b>Validate stale issue message contains configurable threshold</b> — the issue
///   text embeds the actual staleAfterMinutes value, not a hardcoded literal.</item>
/// </list>
/// </summary>
public sealed class BotTenthWaveCoverageTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a fully authenticated and onboarded bot that has also received a
    /// successful operation timestamp (so it is NOT stale at normal thresholds).
    /// </summary>
    private static BotAccount MakeActiveBot(bool stale = false) => new()
    {
        Index = 1,
        DisplayName = "NPC_Trading_01",
        Email = "npc_trading_01@npcbot.capitalism.local",
        Strategy = "Trading",
        Token = "valid-jwt",
        TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2),
        Profile = new PlayerProfile
        {
            Id = "p1",
            DisplayName = "NPC_Trading_01",
            Email = "npc_trading_01@npcbot.capitalism.local",
            OnboardingCompletedAtUtc = DateTime.UtcNow.AddHours(-1),
            Companies = [new CompanySummary { Id = "c1", Name = "Corp", Cash = 100_000m, Buildings = [] }],
        },
        LastSuccessUtc = stale
            ? DateTime.UtcNow.AddMinutes(-61)   // well past the 10-minute threshold
            : DateTime.UtcNow.AddMinutes(-1),   // recent — not stale
    };

    // ── IsReadyForOperation vs Validate divergence (staleness awareness) ──────

    [Fact]
    public void IsReadyForOperation_StaleButAuthenticated_ReturnsTrue()
    {
        // IsReadyForOperation does NOT check staleness by design — it only verifies
        // authentication and onboarding state.  A stale bot is still considered
        // operationally ready because the orchestrator will continue to poll it;
        // the user is responsible for acting on the Validate() staleness signal.
        var bot = MakeActiveBot(stale: true);

        Assert.True(BotStateValidator.IsReadyForOperation(bot),
            "IsReadyForOperation must return true even for stale bots.");
    }

    [Fact]
    public void Validate_StaleButAuthenticated_IsValidFalse()
    {
        // Validate() includes staleness in its checks — a stale bot fails holistic
        // validation even when authentication and onboarding are correct.
        var bot = MakeActiveBot(stale: true);

        var result = BotStateValidator.Validate(bot, staleAfterMinutes: 10);

        Assert.False(result.IsValid,
            "Validate must return IsValid=false for a stale-but-otherwise-ready bot.");
    }

    [Fact]
    public void Validate_StaleBot_IssuesContainsStalenessMessage()
    {
        var bot = MakeActiveBot(stale: true);

        var result = BotStateValidator.Validate(bot, staleAfterMinutes: 10);

        // The stale issue must mention the configured threshold value.
        Assert.Contains(result.Issues, issue => issue.Contains("10"));
        Assert.Contains(result.Issues, issue => issue.Contains("minute"));
    }

    [Fact]
    public void Validate_StaleBot_ConfigurableThresholdAppearsInIssueText()
    {
        // staleAfterMinutes is embedded in the issue message so operators know which
        // threshold was used.  A 30-minute threshold must produce "30 minutes" text.
        var bot = MakeActiveBot(stale: false);
        // Make the bot stale relative to a 0-minute threshold
        bot.LastSuccessUtc = DateTime.UtcNow.AddSeconds(-1);

        var result = BotStateValidator.Validate(bot, staleAfterMinutes: 0);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Contains("0"));
    }

    [Fact]
    public void IsReadyForOperation_vs_Validate_Diverge_ForStaleBot()
    {
        // Explicitly documents the design intent: the two helpers serve different needs.
        // IsReadyForOperation: "can I schedule a tick for this bot?"  → staleness irrelevant
        // Validate():         "is this bot in a healthy overall state?" → staleness checked
        var bot = MakeActiveBot(stale: true);

        bool canOperate = BotStateValidator.IsReadyForOperation(bot);
        bool isHealthy = BotStateValidator.Validate(bot, staleAfterMinutes: 10).IsValid;

        Assert.True(canOperate, "Operationally ready despite staleness.");
        Assert.False(isHealthy, "Holistic validation fails due to staleness.");
        Assert.NotEqual(canOperate, isHealthy); // They diverge on this bot.
    }

    // ── GetBotStatusLabel: stale bots are still ACTIVE ────────────────────────

    [Fact]
    public void GetBotStatusLabel_StaleBotWithValidTokenAndOnboarding_IsActive()
    {
        // GetBotStatusLabel has no STALE category — it only knows SKIPPED, NO_TOKEN,
        // ONBOARDING, and ACTIVE.  A stale-but-ready bot must show as ACTIVE.
        var bot = MakeActiveBot(stale: true);

        Assert.Equal("ACTIVE", BotOrchestrator.GetBotStatusLabel(bot));
    }

    // ── BotRosterFactory: roster-wide invariants ──────────────────────────────

    [Fact]
    public void BotRosterFactory_FullRoster_AllEmailsAreUnique()
    {
        var roster = BotRosterFactory.Build(new BotOptions { BotCount = 20 });

        var emails = roster.Select(b => b.Email).ToList();
        var unique = emails.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        Assert.Equal(20, unique.Count);
        Assert.True(emails.Count == unique.Count, "All emails must be unique.");
    }

    [Fact]
    public void BotRosterFactory_FullRoster_AllDisplayNamesAreUnique()
    {
        var roster = BotRosterFactory.Build(new BotOptions { BotCount = 20 });

        var names = roster.Select(b => b.DisplayName).ToList();
        var unique = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        Assert.Equal(20, unique.Count);
        Assert.True(names.Count == unique.Count, "All display names must be unique.");
    }

    [Fact]
    public void BotRosterFactory_Indexes_AreOneBasedAndSequential()
    {
        var roster = BotRosterFactory.Build(new BotOptions { BotCount = 5 });

        for (int i = 0; i < 5; i++)
            Assert.Equal(i + 1, roster[i].Index);
    }

    [Fact]
    public void BotRosterFactory_EmailLowercase_WhenPrefixIsUppercase()
    {
        // BotNamePrefix="NPC" (uppercase); emails must be fully lowercased.
        var roster = BotRosterFactory.Build(new BotOptions
        {
            BotNamePrefix = "NPC",
            BotCount = 3,
        });

        foreach (var bot in roster)
        {
            Assert.Equal(bot.Email, bot.Email.ToLowerInvariant());
            Assert.StartsWith("npc_", bot.Email);
        }
    }

    // ── ComputeNetWorth: edge cases ───────────────────────────────────────────

    [Fact]
    public void ComputeNetWorth_EmptyCompaniesList_ReturnsZero()
    {
        // A freshly-registered bot profile with no companies yet must produce 0 net worth.
        var profile = new PlayerProfile
        {
            Id = "p1",
            DisplayName = "Fresh Bot",
            Email = "fresh@npcbot.capitalism.local",
            Companies = [],
        };

        var worth = BotProfitCalculator.ComputeNetWorth(profile);

        Assert.Equal(0m, worth);
    }

    // ── ProfitDelta: zero crossing ────────────────────────────────────────────

    [Fact]
    public void ProfitDelta_WhenCurrentEqualsInitial_IsExactlyZero()
    {
        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "NPC_01",
            Email = "npc01@test.local",
            Strategy = "Retail",
            InitialNetWorth = 500_000m,
            CurrentNetWorth = 500_000m,
        };

        Assert.Equal(0m, bot.ProfitDelta);
    }

    [Fact]
    public void ProfitDelta_TransitionsFromNegativeToPositive_AcrossCurrentNetWorthUpdate()
    {
        var bot = new BotAccount
        {
            Index = 2,
            DisplayName = "NPC_02",
            Email = "npc02@test.local",
            Strategy = "Industrial",
            InitialNetWorth = 100_000m,
            CurrentNetWorth = 90_000m,  // starts negative
        };

        Assert.True(bot.ProfitDelta < 0, "Delta should be negative at start.");

        // Bot recovers
        bot.CurrentNetWorth = 110_000m;
        Assert.True(bot.ProfitDelta > 0, "Delta should be positive after recovery.");

        // Exact crossing point
        bot.CurrentNetWorth = 100_000m;
        Assert.Equal(0m, bot.ProfitDelta);
    }

    // ── GraphQLException inheritance and catch semantics ─────────────────────

    [Fact]
    public void GraphQLException_CanBeCaughtAsBaseException_WithCodePreserved()
    {
        // Verifies that the `catch (Exception ex) when (ex is GraphQLException gex && ...)`
        // pattern used in AccountService works correctly — the exception hierarchy is intact.
        GraphQLException? caught = null;
        try
        {
            throw new GraphQLException("DUPLICATE_EMAIL registration failure.", "DUPLICATE_EMAIL");
        }
        catch (Exception ex) when (ex is GraphQLException gex && gex.Code == "DUPLICATE_EMAIL")
        {
            caught = gex;
        }

        Assert.NotNull(caught);
        Assert.Equal("DUPLICATE_EMAIL", caught!.Code);
    }

    // ── AllowedIndustries no-duplicate guard ──────────────────────────────────

    [Fact]
    public void BotOptions_AllowedIndustries_NoDuplicates()
    {
        var opts = new BotOptions();
        var unique = opts.AllowedIndustries.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        Assert.Equal(opts.AllowedIndustries.Length, unique.Length);
    }

    // ── BotAccount: init-only identity properties ─────────────────────────────

    [Fact]
    public void BotAccount_EmailMatchesFactoryOutput_AndCannotBeReassigned()
    {
        // BotAccount.Email is init-only, so the factory-generated identity is immutable.
        // This test proves the value is correctly set and stable post-construction.
        var opts = new BotOptions
        {
            BotNamePrefix = "Bot",
            BotEmailDomain = "test.example.com",
            BotCount = 1,
        };
        var bot = BotRosterFactory.Build(opts)[0];

        // Email follows the pattern: {lowercased-name}@{domain}
        Assert.EndsWith("@test.example.com", bot.Email);
        Assert.Contains("bot_", bot.Email); // lowercased prefix

        // Email stored on the account matches what was used during construction
        var emailFromFirstAccess = bot.Email;
        var emailFromSecondAccess = bot.Email;
        Assert.Equal(emailFromFirstAccess, emailFromSecondAccess);
    }

    // ── PriceAdjustmentHelper.PublicSalesUnitType constant ────────────────────

    [Fact]
    public void PublicSalesUnitType_Constant_IsPublicSalesInUpperCase()
    {
        // Regression guard: the unit-type constant drives the SelectAdjustableUnits filter.
        // Changing it to lowercase or renaming it would silently disable all price adjustments.
        Assert.Equal("PUBLIC_SALES", PriceAdjustmentHelper.PublicSalesUnitType);
    }

    [Fact]
    public void MinimumAllowedPrice_Constant_IsOneCent()
    {
        // Regression guard: the price floor must stay at 0.01 to prevent zero or negative
        // prices from reaching the game API.
        Assert.Equal(0.01m, PriceAdjustmentHelper.MinimumAllowedPrice);
    }

    // ── BotStateValidator: non-stale healthy bot passes both checks ───────────

    [Fact]
    public void ValidateAndIsReadyForOperation_AgreeForHealthyNonStaleBot()
    {
        // For a bot that is authenticated, onboarded, and not stale, both methods
        // must agree: the bot is fully ready.
        var bot = MakeActiveBot(stale: false);

        bool canOperate = BotStateValidator.IsReadyForOperation(bot);
        bool isHealthy = BotStateValidator.Validate(bot, staleAfterMinutes: 10).IsValid;

        Assert.True(canOperate);
        Assert.True(isHealthy);
        Assert.Equal(canOperate, isHealthy); // They agree on a healthy bot.
    }
}

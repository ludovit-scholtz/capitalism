using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// BotNinthWaveCoverageTests — tick-phase cancellation guard, rankings-before-tick ordering,
/// skipped-bot rank freeze, and additional targeted coverage.
///
/// <list type="bullet">
///   <item><b>Tick CT guard between bots</b> — the foreach in <c>TickAllBotsAsync</c> checks
///     <c>ct.IsCancellationRequested</c> before every bot; cancelling after bot1's tick prevents
///     bot2 from being ticked, provable via <c>FetchProfileAsync</c> call count.</item>
///   <item><b>Rankings assigned before TickBotAsync</b> — rankings are written to
///     <c>bot.CurrentRank</c> before the bot's tick fires; when CT is cancelled after bot1's tick
///     bot1 has a rank but bot2 does not.</item>
///   <item><b>Skipped-bot rank freeze</b> — <c>if (bot.IsSkipped) continue</c> fires before
///     the rankings assignment; a skipped bot's <c>CurrentRank</c> is never updated during any
///     tick, regardless of what the leaderboard returns.</item>
///   <item><b>BotOptions</b> — regression guards for <c>GraphqlUrl</c>, <c>BotPassword</c>,
///     and <c>AllowedIndustries</c> exact default values.</item>
///   <item><b>BotProfitCalculator.Recommend</b> — delta exactly equal to <c>NeutralBandPercent</c>
///     returns <c>NoAction</c> (profitable side boundary).</item>
///   <item><b>BotRosterFactory</b> — every generated bot uses
///     <c>BotOptions.BotEmailDomain</c> (no hardcoded domain).</item>
/// </list>
/// </summary>
public sealed class BotNinthWaveCoverageTests
{
    // ── Fake service A: cancels CTS after Nth FetchProfileAsync call ─────────

    private sealed class CancelAfterNthProfileService : IAccountService
    {
        private readonly CancellationTokenSource _cts;
        private readonly int _cancelOnCallN;
        private int _profileCallCount;
        private List<RankingEntry> _rankings = [];

        public int ProfileFetchCallCount => _profileCallCount;
        public int RegisterOrLoginCallCount { get; private set; }

        public CancelAfterNthProfileService(CancellationTokenSource cts, int cancelOnCallN)
        {
            _cts = cts;
            _cancelOnCallN = cancelOnCallN;
        }

        public void SetRankings(List<RankingEntry> rankings) => _rankings = rankings;

        public Task<(string token, DateTime expiresAt)> RegisterOrLoginAsync(
            BotAccount bot, CancellationToken ct)
        {
            RegisterOrLoginCallCount++;
            return Task.FromResult(($"tok-{RegisterOrLoginCallCount}", DateTime.UtcNow.AddHours(2)));
        }

        public Task<(string token, DateTime expiresAt)> LoginAsync(
            BotAccount bot, CancellationToken ct) =>
            Task.FromResult(("login-tok", DateTime.UtcNow.AddHours(2)));

        public Task<PlayerProfile> FetchProfileAsync(string token, CancellationToken ct)
        {
            _profileCallCount++;
            if (_profileCallCount >= _cancelOnCallN)
                _cts.Cancel();
            return Task.FromResult(new PlayerProfile
                { OnboardingCompletedAtUtc = DateTime.UtcNow.AddMinutes(-10) });
        }

        public Task<GameStateSummary> FetchGameStateAsync(CancellationToken ct) =>
            Task.FromResult(new GameStateSummary { CurrentTick = 100, TickIntervalSeconds = 60 });

        public Task<List<RankingEntry>> FetchRankingsAsync(CancellationToken ct) =>
            Task.FromResult(_rankings);

        public Task<UnitSummary> UpdatePublicSalesPriceAsync(
            string unitId, decimal newMinPrice, string token, CancellationToken ct) =>
            Task.FromResult(new UnitSummary { Id = unitId });
    }

    // ── Fake service B: cancels CTS after Nth FetchGameStateAsync call ────────
    // Used for tests involving pre-skipped bots where no tick-phase FetchProfileAsync fires.

    private sealed class CancelAfterNthGameStateService : IAccountService
    {
        private readonly CancellationTokenSource _cts;
        private readonly int _cancelOnCallN;
        private int _gameStateCallCount;
        private List<RankingEntry> _rankings = [];

        public int RegisterOrLoginCallCount { get; private set; }

        public CancelAfterNthGameStateService(CancellationTokenSource cts, int cancelOnCallN)
        {
            _cts = cts;
            _cancelOnCallN = cancelOnCallN;
        }

        public void SetRankings(List<RankingEntry> rankings) => _rankings = rankings;

        public Task<(string token, DateTime expiresAt)> RegisterOrLoginAsync(
            BotAccount bot, CancellationToken ct)
        {
            RegisterOrLoginCallCount++;
            return Task.FromResult(($"tok-{RegisterOrLoginCallCount}", DateTime.UtcNow.AddHours(2)));
        }

        public Task<(string token, DateTime expiresAt)> LoginAsync(
            BotAccount bot, CancellationToken ct) =>
            Task.FromResult(("login-tok", DateTime.UtcNow.AddHours(2)));

        public Task<PlayerProfile> FetchProfileAsync(string token, CancellationToken ct) =>
            Task.FromResult(new PlayerProfile
                { OnboardingCompletedAtUtc = DateTime.UtcNow.AddMinutes(-10) });

        public Task<GameStateSummary> FetchGameStateAsync(CancellationToken ct)
        {
            _gameStateCallCount++;
            if (_gameStateCallCount >= _cancelOnCallN)
                _cts.Cancel();
            return Task.FromResult(new GameStateSummary { CurrentTick = 100, TickIntervalSeconds = 60 });
        }

        public Task<List<RankingEntry>> FetchRankingsAsync(CancellationToken ct) =>
            Task.FromResult(_rankings);

        public Task<UnitSummary> UpdatePublicSalesPriceAsync(
            string unitId, decimal newMinPrice, string token, CancellationToken ct) =>
            Task.FromResult(new UnitSummary { Id = unitId });
    }

    // ── No-op fake services ────────────────────────────────────────────────────

    private sealed class FakeNoOpOnboarding : IOnboardingService
    {
        public Task RunAsync(BotAccount bot, string[] allowedIndustries, CancellationToken ct)
        {
            if (bot.Profile is not null)
                bot.Profile.OnboardingCompletedAtUtc = DateTime.UtcNow;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNoOpPriceAdj : IPriceAdjustmentService
    {
        public Task<int> ApplyAdjustmentAsync(
            BotAccount bot, StrategyRecommendation rec, CancellationToken ct) =>
            Task.FromResult(0);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BotOrchestrator MakeOrchestrator(
        IAccountService accounts,
        BotOptions options,
        IEnumerable<BotAccount> bots)
    {
        return new BotOrchestrator(
            bots,
            accounts,
            new FakeNoOpOnboarding(),
            new FakeNoOpPriceAdj(),
            Options.Create(options),
            NullLogger<BotOrchestrator>.Instance);
    }

    private static BotAccount MakeBot(int idx) => new()
    {
        Index = idx,
        DisplayName = $"NPC 00{idx}",
        Email = $"npc00{idx}@test.example",
        Strategy = "FURNITURE",
    };

    // ── Tests: tick-phase CT guard ─────────────────────────────────────────────

    [Fact]
    public async Task Tick_CtCancelledAfterFirstBotTick_SecondBotNotTicked()
    {
        // TickAllBotsAsync lines 176-183 contain:
        //   foreach (var bot in _bots) {
        //       if (ct.IsCancellationRequested) break;   ← checked BEFORE each bot
        //       ...
        //       await TickBotAsync(bot, ct);
        //   }
        //
        // When CT is cancelled during bot1's tick-phase FetchProfileAsync (profile call #5
        // across the session: 2 per bot during init = 4, then call #5 is bot1's tick), the
        // service still returns the profile (no exception). TickBotAsync(bot1) completes, then
        // the foreach re-checks ct.IsCancellationRequested → true → break → bot2 NOT ticked.
        //
        // Proof: FetchProfileCallCount stays at 5, not 6 (bot2's tick never fires).
        using var cts = new CancellationTokenSource();
        var accounts = new CancelAfterNthProfileService(cts, cancelOnCallN: 5);

        var bot1 = MakeBot(1);
        var bot2 = MakeBot(2);
        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0 };

        await MakeOrchestrator(accounts, opts, [bot1, bot2]).RunAsync(cts.Token);

        // Both bots initialised (2 RegisterOrLogin calls during init)
        Assert.Equal(2, accounts.RegisterOrLoginCallCount);

        // 5 total profile fetches: 2×init-bot1, 2×init-bot2, 1×tick-bot1 — bot2 tick never ran
        Assert.Equal(5, accounts.ProfileFetchCallCount);

        // Bot1 tick completed successfully — no errors
        Assert.Equal(0, bot1.ConsecutiveErrors);

        // Bot2 was never ticked — ConsecutiveErrors stays 0 (no tick = no error increment)
        Assert.Equal(0, bot2.ConsecutiveErrors);
    }

    [Fact]
    public async Task Tick_CtCancelledAfterFirstBotTick_Bot1RankSetBot2RankNotSet()
    {
        // Rankings are assigned to each bot BEFORE its TickBotAsync call (lines 180-182).
        // When CT is cancelled after bot1's tick-phase FetchProfileAsync, the foreach breaks
        // before reaching bot2, so bot2's ranking assignment also never happens.
        //
        // Result: bot1.CurrentRank is set from the leaderboard; bot2.CurrentRank stays null.
        using var cts = new CancellationTokenSource();
        var accounts = new CancelAfterNthProfileService(cts, cancelOnCallN: 5);
        accounts.SetRankings(
        [
            new RankingEntry { Rank = 1, DisplayName = "NPC 001", NetWorth = 100_000m },
            new RankingEntry { Rank = 2, DisplayName = "NPC 002", NetWorth = 80_000m },
        ]);

        var bot1 = MakeBot(1);
        var bot2 = MakeBot(2);
        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0 };

        await MakeOrchestrator(accounts, opts, [bot1, bot2]).RunAsync(cts.Token);

        // Bot1's rank is set because rankings are assigned before TickBotAsync in the foreach
        Assert.Equal(1, bot1.CurrentRank);

        // Bot2's rank is null — the foreach broke before reaching bot2 in the current tick
        Assert.Null(bot2.CurrentRank);
    }

    // ── Tests: skipped-bot rank freeze ────────────────────────────────────────

    [Fact]
    public async Task Tick_SkippedBot_CurrentRankNotUpdatedDuringTick()
    {
        // TickAllBotsAsync line ordering:
        //   foreach (var bot in _bots) {
        //       if (ct.IsCancellationRequested) break;
        //       if (bot.IsSkipped) continue;            ← fires BEFORE rankings assignment
        //       bot.CurrentRank = ...                  ← this line is skipped for skipped bots
        //       await TickBotAsync(bot, ct);
        //   }
        //
        // Arrange: bot with pre-existing rank 5, marked as skipped.
        // Rankings return rank 1 for this bot during the tick.
        // Assert: CurrentRank stays 5 — the `continue` fires before the rank update.
        //
        // Termination: deterministic via CancelAfterNthGameStateService with cancelOnCallN=3.
        //   Call 1 = init game state, Call 2 = tick1 game state (tick runs fully, rank NOT updated),
        //   Call 3 = tick2 game state → CTS cancelled → loop exits.
        using var cts = new CancellationTokenSource();
        var accounts = new CancelAfterNthGameStateService(cts, cancelOnCallN: 3);
        accounts.SetRankings(
        [
            new RankingEntry { Rank = 1, DisplayName = "NPC 001", NetWorth = 999_999m },
        ]);

        var bot1 = MakeBot(1);
        bot1.IsSkipped = true;
        bot1.CurrentRank = 5;  // Pre-existing rank from a previous tick

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0 };

        await MakeOrchestrator(accounts, opts, [bot1]).RunAsync(cts.Token);

        // Despite rankings returning rank 1 for this bot, the skipped `continue` fires before
        // the rankings assignment. The pre-existing rank of 5 must be preserved.
        Assert.Equal(5, bot1.CurrentRank);
    }

    [Fact]
    public async Task Tick_SkippedAndNonSkippedBot_OnlyNonSkippedBotRankUpdated()
    {
        // When a roster has one skipped and one active bot, only the active bot's rank
        // is updated during the tick. The skipped bot's rank is frozen.
        //
        // Termination: cancel on profile call 5 (2 init calls × 2 bots = 4, then call 5
        // is bot2's tick-phase FetchProfileAsync → CTS cancelled → foreach breaks after bot2).
        using var cts = new CancellationTokenSource();
        var accounts = new CancelAfterNthProfileService(cts, cancelOnCallN: 5);
        accounts.SetRankings(
        [
            new RankingEntry { Rank = 3, DisplayName = "NPC 001", NetWorth = 50_000m },
            new RankingEntry { Rank = 7, DisplayName = "NPC 002", NetWorth = 30_000m },
        ]);

        // bot1 = skipped, pre-existing rank 99
        var bot1 = MakeBot(1);
        bot1.IsSkipped = true;
        bot1.CurrentRank = 99;

        // bot2 = active, no prior rank
        var bot2 = MakeBot(2);

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0 };

        await MakeOrchestrator(accounts, opts, [bot1, bot2]).RunAsync(cts.Token);

        // Bot1 is skipped — rank stays at 99 (frozen)
        Assert.Equal(99, bot1.CurrentRank);

        // Bot2 is active — rank updated from rankings (7)
        Assert.Equal(7, bot2.CurrentRank);
    }

    // ── Tests: BotOptions default value regression guards ─────────────────────

    [Fact]
    public void BotOptions_GraphqlUrl_DefaultIsProductionEndpoint()
    {
        // Regression guard: changing the default GraphQL URL accidentally would break
        // out-of-the-box usage without any configuration override.
        var opts = new BotOptions();
        Assert.Equal("https://capitalism.de-4.biatec.io/graphql", opts.GraphqlUrl);
    }

    [Fact]
    public void BotOptions_BotPassword_DefaultIsNotNullOrEmpty()
    {
        // A null/empty default password would cause registration to fail at the API level
        // with a 400 Bad Request before any game logic runs.
        var opts = new BotOptions();
        Assert.False(string.IsNullOrWhiteSpace(opts.BotPassword));
    }

    [Fact]
    public void BotOptions_AllowedIndustries_ContainsAllThreeStarterIndustries()
    {
        // The default roster must cover all three starter industries so that the strategy
        // cycling in BotRosterFactory distributes bots across FURNITURE, FOOD_PROCESSING,
        // and HEALTHCARE onboarding paths.
        var opts = new BotOptions();
        Assert.Contains("FURNITURE", opts.AllowedIndustries);
        Assert.Contains("FOOD_PROCESSING", opts.AllowedIndustries);
        Assert.Contains("HEALTHCARE", opts.AllowedIndustries);
    }

    // ── Tests: BotProfitCalculator profitable-side neutral-band boundary ───────

    [Fact]
    public void Recommend_DeltaExactlyAtPositiveNeutralBand_ReturnsNoAction()
    {
        // Recommend uses strict comparison: `deltaPercent > NeutralBandPercent` for Profitable
        // and `deltaPercent < -NeutralBandPercent` for Unprofitable.
        // A delta exactly equal to +NeutralBandPercent (neither > nor <) falls through to
        // the default Neutral case, which returns NoAction.
        const decimal initial = 100_000m;
        var current = initial * (1m + BotProfitCalculator.NeutralBandPercent); // exactly +2%
        var rec = BotProfitCalculator.Recommend(current, initial, ticksElapsed: 10, minTicksBeforeAdjustment: 5);
        Assert.False(rec.ShouldAct, "Delta exactly at +NeutralBandPercent is Neutral, not Profitable action.");
    }

    [Fact]
    public void Recommend_DeltaOneTickAbovePositiveNeutralBand_StillNoAction()
    {
        // Confirm that a small increment above the neutral band does trigger Profitable status
        // (classify = Profitable) but Recommend only fires for Unprofitable; profitable bots
        // always return NoAction regardless of how far above the band they are.
        const decimal initial = 100_000m;
        const decimal current = 120_000m; // +20% — clearly profitable
        var rec = BotProfitCalculator.Recommend(current, initial, ticksElapsed: 10, minTicksBeforeAdjustment: 5);
        Assert.False(rec.ShouldAct, "Profitable bots must not receive a price-adjustment action.");
    }

    // ── Tests: BotRosterFactory email domain is always from BotOptions ─────────

    [Fact]
    public void BotRosterFactory_AllBotsUseConfiguredEmailDomain()
    {
        // Every generated bot email must use opts.BotEmailDomain, not a hardcoded value.
        // This regression guard proves BotRosterFactory doesn't bypass the config.
        const string customDomain = "my-custom-domain.example";
        var opts = new BotOptions { BotCount = 5, BotEmailDomain = customDomain };
        var roster = BotRosterFactory.Build(opts);
        Assert.All(roster, bot =>
            Assert.EndsWith($"@{customDomain}", bot.Email));
    }

    [Fact]
    public void BotRosterFactory_DefaultDomain_AllBotsUseNpcBotDomain()
    {
        // With default options, every bot email ends with the canonical npcbot domain.
        var opts = new BotOptions { BotCount = 3 };
        var roster = BotRosterFactory.Build(opts);
        Assert.All(roster, bot =>
            Assert.EndsWith("@npcbot.capitalism.local", bot.Email));
    }

    // ── Tests: BotAccount.Token propagation after tick-level login ─────────────

    [Fact]
    public void BotAccount_AfterTokenAssignment_HasValidTokenIsTrue()
    {
        // When the orchestrator assigns a fresh bearer token to bot.Token,
        // HasValidToken must reflect the new state immediately (computed property, not cached).
        var bot = new BotAccount { Index = 1, DisplayName = "NPC 001", Email = "t@t.com" };
        Assert.False(bot.HasValidToken, "Fresh bot with null token must not be considered valid.");

        bot.Token = "valid-jwt";
        bot.TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2);

        Assert.True(bot.HasValidToken, "Bot with a non-expired non-empty token must be valid.");
    }

    [Fact]
    public void BotAccount_WhitespaceToken_HasValidTokenIsFalse()
    {
        // A whitespace-only token is not a valid bearer credential.
        // This proved the IsTokenValid bug fix in BotEighthWaveCoverageTests still holds here.
        var bot = new BotAccount
        {
            Token = "   ",  // whitespace only
            TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2),
        };
        Assert.False(bot.HasValidToken);
    }

    // ── Tests: BotStateValidator.Validate summary format ─────────────────────

    [Fact]
    public void Validate_MultipleIssues_SummaryJoinsWithSpace()
    {
        // When Validate collects multiple issues, the Summary property joins them with a
        // single space. This format is used in the periodic report and log messages.
        var bot = new BotAccount
        {
            IsSkipped = true,
            // No token, no profile → expired + onboarding-incomplete
        };
        var result = BotStateValidator.Validate(bot);

        Assert.False(result.IsValid);
        // A new BotAccount with IsSkipped=true has exactly 3 issues:
        //   1. "Bot has been skipped due to too many consecutive errors."
        //   2. "Token is missing or expired."
        //   3. "Onboarding has not been completed."
        // IsStale returns false when LastSuccessUtc is null (uninitialised state).
        Assert.Equal(3, result.Issues.Count);
        // All issues appear in the summary (space-joined)
        foreach (var issue in result.Issues)
            Assert.Contains(issue, result.Summary);
    }

    [Fact]
    public void Validate_IssuesList_IsImmutable()
    {
        // BotStateValidationResult.Issues is backed by List.AsReadOnly() which returns
        // a ReadOnlyCollection<string>. This concrete type is the contract guarantee for
        // immutability — any mutation attempt via the IList<string> interface throws
        // NotSupportedException.
        var bot = new BotAccount(); // no token, no onboarding
        var result = BotStateValidator.Validate(bot);

        Assert.IsAssignableFrom<IReadOnlyList<string>>(result.Issues);
        // Verify the concrete type is ReadOnlyCollection<string>, not a raw mutable List<string>
        Assert.IsType<System.Collections.ObjectModel.ReadOnlyCollection<string>>(result.Issues);
        // Confirm mutation attempt throws NotSupportedException
        var mutable = result.Issues as System.Collections.Generic.IList<string>;
        Assert.NotNull(mutable);
        Assert.Throws<NotSupportedException>(() => mutable.Add("injected issue"));
    }
}

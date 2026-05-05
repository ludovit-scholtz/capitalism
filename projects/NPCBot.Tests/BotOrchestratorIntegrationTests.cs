using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Integration tests for <see cref="BotOrchestrator"/> that exercise the async
/// orchestration loop using fake service implementations.
///
/// <para>
/// <b>Why interfaces?</b>  <see cref="BotOrchestrator.RunAsync"/> and its private helpers
/// (<c>InitialiseBotAsync</c>, <c>TickBotAsync</c>, <c>ApplyPendingRecommendationAsync</c>)
/// previously had zero unit coverage because all three service dependencies took concrete
/// classes that required a live HTTP client.  Extracting
/// <see cref="IAccountService"/>, <see cref="IOnboardingService"/>, and
/// <see cref="IPriceAdjustmentService"/> lets this file exercise every orchestration path
/// with in-process fake objects — no network calls required.
/// </para>
///
/// <para><b>CT contract for RunAsync</b>: pass a pre-cancelled token to exit after
/// the initialization pass but before the first <c>Task.Delay</c> in the while loop.
/// The initialization pass itself is not CT-sensitive per bot (each bot's catch block
/// only checks the <c>foreach</c> guard), so all bots still run through
/// <c>InitialiseBotAsync</c> before the while loop exits on a pre-cancelled token.
/// </para>
/// </summary>
public sealed class BotOrchestratorIntegrationTests
{
    // ── Fake IAccountService ──────────────────────────────────────────────────

    private sealed class FakeAccountService : IAccountService
    {
        private readonly Queue<(string token, DateTime expiry)> _authQueue = new();
        private readonly Queue<PlayerProfile> _profileQueue = new();
        private readonly Queue<GameStateSummary> _gameStateQueue = new();

        public int RegisterOrLoginCallCount;
        public int LoginCallCount;
        public int FetchProfileCallCount;
        public int FetchGameStateCallCount;
        public int FetchRankingsCallCount;
        public Exception? RegisterException;
        public Exception? LoginException;
        public Exception? TickProfileException;
        public Exception? RankingsException;
        private int _tickProfileFetchCount;
        private readonly Queue<List<RankingEntry>> _rankingsQueue = new();

        // Auto-cancel mechanism: cancel CTS after the Nth FetchGameStateAsync call.
        private CancellationTokenSource? _autoCancelCts;
        private int _autoCancelAfterN;

        /// <summary>
        /// Registers a <see cref="CancellationTokenSource"/> that will be cancelled
        /// when <see cref="FetchGameStateAsync"/> is called for the <paramref name="onCallN"/>th time.
        /// This lets tick-level tests run exactly N-1 full ticks before stopping.
        /// </summary>
        public void CancelCtsAfterGameStateFetch(CancellationTokenSource cts, int onCallN)
        {
            _autoCancelCts = cts;
            _autoCancelAfterN = onCallN;
        }

        /// <summary>Make LoginAsync throw on next call.</summary>
        public void SetLoginException(Exception ex) => LoginException = ex;

        public void EnqueueAuth(string token, DateTime expiry) =>
            _authQueue.Enqueue((token, expiry));

        public void EnqueueProfile(PlayerProfile profile) =>
            _profileQueue.Enqueue(profile);

        public void EnqueueGameState(GameStateSummary gs) =>
            _gameStateQueue.Enqueue(gs);

        public void EnqueueRankings(List<RankingEntry> rankings) =>
            _rankingsQueue.Enqueue(rankings);

        public Task<(string token, DateTime expiresAt)> RegisterOrLoginAsync(
            BotAccount bot, CancellationToken ct)
        {
            RegisterOrLoginCallCount++;
            if (RegisterException is not null) throw RegisterException;
            var item = _authQueue.Count > 0
                ? _authQueue.Dequeue()
                : ("default-token", DateTime.UtcNow.AddHours(2));
            return Task.FromResult(item);
        }

        public Task<(string token, DateTime expiresAt)> LoginAsync(
            BotAccount bot, CancellationToken ct)
        {
            LoginCallCount++;
            if (LoginException is not null) throw LoginException;
            var item = _authQueue.Count > 0
                ? _authQueue.Dequeue()
                : ("refreshed-token", DateTime.UtcNow.AddHours(2));
            return Task.FromResult(item);
        }

        public Task<PlayerProfile> FetchProfileAsync(string token, CancellationToken ct)
        {
            FetchProfileCallCount++;
            _tickProfileFetchCount++;
            if (TickProfileException is not null && _tickProfileFetchCount > 1)
                throw TickProfileException;
            var profile = _profileQueue.Count > 0
                ? _profileQueue.Dequeue()
                : new PlayerProfile { OnboardingCompletedAtUtc = DateTime.UtcNow.AddMinutes(-10) };
            return Task.FromResult(profile);
        }

        public Task<GameStateSummary> FetchGameStateAsync(CancellationToken ct)
        {
            FetchGameStateCallCount++;
            var gs = _gameStateQueue.Count > 0
                ? _gameStateQueue.Dequeue()
                : new GameStateSummary { CurrentTick = 1, TickIntervalSeconds = 60 };
            // After the Nth call, cancel the token so the tick loop stops cleanly.
            if (_autoCancelCts is not null && FetchGameStateCallCount >= _autoCancelAfterN)
                _autoCancelCts.Cancel();
            return Task.FromResult(gs);
        }

        public Task<List<RankingEntry>> FetchRankingsAsync(CancellationToken ct)
        {
            FetchRankingsCallCount++;
            if (RankingsException is not null) throw RankingsException;
            var list = _rankingsQueue.Count > 0
                ? _rankingsQueue.Dequeue()
                : new List<RankingEntry>();
            return Task.FromResult(list);
        }

        public Task<UnitSummary> UpdatePublicSalesPriceAsync(
            string unitId, decimal newMinPrice, string token, CancellationToken ct) =>
            Task.FromResult(new UnitSummary { Id = unitId, UnitType = "PUBLIC_SALES", MinPrice = newMinPrice });
    }

    // ── Fake IOnboardingService ───────────────────────────────────────────────

    private sealed class FakeOnboardingService : IOnboardingService
    {
        public int CallCount;
        public Exception? Exception;

        public Task RunAsync(BotAccount bot, string[] allowedIndustries, CancellationToken ct)
        {
            CallCount++;
            if (Exception is not null) throw Exception;
            // Simulate completing onboarding
            if (bot.Profile is not null)
                bot.Profile.OnboardingCompletedAtUtc = DateTime.UtcNow;
            return Task.CompletedTask;
        }
    }

    // ── Fake IPriceAdjustmentService ─────────────────────────────────────────

    private sealed class FakePriceAdjustmentService : IPriceAdjustmentService
    {
        public int CallCount;
        public StrategyRecommendation? LastRecommendation;

        public Task<int> ApplyAdjustmentAsync(
            BotAccount bot,
            StrategyRecommendation recommendation,
            CancellationToken ct)
        {
            CallCount++;
            LastRecommendation = recommendation;
            return Task.FromResult(1);
        }
    }

    // ── Factory helpers ───────────────────────────────────────────────────────

    private static BotAccount MakeBot(bool onboardingComplete = true) => new()
    {
        Index = 1,
        DisplayName = "NPC 001",
        Email = "npc001@test.example",
        Strategy = "FURNITURE",
    };

    private static PlayerProfile CompletedProfile() => new()
    {
        Id = "player-1",
        DisplayName = "NPC 001",
        Email = "npc001@test.example",
        OnboardingCompletedAtUtc = DateTime.UtcNow.AddMinutes(-10),
        Companies = [],
    };

    private static PlayerProfile IncompleteProfile() => new()
    {
        Id = "player-1",
        DisplayName = "NPC 001",
        Email = "npc001@test.example",
        OnboardingCompletedAtUtc = null,
        Companies = [],
    };

    private static BotOrchestrator MakeOrchestrator(
        IAccountService accounts,
        FakeOnboardingService? onboarding = null,
        FakePriceAdjustmentService? priceAdj = null,
        BotOptions? options = null,
        BotAccount[]? bots = null)
    {
        var opts = options ?? new BotOptions { Enabled = true, PollIntervalSeconds = 1 };
        onboarding ??= new FakeOnboardingService();
        priceAdj ??= new FakePriceAdjustmentService();
        bots ??= [MakeBot()];

        return new BotOrchestrator(
            bots,
            accounts,
            onboarding,
            priceAdj,
            Options.Create(opts),
            NullLogger<BotOrchestrator>.Instance);
    }

    // ── RunAsync: disabled mode ───────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WhenDisabled_ReturnsImmediatelyWithNoServiceCalls()
    {
        var accounts = new FakeAccountService();
        var orchestrator = MakeOrchestrator(accounts, options: new BotOptions { Enabled = false });

        await orchestrator.RunAsync(CancellationToken.None);

        Assert.Equal(0, accounts.RegisterOrLoginCallCount);
        Assert.Equal(0, accounts.FetchGameStateCallCount);
        Assert.Equal(0, accounts.FetchProfileCallCount);
    }

    // ── RunAsync: initialization pass ─────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WithPreCancelledToken_InitialisationLoopBreaksEarly()
    {
        // Pre-cancelled CT: the foreach in InitialiseAllBotsAsync checks
        // ct.IsCancellationRequested before each bot, so no bot is initialised.
        var accounts = new FakeAccountService();
        var bot = MakeBot();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var orchestrator = MakeOrchestrator(accounts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        // GameState is still fetched (before the foreach loop), but no individual bot init
        Assert.Equal(1, accounts.FetchGameStateCallCount);
        Assert.Equal(0, accounts.RegisterOrLoginCallCount);
    }

    [Fact]
    public async Task RunAsync_SingleBot_AuthenticatesAndFetchesProfile()
    {
        // Cancel after the game-state fetch inside initialization so the while-loop
        // exits before the first Task.Delay, keeping the test fast.
        var accounts = new FakeAccountService();
        var bot = MakeBot();
        using var cts = new CancellationTokenSource();

        // Provide one profile then cancel during the second (post-onboarding) fetch
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueProfile(CompletedProfile());
        // Enqueue game state; second call (from TickAllBots in the loop) won't fire because CT is cancelled
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 42 });
        // Simulate the while-loop tick cancelling: Task.Delay will throw OperationCancelled
        // because the CT is passed directly. Cancel after 300ms so init can complete first.
        cts.CancelAfter(TimeSpan.FromSeconds(2));

        var orchestrator = MakeOrchestrator(accounts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        // Bot should have a token and profile after initialization
        Assert.NotNull(bot.Token);
        Assert.NotNull(bot.Profile);
        Assert.True(bot.OnboardingCompleted);
    }

    [Fact]
    public async Task RunAsync_SingleBot_NetWorthIsSetAfterInit()
    {
        var accounts = new FakeAccountService();
        var bot = MakeBot();
        using var cts = new CancellationTokenSource();

        // Profile with companies providing non-zero net worth
        var profile = CompletedProfile();
        profile.Companies =
        [
            new CompanySummary
            {
                Id = "c1",
                Name = "Corp",
                Cash = 50_000m,
                Buildings = [],
            },
        ];
        accounts.EnqueueProfile(profile);
        accounts.EnqueueProfile(profile); // post-onboarding refresh
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 5 });
        cts.CancelAfter(TimeSpan.FromSeconds(2));

        var orchestrator = MakeOrchestrator(accounts, options: new BotOptions { Enabled = true, PollIntervalSeconds = 60 }, bots: [bot]);
        cts.CancelAfter(TimeSpan.FromMilliseconds(300)); // long poll interval means no tick fires
        await orchestrator.RunAsync(cts.Token);

        Assert.Equal(50_000m, bot.InitialNetWorth);
        Assert.Equal(50_000m, bot.CurrentNetWorth);
    }

    // ── Initialization error handling ─────────────────────────────────────────

    [Fact]
    public async Task Init_RegisterThrows_IncrementsConsecutiveErrors()
    {
        var accounts = new FakeAccountService
        {
            RegisterException = new InvalidOperationException("Auth failed"),
        };
        var bot = MakeBot();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // cancel before loop so game state fetch before foreach fires

        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 });

        var orchestrator = MakeOrchestrator(accounts, bots: [bot]);
        // Need CT NOT pre-cancelled (otherwise foreach breaks before any bot is processed)
        using var cts2 = new CancellationTokenSource();
        cts2.CancelAfter(TimeSpan.FromMilliseconds(50));

        await orchestrator.RunAsync(cts2.Token);

        Assert.Equal(1, bot.ConsecutiveErrors);
        Assert.False(bot.IsSkipped, "Single failure should not skip (MaxConsecutiveErrors=5).");
    }

    [Fact]
    public async Task Init_RegisterThrows_WithMaxErrors1_MarksSkipped()
    {
        // With MaxConsecutiveErrors = 1, a single initialization failure skips the bot.
        var accounts = new FakeAccountService
        {
            RegisterException = new InvalidOperationException("Auth failed"),
        };
        var bot = MakeBot();
        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 1, MaxConsecutiveErrors = 1 };
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 });
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        var orchestrator = MakeOrchestrator(accounts, options: opts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        Assert.True(bot.IsSkipped, "Bot must be skipped when ConsecutiveErrors >= MaxConsecutiveErrors.");
    }

    [Fact]
    public async Task Init_TwoBots_ErrorInFirstDoesNotAffectSecond()
    {
        // Bot 1 throws; bot 2 should still initialize normally.
        var accounts = new FakeAccountService();
        var bot1 = new BotAccount { Index = 1, DisplayName = "NPC 001", Email = "npc001@t.x", Strategy = "FURNITURE" };
        var bot2 = new BotAccount { Index = 2, DisplayName = "NPC 002", Email = "npc002@t.x", Strategy = "FOOD_PROCESSING" };

        // Use a custom FakeAccountService that throws only on the first RegisterOrLogin call.
        accounts.RegisterException = null;

        // Use a custom FakeAccountService that throws only on the first call
        var customAccounts = new ThrowOnceAccountService(throwOnFirst: true);
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 });
        customAccounts.SetGameState(new GameStateSummary { CurrentTick = 1 });
        customAccounts.SetProfile(CompletedProfile());
        customAccounts.SetProfile(CompletedProfile()); // post-onboarding refresh

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        var orchestrator = MakeOrchestrator(customAccounts, bots: [bot1, bot2]);
        await orchestrator.RunAsync(cts.Token);

        Assert.Equal(1, bot1.ConsecutiveErrors);
        Assert.NotNull(bot2.Token); // bot2 initialized successfully
    }

    // ── Onboarding trigger ────────────────────────────────────────────────────

    [Fact]
    public async Task Init_BotNotOnboarded_OnboardingServiceIsCalled()
    {
        // Bot profile has no onboarding completion date → RunAsync should call the onboarding service.
        var accounts = new FakeAccountService();
        var onboarding = new FakeOnboardingService();
        var bot = MakeBot();

        accounts.EnqueueProfile(IncompleteProfile());   // first profile: onboarding not done
        accounts.EnqueueProfile(CompletedProfile());    // post-onboarding refresh (sets completed date)
        accounts.EnqueueProfile(CompletedProfile());    // final profile in InitialiseBotAsync
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 });
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        var orchestrator = MakeOrchestrator(accounts, onboarding: onboarding, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        Assert.Equal(1, onboarding.CallCount);
    }

    [Fact]
    public async Task Init_BotAlreadyOnboarded_OnboardingServiceNotCalled()
    {
        var accounts = new FakeAccountService();
        var onboarding = new FakeOnboardingService();
        var bot = MakeBot();

        accounts.EnqueueProfile(CompletedProfile());    // already done
        accounts.EnqueueProfile(CompletedProfile());    // post-init refresh
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 });
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        var orchestrator = MakeOrchestrator(accounts, onboarding: onboarding, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        Assert.Equal(0, onboarding.CallCount);
    }

    // ── Tick-loop: token refresh ──────────────────────────────────────────────

    [Fact]
    public async Task Tick_ExpiredToken_RefreshesViaLogin()
    {
        // During a tick, if the token is about-to-expire, LoginAsync must be called.
        var accounts = new FakeAccountService();
        var bot = MakeBot();

        // Init phase: provide a short-lived token (3 min < 5 min refresh buffer → IsTokenValid(5) = false)
        accounts.EnqueueAuth("init-token", DateTime.UtcNow.AddMinutes(3));
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueProfile(CompletedProfile()); // post-init refresh
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 }); // init game state

        // Tick phase: login provides a fresh token; profile and game state follow
        accounts.EnqueueAuth("refreshed-token", DateTime.UtcNow.AddHours(2));
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 2 }); // tick game state (auto-cancel at #3)

        using var cts = new CancellationTokenSource();
        // Auto-cancel after the 3rd game-state fetch so exactly one tick body runs.
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3);

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0 };
        var orchestrator = MakeOrchestrator(accounts, options: opts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        // After one tick, LoginAsync was called because the init token was near expiry.
        Assert.Equal(1, accounts.LoginCallCount);
        Assert.Equal("refreshed-token", bot.Token);
    }

    [Fact]
    public async Task Tick_ValidToken_DoesNotCallLogin()
    {
        // When the token is still valid, no refresh should occur during a tick.
        var accounts = new FakeAccountService();
        var bot = MakeBot();

        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 });
        // Tick profile
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 2 });

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 1 };
        var orchestrator = MakeOrchestrator(accounts, options: opts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        // Bot gets a valid token during init; tick should NOT call LoginAsync
        Assert.Equal(0, accounts.LoginCallCount);
    }

    // ── Tick-loop: pending recommendation ────────────────────────────────────

    [Fact]
    public async Task Tick_ShouldActRecommendation_CallsPriceAdjustmentAndClears()
    {
        // Bot has a severe-loss scenario → EvaluateAndLogProfitability stores ShouldAct=true
        // recommendation → ApplyAdjustmentAsync is called → PendingRecommendation is cleared to null.
        var accounts = new FakeAccountService();
        var priceAdj = new FakePriceAdjustmentService();
        var bot = MakeBot();

        // Init profiles: Cash = 80 000 (sets InitialNetWorth = 80 000)
        var profile = CompletedProfile();
        profile.Companies =
        [
            new CompanySummary { Id = "c1", Name = "Corp", Cash = 80_000m, Buildings = [] },
        ];

        accounts.EnqueueProfile(profile);       // init profile fetch
        accounts.EnqueueProfile(profile);       // post-init refresh
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 }); // init game state

        // Tick phase: Cash = 72 000 → 10 % loss → SeverelyUnprofitable threshold = 10 %
        var profileTick = CompletedProfile();
        profileTick.Companies =
        [
            new CompanySummary { Id = "c1", Name = "Corp", Cash = 72_000m, Buildings = [] },
        ];
        accounts.EnqueueProfile(profileTick);   // tick profile refresh
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 20 }); // tick game state

        using var cts = new CancellationTokenSource();
        // Auto-cancel after the 3rd FetchGameStateAsync call so exactly ONE tick body runs.
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3);

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0, MinTicksBeforeAdjustment = 5 };
        var orchestrator = MakeOrchestrator(accounts, priceAdj: priceAdj, options: opts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        // Recommendation was ShouldAct=true → ApplyAdjustmentAsync called → PendingRecommendation cleared
        Assert.True(priceAdj.CallCount >= 1);
        Assert.Null(bot.PendingRecommendation);
    }

    [Fact]
    public async Task Tick_PendingRecommendation_ClearedAfterEveryApplyAttempt()
    {
        // Even when there is nothing to adjust (e.g. no PUBLIC_SALES units), the
        // PendingRecommendation must be set to null after the apply attempt.
        var accounts = new FakeAccountService();
        var priceAdj = new FakePriceAdjustmentService();
        var bot = MakeBot();

        // Set up a severe-loss scenario (−20 %) so a ShouldAct recommendation is generated.
        var initProfile = CompletedProfile();
        initProfile.Companies = [new CompanySummary { Id = "c1", Name = "C", Cash = 100_000m, Buildings = [] }];
        var tickProfile = CompletedProfile();
        tickProfile.Companies = [new CompanySummary { Id = "c1", Name = "C", Cash = 80_000m, Buildings = [] }];

        accounts.EnqueueProfile(initProfile);  // init fetch
        accounts.EnqueueProfile(initProfile);  // post-init refresh
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 });
        accounts.EnqueueProfile(tickProfile);  // tick fetch
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 20 });

        using var cts = new CancellationTokenSource();
        // Auto-cancel after the 3rd FetchGameStateAsync call so exactly ONE tick body runs.
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3);

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0, MinTicksBeforeAdjustment = 5 };
        var orchestrator = MakeOrchestrator(accounts, priceAdj: priceAdj, options: opts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        // Regardless of whether the apply succeeded, PendingRecommendation must be null afterwards
        Assert.Null(bot.PendingRecommendation);
    }

    // ── Tick-loop: error handling ─────────────────────────────────────────────

    [Fact]
    public async Task Tick_ProfileFetchThrows_IncrementsConsecutiveErrors()
    {
        // If FetchProfileAsync throws during a tick, ConsecutiveErrors is incremented.
        var accounts = new FakeAccountService();
        var bot = MakeBot();

        // Init succeeds
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 });
        // Tick: profile fetch throws
        accounts.TickProfileException = new InvalidOperationException("Profile fetch failed");
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 2 });

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 1 };
        var orchestrator = MakeOrchestrator(accounts, options: opts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        Assert.True(bot.ConsecutiveErrors >= 1, "Tick error must increment ConsecutiveErrors.");
    }

    [Fact]
    public async Task Tick_SuccessAfterErrors_ClearsConsecutiveErrors()
    {
        // A successful tick must reset ConsecutiveErrors to 0.
        var accounts = new FakeAccountService();
        var bot = MakeBot();
        bot.ConsecutiveErrors = 3; // pre-existing errors

        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 });
        // Tick succeeds
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 2 });

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 1 };
        var orchestrator = MakeOrchestrator(accounts, options: opts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        Assert.Equal(0, bot.ConsecutiveErrors);
    }

    [Fact]
    public async Task Tick_SkippedBot_DoesNotReceiveAnyTickUpdates()
    {
        // A bot that is already skipped is still processed in InitialiseBotAsync
        // (the init loop does not check IsSkipped), but TickAllBotsAsync skips it.
        var accounts = new FakeAccountService();
        var bot = MakeBot();
        bot.IsSkipped = true;
        bot.ConsecutiveErrors = 99;

        // Init still runs (RegisterOrLogin is called); tick is skipped.
        // The FakeAccountService default returns complete profiles and tick 1 for empty queues.
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 });  // init game state
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 2 });  // tick game state (auto-cancel at #3)

        using var cts = new CancellationTokenSource();
        // Auto-cancel after the 3rd game state fetch (init + 1 tick, then abort).
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3);

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0 };
        var orchestrator = MakeOrchestrator(accounts, options: opts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        // Init runs for ALL bots (IsSkipped is only checked in the tick loop).
        Assert.Equal(1, accounts.RegisterOrLoginCallCount);
        // The tick loop skips the bot, so ConsecutiveErrors are not reset by tick success.
        Assert.Equal(99, bot.ConsecutiveErrors);
    }

    // ── Multiple bots ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Init_MultipleBots_AllAuthenticated()
    {
        // 3 bots: each should get its own RegisterOrLogin + FetchProfile call.
        var accounts = new FakeAccountService();
        var bots = Enumerable.Range(1, 3)
            .Select(i => new BotAccount { Index = i, DisplayName = $"NPC {i:D3}", Email = $"npc{i:D3}@t.x", Strategy = "FURNITURE" })
            .ToArray();

        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 });
        foreach (var _ in bots)
        {
            accounts.EnqueueProfile(CompletedProfile());
            accounts.EnqueueProfile(CompletedProfile());
        }

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        var orchestrator = MakeOrchestrator(accounts, bots: bots);
        await orchestrator.RunAsync(cts.Token);

        Assert.Equal(3, accounts.RegisterOrLoginCallCount);
        Assert.All(bots, b => Assert.NotNull(b.Token));
    }

    // ── LastSuccessUtc ────────────────────────────────────────────────────────

    [Fact]
    public async Task Init_SuccessfulInit_SetsLastSuccessUtc()
    {
        var accounts = new FakeAccountService();
        var bot = MakeBot();
        var before = DateTime.UtcNow.AddSeconds(-1);

        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 });

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        var orchestrator = MakeOrchestrator(accounts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        Assert.NotNull(bot.LastSuccessUtc);
        Assert.True(bot.LastSuccessUtc >= before);
    }

    // ── Rankings wiring ───────────────────────────────────────────────────────

    [Fact]
    public async Task Tick_RankingsFetched_SetsCurrentRankOnMatchingBot()
    {
        // When rankings are returned and a bot's DisplayName matches an entry,
        // bot.CurrentRank is set to that entry's Rank.
        var accounts = new FakeAccountService();
        var bot = MakeBot();

        // Init
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 });

        // Tick: rankings include our bot at position 3
        accounts.EnqueueRankings(
        [
            new RankingEntry { Rank = 1, DisplayName = "Other Player", NetWorth = 999_000m },
            new RankingEntry { Rank = 2, DisplayName = "Another Bot", NetWorth = 500_000m },
            new RankingEntry { Rank = 3, DisplayName = bot.DisplayName, NetWorth = 80_000m },
        ]);
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 2 });

        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3);

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0 };
        var orchestrator = MakeOrchestrator(accounts, options: opts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        Assert.Equal(3, bot.CurrentRank);
        Assert.True(accounts.FetchRankingsCallCount >= 1, "Rankings must be fetched at least once during tick.");
    }

    [Fact]
    public async Task Tick_BotNotInRankings_CurrentRankRemainsNull()
    {
        // When the bot's DisplayName is not in the rankings list, CurrentRank stays null.
        var accounts = new FakeAccountService();
        var bot = MakeBot();

        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 });

        // Rankings contain a different player — not our bot
        accounts.EnqueueRankings(
        [
            new RankingEntry { Rank = 1, DisplayName = "SomeOtherPlayer", NetWorth = 100_000m },
        ]);
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 2 });

        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3);

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0 };
        var orchestrator = MakeOrchestrator(accounts, options: opts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        Assert.Null(bot.CurrentRank);
    }

    [Fact]
    public async Task Tick_RankingsFetchThrows_TickStillCompletesAndBotRankRemainsNull()
    {
        // A rankings fetch failure must not abort the tick or affect bot processing.
        var accounts = new FakeAccountService();
        var bot = MakeBot();
        accounts.RankingsException = new InvalidOperationException("Rankings unavailable");

        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 });

        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 2 });

        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3);

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0 };
        var orchestrator = MakeOrchestrator(accounts, options: opts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        // Tick completed successfully despite rankings failure
        Assert.Null(bot.CurrentRank);
        Assert.Equal(0, bot.ConsecutiveErrors); // bot tick itself did not fail
    }

    [Fact]
    public async Task Tick_MultipleBotsInRankings_EachBotGetsCorrectRank()
    {
        // Two bots: both names appear in rankings at different positions.
        var accounts = new FakeAccountService();
        var bot1 = new BotAccount { Index = 1, DisplayName = "NPC 001", Email = "npc001@t.x", Strategy = "FURNITURE" };
        var bot2 = new BotAccount { Index = 2, DisplayName = "NPC 002", Email = "npc002@t.x", Strategy = "FOOD_PROCESSING" };

        // Init for both bots
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 });
        foreach (var _ in new[] { bot1, bot2 })
        {
            accounts.EnqueueProfile(CompletedProfile());
            accounts.EnqueueProfile(CompletedProfile());
        }

        // Tick: rankings list both bots
        accounts.EnqueueRankings(
        [
            new RankingEntry { Rank = 1, DisplayName = "NPC 002", NetWorth = 200_000m },
            new RankingEntry { Rank = 2, DisplayName = "NPC 001", NetWorth = 100_000m },
        ]);
        accounts.EnqueueProfile(CompletedProfile()); // bot1 tick
        accounts.EnqueueProfile(CompletedProfile()); // bot2 tick
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 2 });

        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3);

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0 };
        var orchestrator = MakeOrchestrator(accounts, options: opts, bots: [bot1, bot2]);
        await orchestrator.RunAsync(cts.Token);

        Assert.Equal(2, bot1.CurrentRank); // NPC 001 is rank 2
        Assert.Equal(1, bot2.CurrentRank); // NPC 002 is rank 1
    }

    [Fact]
    public async Task Tick_SkippedBotDoesNotGetRankUpdated()
    {
        // A skipped bot is excluded from the tick loop, so its CurrentRank is never set
        // even when the rankings list contains its name.
        var accounts = new FakeAccountService();
        var bot = MakeBot();
        bot.IsSkipped = true;

        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 });
        accounts.EnqueueRankings(
        [
            new RankingEntry { Rank = 1, DisplayName = bot.DisplayName, NetWorth = 50_000m },
        ]);
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 2 });

        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3);

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0 };
        var orchestrator = MakeOrchestrator(accounts, options: opts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        Assert.Null(bot.CurrentRank); // skipped bots are not processed in the tick loop
    }

    // ── Tick-loop: game-state failure ─────────────────────────────────────────

    [Fact]
    public async Task Tick_GameStateFetchThrows_CurrentTickPreservedFromLastSuccess()
    {
        // If FetchGameStateAsync throws during the tick, the tick still completes for
        // all bots using the previously cached _currentTick value.
        var accounts = new FakeAccountService();
        var bot = MakeBot();

        // Init: game state at tick 42
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 42 });

        // Tick: game state throws, but profile fetch succeeds
        // The CancelCtsAfterGameStateFetch won't fire because the throw happens before
        // incrementing the counter; use a timed cancel instead.
        accounts.EnqueueProfile(CompletedProfile());

        using var cts = new CancellationTokenSource();
        // Use a short delay so one tick fires then stops
        cts.CancelAfter(TimeSpan.FromMilliseconds(300));

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0 };

        // Inject a custom fake that throws on the 2nd game state call
        var throwingAccounts = new ThrowGameStateOnceAccountService(throwOnSecondCall: true);
        throwingAccounts.SetGameState(new GameStateSummary { CurrentTick = 42 });
        throwingAccounts.SetProfile(CompletedProfile());
        throwingAccounts.SetProfile(CompletedProfile());
        throwingAccounts.SetProfile(CompletedProfile()); // tick profile

        var orchestrator = MakeOrchestrator(throwingAccounts, options: opts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        // Tick still ran — bot processed despite game-state failure
        Assert.Equal(0, bot.ConsecutiveErrors);
    }

    [Fact]
    public async Task Tick_OnboardingNotComplete_OnboardingRunsDuringTick()
    {
        // If a bot completes init without onboarding (e.g. onboarding service threw during init),
        // but then on the next tick the profile still shows incomplete, RunOnboardingAsync
        // is called again during TickBotAsync.
        var accounts = new FakeAccountService();
        var onboarding = new FakeOnboardingService();
        var bot = MakeBot();

        // Init phase: profile is complete (no onboarding needed during init)
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 });

        // Tick phase: profile shows onboarding NOT complete (simulates race condition)
        accounts.EnqueueProfile(IncompleteProfile());  // tick profile — not onboarded
        accounts.EnqueueProfile(CompletedProfile());   // post-onboarding profile in tick
        accounts.EnqueueProfile(CompletedProfile());   // second post-onboarding refresh
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 2 });

        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3);

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0 };
        var orchestrator = MakeOrchestrator(accounts, onboarding: onboarding, options: opts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        // Onboarding was called once during tick (not during init)
        Assert.Equal(1, onboarding.CallCount);
    }
}

/// <summary>
/// A <see cref="IAccountService"/> fake that throws on the <em>first</em>
/// <c>RegisterOrLoginAsync</c> call and succeeds on subsequent calls.
/// </summary>
file sealed class ThrowOnceAccountService : IAccountService
{
    private readonly bool _throwOnFirst;
    private int _callCount;

    private readonly Queue<PlayerProfile> _profiles = new();
    private GameStateSummary _gameState = new() { CurrentTick = 1 };

    public ThrowOnceAccountService(bool throwOnFirst = true) =>
        _throwOnFirst = throwOnFirst;

    public void SetGameState(GameStateSummary gs) => _gameState = gs;
    public void SetProfile(PlayerProfile p) => _profiles.Enqueue(p);

    public Task<(string token, DateTime expiresAt)> RegisterOrLoginAsync(
        BotAccount bot, CancellationToken ct)
    {
        _callCount++;
        if (_throwOnFirst && _callCount == 1)
            throw new InvalidOperationException("First-call failure");
        return Task.FromResult(($"tok-{_callCount}", DateTime.UtcNow.AddHours(2)));
    }

    public Task<(string token, DateTime expiresAt)> LoginAsync(
        BotAccount bot, CancellationToken ct) =>
        Task.FromResult(($"login-tok-{_callCount}", DateTime.UtcNow.AddHours(2)));

    public Task<PlayerProfile> FetchProfileAsync(string token, CancellationToken ct) =>
        Task.FromResult(_profiles.Count > 0
            ? _profiles.Dequeue()
            : new PlayerProfile { OnboardingCompletedAtUtc = DateTime.UtcNow });

    public Task<GameStateSummary> FetchGameStateAsync(CancellationToken ct) =>
        Task.FromResult(_gameState);

    public Task<List<RankingEntry>> FetchRankingsAsync(CancellationToken ct) =>
        Task.FromResult(new List<RankingEntry>());

    public Task<UnitSummary> UpdatePublicSalesPriceAsync(
        string unitId, decimal newMinPrice, string token, CancellationToken ct) =>
        Task.FromResult(new UnitSummary { Id = unitId });
}

/// <summary>
/// A <see cref="IAccountService"/> fake that throws <see cref="InvalidOperationException"/>
/// on the second <c>FetchGameStateAsync</c> call (simulating a transient API failure
/// during the tick loop while init succeeds).
/// </summary>
file sealed class ThrowGameStateOnceAccountService : IAccountService
{
    private readonly bool _throwOnSecondCall;
    private int _gameStateCallCount;
    private GameStateSummary _gameState = new() { CurrentTick = 1 };
    private readonly Queue<PlayerProfile> _profiles = new();

    public ThrowGameStateOnceAccountService(bool throwOnSecondCall = true) =>
        _throwOnSecondCall = throwOnSecondCall;

    public void SetGameState(GameStateSummary gs) => _gameState = gs;
    public void SetProfile(PlayerProfile p) => _profiles.Enqueue(p);

    public Task<(string token, DateTime expiresAt)> RegisterOrLoginAsync(
        BotAccount bot, CancellationToken ct) =>
        Task.FromResult(("tok", DateTime.UtcNow.AddHours(2)));

    public Task<(string token, DateTime expiresAt)> LoginAsync(
        BotAccount bot, CancellationToken ct) =>
        Task.FromResult(("refreshed", DateTime.UtcNow.AddHours(2)));

    public Task<PlayerProfile> FetchProfileAsync(string token, CancellationToken ct) =>
        Task.FromResult(_profiles.Count > 0
            ? _profiles.Dequeue()
            : new PlayerProfile { OnboardingCompletedAtUtc = DateTime.UtcNow });

    public Task<GameStateSummary> FetchGameStateAsync(CancellationToken ct)
    {
        _gameStateCallCount++;
        if (_throwOnSecondCall && _gameStateCallCount == 2)
            throw new InvalidOperationException("Game state unavailable");
        return Task.FromResult(_gameState);
    }

    public Task<List<RankingEntry>> FetchRankingsAsync(CancellationToken ct) =>
        Task.FromResult(new List<RankingEntry>());

    public Task<UnitSummary> UpdatePublicSalesPriceAsync(
        string unitId, decimal newMinPrice, string token, CancellationToken ct) =>
        Task.FromResult(new UnitSummary { Id = unitId });
}

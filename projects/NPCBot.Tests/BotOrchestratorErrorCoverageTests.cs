using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Additional integration tests for <see cref="BotOrchestrator"/> focusing on
/// error-handling paths that are exercised during the tick loop rather than
/// during initialization.
///
/// Coverage areas:
/// <list type="bullet">
/// <item>LoginAsync failure during tick-level token refresh</item>
/// <item>MaxConsecutiveErrors threshold reached via tick failures → IsSkipped</item>
/// <item>Error isolation: one bot's tick failure must not affect sibling bots</item>
/// <item>ConsecutiveErrors not reset by successful init (only by successful tick)</item>
/// </list>
/// </summary>
public sealed class BotOrchestratorErrorCoverageTests
{
    // ── Minimal fake IAccountService ──────────────────────────────────────────

    private sealed class FakeAccountService : IAccountService
    {
        private readonly Queue<(string token, DateTime expiry)> _authQueue = new();
        public int LoginCallCount;
        public Exception? LoginException;

        // Auto-cancel mechanism: cancel CTS after the Nth FetchGameStateAsync call.
        private CancellationTokenSource? _autoCancelCts;
        private int _autoCancelAfterN;
        private int _fetchGameStateCount;

        public void EnqueueAuth(string token, DateTime expiry) =>
            _authQueue.Enqueue((token, expiry));

        public void CancelCtsAfterGameStateFetch(CancellationTokenSource cts, int onCallN)
        {
            _autoCancelCts = cts;
            _autoCancelAfterN = onCallN;
        }

        public Task<(string token, DateTime expiresAt)> RegisterOrLoginAsync(
            BotAccount bot, CancellationToken ct)
        {
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
            return Task.FromResult(("refreshed-token", DateTime.UtcNow.AddHours(2)));
        }

        public Task<PlayerProfile> FetchProfileAsync(string token, CancellationToken ct) =>
            Task.FromResult(new PlayerProfile
            {
                Id = "player-1",
                DisplayName = "NPC 001",
                Email = "npc001@test.example",
                OnboardingCompletedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                Companies = [],
            });

        public Task<GameStateSummary> FetchGameStateAsync(CancellationToken ct)
        {
            _fetchGameStateCount++;
            if (_autoCancelCts is not null && _fetchGameStateCount >= _autoCancelAfterN)
                _autoCancelCts.Cancel();
            return Task.FromResult(new GameStateSummary { CurrentTick = _fetchGameStateCount, TickIntervalSeconds = 60 });
        }

        public Task<List<RankingEntry>> FetchRankingsAsync(CancellationToken ct) =>
            Task.FromResult(new List<RankingEntry>());

        public Task<UnitSummary> UpdatePublicSalesPriceAsync(
            string unitId, decimal newMinPrice, string token, CancellationToken ct) =>
            Task.FromResult(new UnitSummary { Id = unitId });
    }

    private sealed class FakeOnboardingService : IOnboardingService
    {
        public Task RunAsync(BotAccount bot, string[] allowedIndustries, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class FakePriceAdjustmentService : IPriceAdjustmentService
    {
        public Task<int> ApplyAdjustmentAsync(BotAccount bot, StrategyRecommendation rec, CancellationToken ct) =>
            Task.FromResult(0);
    }

    private static BotAccount MakeBot() => new()
    {
        Index = 1,
        DisplayName = "NPC 001",
        Email = "npc001@test.example",
        Strategy = "FURNITURE",
    };

    private static BotOrchestrator MakeOrchestrator(
        FakeAccountService accounts,
        BotOptions? options = null,
        BotAccount[]? bots = null) =>
        new BotOrchestrator(
            bots ?? [MakeBot()],
            accounts,
            new FakeOnboardingService(),
            new FakePriceAdjustmentService(),
            Options.Create(options ?? new BotOptions { Enabled = true, PollIntervalSeconds = 0 }),
            NullLogger<BotOrchestrator>.Instance);

    // ── Login failure during tick ─────────────────────────────────────────────

    [Fact]
    public async Task Tick_LoginThrows_IncrementsConsecutiveErrors()
    {
        // When the bot's token is near-expired and LoginAsync throws during a tick,
        // ConsecutiveErrors must be incremented by exactly 1.
        var accounts = new FakeAccountService();
        var bot = MakeBot();

        // Give bot a short-lived token (3 min < 5 min buffer) so LoginAsync is called during tick.
        accounts.EnqueueAuth("short-token", DateTime.UtcNow.AddMinutes(3));

        // After init + 1 tick, cancel; don't run a 2nd full tick.
        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3); // init + tick + abort

        // LoginAsync always throws.
        accounts.LoginException = new InvalidOperationException("Login server down");

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0 };
        var orchestrator = MakeOrchestrator(accounts, options: opts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        Assert.Equal(1, bot.ConsecutiveErrors);
        Assert.Equal(1, accounts.LoginCallCount);
        Assert.False(bot.IsSkipped, "One failure must not skip the bot (MaxConsecutiveErrors=5).");
    }

    [Fact]
    public async Task Tick_LoginThrows_ConsecutiveErrorsNotResetByFailedTick()
    {
        // Confirm that a failed tick DOES NOT reset ConsecutiveErrors to zero —
        // only a successful tick resets them.
        var accounts = new FakeAccountService();
        var bot = MakeBot();
        bot.ConsecutiveErrors = 2; // pre-existing errors from a previous session

        accounts.EnqueueAuth("short-token", DateTime.UtcNow.AddMinutes(3));

        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3);

        accounts.LoginException = new InvalidOperationException("Login failed");

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0 };
        var orchestrator = MakeOrchestrator(accounts, options: opts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        // Pre-existing 2 + 1 new tick failure = 3
        Assert.Equal(3, bot.ConsecutiveErrors);
    }

    // ── MaxConsecutiveErrors via tick failures ────────────────────────────────

    [Fact]
    public async Task Tick_MaxConsecutiveErrors_ViaLoginFailures_MarksSkipped()
    {
        // With MaxConsecutiveErrors=2 and login always failing during ticks,
        // the bot must become IsSkipped=true after 2 tick failures.
        var accounts = new FakeAccountService();
        var bot = MakeBot();

        // Short-lived token → LoginAsync is called every tick
        accounts.EnqueueAuth("short-token", DateTime.UtcNow.AddMinutes(3));
        accounts.LoginException = new InvalidOperationException("Login server unavailable");

        // Run 2 full ticks before aborting: init (call 1) + tick 1 (call 2) + tick 2 (call 3) + abort (call 4).
        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 4);

        var opts = new BotOptions
        {
            Enabled = true,
            PollIntervalSeconds = 0,
            MaxConsecutiveErrors = 2,
        };
        var orchestrator = MakeOrchestrator(accounts, options: opts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        Assert.True(bot.IsSkipped,
            "Bot must be skipped after ConsecutiveErrors reaches MaxConsecutiveErrors (2).");
        Assert.True(bot.ConsecutiveErrors >= 2,
            $"ConsecutiveErrors ({bot.ConsecutiveErrors}) must be >= MaxConsecutiveErrors (2).");
        Assert.Equal(2, accounts.LoginCallCount);
    }

    [Fact]
    public async Task Tick_MaxConsecutiveErrors_OneBelowThreshold_NotSkipped()
    {
        // MaxConsecutiveErrors=3: after 2 tick failures the bot must NOT be skipped.
        var accounts = new FakeAccountService();
        var bot = MakeBot();

        accounts.EnqueueAuth("short-token", DateTime.UtcNow.AddMinutes(3));
        accounts.LoginException = new InvalidOperationException("Intermittent failure");

        // Run 2 ticks: init (1) + tick1 (2) + tick2 (3) + abort (4)
        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 4);

        var opts = new BotOptions
        {
            Enabled = true,
            PollIntervalSeconds = 0,
            MaxConsecutiveErrors = 3, // threshold is 3, only 2 failures → not skipped
        };
        var orchestrator = MakeOrchestrator(accounts, options: opts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        Assert.Equal(2, bot.ConsecutiveErrors);
        Assert.False(bot.IsSkipped, "Bot with 2 errors must not be skipped when MaxConsecutiveErrors=3.");
    }

    // ── Error isolation at tick level ─────────────────────────────────────────

    [Fact]
    public async Task Tick_OneBotLoginFails_SiblingBotUnaffected()
    {
        // Bot1 has a near-expired token (LoginAsync is called → throws).
        // Bot2 has a long-lived token (LoginAsync is never called → tick succeeds).
        // After one tick: bot1.ConsecutiveErrors==1, bot2.ConsecutiveErrors==0.
        var accounts = new FakeAccountService();

        // Bot1: short-lived token (triggers LoginAsync during tick)
        var bot1 = new BotAccount
        {
            Index = 1, DisplayName = "NPC 001", Email = "npc001@test.example", Strategy = "FURNITURE",
        };
        // Bot2: long-lived token supplied via default (AddHours(2) → no refresh needed)
        var bot2 = new BotAccount
        {
            Index = 2, DisplayName = "NPC 002", Email = "npc002@test.example", Strategy = "FOOD_PROCESSING",
        };

        // Enqueue a short-lived auth that bot1 will pick up from RegisterOrLoginAsync.
        // Bot2's RegisterOrLoginAsync uses the default (2-hour token).
        accounts.EnqueueAuth("short-token-b1", DateTime.UtcNow.AddMinutes(3));

        // LoginAsync always throws — but only bot1 triggers it (bot2's token is valid).
        accounts.LoginException = new InvalidOperationException("Login server down");

        // Run 1 tick: init (1) + tick (2) + abort (3)
        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3);

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0 };
        var orchestrator = MakeOrchestrator(accounts, options: opts, bots: [bot1, bot2]);
        await orchestrator.RunAsync(cts.Token);

        Assert.Equal(1, bot1.ConsecutiveErrors);
        Assert.Equal(0, bot2.ConsecutiveErrors);
        Assert.False(bot1.IsSkipped, "Single failure must not skip bot1.");
        Assert.False(bot2.IsSkipped, "Bot2 must not be skipped — it had a successful tick.");
    }

    [Fact]
    public async Task Tick_BothBotsLoginFail_BothIncrementErrorsIndependently()
    {
        // Both bots have near-expired tokens; LoginAsync throws for both.
        // After one tick, each bot's ConsecutiveErrors is 1 (independent counters).
        var accounts = new FakeAccountService();

        var bot1 = new BotAccount
        {
            Index = 1, DisplayName = "NPC 001", Email = "npc001@test.example", Strategy = "FURNITURE",
        };
        var bot2 = new BotAccount
        {
            Index = 2, DisplayName = "NPC 002", Email = "npc002@test.example", Strategy = "HEALTHCARE",
        };

        // Both get short-lived tokens from the auth queue.
        accounts.EnqueueAuth("short-token-1", DateTime.UtcNow.AddMinutes(3));
        accounts.EnqueueAuth("short-token-2", DateTime.UtcNow.AddMinutes(3));

        accounts.LoginException = new InvalidOperationException("Global auth failure");

        // Run 1 tick: init (1) + tick (2) + abort (3)
        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3);

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0 };
        var orchestrator = MakeOrchestrator(accounts, options: opts, bots: [bot1, bot2]);
        await orchestrator.RunAsync(cts.Token);

        // Each bot independently incremented its own error counter.
        Assert.Equal(1, bot1.ConsecutiveErrors);
        Assert.Equal(1, bot2.ConsecutiveErrors);
    }

    // ── Successful init does not reset ConsecutiveErrors ─────────────────────

    [Fact]
    public async Task Init_SuccessfulInit_DoesNotResetPreExistingConsecutiveErrors()
    {
        // ConsecutiveErrors from a previous session are carried into this session.
        // A successful init does NOT reset them — only a successful tick does.
        var accounts = new FakeAccountService();
        var bot = MakeBot();
        bot.ConsecutiveErrors = 3; // pre-existing errors from earlier

        // Use a long-lived token so no tick fires (only init).
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 60 }; // 60s delay → no tick fires
        var orchestrator = MakeOrchestrator(accounts, options: opts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        // Init succeeded, but ConsecutiveErrors was NOT reset.
        Assert.Equal(3, bot.ConsecutiveErrors);
    }

    [Fact]
    public async Task Tick_SuccessfulTick_ResetsConsecutiveErrors()
    {
        // A bot with pre-existing ConsecutiveErrors (e.g., from a previous bot run) should have
        // them reset to zero after a single successful tick with a valid, long-lived token.
        // This proves that successful ticks always clear the error counter.
        var accounts = new FakeAccountService();
        var bot = MakeBot();
        // Pre-set errors as if previous runs had failures (init success does NOT reset them).
        bot.ConsecutiveErrors = 2;

        // Default RegisterOrLoginAsync gives a 2-hour token → valid for the tick → no login refresh.
        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3); // init + 1 successful tick + abort

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0 };
        var orchestrator = MakeOrchestrator(accounts, options: opts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        // The successful tick sets bot.ConsecutiveErrors = 0.
        Assert.Equal(0, bot.ConsecutiveErrors);
    }

    // ── Skipped-bot during tick leaves ConsecutiveErrors unchanged ────────────

    [Fact]
    public async Task Tick_SkippedBot_ConsecutiveErrorsUnchangedByTickLoop()
    {
        // A bot that is already skipped must not have its ConsecutiveErrors modified
        // during the tick loop (neither incremented nor reset).
        var accounts = new FakeAccountService();
        var bot = MakeBot();
        bot.IsSkipped = true;
        bot.ConsecutiveErrors = 7;

        // Run 1 tick: init (1) + tick (2) + abort (3)
        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3);

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0 };
        var orchestrator = MakeOrchestrator(accounts, options: opts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        // ConsecutiveErrors must stay exactly at 7 — the tick loop never touched it.
        Assert.Equal(7, bot.ConsecutiveErrors);
        Assert.True(bot.IsSkipped);
    }
}

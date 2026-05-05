using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Additional orchestrator integration tests covering edge cases not addressed in the
/// main <see cref="BotOrchestratorIntegrationTests"/> suite:
/// <list type="bullet">
/// <item>Zero-bot roster — RunAsync completes without touching any service</item>
/// <item>Rank cleared when bot drops out of the rankings list between ticks</item>
/// <item>Rank updates correctly when it changes across consecutive ticks</item>
/// <item>All bots pre-skipped — tick loop fires and fetches rankings but skips every bot</item>
/// <item>CurrentNetWorth updated from fresh profile on each tick</item>
/// <item>BotStateValidator.IsAtRisk edge case: maxConsecutiveErrors = 0</item>
/// </list>
/// </summary>
public sealed class BotOrchestratorAdvancedTests
{
    // ── Minimal fake implementations ──────────────────────────────────────────

    private sealed class FakeAccountService : IAccountService
    {
        private readonly Queue<(string token, DateTime expiry)> _authQueue = new();
        private readonly Queue<PlayerProfile> _profileQueue = new();
        private readonly Queue<GameStateSummary> _gameStateQueue = new();
        private readonly Queue<List<RankingEntry>> _rankingsQueue = new();

        public int RegisterOrLoginCallCount;
        public int FetchGameStateCallCount;
        public int FetchRankingsCallCount;

        private CancellationTokenSource? _autoCancelCts;
        private int _autoCancelAfterN;

        public void CancelCtsAfterGameStateFetch(CancellationTokenSource cts, int onCallN)
        {
            _autoCancelCts = cts;
            _autoCancelAfterN = onCallN;
        }

        public void EnqueueAuth(string token, DateTime expiry) => _authQueue.Enqueue((token, expiry));
        public void EnqueueProfile(PlayerProfile profile) => _profileQueue.Enqueue(profile);
        public void EnqueueGameState(GameStateSummary gs) => _gameStateQueue.Enqueue(gs);
        public void EnqueueRankings(List<RankingEntry> rankings) => _rankingsQueue.Enqueue(rankings);

        public Task<(string token, DateTime expiresAt)> RegisterOrLoginAsync(BotAccount bot, CancellationToken ct)
        {
            RegisterOrLoginCallCount++;
            var item = _authQueue.Count > 0
                ? _authQueue.Dequeue()
                : ("default-token", DateTime.UtcNow.AddHours(2));
            return Task.FromResult(item);
        }

        public Task<(string token, DateTime expiresAt)> LoginAsync(BotAccount bot, CancellationToken ct) =>
            Task.FromResult(("refreshed-token", DateTime.UtcNow.AddHours(2)));

        public Task<PlayerProfile> FetchProfileAsync(string token, CancellationToken ct)
        {
            var profile = _profileQueue.Count > 0
                ? _profileQueue.Dequeue()
                : CompletedProfile();
            return Task.FromResult(profile);
        }

        public Task<GameStateSummary> FetchGameStateAsync(CancellationToken ct)
        {
            FetchGameStateCallCount++;
            if (_autoCancelCts is not null && FetchGameStateCallCount >= _autoCancelAfterN)
                _autoCancelCts.Cancel();
            var gs = _gameStateQueue.Count > 0
                ? _gameStateQueue.Dequeue()
                : new GameStateSummary { CurrentTick = FetchGameStateCallCount, TickIntervalSeconds = 60 };
            return Task.FromResult(gs);
        }

        public Task<List<RankingEntry>> FetchRankingsAsync(CancellationToken ct)
        {
            FetchRankingsCallCount++;
            var list = _rankingsQueue.Count > 0
                ? _rankingsQueue.Dequeue()
                : new List<RankingEntry>();
            return Task.FromResult(list);
        }

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
        public int CallCount;

        public Task<int> ApplyAdjustmentAsync(BotAccount bot, StrategyRecommendation rec, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(0);
        }
    }

    private static PlayerProfile CompletedProfile(decimal cash = 100_000m) => new()
    {
        Id = "player-1",
        DisplayName = "NPC 001",
        Email = "npc001@test.example",
        OnboardingCompletedAtUtc = DateTime.UtcNow.AddDays(-1),
        Companies = [new CompanySummary { Id = "comp-1", Name = "Bot Corp", Cash = cash, Buildings = [] }],
    };

    private static BotAccount MakeBot(string displayName = "NPC 001") => new()
    {
        Index = 1,
        DisplayName = displayName,
        Email = $"{displayName.ToLowerInvariant().Replace(' ', '.')}@test.example",
        Strategy = "FURNITURE",
    };

    private static BotOrchestrator MakeOrchestrator(
        IEnumerable<BotAccount> bots,
        FakeAccountService accounts,
        FakePriceAdjustmentService? priceAdj = null,
        BotOptions? options = null) =>
        new BotOrchestrator(
            bots.ToArray(),
            accounts,
            new FakeOnboardingService(),
            priceAdj ?? new FakePriceAdjustmentService(),
            Options.Create(options ?? new BotOptions { Enabled = true, PollIntervalSeconds = 0 }),
            NullLogger<BotOrchestrator>.Instance);

    // ── Zero-bot roster ───────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ZeroBots_CompletesWithoutError()
    {
        // When the bot list is empty, RunAsync must return without touching any service
        // and without throwing. This is a guard against roster misconfiguration.
        var accounts = new FakeAccountService();

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // stop immediately

        var orchestrator = MakeOrchestrator([], accounts);
        await orchestrator.RunAsync(cts.Token); // must not throw

        Assert.Equal(0, accounts.RegisterOrLoginCallCount);
        Assert.Equal(0, accounts.FetchRankingsCallCount);
    }

    // ── Rank cleared when bot drops out of rankings ───────────────────────────

    [Fact]
    public async Task Tick_BotPreviouslyRanked_RankClearedWhenNotInSubsequentRankings()
    {
        // Bot starts with CurrentRank = 3 (e.g., set in a previous session or init).
        // The tick fetches rankings that do NOT include the bot.
        // After the tick, CurrentRank must be null — the "unknown" state.
        var accounts = new FakeAccountService();
        var bot = MakeBot();
        bot.CurrentRank = 3; // pre-set from a previous tick or session

        // Enqueue two profiles (init fetch + tick fetch)
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 10 });

        // Rankings for this tick: bot is NOT in the list
        accounts.EnqueueRankings(
        [
            new RankingEntry { Rank = 1, DisplayName = "TopPlayer", NetWorth = 999_000m },
            new RankingEntry { Rank = 2, DisplayName = "AnotherBot", NetWorth = 500_000m },
        ]);

        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3); // init + 1 tick + abort

        var orchestrator = MakeOrchestrator([bot], accounts);
        await orchestrator.RunAsync(cts.Token);

        Assert.Null(bot.CurrentRank);
    }

    // ── Rank changes between consecutive ticks ────────────────────────────────

    [Fact]
    public async Task Tick_RankChanges_BetweenConsecutiveTicks_UpdatesToLatestRank()
    {
        // Tick 1: bot is ranked 3rd.
        // Tick 2: bot is ranked 7th (slipped down the leaderboard).
        // After tick 2, CurrentRank must be 7.
        var accounts = new FakeAccountService();
        var bot = MakeBot();

        // Init fetch (RegisterOrLogin + FetchProfile)
        accounts.EnqueueProfile(CompletedProfile());

        // Tick 1 profile + game state
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 });
        accounts.EnqueueRankings(
        [
            new RankingEntry { Rank = 1, DisplayName = "Player A", NetWorth = 999_000m },
            new RankingEntry { Rank = 2, DisplayName = "Player B", NetWorth = 800_000m },
            new RankingEntry { Rank = 3, DisplayName = bot.DisplayName, NetWorth = 100_000m },
        ]);

        // Tick 2 profile + game state
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 2 });
        accounts.EnqueueRankings(
        [
            new RankingEntry { Rank = 1, DisplayName = "Player A", NetWorth = 1_100_000m },
            new RankingEntry { Rank = 2, DisplayName = "Player B", NetWorth = 900_000m },
            new RankingEntry { Rank = 3, DisplayName = "Player C", NetWorth = 700_000m },
            new RankingEntry { Rank = 4, DisplayName = "Player D", NetWorth = 600_000m },
            new RankingEntry { Rank = 5, DisplayName = "Player E", NetWorth = 500_000m },
            new RankingEntry { Rank = 6, DisplayName = "Player F", NetWorth = 400_000m },
            new RankingEntry { Rank = 7, DisplayName = bot.DisplayName, NetWorth = 100_000m },
        ]);

        // Run init + 2 ticks (abort after 4th game-state call: init=1, tick1=2, tick2=3, abort=4)
        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 4);

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0 };
        var orchestrator = MakeOrchestrator([bot], accounts, options: opts);
        await orchestrator.RunAsync(cts.Token);

        Assert.Equal(7, bot.CurrentRank);
    }

    // ── All bots pre-skipped ──────────────────────────────────────────────────

    [Fact]
    public async Task Tick_AllBotsPreSkipped_RankingsFetchedButNoTickBotRunsAndNoPriceAdjustment()
    {
        // Even when all bots are already skipped, the tick loop still runs and fetches rankings.
        // However, no TickBotAsync is executed (all bots bypass the foreach body).
        // PriceAdjustmentService must receive 0 calls.
        var accounts = new FakeAccountService();
        var priceAdj = new FakePriceAdjustmentService();

        var bot1 = MakeBot("NPC 001"); bot1.IsSkipped = true;
        var bot2 = new BotAccount
        {
            Index = 2, DisplayName = "NPC 002", Email = "npc002@test.example",
            Strategy = "HEALTHCARE", IsSkipped = true,
        };

        // Enqueue profiles for init (two bots, even pre-skipped bots go through init)
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 5 });

        // After the tick game-state fetch, cancel.
        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3); // init(1) + tick(2) + abort(3)

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0 };
        var orchestrator = MakeOrchestrator([bot1, bot2], accounts, priceAdj, opts);
        await orchestrator.RunAsync(cts.Token);

        // Rankings must still be fetched during the tick (the call happens before the foreach)
        Assert.True(accounts.FetchRankingsCallCount >= 1,
            "Rankings must be fetched even when all bots are pre-skipped.");

        // No price adjustment called (all bots skipped)
        Assert.Equal(0, priceAdj.CallCount);
    }

    // ── CurrentNetWorth updated from fresh profile each tick ──────────────────

    [Fact]
    public async Task Tick_CurrentNetWorthUpdatedFromFreshProfile()
    {
        // During init the bot's net worth is set from the second FetchProfileAsync call
        // (InitialiseBotAsync calls FetchProfileAsync twice: once to check onboarding status,
        // then again to record the definitive initial net worth).
        // During each subsequent tick, FetchProfileAsync is called again and CurrentNetWorth
        // must reflect the new profile's company cash sum.
        var accounts = new FakeAccountService();
        var bot = MakeBot();

        // Init: 2 FetchProfileAsync calls (1st = onboarding check, 2nd = definitive init)
        accounts.EnqueueProfile(CompletedProfile(cash: 100_000m)); // 1st call (onboarding check)
        accounts.EnqueueProfile(CompletedProfile(cash: 100_000m)); // 2nd call (sets InitialNetWorth)

        // Tick 1: 1 FetchProfileAsync call (updates CurrentNetWorth)
        accounts.EnqueueProfile(CompletedProfile(cash: 120_000m)); // tick profile (bot made money)
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 10 });

        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3); // init(1) + tick(2) + abort(3)

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0 };
        var orchestrator = MakeOrchestrator([bot], accounts, options: opts);
        await orchestrator.RunAsync(cts.Token);

        // InitialNetWorth set during init (100_000), CurrentNetWorth updated during tick (120_000)
        Assert.Equal(100_000m, bot.InitialNetWorth);
        Assert.Equal(120_000m, bot.CurrentNetWorth);
        Assert.Equal(20_000m, bot.ProfitDelta);
    }

    // ── BotStateValidator: IsAtRisk with maxConsecutiveErrors=0 ──────────────

    [Fact]
    public void IsAtRisk_MaxConsecutiveErrorsIsZero_ReturnsTrueForNonZeroErrors()
    {
        // When the limit is configured as 0, any non-zero error count means the bot
        // is at risk (n/0 = ∞ ≥ 0.5). This edge case must not throw a DivideByZeroException.
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC 001", Email = "npc001@test.example",
            Strategy = "FURNITURE", ConsecutiveErrors = 1,
        };

        // This must not throw (double division returns PositiveInfinity, which ≥ 0.5 = true)
        bool result = BotStateValidator.IsAtRisk(bot, maxConsecutiveErrors: 0);
        Assert.True(result, "Any non-zero errors relative to a limit of 0 means at-risk.");
    }

    [Fact]
    public void IsAtRisk_MaxConsecutiveErrorsIsZero_BotWithZeroErrors_ReturnsFalse()
    {
        // ConsecutiveErrors == 0 means !bot.ConsecutiveErrors > 0 is false → returns false
        // regardless of the maxConsecutiveErrors value (including 0).
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC 001", Email = "npc001@test.example",
            Strategy = "FURNITURE", ConsecutiveErrors = 0,
        };

        Assert.False(BotStateValidator.IsAtRisk(bot, maxConsecutiveErrors: 0));
    }

    // ── Rank set on multiple bots simultaneously ──────────────────────────────

    [Fact]
    public async Task Tick_ThreeBots_EachGetsCorrectRankFromSameRankingsList()
    {
        // Three bots are all in the rankings. After one tick, each bot should have
        // the rank that matches its DisplayName in the returned list.
        var accounts = new FakeAccountService();
        var bot1 = MakeBot("NPC 001");
        var bot2 = new BotAccount { Index = 2, DisplayName = "NPC 002", Email = "npc002@test.example", Strategy = "FOOD_PROCESSING" };
        var bot3 = new BotAccount { Index = 3, DisplayName = "NPC 003", Email = "npc003@test.example", Strategy = "HEALTHCARE" };

        // Profiles for init (3 bots)
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueProfile(CompletedProfile());

        // Tick 1: profiles + game state + rankings
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 3 });

        accounts.EnqueueRankings(
        [
            new RankingEntry { Rank = 1, DisplayName = "ExternalPlayer", NetWorth = 999_000m },
            new RankingEntry { Rank = 2, DisplayName = bot2.DisplayName, NetWorth = 200_000m },
            new RankingEntry { Rank = 3, DisplayName = bot3.DisplayName, NetWorth = 150_000m },
            new RankingEntry { Rank = 4, DisplayName = bot1.DisplayName, NetWorth = 100_000m },
        ]);

        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3); // init(1) + tick(2) + abort(3)

        var opts = new BotOptions { Enabled = true, PollIntervalSeconds = 0 };
        var orchestrator = MakeOrchestrator([bot1, bot2, bot3], accounts, options: opts);
        await orchestrator.RunAsync(cts.Token);

        Assert.Equal(4, bot1.CurrentRank);
        Assert.Equal(2, bot2.CurrentRank);
        Assert.Equal(3, bot3.CurrentRank);
    }

    // ── Rank: rankings return empty list (no entries) ─────────────────────────

    [Fact]
    public async Task Tick_EmptyRankingsList_AllBotsCurrentRankRemainsNull()
    {
        // When the rankings API returns an empty list (e.g., before any player has traded),
        // all bot CurrentRank values must remain null — no assertion errors, no defaults.
        var accounts = new FakeAccountService();
        var bot = MakeBot();
        bot.CurrentRank = 5; // pre-set value that should be cleared

        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 });
        accounts.EnqueueRankings([]); // empty list

        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3); // init + 1 tick + abort

        var orchestrator = MakeOrchestrator([bot], accounts);
        await orchestrator.RunAsync(cts.Token);

        Assert.Null(bot.CurrentRank);
    }

    // ── MinTicksBeforeAdjustment: first tick after tracking start never adjusts ──

    [Fact]
    public async Task Tick_OnlyOneTickElapsed_PriceAdjustmentNotCalledDespiteLoss()
    {
        // With minTicksBeforeAdjustment=5, a severely losing bot that has only been
        // tracking for 1 tick must not trigger a price adjustment.
        var accounts = new FakeAccountService();
        var priceAdj = new FakePriceAdjustmentService();
        var bot = MakeBot();

        // Init: bot starts with 100_000, set TrackingStartTick via first game state
        accounts.EnqueueProfile(CompletedProfile(cash: 100_000m));

        // Tick 1: severe loss (−15%), only 1 tick elapsed → below MinTicksBeforeAdjustment
        accounts.EnqueueProfile(CompletedProfile(cash: 85_000m)); // −15%
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 });

        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3); // init(1) + tick(2) + abort(3)

        var opts = new BotOptions
        {
            Enabled = true,
            PollIntervalSeconds = 0,
            MinTicksBeforeAdjustment = 5, // requires at least 5 ticks
        };
        var orchestrator = MakeOrchestrator([bot], accounts, priceAdj, opts);
        await orchestrator.RunAsync(cts.Token);

        // TrackingStartTick = 1 (set during init), currentTick = 1 in tick loop → 0 ticks elapsed
        // → Recommend returns NoAction → PriceAdjustmentService NOT called
        Assert.Equal(0, priceAdj.CallCount);
    }
}

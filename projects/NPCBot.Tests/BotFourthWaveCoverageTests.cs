using System.Net;
using System.Text;
using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Fourth coverage wave — tests for paths genuinely absent from all prior waves:
/// <list type="bullet">
///   <item>
///     <see cref="GameApiClient"/>: empty <c>errors</c> array (bug-fix regression guard),
///     non-array <c>errors</c> field, null-value <c>errors</c> field, and
///     <c>"data": null</c> (deserialisation error path).
///   </item>
///   <item>
///     <see cref="AccountService"/> bearer-token forwarding:
///     <c>FetchProfileAsync</c> attaches the supplied token;
///     <c>FetchGameStateAsync</c>, <c>FetchRankingsAsync</c>, <c>LoginAsync</c>, and
///     <c>RegisterOrLoginAsync</c> do NOT attach a token (unauthenticated calls).
///   </item>
///   <item>
///     Multi-tick orchestrator integration: two consecutive unprofitable ticks trigger
///     two separate price-adjustment calls; profitable → unprofitable transition triggers
///     exactly one call; two profitable ticks trigger zero calls.
///   </item>
///   <item>
///     <see cref="BotAccount.InitialNetWorth"/> is set during initialisation and is NOT
///     overwritten by subsequent tick profile refreshes.
///   </item>
///   <item>
///     Three-bot scenario: only the single unprofitable bot's price adjustment is called.
///   </item>
/// </list>
/// </summary>
public sealed class BotFourthWaveCoverageTests
{
    // ── Infrastructure: fake HTTP handlers ────────────────────────────────────

    private sealed class FakeHttpHandler(Func<HttpResponseMessage> factory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(factory());
    }

    /// <summary>Captures the last outbound request for header inspection.</summary>
    private sealed class CapturingHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _factory;

        public CapturingHttpHandler(Func<HttpResponseMessage> factory)
            => _factory = factory;

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken _)
        {
            LastRequest = request;
            return Task.FromResult(_factory());
        }
    }

    private static HttpResponseMessage OkJson(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    // ── GameApiClient factory ─────────────────────────────────────────────────

    private static (GameApiClient client, FakeHttpHandler handler) MakeGameApiClient(
        Func<HttpResponseMessage> factory)
    {
        var h = new FakeHttpHandler(factory);
        var opts = Options.Create(new BotOptions { GraphqlUrl = "https://fake.example/graphql" });
        var client = new GameApiClient(new HttpClient(h), opts, NullLogger<GameApiClient>.Instance);
        return (client, h);
    }

    // ── AccountService factory (capturing) ───────────────────────────────────

    private static (AccountService svc, CapturingHttpHandler handler) MakeAccountService(
        Func<HttpResponseMessage> factory)
    {
        var h = new CapturingHttpHandler(factory);
        var opts = Options.Create(new BotOptions
        {
            BotPassword = "test-pass-123",
            GraphqlUrl = "https://fake.example/graphql",
        });
        var api = new GameApiClient(new HttpClient(h), opts, NullLogger<GameApiClient>.Instance);
        var svc = new AccountService(api, opts, NullLogger<AccountService>.Instance);
        return (svc, h);
    }

    private sealed record SimpleWrapper(string Value);

    // ═══════════════════════════════════════════════════════════════════════════
    // GameApiClient: empty / non-array / null errors field
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Regression guard for the empty-errors-array bug:
    /// before the fix <c>{"errors":[]}</c> caused <see cref="IndexOutOfRangeException"/>
    /// because <c>ParseFirstError</c> tried to access <c>errors[0]</c> on an empty array.
    /// After the fix the empty array is skipped and <c>data</c> is deserialised normally.
    /// </summary>
    [Fact]
    public async Task GameApiClient_EmptyErrorsArray_TreatedAsNoErrors_DataIsReturned()
    {
        const string json = """{"errors":[],"data":{"value":"hello"}}""";
        var (client, _) = MakeGameApiClient(() => OkJson(json));

        var result = await client.ExecuteAsync<SimpleWrapper>("{ value }", ct: CancellationToken.None);

        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public async Task GameApiClient_ErrorsIsNonArray_TreatedAsNoErrors_DataIsReturned()
    {
        // A non-conforming server may return errors as an object instead of an array.
        // The guard (ValueKind == Array) skips it and processes data normally.
        const string json = """{"errors":{"message":"ignore"},"data":{"value":"ok"}}""";
        var (client, _) = MakeGameApiClient(() => OkJson(json));

        var result = await client.ExecuteAsync<SimpleWrapper>("{ value }", ct: CancellationToken.None);

        Assert.Equal("ok", result.Value);
    }

    [Fact]
    public async Task GameApiClient_ErrorsIsNull_TreatedAsNoErrors_DataIsReturned()
    {
        // "errors": null is also not an array; guard skips it.
        const string json = """{"errors":null,"data":{"value":"skipped-null-errors"}}""";
        var (client, _) = MakeGameApiClient(() => OkJson(json));

        var result = await client.ExecuteAsync<SimpleWrapper>("{ value }", ct: CancellationToken.None);

        Assert.Equal("skipped-null-errors", result.Value);
    }

    /// <summary>
    /// <c>"data": null</c> is a valid GraphQL response for a null top-level field.
    /// The client deserialises <c>null</c> into a reference type and the
    /// null-coalescing throw produces "Deserialisation returned null."
    /// </summary>
    [Fact]
    public async Task GameApiClient_NullDataValue_ThrowsInvalidOperationException()
    {
        const string json = """{"data":null}""";
        var (client, _) = MakeGameApiClient(() => OkJson(json));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ExecuteAsync<SimpleWrapper>("{ value }", ct: CancellationToken.None));

        Assert.Contains("Deserialisation returned null", ex.Message);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // AccountService: bearer-token forwarding / absence
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AccountService_FetchProfileAsync_AttachesBearerTokenInRequest()
    {
        const string json = """
            {"data":{"me":{"id":"p1","displayName":"NPC","email":"n@e.com",
            "onboardingCompletedAtUtc":null,"companies":[]}}}
            """;
        var (svc, handler) = MakeAccountService(() => OkJson(json));

        await svc.FetchProfileAsync("my-bearer-token", CancellationToken.None);

        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("my-bearer-token", handler.LastRequest?.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task AccountService_FetchProfileAsync_DifferentTokenValue_IsForwarded()
    {
        // Verify the token value is dynamic (not hardcoded or cached).
        const string json = """
            {"data":{"me":{"id":"p2","displayName":"X","email":"x@e.com",
            "onboardingCompletedAtUtc":null,"companies":[]}}}
            """;
        var (svc, handler) = MakeAccountService(() => OkJson(json));

        await svc.FetchProfileAsync("token-ALPHA", CancellationToken.None);
        Assert.Equal("token-ALPHA", handler.LastRequest?.Headers.Authorization?.Parameter);

        await svc.FetchProfileAsync("token-BETA", CancellationToken.None);
        Assert.Equal("token-BETA", handler.LastRequest?.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task AccountService_FetchGameStateAsync_DoesNotAttachBearerToken()
    {
        const string json = """
            {"data":{"gameState":{"currentTick":42,"tickIntervalSeconds":30,"taxCycleTicks":8760}}}
            """;
        var (svc, handler) = MakeAccountService(() => OkJson(json));

        await svc.FetchGameStateAsync(CancellationToken.None);

        Assert.Null(handler.LastRequest?.Headers.Authorization);
    }

    [Fact]
    public async Task AccountService_FetchRankingsAsync_DoesNotAttachBearerToken()
    {
        const string json = """
            {"data":{"rankings":[{"rank":1,"displayName":"Top","score":9999}]}}
            """;
        var (svc, handler) = MakeAccountService(() => OkJson(json));

        await svc.FetchRankingsAsync(CancellationToken.None);

        Assert.Null(handler.LastRequest?.Headers.Authorization);
    }

    [Fact]
    public async Task AccountService_LoginAsync_DoesNotAttachBearerToken()
    {
        const string json = """
            {"data":{"login":{"token":"tok","expiresAtUtc":"2099-01-01T00:00:00Z",
            "player":{"id":"p1","displayName":"Bot","email":"b@e.com",
            "onboardingCompletedAtUtc":null}}}}
            """;
        var (svc, handler) = MakeAccountService(() => OkJson(json));
        var bot = new BotAccount { Index = 1, Email = "b@e.com", Strategy = "FURNITURE" };

        await svc.LoginAsync(bot, CancellationToken.None);

        Assert.Null(handler.LastRequest?.Headers.Authorization);
    }

    [Fact]
    public async Task AccountService_RegisterOrLoginAsync_DoesNotAttachBearerToken()
    {
        const string json = """
            {"data":{"register":{"token":"tok","expiresAtUtc":"2099-01-01T00:00:00Z",
            "player":{"id":"p1","displayName":"NPC","email":"npc@e.com",
            "onboardingCompletedAtUtc":null}}}}
            """;
        var (svc, handler) = MakeAccountService(() => OkJson(json));
        var bot = new BotAccount { Index = 1, Email = "npc@e.com", Strategy = "FURNITURE" };

        await svc.RegisterOrLoginAsync(bot, CancellationToken.None);

        Assert.Null(handler.LastRequest?.Headers.Authorization);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Multi-tick orchestrator integration (local fake service implementations)
    // ═══════════════════════════════════════════════════════════════════════════

    private sealed class FakeAccountService : IAccountService
    {
        // Default fallback values when the enqueued queue is exhausted.
        // All well-formed tests enqueue enough items so these should not be reached.
        private static readonly PlayerProfile DefaultProfile = new()
        {
            OnboardingCompletedAtUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        private static readonly GameStateSummary DefaultGameState = new()
        {
            CurrentTick = 100,
            TickIntervalSeconds = 60,
        };

        private readonly Queue<PlayerProfile> _profiles = new();
        private readonly Queue<GameStateSummary> _gameStates = new();
        private CancellationTokenSource? _cts;
        private int _cancelOnN;
        private int _gsCallCount;

        public void EnqueueProfile(PlayerProfile p) => _profiles.Enqueue(p);

        public void EnqueueGameState(GameStateSummary gs) => _gameStates.Enqueue(gs);

        public void CancelOnGameStateCallN(CancellationTokenSource cts, int n)
        {
            _cts = cts;
            _cancelOnN = n;
        }

        public Task<(string token, DateTime expiresAt)> RegisterOrLoginAsync(
            BotAccount bot, CancellationToken ct) =>
            Task.FromResult(("tok", DateTime.UtcNow.AddHours(2)));

        public Task<(string token, DateTime expiresAt)> LoginAsync(
            BotAccount bot, CancellationToken ct) =>
            Task.FromResult(("tok", DateTime.UtcNow.AddHours(2)));

        public Task<PlayerProfile> FetchProfileAsync(string token, CancellationToken ct)
        {
            var p = _profiles.Count > 0 ? _profiles.Dequeue() : DefaultProfile;
            return Task.FromResult(p);
        }

        public Task<GameStateSummary> FetchGameStateAsync(CancellationToken ct)
        {
            _gsCallCount++;
            if (_cts is not null && _gsCallCount >= _cancelOnN)
                _cts.Cancel();
            var gs = _gameStates.Count > 0 ? _gameStates.Dequeue() : DefaultGameState;
            return Task.FromResult(gs);
        }

        public Task<List<RankingEntry>> FetchRankingsAsync(CancellationToken ct) =>
            Task.FromResult(new List<RankingEntry>());

        public Task<UnitSummary> UpdatePublicSalesPriceAsync(
            string unitId, decimal newMinPrice, string token, CancellationToken ct) =>
            Task.FromResult(new UnitSummary { Id = unitId, MinPrice = newMinPrice });
    }

    private sealed class FakeOnboardingService : IOnboardingService
    {
        public Task RunAsync(BotAccount bot, string[] allowedIndustries, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class CountingPriceAdjustmentService : IPriceAdjustmentService
    {
        public int CallCount { get; private set; }

        public Task<int> ApplyAdjustmentAsync(
            BotAccount bot, StrategyRecommendation recommendation, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(1);
        }
    }

    private static PlayerProfile CompletedProfile(decimal cash) => new()
    {
        Id = "p1",
        DisplayName = "NPC 001",
        Email = "npc@test.example",
        OnboardingCompletedAtUtc = DateTime.UtcNow.AddMinutes(-5),
        Companies = [new CompanySummary { Id = "c1", Name = "Corp", Cash = cash }],
    };

    private static PlayerProfile CompletedProfileMultiBot(string displayName, decimal cash) => new()
    {
        Id = Guid.NewGuid().ToString(),
        DisplayName = displayName,
        Email = $"{displayName.ToLowerInvariant()}@test.example",
        OnboardingCompletedAtUtc = DateTime.UtcNow.AddMinutes(-5),
        Companies = [new CompanySummary { Id = Guid.NewGuid().ToString(), Name = "Corp", Cash = cash }],
    };

    private static BotOrchestrator MakeOrchestrator(
        FakeAccountService accounts,
        CountingPriceAdjustmentService priceAdj,
        BotAccount[]? bots = null)
    {
        bots ??= [new BotAccount
        {
            Index = 1,
            DisplayName = "NPC 001",
            Email = "npc@test.example",
            Strategy = "FURNITURE",
        }];

        var opts = Options.Create(new BotOptions
        {
            Enabled = true,
            PollIntervalSeconds = 1,
            MinTicksBeforeAdjustment = 5,
        });

        return new BotOrchestrator(
            bots,
            accounts,
            new FakeOnboardingService(),
            priceAdj,
            opts,
            NullLogger<BotOrchestrator>.Instance);
    }

    // Game-state factory helpers used by multi-tick tests.
    // Init uses tick=0 so TrackingStartTick=0; ticks use tick=100 so ticksElapsed=100 >> MinTicksBeforeAdjustment.
    private static GameStateSummary InitGameState =>
        new() { CurrentTick = 0, TickIntervalSeconds = 60 };

    private static GameStateSummary TickGameState =>
        new() { CurrentTick = 100, TickIntervalSeconds = 60 };

    [Fact]
    public async Task TwoConsecutiveUnprofitableTicks_PriceAdjustmentCalledTwice()
    {
        using var cts = new CancellationTokenSource();
        var accounts = new FakeAccountService();
        var priceAdj = new CountingPriceAdjustmentService();
        var orchestrator = MakeOrchestrator(accounts, priceAdj);

        // Cancel on the 4th FetchGameStateAsync (start of tick 3) so we get exactly 2 full ticks.
        // TrackingStartTick=0 (from init), _currentTick=100 in ticks → ticksElapsed=100 > 5.
        accounts.CancelOnGameStateCallN(cts, 4);
        accounts.EnqueueGameState(InitGameState); // init: TrackingStartTick=0
        accounts.EnqueueGameState(TickGameState); // tick 1: _currentTick=100
        accounts.EnqueueGameState(TickGameState); // tick 2: _currentTick=100

        // Profiles: init onboarding-check + net-worth, then one per tick.
        accounts.EnqueueProfile(CompletedProfile(100_000m)); // init: onboarding check
        accounts.EnqueueProfile(CompletedProfile(100_000m)); // init: initial net worth = 100k
        accounts.EnqueueProfile(CompletedProfile(50_000m));  // tick 1: -50% → severely unprofitable
        accounts.EnqueueProfile(CompletedProfile(40_000m));  // tick 2: -60% → severely unprofitable

        await orchestrator.RunAsync(cts.Token);

        Assert.Equal(2, priceAdj.CallCount);
    }

    [Fact]
    public async Task ProfitableFirstTick_ThenUnprofitableSecondTick_PriceAdjustmentCalledOnce()
    {
        using var cts = new CancellationTokenSource();
        var accounts = new FakeAccountService();
        var priceAdj = new CountingPriceAdjustmentService();
        var orchestrator = MakeOrchestrator(accounts, priceAdj);

        accounts.CancelOnGameStateCallN(cts, 4);
        accounts.EnqueueGameState(InitGameState);
        accounts.EnqueueGameState(TickGameState);
        accounts.EnqueueGameState(TickGameState);

        accounts.EnqueueProfile(CompletedProfile(100_000m)); // init onboarding check
        accounts.EnqueueProfile(CompletedProfile(100_000m)); // init net worth = 100k
        accounts.EnqueueProfile(CompletedProfile(120_000m)); // tick 1: +20% → profitable → NO adj
        accounts.EnqueueProfile(CompletedProfile(80_000m));  // tick 2: -20% → unprofitable → adj

        await orchestrator.RunAsync(cts.Token);

        Assert.Equal(1, priceAdj.CallCount);
    }

    [Fact]
    public async Task TwoConsecutiveProfitableTicks_NoPriceAdjustmentCalls()
    {
        using var cts = new CancellationTokenSource();
        var accounts = new FakeAccountService();
        var priceAdj = new CountingPriceAdjustmentService();
        var orchestrator = MakeOrchestrator(accounts, priceAdj);

        accounts.CancelOnGameStateCallN(cts, 4);
        accounts.EnqueueGameState(InitGameState);
        accounts.EnqueueGameState(TickGameState);
        accounts.EnqueueGameState(TickGameState);

        accounts.EnqueueProfile(CompletedProfile(100_000m)); // init
        accounts.EnqueueProfile(CompletedProfile(100_000m)); // init net worth = 100k
        accounts.EnqueueProfile(CompletedProfile(115_000m)); // tick 1: +15% → profitable
        accounts.EnqueueProfile(CompletedProfile(125_000m)); // tick 2: +25% → profitable

        await orchestrator.RunAsync(cts.Token);

        Assert.Equal(0, priceAdj.CallCount);
    }

    [Fact]
    public async Task ThreeBots_OnlyUnprofitableBot_PriceAdjustmentCalledOnce()
    {
        using var cts = new CancellationTokenSource();
        var accounts = new FakeAccountService();
        var priceAdj = new CountingPriceAdjustmentService();

        // Three bots with different profitability profiles.
        var bot1 = new BotAccount { Index = 1, DisplayName = "NPC 001", Email = "npc001@t.e", Strategy = "FURNITURE" };
        var bot2 = new BotAccount { Index = 2, DisplayName = "NPC 002", Email = "npc002@t.e", Strategy = "FOOD_PROCESSING" };
        var bot3 = new BotAccount { Index = 3, DisplayName = "NPC 003", Email = "npc003@t.e", Strategy = "HEALTHCARE" };

        var orchestrator = MakeOrchestrator(accounts, priceAdj, [bot1, bot2, bot3]);

        // Cancel on the 3rd FetchGameStateAsync (start of tick 2) → exactly 1 full tick.
        accounts.CancelOnGameStateCallN(cts, 3);
        accounts.EnqueueGameState(InitGameState); // init: TrackingStartTick=0
        accounts.EnqueueGameState(TickGameState); // tick 1: ticksElapsed=100

        // Init: 6 profile fetches (2 per bot: onboarding check + net worth).
        accounts.EnqueueProfile(CompletedProfileMultiBot("NPC 001", 100_000m));
        accounts.EnqueueProfile(CompletedProfileMultiBot("NPC 001", 100_000m));
        accounts.EnqueueProfile(CompletedProfileMultiBot("NPC 002", 100_000m));
        accounts.EnqueueProfile(CompletedProfileMultiBot("NPC 002", 100_000m));
        accounts.EnqueueProfile(CompletedProfileMultiBot("NPC 003", 100_000m));
        accounts.EnqueueProfile(CompletedProfileMultiBot("NPC 003", 100_000m));

        // Tick 1: 3 profile fetches (one per bot).
        accounts.EnqueueProfile(CompletedProfileMultiBot("NPC 001", 50_000m));  // -50% → unprofitable
        accounts.EnqueueProfile(CompletedProfileMultiBot("NPC 002", 115_000m)); // +15% → profitable
        accounts.EnqueueProfile(CompletedProfileMultiBot("NPC 003", 105_000m)); // +5%  → profitable

        await orchestrator.RunAsync(cts.Token);

        // Only bot 1 should trigger a price adjustment.
        Assert.Equal(1, priceAdj.CallCount);
    }

    [Fact]
    public async Task TwoTicks_InitialNetWorth_PreservedAcrossTicksNotOverwritten()
    {
        // InitialNetWorth is set once during init and must NOT be overwritten
        // by subsequent tick profile refreshes (unless mid-tick onboarding fires).
        using var cts = new CancellationTokenSource();
        var accounts = new FakeAccountService();
        var priceAdj = new CountingPriceAdjustmentService();
        var bot = new BotAccount { Index = 1, DisplayName = "NPC 001", Email = "npc@t.e", Strategy = "FURNITURE" };
        var orchestrator = MakeOrchestrator(accounts, priceAdj, [bot]);

        accounts.CancelOnGameStateCallN(cts, 4);
        accounts.EnqueueGameState(InitGameState);
        accounts.EnqueueGameState(TickGameState);
        accounts.EnqueueGameState(TickGameState);

        const decimal initialCash = 200_000m;
        accounts.EnqueueProfile(CompletedProfile(initialCash));
        accounts.EnqueueProfile(CompletedProfile(initialCash)); // InitialNetWorth = 200k
        accounts.EnqueueProfile(CompletedProfile(180_000m));    // tick 1 profile
        accounts.EnqueueProfile(CompletedProfile(160_000m));    // tick 2 profile

        await orchestrator.RunAsync(cts.Token);

        // After two ticks the InitialNetWorth must still be the value from init.
        Assert.Equal(initialCash, bot.InitialNetWorth);
        // CurrentNetWorth reflects the latest profile.
        Assert.Equal(160_000m, bot.CurrentNetWorth);
    }
}

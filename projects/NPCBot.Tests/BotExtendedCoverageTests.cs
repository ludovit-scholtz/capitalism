using System.Net;
using System.Text;
using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Extended coverage tests addressing uncovered paths across multiple components.
/// <list type="bullet">
///   <item><b>OnboardingService</b> — FOOD_PROCESSING and HEALTHCARE single-industry happy paths
///   (companion to the existing FURNITURE coverage; ROADMAP requires all three free starter
///   industries to work end-to-end).</item>
///   <item><b>PriceAdjustmentService</b> — CT cancelled <em>mid-loop</em> (after first unit
///   processed, before second) so the per-unit <c>if (ct.IsCancellationRequested) break;</c>
///   guard inside the foreach loop is exercised.</item>
///   <item><b>BotOrchestrator init</b> — game-state fetch failure during initialisation (all bots
///   still initialised with <c>_currentTick = 0</c>), and CT cancelled between bots so the second
///   bot's <c>InitialiseBotAsync</c> is never reached.</item>
///   <item><b>BotProfitCalculator</b> — Classify with current == initial (exact zero-% delta)
///   returns Neutral.</item>
/// </list>
/// </summary>
public sealed class BotExtendedCoverageTests
{
    // ══════════════════════════════════════════════════════════════════════════
    //  OnboardingService helpers
    // ══════════════════════════════════════════════════════════════════════════

    private sealed class QueuedHttpHandler(params HttpResponseMessage[] responses)
        : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _queue = new(responses);
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage _, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            CallCount++;
            if (_queue.Count == 0)
                throw new InvalidOperationException("Test handler ran out of responses.");
            return Task.FromResult(_queue.Dequeue());
        }
    }

    private static HttpResponseMessage OkJson(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private static (OnboardingService svc, QueuedHttpHandler handler) MakeOnboardingService(
        params HttpResponseMessage[] responses)
    {
        var handler = new QueuedHttpHandler(responses);
        var options = Options.Create(new BotOptions { GraphqlUrl = "https://test.example/graphql" });
        var api = new GameApiClient(new HttpClient(handler), options, NullLogger<GameApiClient>.Instance);
        return (new OnboardingService(api, NullLogger<OnboardingService>.Instance), handler);
    }

    // ── JSON fixtures ──────────────────────────────────────────────────────────

    private const string SingleCityJson =
        """{"data":{"cities":[{"id":"city-1","name":"Bratislava","countryCode":"SK","population":475000}]}}""";

    private const string FactoryLotJson =
        """{"data":{"cityLots":[{"id":"lot-f1","district":"Industrial","price":75000.00,"suitableTypes":"FACTORY","buildingId":null}]}}""";

    private const string ShopLotJson =
        """{"data":{"cityLots":[{"id":"lot-s1","district":"Commercial","price":50000.00,"suitableTypes":"SALES_SHOP","buildingId":null}]}}""";

    private const string StartSuccessJson =
        """{"data":{"startOnboardingCompany":{"company":{"id":"co-1","name":"Bot Corp"},"factory":{"id":"bld-f1","name":"Factory A","type":"FACTORY"},"factoryLot":{"id":"lot-f1","district":"Industrial","price":75000.00},"nextStep":"SHOP_SELECTION"}}}""";

    private const string FoodProcessingProductsJson =
        """{"data":{"productTypes":[{"id":"prod-bread","name":"Bread","slug":"bread","industry":"FOOD_PROCESSING","basePrice":3.00,"isProOnly":false}]}}""";

    private const string HealthcareProductsJson =
        """{"data":{"productTypes":[{"id":"prod-med","name":"Basic Medicine","slug":"basic-medicine","industry":"HEALTHCARE","basePrice":25.00,"isProOnly":false}]}}""";

    private const string FinishSuccessJson =
        """{"data":{"finishOnboarding":{"company":{"id":"co-1","name":"Bot Corp","cash":5000.00},"factory":{"id":"bld-f1","name":"Factory A","type":"FACTORY"},"salesShop":{"id":"bld-s1","name":"Shop A","type":"SALES_SHOP"},"selectedProduct":{"id":"prod-bread","name":"Bread","slug":"bread","basePrice":3.00}}}}""";

    // ══════════════════════════════════════════════════════════════════════════
    //  OnboardingService — starter industry coverage
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task OnboardingService_FoodProcessingIndustry_CompletesFullSixStepHappyPath()
    {
        // The OnboardingService must succeed end-to-end with FOOD_PROCESSING as the only
        // allowed industry, picking Bread as the starter product.
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC_FP", Email = "npc_fp@test.example", Strategy = "FOOD_PROCESSING",
            Profile = new PlayerProfile { Id = "p-fp", DisplayName = "NPC_FP" },
        };

        var (svc, handler) = MakeOnboardingService(
            OkJson(SingleCityJson),
            OkJson(FactoryLotJson),
            OkJson(StartSuccessJson),
            OkJson(ShopLotJson),
            OkJson(FoodProcessingProductsJson),
            OkJson(FinishSuccessJson));

        var ex = await Record.ExceptionAsync(
            () => svc.RunAsync(bot, ["FOOD_PROCESSING"], CancellationToken.None));

        Assert.Null(ex);
        Assert.Equal(6, handler.CallCount);
    }

    [Fact]
    public async Task OnboardingService_HealthcareIndustry_CompletesFullSixStepHappyPath()
    {
        // The OnboardingService must succeed end-to-end with HEALTHCARE as the only
        // allowed industry, picking Basic Medicine as the starter product.
        var bot = new BotAccount
        {
            Index = 2, DisplayName = "NPC_HC", Email = "npc_hc@test.example", Strategy = "HEALTHCARE",
            Profile = new PlayerProfile { Id = "p-hc", DisplayName = "NPC_HC" },
        };

        const string finishHealthcareJson =
            """{"data":{"finishOnboarding":{"company":{"id":"co-2","name":"HC Corp","cash":5000.00},"factory":{"id":"bld-hc","name":"HC Factory","type":"FACTORY"},"salesShop":{"id":"bld-hcs","name":"HC Shop","type":"SALES_SHOP"},"selectedProduct":{"id":"prod-med","name":"Basic Medicine","slug":"basic-medicine","basePrice":25.00}}}}""";

        var (svc, handler) = MakeOnboardingService(
            OkJson(SingleCityJson),
            OkJson(FactoryLotJson),
            OkJson(StartSuccessJson),
            OkJson(ShopLotJson),
            OkJson(HealthcareProductsJson),
            OkJson(finishHealthcareJson));

        var ex = await Record.ExceptionAsync(
            () => svc.RunAsync(bot, ["HEALTHCARE"], CancellationToken.None));

        Assert.Null(ex);
        Assert.Equal(6, handler.CallCount);
    }

    [Fact]
    public async Task OnboardingService_FurnitureIndustry_CompletesFullSixStepHappyPath()
    {
        // Omnibus guard: FURNITURE (the third free starter industry) must also work.
        const string furnitureProductsJson =
            """{"data":{"productTypes":[{"id":"p-chair","name":"Wooden Chair","slug":"wooden-chair","industry":"FURNITURE","basePrice":45.00,"isProOnly":false}]}}""";

        var bot = new BotAccount
        {
            Index = 3, DisplayName = "NPC_FU", Email = "npc_fu@test.example", Strategy = "FURNITURE",
            Profile = new PlayerProfile { Id = "p-fu", DisplayName = "NPC_FU" },
        };

        var (svc, handler) = MakeOnboardingService(
            OkJson(SingleCityJson),
            OkJson(FactoryLotJson),
            OkJson(StartSuccessJson),
            OkJson(ShopLotJson),
            OkJson(furnitureProductsJson),
            OkJson(FinishSuccessJson));

        var ex = await Record.ExceptionAsync(
            () => svc.RunAsync(bot, ["FURNITURE"], CancellationToken.None));

        Assert.Null(ex);
        Assert.Equal(6, handler.CallCount);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PriceAdjustmentService — CT cancelled mid-loop
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PriceAdjustment_CtCancelledAfterFirstUnit_BreaksLoopAndReturnsOne()
    {
        // The FakeHttpHandler cancels the CTS after the first successful HTTP response.
        // The per-unit loop checks ct.IsCancellationRequested before each iteration,
        // so the second unit must NOT be processed even though it is valid.
        using var cts = new CancellationTokenSource();
        var httpCalls = 0;

        var service = CreatePriceAdjService(() =>
        {
            httpCalls++;
            // Cancel after the first unit update succeeds.
            if (httpCalls == 1) cts.Cancel();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"data":{"updatePublicSalesPrice":{"id":"u1","unitType":"PUBLIC_SALES","minPrice":47.5}}}""",
                    Encoding.UTF8, "application/json"),
            };
        });

        var bot = MakeBotWithTwoUnits("u1", 50m, "u2", 30m);
        var rec = new StrategyRecommendation
            { ShouldAct = true, Reason = "test", PriceAdjustmentFactor = 0.95m };

        var result = await service.ApplyAdjustmentAsync(bot, rec, cts.Token);

        Assert.Equal(1, result);     // only the first unit was updated
        Assert.Equal(1, httpCalls);  // second HTTP call was never made
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private static PriceAdjustmentService CreatePriceAdjService(
        Func<HttpResponseMessage> factory)
    {
        var options = Options.Create(new BotOptions());
        var api = new GameApiClient(
            new HttpClient(new DelegatingFakeHandler(factory)),
            options,
            NullLogger<GameApiClient>.Instance);
        var accounts = new AccountService(api, options, NullLogger<AccountService>.Instance);
        return new PriceAdjustmentService(accounts, NullLogger<PriceAdjustmentService>.Instance);
    }

    private sealed class DelegatingFakeHandler(Func<HttpResponseMessage> factory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage _, CancellationToken __) => Task.FromResult(factory());
    }

    private static BotAccount MakeBotWithTwoUnits(
        string id1, decimal price1, string id2, decimal price2) =>
        new()
        {
            Index = 1, DisplayName = "NPC_01", Email = "npc01@test.example",
            Strategy = "FURNITURE", Token = "valid-token",
            Profile = new PlayerProfile
            {
                Companies =
                [
                    new CompanySummary
                    {
                        Id = "c1", Name = "Corp",
                        Buildings =
                        [
                            new BuildingSummary
                            {
                                Id = "b1", Name = "Shop", Type = "SALES_SHOP", CityId = "ba",
                                Units =
                                [
                                    new UnitSummary { Id = id1, UnitType = "PUBLIC_SALES", MinPrice = price1 },
                                    new UnitSummary { Id = id2, UnitType = "PUBLIC_SALES", MinPrice = price2 },
                                ],
                            },
                        ],
                    },
                ],
            },
        };

    // ══════════════════════════════════════════════════════════════════════════
    //  BotOrchestrator — init-level paths
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A FakeAccountService that cancels a CTS when FetchProfileAsync is called for the
    /// Nth time or later (the check is <c>&gt;= N</c>, so the CT is cancelled on the Nth call
    /// itself before returning).  Used to cancel the CT at a predictable point so the
    /// orchestrator foreach guard fires before the next bot starts.
    /// </summary>
    private sealed class CancelAfterProfileFetchService : IAccountService
    {
        private readonly CancellationTokenSource _cts;
        private readonly int _cancelOnCallN;
        private int _profileCallCount;
        private GameStateSummary _gameState = new() { CurrentTick = 10 };
        public Exception? GameStateException;
        public int RegisterOrLoginCallCount;

        public CancelAfterProfileFetchService(CancellationTokenSource cts, int cancelOnCallN)
        {
            _cts = cts;
            _cancelOnCallN = cancelOnCallN;
        }

        public void SetGameState(GameStateSummary gs) => _gameState = gs;

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
                { OnboardingCompletedAtUtc = DateTime.UtcNow.AddMinutes(-5) });
        }

        public Task<GameStateSummary> FetchGameStateAsync(CancellationToken ct)
        {
            if (GameStateException is not null) throw GameStateException;
            return Task.FromResult(_gameState);
        }

        public Task<List<RankingEntry>> FetchRankingsAsync(CancellationToken ct) =>
            Task.FromResult(new List<RankingEntry>());

        public Task<UnitSummary> UpdatePublicSalesPriceAsync(
            string unitId, decimal newMinPrice, string token, CancellationToken ct) =>
            Task.FromResult(new UnitSummary { Id = unitId });
    }

    private static BotOrchestrator MakeOrchestrator(
        IAccountService accounts,
        BotOptions? options = null,
        params BotAccount[] bots)
    {
        var opts = options ?? new BotOptions { Enabled = true, PollIntervalSeconds = 60 };
        return new BotOrchestrator(
            bots.Length == 0 ? [MakeBot(1)] : bots,
            accounts,
            new FakeNoOpOnboarding(),
            new FakeNoOpPriceAdj(),
            Options.Create(opts),
            NullLogger<BotOrchestrator>.Instance);
    }

    private sealed class FakeNoOpOnboarding : IOnboardingService
    {
        public Task RunAsync(BotAccount bot, string[] allowedIndustries, CancellationToken ct)
        {
            if (bot.Profile is not null) bot.Profile.OnboardingCompletedAtUtc = DateTime.UtcNow;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNoOpPriceAdj : IPriceAdjustmentService
    {
        public Task<int> ApplyAdjustmentAsync(
            BotAccount bot, StrategyRecommendation rec, CancellationToken ct) =>
            Task.FromResult(0);
    }

    private static BotAccount MakeBot(int idx = 1) => new()
    {
        Index = idx,
        DisplayName = $"NPC 00{idx}",
        Email = $"npc00{idx}@test.example",
        Strategy = "FURNITURE",
    };

    [Fact]
    public async Task Init_GameStateFetchFails_BotsAreStillInitialised()
    {
        // When FetchGameStateAsync throws during InitialiseAllBotsAsync, the orchestrator
        // logs the warning and continues so all bots are still initialised.
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        var accounts = new CancelAfterProfileFetchService(cts, int.MaxValue); // never cancel via profile
        accounts.GameStateException = new InvalidOperationException("Game API unreachable");
        var bot = MakeBot();
        var orchestrator = MakeOrchestrator(accounts, bots: [bot]);

        await orchestrator.RunAsync(cts.Token);

        // Bot was still initialised despite game-state failure
        Assert.NotNull(bot.Token);
        Assert.Equal(0, bot.ConsecutiveErrors);
    }

    [Fact]
    public async Task Init_CtCancelledAfterFirstBot_SecondBotIsNotInitialised()
    {
        // InitialiseAllBotsAsync has a guard: if (ct.IsCancellationRequested) break;
        // before each bot's InitialiseBotAsync call.
        // By cancelling the CT after the first bot's init completes (profile call #2 = net worth),
        // the guard fires and the second bot never receives a token.
        using var cts = new CancellationTokenSource();

        // InitialiseBotAsync (onboarding already complete) calls FetchProfileAsync TWICE:
        //   call #1 → onboarding check   (profile returned with OnboardingCompletedAtUtc set)
        //   call #2 → definitive net worth  ← CTS is cancelled during this call (>= 2 guard)
        // The cancellation fires mid-init for bot1's second FetchProfileAsync call.
        // On the next foreach iteration the CT-guard `if (ct.IsCancellationRequested) break;`
        // fires before bot2's InitialiseBotAsync is reached, so bot2 never gets a token.
        var accounts = new CancelAfterProfileFetchService(cts, cancelOnCallN: 2);

        var bot1 = MakeBot(1);
        var bot2 = MakeBot(2);
        var orchestrator = MakeOrchestrator(accounts, bots: [bot1, bot2]);

        await orchestrator.RunAsync(cts.Token);

        Assert.NotNull(bot1.Token);       // bot1 completed init
        Assert.Null(bot2.Token);          // bot2 was never initialised — CT fired before its init
        Assert.Equal(0, bot1.ConsecutiveErrors);
        Assert.Equal(0, bot2.ConsecutiveErrors);
    }

    [Fact]
    public async Task Init_TwoBots_BothInitialised_EachGetsIndependentToken()
    {
        // Without CT interruption both bots must complete init with distinct tokens.
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        // cancelOnCallN = int.MaxValue means the CT is never cancelled via profile fetch.
        var accounts = new CancelAfterProfileFetchService(cts, int.MaxValue);
        var bot1 = MakeBot(1);
        var bot2 = MakeBot(2);
        var orchestrator = MakeOrchestrator(accounts, bots: [bot1, bot2]);

        await orchestrator.RunAsync(cts.Token);

        Assert.NotNull(bot1.Token);
        Assert.NotNull(bot2.Token);
        Assert.NotEqual(bot1.Token, bot2.Token); // distinct tokens per bot
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  BotProfitCalculator — edge cases
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void BotProfitCalculator_Classify_ExactlyZeroDelta_ReturnsNeutral()
    {
        // When current == initial the net-worth change is exactly 0% → within the neutral band.
        var status = BotProfitCalculator.Classify(100m, 100m);
        Assert.Equal(ProfitabilityStatus.Neutral, status);
    }

    [Fact]
    public void BotProfitCalculator_Classify_TinyPositiveDelta_ReturnsNeutral()
    {
        // A 0.001% gain is well within the ±1% (NeutralBandPercent) neutral band so the
        // result must still be Neutral — not Profitable.
        var status = BotProfitCalculator.Classify(100.001m, 100m);
        Assert.Equal(ProfitabilityStatus.Neutral, status);
    }
}

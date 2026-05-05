using System.Net;
using System.Text;
using System.Text.Json;
using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Additional tests covering genuine gaps identified during gap analysis:
/// <list type="bullet">
///   <item><see cref="PriceAdjustmentService"/> resilience when
///     <see cref="GameApiClient"/> throws <see cref="GraphQLException"/>
///     (GraphQL-level error, HTTP 200 with errors array) rather than
///     <see cref="InvalidOperationException"/> (HTTP 4xx/5xx).</item>
///   <item><see cref="BotStateValidator.IsStale"/> with zero-minute threshold
///     and one-millisecond-past-threshold edge case.</item>
///   <item><see cref="BotProfitCalculator.Recommend"/> when
///     <c>minTicksBeforeAdjustment=0</c> and <c>ticksElapsed=0</c>:
///     the guard <c>0 &lt; 0</c> is false, so an unprofitable bot should
///     receive a recommendation even on its very first tick.</item>
///   <item><see cref="OnboardingHelpers.PickCheapestAvailableLot"/> when
///     multiple matching lots share the same minimum price.</item>
///   <item><see cref="GraphQLResponseParser.ParseFirstError"/> when the
///     <c>extensions.code</c> field is an integer rather than a string —
///     defensive against non-conforming servers.</item>
///   <item>Orchestrator integration: two-company profile at init time causes
///     <c>InitialNetWorth</c> to sum both companies' <c>Cash</c> values.</item>
/// </list>
/// </summary>
public sealed class BotGapCoverageTests
{
    // ── PriceAdjustmentService infrastructure ─────────────────────────────────

    private sealed class FakeHttpHandler(Func<HttpResponseMessage> factory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage _, CancellationToken __) => Task.FromResult(factory());
    }

    private static string OkJson(string id = "u1", decimal price = 47.5m) =>
        "{\"data\":{\"updatePublicSalesPrice\":{\"id\":\"" + id +
        "\",\"unitType\":\"PUBLIC_SALES\",\"minPrice\":" + price + "}}}";

    private static string GraphQLErrorJson(string code = "UNIT_NOT_FOUND") =>
        "{\"errors\":[{\"message\":\"Unit not found.\",\"extensions\":{\"code\":\"" + code + "\"}}]}";

    private static PriceAdjustmentService CreateService(Func<HttpResponseMessage> httpFactory)
    {
        var options = Options.Create(new BotOptions());
        var http = new HttpClient(new FakeHttpHandler(httpFactory));
        var api = new GameApiClient(http, options, NullLogger<GameApiClient>.Instance);
        var accounts = new AccountService(api, options, NullLogger<AccountService>.Instance);
        return new PriceAdjustmentService(accounts, NullLogger<PriceAdjustmentService>.Instance);
    }

    private static BotAccount BotWithUnits(params (string id, decimal? price)[] units)
    {
        var botUnits = units
            .Select(u => new UnitSummary { Id = u.id, UnitType = "PUBLIC_SALES", MinPrice = u.price })
            .ToList();

        return new BotAccount
        {
            Index = 1,
            DisplayName = "NPC_Test_01",
            Email = "npc@test.example",
            Strategy = "Trading",
            Token = "valid-token",
            Profile = new PlayerProfile
            {
                Companies =
                [
                    new CompanySummary
                    {
                        Id = "c1",
                        Name = "Corp",
                        Buildings =
                        [
                            new BuildingSummary
                            {
                                Id = "b1",
                                Name = "Downtown Shop",
                                Type = "SALES_SHOP",
                                CityId = "ba",
                                Units = botUnits,
                            },
                        ],
                    },
                ],
            },
        };
    }

    // ── PriceAdjustmentService: GraphQLException resilience ───────────────────

    /// <summary>
    /// When the game API returns HTTP 200 but with a GraphQL errors array
    /// (e.g. "UNIT_NOT_FOUND"), <see cref="GameApiClient"/> throws a
    /// <see cref="GraphQLException"/>.  The <c>catch (Exception ex)</c> guard
    /// in <see cref="PriceAdjustmentService.ApplyAdjustmentAsync"/> must
    /// catch it and continue to the next unit — exactly the same as an HTTP 500.
    /// </summary>
    [Fact]
    public async Task ApplyAdjustmentAsync_GraphQLErrorOnFirstUnit_CaughtAndContinuesToSecondUnit()
    {
        var callCount = 0;
        var service = CreateService(() =>
        {
            callCount++;
            // First unit: HTTP 200 with GraphQL errors array → throws GraphQLException
            if (callCount == 1)
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        GraphQLErrorJson("UNIT_NOT_FOUND"), Encoding.UTF8, "application/json"),
                };
            // Second unit: success
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(OkJson("u2", 45.0m), Encoding.UTF8, "application/json"),
            };
        });

        var bot = BotWithUnits(("u1", 50.0m), ("u2", 50.0m));
        var recommendation = new StrategyRecommendation
        {
            ShouldAct = true,
            Reason = "test",
            PriceAdjustmentFactor = 0.90m,
        };

        var result = await service.ApplyAdjustmentAsync(bot, recommendation, CancellationToken.None);

        Assert.Equal(1, result);      // second unit was updated
        Assert.Equal(2, callCount);   // both HTTP calls were attempted
    }

    /// <summary>
    /// When ALL units fail with GraphQL errors, the service returns zero.
    /// This complements the existing HTTP-500 two-unit resilience test.
    /// </summary>
    [Fact]
    public async Task ApplyAdjustmentAsync_GraphQLErrorOnBothUnits_ReturnsZero()
    {
        var service = CreateService(() =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    GraphQLErrorJson("PERMISSION_DENIED"), Encoding.UTF8, "application/json"),
            });

        var bot = BotWithUnits(("u1", 50.0m), ("u2", 60.0m));
        var recommendation = new StrategyRecommendation
        {
            ShouldAct = true,
            Reason = "test",
            PriceAdjustmentFactor = 0.85m,
        };

        var result = await service.ApplyAdjustmentAsync(bot, recommendation, CancellationToken.None);

        Assert.Equal(0, result);  // no unit was updated successfully
    }

    // ── BotStateValidator.IsStale: zero-minute threshold ─────────────────────

    /// <summary>
    /// With <c>staleAfterMinutes=0</c>, any <c>LastSuccessUtc</c> that is in the past
    /// is considered stale because the elapsed time is greater than
    /// <see cref="TimeSpan.Zero"/>.
    /// </summary>
    [Fact]
    public void IsStale_ZeroMinuteThreshold_PastSuccess_IsStale()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC", Email = "n@t.com", Strategy = "S",
            LastSuccessUtc = DateTime.UtcNow.AddSeconds(-30),  // 30 seconds ago
        };

        Assert.True(BotStateValidator.IsStale(bot, staleAfterMinutes: 0),
            "Any past LastSuccessUtc must be considered stale when threshold is 0 minutes.");
    }

    /// <summary>
    /// With <c>staleAfterMinutes=0</c>, a <c>LastSuccessUtc</c> set slightly in the
    /// future (e.g. from a clock-skewed server) is NOT stale because elapsed &lt; 0.
    /// </summary>
    [Fact]
    public void IsStale_ZeroMinuteThreshold_FutureSuccess_IsNotStale()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC", Email = "n@t.com", Strategy = "S",
            LastSuccessUtc = DateTime.UtcNow.AddSeconds(60),  // 60 seconds in the future
        };

        Assert.False(BotStateValidator.IsStale(bot, staleAfterMinutes: 0),
            "A future LastSuccessUtc must never be considered stale.");
    }

    /// <summary>
    /// A bot whose last success was exactly one millisecond past the staleness threshold
    /// MUST be considered stale (the comparison uses strict &gt;).
    /// </summary>
    [Fact]
    public void IsStale_OneMillisecondPastThreshold_IsStale()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC", Email = "n@t.com", Strategy = "S",
            LastSuccessUtc = DateTime.UtcNow.AddMinutes(-10).AddMilliseconds(-1),
        };

        Assert.True(BotStateValidator.IsStale(bot, staleAfterMinutes: 10),
            "Last success 10 minutes and 1 ms ago must be stale (threshold is exactly 10 min).");
    }

    // ── BotProfitCalculator: MinTicks=0 edge cases ───────────────────────────

    /// <summary>
    /// When <c>minTicksBeforeAdjustment=0</c> the guard
    /// <c>ticksElapsed &lt; minTicksBeforeAdjustment</c> evaluates to <c>0 &lt; 0 = false</c>
    /// so it does NOT fire — an unprofitable bot on tick 0 should still receive a
    /// price-reduction recommendation.
    /// </summary>
    [Fact]
    public void Recommend_MinTicksIsZero_ZeroTicksElapsed_UnprofitableBot_ProducesRecommendation()
    {
        // −15 % loss → severe → aggressive price reduction
        var rec = BotProfitCalculator.Recommend(
            currentNetWorth: 85_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: 0,
            minTicksBeforeAdjustment: 0);

        Assert.True(rec.ShouldAct,
            "With minTicks=0, a severely unprofitable bot should act even on tick 0.");
        Assert.Equal(BotProfitCalculator.AggressivePriceReductionFactor, rec.PriceAdjustmentFactor);
    }

    /// <summary>
    /// Even when <c>minTicksBeforeAdjustment=0</c>, a profitable bot must still return
    /// <see cref="StrategyRecommendation.NoAction"/> — profitability short-circuits
    /// before any price adjustment is suggested.
    /// </summary>
    [Fact]
    public void Recommend_MinTicksIsZero_ZeroTicksElapsed_ProfitableBot_ReturnsNoAction()
    {
        // +5 % gain → Profitable → no adjustment
        var rec = BotProfitCalculator.Recommend(
            currentNetWorth: 105_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: 0,
            minTicksBeforeAdjustment: 0);

        Assert.False(rec.ShouldAct,
            "A profitable bot must never trigger a price adjustment regardless of minTicks.");
    }

    /// <summary>
    /// With <c>minTicksBeforeAdjustment=0</c>, a mildly unprofitable bot (−5 % to −10 %)
    /// on tick 0 should produce a mild price-reduction recommendation.
    /// </summary>
    [Fact]
    public void Recommend_MinTicksIsZero_ZeroTicksElapsed_MildLoss_ReturnsMildReduction()
    {
        // −5 % loss → mild (between neutral band and severe threshold)
        var rec = BotProfitCalculator.Recommend(
            currentNetWorth: 95_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: 0,
            minTicksBeforeAdjustment: 0);

        Assert.True(rec.ShouldAct,
            "A mildly unprofitable bot with minTicks=0 should receive a mild recommendation.");
        Assert.Equal(BotProfitCalculator.MildPriceReductionFactor, rec.PriceAdjustmentFactor);
    }

    // ── OnboardingHelpers: same-price tie-breaking ────────────────────────────

    /// <summary>
    /// When multiple factory lots share the same minimum price,
    /// <see cref="OnboardingHelpers.PickCheapestAvailableLot"/> must return
    /// one of them (not null) and the returned lot must have the minimum price.
    /// </summary>
    [Fact]
    public void PickCheapestAvailableLot_MultipleLotsWithSamePrice_ReturnsAMatch()
    {
        const decimal samePrice = 75_000m;

        var lots = new List<BuildingLotSummary>
        {
            new() { Id = "lot-a", SuitableTypes = "FACTORY", Price = samePrice, BuildingId = null },
            new() { Id = "lot-b", SuitableTypes = "FACTORY", Price = samePrice, BuildingId = null },
            new() { Id = "lot-c", SuitableTypes = "FACTORY", Price = samePrice, BuildingId = null },
        };

        var result = OnboardingHelpers.PickCheapestAvailableLot(lots, "FACTORY");

        Assert.NotNull(result);
        Assert.Equal(samePrice, result.Price);
        Assert.Contains(result.Id, new[] { "lot-a", "lot-b", "lot-c" });
    }

    /// <summary>
    /// When multiple lots contain the desired type alongside other types (e.g. "FACTORY,MINE"),
    /// all matching lots are eligible and the cheapest is returned correctly.
    /// </summary>
    [Fact]
    public void PickCheapestAvailableLot_MultiTypeSuitableTypes_PicksCheapestMatch()
    {
        var lots = new List<BuildingLotSummary>
        {
            new() { Id = "lot-expensive", SuitableTypes = "FACTORY,MINE", Price = 200_000m, BuildingId = null },
            new() { Id = "lot-cheap",     SuitableTypes = "FACTORY",      Price = 75_000m,  BuildingId = null },
            new() { Id = "lot-mine-only", SuitableTypes = "MINE",         Price = 10_000m,  BuildingId = null },
        };

        var result = OnboardingHelpers.PickCheapestAvailableLot(lots, "FACTORY");

        Assert.NotNull(result);
        Assert.Equal("lot-cheap", result.Id);  // cheapest lot that contains "FACTORY"
    }

    // ── GraphQLResponseParser: integer code field ─────────────────────────────

    /// <summary>
    /// Some non-conforming GraphQL servers return numeric error codes instead of
    /// string codes in the extensions block.
    /// <see cref="GraphQLResponseParser.ParseFirstError"/> should handle this gracefully
    /// by returning an empty code string rather than throwing an exception.
    /// </summary>
    [Fact]
    public void ParseFirstError_IntegerCodeField_ReturnsEmptyCodeNotException()
    {
        // Non-conforming: "code" is an integer (401) instead of a string
        const string json = """
            [{"message":"Unauthorized.","extensions":{"code":401}}]
            """;
        using var doc = JsonDocument.Parse(json);

        // Must not throw; code falls back to empty string
        var (message, code) = GraphQLResponseParser.ParseFirstError(doc.RootElement);

        Assert.Equal("Unauthorized.", message);
        Assert.Equal(string.Empty, code);
    }

    /// <summary>
    /// When <c>extensions.code</c> is a boolean value (another type mismatch),
    /// the parser should still return empty code without throwing.
    /// </summary>
    [Fact]
    public void ParseFirstError_BooleanCodeField_ReturnsEmptyCode()
    {
        const string json = """
            [{"message":"Server fault.","extensions":{"code":true}}]
            """;
        using var doc = JsonDocument.Parse(json);

        var (message, code) = GraphQLResponseParser.ParseFirstError(doc.RootElement);

        Assert.Equal("Server fault.", message);
        Assert.Equal(string.Empty, code);
    }

    // ── BotOrchestrator integration: two-company InitialNetWorth ─────────────

    private sealed class FakeAccountService : IAccountService
    {
        private readonly Queue<PlayerProfile> _profileQueue = new();
        private readonly Queue<GameStateSummary> _gameStateQueue = new();

        public void EnqueueProfile(PlayerProfile p) => _profileQueue.Enqueue(p);
        public void EnqueueGameState(GameStateSummary gs) => _gameStateQueue.Enqueue(gs);

        public Task<(string token, DateTime expiresAt)> RegisterOrLoginAsync(
            BotAccount bot, CancellationToken ct) =>
            Task.FromResult(("tok-" + bot.Index, DateTime.UtcNow.AddHours(2)));

        public Task<(string token, DateTime expiresAt)> LoginAsync(
            BotAccount bot, CancellationToken ct) =>
            Task.FromResult(("refreshed-" + bot.Index, DateTime.UtcNow.AddHours(2)));

        public Task<PlayerProfile> FetchProfileAsync(string token, CancellationToken ct)
        {
            var p = _profileQueue.Count > 0
                ? _profileQueue.Dequeue()
                : new PlayerProfile { OnboardingCompletedAtUtc = DateTime.UtcNow.AddMinutes(-5) };
            return Task.FromResult(p);
        }

        public Task<GameStateSummary> FetchGameStateAsync(CancellationToken ct)
        {
            var gs = _gameStateQueue.Count > 0
                ? _gameStateQueue.Dequeue()
                : new GameStateSummary { CurrentTick = 1, TickIntervalSeconds = 60 };
            return Task.FromResult(gs);
        }

        public Task<List<RankingEntry>> FetchRankingsAsync(CancellationToken ct) =>
            Task.FromResult(new List<RankingEntry>());

        public Task<UnitSummary> UpdatePublicSalesPriceAsync(
            string unitId, decimal newMinPrice, string token, CancellationToken ct) =>
            Task.FromResult(new UnitSummary { Id = unitId, UnitType = "PUBLIC_SALES", MinPrice = newMinPrice });
    }

    private sealed class FakeOnboardingService : IOnboardingService
    {
        public Task RunAsync(BotAccount bot, string[] allowedIndustries, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class FakePriceAdjustmentService : IPriceAdjustmentService
    {
        public Task<int> ApplyAdjustmentAsync(
            BotAccount bot, StrategyRecommendation rec, CancellationToken ct) =>
            Task.FromResult(0);
    }

    /// <summary>
    /// When the bot's profile at init time contains two companies, the
    /// <c>InitialNetWorth</c> should be the sum of both companies' <c>Cash</c> values.
    /// This verifies that <see cref="BotProfitCalculator.ComputeNetWorth"/> is used
    /// correctly by the orchestrator for multi-company bots.
    /// </summary>
    [Fact]
    public async Task Init_TwoCompanyProfile_InitialNetWorthSumsBothCompanies()
    {
        var accounts = new FakeAccountService();
        using var cts = new CancellationTokenSource();

        // InitialiseBotAsync calls FetchProfileAsync TWICE:
        //   1st call (line 107): onboarding check
        //   2nd call (line 117): definitive InitialNetWorth
        // We enqueue both profiles. The CTS is cancelled after 300ms so the
        // Task.Delay(60s) in the while loop times out quickly without any ticks firing.
        var onboardingCheckProfile = new PlayerProfile
        {
            Id = "player-1",
            DisplayName = "NPC 001",
            Email = "npc001@test.example",
            OnboardingCompletedAtUtc = DateTime.UtcNow.AddDays(-1),
            Companies = [],
        };
        var netWorthProfile = new PlayerProfile
        {
            Id = "player-1",
            DisplayName = "NPC 001",
            Email = "npc001@test.example",
            OnboardingCompletedAtUtc = DateTime.UtcNow.AddDays(-1),
            Companies =
            [
                new CompanySummary { Id = "c1", Name = "Corp A", Cash = 80_000m },
                new CompanySummary { Id = "c2", Name = "Corp B", Cash = 70_000m },
            ],
        };

        accounts.EnqueueProfile(onboardingCheckProfile);
        accounts.EnqueueProfile(netWorthProfile);
        // EnqueueGameState is consumed by InitialiseAllBotsAsync's FetchGameStateAsync call
        // (which runs before the bot foreach). Without it the fake returns a default tick=1 state.
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 10 });

        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "NPC 001",
            Email = "npc001@test.example",
            Strategy = "FURNITURE",
        };

        var opts = Options.Create(new BotOptions
        {
            Enabled = true,
            PollIntervalSeconds = 60,  // long poll ensures Task.Delay fires before tick runs
        });

        var orchestrator = new BotOrchestrator(
            [bot],
            accounts,
            new FakeOnboardingService(),
            new FakePriceAdjustmentService(),
            opts,
            NullLogger<BotOrchestrator>.Instance);

        // Cancel after 2s — the 60-second Task.Delay in the while-loop is interrupted by the CT,
        // throwing OperationCanceledException, which the orchestrator catches and exits cleanly.
        // 2 seconds is conservative but still fast: init completes in <100ms, leaving 1.9s margin
        // before the first tick could fire. This matches the pattern used in
        // RunAsync_SingleBot_NetWorthIsSetAfterInit.
        cts.CancelAfter(TimeSpan.FromSeconds(2));
        await orchestrator.RunAsync(cts.Token);

        // InitialNetWorth = 80 000 (Corp A) + 70 000 (Corp B) = 150 000
        Assert.Equal(150_000m, bot.InitialNetWorth);
    }

    // ── BotProfitCalculator: MinTicks=1 boundary (complement to MinTicks=0) ──

    /// <summary>
    /// With <c>minTicksBeforeAdjustment=1</c> and <c>ticksElapsed=0</c>, the guard
    /// <c>0 &lt; 1</c> fires and returns NoAction — even for a severely unprofitable bot.
    /// This confirms the MinTicks=0 test above is testing a genuinely different code path.
    /// </summary>
    [Fact]
    public void Recommend_MinTicksIsOne_ZeroTicksElapsed_UnprofitableBot_ReturnsNoAction()
    {
        var rec = BotProfitCalculator.Recommend(
            currentNetWorth: 85_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: 0,
            minTicksBeforeAdjustment: 1);

        Assert.False(rec.ShouldAct,
            "With minTicks=1 and ticksElapsed=0, no action should be taken (guard 0<1 fires).");
    }

    // ── BotStateValidator: Validate with zero-minute stale threshold ──────────

    /// <summary>
    /// A bot with a past <c>LastSuccessUtc</c> and <c>staleAfterMinutes=0</c> should be
    /// reported as stale in the <c>Validate</c> result, exercising the zero-minute path
    /// through the full validation pipeline.
    /// </summary>
    [Fact]
    public void Validate_ZeroMinuteThreshold_PastSuccess_ReportsStaleIssue()
    {
        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "NPC",
            Email = "n@t.com",
            Strategy = "S",
            Token = "valid",
            TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2),
            LastSuccessUtc = DateTime.UtcNow.AddSeconds(-30),
            Profile = new PlayerProfile
            {
                OnboardingCompletedAtUtc = DateTime.UtcNow.AddDays(-1),
            },
        };

        var result = BotStateValidator.Validate(bot, staleAfterMinutes: 0);

        Assert.False(result.IsValid, "Bot with past last-success under zero-minute threshold must be invalid.");
        Assert.Contains(result.Issues, issue => issue.Contains("0 minutes", StringComparison.OrdinalIgnoreCase));
    }
}

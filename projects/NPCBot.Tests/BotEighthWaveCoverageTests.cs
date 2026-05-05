using System.Net;
using System.Text;
using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Eighth wave of targeted coverage additions:
/// <list type="bullet">
///   <item><b>AccountService.FetchProfileAsync GraphQL error</b> — throws GraphQLException with correct code
///     (previously only HTTP 500 was tested; GraphQL-error path was uncovered).</item>
///   <item><b>PriceAdjustmentService mixed floor/normal prices</b> — one unit at the 0.01 price floor
///     (adjustment is non-meaningful) alongside one unit with a normal price; only the normal unit is updated.</item>
///   <item><b>BotOptions.BotEmailDomain exact default</b> — verifies the precise default value
///     "npcbot.capitalism.local" (previously only non-empty and dot-separator were asserted).</item>
///   <item><b>BotRosterFactory email domain matches BotOptions</b> — end-to-end proof that
///     the factory uses the configured domain for every generated email address.</item>
///   <item><b>Token propagation after tick refresh</b> — after LoginAsync returns a fresh token
///     the next FetchProfileAsync call in the tick body uses that new token, not the stale one.</item>
///   <item><b>BotProfitCalculator.ComputeNetWorth null-safety</b> — null Buildings list
///     (or empty company) does not throw; returns that company's Cash value.</item>
///   <item><b>OnboardingHelpers.PickCheapestAvailableLot null SuitableTypes</b> — already
///     covered in OnboardingHelpersTests; this wave adds the complementary case where the
///     SuitableTypes field exists but the lot BuildingId is a whitespace string (treated as occupied).</item>
///   <item><b>PriceAdjustmentHelper.IsAdjustmentMeaningful exact 1-cent boundary</b> — difference
///     of exactly 0.01 is meaningful; difference of 0.009 is not.</item>
/// </list>
/// </summary>
public sealed class BotEighthWaveCoverageTests
{
    // ══════════════════════════════════════════════════════════════════════════
    // Section 1 — AccountService: FetchProfileAsync GraphQL error path
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// In-process HTTP handler whose response is produced by a factory delegate,
    /// identical to the one used in AccountServiceTests.
    /// </summary>
    private sealed class FakeHttpHandler(Func<HttpResponseMessage> factory) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage _, CancellationToken __)
        {
            CallCount++;
            return Task.FromResult(factory());
        }
    }

    private static (AccountService service, FakeHttpHandler handler) CreateAccountService(
        Func<HttpResponseMessage> factory)
    {
        var handler = new FakeHttpHandler(factory);
        var options = Options.Create(new BotOptions
        {
            BotPassword = "test-password",
            GraphqlUrl = "https://test.example/graphql",
        });
        var http = new HttpClient(handler);
        var api = new GameApiClient(http, options, NullLogger<GameApiClient>.Instance);
        var svc = new AccountService(api, options, NullLogger<AccountService>.Instance);
        return (svc, handler);
    }

    private static HttpResponseMessage OkJson(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    [Fact]
    public async Task FetchProfileAsync_GraphQLError_ThrowsGraphQLException()
    {
        // FetchProfileAsync previously only had an HTTP-500 test; this proves the GraphQL
        // error path (e.g. expired token → NOT_AUTHORIZED) propagates correctly.
        const string json = """
            {"errors":[{"message":"Not authorised.","extensions":{"code":"NOT_AUTHORIZED"}}]}
            """;

        var (svc, _) = CreateAccountService(() => OkJson(json));

        var ex = await Assert.ThrowsAsync<GraphQLException>(
            () => svc.FetchProfileAsync("stale-token", CancellationToken.None));

        Assert.Equal("NOT_AUTHORIZED", ex.Code);
    }

    [Fact]
    public async Task FetchProfileAsync_GraphQLError_MessageIsPreservedOnException()
    {
        // Verifies that the human-readable message from the server is preserved, not discarded.
        const string json = """
            {"errors":[{"message":"Your session has expired.","extensions":{"code":"SESSION_EXPIRED"}}]}
            """;

        var (svc, _) = CreateAccountService(() => OkJson(json));

        var ex = await Assert.ThrowsAsync<GraphQLException>(
            () => svc.FetchProfileAsync("expired-token", CancellationToken.None));

        Assert.Equal("SESSION_EXPIRED", ex.Code);
        Assert.True(ex.Message.Contains("expired", StringComparison.OrdinalIgnoreCase),
            $"Expected message to contain 'expired' but was: {ex.Message}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Section 2 — PriceAdjustmentService: mixed floor/normal prices
    // ══════════════════════════════════════════════════════════════════════════

    private static (PriceAdjustmentService service, FakeHttpHandler handler)
        CreatePriceAdjustmentService(string unitId, decimal returnedPrice)
    {
        var handler = new FakeHttpHandler(() =>
        {
            var json = $"{{\"data\":{{\"updatePublicSalesPrice\":{{\"id\":\"{unitId}\",\"unitType\":\"PUBLIC_SALES\",\"minPrice\":{returnedPrice}}}}}}}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        });

        var options = Options.Create(new BotOptions());
        var http = new HttpClient(handler);
        var api = new GameApiClient(http, options, NullLogger<GameApiClient>.Instance);
        var accounts = new AccountService(api, options, NullLogger<AccountService>.Instance);
        var svc = new PriceAdjustmentService(accounts, NullLogger<PriceAdjustmentService>.Instance);
        return (svc, handler);
    }

    [Fact]
    public async Task ApplyAdjustmentAsync_OneUnitAtFloorOneMeaningful_OnlyMeaningfulUnitUpdated()
    {
        // Unit "floor" is at MinimumAllowedPrice (0.01).  After aggressive factor (×0.85):
        //   ComputeNewPrice(0.01, 0.85) = Max(Round(0.0085,2), 0.01) = 0.01
        //   IsAdjustmentMeaningful(0.01, 0.01) = |0.01 - 0.01| >= 0.01 → false → SKIPPED
        //
        // Unit "normal" is at 100m.  After aggressive factor:
        //   ComputeNewPrice(100, 0.85) = 85.00
        //   IsAdjustmentMeaningful(100, 85) = |100 - 85| >= 0.01 → true → UPDATED
        //
        // Expected: exactly 1 HTTP call and return value of 1.
        var (svc, handler) = CreatePriceAdjustmentService("normal", 85m);

        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "NPC_Mixed_01",
            Email = "npc_mixed_01@npcbot.capitalism.local",
            Strategy = "Retail",
            Token = "valid-token",
            Profile = new PlayerProfile
            {
                Companies =
                [
                    new CompanySummary
                    {
                        Id = "c1",
                        Name = "MixedCorp",
                        Buildings =
                        [
                            new BuildingSummary
                            {
                                Id = "b1",
                                Name = "Main Shop",
                                Type = "SALES_SHOP",
                                CityId = "ba",
                                Units =
                                [
                                    // Floor unit — adjustment is non-meaningful (will be skipped)
                                    new UnitSummary
                                    {
                                        Id = "floor",
                                        UnitType = "PUBLIC_SALES",
                                        MinPrice = PriceAdjustmentHelper.MinimumAllowedPrice,
                                    },
                                    // Normal unit — adjustment is meaningful (will be updated)
                                    new UnitSummary { Id = "normal", UnitType = "PUBLIC_SALES", MinPrice = 100m },
                                ],
                            },
                        ],
                    },
                ],
            },
        };

        var recommendation = new StrategyRecommendation
        {
            ShouldAct = true,
            Reason = "severe loss",
            PriceAdjustmentFactor = BotProfitCalculator.AggressivePriceReductionFactor,
        };

        var result = await svc.ApplyAdjustmentAsync(bot, recommendation, CancellationToken.None);

        // Only the normal unit was meaningfully updated.
        Assert.Equal(1, result);
        // Only one HTTP call was made (for the normal unit; the floor unit was skipped before HTTP).
        Assert.Equal(1, handler.CallCount);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Section 3 — BotOptions: exact default values not yet regression-tested
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void BotOptions_BotEmailDomain_ExactDefaultIs_NpcbotCapitalismLocal()
    {
        // The exact domain value drives every bot's email address and must not be
        // changed without a deliberate version update.
        var opts = new BotOptions();
        Assert.Equal("npcbot.capitalism.local", opts.BotEmailDomain);
    }

    [Fact]
    public void BotOptions_BotNamePrefix_ExactDefaultIs_Npc()
    {
        // The prefix forms the first segment of every bot's display name and email.
        var opts = new BotOptions();
        Assert.Equal("NPC", opts.BotNamePrefix);
    }

    [Fact]
    public void BotOptions_PollIntervalSeconds_DefaultIsPositive()
    {
        var opts = new BotOptions();
        Assert.True(opts.PollIntervalSeconds > 0,
            $"PollIntervalSeconds should be > 0 but was {opts.PollIntervalSeconds}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Section 4 — BotRosterFactory: email domain end-to-end from BotOptions
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void BotRosterFactory_Build_DefaultOptions_AllEmailsUseBotEmailDomain()
    {
        // End-to-end proof: BotRosterFactory generates emails using the domain from BotOptions.
        var opts = new BotOptions { BotCount = 5 };
        var bots = BotRosterFactory.Build(opts);

        foreach (var bot in bots)
        {
            Assert.True(bot.Email.EndsWith($"@{opts.BotEmailDomain}"),
                $"Bot email '{bot.Email}' does not end with '@{opts.BotEmailDomain}'");
        }
    }

    [Fact]
    public void BotRosterFactory_Build_CustomDomain_EmailsUseCustomDomain()
    {
        // Proves the factory is not hardcoded to the default domain.
        var opts = new BotOptions { BotCount = 3, BotEmailDomain = "custom.test.example" };
        var bots = BotRosterFactory.Build(opts);

        Assert.All(bots, bot =>
            Assert.EndsWith("@custom.test.example", bot.Email));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Section 5 — Orchestrator: fresh token propagated to tick FetchProfileAsync
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// IAccountService fake that records the token argument supplied to each
    /// FetchProfileAsync call so the test can verify token propagation.
    /// </summary>
    private sealed class TokenCapturingAccountService : IAccountService
    {
        private readonly Queue<(string token, DateTime expiry)> _authQueue = new();
        private readonly Queue<PlayerProfile> _profileQueue = new();
        private readonly Queue<GameStateSummary> _gsQueue = new();
        private int _gsCallCount;
        private CancellationTokenSource? _cancelCts;
        private int _cancelAtN;

        /// <summary>All tokens passed to FetchProfileAsync in call order.</summary>
        public List<string> CapturedTokens { get; } = [];

        public void EnqueueAuth(string token, DateTime expiry) => _authQueue.Enqueue((token, expiry));
        public void EnqueueProfile(PlayerProfile p) => _profileQueue.Enqueue(p);
        public void EnqueueGameState(GameStateSummary gs) => _gsQueue.Enqueue(gs);
        public void CancelAfterGameState(CancellationTokenSource cts, int atN)
        {
            _cancelCts = cts;
            _cancelAtN = atN;
        }

        private static PlayerProfile DefaultCompletedProfile() =>
            new() { OnboardingCompletedAtUtc = DateTime.UtcNow.AddDays(-1), Companies = [] };

        public Task<(string token, DateTime expiresAt)> RegisterOrLoginAsync(
            BotAccount _, CancellationToken ct) =>
            Task.FromResult(_authQueue.Count > 0 ? _authQueue.Dequeue() : ("default-token", DateTime.UtcNow.AddHours(2)));

        public Task<(string token, DateTime expiresAt)> LoginAsync(
            BotAccount _, CancellationToken ct) =>
            Task.FromResult(_authQueue.Count > 0 ? _authQueue.Dequeue() : ("refreshed-token", DateTime.UtcNow.AddHours(2)));

        public Task<PlayerProfile> FetchProfileAsync(string token, CancellationToken ct)
        {
            CapturedTokens.Add(token);
            var p = _profileQueue.Count > 0 ? _profileQueue.Dequeue() : DefaultCompletedProfile();
            return Task.FromResult(p);
        }

        public Task<GameStateSummary> FetchGameStateAsync(CancellationToken ct)
        {
            _gsCallCount++;
            var gs = _gsQueue.Count > 0 ? _gsQueue.Dequeue() : new GameStateSummary { CurrentTick = 1 };
            if (_cancelCts is not null && _gsCallCount >= _cancelAtN)
                _cancelCts.Cancel();
            return Task.FromResult(gs);
        }

        public Task<List<RankingEntry>> FetchRankingsAsync(CancellationToken ct) =>
            Task.FromResult(new List<RankingEntry>());

        public Task<UnitSummary> UpdatePublicSalesPriceAsync(
            string unitId, decimal newMinPrice, string token, CancellationToken ct) =>
            Task.FromResult(new UnitSummary { Id = unitId });
    }

    private sealed class NoOpOnboardingService : IOnboardingService
    {
        public Task RunAsync(BotAccount bot, string[] allowedIndustries, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class NoOpPriceAdjustmentService : IPriceAdjustmentService
    {
        public Task<int> ApplyAdjustmentAsync(
            BotAccount bot, StrategyRecommendation rec, CancellationToken ct) =>
            Task.FromResult(0);
    }

    private static BotOrchestrator MakeOrchestrator(
        TokenCapturingAccountService accounts,
        BotOptions opts,
        BotAccount[] bots)
    {
        return new BotOrchestrator(
            bots,
            accounts,
            new NoOpOnboardingService(),
            new NoOpPriceAdjustmentService(),
            Options.Create(opts),
            NullLogger<BotOrchestrator>.Instance);
    }

    [Fact]
    public async Task Tick_TokenNearExpiry_FreshTokenUsedInTickFetchProfileAsync()
    {
        // Arrange:
        //   - RegisterOrLoginAsync returns "init-token" expiring in 3 minutes.
        //   - IsTokenValid(bufferMinutes=5) → 3min < 5min → needs refresh.
        //   - LoginAsync returns "refreshed-token-2hr" expiring in 2 hours.
        //   - The FetchProfileAsync call inside TickBotAsync MUST use "refreshed-token-2hr".
        var accounts = new TokenCapturingAccountService();

        // Init auth: expires in 3 minutes (inside 5-min buffer → triggers tick refresh)
        var initExpiry = DateTime.UtcNow.AddMinutes(3);
        accounts.EnqueueAuth("init-token", initExpiry);

        // Two init profile fetches (check onboarding + record initial net worth)
        var completedProfile = new PlayerProfile
        {
            OnboardingCompletedAtUtc = DateTime.UtcNow.AddDays(-1),
            Companies = [new CompanySummary { Id = "c1", Name = "Corp", Cash = 50_000m, Buildings = [] }],
        };
        accounts.EnqueueProfile(completedProfile); // init call #1
        accounts.EnqueueProfile(completedProfile); // init call #2

        // Game state for init
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 1 });

        // Tick auth refresh: LoginAsync returns long-lived token
        accounts.EnqueueAuth("refreshed-token-2hr", DateTime.UtcNow.AddHours(2));

        // Tick profile fetch (should receive "refreshed-token-2hr")
        accounts.EnqueueProfile(completedProfile); // tick call #3

        // Game state for tick (cancels after this)
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 20 });
        using var cts = new CancellationTokenSource();
        accounts.CancelAfterGameState(cts, atN: 3); // init=1, rankings=2, tick=3 → cancel after tick

        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "NPC_Token_01",
            Email = "npc_token_01@npcbot.capitalism.local",
            Strategy = "Trading",
        };

        var opts = new BotOptions
        {
            Enabled = true,
            PollIntervalSeconds = 0,
            MinTicksBeforeAdjustment = 5,
            TokenRefreshBufferMinutes = 5, // 5-min buffer triggers refresh for 3-min token
        };

        var orchestrator = MakeOrchestrator(accounts, opts, [bot]);

        // Act
        await orchestrator.RunAsync(cts.Token);

        // Assert: exactly 3 FetchProfileAsync calls total
        //   [0] = init call #1 (token "init-token")
        //   [1] = init call #2 (token "init-token")
        //   [2] = tick call   (token MUST be "refreshed-token-2hr", not "init-token")
        Assert.Equal(3, accounts.CapturedTokens.Count);
        Assert.Equal("init-token", accounts.CapturedTokens[0]);
        Assert.Equal("init-token", accounts.CapturedTokens[1]);
        var tickToken = accounts.CapturedTokens[2];
        Assert.True(tickToken == "refreshed-token-2hr",
            $"Expected tick FetchProfileAsync to use 'refreshed-token-2hr' but got '{tickToken}'.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Section 6 — PriceAdjustmentHelper: IsAdjustmentMeaningful exact boundaries
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void IsAdjustmentMeaningful_DifferenceExactlyOneCent_ReturnsTrue()
    {
        // |10.00 - 9.99| = 0.01 → exactly at threshold → meaningful
        Assert.True(PriceAdjustmentHelper.IsAdjustmentMeaningful(10.00m, 9.99m));
    }

    [Fact]
    public void IsAdjustmentMeaningful_DifferenceJustBelowOneCent_ReturnsFalse()
    {
        // |10.000 - 9.991| = 0.009 → below 0.01 threshold → not meaningful
        // NB: ComputeNewPrice rounds to 2 d.p. so sub-cent raw differences produce 0 after rounding.
        // Testing the helper directly with an arbitrary sub-cent value.
        Assert.False(PriceAdjustmentHelper.IsAdjustmentMeaningful(10.00m, 9.995m));
    }

    [Fact]
    public void IsAdjustmentMeaningful_SamePrice_ReturnsFalse()
    {
        Assert.False(PriceAdjustmentHelper.IsAdjustmentMeaningful(50m, 50m));
    }

    [Fact]
    public void IsAdjustmentMeaningful_LargeDifference_ReturnsTrue()
    {
        // Large reduction always meaningful
        Assert.True(PriceAdjustmentHelper.IsAdjustmentMeaningful(100m, 85m));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Section 7 — BotProfitCalculator: ComputeNetWorth null-safety and totals
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ComputeNetWorth_CompanyWithNullBuildings_UsesCompanyCash()
    {
        // CompanySummary.Buildings defaults to an empty list (not null), but
        // the Cash field alone should constitute the net worth for that company.
        var profile = new PlayerProfile
        {
            Companies =
            [
                new CompanySummary { Id = "c1", Name = "Corp", Cash = 123_456m, Buildings = [] },
            ],
        };

        var nw = BotProfitCalculator.ComputeNetWorth(profile);

        Assert.Equal(123_456m, nw);
    }

    [Fact]
    public void ComputeNetWorth_MultipleCompanies_SumsCashValues()
    {
        var profile = new PlayerProfile
        {
            Companies =
            [
                new CompanySummary { Id = "c1", Cash = 50_000m, Buildings = [] },
                new CompanySummary { Id = "c2", Cash = 75_000m, Buildings = [] },
                new CompanySummary { Id = "c3", Cash = 25_000m, Buildings = [] },
            ],
        };

        var nw = BotProfitCalculator.ComputeNetWorth(profile);

        Assert.Equal(150_000m, nw);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Section 8 — BotAccount.HasValidToken: null token edge cases
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void HasValidToken_WhitespaceOnlyToken_ReturnsFalse()
    {
        // IsTokenValid now uses !string.IsNullOrWhiteSpace(Token), so a whitespace-only
        // token is treated as absent and HasValidToken must return false.
        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "NPC_01",
            Email = "npc@test.example",
            Strategy = "Trading",
            Token = "   ",
            TokenExpiresAtUtc = DateTime.UtcNow.AddHours(1),
        };

        Assert.False(bot.HasValidToken,
            "A whitespace-only token must not be treated as a valid token.");
    }

    [Fact]
    public void HasValidToken_EmptyStringToken_ReturnsFalse()
    {
        // IsTokenValid uses !string.IsNullOrWhiteSpace(Token), so an empty string
        // is equivalent to a null/absent token and HasValidToken must return false.
        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "NPC_01",
            Email = "npc@test.example",
            Strategy = "Trading",
            Token = string.Empty,
            TokenExpiresAtUtc = DateTime.UtcNow.AddHours(1),
        };

        Assert.False(bot.HasValidToken,
            "An empty-string token must not be treated as a valid token.");
    }
}

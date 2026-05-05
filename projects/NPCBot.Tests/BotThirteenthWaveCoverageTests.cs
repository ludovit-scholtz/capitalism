using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Thirteenth-wave coverage tests, focusing on:
/// <list type="bullet">
///   <item><b>Full adaptive recovery integration</b> — tick 1 unprofitable (price reduced),
///   tick 2 profitable (no reduction): verifies the complete feedback loop in the orchestrator.</item>
///   <item><b>BotOptions.MaxConsecutiveErrors default</b> — regression lock at 5.</item>
///   <item><b>BotRosterFactory second strategy cycle</b> — bot index 6 cycles back to "Trading".</item>
///   <item><b>BotStateValidator.Validate perfectly-healthy bot</b> — zero issues, IsValid=true.</item>
///   <item><b>PriceAdjustmentHelper.ComputeNewPrice with AggressivePriceReductionFactor</b> — constant
///   regression guard paired with a concrete computation check.</item>
///   <item><b>PickCheapestFreeProduct multiple products at same price</b> — returns non-null.</item>
///   <item><b>BotAccount.ProfitDelta consistency after consecutive net-worth updates</b> — always
///   CurrentNetWorth − InitialNetWorth regardless of how many updates are applied.</item>
///   <item><b>ComputeRecommendationForBot with zero initial net worth</b> — returns NoAction.</item>
///   <item><b>IsTokenValid exact 5-minute buffer boundary</b> — expiry at exactly buffer = false,
///   expiry at buffer + 1 second = true.</item>
///   <item><b>Multi-bot tick: profitable and unprofitable bots in same tick</b> — price adjustment
///   called only for the unprofitable bot in a two-bot roster.</item>
///   <item><b>BotProfitCalculator.Recommend exactly at −10% boundary</b> — severe vs mild boundary.</item>
///   <item><b>BotAccount.ToString format</b> — contains index, display name, and strategy.</item>
///   <item><b>PickCheapestAvailableLot with multiple lots at same price</b> — returns non-null.</item>
///   <item><b>SelectAdjustableUnits ordering invariant</b> — only PUBLIC_SALES units returned.</item>
/// </list>
/// </summary>
public sealed class BotThirteenthWaveCoverageTests
{
    // ── Fake services ────────────────────────────────────────────────────────

    private sealed class FakeAccountService : IAccountService
    {
        private readonly Queue<(string token, DateTime expiry)> _authQueue = new();
        private readonly Queue<PlayerProfile> _profileQueue = new();
        private readonly Queue<GameStateSummary> _gameStateQueue = new();
        private readonly Queue<List<RankingEntry>> _rankingsQueue = new();

        public int RegisterOrLoginCallCount;
        public int FetchProfileCallCount;
        public int FetchGameStateCallCount;

        private CancellationTokenSource? _cancelCts;
        private int _cancelAfterN;

        /// <summary>
        /// Cancel <paramref name="cts"/> on the <paramref name="onCallN"/>th
        /// <see cref="FetchGameStateAsync"/> call (same semantics as BotFourthWaveCoverageTests).
        /// onCallN=3 → 1 full tick (cancel at start of tick 2, bots for tick 2 never run).
        /// onCallN=4 → 2 full ticks.
        /// </summary>
        public void CancelCtsAfterGameStateFetch(CancellationTokenSource cts, int onCallN)
        {
            _cancelCts = cts;
            _cancelAfterN = onCallN;
        }

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
            var item = _authQueue.Count > 0
                ? _authQueue.Dequeue()
                : ("default-token", DateTime.UtcNow.AddHours(2));
            return Task.FromResult(item);
        }

        public Task<(string token, DateTime expiresAt)> LoginAsync(
            BotAccount bot, CancellationToken ct) =>
            Task.FromResult(_authQueue.Count > 0
                ? _authQueue.Dequeue()
                : ("refreshed-token", DateTime.UtcNow.AddHours(2)));

        public Task<PlayerProfile> FetchProfileAsync(string token, CancellationToken ct)
        {
            FetchProfileCallCount++;
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
            if (_cancelCts is not null && FetchGameStateCallCount >= _cancelAfterN)
                _cancelCts.Cancel();
            return Task.FromResult(gs);
        }

        public Task<List<RankingEntry>> FetchRankingsAsync(CancellationToken ct) =>
            Task.FromResult(_rankingsQueue.Count > 0
                ? _rankingsQueue.Dequeue()
                : new List<RankingEntry>());

        public Task<UnitSummary> UpdatePublicSalesPriceAsync(
            string unitId, decimal newMinPrice, string token, CancellationToken ct) =>
            Task.FromResult(new UnitSummary { Id = unitId, MinPrice = newMinPrice });
    }

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

    private sealed class FakeOnboardingService : IOnboardingService
    {
        public Task RunAsync(BotAccount bot, string[] allowedIndustries, CancellationToken ct)
            => Task.CompletedTask;
    }

    private static BotOrchestrator MakeOrchestrator(
        FakeAccountService accounts,
        FakePriceAdjustmentService? priceAdj = null,
        BotOptions? options = null,
        BotAccount[]? bots = null)
    {
        // PollIntervalSeconds=1 is critical so tests run in ~N seconds, not N minutes.
        var opts = options ?? new BotOptions { Enabled = true, PollIntervalSeconds = 1 };
        priceAdj ??= new FakePriceAdjustmentService();
        bots ??= [new BotAccount { Index = 1, DisplayName = "NPC_Trading_01", Email = "npc01@test", Strategy = "Trading" }];

        return new BotOrchestrator(
            bots,
            accounts,
            new FakeOnboardingService(),
            priceAdj,
            Options.Create(opts),
            NullLogger<BotOrchestrator>.Instance);
    }

    private static PlayerProfile MakeProfile(decimal cash) => new()
    {
        Id = "p1",
        DisplayName = "NPC 001",
        Email = "npc@test",
        OnboardingCompletedAtUtc = DateTime.UtcNow.AddMinutes(-60),
        Companies = [new CompanySummary { Id = "c1", Name = "MyCompany", Cash = cash }],
    };

    // ── Full adaptive recovery ────────────────────────────────────────────────

    /// <summary>
    /// Tick 1 sees a mild loss (−6 %); price adjustment service is called.
    /// Tick 2 the bot is profitable (+11 %); price adjustment service is NOT called.
    ///
    /// <para>
    /// Cancel timing: onCallN=4 cancels at the start of tick 3 (before bots are ticked),
    /// so exactly 2 full ticks run. PollIntervalSeconds=1 keeps the test runtime to ~2 s.
    /// </para>
    /// </summary>
    [Fact]
    public async Task AdaptiveRecovery_UnprofitableTick1_ProfitableTick2_AdjustmentCalledOnce()
    {
        const decimal initCash  = 100_000m;
        const decimal lossCash  = 94_000m;    // –6 % → mild loss → ShouldAct=true
        const decimal gainCash  = 111_000m;   // +11 % → profitable → ShouldAct=false

        var accounts = new FakeAccountService();
        var priceAdj = new FakePriceAdjustmentService();

        // Init game state (TrackingStartTick=0)
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 0, TickIntervalSeconds = 60 });
        // Tick 1 game state (ticksElapsed = 100-0 = 100 ≥ MinTicksBeforeAdjustment=5)
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 100, TickIntervalSeconds = 60 });
        // Tick 2 game state
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 200, TickIntervalSeconds = 60 });

        // Profiles: init-check + init-net-worth + tick1 + tick2
        accounts.EnqueueProfile(MakeProfile(initCash));   // init: onboarding check
        accounts.EnqueueProfile(MakeProfile(initCash));   // init: initial net worth
        accounts.EnqueueProfile(MakeProfile(lossCash));   // tick 1: mild loss
        accounts.EnqueueProfile(MakeProfile(gainCash));   // tick 2: profitable

        // Cancel at call 4 → 2 full ticks run (init=1, tick1=2, tick2=3, cancel-at=4)
        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 4);

        var orchestrator = MakeOrchestrator(accounts, priceAdj,
            options: new BotOptions
            {
                Enabled = true,
                PollIntervalSeconds = 1,
                MinTicksBeforeAdjustment = 5,
            });

        await orchestrator.RunAsync(cts.Token);

        // Tick 1: mild loss → 1 price adjustment
        // Tick 2: profitable → 0 price adjustments
        Assert.Equal(1, priceAdj.CallCount);
        Assert.NotNull(priceAdj.LastRecommendation);
        Assert.True(priceAdj.LastRecommendation!.ShouldAct);
        Assert.Equal(BotProfitCalculator.MildPriceReductionFactor, priceAdj.LastRecommendation.PriceAdjustmentFactor);
    }

    // ── BotOptions.MaxConsecutiveErrors default ───────────────────────────────

    [Fact]
    public void BotOptions_MaxConsecutiveErrors_DefaultIsFive()
    {
        var opts = new BotOptions();
        Assert.Equal(5, opts.MaxConsecutiveErrors);
    }

    // ── BotRosterFactory: second strategy cycle ───────────────────────────────

    [Fact]
    public void BotRosterFactory_Bot6_CyclesBackToTradingStrategy()
    {
        // Strategies = ["Trading", "Industrial", "Retail", "Mixed", "Aggressive"]
        // Bot 6 (i=6) → (6-1) % 5 = 0 → "Trading" (wrap-around)
        var bots = BotRosterFactory.Build(new BotOptions { BotCount = 6 });

        Assert.Equal("Trading",    bots[0].Strategy); // bot 1
        Assert.Equal("Industrial", bots[1].Strategy); // bot 2
        Assert.Equal("Retail",     bots[2].Strategy); // bot 3
        Assert.Equal("Mixed",      bots[3].Strategy); // bot 4
        Assert.Equal("Aggressive", bots[4].Strategy); // bot 5
        Assert.Equal("Trading",    bots[5].Strategy); // bot 6 – cycles back to first strategy
    }

    [Fact]
    public void BotRosterFactory_Bot10_HasAggressiveStrategy()
    {
        // Bot 10 (i=10) → (10-1) % 5 = 4 → "Aggressive"
        var bots = BotRosterFactory.Build(new BotOptions { BotCount = 10 });

        Assert.Equal("Aggressive", bots[4].Strategy); // bot 5
        Assert.Equal("Aggressive", bots[9].Strategy); // bot 10 (second cycle)
    }

    // ── BotStateValidator: perfectly-healthy bot ─────────────────────────────

    [Fact]
    public void BotStateValidator_Validate_HealthyBot_IsValidAndNoIssues()
    {
        var bot = new BotAccount
        {
            Token = "valid-jwt",
            TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2),
            Profile = new PlayerProfile
            {
                OnboardingCompletedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            },
            LastSuccessUtc = DateTime.UtcNow.AddMinutes(-1),
        };

        var result = BotStateValidator.Validate(bot, staleAfterMinutes: 10);

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
        Assert.Equal("Bot is ready for operation.", result.Summary);
    }

    // ── PriceAdjustmentHelper: AggressivePriceReductionFactor computation ────

    [Fact]
    public void PriceAdjustmentHelper_ComputeNewPrice_AggressiveFactor_Is85PercentOfOriginal()
    {
        // AggressivePriceReductionFactor = 0.85 → 1 000 × 0.85 = 850.00
        const decimal current = 1_000m;
        var result = PriceAdjustmentHelper.ComputeNewPrice(current, BotProfitCalculator.AggressivePriceReductionFactor);

        Assert.Equal(850.00m, result);
    }

    [Fact]
    public void BotProfitCalculator_AggressiveFactor_IsLessThanMildFactor()
    {
        // "Aggressive" means a bigger price cut → smaller factor value.
        Assert.True(
            BotProfitCalculator.AggressivePriceReductionFactor < BotProfitCalculator.MildPriceReductionFactor,
            "Aggressive factor should be smaller (bigger discount) than mild factor.");
    }

    // ── PickCheapestFreeProduct: multiple products at same price ──────────────

    [Fact]
    public void PickCheapestFreeProduct_MultipleAtSameLowestPrice_ReturnsNonNull()
    {
        var products = new[]
        {
            new ProductTypeSummary { Id = "p1", Name = "Chair", BasePrice = 5m,  IsProOnly = false },
            new ProductTypeSummary { Id = "p2", Name = "Table", BasePrice = 5m,  IsProOnly = false },
            new ProductTypeSummary { Id = "p3", Name = "Bed",   BasePrice = 7m,  IsProOnly = false },
        };

        var result = OnboardingHelpers.PickCheapestFreeProduct(products);

        Assert.NotNull(result);
        Assert.Equal(5m, result.BasePrice);
        Assert.False(result.IsProOnly);
    }

    // ── BotAccount.ProfitDelta consistency ───────────────────────────────────

    [Fact]
    public void BotAccount_ProfitDelta_AlwaysCurrentMinusInitial_AfterConsecutiveUpdates()
    {
        var bot = new BotAccount { InitialNetWorth = 50_000m };

        bot.CurrentNetWorth = 55_000m;
        Assert.Equal(5_000m, bot.ProfitDelta);

        bot.CurrentNetWorth = 48_000m;
        Assert.Equal(-2_000m, bot.ProfitDelta);

        bot.CurrentNetWorth = 50_000m;
        Assert.Equal(0m, bot.ProfitDelta);
    }

    // ── ComputeRecommendationForBot: zero initial net worth ──────────────────

    [Fact]
    public void ComputeRecommendationForBot_ZeroInitialNetWorth_ReturnsNoAction()
    {
        var bot = new BotAccount
        {
            InitialNetWorth = 0m,
            CurrentNetWorth = 0m,
            TrackingStartTick = 0,
        };

        var result = BotOrchestrator.ComputeRecommendationForBot(bot, currentTick: 100, minTicksBeforeAdjustment: 5);

        Assert.False(result.ShouldAct);
        Assert.Same(StrategyRecommendation.NoAction, result);
    }

    // ── IsTokenValid exact 5-minute buffer boundary ──────────────────────────

    [Fact]
    public void IsTokenValid_ExpiryExactlyAtBuffer_ReturnsFalse()
    {
        // Token expires in exactly 5 minutes: expiry - buffer = now → not strictly in the future.
        var bot = new BotAccount
        {
            Token = "any-token",
            TokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
        };

        Assert.False(bot.IsTokenValid(bufferMinutes: 5));
    }

    [Fact]
    public void IsTokenValid_ExpiryJustAboveBuffer_ReturnsTrue()
    {
        // Token expires in 5 min + 10 sec: still safely valid.
        var bot = new BotAccount
        {
            Token = "any-token",
            TokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(5).AddSeconds(10),
        };

        Assert.True(bot.IsTokenValid(bufferMinutes: 5));
    }

    // ── Multi-bot tick: profitable + unprofitable in same tick ────────────────

    /// <summary>
    /// Two bots run in the same tick: bot1 is profitable, bot2 is unprofitable.
    /// Price adjustment must be called exactly once (for bot2 only).
    ///
    /// <para>
    /// Cancel timing: onCallN=3 gives exactly 1 full tick
    /// (init=call1, tick1=call2, cancel-at=call3 which aborts tick2 before bots run).
    /// PollIntervalSeconds=1 keeps runtime to ~1 s.
    /// </para>
    /// </summary>
    [Fact]
    public async Task TwoBots_OneProfitable_OneUnprofitable_PriceAdjustmentCalledOnceForUnprofitable()
    {
        const decimal initCash   = 100_000m;
        const decimal profitCash = 115_000m; // +15 % → Profitable → no adjustment
        const decimal lossCash   =  93_000m; // –7 % → Unprofitable (mild) → adjustment

        var bot1 = new BotAccount { Index = 1, DisplayName = "NPC_Trading_01",    Email = "b1@t", Strategy = "Trading" };
        var bot2 = new BotAccount { Index = 2, DisplayName = "NPC_Industrial_02", Email = "b2@t", Strategy = "Industrial" };

        var accounts = new FakeAccountService();
        var priceAdj = new FakePriceAdjustmentService();

        // Game states
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 0,   TickIntervalSeconds = 60 }); // init
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 100, TickIntervalSeconds = 60 }); // tick 1

        // Init profiles: 2 per bot (init-check + init-net-worth)
        accounts.EnqueueProfile(MakeProfile(initCash));  // bot1 init-check
        accounts.EnqueueProfile(MakeProfile(initCash));  // bot1 init-net-worth
        accounts.EnqueueProfile(MakeProfile(initCash));  // bot2 init-check
        accounts.EnqueueProfile(MakeProfile(initCash));  // bot2 init-net-worth

        // Tick 1 profiles: 1 per bot
        accounts.EnqueueProfile(MakeProfile(profitCash)); // bot1: profitable
        accounts.EnqueueProfile(MakeProfile(lossCash));   // bot2: mild loss

        // Cancel at call 3 → exactly 1 full tick runs
        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3);

        var orchestrator = MakeOrchestrator(
            accounts,
            priceAdj,
            options: new BotOptions
            {
                Enabled = true,
                PollIntervalSeconds = 1,
                MinTicksBeforeAdjustment = 5,
            },
            bots: [bot1, bot2]);

        await orchestrator.RunAsync(cts.Token);

        // Bot1 (profitable) should NOT trigger adjustment; bot2 (mild loss) SHOULD.
        Assert.Equal(1, priceAdj.CallCount);
    }

    // ── BotProfitCalculator.Recommend exactly at –10 % boundary ─────────────

    [Fact]
    public void BotProfitCalculator_Recommend_ExactlyAtSevereThreshold_ProducesAggressiveReduction()
    {
        // –10 % exactly → severe (≤ SeverelyUnprofitableThresholdPercent = –0.10)
        const decimal initial = 100_000m;
        const decimal current = 90_000m;  // –10.00 %
        const int elapsed = 10;

        var result = BotProfitCalculator.Recommend(current, initial, elapsed, minTicksBeforeAdjustment: 5);

        Assert.True(result.ShouldAct);
        Assert.Equal(BotProfitCalculator.AggressivePriceReductionFactor, result.PriceAdjustmentFactor);
    }

    [Fact]
    public void BotProfitCalculator_Recommend_JustAboveSevereThreshold_ProducesMildReduction()
    {
        // –9.9 % → mild (within "mild loss" range: –2 % to –10 % exclusive)
        const decimal initial = 100_000m;
        const decimal current = 90_100m;  // –9.9 %
        const int elapsed = 10;

        var result = BotProfitCalculator.Recommend(current, initial, elapsed, minTicksBeforeAdjustment: 5);

        Assert.True(result.ShouldAct);
        Assert.Equal(BotProfitCalculator.MildPriceReductionFactor, result.PriceAdjustmentFactor);
    }

    // ── BotOptions numeric defaults ───────────────────────────────────────────

    [Fact]
    public void BotOptions_PollIntervalSeconds_DefaultIsPositive()
    {
        var opts = new BotOptions();
        Assert.True(opts.PollIntervalSeconds > 0,
            $"PollIntervalSeconds must be positive; got {opts.PollIntervalSeconds}.");
    }

    // ── BotAccount.ToString format ────────────────────────────────────────────

    [Fact]
    public void BotAccount_ToString_ContainsIndexDisplayNameAndStrategy()
    {
        var bot = new BotAccount
        {
            Index = 7,
            DisplayName = "NPC_Industrial_07",
            Strategy = "Industrial",
        };

        var str = bot.ToString();

        Assert.Contains("#7", str);
        Assert.Contains("NPC_Industrial_07", str);
        Assert.Contains("Industrial", str);
    }

    // ── PickCheapestAvailableLot: multiple lots with same price ───────────────

    [Fact]
    public void PickCheapestAvailableLot_MultipleLotsSamePrice_ReturnsOneWithLowestPrice()
    {
        var lots = new[]
        {
            new BuildingLotSummary { Id = "l1", Price = 50_000m, SuitableTypes = "FACTORY", BuildingId = null },
            new BuildingLotSummary { Id = "l2", Price = 50_000m, SuitableTypes = "FACTORY", BuildingId = null },
            new BuildingLotSummary { Id = "l3", Price = 75_000m, SuitableTypes = "FACTORY", BuildingId = null },
        };

        var result = OnboardingHelpers.PickCheapestAvailableLot(lots, "FACTORY");

        Assert.NotNull(result);
        Assert.Equal(50_000m, result.Price);
    }

    // ── SelectAdjustableUnits: only PUBLIC_SALES returned ────────────────────

    [Fact]
    public void SelectAdjustableUnits_MixedUnitTypes_OnlyPublicSalesReturned()
    {
        var companies = new List<CompanySummary>
        {
            new()
            {
                Id = "c1",
                Name = "Alpha",
                Buildings =
                [
                    new BuildingSummary
                    {
                        Id = "b1",
                        Name = "Shop A",
                        Units =
                        [
                            new UnitSummary { Id = "u1", UnitType = "PUBLIC_SALES",  MinPrice = 10m },
                            new UnitSummary { Id = "u2", UnitType = "MANUFACTURING", MinPrice = 5m  },
                            new UnitSummary { Id = "u3", UnitType = "PUBLIC_SALES",  MinPrice = 20m },
                        ],
                    },
                ],
            },
        };

        var adjustable = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();

        Assert.Equal(2, adjustable.Count);
        Assert.All(adjustable, pair =>
            Assert.Equal("PUBLIC_SALES", pair.Unit.UnitType, StringComparer.OrdinalIgnoreCase));
    }
}

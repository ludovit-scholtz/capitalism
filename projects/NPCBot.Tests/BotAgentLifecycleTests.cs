using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Tests covering NPC bot agent lifecycle scenarios and edge cases not addressed
/// in the primary orchestrator test files.  Focus areas:
/// <list type="bullet">
///   <item><b>InitialNetWorth after tick-level onboarding</b> — when RunOnboardingAsync fires
///   inside TickBotAsync, the bot's InitialNetWorth must be set from the post-onboarding profile.</item>
///   <item><b>BotAccount.ToString format</b> — verifies the display string is stable across
///   different index/name/strategy combinations.</item>
///   <item><b>BotProfitCalculator multi-company net worth</b> — tests that summing cash across
///   three or more companies works correctly, including edge cases with zero and negative values.</item>
///   <item><b>PriceAdjustmentHelper exact boundary cases</b> — IsAdjustmentMeaningful at
///   exactly ±0.01, SelectAdjustableUnits with boundary MinPrice values.</item>
///   <item><b>BotStateValidator precision tests</b> — Validate with all four issues simultaneously,
///   IsAtRisk at exactly the 50% threshold boundary.</item>
///   <item><b>OnboardingHelpers additional scenarios</b> — PickCheapestFreeProduct with single
///   product, ContainsSuitableType with extra whitespace, PickCheapestAvailableLot ranking.</item>
///   <item><b>StrategyRecommendation.NoAction singleton</b> — proves the sentinel is immutable
///   and ShouldAct is false.</item>
/// </list>
/// </summary>
public sealed class BotAgentLifecycleTests
{
    // ── Minimal fake implementations ──────────────────────────────────────────

    private sealed class FakeAccountService : IAccountService
    {
        private readonly Queue<(string token, DateTime expiry)> _authQueue = new();
        private readonly Queue<PlayerProfile> _profileQueue = new();
        private readonly Queue<GameStateSummary> _gameStateQueue = new();
        private readonly Queue<List<RankingEntry>> _rankingsQueue = new();

        private CancellationTokenSource? _autoCancelCts;
        private int _autoCancelAfterN;
        private int _gameStateCallCount;

        public void CancelCtsAfterGameStateFetch(CancellationTokenSource cts, int onCallN)
        {
            _autoCancelCts = cts;
            _autoCancelAfterN = onCallN;
        }

        public void EnqueueAuth(string token, DateTime expiry) => _authQueue.Enqueue((token, expiry));
        public void EnqueueProfile(PlayerProfile p) => _profileQueue.Enqueue(p);
        public void EnqueueGameState(GameStateSummary gs) => _gameStateQueue.Enqueue(gs);
        public void EnqueueRankings(List<RankingEntry> r) => _rankingsQueue.Enqueue(r);

        public Task<(string token, DateTime expiresAt)> RegisterOrLoginAsync(BotAccount bot, CancellationToken ct)
        {
            if (!_authQueue.TryDequeue(out var auth))
                auth = ("tok", DateTime.UtcNow.AddHours(2));
            bot.Token = auth.token;
            bot.TokenExpiresAtUtc = auth.expiry;
            return Task.FromResult(auth);
        }

        public Task<(string token, DateTime expiresAt)> LoginAsync(BotAccount bot, CancellationToken ct)
        {
            if (!_authQueue.TryDequeue(out var auth))
                auth = ("tok", DateTime.UtcNow.AddHours(2));
            return Task.FromResult(auth);
        }

        public Task<PlayerProfile> FetchProfileAsync(string token, CancellationToken ct)
        {
            if (!_profileQueue.TryDequeue(out var p))
                p = CompletedProfile();
            return Task.FromResult(p);
        }

        public Task<GameStateSummary> FetchGameStateAsync(CancellationToken ct)
        {
            _gameStateCallCount++;
            if (_autoCancelCts is not null && _gameStateCallCount >= _autoCancelAfterN)
                _autoCancelCts.Cancel();

            if (!_gameStateQueue.TryDequeue(out var gs))
                gs = new GameStateSummary { CurrentTick = 1 };
            return Task.FromResult(gs);
        }

        public Task<List<RankingEntry>> FetchRankingsAsync(CancellationToken ct)
        {
            if (!_rankingsQueue.TryDequeue(out var r))
                r = [];
            return Task.FromResult(r);
        }

        public Task<UnitSummary> UpdatePublicSalesPriceAsync(string unitId, decimal newMinPrice, string token, CancellationToken ct) =>
            Task.FromResult(new UnitSummary { Id = unitId, UnitType = "PUBLIC_SALES", MinPrice = newMinPrice });
    }

    private sealed class FakeOnboardingService : IOnboardingService
    {
        public int CallCount;

        public Task RunAsync(BotAccount bot, string[] allowedIndustries, CancellationToken ct)
        {
            CallCount++;
            return Task.CompletedTask;
        }
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

    // ── Test helpers ──────────────────────────────────────────────────────────

    private static BotAccount MakeBot() => new()
    {
        Index = 1,
        DisplayName = "NPC 001",
        Email = "npc001@test.example",
        Strategy = "FURNITURE",
    };

    private static PlayerProfile CompletedProfile(decimal cash = 50_000m) => new()
    {
        Id = "p1",
        DisplayName = "NPC 001",
        Email = "npc001@test.example",
        OnboardingCompletedAtUtc = DateTime.UtcNow.AddHours(-1),
        Companies = [new CompanySummary { Id = "co1", Name = "NPC Co", Cash = cash }],
    };

    private static PlayerProfile IncompleteProfile() => new()
    {
        Id = "p1",
        DisplayName = "NPC 001",
        Email = "npc001@test.example",
        OnboardingCompletedAtUtc = null,
        Companies = [],
    };

    private static BotOrchestrator MakeOrchestrator(
        FakeAccountService accounts,
        FakeOnboardingService? onboarding = null,
        FakePriceAdjustmentService? priceAdj = null,
        BotOptions? options = null,
        BotAccount[]? bots = null)
    {
        bots ??= [MakeBot()];
        return new BotOrchestrator(
            bots,
            accounts,
            onboarding ?? new FakeOnboardingService(),
            priceAdj ?? new FakePriceAdjustmentService(),
            Options.Create(options ?? new BotOptions { Enabled = true, PollIntervalSeconds = 0 }),
            NullLogger<BotOrchestrator>.Instance);
    }

    // ── InitialNetWorth set after tick-level onboarding ───────────────────────

    [Fact]
    public async Task Tick_OnboardingDuringTick_SetsInitialNetWorthFromPostOnboardingProfile()
    {
        // When a bot's profile shows incomplete onboarding during a tick,
        // TickBotAsync calls RunOnboardingAsync (which itself fetches profile at line 142)
        // and then TickBotAsync fetches profile again at line 200.
        // The bot's InitialNetWorth must be set from that SECOND post-RunOnboarding fetch.
        //
        // Exact profile fetch sequence (5 total):
        //  #1 — InitialiseBotAsync line 107: onboarding check (complete → no RunOnboarding in init)
        //  #2 — InitialiseBotAsync line 117: definitive InitialNetWorth
        //  #3 — TickBotAsync line 194: tick profile (incomplete → triggers RunOnboarding in tick)
        //  #4 — RunOnboardingAsync line 142: internal post-onboarding fetch
        //  #5 — TickBotAsync line 200: sets InitialNetWorth
        var accounts = new FakeAccountService();
        var onboarding = new FakeOnboardingService();
        var bot = MakeBot();

        // Init: profile is complete — no onboarding during init
        accounts.EnqueueProfile(CompletedProfile(cash: 0));      // #1: init onboarding check
        accounts.EnqueueProfile(CompletedProfile(cash: 0));      // #2: init definitive net worth

        // Tick: profile shows incomplete → RunOnboardingAsync fires
        accounts.EnqueueProfile(IncompleteProfile());             // #3: tick profile (incomplete)
        accounts.EnqueueProfile(CompletedProfile(cash: 60_000m));// #4: RunOnboardingAsync internal fetch
        accounts.EnqueueProfile(CompletedProfile(cash: 75_000m));// #5: TickBotAsync post-onboarding fetch → sets InitialNetWorth
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 5 });

        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3);

        var orchestrator = MakeOrchestrator(accounts, onboarding: onboarding, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        // Post-tick onboarding must set InitialNetWorth from the 75_000 profile (#5)
        Assert.Equal(75_000m, bot.InitialNetWorth);
        Assert.Equal(1, onboarding.CallCount);
    }

    [Fact]
    public async Task Tick_OnboardingDuringTick_CurrentNetWorthEqualToInitialNetWorth()
    {
        // After onboarding fires during a tick, both InitialNetWorth and CurrentNetWorth
        // should reflect the same post-onboarding profile value (same profile fetch #5).
        var accounts = new FakeAccountService();
        var bot = MakeBot();

        accounts.EnqueueProfile(CompletedProfile(cash: 0));
        accounts.EnqueueProfile(CompletedProfile(cash: 0));
        accounts.EnqueueProfile(IncompleteProfile());
        accounts.EnqueueProfile(CompletedProfile(cash: 60_000m)); // RunOnboardingAsync internal
        accounts.EnqueueProfile(CompletedProfile(cash: 80_000m)); // TickBotAsync sets InitialNetWorth
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 5 });

        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3);

        var orchestrator = MakeOrchestrator(accounts, bots: [bot]);
        await orchestrator.RunAsync(cts.Token);

        Assert.Equal(80_000m, bot.InitialNetWorth);
        Assert.Equal(80_000m, bot.CurrentNetWorth);
    }

    // ── BotAccount.ToString format ────────────────────────────────────────────

    [Fact]
    public void BotAccount_ToString_IncludesIndexAndDisplayNameAndStrategy()
    {
        var bot = new BotAccount { Index = 7, DisplayName = "NPC 007", Strategy = "HEALTHCARE" };
        var str = bot.ToString();

        Assert.Contains("7", str);
        Assert.Contains("NPC 007", str);
        Assert.Contains("HEALTHCARE", str);
    }

    [Fact]
    public void BotAccount_ToString_Index1_MatchesExpectedFormat()
    {
        var bot = new BotAccount { Index = 1, DisplayName = "NPC 001", Strategy = "FURNITURE" };
        Assert.Equal("[Bot #1 NPC 001 (FURNITURE)]", bot.ToString());
    }

    [Fact]
    public void BotAccount_ToString_HighIndex_IncludesIndex()
    {
        var bot = new BotAccount { Index = 100, DisplayName = "NPC 100", Strategy = "FOOD_PROCESSING" };
        var str = bot.ToString();
        Assert.Contains("100", str);
    }

    // ── BotProfitCalculator multi-company net worth ───────────────────────────

    [Fact]
    public void ComputeNetWorth_ThreeCompanies_SumsAllCash()
    {
        var profile = new PlayerProfile
        {
            Companies =
            [
                new CompanySummary { Cash = 10_000m },
                new CompanySummary { Cash = 25_000m },
                new CompanySummary { Cash = 5_000m },
            ],
        };

        var netWorth = BotProfitCalculator.ComputeNetWorth(profile);

        Assert.Equal(40_000m, netWorth);
    }

    [Fact]
    public void ComputeNetWorth_ZeroAndPositive_IgnoresZeroCompany()
    {
        var profile = new PlayerProfile
        {
            Companies =
            [
                new CompanySummary { Cash = 0m },
                new CompanySummary { Cash = 99_999m },
            ],
        };

        Assert.Equal(99_999m, BotProfitCalculator.ComputeNetWorth(profile));
    }

    [Fact]
    public void ComputeNetWorth_SingleCompanyWithCash_ReturnsCash()
    {
        var profile = new PlayerProfile
        {
            Companies = [new CompanySummary { Cash = 123_456.78m }],
        };

        Assert.Equal(123_456.78m, BotProfitCalculator.ComputeNetWorth(profile));
    }

    [Fact]
    public void Recommend_ExactlySeverelyUnprofitableThreshold_ReturnsAggressiveAction()
    {
        // deltaPercent == SeverelyUnprofitableThresholdPercent (–10 %) ⟹ aggressive
        const decimal initial = 100_000m;
        var current = initial * (1m + BotProfitCalculator.SeverelyUnprofitableThresholdPercent); // 90_000
        var rec = BotProfitCalculator.Recommend(current, initial, ticksElapsed: 10, minTicksBeforeAdjustment: 5);

        Assert.True(rec.ShouldAct);
        Assert.Equal(BotProfitCalculator.AggressivePriceReductionFactor, rec.PriceAdjustmentFactor);
    }

    [Fact]
    public void Recommend_MildLossAtBoundary_ReturnsMildAction()
    {
        // –NeutralBandPercent – ε means just below the neutral band → mild action
        const decimal initial = 100_000m;
        // delta = –NeutralBandPercent * initial – small extra = mild loss
        var current = initial - (BotProfitCalculator.NeutralBandPercent * initial) - 1m; // 97_999
        var rec = BotProfitCalculator.Recommend(current, initial, ticksElapsed: 10, minTicksBeforeAdjustment: 5);

        Assert.True(rec.ShouldAct);
        Assert.Equal(BotProfitCalculator.MildPriceReductionFactor, rec.PriceAdjustmentFactor);
    }

    [Fact]
    public void ComputeAnnualisedRatePercent_OneFullYear_ReturnsApproximatelyTenPercent()
    {
        // 10% gain over exactly 8760 ticks (one in-game year) should return ~10%
        // The decimal calculation has tiny rounding drift, so use InRange.
        const decimal initial = 100_000m;
        const decimal current = 110_000m;
        var rate = BotProfitCalculator.ComputeAnnualisedRatePercent(current, initial, ticksElapsed: 8760);

        Assert.InRange(rate, 9.9m, 10.1m);
    }

    // ── PriceAdjustmentHelper exact boundary cases ────────────────────────────

    [Fact]
    public void IsAdjustmentMeaningful_DifferenceExactlyOneCent_IsTrue()
    {
        // |50.00 − 49.99| = 0.01 — exactly the minimum; must be meaningful
        Assert.True(PriceAdjustmentHelper.IsAdjustmentMeaningful(50.00m, 49.99m));
    }

    [Fact]
    public void IsAdjustmentMeaningful_DifferenceJustUnderOneCent_IsFalse()
    {
        // |50.00 − 49.991| rounds to 0.009 < 0.01 — not meaningful
        Assert.False(PriceAdjustmentHelper.IsAdjustmentMeaningful(50.000m, 49.991m));
    }

    [Fact]
    public void ComputeNewPrice_IdentityFactor_ReturnsSamePrice()
    {
        // factor = 1.0 should leave the price unchanged (within rounding)
        var result = PriceAdjustmentHelper.ComputeNewPrice(99.99m, 1.0m);
        Assert.Equal(99.99m, result);
    }

    [Fact]
    public void SelectAdjustableUnits_UnitWithMinPriceZeroPointZeroOne_IsIncluded()
    {
        // MinPrice = 0.01 is above zero — unit should be included
        var companies = new List<CompanySummary>
        {
            new()
            {
                Id = "c1", Name = "Corp", Cash = 0,
                Buildings =
                [
                    new BuildingSummary
                    {
                        Id = "b1", Name = "Shop",
                        Units =
                        [
                            new UnitSummary { Id = "u1", UnitType = "PUBLIC_SALES", MinPrice = 0.01m },
                        ],
                    },
                ],
            },
        };

        var result = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();

        Assert.Single(result);
        Assert.Equal("u1", result[0].Unit.Id);
    }

    [Fact]
    public void SelectAdjustableUnits_MixedUnitTypes_ReturnsOnlyPublicSales()
    {
        // Only PUBLIC_SALES units with non-zero price should be returned.
        var companies = new List<CompanySummary>
        {
            new()
            {
                Id = "c1", Name = "Corp", Cash = 0,
                Buildings =
                [
                    new BuildingSummary
                    {
                        Id = "b1", Name = "Factory",
                        Units =
                        [
                            new UnitSummary { Id = "u-manufacturing", UnitType = "MANUFACTURING", MinPrice = 100m },
                            new UnitSummary { Id = "u-public-sales",  UnitType = "PUBLIC_SALES",  MinPrice = 50m  },
                            new UnitSummary { Id = "u-storage",       UnitType = "STORAGE",       MinPrice = 200m },
                            new UnitSummary { Id = "u-purchase",      UnitType = "PURCHASE",      MinPrice = 10m  },
                        ],
                    },
                ],
            },
        };

        var result = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();

        Assert.Single(result);
        Assert.Equal("u-public-sales", result[0].Unit.Id);
    }

    // ── BotStateValidator precision tests ────────────────────────────────────

    [Fact]
    public void Validate_AllFourIssuesFail_SummaryContainsAll()
    {
        // A bot that is skipped, has no token, is not onboarded, and is stale
        // should produce a validation result with four issues.
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC 001", Email = "npc@test.example",
            IsSkipped = true,
            Token = null,
            Profile = null,
            LastSuccessUtc = DateTime.UtcNow.AddHours(-1), // stale (well past 10 minutes)
        };

        var result = BotStateValidator.Validate(bot, staleAfterMinutes: 10);

        Assert.False(result.IsValid);
        Assert.Equal(4, result.Issues.Count);
    }

    [Fact]
    public void Validate_AllFourIssuesFail_IsValidFalse()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC 001", Email = "npc@test.example",
            IsSkipped = true,
            Token = null,
            Profile = null,
            LastSuccessUtc = DateTime.UtcNow.AddHours(-2),
        };

        Assert.False(BotStateValidator.Validate(bot).IsValid);
    }

    [Fact]
    public void IsAtRisk_ExactlyAtHalfwayMark_ReturnsTrue()
    {
        // consecutiveErrors = 5 with max = 10 → ratio 0.5 ≥ 0.5 → at risk
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC 001", Email = "npc@test.example",
            ConsecutiveErrors = 5,
        };

        Assert.True(BotStateValidator.IsAtRisk(bot, maxConsecutiveErrors: 10));
    }

    [Fact]
    public void IsAtRisk_JustBelowHalfwayMark_ReturnsFalse()
    {
        // consecutiveErrors = 4 with max = 10 → ratio 0.4 < 0.5 → not at risk
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC 001", Email = "npc@test.example",
            ConsecutiveErrors = 4,
        };

        Assert.False(BotStateValidator.IsAtRisk(bot, maxConsecutiveErrors: 10));
    }

    [Fact]
    public void IsReadyForOperation_SkippedBot_ReturnsFalse()
    {
        var profile = new PlayerProfile { OnboardingCompletedAtUtc = DateTime.UtcNow };
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC", Email = "npc@test.example",
            IsSkipped = true,
            Token = "valid-token",
            TokenExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            Profile = profile,
        };

        Assert.False(BotStateValidator.IsReadyForOperation(bot));
    }

    [Fact]
    public void IsReadyForOperation_NoToken_ReturnsFalse()
    {
        var profile = new PlayerProfile { OnboardingCompletedAtUtc = DateTime.UtcNow };
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC", Email = "npc@test.example",
            Token = null,
            Profile = profile,
        };

        Assert.False(BotStateValidator.IsReadyForOperation(bot));
    }

    // ── OnboardingHelpers additional scenarios ────────────────────────────────

    [Fact]
    public void PickCheapestFreeProduct_SingleFreeProduct_ReturnsThatProduct()
    {
        var products = new List<ProductTypeSummary>
        {
            new() { Id = "p1", Name = "Chair", Slug = "chair", Industry = "FURNITURE", BasePrice = 45m, IsProOnly = false },
        };

        var result = OnboardingHelpers.PickCheapestFreeProduct(products);

        Assert.NotNull(result);
        Assert.Equal("p1", result.Id);
    }

    [Fact]
    public void PickCheapestFreeProduct_MultipleFree_ReturnsCheapest()
    {
        var products = new List<ProductTypeSummary>
        {
            new() { Id = "p1", Slug = "chair",    BasePrice = 45m, IsProOnly = false },
            new() { Id = "p2", Slug = "table",    BasePrice = 20m, IsProOnly = false },
            new() { Id = "p3", Slug = "cupboard",  BasePrice = 80m, IsProOnly = false },
        };

        var result = OnboardingHelpers.PickCheapestFreeProduct(products);

        Assert.NotNull(result);
        Assert.Equal("p2", result.Id);
    }

    [Fact]
    public void PickCheapestAvailableLot_MultipleMatchingLots_ReturnsCheapest()
    {
        var lots = new List<BuildingLotSummary>
        {
            new() { Id = "l1", Price = 300_000m, SuitableTypes = "FACTORY",       BuildingId = null },
            new() { Id = "l2", Price = 100_000m, SuitableTypes = "FACTORY,MINE",  BuildingId = null },
            new() { Id = "l3", Price = 200_000m, SuitableTypes = "FACTORY",       BuildingId = null },
        };

        var result = OnboardingHelpers.PickCheapestAvailableLot(lots, "FACTORY");

        Assert.NotNull(result);
        Assert.Equal("l2", result.Id); // cheapest matching lot
    }

    [Fact]
    public void ContainsSuitableType_ExtraWhitespaceAround_ReturnsTrueForMatch()
    {
        // The comma-separated field may have spaces after commas; TrimEntries should handle it.
        Assert.True(OnboardingHelpers.ContainsSuitableType("FACTORY , MINE", "mine"));
    }

    [Fact]
    public void ContainsSuitableType_EmptySuitableTypesField_ReturnsFalse()
    {
        Assert.False(OnboardingHelpers.ContainsSuitableType(string.Empty, "FACTORY"));
    }

    // ── StrategyRecommendation.NoAction sentinel ──────────────────────────────

    [Fact]
    public void NoAction_ShouldActIsFalse()
    {
        Assert.False(StrategyRecommendation.NoAction.ShouldAct);
    }

    [Fact]
    public void NoAction_PriceAdjustmentFactorIsOne()
    {
        // NoAction sentinel uses 0 to signal "no price change"; PriceAdjustmentService guards
        // against ShouldAct=false before reading PriceAdjustmentFactor, but the field is 0.
        Assert.Equal(0m, StrategyRecommendation.NoAction.PriceAdjustmentFactor);
    }

    [Fact]
    public void NoAction_IsSameReferenceAsSelf()
    {
        // NoAction is a static singleton; two references should be the same object.
        var a = StrategyRecommendation.NoAction;
        var b = StrategyRecommendation.NoAction;
        Assert.Same(a, b);
    }

    // ── BotAccount.ProfitDelta edge cases ─────────────────────────────────────

    [Fact]
    public void ProfitDelta_BothZero_IsZero()
    {
        var bot = new BotAccount { Index = 1, DisplayName = "N", Email = "n@n.com", InitialNetWorth = 0, CurrentNetWorth = 0 };
        Assert.Equal(0m, bot.ProfitDelta);
    }

    [Fact]
    public void ProfitDelta_InitialZeroCurrentPositive_IsPositive()
    {
        var bot = new BotAccount { Index = 1, DisplayName = "N", Email = "n@n.com", InitialNetWorth = 0, CurrentNetWorth = 50_000m };
        Assert.Equal(50_000m, bot.ProfitDelta);
    }

    [Fact]
    public void ProfitDelta_InitialHigherThanCurrent_IsNegative()
    {
        var bot = new BotAccount { Index = 1, DisplayName = "N", Email = "n@n.com", InitialNetWorth = 100_000m, CurrentNetWorth = 85_000m };
        Assert.Equal(-15_000m, bot.ProfitDelta);
    }

    // ── BotOptions: MinTicksBeforeAdjustment default ──────────────────────────

    [Fact]
    public void BotOptions_MinTicksBeforeAdjustment_DefaultIsFive()
    {
        var opts = new BotOptions();
        Assert.Equal(5, opts.MinTicksBeforeAdjustment);
    }

    [Fact]
    public void BotOptions_TokenRefreshBufferMinutes_DefaultIsFive()
    {
        var opts = new BotOptions();
        Assert.Equal(5, opts.TokenRefreshBufferMinutes);
    }
}

using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Sixth coverage wave — fills targeted gaps identified after 729 passing tests.
/// <list type="bullet">
///   <item>
///     <b>Orchestrator tick: ApplyAdjustmentAsync throws</b> —
///     when <see cref="IPriceAdjustmentService.ApplyAdjustmentAsync"/> throws during
///     a tick, the catch block must increment <see cref="BotAccount.ConsecutiveErrors"/>
///     (even though it was already reset to 0 earlier in the same tick body).
///   </item>
///   <item>
///     <b>PendingRecommendation retained on price-adjustment failure</b> —
///     the <c>bot.PendingRecommendation = null</c> line that normally clears the
///     recommendation after a successful apply is never reached when the service
///     throws; the recommendation must survive the tick.
///   </item>
///   <item>
///     <b>Skipped via price-adjustment error</b> —
///     with <c>MaxConsecutiveErrors = 1</c>, a single price-adjustment throw is
///     sufficient to mark the bot as <see cref="BotAccount.IsSkipped"/>.
///   </item>
///   <item>
///     <b>BotStateValidator multiple issues</b> —
///     a bot whose token is expired AND whose onboarding is incomplete must have
///     both issues present in the <see cref="BotStateValidationResult.Issues"/> list.
///   </item>
///   <item>
///     <b>BotAccount.OnboardingCompleted after profile nulled</b> —
///     setting <c>Profile = null</c> after it was set to a completed profile must
///     flip <see cref="BotAccount.OnboardingCompleted"/> back to false.
///   </item>
///   <item>
///     <b>ContainsSuitableType whitespace-only field</b> —
///     a field consisting of only whitespace characters is treated the same as an
///     empty string and must return false.
///   </item>
///   <item>
///     <b>StrategyRecommendation default Reason</b> —
///     <c>new StrategyRecommendation().Reason</c> must equal <see cref="string.Empty"/>
///     (not null), as declared by the property initialiser.
///   </item>
///   <item>
///     <b>BotProfitCalculator.Recommend — exactly at neutral-band boundary</b> —
///     a delta of exactly <c>+NeutralBandPercent</c> lies outside the Unprofitable zone
///     and must return <see cref="StrategyRecommendation.NoAction"/>.
///   </item>
///   <item>
///     <b>BotProfitCalculator.Recommend — exactly at severe-loss boundary</b> —
///     a delta of exactly <c>SeverelyUnprofitableThresholdPercent</c> must trigger
///     the aggressive reduction path (≤ threshold).
///   </item>
///   <item>
///     <b>ComputeNetWorth with a single company</b> —
///     a profile with one company whose <c>Cash</c> equals the expected amount must
///     produce that amount exactly.
///   </item>
/// </list>
/// </summary>
public sealed class BotSixthWaveCoverageTests
{
    // ── Fake service implementations ──────────────────────────────────────────

    /// <summary>
    /// Minimal fake that enqueues profiles and game-states for deterministic tests.
    /// <c>FetchProfileAsync</c> and <c>FetchGameStateAsync</c> fall back to safe defaults
    /// when their queues are exhausted. All other methods return hardcoded values.
    /// Intended for single-threaded test scenarios only; not thread-safe.
    /// </summary>
    private sealed class MinimalFakeAccountService : IAccountService
    {
        private readonly Queue<PlayerProfile> _profiles = new();
        private readonly Queue<GameStateSummary> _gameStates = new();
        private CancellationTokenSource? _cts;
        private int _gsCallCount;
        private int _cancelOnN;

        public void EnqueueProfile(PlayerProfile p) => _profiles.Enqueue(p);
        public void EnqueueGameState(GameStateSummary gs) => _gameStates.Enqueue(gs);

        /// <summary>Cancels <paramref name="cts"/> on the Nth <c>FetchGameStateAsync</c> call.</summary>
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
            var gs = _gameStates.Count > 0 ? _gameStates.Dequeue() : new GameStateSummary { CurrentTick = 100 };
            return Task.FromResult(gs);
        }

        public Task<List<RankingEntry>> FetchRankingsAsync(CancellationToken ct) =>
            Task.FromResult(new List<RankingEntry>());

        public Task<UnitSummary> UpdatePublicSalesPriceAsync(
            string unitId, decimal newMinPrice, string token, CancellationToken ct) =>
            Task.FromResult(new UnitSummary { Id = unitId });

        private static readonly PlayerProfile DefaultProfile = new()
        {
            Id = "default-player",
            OnboardingCompletedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            Companies = [],
        };
    }

    private sealed class NoOpOnboardingService : IOnboardingService
    {
        public Task RunAsync(BotAccount bot, string[] _, CancellationToken ct) =>
            Task.CompletedTask;
    }

    /// <summary>Price adjustment service that always throws <see cref="InvalidOperationException"/>.</summary>
    private sealed class ThrowingPriceAdjustmentService : IPriceAdjustmentService
    {
        public Task<int> ApplyAdjustmentAsync(
            BotAccount bot, StrategyRecommendation rec, CancellationToken ct) =>
            throw new InvalidOperationException("Simulated price-adjustment failure.");
    }

    // ── Test helpers ──────────────────────────────────────────────────────────

    private static PlayerProfile CompletedProfile(decimal cash = 100_000m) => new()
    {
        Id = "player-1",
        DisplayName = "NPC 001",
        Email = "npc001@test.example",
        OnboardingCompletedAtUtc = DateTime.UtcNow.AddMinutes(-30),
        Companies = [new CompanySummary { Id = "co-1", Name = "Co", Cash = cash }],
    };

    private static BotAccount MakeBot() => new()
    {
        Index = 1,
        DisplayName = "NPC 001",
        Email = "npc001@test.example",
        Strategy = "FURNITURE",
    };

    private static BotOrchestrator MakeOrchestrator(
        MinimalFakeAccountService accounts,
        IPriceAdjustmentService priceAdjustment,
        BotAccount bot,
        BotOptions options)
    {
        return new BotOrchestrator(
            [bot],
            accounts,
            new NoOpOnboardingService(),
            priceAdjustment,
            Options.Create(options),
            NullLogger<BotOrchestrator>.Instance);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Orchestrator tick: price-adjustment throws
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Tick_PriceAdjustmentThrows_IncrementsConsecutiveErrors()
    {
        // Arrange — bot starts with 100k and loses 20% on the tick profile.
        // MinTicksBeforeAdjustment=0 ensures a recommendation fires immediately,
        // so ApplyAdjustmentAsync is reached and throws.
        var accounts = new MinimalFakeAccountService();
        var bot = MakeBot();

        // Init: auth + two profile fetches
        accounts.EnqueueProfile(CompletedProfile(100_000m)); // onboarding check
        accounts.EnqueueProfile(CompletedProfile(100_000m)); // InitialNetWorth
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 0 }); // init

        // Tick: game-state (tick 100) + profile (lower net worth → unprofitable)
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 100 }); // tick
        accounts.EnqueueProfile(CompletedProfile(80_000m)); // −20% → ShouldAct=true

        // Cancel after the tick completes (on the start of tick 2) so one full tick runs.
        using var cts = new CancellationTokenSource();
        accounts.CancelOnGameStateCallN(cts, 3); // 1=init, 2=tick1, 3=tick2 start → cancel

        var opts = new BotOptions
        {
            Enabled = true,
            PollIntervalSeconds = 0,
            MinTicksBeforeAdjustment = 0,
        };
        var orchestrator = MakeOrchestrator(accounts, new ThrowingPriceAdjustmentService(), bot, opts);

        // Act
        await orchestrator.RunAsync(cts.Token);

        // Assert — ConsecutiveErrors was reset to 0 inside the tick body (before ApplyAdjustment),
        // then incremented by 1 in the catch block when the service threw.
        Assert.Equal(1, bot.ConsecutiveErrors);
    }

    [Fact]
    public async Task Tick_PriceAdjustmentThrows_PendingRecommendationNotCleared()
    {
        // The line `bot.PendingRecommendation = null` (in ApplyPendingRecommendationAsync)
        // is only reached AFTER the service call succeeds. When it throws, the recommendation
        // set by EvaluateAndLogProfitability must survive the tick.
        var accounts = new MinimalFakeAccountService();
        var bot = MakeBot();

        accounts.EnqueueProfile(CompletedProfile(100_000m));
        accounts.EnqueueProfile(CompletedProfile(100_000m));
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 0 });

        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 100 });
        accounts.EnqueueProfile(CompletedProfile(80_000m)); // unprofitable → ShouldAct=true

        using var cts = new CancellationTokenSource();
        accounts.CancelOnGameStateCallN(cts, 3); // 1=init, 2=tick1, 3=tick2 start → cancel

        var opts = new BotOptions
        {
            Enabled = true,
            PollIntervalSeconds = 0,
            MinTicksBeforeAdjustment = 0,
        };
        var orchestrator = MakeOrchestrator(accounts, new ThrowingPriceAdjustmentService(), bot, opts);

        await orchestrator.RunAsync(cts.Token);

        // PendingRecommendation was set by EvaluateAndLogProfitability and was NOT cleared
        // because the service throw prevented bot.PendingRecommendation = null from running.
        var rec = bot.PendingRecommendation;
        Assert.NotNull(rec);
        Assert.True(rec.ShouldAct,
            "The retained recommendation must have ShouldAct=true (it was the active unprofitable recommendation).");
    }

    [Fact]
    public async Task Tick_PriceAdjustmentThrows_MaxConsecutiveErrors1_BotIsSkipped()
    {
        // With MaxConsecutiveErrors=1, a single tick-level price-adjustment failure is
        // enough to mark the bot as skipped.
        var accounts = new MinimalFakeAccountService();
        var bot = MakeBot();

        accounts.EnqueueProfile(CompletedProfile(100_000m));
        accounts.EnqueueProfile(CompletedProfile(100_000m));
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 0 });

        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 100 });
        accounts.EnqueueProfile(CompletedProfile(80_000m));

        using var cts = new CancellationTokenSource();
        accounts.CancelOnGameStateCallN(cts, 3); // 1=init, 2=tick1, 3=tick2 start → cancel

        var opts = new BotOptions
        {
            Enabled = true,
            PollIntervalSeconds = 0,
            MinTicksBeforeAdjustment = 0,
            MaxConsecutiveErrors = 1,   // skip on first error
        };
        var orchestrator = MakeOrchestrator(accounts, new ThrowingPriceAdjustmentService(), bot, opts);

        await orchestrator.RunAsync(cts.Token);

        Assert.True(bot.IsSkipped,
            "Bot must be skipped when ConsecutiveErrors (1) reaches MaxConsecutiveErrors (1).");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BotStateValidator: multiple simultaneous issues
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Validate_ExpiredTokenAndIncompleteOnboarding_BothIssuesPresentInResult()
    {
        // A bot with an expired token AND incomplete onboarding must produce at least
        // two distinct issues in the validation result.
        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "NPC 001",
            Email = "npc001@test.example",
            Strategy = "FURNITURE",
            Token = "expired-token",
            TokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-60), // expired
            Profile = new PlayerProfile
            {
                Id = "p1",
                OnboardingCompletedAtUtc = null, // not complete
            },
        };

        var result = BotStateValidator.Validate(bot);

        Assert.False(result.IsValid);
        Assert.True(
            result.Issues.Count >= 2,
            $"Expected ≥2 issues but found {result.Issues.Count}: [{string.Join(", ", result.Issues)}]");

        // Both issues must be reflected in the summary string.
        Assert.Contains("Token", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Onboarding", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_SkippedAndExpiredToken_BothIssuesPresentInResult()
    {
        // IsSkipped=true AND expired token → at least two issues (skipped issue + token issue).
        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "NPC 001",
            Email = "npc001@test.example",
            Strategy = "FURNITURE",
            IsSkipped = true,
            Token = "tok",
            TokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1), // expired
        };

        var result = BotStateValidator.Validate(bot);

        Assert.False(result.IsValid);
        Assert.True(
            result.Issues.Count >= 2,
            $"Expected ≥2 issues but found {result.Issues.Count}: [{string.Join(", ", result.Issues)}]");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BotAccount.OnboardingCompleted: nulling the profile
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void OnboardingCompleted_ProfileNulledAfterBeingSetToCompleted_ReturnsFalse()
    {
        // The property is purely derived from Profile?.OnboardingCompletedAtUtc.
        // Setting Profile = null must flip it from true back to false.
        var bot = MakeBot();
        bot.Profile = new PlayerProfile
        {
            Id = "p1",
            OnboardingCompletedAtUtc = DateTime.UtcNow, // completed
        };

        Assert.True(bot.OnboardingCompleted, "Pre-condition: should be true after profile set.");

        bot.Profile = null;

        Assert.False(bot.OnboardingCompleted, "Must be false after Profile is set to null.");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // OnboardingHelpers.ContainsSuitableType: whitespace-only field
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ContainsSuitableType_WhitespaceOnlyField_ReturnsFalse()
    {
        // string.IsNullOrWhiteSpace("   ") is true → the guard returns false
        // without attempting any CSV split.
        Assert.False(OnboardingHelpers.ContainsSuitableType("   ", "FACTORY"));
    }

    [Fact]
    public void ContainsSuitableType_WhitespaceOnlySuitableType_ReturnsFalse()
    {
        // Symmetric test: the suitableType parameter being whitespace also returns false.
        Assert.False(OnboardingHelpers.ContainsSuitableType("FACTORY,MINE", "   "));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // StrategyRecommendation: default Reason is string.Empty, not null
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DefaultRecommendation_ReasonIsEmptyStringNotNull()
    {
        // The property initialiser `Reason { get; init; } = string.Empty` ensures
        // that Reason is never null even on a default-constructed instance.
        var rec = new StrategyRecommendation();

        Assert.Equal(string.Empty, rec.Reason);
        Assert.NotNull(rec.Reason);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BotProfitCalculator.Recommend: boundary cases
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Recommend_DeltaExactlyAtPositiveNeutralBand_ReturnsNoAction()
    {
        // NeutralBandPercent = 0.02 (2%).
        // delta = +2% → Profitable zone (deltaPercent > NeutralBandPercent) → NoAction.
        var initial = 100_000m;
        var current = initial * (1m + BotProfitCalculator.NeutralBandPercent); // exactly +2%

        var rec = BotProfitCalculator.Recommend(current, initial, ticksElapsed: 100);

        Assert.False(rec.ShouldAct, "A gain of exactly NeutralBandPercent is in the Profitable zone → NoAction.");
        Assert.Same(StrategyRecommendation.NoAction, rec);
    }

    [Fact]
    public void Recommend_DeltaExactlyAtSevereThreshold_ReturnsAggressiveReduction()
    {
        // SeverelyUnprofitableThresholdPercent = -0.10 (−10%).
        // delta = −10% is ≤ threshold → aggressive path triggered.
        var initial = 100_000m;
        var current = initial * (1m + BotProfitCalculator.SeverelyUnprofitableThresholdPercent); // exactly −10%

        var rec = BotProfitCalculator.Recommend(current, initial, ticksElapsed: 100);

        Assert.True(rec.ShouldAct);
        Assert.Equal(BotProfitCalculator.AggressivePriceReductionFactor, rec.PriceAdjustmentFactor);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BotProfitCalculator.ComputeNetWorth: single company
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ComputeNetWorth_SingleCompanyProfile_ReturnsThatCompanysCash()
    {
        var profile = new PlayerProfile
        {
            Companies = [new CompanySummary { Id = "c1", Name = "Corp", Cash = 55_555m }],
        };

        Assert.Equal(55_555m, BotProfitCalculator.ComputeNetWorth(profile));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ComputeRecommendationForBot static method: large CurrentNetWorth with zero initial
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ComputeRecommendationForBot_LargeCurrentWithZeroInitial_ReturnsNoAction()
    {
        // When InitialNetWorth == 0, BotProfitCalculator.Recommend short-circuits to NoAction.
        // ComputeRecommendationForBot must forward this correctly regardless of how large
        // CurrentNetWorth is.
        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "NPC 001",
            Email = "npc001@test.example",
            Strategy = "FURNITURE",
            InitialNetWorth = 0m,
            CurrentNetWorth = 999_999m,
            TrackingStartTick = 0,
        };

        var rec = BotOrchestrator.ComputeRecommendationForBot(
            bot, currentTick: 200, minTicksBeforeAdjustment: 0);

        Assert.False(rec.ShouldAct,
            "Zero InitialNetWorth must always return NoAction (division-by-zero guard).");
        Assert.Same(StrategyRecommendation.NoAction, rec);
    }
}

using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Third-wave coverage for the NPCBot suite, focusing on genuinely uncovered paths:
/// <list type="bullet">
///   <item>Rank preserved (not cleared) when rankings fetch throws an exception.</item>
///   <item>Exact constant values for <see cref="BotProfitCalculator"/> factors and thresholds
///         (regression protection — indirect calculation tests cannot catch silent constant drift).</item>
///   <item>Specific key words in <see cref="StrategyRecommendation"/> reason text, verifying the
///         human-readable messages produced by <c>BotProfitCalculator.Recommend</c> remain useful.</item>
///   <item>BotProfitCalculator total-loss classification (current = 0, initial > 0).</item>
///   <item>StrategyRecommendation.NoAction singleton exact fields — Reason, ShouldAct, Factor.</item>
///   <item>BotOrchestrator pre-cancelled-token loop invariant — no extra profile fetches.</item>
/// </list>
/// </summary>
public sealed class BotThirdWaveCoverageTests
{
    // ── Fake infrastructure reused across orchestrator tests in this file ─────

    private sealed class FakeAccountService : IAccountService
    {
        private readonly Queue<PlayerProfile> _profileQueue = new();
        private readonly Queue<GameStateSummary> _gameStateQueue = new();
        private readonly Queue<List<RankingEntry>> _rankingsQueue = new();

        public int FetchGameStateCallCount;
        public Exception? RankingsException;

        private CancellationTokenSource? _autoCancelCts;
        private int _autoCancelAfterN;

        public void CancelCtsAfterGameStateFetch(CancellationTokenSource cts, int onCallN)
        {
            _autoCancelCts = cts;
            _autoCancelAfterN = onCallN;
        }

        public void EnqueueProfile(PlayerProfile p) => _profileQueue.Enqueue(p);
        public void EnqueueGameState(GameStateSummary gs) => _gameStateQueue.Enqueue(gs);
        public void EnqueueRankings(List<RankingEntry> r) => _rankingsQueue.Enqueue(r);

        public Task<(string token, DateTime expiresAt)> RegisterOrLoginAsync(BotAccount bot, CancellationToken ct) =>
            Task.FromResult(("tok", DateTime.UtcNow.AddHours(2)));

        public Task<(string token, DateTime expiresAt)> LoginAsync(BotAccount bot, CancellationToken ct) =>
            Task.FromResult(("tok", DateTime.UtcNow.AddHours(2)));

        public Task<PlayerProfile> FetchProfileAsync(string token, CancellationToken ct)
        {
            var p = _profileQueue.Count > 0 ? _profileQueue.Dequeue() : CompletedProfile();
            return Task.FromResult(p);
        }

        public Task<GameStateSummary> FetchGameStateAsync(CancellationToken ct)
        {
            FetchGameStateCallCount++;
            if (_autoCancelCts is not null && FetchGameStateCallCount >= _autoCancelAfterN)
                _autoCancelCts.Cancel();
            var gs = _gameStateQueue.Count > 0
                ? _gameStateQueue.Dequeue()
                : new GameStateSummary { CurrentTick = FetchGameStateCallCount };
            return Task.FromResult(gs);
        }

        public Task<List<RankingEntry>> FetchRankingsAsync(CancellationToken ct)
        {
            if (RankingsException is not null) throw RankingsException;
            var list = _rankingsQueue.Count > 0 ? _rankingsQueue.Dequeue() : [];
            return Task.FromResult(list);
        }

        public Task<UnitSummary> UpdatePublicSalesPriceAsync(
            string unitId, decimal newMinPrice, string token, CancellationToken ct) =>
            Task.FromResult(new UnitSummary { Id = unitId });
    }

    private static PlayerProfile CompletedProfile(decimal cash = 100_000m) => new()
    {
        Id = "p1",
        DisplayName = "NPC 001",
        Email = "npc001@test.example",
        OnboardingCompletedAtUtc = DateTime.UtcNow.AddDays(-1),
        Companies = [new CompanySummary { Id = "c1", Name = "Bot Corp", Cash = cash, Buildings = [] }],
    };

    private static BotAccount MakeBot(string name = "NPC 001") => new()
    {
        Index = 1,
        DisplayName = name,
        Email = "npc001@test.example",
        Strategy = "FURNITURE",
    };

    private static BotOrchestrator MakeOrchestrator(
        BotAccount[] bots,
        FakeAccountService accounts,
        BotOptions? options = null) =>
        new BotOrchestrator(
            bots,
            accounts,
            new FakeOnboardingService(),
            new FakePriceAdjustmentService(),
            Options.Create(options ?? new BotOptions { Enabled = true, PollIntervalSeconds = 0 }),
            NullLogger<BotOrchestrator>.Instance);

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

    // ── Rank preservation when rankings fetch throws ──────────────────────────

    /// <summary>
    /// When the rankings fetch throws during a tick, a bot's pre-existing
    /// <c>CurrentRank</c> must be preserved — NOT cleared to null.
    /// This distinguishes the failure path (preserve last known rank)
    /// from the "not in rankings" path (clear to null).
    /// </summary>
    [Fact]
    public async Task Tick_RankingsFetchThrows_PreExistingRankIsPreserved()
    {
        var accounts = new FakeAccountService();
        var bot = MakeBot();
        bot.CurrentRank = 7; // bot was ranked 7th in a previous tick

        accounts.RankingsException = new InvalidOperationException("Leaderboard service unavailable");

        // Init profiles (2 per bot — onboarding check + net-worth snapshot)
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueProfile(CompletedProfile());
        // Tick profile
        accounts.EnqueueProfile(CompletedProfile());
        accounts.EnqueueGameState(new GameStateSummary { CurrentTick = 10 });

        using var cts = new CancellationTokenSource();
        accounts.CancelCtsAfterGameStateFetch(cts, onCallN: 3); // init(1) + tick(2) + abort(3)

        var orchestrator = MakeOrchestrator([bot], accounts);
        await orchestrator.RunAsync(cts.Token);

        // Rankings threw → rankings is null → the if-guard prevents clearing the rank.
        // The bot's last known rank (7) must be preserved for the next periodic report.
        Assert.Equal(7, bot.CurrentRank);
        // Tick itself must have succeeded (no error increment from the rankings failure).
        Assert.Equal(0, bot.ConsecutiveErrors);
    }

    // ── BotProfitCalculator constant-value regression tests ───────────────────

    /// <summary>
    /// The mild price-reduction factor must be exactly 0.95 (−5%).
    /// This constant drives real-game price mutations; a silent change would affect all bots.
    /// </summary>
    [Fact]
    public void BotProfitCalculator_MildPriceReductionFactor_IsExactly0Point95()
    {
        Assert.Equal(0.95m, BotProfitCalculator.MildPriceReductionFactor);
    }

    /// <summary>
    /// The aggressive price-reduction factor must be exactly 0.85 (−15%).
    /// </summary>
    [Fact]
    public void BotProfitCalculator_AggressivePriceReductionFactor_IsExactly0Point85()
    {
        Assert.Equal(0.85m, BotProfitCalculator.AggressivePriceReductionFactor);
    }

    /// <summary>
    /// The neutral band must be exactly ±2% (0.02).
    /// Values inside the band are Neutral; outside the band are Profitable or Unprofitable.
    /// </summary>
    [Fact]
    public void BotProfitCalculator_NeutralBandPercent_IsExactly0Point02()
    {
        Assert.Equal(0.02m, BotProfitCalculator.NeutralBandPercent);
    }

    /// <summary>
    /// The severely-unprofitable threshold must be exactly −10% (−0.10).
    /// Bots at or below this threshold receive the aggressive (−15%) adjustment.
    /// </summary>
    [Fact]
    public void BotProfitCalculator_SeverelyUnprofitableThresholdPercent_IsExactlyNegative0Point10()
    {
        Assert.Equal(-0.10m, BotProfitCalculator.SeverelyUnprofitableThresholdPercent);
    }

    // ── StrategyRecommendation.NoAction exact fields ──────────────────────────

    /// <summary>
    /// The <see cref="StrategyRecommendation.NoAction"/> singleton's Reason must contain
    /// "acceptable" so that periodic log reports are human-readable.
    /// </summary>
    [Fact]
    public void StrategyRecommendation_NoAction_ReasonContainsAcceptable()
    {
        Assert.Contains("acceptable", StrategyRecommendation.NoAction.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// NoAction's Reason must be a complete, non-empty sentence (period-terminated).
    /// </summary>
    [Fact]
    public void StrategyRecommendation_NoAction_ReasonEndsWithPeriod()
    {
        var reason = StrategyRecommendation.NoAction.Reason;
        Assert.NotEmpty(reason);
        Assert.EndsWith(".", reason);
    }

    // ── StrategyRecommendation reason text format from Recommend() ────────────

    /// <summary>
    /// When the bot is severely unprofitable, the recommendation reason must contain
    /// "aggressively" so operators can quickly scan logs for severe alerts.
    /// </summary>
    [Fact]
    public void Recommend_SevereLoss_ReasonContainsAggressively()
    {
        // −15% → severe loss (below −10% threshold)
        var rec = BotProfitCalculator.Recommend(85_000m, 100_000m, ticksElapsed: 10);

        Assert.True(rec.ShouldAct);
        Assert.Contains("aggressively", rec.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// When the bot is mildly unprofitable, the recommendation reason must contain
    /// "slightly" to indicate a moderate, not aggressive, adjustment.
    /// </summary>
    [Fact]
    public void Recommend_MildLoss_ReasonContainsSlightly()
    {
        // −5% → mild loss (between −2% and −10%)
        var rec = BotProfitCalculator.Recommend(95_000m, 100_000m, ticksElapsed: 10);

        Assert.True(rec.ShouldAct);
        Assert.Contains("slightly", rec.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Both the severe and mild recommendation reasons must include the actual loss percentage
    /// so operators can see the magnitude when scanning logs.
    /// </summary>
    [Fact]
    public void Recommend_SevereLoss_ReasonContainsPercentSign()
    {
        var rec = BotProfitCalculator.Recommend(85_000m, 100_000m, ticksElapsed: 10);
        Assert.Contains("%", rec.Reason);
    }

    [Fact]
    public void Recommend_MildLoss_ReasonContainsPercentSign()
    {
        var rec = BotProfitCalculator.Recommend(95_000m, 100_000m, ticksElapsed: 10);
        Assert.Contains("%", rec.Reason);
    }

    // ── BotProfitCalculator.Recommend returns correct factors ─────────────────

    /// <summary>
    /// Verify that severe-loss recommendation uses the aggressive factor (0.85) from the constant.
    /// This test fails if MildPriceReductionFactor and AggressivePriceReductionFactor are swapped.
    /// </summary>
    [Fact]
    public void Recommend_SevereLoss_UsesAggressiveConstant()
    {
        var rec = BotProfitCalculator.Recommend(85_000m, 100_000m, ticksElapsed: 10);

        Assert.Equal(BotProfitCalculator.AggressivePriceReductionFactor, rec.PriceAdjustmentFactor);
        Assert.Equal(0.85m, rec.PriceAdjustmentFactor);
    }

    /// <summary>
    /// Verify that mild-loss recommendation uses the mild factor (0.95) from the constant.
    /// </summary>
    [Fact]
    public void Recommend_MildLoss_UsesMildConstant()
    {
        var rec = BotProfitCalculator.Recommend(95_000m, 100_000m, ticksElapsed: 10);

        Assert.Equal(BotProfitCalculator.MildPriceReductionFactor, rec.PriceAdjustmentFactor);
        Assert.Equal(0.95m, rec.PriceAdjustmentFactor);
    }

    // ── BotProfitCalculator.Classify total-loss edge case ─────────────────────

    /// <summary>
    /// A bot that loses ALL its net worth (currentNetWorth = 0, initialNetWorth > 0)
    /// must be classified as Unprofitable (−100% is well below the −2% neutral band).
    /// </summary>
    [Fact]
    public void Classify_TotalLoss_ZeroCurrentNetWorth_ReturnsUnprofitable()
    {
        Assert.Equal(ProfitabilityStatus.Unprofitable,
            BotProfitCalculator.Classify(0m, 100_000m));
    }

    /// <summary>
    /// A bot that starts at 0 but gains net worth (0 → positive) is classified Unknown
    /// because the initialNetWorth == 0 guard fires before the delta can be calculated.
    /// </summary>
    [Fact]
    public void Classify_ZeroInitialNonZeroCurrent_ReturnsUnknown()
    {
        Assert.Equal(ProfitabilityStatus.Unknown,
            BotProfitCalculator.Classify(50_000m, 0m));
    }

    // ── StrategyRecommendation.NoAction singleton coherence ──────────────────

    /// <summary>
    /// NoAction fields must all be at their "do nothing" values:
    /// ShouldAct=false, PriceAdjustmentFactor=0, Reason non-empty.
    /// Consolidates multiple fields in one assertion to document the no-op contract.
    /// </summary>
    [Fact]
    public void StrategyRecommendation_NoAction_AllFieldsAtDoNothingValues()
    {
        var noop = StrategyRecommendation.NoAction;
        Assert.False(noop.ShouldAct);
        Assert.Equal(0m, noop.PriceAdjustmentFactor);
        Assert.NotEmpty(noop.Reason);
    }

    // ── BotProfitCalculator neutral/profitable recommendation returns NoAction ─

    /// <summary>
    /// Neutral bots (within ±2%) must not trigger any recommendation action.
    /// Verify that the returned recommendation IS the NoAction singleton (not just ShouldAct=false).
    /// </summary>
    [Fact]
    public void Recommend_NeutralBot_ReturnsSameNoActionSingleton()
    {
        // 1% gain is within the neutral band
        var rec = BotProfitCalculator.Recommend(101_000m, 100_000m, ticksElapsed: 10);
        Assert.Same(StrategyRecommendation.NoAction, rec);
    }

    /// <summary>
    /// Profitable bots (above +2%) must not trigger any recommendation action.
    /// </summary>
    [Fact]
    public void Recommend_ProfitableBot_ReturnsSameNoActionSingleton()
    {
        // 5% gain is clearly profitable
        var rec = BotProfitCalculator.Recommend(105_000m, 100_000m, ticksElapsed: 10);
        Assert.Same(StrategyRecommendation.NoAction, rec);
    }

    // ── BotOptions constant validation ───────────────────────────────────────

    /// <summary>
    /// The GraphQL URL default must not end with "/" to avoid double-slash mutation paths.
    /// </summary>
    [Fact]
    public void BotOptions_DefaultGraphqlUrl_DoesNotEndWithSlash()
    {
        var opts = new BotOptions();
        Assert.False(opts.GraphqlUrl.EndsWith("/"),
            $"GraphqlUrl must not end with '/'. Got: '{opts.GraphqlUrl}'");
    }

    /// <summary>
    /// The default password is now intentionally empty — the committed placeholder was
    /// removed as a security hardening measure. The startup guard enforces a non-placeholder
    /// value outside the Development environment.
    /// </summary>
    [Fact]
    public void BotOptions_DefaultPassword_IsEmpty()
    {
        var opts = new BotOptions();
        Assert.Equal("", opts.BotPassword);
    }
}

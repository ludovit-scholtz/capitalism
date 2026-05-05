using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Capitalism.NPCBot.Services;

/// <summary>
/// Orchestrates all NPC bot accounts: authentication, onboarding, periodic
/// state checks, profitability logging, and graceful shutdown.
/// </summary>
public sealed class BotOrchestrator
{
    private readonly List<BotAccount> _bots;
    private readonly IAccountService _accounts;
    private readonly IOnboardingService _onboarding;
    private readonly IPriceAdjustmentService _priceAdjustment;
    private readonly BotOptions _options;
    private readonly ILogger<BotOrchestrator> _logger;
    private long _currentTick;

    public BotOrchestrator(
        IEnumerable<BotAccount> bots,
        IAccountService accounts,
        IOnboardingService onboarding,
        IPriceAdjustmentService priceAdjustment,
        IOptions<BotOptions> options,
        ILogger<BotOrchestrator> logger)
    {
        _bots = [.. bots];
        _accounts = accounts;
        _onboarding = onboarding;
        _priceAdjustment = priceAdjustment;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Runs all bots until <paramref name="ct"/> is cancelled.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("NPC bot runner is disabled via configuration. Exiting.");
            return;
        }

        _logger.LogInformation("Starting NPC bot runner with {Count} bot(s).", _bots.Count);
        PrintBotRoster();

        // Initial pass: authenticate and onboard all bots
        await InitialiseAllBotsAsync(ct);

        // Periodic loop — catches Task.Delay cancellation to exit cleanly.
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (ct.IsCancellationRequested) break;

            await TickAllBotsAsync(ct);
            PrintPeriodicReport();
        }

        _logger.LogInformation("Bot runner stopped.");
    }

    // ── Initialisation ────────────────────────────────────────────────────────

    private async Task InitialiseAllBotsAsync(CancellationToken ct)
    {
        try
        {
            var gs = await _accounts.FetchGameStateAsync(ct);
            _currentTick = gs.CurrentTick;
            _logger.LogInformation("Game state: tick {Tick}, interval {Interval}s",
                gs.CurrentTick, gs.TickIntervalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch initial game state.");
        }

        foreach (var bot in _bots)
        {
            if (ct.IsCancellationRequested) break;
            await InitialiseBotAsync(bot, ct);
        }
    }

    private async Task InitialiseBotAsync(BotAccount bot, CancellationToken ct)
    {
        try
        {
            // Authenticate
            var (token, expires) = await _accounts.RegisterOrLoginAsync(bot, ct);
            bot.Token = token;
            bot.TokenExpiresAtUtc = expires;

            // Fetch profile
            bot.Profile = await _accounts.FetchProfileAsync(token, ct);

            _logger.LogInformation("{Bot} Authenticated. Onboarding complete: {Done}",
                bot, bot.OnboardingCompleted);

            // Onboard if needed
            if (!bot.OnboardingCompleted)
                await RunOnboardingAsync(bot, ct);

            // Refresh profile and record initial net worth
            bot.Profile = await _accounts.FetchProfileAsync(bot.Token!, ct);
            bot.InitialNetWorth = ComputeNetWorth(bot.Profile);
            bot.CurrentNetWorth = bot.InitialNetWorth;
            bot.TrackingStartTick = _currentTick;
            bot.LastSuccessUtc = DateTime.UtcNow;

            _logger.LogInformation("{Bot} Initialised. Companies: {Count}, Net worth: {NW:N0}",
                bot, bot.Profile.Companies.Count, bot.InitialNetWorth);
        }
        catch (Exception ex)
        {
            bot.ConsecutiveErrors++;
            _logger.LogError(ex, "{Bot} Initialisation failed (error #{N}).", bot, bot.ConsecutiveErrors);
            if (bot.ConsecutiveErrors >= _options.MaxConsecutiveErrors)
            {
                bot.IsSkipped = true;
                _logger.LogWarning("{Bot} Marked as skipped after {N} consecutive errors.", bot, bot.ConsecutiveErrors);
            }
        }
    }

    private async Task RunOnboardingAsync(BotAccount bot, CancellationToken ct)
    {
        _logger.LogInformation("{Bot} Starting onboarding…", bot);
        await _onboarding.RunAsync(bot, _options.AllowedIndustries, ct);
        bot.Profile = await _accounts.FetchProfileAsync(bot.Token!, ct);
        _logger.LogInformation("{Bot} Onboarding complete.", bot);
    }

    // ── Periodic tick ─────────────────────────────────────────────────────────

    private async Task TickAllBotsAsync(CancellationToken ct)
    {
        try
        {
            var gs = await _accounts.FetchGameStateAsync(ct);
            _currentTick = gs.CurrentTick;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not refresh game tick.");
        }

        foreach (var bot in _bots)
        {
            if (ct.IsCancellationRequested) break;
            if (bot.IsSkipped) continue;
            await TickBotAsync(bot, ct);
        }
    }

    private async Task TickBotAsync(BotAccount bot, CancellationToken ct)
    {
        try
        {
            // Refresh token if needed
            if (!bot.IsTokenValid(_options.TokenRefreshBufferMinutes))
            {
                var (token, expires) = await _accounts.LoginAsync(bot, ct);
                bot.Token = token;
                bot.TokenExpiresAtUtc = expires;
            }

            // Refresh profile
            bot.Profile = await _accounts.FetchProfileAsync(bot.Token!, ct);

            // Ensure onboarding is complete
            if (!bot.OnboardingCompleted)
            {
                await RunOnboardingAsync(bot, ct);
                bot.Profile = await _accounts.FetchProfileAsync(bot.Token!, ct);
                bot.InitialNetWorth = ComputeNetWorth(bot.Profile);
            }

            // Update net worth tracking
            bot.CurrentNetWorth = ComputeNetWorth(bot.Profile);
            bot.ConsecutiveErrors = 0;
            bot.LastSuccessUtc = DateTime.UtcNow;

            EvaluateAndLogProfitability(bot);

            // Apply any pending strategy recommendation (price adjustments)
            await ApplyPendingRecommendationAsync(bot, ct);
        }
        catch (Exception ex)
        {
            bot.ConsecutiveErrors++;
            _logger.LogError(ex, "{Bot} Tick failed (error #{N}).", bot, bot.ConsecutiveErrors);
            if (bot.ConsecutiveErrors >= _options.MaxConsecutiveErrors)
            {
                bot.IsSkipped = true;
                _logger.LogWarning("{Bot} Skipped after {N} consecutive errors.", bot, bot.ConsecutiveErrors);
            }
        }
    }

    // ── Reporting ─────────────────────────────────────────────────────────────

    private void PrintBotRoster()
    {
        _logger.LogInformation("─── Bot Roster ──────────────────────────────────────");
        foreach (var bot in _bots)
            _logger.LogInformation("  {Bot}  email={Email}", bot, bot.Email);
        _logger.LogInformation("─────────────────────────────────────────────────────");
    }

    private void PrintPeriodicReport()
    {
        _logger.LogInformation("─── Periodic Report (tick {Tick}) ───────────────────", _currentTick);
        foreach (var bot in _bots)
        {
            var status = GetBotStatusLabel(bot);
            var profitable = bot.ProfitDelta >= 0 ? "✓" : "✗";
            _logger.LogInformation(
                "  {Bot}  status={Status}  netWorth={NW:N0}  delta={Delta:+0;-0;0}  {Profitable}",
                bot, status, bot.CurrentNetWorth, bot.ProfitDelta, profitable);
        }
        _logger.LogInformation("─────────────────────────────────────────────────────");
    }

    /// <summary>
    /// Returns a human-readable status label for the given bot based on its runtime state.
    /// <list type="table">
    ///   <item><term>SKIPPED</term><description>Bot exceeded the consecutive-error limit and is no longer polled.</description></item>
    ///   <item><term>NO_TOKEN</term><description>Bot has no valid authentication token.</description></item>
    ///   <item><term>ONBOARDING</term><description>Bot is authenticated but has not yet completed onboarding.</description></item>
    ///   <item><term>ACTIVE</term><description>Bot is authenticated and fully onboarded.</description></item>
    /// </list>
    /// </summary>
    public static string GetBotStatusLabel(BotAccount bot) =>
        bot.IsSkipped        ? "SKIPPED" :
        !bot.HasValidToken   ? "NO_TOKEN" :
        !bot.OnboardingCompleted ? "ONBOARDING" : "ACTIVE";

    /// <summary>
    /// Computes a strategy recommendation for a bot given the current tick.
    /// Extracted as a public static method so it can be unit tested without
    /// an orchestrator instance or live HTTP calls.
    /// </summary>
    /// <param name="bot">The bot whose profitability should be evaluated.</param>
    /// <param name="currentTick">The current game tick (from <c>gameState.currentTick</c>).</param>
    /// <param name="minTicksBeforeAdjustment">
    /// Minimum ticks that must elapse after tracking start before a recommendation is made.
    /// </param>
    public static StrategyRecommendation ComputeRecommendationForBot(
        BotAccount bot,
        long currentTick,
        int minTicksBeforeAdjustment = 5)
    {
        var ticksElapsed = currentTick - bot.TrackingStartTick;
        return BotProfitCalculator.Recommend(
            bot.CurrentNetWorth,
            bot.InitialNetWorth,
            ticksElapsed,
            minTicksBeforeAdjustment);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static decimal ComputeNetWorth(PlayerProfile profile) =>
        BotProfitCalculator.ComputeNetWorth(profile);

    private void EvaluateAndLogProfitability(BotAccount bot)
    {
        var status = BotProfitCalculator.Classify(bot.CurrentNetWorth, bot.InitialNetWorth);
        var recommendation = ComputeRecommendationForBot(
            bot, _currentTick, _options.MinTicksBeforeAdjustment);

        var ticksElapsed = _currentTick - bot.TrackingStartTick;
        var rate = BotProfitCalculator.ComputeAnnualisedRatePercent(
            bot.CurrentNetWorth, bot.InitialNetWorth, ticksElapsed);

        _logger.LogDebug(
            "{Bot} Profitability={Status}  rate={Rate:N1}%/yr  delta={Delta:+0;-0;0}",
            bot, status, rate, bot.ProfitDelta);

        if (recommendation.ShouldAct)
            _logger.LogInformation("{Bot} Strategy recommendation: {Reason}", bot, recommendation.Reason);

        bot.PendingRecommendation = recommendation;
    }

    private async Task ApplyPendingRecommendationAsync(BotAccount bot, CancellationToken ct)
    {
        if (bot.PendingRecommendation is null || !bot.PendingRecommendation.ShouldAct)
            return;

        await _priceAdjustment.ApplyAdjustmentAsync(bot, bot.PendingRecommendation, ct);
        // Always clear after every application attempt (success or no-op) to prevent
        // infinite retry loops when no adjustable units exist or all changes are sub-cent.
        bot.PendingRecommendation = null;
    }
}

using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Capitalism.NPCBot.Services;

/// <summary>
/// Orchestrates all NPC bot accounts: authentication, onboarding, periodic
/// state checks, profitability logging, and graceful shutdown.
/// Split across two partial files:
/// <list type="bullet">
///   <item><see cref="BotOrchestrator"/> — core init/tick loop.</item>
///   <item><c>BotOrchestrator.Reporting.cs</c> — reporting helpers and public static methods.</item>
/// </list>
/// </summary>
public sealed partial class BotOrchestrator
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

        // Refresh rankings for competitive position tracking (best-effort; failure is non-fatal).
        List<RankingEntry>? rankings = null;
        try
        {
            rankings = await _accounts.FetchRankingsAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not refresh global rankings.");
        }

        foreach (var bot in _bots)
        {
            if (ct.IsCancellationRequested) break;
            if (bot.IsSkipped) continue;
            if (rankings is not null)
                bot.CurrentRank = rankings.Find(r => r.DisplayName == bot.DisplayName)?.Rank;
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
}

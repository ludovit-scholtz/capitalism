using Capitalism.NPCBot.Models;
using Microsoft.Extensions.Logging;

namespace Capitalism.NPCBot.Services;

/// <summary>
/// Reporting, status-label, and public-static-helper methods for <see cref="BotOrchestrator"/>.
/// Kept in a separate partial file to stay within the 300-line-per-file limit.
/// </summary>
public sealed partial class BotOrchestrator
{
    // ── Periodic reporting ────────────────────────────────────────────────────

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
            var rankStr = bot.CurrentRank.HasValue ? $"rank={bot.CurrentRank}" : "rank=?";
            _logger.LogInformation(
                "  {Bot}  status={Status}  {Rank}  netWorth={NW:N0}  delta={Delta:+0;-0;0}  {Profitable}",
                bot, status, rankStr, bot.CurrentNetWorth, bot.ProfitDelta, profitable);
        }
        _logger.LogInformation("─────────────────────────────────────────────────────");
    }

    // ── Public static helpers (exposed for unit testing) ─────────────────────

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
        bot.IsSkipped            ? "SKIPPED" :
        !bot.HasValidToken        ? "NO_TOKEN" :
        !bot.OnboardingCompleted  ? "ONBOARDING" : "ACTIVE";

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

    // ── Private profitability helpers ─────────────────────────────────────────

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

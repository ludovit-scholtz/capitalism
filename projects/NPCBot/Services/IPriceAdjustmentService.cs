using Capitalism.NPCBot.Models;

namespace Capitalism.NPCBot.Services;

/// <summary>
/// Abstraction over the price adjustment mutation.
/// Extracted so <see cref="BotOrchestrator"/> can be tested with a fake implementation.
/// </summary>
public interface IPriceAdjustmentService
{
    /// <summary>
    /// Applies the price-adjustment factor from <paramref name="recommendation"/> to all
    /// adjustable PUBLIC_SALES units owned by the bot.
    /// </summary>
    Task<int> ApplyAdjustmentAsync(BotAccount bot, StrategyRecommendation recommendation, CancellationToken ct);
}

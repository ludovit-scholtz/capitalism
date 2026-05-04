using Capitalism.NPCBot.Models;
using Microsoft.Extensions.Logging;

namespace Capitalism.NPCBot.Services;

/// <summary>
/// Applies strategy price recommendations to a bot's PUBLIC_SALES building units
/// by calling the <c>updatePublicSalesPrice</c> GraphQL mutation on the game API.
/// </summary>
public sealed class PriceAdjustmentService
{
    private readonly AccountService _accounts;
    private readonly ILogger<PriceAdjustmentService> _logger;

    public PriceAdjustmentService(
        AccountService accounts,
        ILogger<PriceAdjustmentService> logger)
    {
        _accounts = accounts;
        _logger = logger;
    }

    /// <summary>
    /// Applies the price-adjustment factor from <paramref name="recommendation"/> to all
    /// adjustable PUBLIC_SALES units owned by the bot.  Only units with an existing non-zero
    /// minimum price are updated; no-op adjustments (less than 1 cent change) are skipped.
    /// </summary>
    /// <param name="bot">The bot whose units should be adjusted.</param>
    /// <param name="recommendation">The strategy recommendation produced by <see cref="BotProfitCalculator"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of units that were actually updated.</returns>
    public async Task<int> ApplyAdjustmentAsync(
        BotAccount bot,
        StrategyRecommendation recommendation,
        CancellationToken ct)
    {
        if (!recommendation.ShouldAct)
            return 0;

        if (bot.Token is null || bot.Profile is null)
        {
            _logger.LogWarning("{Bot} Skipping price adjustment — no token or profile.", bot);
            return 0;
        }

        var adjustable = PriceAdjustmentHelper
            .SelectAdjustableUnits(bot.Profile.Companies)
            .ToList();

        if (adjustable.Count == 0)
        {
            _logger.LogDebug("{Bot} No adjustable PUBLIC_SALES units found.", bot);
            return 0;
        }

        var factor = recommendation.PriceAdjustmentFactor;
        int updated = 0;

        foreach (var (unit, buildingName) in adjustable)
        {
            if (ct.IsCancellationRequested) break;

            var current = unit.MinPrice!.Value;
            var newPrice = PriceAdjustmentHelper.ComputeNewPrice(current, factor);

            if (!PriceAdjustmentHelper.IsAdjustmentMeaningful(current, newPrice))
            {
                _logger.LogDebug("{Bot} Unit {UnitId} in {Building}: new price {New} not meaningfully different from {Current} — skipping.",
                    bot, unit.Id, buildingName, newPrice, current);
                continue;
            }

            try
            {
                await _accounts.UpdatePublicSalesPriceAsync(unit.Id, newPrice, bot.Token, ct);
                _logger.LogInformation(
                    "{Bot} Updated unit {UnitId} in {Building}: {Old} → {New} (×{Factor:P0})",
                    bot, unit.Id, buildingName, current, newPrice, factor);
                updated++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{Bot} Failed to update price for unit {UnitId} in {Building}.",
                    bot, unit.Id, buildingName);
            }
        }

        if (updated > 0)
            _logger.LogInformation("{Bot} Applied price adjustment to {Count}/{Total} unit(s). Reason: {Reason}",
                bot, updated, adjustable.Count, recommendation.Reason);

        return updated;
    }
}

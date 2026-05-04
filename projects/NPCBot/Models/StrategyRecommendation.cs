namespace Capitalism.NPCBot.Models;

/// <summary>
/// A suggested action the orchestrator should consider for a bot, produced by
/// <see cref="Capitalism.NPCBot.Services.BotProfitCalculator"/>.
/// </summary>
public sealed class StrategyRecommendation
{
    /// <summary>Whether any corrective action is suggested.</summary>
    public bool ShouldAct { get; init; }

    /// <summary>Human-readable description of the recommended action.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Fractional multiplier to apply to the current price (e.g. 0.95 = reduce by 5%).
    /// Zero means no price change is recommended.
    /// </summary>
    public decimal PriceAdjustmentFactor { get; init; }

    public static StrategyRecommendation NoAction { get; } = new()
    {
        ShouldAct = false,
        Reason = "Performance is acceptable.",
        PriceAdjustmentFactor = 0m,
    };
}

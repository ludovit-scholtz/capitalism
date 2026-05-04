using Capitalism.NPCBot.Models;

namespace Capitalism.NPCBot.Services;

/// <summary>
/// Pure-function profitability helpers used by the orchestrator to decide whether
/// a bot needs to adjust its game settings on the next poll cycle.
///
/// All methods are static so they can be unit-tested without an HTTP client.
/// </summary>
public static class BotProfitCalculator
{
    /// <summary>
    /// Neutral band: if the net-worth delta is within ±<c>NeutralBandPercent</c> of the
    /// initial net worth the bot is considered "neutral" rather than profitable/unprofitable.
    /// </summary>
    public const decimal NeutralBandPercent = 0.02m; // ±2 %

    /// <summary>
    /// Threshold below which a bot is considered severely unprofitable and should take
    /// aggressive corrective action (price reduction by <see cref="AggressivePriceReductionFactor"/>).
    /// </summary>
    public const decimal SeverelyUnprofitableThresholdPercent = -0.10m; // –10 %

    /// <summary>Price multiplier applied when the bot is mildly unprofitable (−5%).</summary>
    public const decimal MildPriceReductionFactor = 0.95m;

    /// <summary>Price multiplier applied when the bot is severely unprofitable (−10%).</summary>
    public const decimal AggressivePriceReductionFactor = 0.85m;

    // ── Net-worth computation ─────────────────────────────────────────────────

    /// <summary>
    /// Computes a net-worth proxy by summing <c>Company.Cash</c> across all owned companies.
    /// <para>
    /// NOTE: the game's target money model stores spendable funds in bank accounts, not on
    /// <c>Company.Cash</c>. This method should be replaced with a bank-account balance query
    /// once the GraphQL surface exposes per-company account totals.
    /// </para>
    /// </summary>
    public static decimal ComputeNetWorth(PlayerProfile profile) =>
        profile.Companies.Sum(c => c.Cash);

    // ── Profitability classification ──────────────────────────────────────────

    /// <summary>
    /// Classifies a bot's current profitability relative to its starting net worth.
    /// </summary>
    /// <param name="currentNetWorth">Net worth measured at the current tick.</param>
    /// <param name="initialNetWorth">Net worth measured at tracking start.</param>
    /// <returns>A <see cref="ProfitabilityStatus"/> value.</returns>
    public static ProfitabilityStatus Classify(decimal currentNetWorth, decimal initialNetWorth)
    {
        if (initialNetWorth == 0m)
            return ProfitabilityStatus.Unknown;

        var deltaPercent = (currentNetWorth - initialNetWorth) / Math.Abs(initialNetWorth);

        return deltaPercent switch
        {
            _ when deltaPercent > NeutralBandPercent => ProfitabilityStatus.Profitable,
            _ when deltaPercent < -NeutralBandPercent => ProfitabilityStatus.Unprofitable,
            _ => ProfitabilityStatus.Neutral,
        };
    }

    // ── Profit-rate computation ───────────────────────────────────────────────

    /// <summary>
    /// Computes the annualised profit rate as a percentage of the initial net worth.
    /// Returns zero when <paramref name="ticksElapsed"/> is zero or
    /// <paramref name="initialNetWorth"/> is zero.
    /// </summary>
    /// <param name="currentNetWorth">Net worth at the current tick.</param>
    /// <param name="initialNetWorth">Net worth at tracking start.</param>
    /// <param name="ticksElapsed">Number of ticks since tracking started.</param>
    /// <param name="ticksPerYear">Number of ticks in one in-game year (default 8760).</param>
    public static decimal ComputeAnnualisedRatePercent(
        decimal currentNetWorth,
        decimal initialNetWorth,
        long ticksElapsed,
        int ticksPerYear = 8760)
    {
        if (ticksElapsed <= 0 || initialNetWorth == 0m)
            return 0m;

        var delta = currentNetWorth - initialNetWorth;
        var ratePerTick = delta / (initialNetWorth * ticksElapsed);
        return ratePerTick * ticksPerYear * 100m;
    }

    // ── Strategy recommendation ───────────────────────────────────────────────

    /// <summary>
    /// Evaluates whether the bot should adjust its public-sales price on the next tick.
    /// Returns a <see cref="StrategyRecommendation"/> containing the factor to apply.
    /// </summary>
    /// <param name="currentNetWorth">Net worth at the current tick.</param>
    /// <param name="initialNetWorth">Net worth at tracking start.</param>
    /// <param name="ticksElapsed">Number of ticks since tracking started.</param>
    /// <param name="minTicksBeforeAdjustment">
    /// Minimum number of ticks that must have elapsed before a price recommendation is made.
    /// This prevents the bot from reacting prematurely to startup noise.
    /// </param>
    public static StrategyRecommendation Recommend(
        decimal currentNetWorth,
        decimal initialNetWorth,
        long ticksElapsed,
        int minTicksBeforeAdjustment = 5)
    {
        // Do not advise action before the bot has had time to establish a baseline.
        if (ticksElapsed < minTicksBeforeAdjustment || initialNetWorth == 0m)
            return StrategyRecommendation.NoAction;

        var deltaPercent = (currentNetWorth - initialNetWorth) / Math.Abs(initialNetWorth);

        // Severe loss → aggressively reduce prices to stimulate sales
        if (deltaPercent <= SeverelyUnprofitableThresholdPercent)
            return new StrategyRecommendation
            {
                ShouldAct = true,
                Reason = $"Severe loss ({deltaPercent:P1}): reduce prices aggressively.",
                PriceAdjustmentFactor = AggressivePriceReductionFactor,
            };

        // Mild loss → moderate price reduction
        if (deltaPercent < -NeutralBandPercent)
            return new StrategyRecommendation
            {
                ShouldAct = true,
                Reason = $"Mild loss ({deltaPercent:P1}): reduce prices slightly.",
                PriceAdjustmentFactor = MildPriceReductionFactor,
            };

        // Neutral or profitable → no adjustment needed
        return StrategyRecommendation.NoAction;
    }
}

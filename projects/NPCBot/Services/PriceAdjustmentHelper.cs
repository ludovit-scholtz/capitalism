using Capitalism.NPCBot.Models;

namespace Capitalism.NPCBot.Services;

/// <summary>
/// Pure-function helpers for computing NPC bot public-sales price adjustments.
/// All methods are static so they can be unit-tested without an HTTP client.
/// </summary>
public static class PriceAdjustmentHelper
{
    /// <summary>
    /// Unit type constant used to identify adjustable public-sales units.
    /// </summary>
    public const string PublicSalesUnitType = "PUBLIC_SALES";

    /// <summary>
    /// Minimum meaningful price floor: new prices are clamped to at least this value
    /// to avoid accidentally zeroing a unit's selling price due to floating-point drift.
    /// </summary>
    public const decimal MinimumAllowedPrice = 0.01m;

    // ── Unit selection ────────────────────────────────────────────────────────

    /// <summary>
    /// Enumerates all PUBLIC_SALES building units across all companies owned by the player.
    /// Only units with a non-null, non-zero existing price are returned — units that have
    /// never been configured are excluded because there is no sensible base price to adjust.
    /// </summary>
    /// <param name="companies">All companies from the player's profile.</param>
    /// <returns>Sequence of (unit, buildingName) tuples for units that can be adjusted.</returns>
    public static IEnumerable<(UnitSummary Unit, string BuildingName)> SelectAdjustableUnits(
        IEnumerable<CompanySummary> companies) =>
        from company in companies
        from building in company.Buildings
        from unit in building.Units
        where string.Equals(unit.UnitType, PublicSalesUnitType, StringComparison.OrdinalIgnoreCase)
           && unit.MinPrice is > 0m
        select (unit, building.Name);

    // ── Price computation ─────────────────────────────────────────────────────

    /// <summary>
    /// Computes a new minimum sale price by applying a factor to the current price.
    /// The result is rounded to 2 decimal places and clamped to <see cref="MinimumAllowedPrice"/>
    /// so no zero or negative prices can be sent to the API.
    /// </summary>
    /// <param name="currentPrice">Current minimum sale price of the unit.</param>
    /// <param name="factor">Multiplier to apply (e.g. 0.95 reduces by 5%).</param>
    /// <returns>Adjusted price, rounded to 2 d.p. and at least <see cref="MinimumAllowedPrice"/>.</returns>
    public static decimal ComputeNewPrice(decimal currentPrice, decimal factor)
    {
        var raw = currentPrice * factor;
        var rounded = Math.Round(raw, 2, MidpointRounding.AwayFromZero);
        return Math.Max(rounded, MinimumAllowedPrice);
    }

    /// <summary>
    /// Returns true when the computed new price is meaningfully different from the current
    /// price (differs by at least one cent) so the bot does not spam no-op price updates.
    /// </summary>
    /// <param name="currentPrice">Current minimum sale price.</param>
    /// <param name="newPrice">Proposed new minimum sale price from <see cref="ComputeNewPrice"/>.</param>
    public static bool IsAdjustmentMeaningful(decimal currentPrice, decimal newPrice) =>
        Math.Abs(currentPrice - newPrice) >= 0.01m;
}

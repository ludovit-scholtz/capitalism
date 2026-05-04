using Capitalism.NPCBot.Models;

namespace Capitalism.NPCBot.Services;

/// <summary>
/// Pure static helpers for lot and product selection during NPC onboarding.
/// Extracted from <see cref="OnboardingService"/> to allow unit testing without I/O.
/// </summary>
public static class OnboardingHelpers
{
    /// <summary>
    /// Returns the cheapest available lot that has no building placed on it yet
    /// and whose <see cref="BuildingLotSummary.SuitableTypes"/> contains
    /// <paramref name="suitableType"/>.
    /// </summary>
    /// <param name="lots">All lots in the target city.</param>
    /// <param name="suitableType">The type to look for (e.g. "FACTORY", "SALES_SHOP").</param>
    /// <returns>The matching lot with the lowest price, or <c>null</c> if none found.</returns>
    public static BuildingLotSummary? PickCheapestAvailableLot(
        IEnumerable<BuildingLotSummary> lots,
        string suitableType) =>
        lots
            .Where(l => l.BuildingId is null && ContainsSuitableType(l.SuitableTypes, suitableType))
            .OrderBy(l => l.Price)
            .FirstOrDefault();

    /// <summary>
    /// Returns the cheapest non-Pro product from <paramref name="products"/>,
    /// or <c>null</c> if all products are Pro-only or the list is empty.
    /// </summary>
    public static ProductTypeSummary? PickCheapestFreeProduct(
        IEnumerable<ProductTypeSummary> products) =>
        products
            .Where(p => !p.IsProOnly)
            .OrderBy(p => p.BasePrice)
            .FirstOrDefault();

    /// <summary>
    /// Checks whether <paramref name="suitableTypesField"/> (a comma-separated list
    /// such as "FACTORY,MINE") contains the given <paramref name="suitableType"/>
    /// as a whole segment (case-insensitive).
    /// </summary>
    public static bool ContainsSuitableType(string suitableTypesField, string suitableType)
    {
        if (string.IsNullOrWhiteSpace(suitableTypesField) ||
            string.IsNullOrWhiteSpace(suitableType))
            return false;

        return suitableTypesField
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(t => t.Equals(suitableType, StringComparison.OrdinalIgnoreCase));
    }
}

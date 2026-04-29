using Api.Data.Entities;

namespace Api.Types;

/// <summary>
/// A single ranked product candidate returned by the <c>rankedProductTypes</c> query.
/// The <see cref="RankingReason"/> field lets the UI display a concise hint about why the
/// product was promoted so players can make informed decisions without needing to infer
/// business logic client-side.
/// </summary>
public sealed class RankedProductResult
{
    /// <summary>The full product type, including recipes and access metadata.</summary>
    public ProductType ProductType { get; set; } = null!;

    /// <summary>
    /// Machine-readable reason this product was ranked where it is.
    /// One of: <c>connected</c>, <c>manufacturing</c>, <c>used_by_company</c>, <c>catalog</c>.
    /// <list type="bullet">
    ///   <item><c>connected</c> – the product is already configured in another unit of the same building.</item>
    ///   <item><c>manufacturing</c> – the player's company is actively manufacturing this product (highest R&amp;D priority, score 80).</item>
    ///   <item><c>used_by_company</c> – the player's company sells or stocks this product but does not manufacture it (secondary R&amp;D priority, score 50).</item>
    ///   <item><c>catalog</c> – no special priority; falls back to alphabetical order.</item>
    /// </list>
    /// </summary>
    public string RankingReason { get; set; } = "catalog";

    /// <summary>
    /// Numeric score used to sort results. Higher scores appear first.
    /// Connected = 100, manufacturing = 80, used_by_company = 50, catalog = 10.
    /// Within the same score tier products are sorted alphabetically.
    /// </summary>
    public int RankingScore { get; set; } = 10;
}

/// <summary>Well-known ranking reason constants.</summary>
public static class ProductRankingReason
{
    /// <summary>Product is already configured in another unit of the same building.</summary>
    public const string Connected = "connected";

    /// <summary>Player's company is actively manufacturing this product in at least one building (R&amp;D context, highest priority after connected, score 80).</summary>
    public const string Manufacturing = "manufacturing";

    /// <summary>Player's company sells or stocks this product but does not manufacture it (R&amp;D context, secondary priority, score 50).</summary>
    public const string UsedByCompany = "used_by_company";

    /// <summary>No special context; alphabetical fallback.</summary>
    public const string Catalog = "catalog";
}

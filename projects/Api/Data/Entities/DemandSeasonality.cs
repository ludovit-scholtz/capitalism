namespace Api.Data.Entities;

/// <summary>
/// Stores per-product seasonal demand multipliers for each game-year quarter.
/// One row per product type; the appropriate multiplier is applied to city base demand
/// in <see cref="Engine.Phases.PublicSalesPhase"/> based on the current game tick.
/// </summary>
/// <remarks>
/// Quarter index is derived from the current tick:
///   quarterIndex = (currentTick / GameConstants.TicksPerQuarter) % 4
/// Where: 0 = Q1 (Jan–Mar), 1 = Q2 (Apr–Jun), 2 = Q3 (Jul–Sep), 3 = Q4 (Oct–Dec).
/// </remarks>
public sealed class DemandSeasonality
{
    public Guid Id { get; set; }

    /// <summary>Product type this seasonality row applies to.</summary>
    public Guid ProductTypeId { get; set; }

    /// <summary>
    /// Demand multiplier for Q1 (January – March).
    /// 1.0 = neutral. &gt;1.0 = higher demand. &lt;1.0 = lower demand.
    /// </summary>
    public decimal Q1Multiplier { get; set; } = 1.0m;

    /// <summary>
    /// Demand multiplier for Q2 (April – June).
    /// 1.0 = neutral. &gt;1.0 = higher demand. &lt;1.0 = lower demand.
    /// </summary>
    public decimal Q2Multiplier { get; set; } = 1.0m;

    /// <summary>
    /// Demand multiplier for Q3 (July – September).
    /// 1.0 = neutral. &gt;1.0 = higher demand. &lt;1.0 = lower demand.
    /// </summary>
    public decimal Q3Multiplier { get; set; } = 1.0m;

    /// <summary>
    /// Demand multiplier for Q4 (October – December).
    /// 1.0 = neutral. &gt;1.0 = higher demand. &lt;1.0 = lower demand.
    /// </summary>
    public decimal Q4Multiplier { get; set; } = 1.0m;

    /// <summary>Navigation property for the related product type.</summary>
    public ProductType? ProductType { get; set; }

    /// <summary>
    /// Returns the seasonal multiplier for the given quarter index (0=Q1, 1=Q2, 2=Q3, 3=Q4).
    /// Defaults to 1.0m for any out-of-range index.
    /// </summary>
    public decimal GetMultiplierForQuarter(int quarterIndex) => quarterIndex switch
    {
        0 => Q1Multiplier,
        1 => Q2Multiplier,
        2 => Q3Multiplier,
        3 => Q4Multiplier,
        _ => 1.0m,
    };
}

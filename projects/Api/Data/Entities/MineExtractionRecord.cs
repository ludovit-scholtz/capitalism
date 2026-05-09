namespace Api.Data.Entities;

/// <summary>
/// Per-tick extraction record for a mine building.
/// Written by <see cref="Engine.Phases.MiningPhase"/> each tick to enable historical
/// sparkline charts and depletion forecasting in the building detail view.
/// Records older than 90 game days are pruned automatically.
/// </summary>
public sealed class MineExtractionRecord
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The mine building that produced the extraction.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>Game tick at which this extraction occurred.</summary>
    public long Tick { get; set; }

    /// <summary>Total quantity extracted from the deposit on this tick (sum of all MINING units).</summary>
    public decimal ExtractedAmount { get; set; }

    /// <summary>Efficiency factor applied on this tick (0–1), derived from remaining reserve ratio.</summary>
    public decimal EfficiencyPercent { get; set; }

    /// <summary>Reserve quantity remaining in the lot after this tick's extraction.</summary>
    public decimal ReserveRemaining { get; set; }
}

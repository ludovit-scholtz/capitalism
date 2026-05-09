namespace Api.Types;

/// <summary>
/// A single per-tick extraction record returned by <see cref="Query.getMineExtractionHistory"/>.
/// </summary>
public sealed class MineExtractionHistoryRecord
{
    /// <summary>Game tick when extraction occurred.</summary>
    public long Tick { get; set; }

    /// <summary>Total quantity extracted this tick (all MINING units combined).</summary>
    public decimal ExtractedAmount { get; set; }

    /// <summary>Efficiency factor applied this tick (0–1 range expressed as a fraction, e.g. 0.85 = 85%).</summary>
    public decimal EfficiencyPercent { get; set; }

    /// <summary>Reserve quantity remaining in the lot after this tick's extraction.</summary>
    public decimal ReserveRemaining { get; set; }
}

/// <summary>
/// Depletion forecast returned by <see cref="Query.getMineDepletionForecast"/>.
/// </summary>
public sealed class MineDepletionForecast
{
    /// <summary>Rolling average extraction rate (units/tick) used for projection. Null if no history.</summary>
    public decimal? AverageExtractionRatePerTick { get; set; }

    /// <summary>Projected tick at which the reserve will reach 0. Null if rate is 0 or no history.</summary>
    public long? DepletionTick { get; set; }

    /// <summary>Projected tick at which the reserve will fall to 5% of original. Null if already below or rate is unknown.</summary>
    public long? Critical5PctTick { get; set; }

    /// <summary>Projected tick at which the reserve will fall to 20% of original. Null if already below or rate is unknown.</summary>
    public long? Critical20PctTick { get; set; }

    /// <summary>Estimated game days remaining until full depletion. Null if rate is 0 or no history.</summary>
    public decimal? EstimatedGameDaysRemaining { get; set; }

    /// <summary>Current reserve quantity remaining.</summary>
    public decimal? CurrentReserve { get; set; }

    /// <summary>Original reserve quantity (initial deposit size).</summary>
    public decimal? OriginalReserve { get; set; }
}

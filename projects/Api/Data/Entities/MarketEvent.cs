namespace Api.Data.Entities;

/// <summary>
/// Time-bounded market event affecting pricing, demand, or interest rates.
/// </summary>
public sealed class MarketEvent
{
    public Guid Id { get; set; }

    /// <summary>COMMODITY_SHOCK | INTEREST_RATE_CHANGE | SEASONAL_DEMAND_SURGE</summary>
    public string EventType { get; set; } = MarketEventType.CommodityShock;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public Guid? AffectedResourceTypeId { get; set; }
    public ResourceType? AffectedResourceType { get; set; }

    public Guid? AffectedCityId { get; set; }
    public City? AffectedCity { get; set; }

    /// <summary>
    /// Event multiplier value. For commodity and demand events this is a direct multiplier.
    /// For interest events this multiplies annual lending rates.
    /// </summary>
    public decimal MagnitudeMultiplier { get; set; } = 1.0m;

    public long StartsAtTick { get; set; }
    public long ExpiresAtTick { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class MarketEventType
{
    public const string CommodityShock = "COMMODITY_SHOCK";
    public const string InterestRateChange = "INTEREST_RATE_CHANGE";
    public const string SeasonalDemandSurge = "SEASONAL_DEMAND_SURGE";
}

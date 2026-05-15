namespace Api.Data.Entities;

/// <summary>
/// Server-wide economic shock event that simultaneously affects multiple tick-engine phases.
/// Events may be triggered automatically by <see cref="Engine.Phases.GlobalEventPhase"/> or
/// manually by an administrator via the admin dashboard.
/// </summary>
public sealed class GlobalEvent
{
    public Guid Id { get; set; }

    /// <summary>
    /// SUPPLY_CHAIN_DISRUPTION | TRADE_WAR | ENVIRONMENTAL_DISASTER | TECH_BOOM |
    /// ENERGY_CRISIS | GLOBAL_RECESSION | PANDEMIC_SHOCK | INFRASTRUCTURE_FAILURE
    /// </summary>
    public string EventType { get; set; } = GlobalEventType.SupplyChainDisruption;

    /// <summary>MINOR | MODERATE | MAJOR | CATASTROPHIC</summary>
    public string Severity { get; set; } = GlobalEventSeverity.Minor;

    /// <summary>Short title shown in banners and event lists.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Detailed description of the event and its economic effects.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Whether the event is currently active and affecting the tick engine.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Game tick when this event started.</summary>
    public long StartTick { get; set; }

    /// <summary>How many ticks this event lasts.</summary>
    public long DurationTicks { get; set; }

    /// <summary>When null the event affects all cities; otherwise restricted to one city.</summary>
    public Guid? AffectedCityId { get; set; }
    public City? AffectedCity { get; set; }

    /// <summary>
    /// Multiplier applied to building operating costs (labor + energy).
    /// Values &gt; 1.0 increase costs; values &lt; 1.0 decrease costs.
    /// </summary>
    public decimal OperatingCostMultiplier { get; set; } = 1.0m;

    /// <summary>
    /// Multiplier applied to trade-route shipping costs.
    /// Values &gt; 1.0 increase shipping cost (logistics disruption).
    /// </summary>
    public decimal TradeRouteMultiplier { get; set; } = 1.0m;

    /// <summary>
    /// Multiplier applied to R&amp;D research budget accumulation per tick.
    /// Values &gt; 1.0 accelerate research (tech boom).
    /// </summary>
    public decimal RdMultiplier { get; set; } = 1.0m;

    /// <summary>
    /// Multiplier applied to raw-material extraction yield per mining tick.
    /// Values &lt; 1.0 reduce mine output (environmental disaster, power crisis).
    /// If <see cref="AffectedCityId"/> is non-null, applies only to mines in that city.
    /// </summary>
    public decimal MineEfficiencyMultiplier { get; set; } = 1.0m;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when the event was deactivated (null = still active or auto-expired).</summary>
    public DateTime? ResolvedAtUtc { get; set; }

    /// <summary>Player ID of the admin who manually triggered this event; null for auto-generated events.</summary>
    public string? TriggeredByAdminId { get; set; }
}

public static class GlobalEventType
{
    /// <summary>Raises trade-route costs and operating expenses.</summary>
    public const string SupplyChainDisruption = "SUPPLY_CHAIN_DISRUPTION";

    /// <summary>Dramatically raises trade-route costs via tariffs.</summary>
    public const string TradeWar = "TRADE_WAR";

    /// <summary>Reduces mine efficiency in an affected city; raises operating costs.</summary>
    public const string EnvironmentalDisaster = "ENVIRONMENTAL_DISASTER";

    /// <summary>Positive event — accelerates R&amp;D and boosts demand.</summary>
    public const string TechBoom = "TECH_BOOM";

    /// <summary>Significantly raises operating costs for all energy-dependent buildings.</summary>
    public const string EnergyCrisis = "ENERGY_CRISIS";

    /// <summary>Lowers operating costs and trade-route costs (depressed demand).</summary>
    public const string GlobalRecession = "GLOBAL_RECESSION";

    /// <summary>Raises operating costs, reduces mine efficiency, disrupts trade routes.</summary>
    public const string PandemicShock = "PANDEMIC_SHOCK";

    /// <summary>Disrupts shipping and reduces mine efficiency in an affected city.</summary>
    public const string InfrastructureFailure = "INFRASTRUCTURE_FAILURE";

    public static readonly string[] All =
    [
        SupplyChainDisruption, TradeWar, EnvironmentalDisaster, TechBoom,
        EnergyCrisis, GlobalRecession, PandemicShock, InfrastructureFailure,
    ];
}

public static class GlobalEventSeverity
{
    public const string Minor = "MINOR";
    public const string Moderate = "MODERATE";
    public const string Major = "MAJOR";
    public const string Catastrophic = "CATASTROPHIC";
}

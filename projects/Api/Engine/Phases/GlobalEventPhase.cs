using Api.Data.Entities;

namespace Api.Engine.Phases;

/// <summary>
/// Manages the lifecycle of global economic shock events each tick.
/// Runs at Order=45 — before <see cref="OperatingCostPhase"/> (50) and all
/// economically sensitive phases, ensuring that newly triggered auto-events are
/// available to subsequent phases within the same tick (via TickContext, which is
/// populated by <see cref="TickProcessor.BuildContextAsync"/> at tick start).
///
/// Responsibilities:
///   1. Expire events whose StartTick + DurationTicks ≤ CurrentTick.
///   2. Optionally auto-trigger new events when the global event queue is empty
///      (once every <see cref="AutoTriggerIntervalTicks"/> ticks, with randomised probability).
///
/// Note: Multipliers for active events are pre-computed in BuildContextAsync and
/// stored on TickContext fields — they are NOT set by this phase.
/// </summary>
public sealed class GlobalEventPhase : ITickPhase
{
    public string Name => "GlobalEvents";
    public int Order => 45;

    // Auto-trigger an event at most once every N ticks when the queue is empty.
    private const int AutoTriggerIntervalTicks = 24;

    // Base probability (0–1) that a new event fires at the trigger interval.
    private const double AutoTriggerBaseProbability = 0.12;

    private static readonly Random Rng = Random.Shared;

    public Task ProcessAsync(TickContext context)
    {
        ExpireOldEvents(context);
        TryAutoTrigger(context);
        return Task.CompletedTask;
    }

    private static void ExpireOldEvents(TickContext context)
    {
        foreach (var evt in context.ActiveGlobalEvents)
        {
            if (evt.StartTick + evt.DurationTicks <= context.CurrentTick)
            {
                evt.IsActive = false;
                evt.ResolvedAtUtc = DateTime.UtcNow;
            }
        }
    }

    private static void TryAutoTrigger(TickContext context)
    {
        // Only consider auto-triggering on the designated interval ticks.
        if (context.CurrentTick % AutoTriggerIntervalTicks != 0)
            return;

        // Skip if there is already an active event (prevent stacking auto-events).
        var stillActive = context.ActiveGlobalEvents.Any(e => e.IsActive);
        if (stillActive)
            return;

        if (Rng.NextDouble() > AutoTriggerBaseProbability)
            return;

        var evt = CreateRandomEvent(context.CurrentTick);
        context.Db.GlobalEvents.Add(evt);
    }

    internal static GlobalEvent CreateRandomEvent(long currentTick)
    {
        var allTypes = GlobalEventType.All;
        var eventType = allTypes[Rng.Next(allTypes.Length)];
        var severity = PickSeverity();
        var (opCost, trade, rd, mine, duration) = PickMultipliers(eventType, severity);
        var affectedCityId = NeedsCityScope(eventType) ? null : (Guid?)null; // city-scoped events require explicit admin selection

        return new GlobalEvent
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            Severity = severity,
            Title = BuildTitle(eventType, severity),
            Description = BuildDescription(eventType, severity),
            IsActive = true,
            StartTick = currentTick,
            DurationTicks = duration,
            AffectedCityId = affectedCityId,
            OperatingCostMultiplier = opCost,
            TradeRouteMultiplier = trade,
            RdMultiplier = rd,
            MineEfficiencyMultiplier = mine,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    private static string PickSeverity()
    {
        var roll = Rng.NextDouble();
        return roll switch
        {
            < 0.45 => GlobalEventSeverity.Minor,
            < 0.75 => GlobalEventSeverity.Moderate,
            < 0.92 => GlobalEventSeverity.Major,
            _      => GlobalEventSeverity.Catastrophic,
        };
    }

    private static bool NeedsCityScope(string eventType) =>
        eventType is GlobalEventType.EnvironmentalDisaster or GlobalEventType.InfrastructureFailure;

    private static (decimal opCost, decimal trade, decimal rd, decimal mine, long durationTicks)
        PickMultipliers(string eventType, string severity)
    {
        var s = severity switch
        {
            GlobalEventSeverity.Moderate    => 1.3m,
            GlobalEventSeverity.Major       => 1.6m,
            GlobalEventSeverity.Catastrophic => 2.0m,
            _                               => 1.0m,
        };

        // Base multipliers per event type, scaled by severity factor.
        return eventType switch
        {
            GlobalEventType.SupplyChainDisruption => (
                opCost: Round(1m + 0.20m * s), trade: Round(1m + 0.50m * s), rd: 1m, mine: 1m,
                durationTicks: SeverityDuration(severity, 24)),

            GlobalEventType.TradeWar => (
                opCost: Round(1m + 0.30m * s), trade: Round(1m + 1.00m * s), rd: 1m, mine: 1m,
                durationTicks: SeverityDuration(severity, 48)),

            GlobalEventType.EnvironmentalDisaster => (
                opCost: Round(1m + 0.40m * s), trade: 1m, rd: 1m, mine: Round(1m - 0.50m * s),
                durationTicks: SeverityDuration(severity, 12)),

            GlobalEventType.TechBoom => (
                opCost: Round(1m + 0.10m * s), trade: 1m, rd: Round(1m + 1.00m * s), mine: 1m,
                durationTicks: SeverityDuration(severity, 48)),

            GlobalEventType.EnergyCrisis => (
                opCost: Round(1m + 0.50m * s), trade: 1m, rd: 1m, mine: Round(1m - 0.20m * s),
                durationTicks: SeverityDuration(severity, 36)),

            GlobalEventType.GlobalRecession => (
                opCost: Round(1m - 0.20m * s), trade: Round(1m - 0.30m * s), rd: Round(1m - 0.10m * s), mine: 1m,
                durationTicks: SeverityDuration(severity, 72)),

            GlobalEventType.PandemicShock => (
                opCost: Round(1m + 0.40m * s), trade: Round(1m + 0.30m * s), rd: 1m, mine: Round(1m - 0.30m * s),
                durationTicks: SeverityDuration(severity, 48)),

            GlobalEventType.InfrastructureFailure => (
                opCost: 1m, trade: Round(1m + 0.80m * s), rd: 1m, mine: Round(1m - 0.60m * s),
                durationTicks: SeverityDuration(severity, 24)),

            _ => (1m, 1m, 1m, 1m, 24),
        };
    }

    private static long SeverityDuration(string severity, long baselineTicks) =>
        severity switch
        {
            GlobalEventSeverity.Moderate    => baselineTicks * 2,
            GlobalEventSeverity.Major       => baselineTicks * 3,
            GlobalEventSeverity.Catastrophic => baselineTicks * 5,
            _                               => baselineTicks,
        };

    private static decimal Round(decimal v) =>
        Math.Clamp(decimal.Round(v, 2, MidpointRounding.AwayFromZero),
            GameConstants.MarketEventMultiplierMin,
            GameConstants.MarketEventMultiplierMax);

    private static string BuildTitle(string eventType, string severity)
    {
        var prefix = severity switch
        {
            GlobalEventSeverity.Catastrophic => "Catastrophic",
            GlobalEventSeverity.Major        => "Major",
            GlobalEventSeverity.Moderate     => "Moderate",
            _                                => "Minor",
        };
        var noun = eventType switch
        {
            GlobalEventType.SupplyChainDisruption => "Supply Chain Disruption",
            GlobalEventType.TradeWar              => "Trade War",
            GlobalEventType.EnvironmentalDisaster => "Environmental Disaster",
            GlobalEventType.TechBoom              => "Technology Boom",
            GlobalEventType.EnergyCrisis          => "Energy Crisis",
            GlobalEventType.GlobalRecession       => "Global Recession",
            GlobalEventType.PandemicShock         => "Pandemic Shock",
            GlobalEventType.InfrastructureFailure => "Infrastructure Failure",
            _                                     => "Market Shock",
        };
        return $"{prefix} {noun}";
    }

    private static string BuildDescription(string eventType, string severity)
    {
        return eventType switch
        {
            GlobalEventType.SupplyChainDisruption =>
                "Global supply chains are under strain. Shipping costs and operating expenses have increased.",
            GlobalEventType.TradeWar =>
                "International trade tensions have escalated. Tariffs are disrupting cross-border shipments.",
            GlobalEventType.EnvironmentalDisaster =>
                "An environmental disaster has impacted the region. Mining operations are severely disrupted and operating costs are elevated.",
            GlobalEventType.TechBoom =>
                "A wave of technological innovation is sweeping the market. R&D budgets are generating accelerated returns.",
            GlobalEventType.EnergyCrisis =>
                "Energy prices have surged dramatically. Operating costs for energy-dependent buildings have increased significantly.",
            GlobalEventType.GlobalRecession =>
                "A global economic downturn has set in. Operating costs and shipping rates are depressed alongside demand.",
            GlobalEventType.PandemicShock =>
                "A global health crisis is disrupting workforces and trade networks. Operating costs, logistics costs, and mine output are all affected.",
            GlobalEventType.InfrastructureFailure =>
                "Critical infrastructure damage is disrupting logistics and resource extraction across affected regions.",
            _ => "An unusual market shock is affecting the global economy.",
        };
    }
}

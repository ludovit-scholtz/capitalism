using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine.Phases;

/// <summary>
/// Maintains the global macroeconomic cycle and emits monthly market events.
/// </summary>
public sealed class EconomicCyclePhase : ITickPhase
{
    public string Name => "EconomicCycle";
    public int Order => 180;

    public async Task ProcessAsync(TickContext context)
    {
        var cycle = await context.Db.EconomicCycles
            .OrderByDescending(c => c.PhaseStartedTick)
            .FirstOrDefaultAsync();

        if (cycle is null)
        {
            cycle = new EconomicCycle
            {
                Id = Guid.NewGuid(),
                Phase = Data.Entities.EconomicCyclePhase.Expansion,
                PhaseStartedTick = context.CurrentTick,
                ExpectedDurationTicks = GetPhaseDuration(Data.Entities.EconomicCyclePhase.Expansion),
                IntensityFactor = GetPhaseIntensity(Data.Entities.EconomicCyclePhase.Expansion),
            };
            context.Db.EconomicCycles.Add(cycle);
        }

        SendRecessionWarningIfDue(context, cycle);

        if (context.CurrentTick % GameConstants.EconomicCycleEvaluationIntervalTicks == 0)
        {
            AdvanceCycleIfDue(cycle, context.CurrentTick);
            await EnsureMonthlyEventsAsync(context);
        }

        RepriceVariableLoans(context);
    }

    private static void AdvanceCycleIfDue(EconomicCycle cycle, long currentTick)
    {
        var elapsed = currentTick - cycle.PhaseStartedTick;
        if (elapsed < cycle.ExpectedDurationTicks)
            return;

        var nextPhase = Data.Entities.EconomicCyclePhase.Next(cycle.Phase);
        cycle.Phase = nextPhase;
        cycle.PhaseStartedTick = currentTick;
        cycle.ExpectedDurationTicks = GetPhaseDuration(nextPhase);
        cycle.IntensityFactor = GetPhaseIntensity(nextPhase);
        cycle.RecessionWarningSentForTick = null;
    }

    private static void SendRecessionWarningIfDue(TickContext context, EconomicCycle cycle)
    {
        if (cycle.Phase != Data.Entities.EconomicCyclePhase.Peak)
            return;

        var recessionStartsAt = cycle.PhaseStartedTick + cycle.ExpectedDurationTicks;
        var ticksUntilRecession = recessionStartsAt - context.CurrentTick;
        if (ticksUntilRecession <= 0 || ticksUntilRecession > GameConstants.RecessionWarningLeadTicks)
            return;

        if (cycle.RecessionWarningSentForTick == recessionStartsAt)
            return;

        var playerIds = context.Db.Players.Select(player => player.Id).ToList();
        foreach (var playerId in playerIds)
        {
            PlayerNotificationService.Add(
                context.Db,
                playerId,
                PlayerNotificationType.EconomicAlert,
                "Recession warning",
                $"A recession is expected in {ticksUntilRecession} ticks. Consider reducing risk and preserving liquidity.",
                context.CurrentTick);
        }

        cycle.RecessionWarningSentForTick = recessionStartsAt;
    }

    private static async Task EnsureMonthlyEventsAsync(TickContext context)
    {
        var activeEvents = await context.Db.MarketEvents
            .Where(me => me.StartsAtTick <= context.CurrentTick && me.ExpiresAtTick >= context.CurrentTick)
            .ToListAsync();

        if (!activeEvents.Any(me => me.EventType == MarketEventType.CommodityShock))
        {
            await CreateCommodityShockAsync(context);
        }

        if (!activeEvents.Any(me => me.EventType == MarketEventType.InterestRateChange))
        {
            await CreateInterestRateChangeAsync(context);
        }

        var monthOfYear = (int)((context.CurrentTick / GameConstants.TicksPerMonth) % 12) + 1;
        if (monthOfYear == 12 && !activeEvents.Any(me => me.EventType == MarketEventType.SeasonalDemandSurge))
        {
            await CreateSeasonalDemandSurgeAsync(context);
        }
    }

    private static async Task CreateCommodityShockAsync(TickContext context)
    {
        var resources = await context.Db.ResourceTypes
            .OrderBy(r => r.Name)
            .ToListAsync();
        if (resources.Count == 0) return;

        var idx = (int)(Math.Abs(context.CurrentTick) % resources.Count);
        var resource = resources[idx];
        var directionUp = ((context.CurrentTick / GameConstants.TicksPerMonth) % 2) == 0;
        var multiplier = directionUp ? 1.25m : 0.85m;

        var marketEvent = new MarketEvent
        {
            Id = Guid.NewGuid(),
            EventType = MarketEventType.CommodityShock,
            Title = $"Commodity shock: {resource.Name}",
            Description = directionUp
                ? $"{resource.Name} prices are spiking due to supply tension."
                : $"{resource.Name} prices are easing due to temporary oversupply.",
            AffectedResourceTypeId = resource.Id,
            MagnitudeMultiplier = Math.Clamp(multiplier, GameConstants.MarketEventMultiplierMin, GameConstants.MarketEventMultiplierMax),
            StartsAtTick = context.CurrentTick,
            ExpiresAtTick = context.CurrentTick + (GameConstants.TicksPerMonth / 2),
            CreatedAtUtc = DateTime.UtcNow,
        };
        context.Db.MarketEvents.Add(marketEvent);

        var playerIds = await context.Db.Players.Select(player => player.Id).ToListAsync();
        foreach (var playerId in playerIds)
        {
            PlayerNotificationService.Add(
                context.Db,
                playerId,
                PlayerNotificationType.EconomicAlert,
                marketEvent.Title,
                marketEvent.Description,
                context.CurrentTick);
        }
    }

    private static Task CreateInterestRateChangeAsync(TickContext context)
    {
        var monthIndex = (context.CurrentTick / GameConstants.TicksPerMonth) % 3;
        var multiplier = monthIndex switch
        {
            0 => 1.10m,
            1 => 0.95m,
            _ => 1.05m,
        };

        context.Db.MarketEvents.Add(new MarketEvent
        {
            Id = Guid.NewGuid(),
            EventType = MarketEventType.InterestRateChange,
            Title = "Interest rate update",
            Description = multiplier >= 1m
                ? "Borrowing rates are rising for newly issued and variable-rate loans."
                : "Borrowing rates are easing for newly issued and variable-rate loans.",
            MagnitudeMultiplier = Math.Clamp(multiplier, GameConstants.MarketEventMultiplierMin, GameConstants.MarketEventMultiplierMax),
            StartsAtTick = context.CurrentTick,
            ExpiresAtTick = context.CurrentTick + GameConstants.TicksPerMonth,
            CreatedAtUtc = DateTime.UtcNow,
        });

        return Task.CompletedTask;
    }

    private static Task CreateSeasonalDemandSurgeAsync(TickContext context)
    {
        context.Db.MarketEvents.Add(new MarketEvent
        {
            Id = Guid.NewGuid(),
            EventType = MarketEventType.SeasonalDemandSurge,
            Title = "Holiday demand surge",
            Description = "Consumer demand is seasonally elevated across retail categories.",
            MagnitudeMultiplier = 1.30m,
            StartsAtTick = context.CurrentTick,
            ExpiresAtTick = context.CurrentTick + GameConstants.TicksPerMonth,
            CreatedAtUtc = DateTime.UtcNow,
        });

        return Task.CompletedTask;
    }

    private static void RepriceVariableLoans(TickContext context)
    {
        var activeLoans = context.Db.Loans
            .Where(loan => loan.Status == LoanStatus.Active || loan.Status == LoanStatus.Overdue)
            .ToList();

        foreach (var loan in activeLoans)
        {
            if (!context.BuildingsById.TryGetValue(loan.BankBuildingId, out var bankBuilding))
                continue;

            var baseRate = bankBuilding.LendingInterestRatePercent ?? 8m;
            var multiplier = context.GetInterestRateMultiplier(bankBuilding.CityId);
            var repriced = decimal.Round(
                Math.Clamp(baseRate * multiplier, 0.1m, 100m),
                4,
                MidpointRounding.AwayFromZero);
            loan.AnnualInterestRatePercent = repriced;
        }
    }

    private static int GetPhaseDuration(string phase) => phase switch
    {
        Data.Entities.EconomicCyclePhase.Expansion => GameConstants.TicksPerMonth * 3,
        Data.Entities.EconomicCyclePhase.Peak => GameConstants.TicksPerMonth,
        Data.Entities.EconomicCyclePhase.Recession => GameConstants.TicksPerMonth * 2,
        Data.Entities.EconomicCyclePhase.Trough => GameConstants.TicksPerMonth,
        _ => GameConstants.TicksPerMonth * 2,
    };

    private static decimal GetPhaseIntensity(string phase) => phase switch
    {
        Data.Entities.EconomicCyclePhase.Expansion => 1.20m,
        Data.Entities.EconomicCyclePhase.Peak => 1.05m,
        Data.Entities.EconomicCyclePhase.Recession => 0.70m,
        Data.Entities.EconomicCyclePhase.Trough => 0.85m,
        _ => 1.0m,
    };
}

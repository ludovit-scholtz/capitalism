using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using HotChocolate;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    [Authorize]
    [GraphQLName("getCurrentEconomicCycle")]
    public async Task<EconomicCycleView?> GetCurrentEconomicCycle([Service] AppDbContext db)
    {
        var cycle = await db.EconomicCycles
            .AsNoTracking()
            .OrderByDescending(c => c.PhaseStartedTick)
            .FirstOrDefaultAsync();
        if (cycle is null) return null;

        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(state => state.CurrentTick)
            .FirstOrDefaultDeterministicAsync();
        var phaseEndTick = cycle.PhaseStartedTick + cycle.ExpectedDurationTicks;

        return new EconomicCycleView
        {
            Id = cycle.Id,
            Phase = cycle.Phase,
            PhaseStartedTick = cycle.PhaseStartedTick,
            ExpectedDurationTicks = cycle.ExpectedDurationTicks,
            IntensityFactor = cycle.IntensityFactor,
            PhaseEndTick = phaseEndTick,
            TicksRemaining = Math.Max(0, phaseEndTick - currentTick),
        };
    }

    [Authorize]
    [GraphQLName("getActiveMarketEvents")]
    public async Task<List<MarketEventView>> GetActiveMarketEvents(
        Guid? cityId,
        [Service] AppDbContext db)
    {
        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(state => state.CurrentTick)
            .FirstOrDefaultDeterministicAsync();

        var query = db.MarketEvents
            .AsNoTracking()
            .Include(me => me.AffectedResourceType)
            .Include(me => me.AffectedCity)
            .Where(me => me.StartsAtTick <= currentTick && me.ExpiresAtTick >= currentTick);

        if (cityId.HasValue)
        {
            query = query.Where(me => me.AffectedCityId == null || me.AffectedCityId == cityId.Value);
        }

        var events = await query
            .OrderBy(me => me.ExpiresAtTick)
            .ThenBy(me => me.EventType)
            .ToListAsync();

        return events.Select(me => new MarketEventView
        {
            Id = me.Id,
            EventType = me.EventType,
            Title = me.Title,
            Description = me.Description,
            MagnitudeMultiplier = me.MagnitudeMultiplier,
            StartsAtTick = me.StartsAtTick,
            ExpiresAtTick = me.ExpiresAtTick,
            TicksRemaining = Math.Max(0, me.ExpiresAtTick - currentTick),
            AffectedResourceTypeId = me.AffectedResourceTypeId,
            AffectedResourceName = me.AffectedResourceType?.Name,
            AffectedResourceSlug = me.AffectedResourceType?.Slug,
            AffectedCityId = me.AffectedCityId,
            AffectedCityName = me.AffectedCity?.Name,
        }).ToList();
    }

    [Authorize]
    [GraphQLName("getEconomicHistory")]
    public async Task<List<EconomicCycleHistoryPoint>> GetEconomicHistory(
        int last,
        [Service] AppDbContext db)
    {
        var cycle = await db.EconomicCycles
            .AsNoTracking()
            .OrderByDescending(c => c.PhaseStartedTick)
            .FirstOrDefaultAsync();
        if (cycle is null) return [];

        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(state => state.CurrentTick)
            .FirstOrDefaultDeterministicAsync();

        var span = Math.Max(1, last);
        var fromTick = Math.Max(0, currentTick - span);
        var step = Math.Max(1, span / 24);

        var points = new List<EconomicCycleHistoryPoint>();
        for (long tick = fromTick; tick <= currentTick; tick += step)
        {
            var phase = ResolvePhaseAtTick(cycle, tick);
            points.Add(new EconomicCycleHistoryPoint
            {
                Tick = tick,
                Phase = phase,
                IntensityFactor = GetPhaseIntensity(phase),
            });
        }

        if (points.Count == 0 || points[^1].Tick != currentTick)
        {
            var phase = ResolvePhaseAtTick(cycle, currentTick);
            points.Add(new EconomicCycleHistoryPoint
            {
                Tick = currentTick,
                Phase = phase,
                IntensityFactor = GetPhaseIntensity(phase),
            });
        }

        return points;
    }

    private static string ResolvePhaseAtTick(EconomicCycle cycle, long tick)
    {
        if (tick >= cycle.PhaseStartedTick)
            return cycle.Phase;

        var phase = cycle.Phase;
        var phaseStart = cycle.PhaseStartedTick;
        var guard = 0;
        while (tick < phaseStart && guard++ < 32)
        {
            var previousPhase = phase switch
            {
                EconomicCyclePhase.Expansion => EconomicCyclePhase.Trough,
                EconomicCyclePhase.Peak => EconomicCyclePhase.Expansion,
                EconomicCyclePhase.Recession => EconomicCyclePhase.Peak,
                _ => EconomicCyclePhase.Recession,
            };
            phaseStart -= GetPhaseDuration(previousPhase);
            phase = previousPhase;
        }

        return phase;
    }

    private static int GetPhaseDuration(string phase) => phase switch
    {
        EconomicCyclePhase.Expansion => GameConstants.TicksPerMonth * 3,
        EconomicCyclePhase.Peak => GameConstants.TicksPerMonth,
        EconomicCyclePhase.Recession => GameConstants.TicksPerMonth * 2,
        EconomicCyclePhase.Trough => GameConstants.TicksPerMonth,
        _ => GameConstants.TicksPerMonth * 2,
    };

    private static decimal GetPhaseIntensity(string phase) => phase switch
    {
        EconomicCyclePhase.Expansion => 1.20m,
        EconomicCyclePhase.Peak => 1.05m,
        EconomicCyclePhase.Recession => 0.70m,
        EconomicCyclePhase.Trough => 0.85m,
        _ => 1.0m,
    };
}

public sealed class EconomicCycleView
{
    public Guid Id { get; set; }
    public string Phase { get; set; } = EconomicCyclePhase.Expansion;
    public long PhaseStartedTick { get; set; }
    public int ExpectedDurationTicks { get; set; }
    public decimal IntensityFactor { get; set; }
    public long PhaseEndTick { get; set; }
    public long TicksRemaining { get; set; }
}

public sealed class MarketEventView
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal MagnitudeMultiplier { get; set; }
    public long StartsAtTick { get; set; }
    public long ExpiresAtTick { get; set; }
    public long TicksRemaining { get; set; }
    public Guid? AffectedResourceTypeId { get; set; }
    public string? AffectedResourceName { get; set; }
    public string? AffectedResourceSlug { get; set; }
    public Guid? AffectedCityId { get; set; }
    public string? AffectedCityName { get; set; }
}

public sealed class EconomicCycleHistoryPoint
{
    public long Tick { get; set; }
    public string Phase { get; set; } = EconomicCyclePhase.Expansion;
    public decimal IntensityFactor { get; set; }
}

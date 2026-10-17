using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using HotChocolate.CostAnalysis.Types;
using HotChocolate.Authorization;
using HotChocolate.Execution.Processing;
using HotChocolate.Resolvers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>Returns drill-down entries for a specific ledger category.</summary>
    [Authorize]
    [Cost(35)]
    public async Task<List<LedgerEntryResult>> GetLedgerDrillDown(
        Guid companyId,
        string category,
        int? gameYear,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var ownsCompany = await db.Companies
            .AnyAsync(c => c.Id == companyId && c.PlayerId == userId);

        if (!ownsCompany) return [];

        // Uses the compound (CompanyId, Category, RecordedAtTick) index.
        var entriesQuery = db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId && e.Category == category);

        if (gameYear.HasValue)
        {
            var startTick = GameTime.GetStartTickForGameYear(gameYear.Value);
            var endTick = GameTime.GetEndTickForGameYear(gameYear.Value);
            entriesQuery = entriesQuery.Where(e => e.RecordedAtTick >= startTick && e.RecordedAtTick <= endTick);
        }

        var entries = await entriesQuery
            .OrderByDescending(e => e.RecordedAtTick)
            .Take(200)
            .Include(e => e.Building).ThenInclude(b => b!.City)
            .Include(e => e.ProductType)
            .Include(e => e.ResourceType)
            .ToListAsync();

        var minTick = entries.Count > 0 ? entries.Min(e => e.RecordedAtTick) : 0L;
        var maxTick = entries.Count > 0 ? entries.Max(e => e.RecordedAtTick) : 0L;

        var relevantEvents = entries.Count == 0
            ? []
            : await db.MarketEvents
                .AsNoTracking()
                .Where(me => me.ExpiresAtTick >= minTick && me.StartsAtTick <= maxTick)
                .ToListAsync();

        var cycle = await db.EconomicCycles
            .AsNoTracking()
            .OrderByDescending(c => c.PhaseStartedTick)
            .FirstOrDefaultAsync();

        return entries.Select(e => new LedgerEntryResult
        {
            Id = e.Id,
            Category = e.Category,
            Description = e.Description,
            Amount = e.Amount,
            RecordedAtTick = e.RecordedAtTick,
            RecordedAtUtc = e.RecordedAtUtc,
            BuildingId = e.BuildingId,
            BuildingName = e.Building?.Name,
            BuildingType = e.Building?.Type,
            BuildingUnitId = e.BuildingUnitId,
            ProductTypeId = e.ProductTypeId,
            ProductName = e.ProductType?.Name,
            ResourceTypeId = e.ResourceTypeId,
            ResourceName = e.ResourceType?.Name,
            CurrencyCode = e.Building?.City?.CurrencyCode ?? "EUR",
            EventTag = ResolveLedgerEventTag(e, relevantEvents, cycle),
            EventDescription = ResolveLedgerEventDescription(e, relevantEvents, cycle),
        }).ToList();
    }

    private static bool IsTickInGameYear(long tick, int gameYear)
    {
        var startTick = GameTime.GetStartTickForGameYear(gameYear);
        var endTick = GameTime.GetEndTickForGameYear(gameYear);
        return tick >= startTick && tick <= endTick;
    }

    private static decimal ComputeCompanyAssetValue(
        Company company,
        IReadOnlyCollection<BuildingLot> allOwnedLots,
        IReadOnlyCollection<Inventory> allInventories)
    {
        var buildingIds = company.Buildings.Select(building => building.Id).ToHashSet();
        var inventoryValue = allInventories
            .Where(inventory => buildingIds.Contains(inventory.BuildingId))
            .Sum(WealthCalculator.GetItemBasePrice);
        var lotValue = allOwnedLots
            .Where(lot => lot.OwnerCompanyId == company.Id)
            .Sum(WealthCalculator.GetLandValue);

        return CompanyBankingService.GetTotalBalance(company) + company.Buildings.Sum(WealthCalculator.GetBuildingValue) + lotValue + allInventories
            .Where(inventory => buildingIds.Contains(inventory.BuildingId))
            .Sum(inventory => inventory.Quantity * WealthCalculator.GetItemBasePrice(inventory));
    }

    private static string? ResolveLedgerEventTag(
        LedgerEntry entry,
        IReadOnlyCollection<MarketEvent> events,
        EconomicCycle? cycle)
    {
        var marketEvent = ResolveMatchingEvent(entry, events);
        if (marketEvent is not null)
        {
            return marketEvent.EventType switch
            {
                MarketEventType.CommodityShock => $"📈 Commodity shock {(marketEvent.MagnitudeMultiplier - 1m) * 100m:+0;-0;0}%",
                MarketEventType.InterestRateChange => $"🏦 Interest shift {(marketEvent.MagnitudeMultiplier - 1m) * 100m:+0;-0;0}%",
                MarketEventType.SeasonalDemandSurge => $"🛍️ Seasonal surge {(marketEvent.MagnitudeMultiplier - 1m) * 100m:+0;-0;0}%",
                _ => null,
            };
        }

        if (cycle is not null && entry.Category == LedgerCategory.Revenue)
        {
            var phase = ResolveCyclePhaseAtTick(cycle, entry.RecordedAtTick);
            if (phase == EconomicCyclePhase.Recession)
                return "📉 Recession";
        }

        return null;
    }

    private static string? ResolveLedgerEventDescription(
        LedgerEntry entry,
        IReadOnlyCollection<MarketEvent> events,
        EconomicCycle? cycle)
    {
        var marketEvent = ResolveMatchingEvent(entry, events);
        if (marketEvent is not null)
            return marketEvent.Description;

        if (cycle is not null && entry.Category == LedgerCategory.Revenue)
        {
            var phase = ResolveCyclePhaseAtTick(cycle, entry.RecordedAtTick);
            if (phase == EconomicCyclePhase.Recession)
                return "Revenue was recorded during a recession phase.";
        }

        return null;
    }

    private static MarketEvent? ResolveMatchingEvent(LedgerEntry entry, IReadOnlyCollection<MarketEvent> events)
    {
        return events.FirstOrDefault(me =>
            me.StartsAtTick <= entry.RecordedAtTick
            && me.ExpiresAtTick >= entry.RecordedAtTick
            && (me.AffectedCityId == null || me.AffectedCityId == entry.Building?.CityId)
            && (me.EventType != MarketEventType.CommodityShock || !me.AffectedResourceTypeId.HasValue || me.AffectedResourceTypeId == entry.ResourceTypeId)
            && (me.EventType != MarketEventType.InterestRateChange
                || entry.Category == LedgerCategory.LoanInterestExpense
                || entry.Category == LedgerCategory.LoanInterestIncome)
            && (me.EventType != MarketEventType.SeasonalDemandSurge || entry.Category == LedgerCategory.Revenue));
    }

    private static string ResolveCyclePhaseAtTick(EconomicCycle cycle, long tick)
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
            phaseStart -= previousPhase switch
            {
                EconomicCyclePhase.Expansion => GameConstants.TicksPerMonth * 3,
                EconomicCyclePhase.Peak => GameConstants.TicksPerMonth,
                EconomicCyclePhase.Recession => GameConstants.TicksPerMonth * 2,
                _ => GameConstants.TicksPerMonth,
            };
            phase = previousPhase;
        }

        return phase;
    }
}

using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Returns per-tick P&amp;L analytics for a power plant building over the last
    /// <paramref name="limit"/> ticks (default 100, max 100).
    ///
    /// The timeline aggregates:
    ///   - surplusIncome  : GRID_SURPLUS_INCOME ledger entries
    ///   - gridFine       : GRID_FINE ledger entries (absolute value)
    ///   - operatingCosts : LABOR_COST + ENERGY_COST ledger entries
    ///   - netProfit      : surplusIncome – gridFine – operatingCosts
    /// </summary>
    [Authorize]
    public async Task<PowerPlantAnalytics> GetPowerPlantAnalytics(
        Guid buildingId,
        int? limit,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .AsNoTracking()
            .Include(b => b.Company)
            .FirstOrDefaultAsync(b => b.Id == buildingId);

        if (building is null || building.Company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Building not found or you don't own it.")
                    .SetCode("BUILDING_NOT_FOUND")
                    .Build());
        }

        if (building.Type != BuildingType.PowerPlant)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Building is not a power plant.")
                    .SetCode("NOT_A_POWER_PLANT")
                    .Build());
        }

        var safeLimit = Math.Clamp(limit ?? 100, 1, 100);
        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(gs => (long?)gs.CurrentTick)
            .FirstOrDefaultDeterministicAsync() ?? 0L;
        var windowStart = Math.Max(0L, currentTick - (safeLimit - 1L));

        var entries = await db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.BuildingId == buildingId
                && e.RecordedAtTick >= windowStart
                && e.RecordedAtTick <= currentTick
                && (e.Category == LedgerCategory.GridSurplusIncome
                    || e.Category == LedgerCategory.GridFine
                    || e.Category == LedgerCategory.LaborCost
                    || e.Category == LedgerCategory.EnergyCost))
            .Select(e => new { e.RecordedAtTick, e.Category, e.Amount })
            .OrderBy(e => e.RecordedAtTick)
            .ToListAsync();

        var byTick = entries.GroupBy(e => e.RecordedAtTick)
            .ToDictionary(g => g.Key, g => g.ToList());

        var snapshots = new List<PowerPlantTickSnapshot>();
        for (var tick = windowStart; tick <= currentTick; tick++)
        {
            var tickEntries = byTick.GetValueOrDefault(tick) ?? [];
            var surplusIncome = tickEntries
                .Where(e => e.Category == LedgerCategory.GridSurplusIncome)
                .Sum(e => e.Amount);
            var gridFine = tickEntries
                .Where(e => e.Category == LedgerCategory.GridFine)
                .Sum(e => Math.Abs(e.Amount));
            var opCosts = tickEntries
                .Where(e => e.Category is LedgerCategory.LaborCost or LedgerCategory.EnergyCost)
                .Sum(e => Math.Abs(e.Amount));

            snapshots.Add(new PowerPlantTickSnapshot
            {
                Tick = tick,
                SurplusIncome = surplusIncome,
                GridFine = gridFine,
                OperatingCosts = opCosts,
                NetProfit = surplusIncome - gridFine - opCosts,
            });
        }

        return new PowerPlantAnalytics
        {
            BuildingId = building.Id,
            BuildingName = building.Name,
            PlantType = building.PowerPlantType ?? "UNKNOWN",
            CurrentOutputMw = building.PowerOutput > 0m
                ? building.PowerOutput.Value
                : GameConstants.DefaultPowerOutputMw(building.PowerPlantType),
            DataFromTick = windowStart,
            DataToTick = currentTick,
            TotalSurplusIncome = snapshots.Sum(s => s.SurplusIncome),
            TotalGridFines = snapshots.Sum(s => s.GridFine),
            TotalOperatingCosts = snapshots.Sum(s => s.OperatingCosts),
            TotalNetProfit = snapshots.Sum(s => s.NetProfit),
            Timeline = snapshots,
        };
    }
}

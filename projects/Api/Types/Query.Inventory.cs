using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Returns per-unit inventory fill information for a building that belongs
    /// to the authenticated player.
    /// </summary>
    [Authorize]
    public async Task<List<BuildingUnitInventorySummary>> GetBuildingUnitInventorySummaries(
        Guid buildingId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .Include(candidate => candidate.Company)
            .Include(candidate => candidate.Units)
            .FirstOrDefaultAsync(candidate => candidate.Id == buildingId);

        if (building is null || building.Company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Building not found or you don't own it.")
                    .SetCode("BUILDING_NOT_FOUND")
                    .Build());
        }

        var inventories = await db.Inventories
            .Where(entry => entry.BuildingId == buildingId && entry.BuildingUnitId.HasValue)
            .ToListAsync();

        // Fetch last-tick movement data: load all history for these units in memory,
        // then pick out the latest tick per unit. Dictionary indexing inside EF Core
        // queries is not translatable to SQL, so we load first and filter in-process.
        var unitIds = building.Units.Select(u => u.Id).ToList();
        var allUnitHistory = await db.BuildingUnitResourceHistories
            .Where(h => unitIds.Contains(h.BuildingUnitId))
            .ToListAsync();

        var lastTickByUnit = allUnitHistory
            .GroupBy(h => h.BuildingUnitId)
            .ToDictionary(g => g.Key, g => g.Max(h => h.Tick));

        // Aggregate inflow/outflow across all items for the latest tick per unit.
        var inflowByUnit = allUnitHistory
            .Where(h => lastTickByUnit.TryGetValue(h.BuildingUnitId, out var maxTick) && h.Tick == maxTick)
            .GroupBy(h => h.BuildingUnitId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(h => h.InflowQuantity + h.ProducedQuantity));

        var outflowByUnit = allUnitHistory
            .Where(h => lastTickByUnit.TryGetValue(h.BuildingUnitId, out var maxTick) && h.Tick == maxTick)
            .GroupBy(h => h.BuildingUnitId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(h => h.OutflowQuantity + h.ConsumedQuantity));

        return building.Units
            .Select(unit =>
            {
                var capacity = GetUnitInventoryCapacity(unit);
                var unitInventories = inventories
                    .Where(entry => entry.BuildingUnitId == unit.Id)
                    .ToList();
                var quantity = unitInventories.Sum(entry => entry.Quantity);
                var averageQuality = quantity > 0m
                    ? decimal.Round(unitInventories.Sum(entry => entry.Quantity * entry.Quality) / quantity, 4, MidpointRounding.AwayFromZero)
                    : (decimal?)null;

                var hasHistory = lastTickByUnit.ContainsKey(unit.Id);
                inflowByUnit.TryGetValue(unit.Id, out var lastTickInflow);
                outflowByUnit.TryGetValue(unit.Id, out var lastTickOutflow);

                return new BuildingUnitInventorySummary
                {
                    BuildingUnitId = unit.Id,
                    Quantity = decimal.Round(quantity, 4, MidpointRounding.AwayFromZero),
                    Capacity = capacity,
                    FillPercent = capacity > 0m
                        ? decimal.Round(Math.Clamp(quantity / capacity, 0m, 1m), 4, MidpointRounding.AwayFromZero)
                        : 0m,
                    AverageQuality = averageQuality,
                    TotalSourcingCost = decimal.Round(unitInventories.Sum(entry => entry.SourcingCostTotal), 4, MidpointRounding.AwayFromZero),
                    SourcingCostPerUnit = quantity > 0m
                        ? decimal.Round(unitInventories.Sum(entry => entry.SourcingCostTotal) / quantity, 4, MidpointRounding.AwayFromZero)
                        : 0m,
                    LastTickInflow = hasHistory
                        ? decimal.Round(lastTickInflow, 4, MidpointRounding.AwayFromZero)
                        : null,
                    LastTickOutflow = hasHistory
                        ? decimal.Round(lastTickOutflow, 4, MidpointRounding.AwayFromZero)
                        : null,
                };
            })
            .Where(summary => summary.Capacity > 0m || summary.Quantity > 0m)
            .ToList();
    }

    /// <summary>
    /// Returns detailed inventory entries for units in a building that belongs
    /// to the authenticated player.
    /// </summary>
    [Authorize]
    public async Task<List<BuildingUnitInventory>> BuildingUnitInventories(
        Guid buildingId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .Include(candidate => candidate.Company)
            .FirstOrDefaultAsync(candidate => candidate.Id == buildingId);

        if (building is null || building.Company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Building not found or you don't own it.")
                    .SetCode("BUILDING_NOT_FOUND")
                    .Build());
        }

        var inventories = await db.Inventories
            .Where(entry => entry.BuildingId == buildingId && entry.BuildingUnitId.HasValue)
            .Select(entry => new BuildingUnitInventory
            {
                Id = entry.Id,
                BuildingUnitId = entry.BuildingUnitId!.Value,
                ResourceTypeId = entry.ResourceTypeId,
                ProductTypeId = entry.ProductTypeId,
                Quantity = entry.Quantity,
                SourcingCostTotal = entry.SourcingCostTotal,
                SourcingCostPerUnit = entry.Quantity > 0m
                    ? decimal.Round(entry.SourcingCostTotal / entry.Quantity, 4, MidpointRounding.AwayFromZero)
                    : 0m,
                Quality = entry.Quality
            })
            .ToListAsync();

        return inventories;
    }

    /// <summary>
    /// Returns recent per-tick movement history for every tracked resource or
    /// product inside the building's units.
    /// </summary>
    [Authorize]
    public async Task<List<BuildingUnitResourceHistoryPoint>> GetBuildingUnitResourceHistories(
        Guid buildingId,
        int? limit,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .Include(candidate => candidate.Company)
            .FirstOrDefaultAsync(candidate => candidate.Id == buildingId);

        if (building is null || building.Company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Building not found or you don't own it.")
                    .SetCode("BUILDING_NOT_FOUND")
                    .Build());
        }

        var historyQuery = db.BuildingUnitResourceHistories
            .Where(entry => entry.BuildingId == buildingId);

        var safeLimit = Math.Clamp(limit ?? 40, 1, 200);
        var maxTick = await historyQuery.MaxAsync(entry => (long?)entry.Tick);
        if (maxTick.HasValue)
        {
            var minTick = maxTick.Value - safeLimit + 1;
            historyQuery = historyQuery.Where(entry => entry.Tick >= minTick);
        }

        return await historyQuery
            .OrderBy(entry => entry.Tick)
            .ThenBy(entry => entry.BuildingUnitId)
            .ThenBy(entry => entry.ResourceTypeId)
            .ThenBy(entry => entry.ProductTypeId)
            .Select(entry => new BuildingUnitResourceHistoryPoint
            {
                BuildingUnitId = entry.BuildingUnitId,
                ResourceTypeId = entry.ResourceTypeId,
                ProductTypeId = entry.ProductTypeId,
                Tick = entry.Tick,
                InflowQuantity = entry.InflowQuantity,
                OutflowQuantity = entry.OutflowQuantity,
                ConsumedQuantity = entry.ConsumedQuantity,
                ProducedQuantity = entry.ProducedQuantity,
            })
            .ToListAsync();
    }

    private static decimal GetUnitInventoryCapacity(Data.Entities.BuildingUnit unit)
    {
        return unit.UnitType switch
        {
            UnitType.Mining or UnitType.Storage or UnitType.B2BSales
                or UnitType.Purchase or UnitType.Manufacturing
                or UnitType.Branding or UnitType.PublicSales
                => GameConstants.GetUnitHoldingCapacity(unit.UnitType, unit.Level),
            _ => 0m
        };
    }
}

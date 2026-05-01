using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

/// <summary>
/// Supply chain visualization queries for factory buildings.
/// </summary>
public sealed partial class Query
{
    /// <summary>
    /// Returns a complete supply chain diagram for a factory building,
    /// including unit statuses, fill levels, and inter-unit links with transit costs.
    /// </summary>
    [Authorize]
    public async Task<BuildingSupplyChainDiagram> BuildingSupplyChain(
        Guid buildingId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
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

        if (building.Type != Data.Entities.BuildingType.Factory)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Supply chain is only available for factory buildings.")
                    .SetCode("INVALID_BUILDING_TYPE")
                    .Build());
        }

        var units = await db.BuildingUnits
            .Where(u => u.BuildingId == buildingId)
            .ToListAsync();

        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync();
        var currentTick = gameState?.CurrentTick ?? 0L;
        var windowStart = Math.Max(0L, currentTick - 4L);

        // Load inventory status for each unit
        var inventoryByUnit = await db.Inventories
            .Where(i => i.BuildingUnitId.HasValue && units.Select(u => u.Id).Contains(i.BuildingUnitId!.Value))
            .GroupBy(i => i.BuildingUnitId!.Value)
            .Select(g => new { UnitId = g.Key, Total = g.Sum(i => i.Quantity) })
            .ToDictionaryAsync(x => x.UnitId, x => x.Total);

        // Load recent history to determine operational status
        var recentHistoryByUnit = await db.BuildingUnitResourceHistories
            .Where(h => h.BuildingId == buildingId && h.Tick >= windowStart)
            .GroupBy(h => h.BuildingUnitId)
            .Select(g => new
            {
                UnitId = g.Key,
                LastActiveTick = g.Max(h => h.Tick),
                TotalInflow = g.Sum(h => h.InflowQuantity),
                TotalOutflow = g.Sum(h => h.OutflowQuantity),
            })
            .ToDictionaryAsync(x => x.UnitId, x => x);

        // Load resource/product type names
        var resourceTypes = await db.ResourceTypes.ToListAsync();
        var productTypes = await db.ProductTypes.ToListAsync();
        var resourceDict = resourceTypes.ToDictionary(r => r.Id);
        var productDict = productTypes.ToDictionary(p => p.Id);

        // Build unit summaries with fill levels and status
        var unitSummaries = new List<SupplyChainUnitSummary>();
        var criticalUnitIds = new List<Guid>();
        var warningUnitIds = new List<Guid>();

        foreach (var unit in units)
        {
            inventoryByUnit.TryGetValue(unit.Id, out var inventoryTotal);
            recentHistoryByUnit.TryGetValue(unit.Id, out var history);

            var capacity = GetUnitInventoryCapacity(unit);
            var fillPercent = capacity > 0m ? (inventoryTotal / capacity) * 100m : 0m;

            var idleTicks = 0;
            if (history is not null && currentTick > 0)
            {
                idleTicks = (int)(currentTick - history.LastActiveTick);
            }
            else if (currentTick > 0)
            {
                idleTicks = (int)Math.Min(currentTick, 5L);
            }

            // Determine status based on operational logic
            var status = "IDLE";
            if (string.IsNullOrEmpty(unit.UnitType) || (unit.ProductTypeId is null && unit.ResourceTypeId is null))
            {
                status = "UNCONFIGURED";
            }
            else if (fillPercent >= 95m && (unit.UnitType == Data.Entities.UnitType.Storage || unit.UnitType == Data.Entities.UnitType.Purchase))
            {
                status = "FULL";
            }
            else if (idleTicks > 0)
            {
                status = "IDLE";
            }
            else if (history is not null && (history.TotalInflow > 0 || history.TotalOutflow > 0))
            {
                status = "ACTIVE";
            }

            // Track critical stalls (RED: > 20 ticks idle)
            if (idleTicks > 20)
            {
                criticalUnitIds.Add(unit.Id);
            }
            // Track warning stalls (YELLOW: > 5 ticks idle)
            else if (idleTicks > 5)
            {
                warningUnitIds.Add(unit.Id);
            }

            string? resourceOrProductName = null;
            if (unit.ResourceTypeId.HasValue && resourceDict.TryGetValue(unit.ResourceTypeId.Value, out var res))
            {
                resourceOrProductName = res.Name;
            }
            else if (unit.ProductTypeId.HasValue && productDict.TryGetValue(unit.ProductTypeId.Value, out var prod))
            {
                resourceOrProductName = prod.Name;
            }

            // Estimate transit cost for this unit's output
            decimal? estimatedTransitCost = null;
            if (unit.UnitType == Data.Entities.UnitType.Storage || unit.UnitType == Data.Entities.UnitType.Manufacturing)
            {
                // Transit cost is estimated based on distance; for now use a placeholder
                // In a full implementation, this would calculate actual destination-based cost
                estimatedTransitCost = ComputeEstimatedTransitCost(unit, building);
            }

            unitSummaries.Add(new SupplyChainUnitSummary
            {
                BuildingUnitId = unit.Id,
                UnitType = unit.UnitType,
                GridX = unit.GridX,
                GridY = unit.GridY,
                Level = unit.Level,
                Status = status,
                IdleTicks = idleTicks,
                FillPercent = Math.Min(100m, Math.Round(fillPercent, 1)),
                ResourceTypeId = unit.ResourceTypeId,
                ProductTypeId = unit.ProductTypeId,
                ResourceOrProductName = resourceOrProductName,
                EstimatedTransitCost = estimatedTransitCost,
            });
        }

        // Build links from unit connection flags
        var links = new List<SupplyChainLink>();
        var linksBySource = new Dictionary<Guid, Guid>();

        foreach (var unit in units)
        {
            // Check each directional link flag
            if (unit.LinkRight)
            {
                var targetUnit = units.FirstOrDefault(u => u.GridX == unit.GridX + 1 && u.GridY == unit.GridY);
                if (targetUnit is not null)
                {
                    links.Add(new SupplyChainLink
                    {
                        FromUnitId = unit.Id,
                        ToUnitId = targetUnit.Id,
                        Direction = "RIGHT",
                        EstimatedTransitCost = ComputeLinkTransitCost(unit, targetUnit, building),
                    });
                }
            }

            if (unit.LinkDown)
            {
                var targetUnit = units.FirstOrDefault(u => u.GridX == unit.GridX && u.GridY == unit.GridY + 1);
                if (targetUnit is not null)
                {
                    links.Add(new SupplyChainLink
                    {
                        FromUnitId = unit.Id,
                        ToUnitId = targetUnit.Id,
                        Direction = "DOWN",
                        EstimatedTransitCost = ComputeLinkTransitCost(unit, targetUnit, building),
                    });
                }
            }

            if (unit.LinkDownRight)
            {
                var targetUnit = units.FirstOrDefault(u => u.GridX == unit.GridX + 1 && u.GridY == unit.GridY + 1);
                if (targetUnit is not null)
                {
                    links.Add(new SupplyChainLink
                    {
                        FromUnitId = unit.Id,
                        ToUnitId = targetUnit.Id,
                        Direction = "DIAGONAL_DR",
                        EstimatedTransitCost = ComputeLinkTransitCost(unit, targetUnit, building),
                    });
                }
            }

            // Add more link directions as needed (LinkUp, LinkLeft, etc.)
        }

        // Compute overall health score
        var healthScore = SupplyChainHealth.Green;
        var healthReason = "All units operating normally";

        if (criticalUnitIds.Any())
        {
            healthScore = SupplyChainHealth.Red;
            healthReason = $"{criticalUnitIds.Count} unit(s) in critical stall";
        }
        else if (warningUnitIds.Any())
        {
            healthScore = SupplyChainHealth.Yellow;
            healthReason = $"{warningUnitIds.Count} unit(s) showing signs of stall";
        }

        return new BuildingSupplyChainDiagram
        {
            BuildingId = building.Id,
            BuildingName = building.Name,
            BuildingType = building.Type,
            Units = unitSummaries,
            Links = links,
            HealthScore = healthScore,
            HealthReason = healthReason,
            CriticalUnitIds = criticalUnitIds,
            WarningUnitIds = warningUnitIds,
        };
    }

    private decimal ComputeEstimatedTransitCost(BuildingUnit unit, Building building)
    {
        // Placeholder: in production this would calculate based on destination city
        // For now, estimate based on unit level
        return unit.Level * 0.5m;
    }

    private decimal ComputeLinkTransitCost(BuildingUnit source, BuildingUnit destination, Building building)
    {
        // Placeholder: actual transit cost would be calculated based on distance and fuel price
        // For now, use a base cost scaled by unit level difference
        return Math.Abs(destination.Level - source.Level) * 0.25m + 0.5m;
    }
}

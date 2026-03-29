using Api.Data.Entities;

namespace Api.Engine.Phases;

/// <summary>
/// Moves resources/products along active unit links within each building.
/// Processing order: top-left to bottom-right within each building so that
/// upstream units push before downstream units are evaluated.
/// Each unit pushes inventory to its outgoing-linked neighbours if they have space.
/// </summary>
public sealed class ResourceMovementPhase : ITickPhase
{
    public string Name => "ResourceMovement";
    public int Order => 400;

    public Task ProcessAsync(TickContext context)
    {
        foreach (var (buildingId, units) in context.UnitsByBuilding)
        {
            // Sort top-left to bottom-right for deterministic push order.
            var sorted = units
                .OrderBy(u => u.GridY)
                .ThenBy(u => u.GridX)
                .ToList();

            foreach (var unit in sorted)
            {
                PushFromUnit(context, buildingId, unit);
            }
        }

        return Task.CompletedTask;
    }

    private static void PushFromUnit(TickContext context, Guid buildingId, BuildingUnit source)
    {
        if (!context.InventoryByUnit.TryGetValue(source.Id, out var inventories))
            return;

        var neighbors = context.GetOutgoingLinkedUnits(source);
        if (neighbors.Count == 0) return;

        foreach (var inv in inventories)
        {
            if (inv.Quantity <= 0m) continue;

            // Distribute evenly among neighbors with free space.
            var eligibleNeighbors = neighbors
                .Where(n => context.GetUnitFreeSpace(n) > 0m)
                .ToList();
            if (eligibleNeighbors.Count == 0) continue;

            var sharePerNeighbor = inv.Quantity / eligibleNeighbors.Count;

            foreach (var neighbor in eligibleNeighbors)
            {
                var space = context.GetUnitFreeSpace(neighbor);
                var transfer = Math.Min(sharePerNeighbor, space);
                transfer = Math.Max(0m, Math.Floor(transfer * 10000m) / 10000m);
                if (transfer <= 0m) continue;

                var moved = context.WithdrawInventory(inv, transfer);
                if (moved.Quantity <= 0m) continue;

                var targetInv = context.GetOrCreateUnitInventory(
                    buildingId, neighbor.Id, inv.ResourceTypeId, inv.ProductTypeId);
                context.AddInventory(targetInv, moved.Quantity, moved.SourcingCostTotal, inv.Quality);

                if (inv.Quantity <= 0m) break;
            }
        }
    }
}

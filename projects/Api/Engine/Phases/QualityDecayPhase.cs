using Api.Data.Entities;

namespace Api.Engine.Phases;

/// <summary>
/// Processes quality decay for perishable product inventory stored in STORAGE units.
///
/// Each tick, inventory.Quality is reduced by <see cref="GameConstants.QualityDecayRatePerTick"/>
/// (0.0005 decimal = 0.05% of the full 0–1 quality range per tick).
/// When quality reaches or falls below zero the entire inventory batch is removed and:
///   1. An <see cref="InventorySpoilageRecord"/> is written to the database.
///   2. A <see cref="LedgerEntry"/> with category <see cref="LedgerCategory.SpoilageLoss"/> is posted.
///
/// Only products whose <see cref="ProductType.IsPerishable"/> flag is true are affected.
/// Only inventory in STORAGE-type building units is decayed (manufacturing/sales buffers are excluded).
///
/// Order = 750, runs before ResearchPhase (800) and after PublicSalesPhase (600).
/// </summary>
public sealed class QualityDecayPhase : ITickPhase
{
    public string Name => "QualityDecay";
    public int Order => 750;

    public Task ProcessAsync(TickContext context)
    {
        var spoiledInventory = new List<Inventory>();
        var spoilageRecords = new List<InventorySpoilageRecord>();
        var ledgerEntries = new List<LedgerEntry>();

        foreach (var (buildingId, units) in context.UnitsByBuilding)
        {
            if (!context.BuildingsById.TryGetValue(buildingId, out var building))
                continue;

            foreach (var unit in units)
            {
                // Only STORAGE units are subject to decay
                if (unit.UnitType != UnitType.Storage)
                    continue;

                if (!context.InventoryByUnit.TryGetValue(unit.Id, out var inventoryList))
                    continue;

                foreach (var inventory in inventoryList)
                {
                    // Only product inventory (not raw resources) can be perishable
                    if (!inventory.ProductTypeId.HasValue)
                        continue;

                    if (!context.ProductTypesById.TryGetValue(inventory.ProductTypeId.Value, out var productType))
                        continue;

                    if (!productType.IsPerishable)
                        continue;

                    // Apply decay
                    inventory.Quality -= GameConstants.QualityDecayRatePerTick;

                    if (inventory.Quality <= 0m)
                    {
                        // Floor quality to zero before recording — QualityAtSpoilage in the audit record
                        // should read exactly 0.0, not a small negative due to floating-point drift.
                        inventory.Quality = 0m;
                        spoiledInventory.Add(inventory);

                        // Estimated loss: total sourcing cost of the spoiled inventory
                        var estimatedLoss = inventory.SourcingCostTotal;

                        spoilageRecords.Add(new InventorySpoilageRecord
                        {
                            Id = Guid.NewGuid(),
                            CompanyId = building.CompanyId,
                            BuildingId = building.Id,
                            BuildingUnitId = unit.Id,
                            ProductTypeId = productType.Id,
                            QuantitySpoiled = inventory.Quantity,
                            QualityAtSpoilage = inventory.Quality,
                            EstimatedLossValue = estimatedLoss,
                            RecordedAtTick = context.GameState.CurrentTick,
                            RecordedAtUtc = DateTime.UtcNow,
                        });

                        ledgerEntries.Add(new LedgerEntry
                        {
                            Id = Guid.NewGuid(),
                            CompanyId = building.CompanyId,
                            BuildingId = building.Id,
                            BuildingUnitId = unit.Id,
                            ProductTypeId = productType.Id,
                            Category = LedgerCategory.SpoilageLoss,
                            Description = $"{inventory.Quantity:F2} units of {productType.Name} spoiled in storage.",
                            Amount = -estimatedLoss,
                            RecordedAtTick = context.GameState.CurrentTick,
                            RecordedAtUtc = DateTime.UtcNow,
                        });
                    }
                }
            }
        }

        // Remove spoiled inventory from context and schedule DB deletions
        foreach (var inv in spoiledInventory)
        {
            if (inv.BuildingUnitId.HasValue && context.InventoryByUnit.TryGetValue(inv.BuildingUnitId.Value, out var unitList))
                unitList.Remove(inv);

            if (context.InventoryByBuilding.TryGetValue(inv.BuildingId, out var buildingList))
                buildingList.Remove(inv);

            context.Db.Inventories.Remove(inv);
        }

        if (spoilageRecords.Count > 0)
            context.Db.InventorySpoilageRecords.AddRange(spoilageRecords);

        if (ledgerEntries.Count > 0)
            context.Db.LedgerEntries.AddRange(ledgerEntries);

        return Task.CompletedTask;
    }
}

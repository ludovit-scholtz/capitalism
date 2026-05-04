using Api.Data.Entities;
using Api.Utilities;

namespace Api.Engine.Phases;

/// <summary>
/// Produces raw materials in MINING units inside MINE buildings.
/// Production rate depends on unit level.
/// Output quality is tied to the mine lot deposit quality and extraction is capped by
/// remaining lot reserves (MaterialQuantity), which are depleted over time.
/// Output is stored in the mining unit's own inventory up to its storage capacity.
/// When a lot transitions from non-zero to zero remaining quantity, a <see cref="MineDepletionRecord"/>
/// is created and a <see cref="PlayerNotification"/> is emitted to the building owner.
/// </summary>
public sealed class MiningPhase : ITickPhase
{
    public string Name => "Mining";
    public int Order => 500;

    public Task ProcessAsync(TickContext context)
    {
        if (!context.BuildingsByType.TryGetValue(BuildingType.Mine, out var mines))
            return Task.CompletedTask;

        foreach (var building in mines)
        {
            if (!context.UnitsByBuilding.TryGetValue(building.Id, out var units))
                continue;

            if (!context.LotsByBuildingId.TryGetValue(building.Id, out var lot))
                continue;

            var lotResourceTypeId = lot.ResourceTypeId;
            if (!lotResourceTypeId.HasValue)
                continue;

            var depositQuality = lot.MaterialQuality;
            var hasFiniteReserve = lot.MaterialQuantity.HasValue;
            var remainingReserve = lot.MaterialQuantity ?? decimal.MaxValue;

            // Snapshot to detect depletion transition this tick.
            var wasNonZeroBefore = hasFiniteReserve && remainingReserve > 0m;

            if (hasFiniteReserve && remainingReserve <= 0m)
                continue;

            // Skip buildings with no power.
            var efficiency = TickContext.GetPowerEfficiency(building);
            if (efficiency <= 0m) continue;

            // Skip buildings suspended for insufficient funds (evaluated by OperatingCostPhase).
            if (building.IsSuspendedForFunds) continue;

            foreach (var unit in units)
            {
                if (unit.UnitType != UnitType.Mining) continue;
                if (context.UnitsUnderUpgrade.Contains(unit.Id)) continue;
                if (!unit.ResourceTypeId.HasValue) continue;
                if (unit.ResourceTypeId.Value != lotResourceTypeId.Value) continue;
                if (hasFiniteReserve && remainingReserve <= 0m) break;

                var production = GameConstants.MiningRate(unit.Level) * efficiency;
                var space = context.GetUnitFreeSpace(unit);
                var actual = Math.Min(production, space);
                if (hasFiniteReserve)
                {
                    actual = Math.Min(actual, remainingReserve);
                }

                if (actual <= 0m) continue;

                var inv = context.GetOrCreateUnitInventory(
                    building.Id, unit.Id, unit.ResourceTypeId, null);
                context.AddInventory(inv, actual, 0m, depositQuality);
                context.RecordUnitResourceHistory(
                    building.Id,
                    unit.Id,
                    unit.ResourceTypeId,
                    null,
                    producedQuantity: actual);

                if (hasFiniteReserve)
                {
                    remainingReserve = Math.Max(0m, remainingReserve - actual);
                }
            }

            if (hasFiniteReserve)
            {
                lot.MaterialQuantity = Math.Max(0m, remainingReserve);

                // Detect depletion transition: lot went from non-zero to zero this tick.
                if (wasNonZeroBefore && lot.MaterialQuantity <= 0m)
                {
                    RecordDepletion(context, building, lot);
                }
            }
        }

        return Task.CompletedTask;
    }

    private static void RecordDepletion(TickContext context, Building building, BuildingLot lot)
    {
        if (!context.CompaniesById.TryGetValue(building.CompanyId, out var company))
            return;

        var resourceName = lot.ResourceTypeId.HasValue
            && context.ResourceTypesById.TryGetValue(lot.ResourceTypeId.Value, out var rt)
            ? rt.Name
            : "Unknown";

        var original = lot.OriginalMaterialQuantity ?? lot.MaterialQuantity ?? 0m;

        // Audit record.
        context.Db.MineDepletionRecords.Add(new MineDepletionRecord
        {
            Id = Guid.NewGuid(),
            LotId = lot.Id,
            BuildingId = building.Id,
            CompanyId = company.Id,
            ResourceTypeId = lot.ResourceTypeId,
            ResourceTypeName = resourceName,
            OriginalQuantity = original,
            DepletedAtTick = context.CurrentTick,
            DepletedAtUtc = DateTime.UtcNow,
        });

        // Player notification.
        PlayerNotificationService.Add(
            context.Db,
            company.PlayerId,
            PlayerNotificationType.MineFullyDepleted,
            $"Mine Depleted: {resourceName}",
            $"Your {resourceName} mine in {building.Name} has been fully extracted. Consider purchasing a new mining lot to maintain production.",
            context.CurrentTick,
            company.Id,
            building.Id);
    }
}

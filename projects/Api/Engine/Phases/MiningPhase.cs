using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Shared.Economy;

namespace Api.Engine.Phases;

/// <summary>
/// Produces raw materials in MINING units inside MINE buildings.
/// Production rate depends on unit level.
/// Output quality is tied to the mine lot deposit quality and extraction is capped by
/// remaining lot reserves (MaterialQuantity), which are depleted over time.
/// Output is stored in the mining unit's own inventory up to its storage capacity.
/// When a lot transitions from non-zero to zero remaining quantity, a <see cref="MineDepletionRecord"/>
/// is created and a <see cref="PlayerNotification"/> is emitted to the building owner.
/// A <see cref="MineExtractionRecord"/> is written each tick for every mine that produced output,
/// enabling the 30-day sparkline chart and depletion forecasting UI.
/// Records older than 90 game days are pruned in this phase.
/// </summary>
public sealed class MiningPhase : ITickPhase
{
    public string Name => "Mining";
    public int Order => 500;

    /// <summary>Game days after which extraction history records are pruned.</summary>
    private const int ExtractionHistoryRetentionDays = 90;

    public async Task ProcessAsync(TickContext context)
    {
        if (!context.BuildingsByType.TryGetValue(BuildingType.Mine, out var mines))
            return;

        var extractionByBuilding = new Dictionary<Guid, (decimal extracted, decimal efficiency, decimal reserve)>();

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
            var originalReserve = lot.OriginalMaterialQuantity ?? lot.MaterialQuantity;
            var previousRatio = MiningScarcityCalculator.ComputeRemainingRatio(remainingReserve, originalReserve);
            var efficiencyFactor = hasFiniteReserve
                ? MiningScarcityCalculator.ComputeEfficiencyFactor(remainingReserve, originalReserve)
                : 1m;

            // Snapshot to detect depletion transition this tick.
            var wasNonZeroBefore = hasFiniteReserve && remainingReserve > 0m;

            if (hasFiniteReserve && remainingReserve <= 0m)
                continue;

            // Skip buildings with no power.
            var efficiency = TickContext.GetPowerEfficiency(building);
            if (efficiency <= 0m) continue;

            // Skip buildings suspended for insufficient funds (evaluated by OperatingCostPhase).
            if (building.IsSuspendedForFunds) continue;

            var totalExtractedThisTick = 0m;

            foreach (var unit in units)
            {
                if (unit.UnitType != UnitType.Mining) continue;
                if (context.UnitsUnderUpgrade.Contains(unit.Id)) continue;
                if (!unit.ResourceTypeId.HasValue) continue;
                if (unit.ResourceTypeId.Value != lotResourceTypeId.Value) continue;
                if (hasFiniteReserve && remainingReserve <= 0m) break;

                var production = GameConstants.MiningRate(unit.Level) * efficiency * efficiencyFactor
                    * context.GetGlobalEventMineEfficiency(building.CityId);
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

                totalExtractedThisTick += actual;

                if (hasFiniteReserve)
                {
                    remainingReserve = Math.Max(0m, remainingReserve - actual);
                }
            }

            if (hasFiniteReserve)
            {
                lot.MaterialQuantity = Math.Max(0m, remainingReserve);
                var currentRatio = MiningScarcityCalculator.ComputeRemainingRatio(lot.MaterialQuantity, originalReserve);

                RecordDepletionThresholdNotifications(context, building, lot, previousRatio, currentRatio);

                // Detect depletion transition: lot went from non-zero to zero this tick.
                if (wasNonZeroBefore && lot.MaterialQuantity <= 0m)
                {
                    RecordDepletion(context, building, lot);
                }
            }

            // Write per-tick extraction record when the mine produced anything.
            if (totalExtractedThisTick > 0m)
            {
                extractionByBuilding[building.Id] = (totalExtractedThisTick, efficiencyFactor, lot.MaterialQuantity ?? 0m);
            }
        }

        // Persist extraction records.
        foreach (var (buildingId, (extracted, efficiency, reserve)) in extractionByBuilding)
        {
            context.Db.MineExtractionRecords.Add(new MineExtractionRecord
            {
                Id = Guid.NewGuid(),
                BuildingId = buildingId,
                Tick = context.CurrentTick,
                ExtractedAmount = extracted,
                EfficiencyPercent = efficiency,
                ReserveRemaining = reserve,
            });
        }

        // Prune extraction records older than 90 game days.
        var pruneBefore = context.CurrentTick - (ExtractionHistoryRetentionDays * GameConstants.TicksPerDay);
        if (pruneBefore > 0)
        {
            await context.Db.MineExtractionRecords
                .Where(r => r.Tick < pruneBefore)
                .ExecuteDeleteAsync();
        }
    }

    private static void RecordDepletionThresholdNotifications(
        TickContext context,
        Building building,
        BuildingLot lot,
        decimal previousRatio,
        decimal currentRatio)
    {
        if (!context.CompaniesById.TryGetValue(building.CompanyId, out var company))
            return;

        var resourceName = lot.ResourceTypeId.HasValue
            && context.ResourceTypesById.TryGetValue(lot.ResourceTypeId.Value, out var rt)
            ? rt.Name
            : "resource";

        if (MiningScarcityCalculator.CrossedDownThreshold(previousRatio, currentRatio, 0.20m))
        {
            PlayerNotificationService.Add(
                context.Db,
                company.PlayerId,
                PlayerNotificationType.MineLowReserveWarning,
                $"Mine Reserve Warning: {resourceName}",
                $"Your mine {building.Name} has fallen below 20% remaining deposit. Remaining: {decimal.Round(currentRatio * 100m, 1):0.#}%.\nPlan replacement lots soon.",
                context.CurrentTick,
                company.Id,
                building.Id);
        }

        if (MiningScarcityCalculator.CrossedDownThreshold(previousRatio, currentRatio, 0.15m)
            && !PlayerNotificationService.HasUnreadDuplicate(
                context.Db,
                company.PlayerId,
                PlayerNotificationType.MineDepleting,
                relatedEntityId: building.Id,
                companyId: company.Id,
                buildingId: building.Id))
        {
            PlayerNotificationService.Add(
                context.Db,
                company.PlayerId,
                PlayerNotificationType.MineDepleting,
                $"Mine depleting: {resourceName}",
                $"Your mine {building.Name} dropped below 15% reserves ({decimal.Round(currentRatio * 100m, 1):0.#}% remaining).",
                context.CurrentTick,
                company.Id,
                building.Id,
                severity: PlayerNotificationSeverity.Warning,
                relatedEntityType: "BUILDING",
                relatedEntityId: building.Id);
        }

        if (MiningScarcityCalculator.CrossedDownThreshold(previousRatio, currentRatio, 0.05m))
        {
            PlayerNotificationService.Add(
                context.Db,
                company.PlayerId,
                PlayerNotificationType.MineCriticalReserveWarning,
                $"Mine Critical Reserve: {resourceName}",
                $"Your mine {building.Name} has fallen below 5% remaining deposit. Remaining: {decimal.Round(currentRatio * 100m, 1):0.#}%.\nProduction efficiency is now critically reduced.",
                context.CurrentTick,
                company.Id,
                building.Id);
        }
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

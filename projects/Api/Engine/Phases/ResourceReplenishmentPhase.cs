using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine.Phases;

/// <summary>
/// Every <see cref="GameConstants.ReplenishmentIntervalTicks"/> game ticks (one game year = 8 760 ticks)
/// a fraction of fully-depleted MINE lot deposits in each city is randomly restored, simulating
/// geological discovery of secondary deposits.
/// <para>
/// For each city whose <see cref="ResourceReplenishmentSchedule.NextReplenishmentTick"/> has arrived:
/// <list type="number">
///   <item>Select 20–30 % of fully-depleted lots (materialQuantity ≤ 0) in the city at random.</item>
///   <item>Restore 10–30 % of <see cref="BuildingLot.OriginalMaterialQuantity"/> on each selected lot.</item>
///   <item>Emit a <see cref="PlayerNotification"/> to the owner of each replenished lot.</item>
///   <item>Advance <see cref="ResourceReplenishmentSchedule.NextReplenishmentTick"/> by <see cref="GameConstants.ReplenishmentIntervalTicks"/>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class ResourceReplenishmentPhase : ITickPhase
{
    public string Name => "ResourceReplenishment";

    /// <summary>Runs after PlayerAlerts (960) so depletion notifications fire first.</summary>
    public int Order => 965;

    public async Task ProcessAsync(TickContext context)
    {
        var tick = context.CurrentTick;

        // Load all schedules that are due (or overdue) this tick.
        var dueSchedules = await context.Db.ResourceReplenishmentSchedules
            .Where(s => s.NextReplenishmentTick <= tick)
            .ToListAsync();

        if (dueSchedules.Count == 0)
            return;

        // Load all lots with resource deposits per city (all, whether depleted or not).
        var cityIds = dueSchedules.Select(s => s.CityId).ToList();
        var mineLots = await context.Db.BuildingLots
            .Where(lot => cityIds.Contains(lot.CityId)
                && lot.ResourceTypeId.HasValue
                && lot.OriginalMaterialQuantity.HasValue
                && lot.OriginalMaterialQuantity > 0m)
            .ToListAsync();

        var lotsByCity = mineLots
            .GroupBy(lot => lot.CityId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var schedule in dueSchedules)
        {
            if (!lotsByCity.TryGetValue(schedule.CityId, out var cityLots))
            {
                // No mine lots in this city — just advance the schedule.
                AdvanceSchedule(schedule);
                continue;
            }

            // Select only fully-depleted lots (≤ 0).
            var depletedLots = cityLots
                .Where(lot => lot.MaterialQuantity.HasValue && lot.MaterialQuantity.Value <= 0m)
                .ToList();

            if (depletedLots.Count == 0)
            {
                AdvanceSchedule(schedule);
                continue;
            }

            // Randomly select 20-30 % of depleted lots.
            var rng = Random.Shared;
            var selectFraction = (double)(GameConstants.ReplenishmentMinLotFraction
                + (decimal)rng.NextDouble() * (GameConstants.ReplenishmentMaxLotFraction - GameConstants.ReplenishmentMinLotFraction));
            var selectCount = Math.Max(1, (int)Math.Ceiling(depletedLots.Count * selectFraction));

            var selected = depletedLots
                .OrderBy(_ => rng.Next())
                .Take(selectCount)
                .ToList();

            foreach (var lot in selected)
            {
                if (!lot.OriginalMaterialQuantity.HasValue || lot.OriginalMaterialQuantity <= 0m)
                    continue;

                // Restore 10-30 % of original deposit.
                var restoreFraction = GameConstants.ReplenishmentMinRestoreFraction
                    + (decimal)rng.NextDouble() * (GameConstants.ReplenishmentMaxRestoreFraction - GameConstants.ReplenishmentMinRestoreFraction);
                var restoreAmount = decimal.Round(lot.OriginalMaterialQuantity.Value * restoreFraction, 2);

                lot.MaterialQuantity = restoreAmount;
                // Also bump ConcurrencyToken so the lot-detail view refreshes.
                lot.ConcurrencyToken = Guid.NewGuid();

                // Notify the owning company (if purchased).
                if (lot.OwnerCompanyId.HasValue
                    && context.CompaniesById.TryGetValue(lot.OwnerCompanyId.Value, out var owner))
                {
                    var resourceName = lot.ResourceTypeId.HasValue
                        && context.ResourceTypesById.TryGetValue(lot.ResourceTypeId.Value, out var rt)
                        ? rt.Name
                        : "Resource";

                    var cityName = context.CitiesById.TryGetValue(schedule.CityId, out var city)
                        ? city.Name
                        : "your city";

                    PlayerNotificationService.Add(
                        context.Db,
                        owner.PlayerId,
                        PlayerNotificationType.MineReplenished,
                        $"Mine Replenished: {resourceName}",
                        $"A secondary {resourceName} deposit has been discovered in {lot.Name} ({cityName}). {restoreAmount:0.##} tonnes are now available for extraction.",
                        tick,
                        owner.Id,
                        lot.BuildingId);
                }
            }

            AdvanceSchedule(schedule);
        }
    }

    private static void AdvanceSchedule(ResourceReplenishmentSchedule schedule)
    {
        schedule.LastReplenishmentTick = schedule.NextReplenishmentTick;
        schedule.NextReplenishmentTick += GameConstants.ReplenishmentIntervalTicks;
    }
}

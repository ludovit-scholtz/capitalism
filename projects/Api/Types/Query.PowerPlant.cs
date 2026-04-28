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
    ///   - fuelCosts      : FUEL_COST ledger entries (absolute value; COAL/GAS plants only)
    ///   - netProfit      : surplusIncome - gridFine - operatingCosts - fuelCosts
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

        // Load installed units so we can compute reserve capacity and constrained output.
        var units = await db.BuildingUnits
            .AsNoTracking()
            .Where(u => u.BuildingId == buildingId)
            .ToListAsync();

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
                    || e.Category == LedgerCategory.EnergyCost
                    || e.Category == LedgerCategory.FuelCost))
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
            var fuelCosts = tickEntries
                .Where(e => e.Category == LedgerCategory.FuelCost)
                .Sum(e => Math.Abs(e.Amount));

            snapshots.Add(new PowerPlantTickSnapshot
            {
                Tick = tick,
                SurplusIncome = surplusIncome,
                GridFine = gridFine,
                OperatingCosts = opCosts,
                FuelCosts = fuelCosts,
                NetProfit = surplusIncome - gridFine - opCosts - fuelCosts,
            });
        }

        // ── Fuel reserve capacity and constrained-output analytics ─────────────
        var isThermal = GameConstants.IsThermalPlant(building.PowerPlantType);

        var fuelPurchaseUnits = units
            .Where(u => u.UnitType == UnitType.FuelPurchase)
            .ToList();

        var maxFuelReserveMwh = isThermal
            ? fuelPurchaseUnits.Sum(u => GameConstants.FuelReserveCapacityPerUnitLevel * u.Level)
            : 0m;

        var fuelPurchaseCapacityMwhPerTick = isThermal
            ? fuelPurchaseUnits.Sum(u => GameConstants.FuelPurchaseBoostMwPerLevel * u.Level)
            : 0m;

        var energyProducingCapacityMw = isThermal
            ? units
                .Where(u => u.UnitType == UnitType.EnergyProducing)
                .Sum(u => GameConstants.EnergyProducingBoostMwPerLevel * u.Level)
            : 0m;

        // Constrained output: how much ENERGY_PRODUCING capacity is unused because the fuel
        // reserve is too low.  This is an instantaneous estimate based on current reserve.
        var fuelConstrainedOutputMw = isThermal && energyProducingCapacityMw > 0m
            ? Math.Max(0m, energyProducingCapacityMw - building.FuelReserveMwh)
            : 0m;

        var fuelReservePercent = maxFuelReserveMwh > 0m
            ? (int)Math.Round(building.FuelReserveMwh / maxFuelReserveMwh * 100m)
            : 0;
        fuelReservePercent = Math.Clamp(fuelReservePercent, 0, 100);

        var fuelTypeLabel = building.PowerPlantType switch
        {
            PowerPlantType.Coal => "Coal",
            PowerPlantType.Gas  => "Natural Gas",
            _                   => string.Empty,
        };

        var fuelCostPerMwhEur = isThermal
            ? GameConstants.FuelCostPerMwhForPlantType(building.PowerPlantType)
            : 0m;

        return new PowerPlantAnalytics
        {
            BuildingId = building.Id,
            BuildingName = building.Name,
            PlantType = building.PowerPlantType ?? "UNKNOWN",
            CurrentOutputMw = building.PowerOutput > 0m
                ? building.PowerOutput.Value
                : GameConstants.DefaultPowerOutputMw(building.PowerPlantType),
            DispatchTargetPercent = building.DispatchTargetPercent,
            FuelReserveMwh = building.FuelReserveMwh,
            MaxFuelReserveMwh = maxFuelReserveMwh,
            FuelReservePercent = fuelReservePercent,
            FuelPurchaseCapacityMwhPerTick = fuelPurchaseCapacityMwhPerTick,
            EnergyProducingCapacityMw = energyProducingCapacityMw,
            FuelConstrainedOutputMw = fuelConstrainedOutputMw,
            FuelTypeLabel = fuelTypeLabel,
            FuelCostPerMwhEur = fuelCostPerMwhEur,
            DataFromTick = windowStart,
            DataToTick = currentTick,
            TotalSurplusIncome = snapshots.Sum(s => s.SurplusIncome),
            TotalGridFines = snapshots.Sum(s => s.GridFine),
            TotalOperatingCosts = snapshots.Sum(s => s.OperatingCosts),
            TotalFuelCosts = snapshots.Sum(s => s.FuelCosts),
            TotalNetProfit = snapshots.Sum(s => s.NetProfit),
            Timeline = snapshots,
        };
    }
}

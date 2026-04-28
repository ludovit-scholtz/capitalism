using Api.Data.Entities;
using Api.Utilities;

namespace Api.Engine.Phases;

/// <summary>
/// Runs before <see cref="PowerDistributionPhase"/> (order 10) to charge thermal power plants
/// for fuel procurement and update their fuel reserves.
///
/// Strategy:
/// <list type="bullet">
///   <item>Only COAL and GAS power plants have fuel reserves (thermal plants).</item>
///   <item>Each FUEL_PURCHASE unit procures up to <see cref="GameConstants.FuelPurchaseBoostMwPerLevel"/> MWh
///         of fuel per level per tick, scaled by the plant's <see cref="Building.DispatchTargetPercent"/>.</item>
///   <item>Procurement is capped by the plant's remaining reserve capacity (to prevent hoarding).</item>
///   <item>Fuel cost = procured MWh × <see cref="GameConstants.FuelCostPerMwhBase"/> × <c>City.FuelPriceIndex</c>
///         × FX rate, debited from the building's bank account.</item>
///   <item>If the bank account cannot cover the full cost, fuel is procured at the affordable quantity
///         (the plant receives partial fill rather than nothing, allowing gradual build-up).</item>
///   <item>A <see cref="LedgerCategory.FuelCost"/> ledger entry is recorded for every successful procurement.</item>
/// </list>
/// </summary>
public sealed class FuelProcurementPhase : ITickPhase
{
    public string Name => "FuelProcurement";

    /// <summary>
    /// Must run before <see cref="PowerDistributionPhase"/> (order 10) so that the
    /// fuel reserve is updated before output calculations use it.
    /// </summary>
    public int Order => 9;

    public Task ProcessAsync(TickContext context)
    {
        if (!context.BuildingsByType.TryGetValue(BuildingType.PowerPlant, out var powerPlants))
            return Task.CompletedTask;

        foreach (var plant in powerPlants)
        {
            // Only thermal (COAL/GAS) plants use fuel reserves.
            if (!GameConstants.IsThermalPlant(plant.PowerPlantType))
                continue;

            if (!context.UnitsByBuilding.TryGetValue(plant.Id, out var units))
                continue;

            var fuelPurchaseUnits = units
                .Where(u => u.UnitType == UnitType.FuelPurchase)
                .ToList();

            if (fuelPurchaseUnits.Count == 0)
                continue;

            if (!context.CompaniesById.TryGetValue(plant.CompanyId, out var company))
                continue;

            if (!context.CitiesById.TryGetValue(plant.CityId, out var city))
                continue;

            // Total procurement capacity per tick (MWh), scaled by dispatch target.
            var dispatchFactor = Math.Clamp(plant.DispatchTargetPercent, 0, 100) / 100m;
            var maxProcurementMwh = fuelPurchaseUnits
                .Sum(u => GameConstants.FuelPurchaseBoostMwPerLevel * u.Level)
                * dispatchFactor;

            if (maxProcurementMwh <= 0m)
                continue;

            // Reserve capacity cap: prevents unbounded hoarding.
            var maxReserveMwh = fuelPurchaseUnits
                .Sum(u => GameConstants.FuelReserveCapacityPerUnitLevel * u.Level);

            var remainingCapacity = maxReserveMwh - plant.FuelReserveMwh;
            if (remainingCapacity <= 0m)
                continue; // Reserve is already full.

            // Actual procurement = min(what we can procure, how much space remains).
            var targetProcurementMwh = Math.Min(maxProcurementMwh, remainingCapacity);

            // Fuel cost in local city currency — GAS costs more per MWh than COAL.
            var fxRate = context.GetCityFxRate(city);
            var costPerMwh = GameConstants.FuelCostPerMwhForPlantType(plant.PowerPlantType) * city.FuelPriceIndex * fxRate;
            var totalCost = decimal.Round(targetProcurementMwh * costPerMwh, 2, MidpointRounding.AwayFromZero);

            // Find the funding bank account.
            var bankAccount = context.GetBuildingFundingAccount(plant);
            if (bankAccount is null)
                continue;

            // If the account cannot cover the full cost, procure a proportionally smaller batch.
            decimal actualProcurementMwh;
            decimal actualCost;

            if (bankAccount.Balance >= totalCost)
            {
                actualProcurementMwh = targetProcurementMwh;
                actualCost = totalCost;
            }
            else if (bankAccount.Balance > 0m && costPerMwh > 0m)
            {
                // Afford as much fuel as possible given available balance.
                actualProcurementMwh = decimal.Round(bankAccount.Balance / costPerMwh, 4, MidpointRounding.AwayFromZero);
                actualProcurementMwh = Math.Min(actualProcurementMwh, targetProcurementMwh);
                actualCost = decimal.Round(actualProcurementMwh * costPerMwh, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                continue; // No funds at all.
            }

            if (actualProcurementMwh <= 0m || actualCost <= 0m)
                continue;

            // Debit the bank account and update the fuel reserve.
            bankAccount.Balance -= actualCost;
            plant.FuelReserveMwh += actualProcurementMwh;

            var fuelTypeName = plant.PowerPlantType == Data.Entities.PowerPlantType.Gas ? "Natural Gas" : "Coal";
            context.Db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                BuildingId = plant.Id,
                BankAccountId = bankAccount.Id,
                Category = LedgerCategory.FuelCost,
                Description = $"{fuelTypeName} procurement: {actualProcurementMwh:F1} MWh @ {costPerMwh:F2} {city.CurrencyCode}/MWh (dispatch {plant.DispatchTargetPercent}%)",
                Amount = -actualCost,
                RecordedAtTick = context.CurrentTick,
                RecordedAtUtc = DateTime.UtcNow,
            });
        }

        return Task.CompletedTask;
    }
}

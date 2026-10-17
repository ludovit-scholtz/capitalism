using Api.Data.Entities;
using Api.Utilities;
using Shared.Economy;

namespace Api.Engine.Phases;

/// <summary>
/// Recomputes every city's labor market at the end of the tick from the city population and the labor hours that all
/// active units require. The stored wage index is read by the next tick's cost phases, so wage pressure always lags the
/// demand that caused it by one tick (no circular dependency inside a tick).
/// </summary>
public sealed class LaborMarketPhase : ITickPhase
{
    public string Name => "LaborMarket";
    public int Order => 1090;

    public Task ProcessAsync(TickContext context)
    {
        var demandByCity = new Dictionary<Guid, decimal>();
        var weightedSalaryByCity = new Dictionary<Guid, decimal>();
        var employers = new List<(Building Building, decimal SalaryMultiplier)>();
        var idleBuildings = new List<Building>();

        foreach (var building in context.BuildingsById.Values)
        {
            // Buildings suspended for funds still want workers: dropping them would lower wages and let them resume,
            // which would raise wages again next tick.
            if (!context.UnitsByBuilding.TryGetValue(building.Id, out var units) || units.Count == 0)
            {
                idleBuildings.Add(building);
                continue;
            }

            var efficiency = TickContext.GetPowerEfficiency(building);
            if (efficiency <= 0m)
            {
                idleBuildings.Add(building);
                continue;
            }

            var salarySettings = context.CitySalarySettingsByCompany.GetValueOrDefault(building.CompanyId, []);
            var salaryMultiplier = CompanyEconomyCalculator.GetSalaryMultiplier(salarySettings, building.CityId);

            var hours = 0m;
            foreach (var unit in units)
            {
                // Same discount OperatingCostPhase applies to units under upgrade, so demand matches billed hours.
                var upgradeMultiplier = context.UnitsUnderUpgrade.Contains(unit.Id) ? 0.5m : 1m;
                hours += CompanyEconomyCalculator.GetBaseUnitLaborHours(unit.UnitType, unit.Level) * efficiency * upgradeMultiplier;
            }

            if (hours <= 0m)
            {
                idleBuildings.Add(building);
                continue;
            }

            employers.Add((building, salaryMultiplier));

            // A striking workforce is not hired and not paid, so it neither demands labor nor shapes the city's average pay.
            if (context.IsOnStrike(building))
            {
                continue;
            }

            demandByCity[building.CityId] = demandByCity.GetValueOrDefault(building.CityId) + hours;
            weightedSalaryByCity[building.CityId] = weightedSalaryByCity.GetValueOrDefault(building.CityId) + (hours * salaryMultiplier);
        }

        foreach (var city in context.CitiesById.Values)
        {
            var demand = demandByCity.GetValueOrDefault(city.Id);
            var state = LaborMarketCalculator.Compute(city.Population, demand);
            var averageSalary = demand > 0m
                ? decimal.Round(weightedSalaryByCity.GetValueOrDefault(city.Id) / demand, 4, MidpointRounding.AwayFromZero)
                : 1m;

            if (!context.LaborMarketsByCity.TryGetValue(city.Id, out var market))
            {
                market = new CityLaborMarket { CityId = city.Id };
                context.Db.CityLaborMarkets.Add(market);
                context.LaborMarketsByCity[city.Id] = market;
            }

            market.LaborPool = state.LaborPool;
            market.LaborDemand = decimal.Round(state.LaborDemand, 4, MidpointRounding.AwayFromZero);
            market.Utilization = state.Utilization;
            market.UnemploymentRate = state.UnemploymentRate;
            market.WageIndex = state.WageIndex;
            market.StaffingFillRate = state.StaffingFillRate;
            market.AverageSalaryMultiplier = averageSalary;
            market.UpdatedAtTick = context.CurrentTick;
        }

        foreach (var (building, salaryMultiplier) in employers)
        {
            if (!context.LaborMarketsByCity.TryGetValue(building.CityId, out var market))
            {
                continue;
            }

            var payRatio = LaborMarketCalculator.ComputePayRatio(salaryMultiplier, market.AverageSalaryMultiplier);
            building.StaffingFactor = LaborMarketCalculator.ComputeCompanyStaffing(market.StaffingFillRate, market.Utilization, payRatio);
            UpdateStrike(context.CurrentTick, building, payRatio);
        }

        // Buildings that do not hire this tick (no units, no power) get a clean slate; a running strike still ends on time.
        foreach (var building in idleBuildings)
        {
            building.StaffingFactor = 1m;
            if (building.StrikeUntilTick.HasValue && context.CurrentTick >= building.StrikeUntilTick.Value)
            {
                building.StrikeStartedAtTick = null;
                building.StrikeUntilTick = null;
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// A running strike ends when its time is up or the company has fixed its pay; otherwise a building whose pay trails the
    /// city average may walk out, decided by a deterministic roll of (tick, building) so it is reproducible from game state.
    /// </summary>
    private static void UpdateStrike(long tick, Building building, decimal payRatio)
    {
        if (building.StrikeUntilTick.HasValue)
        {
            if (LaborMarketCalculator.ShouldStrikeEnd(tick, building.StrikeUntilTick.Value, payRatio))
            {
                building.StrikeStartedAtTick = null;
                building.StrikeUntilTick = null;
            }

            return;
        }

        var probability = LaborMarketCalculator.ComputeStrikeProbability(payRatio);
        if (probability > 0m && LaborMarketCalculator.Roll(tick, building.Id, salt: 1) < (double)probability)
        {
            var duration = LaborMarketCalculator.ComputeStrikeDurationTicks(LaborMarketCalculator.Roll(tick, building.Id, salt: 2));
            building.StrikeStartedAtTick = tick;
            building.StrikeUntilTick = tick + duration;
        }
    }
}

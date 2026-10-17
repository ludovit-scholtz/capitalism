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

        foreach (var building in context.BuildingsById.Values)
        {
            if (building.IsSuspendedForFunds
                || !context.UnitsByBuilding.TryGetValue(building.Id, out var units)
                || units.Count == 0)
            {
                continue;
            }

            var efficiency = TickContext.GetPowerEfficiency(building);
            if (efficiency <= 0m)
            {
                continue;
            }

            var salarySettings = context.CitySalarySettingsByCompany.GetValueOrDefault(building.CompanyId, []);
            var salaryMultiplier = CompanyEconomyCalculator.GetSalaryMultiplier(salarySettings, building.CityId);

            var hours = 0m;
            foreach (var unit in units)
            {
                hours += CompanyEconomyCalculator.GetBaseUnitLaborHours(unit.UnitType, unit.Level) * efficiency;
            }

            if (hours <= 0m)
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

        return Task.CompletedTask;
    }
}

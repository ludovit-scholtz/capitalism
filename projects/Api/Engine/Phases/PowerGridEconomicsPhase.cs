using Api.Data.Entities;

namespace Api.Engine.Phases;

/// <summary>
/// Applies city-level grid economics each tick: power plant operators earn
/// surplus-sale income when the city grid has excess supply, and pay government fines
/// when the city has a power shortage.
///
/// Income and fines are proportional to each plant's share of total effective capacity.
/// Effective capacity = rated output + POWER_GENERATION unit boosts + BATTERY_STORAGE
/// smoothing buffer (battery represents stored energy dispatched to fill gaps).
/// Both amounts are settled through the plant's assigned bank account when one exists;
/// otherwise company cash is used as a legacy fallback.
///
/// Ledger categories:
///   GRID_SURPLUS_INCOME – positive amount (income) when supply &gt; demand
///   GRID_FINE           – negative amount (expense) when supply &lt; demand
///
/// This phase runs immediately after PowerDistributionPhase (order 10) so that the
/// supply/demand balance computed there is already available via building.PowerStatus.
/// </summary>
public sealed class PowerGridEconomicsPhase : ITickPhase
{
    public string Name => "PowerGridEconomics";

    /// <summary>
    /// Must run after <see cref="PowerDistributionPhase"/> (order 10) and before
    /// <see cref="OperatingCostPhase"/> (order 450).
    /// </summary>
    public int Order => 15;

    public Task ProcessAsync(TickContext context)
    {
        var buildingsByCity = context.BuildingsById.Values
            .GroupBy(b => b.CityId);

        foreach (var cityGroup in buildingsByCity)
        {
            var cityId = cityGroup.Key;
            var buildings = cityGroup.ToList();

            var powerPlants = buildings
                .Where(b => b.Type == BuildingType.PowerPlant)
                .ToList();

            if (powerPlants.Count == 0)
                continue;

            if (!context.CitiesById.TryGetValue(cityId, out var city))
                continue;

            context.WeatherByCity.TryGetValue(cityId, out var weather);

            // Compute each plant's raw weather-adjusted output (same logic as PowerDistributionPhase).
            // Smoothing-only units (BATTERY_STORAGE, ENERGY_STORAGE) also count for economics —
            // they represent stored energy being dispatched to fill gaps.
            var plantOutputs = powerPlants
                .Select(plant =>
                {
                    var baseOutput = plant.PowerOutput > 0m
                        ? plant.PowerOutput.Value
                        : GameConstants.DefaultPowerOutputMw(plant.PowerPlantType);

                    // POWER_GENERATION and BATTERY_STORAGE boosts are tied to the plant's rated
                    // capacity and are weather-scaled alongside the base output for SOLAR/WIND.
                    if (context.UnitsByBuilding.TryGetValue(plant.Id, out var plantUnits))
                    {
                        baseOutput += plantUnits
                            .Where(u => u.UnitType == UnitType.PowerGeneration)
                            .Sum(u => GameConstants.PowerGenerationUnitBoostMwPerLevel * u.Level);

                        // BATTERY_STORAGE dispatches stored energy during gaps; treated as part of
                        // the plant's effective rated output for economic share calculations.
                        baseOutput += plantUnits
                            .Where(u => u.UnitType == UnitType.BatteryStorage)
                            .Sum(u => GameConstants.BatterySmoothingMwPerLevel * u.Level);
                    }

                    var factor = plant.PowerPlantType switch
                    {
                        PowerPlantType.Solar => weather is not null ? weather.SolarPercent / 100m : 1m,
                        PowerPlantType.Wind  => weather is not null ? weather.WindPercent  / 100m : 1m,
                        _                    => 1m,
                    };

                    // Weather scaling applies only to the plant's rated capacity, POWER_GENERATION,
                    // and BATTERY_STORAGE. All other unit types are weather-independent.
                    var weatherScaledOutput = baseOutput * factor;

                    if (context.UnitsByBuilding.TryGetValue(plant.Id, out var allPlantUnits))
                    {
                        // FUEL_PURCHASE, WATER_TURBINE, and ENERGY_PRODUCING are weather-independent:
                        // their contribution must NOT be scaled by the plant's solar/wind factor.
                        weatherScaledOutput += allPlantUnits
                            .Where(u => u.UnitType == UnitType.FuelPurchase)
                            .Sum(u => GameConstants.FuelPurchaseBoostMwPerLevel * u.Level);

                        weatherScaledOutput += allPlantUnits
                            .Where(u => u.UnitType == UnitType.WaterTurbine)
                            .Sum(u => GameConstants.WaterTurbineBoostMwPerLevel * u.Level);

                        weatherScaledOutput += allPlantUnits
                            .Where(u => u.UnitType == UnitType.EnergyProducing)
                            .Sum(u => GameConstants.EnergyProducingBoostMwPerLevel * u.Level);

                        // ENERGY_STORAGE smoothing buffer: same treatment as BATTERY_STORAGE
                        // but added after weather scaling since it models a separate mechanical store.
                        weatherScaledOutput += allPlantUnits
                            .Where(u => u.UnitType == UnitType.EnergyStorage)
                            .Sum(u => GameConstants.EnergyStorageSmoothingMwPerLevel * u.Level);

                        // WIND_TURBINE units always scale by current wind percentage regardless of
                        // the plant's primary fuel type.
                        var windPercent = weather is not null ? weather.WindPercent / 100m : 0.5m;
                        weatherScaledOutput += allPlantUnits
                            .Where(u => u.UnitType == UnitType.WindTurbine)
                            .Sum(u => GameConstants.WindTurbineBoostMwPerLevel * u.Level * windPercent);
                    }

                    return (Plant: plant, OutputMw: weatherScaledOutput);
                })
                .ToList();

            var totalSupplyMw = plantOutputs.Sum(p => p.OutputMw);

            var consumers = buildings.Where(b => b.Type != BuildingType.PowerPlant).ToList();
            var totalDemandMw = consumers.Sum(b => b.PowerConsumption);

            if (totalDemandMw == 0m && totalSupplyMw == 0m)
                continue;

            var surplusMw = totalSupplyMw - totalDemandMw;
            var shortageMw = totalDemandMw - totalSupplyMw;

            foreach (var (plant, outputMw) in plantOutputs)
            {
                if (!context.CompaniesById.TryGetValue(plant.CompanyId, out var company))
                    continue;

                // Each plant's share of economics is proportional to its output.
                var capacityShare = totalSupplyMw > 0m
                    ? outputMw / totalSupplyMw
                    : 1m / powerPlants.Count;

                BankAccount? bankAccount = plant.BankAccountId.HasValue
                    && context.BankAccountsById.TryGetValue(plant.BankAccountId.Value, out var ba)
                    ? ba
                    : null;

                if (surplusMw > 0m)
                {
                    ApplySurplusIncome(context, plant, company, bankAccount, city, surplusMw, capacityShare);
                }
                else if (shortageMw > 0m)
                {
                    ApplyGridFine(context, plant, company, bankAccount, city, shortageMw, capacityShare);
                }
            }
        }

        return Task.CompletedTask;
    }

    private static void ApplySurplusIncome(
        TickContext context,
        Building plant,
        Company company,
        BankAccount? bankAccount,
        City city,
        decimal surplusMw,
        decimal capacityShare)
    {
        var income = decimal.Round(
            surplusMw * GameConstants.GridSurplusIncomePerMwTick * capacityShare,
            2,
            MidpointRounding.AwayFromZero);

        if (income <= 0m)
            return;

        if (bankAccount is not null)
        {
            bankAccount.Balance += income;
        }
        else
        {
            var fundingAccount = context.GetCompanyFundingAccount(company.Id, city.CurrencyCode);
            if (fundingAccount is not null)
            {
                fundingAccount.Balance += income;
            }
        }

        context.Db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            BuildingId = plant.Id,
            Category = LedgerCategory.GridSurplusIncome,
            Description = $"Grid surplus income: {surplusMw:F1} MW surplus × {capacityShare:P0} share",
            Amount = income,
            RecordedAtTick = context.CurrentTick,
            RecordedAtUtc = DateTime.UtcNow,
        });
    }

    private static void ApplyGridFine(
        TickContext context,
        Building plant,
        Company company,
        BankAccount? bankAccount,
        City city,
        decimal shortageMw,
        decimal capacityShare)
    {
        var fine = decimal.Round(
            shortageMw * GameConstants.GridFinePerMwTick * capacityShare,
            2,
            MidpointRounding.AwayFromZero);

        if (fine <= 0m)
            return;

        if (bankAccount is not null)
        {
            bankAccount.Balance -= fine;
        }
        else
        {
            var fundingAccount = context.GetCompanyFundingAccount(company.Id, city.CurrencyCode);
            if (fundingAccount is not null)
            {
                fundingAccount.Balance -= fine;
            }
        }

        context.Db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            BuildingId = plant.Id,
            Category = LedgerCategory.GridFine,
            Description = $"Grid shortage fine: {shortageMw:F1} MW shortage × {capacityShare:P0} share",
            Amount = -fine,
            RecordedAtTick = context.CurrentTick,
            RecordedAtUtc = DateTime.UtcNow,
        });
    }
}

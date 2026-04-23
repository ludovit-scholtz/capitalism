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
            // BATTERY_STORAGE smoothing is excluded here because it is not real generation —
            // it only buffers status thresholds. Economics use the raw generation only.
            var plantOutputs = powerPlants
                .Select(plant =>
                {
                    var baseOutput = plant.PowerOutput > 0m
                        ? plant.PowerOutput.Value
                        : GameConstants.DefaultPowerOutputMw(plant.PowerPlantType);

                    if (context.UnitsByBuilding.TryGetValue(plant.Id, out var plantUnits))
                    {
                        baseOutput += plantUnits
                            .Where(u => u.UnitType == UnitType.PowerGeneration)
                            .Sum(u => GameConstants.PowerGenerationUnitBoostMwPerLevel * u.Level);

                        // BATTERY_STORAGE smoothing also counts for economics —
                        // it represents stored energy being dispatched to fill gaps.
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
                    return (Plant: plant, OutputMw: baseOutput * factor);
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
            company.Cash += income;
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
            company.Cash -= fine;
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

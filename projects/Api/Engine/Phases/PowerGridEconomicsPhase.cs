using Api.Data.Entities;

namespace Api.Engine.Phases;

/// <summary>
/// Applies city-level grid economics each tick: power plant operators earn
/// surplus-sale income when the city grid has excess supply, and pay government fines
/// when the city has a power shortage.
///
/// Income and fines are proportional to each plant's share of total effective capacity.
/// The effective output values are taken from <see cref="TickContext.PlantEffectiveOutputMwById"/>
/// which are populated by <see cref="PowerDistributionPhase"/> to avoid re-evaluating
/// fuel-reserve-gated units after reserves have been consumed.
///
/// Ledger categories:
///   GRID_SURPLUS_INCOME -- positive amount (income) when supply > demand
///   GRID_FINE           -- negative amount (expense) when supply < demand
///
/// This phase runs immediately after PowerDistributionPhase (order 10).
/// </summary>
public sealed class PowerGridEconomicsPhase : ITickPhase
{
    public string Name => "PowerGridEconomics";
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

            // Use the authoritative per-plant outputs stored by PowerDistributionPhase.
            // These already account for fuel reserve gating, dispatch target, and weather.
            var plantOutputs = powerPlants
                .Select(plant =>
                {
                    var outputMw = context.PlantEffectiveOutputMwById.TryGetValue(plant.Id, out var mw)
                        ? mw
                        : 0m;
                    return (Plant: plant, OutputMw: outputMw);
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

                var capacityShare = totalSupplyMw > 0m
                    ? outputMw / totalSupplyMw
                    : 1m / powerPlants.Count;

                BankAccount? bankAccount = plant.BankAccountId.HasValue
                    && context.BankAccountsById.TryGetValue(plant.BankAccountId.Value, out var ba)
                    ? ba
                    : null;

                if (surplusMw > 0m)
                    ApplySurplusIncome(context, plant, company, bankAccount, city, surplusMw, capacityShare);
                else if (shortageMw > 0m)
                    ApplyGridFine(context, plant, company, bankAccount, city, shortageMw, capacityShare);
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
            2, MidpointRounding.AwayFromZero);

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
                fundingAccount.Balance += income;
        }

        context.Db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            BuildingId = plant.Id,
            Category = LedgerCategory.GridSurplusIncome,
            Description = $"Grid surplus income: {surplusMw:F1} MW surplus x {capacityShare:P0} share",
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
            2, MidpointRounding.AwayFromZero);

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
                fundingAccount.Balance -= fine;
        }

        context.Db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            BuildingId = plant.Id,
            Category = LedgerCategory.GridFine,
            Description = $"Grid shortage fine: {shortageMw:F1} MW shortage x {capacityShare:P0} share",
            Amount = -fine,
            RecordedAtTick = context.CurrentTick,
            RecordedAtUtc = DateTime.UtcNow,
        });
    }
}

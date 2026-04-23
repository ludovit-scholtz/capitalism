using Api.Data.Entities;
using Api.Utilities;

namespace Api.Engine.Phases;

/// <summary>
/// Applies per-tick labor and energy costs for active units in powered buildings.
/// If a building has an assigned bank account, costs are debited from that account and
/// the building is suspended for the tick when the balance is insufficient.
/// Buildings without an assigned account fall back to company cash (legacy path) but
/// receive a MISSING_BANK_ACCOUNT advisory flag so the UI can guide the player.
/// </summary>
public sealed class OperatingCostPhase : ITickPhase
{
    public string Name => "OperatingCosts";
    public int Order => 450;

    public Task ProcessAsync(TickContext context)
    {
        var maxCompanyAssetValue = context.CompaniesById.Keys
            .Select(context.GetCompanyAssetValue)
            .DefaultIfEmpty(0m)
            .Max();

        foreach (var building in context.BuildingsById.Values)
        {
            if (!context.UnitsByBuilding.TryGetValue(building.Id, out var units) || units.Count == 0)
            {
                continue;
            }

            var efficiency = TickContext.GetPowerEfficiency(building);
            if (efficiency <= 0m)
            {
                continue;
            }

            if (!context.CompaniesById.TryGetValue(building.CompanyId, out var company)
                || !context.CitiesById.TryGetValue(building.CityId, out var city))
            {
                continue;
            }

            var salarySettings = context.CitySalarySettingsByCompany.GetValueOrDefault(company.Id, []);
            var salaryMultiplier = CompanyEconomyCalculator.GetSalaryMultiplier(salarySettings, city.Id);
            var hourlyWage = CompanyEconomyCalculator.GetEffectiveHourlyWage(city, salaryMultiplier);
            var companyAssetValue = context.GetCompanyAssetValue(company.Id);
            var overheadRate = CompanyEconomyCalculator.ComputeAdministrationOverheadRate(
                company,
                companyAssetValue,
                maxCompanyAssetValue,
                context.CurrentTick);

            // ── Calculate total operating cost for this building ──
            var totalBuildingCost = 0m;
            var unitCosts = new List<(BuildingUnit Unit, decimal Labor, decimal Energy)>();

            foreach (var unit in units)
            {
                var baseLaborHours = CompanyEconomyCalculator.GetBaseUnitLaborHours(unit.UnitType, unit.Level) * efficiency;
                var baseEnergyMwh = CompanyEconomyCalculator.GetBaseUnitEnergyMwh(unit.UnitType, unit.Level) * efficiency;

                if (baseLaborHours <= 0m && baseEnergyMwh <= 0m)
                {
                    continue;
                }

                var laborCost = decimal.Round(
                    baseLaborHours * hourlyWage * (unit.UnitType == UnitType.Manufacturing ? 1m + overheadRate : 1m),
                    2,
                    MidpointRounding.AwayFromZero);
                var energyCost = decimal.Round(
                    baseEnergyMwh * GameConstants.EnergyPricePerMwh,
                    2,
                    MidpointRounding.AwayFromZero);

                var upgradeMultiplier = context.UnitsUnderUpgrade.Contains(unit.Id) ? 0.5m : 1m;
                laborCost = decimal.Round(laborCost * upgradeMultiplier, 2, MidpointRounding.AwayFromZero);
                energyCost = decimal.Round(energyCost * upgradeMultiplier, 2, MidpointRounding.AwayFromZero);

                totalBuildingCost += laborCost + energyCost;
                unitCosts.Add((unit, laborCost, energyCost));
            }

            if (unitCosts.Count == 0)
            {
                continue;
            }

            // ── Bank account check ──
            BankAccount? bankAccount = building.BankAccountId.HasValue
                && context.BankAccountsById.TryGetValue(building.BankAccountId.Value, out var ba)
                ? ba
                : null;

            if (bankAccount is not null)
            {
                // Building has an assigned bank account — enforce funding from it.
                if (bankAccount.Balance < totalBuildingCost)
                {
                    // Insufficient funds: suspend the building for this tick.
                    building.IsSuspendedForFunds = true;
                    building.SuspendedReason = $"INSUFFICIENT_FUNDS:{totalBuildingCost:F2}";
                    continue;
                }

                // Sufficient funds: debit from the bank account and reset any previous suspension.
                bankAccount.Balance -= totalBuildingCost;
                building.IsSuspendedForFunds = false;
                building.SuspendedReason = null;
            }
            else
            {
                // No bank account assigned: legacy path — use company cash.
                // Set advisory flag (not a hard suspension) so the frontend can prompt setup.
                building.IsSuspendedForFunds = false;
                building.SuspendedReason = "MISSING_BANK_ACCOUNT";
            }

            // ── Debit individual unit costs and record ledger entries ──
            foreach (var (unit, laborCost, energyCost) in unitCosts)
            {
                if (laborCost > 0m)
                {
                    if (bankAccount is null)
                    {
                        company.Cash -= laborCost;
                    }
                    context.Db.LedgerEntries.Add(new LedgerEntry
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = company.Id,
                        BuildingId = building.Id,
                        BuildingUnitId = unit.Id,
                        Category = LedgerCategory.LaborCost,
                        Description = $"Operating labor for {unit.UnitType}",
                        Amount = -laborCost,
                        RecordedAtTick = context.CurrentTick,
                        RecordedAtUtc = DateTime.UtcNow,
                    });
                }

                if (energyCost > 0m)
                {
                    if (bankAccount is null)
                    {
                        company.Cash -= energyCost;
                    }
                    context.Db.LedgerEntries.Add(new LedgerEntry
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = company.Id,
                        BuildingId = building.Id,
                        BuildingUnitId = unit.Id,
                        Category = LedgerCategory.EnergyCost,
                        Description = $"Operating energy for {unit.UnitType}",
                        Amount = -energyCost,
                        RecordedAtTick = context.CurrentTick,
                        RecordedAtUtc = DateTime.UtcNow,
                    });
                }
            }
        }

        return Task.CompletedTask;
    }
}

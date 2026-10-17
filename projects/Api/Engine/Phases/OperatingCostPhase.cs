using Api.Data.Entities;
using Api.Utilities;

namespace Api.Engine.Phases;

/// <summary>
/// Applies per-tick labor and energy costs for active units in powered buildings.
/// Runs at Order 50 — before all production phases — so that the
/// <see cref="Building.IsSuspendedForFunds"/> flag is current when
/// ManufacturingPhase, PublicSalesPhase, PurchasingPhase, and similar phases
/// decide whether to process a building that tick.
/// If a building has an assigned bank account, costs are debited from that account and
/// the building is suspended for the tick when the balance is insufficient.
/// Buildings without an assigned account fall back to company cash (legacy path) but
/// receive a MISSING_BANK_ACCOUNT advisory flag so the UI can guide the player.
/// </summary>
public sealed class OperatingCostPhase : ITickPhase
{
    public string Name => "OperatingCosts";
    public int Order => 50;

    public Task ProcessAsync(TickContext context)
    {
        // Snapshot of every company's asset value, computed in one pass. Recomputing it per building scans all buildings each
        // time (quadratic in the number of buildings) and made the overhead of a building depend on which buildings were
        // billed before it within the same phase.
        var assetValues = context.ComputeAllCompanyAssetValues();
        var maxCompanyAssetValue = assetValues.Values.DefaultIfEmpty(0m).Max();

        foreach (var building in context.BuildingsById.Values)
        {
            if (!context.UnitsByBuilding.TryGetValue(building.Id, out var units) || units.Count == 0)
            {
                continue;
            }

            var efficiency = context.GetOperationalEfficiency(building);
            var powerEfficiency = TickContext.GetPowerEfficiency(building);
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
            var hourlyWage = CompanyEconomyCalculator.GetEffectiveHourlyWage(city, salaryMultiplier, context.GetWageIndex(city.Id));
            var companyAssetValue = context.AssetValueOrLive(assetValues, company.Id);
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
                // Machines draw power whether or not they are fully staffed (a strike shuts the building down entirely above).
                var baseEnergyMwh = CompanyEconomyCalculator.GetBaseUnitEnergyMwh(unit.UnitType, unit.Level) * powerEfficiency;

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

                // The global economic shock applies per unit, so the booked rows always sum to exactly what the account is debited.
                if (context.GlobalEventOperatingCostMultiplier != 1m)
                {
                    laborCost = decimal.Round(laborCost * context.GlobalEventOperatingCostMultiplier, 2, MidpointRounding.AwayFromZero);
                    energyCost = decimal.Round(energyCost * context.GlobalEventOperatingCostMultiplier, 2, MidpointRounding.AwayFromZero);
                }

                totalBuildingCost += laborCost + energyCost;
                unitCosts.Add((unit, laborCost, energyCost));
            }

            if (unitCosts.Count == 0)
            {
                continue;
            }

            // ── Bank account check ──
            var hasAssignedAccount = building.BankAccountId.HasValue;
            BankAccount? bankAccount = building.BankAccountId.HasValue
                && context.BankAccountsById.TryGetValue(building.BankAccountId.Value, out var ba)
                ? ba
                : context.GetBuildingFundingAccount(building);

            if (bankAccount is not null)
            {
                if (bankAccount.Balance < totalBuildingCost)
                {
                    building.IsSuspendedForFunds = true;
                    building.SuspendedReason = $"INSUFFICIENT_FUNDS:{totalBuildingCost:F2}";

                    // Record an audit ledger entry so players can see why the building stopped.
                    context.Db.LedgerEntries.Add(new LedgerEntry
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = company.Id,
                        BuildingId = building.Id,
                        BankAccountId = bankAccount.Id,
                        Category = LedgerCategory.Other,
                        Description = $"Building suspended — insufficient funds (needed {totalBuildingCost:F2}, available {bankAccount.Balance:F2})",
                        Amount = 0m,
                        RecordedAtTick = context.CurrentTick,
                        RecordedAtUtc = DateTime.UtcNow,
                    });
                    continue;
                }

                bankAccount.Balance -= totalBuildingCost;
                building.IsSuspendedForFunds = false;
                building.SuspendedReason = hasAssignedAccount ? null : "MISSING_BANK_ACCOUNT";
            }
            else
            {
                building.IsSuspendedForFunds = true;
                building.SuspendedReason = "MISSING_BANK_ACCOUNT";
                continue;
            }

            // ── Record the costs: one labor and one energy row per building (a tick writes thousands of units' worth of
            // rows otherwise, and SaveChanges dominated the tick, #630). R&D units keep their own labelled per-unit rows. ──
            var operatingLabor = 0m;
            var operatingEnergy = 0m;
            var operatingUnits = 0;
            foreach (var (unit, laborCost, energyCost) in unitCosts)
            {
                if (unit.UnitType is UnitType.ProductQuality or UnitType.BrandQuality)
                {
                    var isProduct = unit.UnitType == UnitType.ProductQuality;
                    AddLedgerEntry(context, company, building, bankAccount, unit.Id, LedgerCategory.LaborCost, laborCost,
                        isProduct ? "R&D Salary: Product Quality Research" : "R&D Salary: Brand Quality Research");
                    AddLedgerEntry(context, company, building, bankAccount, unit.Id, LedgerCategory.EnergyCost, energyCost,
                        isProduct ? "R&D Energy: Product Quality Lab" : "R&D Energy: Brand Quality Lab");
                    continue;
                }

                operatingLabor += laborCost;
                operatingEnergy += energyCost;
                operatingUnits++;
            }

            if (operatingUnits > 0)
            {
                AddLedgerEntry(context, company, building, bankAccount, null, LedgerCategory.LaborCost, operatingLabor, $"Operating labor ({operatingUnits} units)");
                AddLedgerEntry(context, company, building, bankAccount, null, LedgerCategory.EnergyCost, operatingEnergy, $"Operating energy ({operatingUnits} units)");
            }
        }

        return Task.CompletedTask;
    }

    private static void AddLedgerEntry(
        TickContext context,
        Company company,
        Building building,
        BankAccount? bankAccount,
        Guid? unitId,
        string category,
        decimal cost,
        string description)
    {
        if (cost <= 0m)
        {
            return;
        }

        context.Db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            BuildingId = building.Id,
            BuildingUnitId = unitId,
            BankAccountId = bankAccount?.Id,
            Category = category,
            Description = description,
            Amount = -cost,
            RecordedAtTick = context.CurrentTick,
            RecordedAtUtc = DateTime.UtcNow,
        });
    }
}

using Api.Data.Entities;
using Api.Utilities;

namespace Api.Engine.Phases;

/// <summary>
/// Processes active media-house campaign units.
/// During construction only labor/energy costs are charged.
/// After construction, campaign budget is charged and brand-quality boost is applied.
/// </summary>
public sealed class MediaHousePhase : ITickPhase
{
    public string Name => "MediaHouse";
    public int Order => 190;

    public Task ProcessAsync(TickContext context)
    {
        if (!context.BuildingsByType.TryGetValue(BuildingType.MediaHouse, out var mediaHouses))
        {
            return Task.CompletedTask;
        }

        foreach (var building in mediaHouses)
        {
            building.IsAdvertisingActive = false;

            if (building.DestroyedAtUtc.HasValue)
            {
                continue;
            }

            if (!context.MediaHouseUnitsByBuilding.TryGetValue(building.Id, out var units) || units.Count == 0)
            {
                continue;
            }

            var fundingAccount = context.GetBuildingFundingAccount(building);
            if (fundingAccount is null)
            {
                continue;
            }

            foreach (var unit in units)
            {
                if (!unit.IsActive)
                {
                    continue;
                }

                var laborCost = Math.Max(0m, unit.LaborCostPerTick);
                var energyCost = Math.Max(0m, unit.EnergyCostPerTick);
                var campaignCost = building.IsUnderConstruction ? 0m : Math.Max(0m, unit.CampaignBudgetPerTick);
                var totalCost = laborCost + energyCost + campaignCost;

                if (totalCost <= 0m || fundingAccount.Balance < totalCost)
                {
                    continue;
                }

                fundingAccount.Balance -= totalCost;
                building.IsAdvertisingActive = true;

                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = building.CompanyId,
                    BuildingId = building.Id,
                    BankAccountId = fundingAccount.Id,
                    Category = LedgerCategory.MediaHouseExpense,
                    Description = $"Media house campaign spend ({unit.MediaType})",
                    Amount = -totalCost,
                    RecordedAtTick = context.CurrentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                });

                if (building.IsUnderConstruction || campaignCost <= 0m)
                {
                    continue;
                }

                if (!context.CompaniesById.ContainsKey(unit.TargetCompanyId))
                {
                    continue;
                }

                var boost = decimal.Round(unit.BrandQualityBoostPerTick, 6, MidpointRounding.AwayFromZero);
                if (boost <= 0m)
                {
                    continue;
                }

                var targetBrands = context.BrandsByCompany.GetValueOrDefault(unit.TargetCompanyId, []);
                foreach (var brand in targetBrands)
                {
                    brand.MarketingQuality = Math.Clamp(brand.MarketingQuality + boost, 0m, 1m);
                }

                context.Db.BrandQualityRecords.Add(new BrandQualityRecord
                {
                    Id = Guid.NewGuid(),
                    BuildingId = building.Id,
                    MediaHouseUnitId = unit.Id,
                    TargetCompanyId = unit.TargetCompanyId,
                    RecordedAtTick = context.CurrentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                    BoostApplied = boost,
                    CampaignBudgetSpent = campaignCost,
                    LaborCostSpent = laborCost,
                    EnergyCostSpent = energyCost,
                });

                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = building.CompanyId,
                    BuildingId = building.Id,
                    BankAccountId = fundingAccount.Id,
                    Category = LedgerCategory.BrandQualityRecord,
                    Description = $"Brand quality boost applied to target company {unit.TargetCompanyId}",
                    Amount = 0m,
                    RecordedAtTick = context.CurrentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                });
            }
        }

        return Task.CompletedTask;
    }
}

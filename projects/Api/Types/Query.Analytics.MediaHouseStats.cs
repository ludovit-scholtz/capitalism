using Api.Data;
using Api.Data.Entities;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Returns current media-house campaign boost and spend statistics for a building
    /// owned by the authenticated player.
    /// </summary>
    [Authorize]
    public async Task<MediaHouseStatsResult?> GetMediaHouseStats(
        Guid buildingId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .Include(b => b.Company)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == buildingId && b.Type == BuildingType.MediaHouse);

        if (building is null || building.Company.PlayerId != userId)
        {
            return null;
        }

        var gameState = await db.GameStates.AsNoTracking().FirstOrDefaultDeterministicAsync();
        var currentTick = gameState?.CurrentTick ?? 0;
        var taxCycleTicks = Math.Max(1, gameState?.TaxCycleTicks ?? 720L);
        var taxCycleStartTick = currentTick - (currentTick % taxCycleTicks);
        var historyStartTick = Math.Max(0, currentTick - 29);

        var units = await db.MediaHouseUnits
            .Where(unit => unit.BuildingId == buildingId)
            .ToListAsync();

        var targetCompanyIds = units.Select(unit => unit.TargetCompanyId).Distinct().ToList();
        var targetNames = targetCompanyIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Companies
                .Where(company => targetCompanyIds.Contains(company.Id))
                .ToDictionaryAsync(company => company.Id, company => company.Name);

        var brandRecords = await db.BrandQualityRecords
            .Where(record => record.BuildingId == buildingId)
            .ToListAsync();

        var currentBoostDelivered = brandRecords
            .Where(record => record.RecordedAtTick == currentTick)
            .Sum(record => record.BoostApplied);

        var campaignCostThisTaxCycle = brandRecords
            .Where(record => record.RecordedAtTick >= taxCycleStartTick)
            .Sum(record => record.CampaignBudgetSpent + record.LaborCostSpent + record.EnergyCostSpent);

        var boostHistory = brandRecords
            .Where(record => record.RecordedAtTick >= historyStartTick)
            .GroupBy(record => record.RecordedAtTick)
            .OrderBy(group => group.Key)
            .Select(group => new MediaHouseBoostHistoryPoint
            {
                Tick = group.Key,
                Boost = group.Sum(record => record.BoostApplied),
            })
            .ToList();

        return new MediaHouseStatsResult
        {
            BuildingId = buildingId,
            CurrentBoostDelivered = currentBoostDelivered,
            CampaignCostThisTaxCycle = campaignCostThisTaxCycle,
            EstimatedSalesImpact = decimal.Round(currentBoostDelivered * 100m, 2, MidpointRounding.AwayFromZero),
            BoostHistory = boostHistory,
            Units = units.Select(unit => new MediaHouseUnitState
            {
                Id = unit.Id,
                TargetCompanyId = unit.TargetCompanyId,
                TargetCompanyName = targetNames.GetValueOrDefault(unit.TargetCompanyId) ?? "Unknown",
                MediaType = unit.MediaType,
                CampaignBudgetPerTick = unit.CampaignBudgetPerTick,
                BrandQualityBoostPerTick = unit.BrandQualityBoostPerTick,
                IsActive = unit.IsActive,
                LaborCostPerTick = unit.LaborCostPerTick,
                EnergyCostPerTick = unit.EnergyCostPerTick,
            }).ToList(),
        };
    }
}

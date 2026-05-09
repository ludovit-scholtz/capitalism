using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using HotChocolate;
using Microsoft.EntityFrameworkCore;
using Shared.Economy;

namespace Api.Types;

public sealed partial class Query
{
    [GraphQLName("getLandResourceStatus")]
    public async Task<LandResourceStatus?> GetLandResourceStatus(
        Guid landId,
        [Service] AppDbContext db)
    {
        var lot = await db.BuildingLots
            .Include(candidate => candidate.ResourceType)
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == landId);

        if (lot is null)
        {
            return null;
        }

        var estimatedTicksRemaining = await ComputeEstimatedTicksRemainingAsync(db, lot);
        var efficiencyFactor = MiningScarcityCalculator.ComputeEfficiencyFactor(lot.MaterialQuantity, lot.OriginalMaterialQuantity);

        return new LandResourceStatus
        {
            LandId = lot.Id,
            CityId = lot.CityId,
            ResourceTypeId = lot.ResourceTypeId,
            ResourceName = lot.ResourceType?.Name,
            IsDepletable = lot.ResourceTypeId.HasValue && lot.MaterialQuantity.HasValue,
            IsDepleted = lot.MaterialQuantity.HasValue && lot.MaterialQuantity <= 0m,
            QuantityRemaining = lot.MaterialQuantity,
            InitialQuantity = lot.OriginalMaterialQuantity,
            QualityIndex = lot.MaterialQuality,
            EfficiencyFactor = efficiencyFactor,
            EstimatedTicksRemaining = estimatedTicksRemaining,
        };
    }

    [GraphQLName("getCityResourceMap")]
    public async Task<List<CityResourceMapEntry>> GetCityResourceMap(
        Guid cityId,
        [Service] AppDbContext db)
    {
        var lots = await db.BuildingLots
            .Include(candidate => candidate.ResourceType)
            .AsNoTracking()
            .Where(candidate => candidate.CityId == cityId)
            .OrderBy(candidate => candidate.District)
            .ThenBy(candidate => candidate.Name)
            .ToListAsync();

        var estimatedTicksByLotId = await ComputeEstimatedTicksRemainingByLotAsync(
            db,
            lots.Where(lot => lot.ResourceTypeId.HasValue && lot.MaterialQuantity.HasValue && lot.MaterialQuantity > 0m)
                .Select(lot => lot.Id));

        return lots.Select(lot => new CityResourceMapEntry
        {
            LandId = lot.Id,
            CityId = lot.CityId,
            LotName = lot.Name,
            Latitude = lot.Latitude,
            Longitude = lot.Longitude,
            ResourceTypeId = lot.ResourceTypeId,
            ResourceName = lot.ResourceType?.Name,
            IsDepleted = lot.MaterialQuantity.HasValue && lot.MaterialQuantity <= 0m,
            QuantityRemaining = lot.MaterialQuantity,
            InitialQuantity = lot.OriginalMaterialQuantity,
            QualityIndex = lot.MaterialQuality,
            EfficiencyFactor = MiningScarcityCalculator.ComputeEfficiencyFactor(lot.MaterialQuantity, lot.OriginalMaterialQuantity),
            EstimatedTicksRemaining = estimatedTicksByLotId.GetValueOrDefault(lot.Id),
        }).ToList();
    }

    private static async Task<decimal?> ComputeEstimatedTicksRemainingAsync(AppDbContext db, BuildingLot lot)
    {
        var byLot = await ComputeEstimatedTicksRemainingByLotAsync(db, [lot.Id]);
        return byLot.GetValueOrDefault(lot.Id);
    }

    private static async Task<Dictionary<Guid, decimal?>> ComputeEstimatedTicksRemainingByLotAsync(
        AppDbContext db,
        IEnumerable<Guid> lotIds)
    {
        var lotIdList = lotIds.Distinct().ToList();
        if (lotIdList.Count == 0)
        {
            return [];
        }

        var lotsById = await db.BuildingLots
            .Where(lot => lotIdList.Contains(lot.Id))
            .ToDictionaryAsync(lot => lot.Id);

        var buildingByLotId = await db.BuildingLots
            .Where(lot => lotIdList.Contains(lot.Id) && lot.BuildingId.HasValue)
            .Select(lot => new { lot.Id, BuildingId = lot.BuildingId!.Value })
            .ToDictionaryAsync(x => x.Id, x => x.BuildingId);

        var buildingIds = buildingByLotId.Values.Distinct().ToList();
        var miningUnits = await db.BuildingUnits
            .Where(unit => buildingIds.Contains(unit.BuildingId) && unit.UnitType == UnitType.Mining)
            .Select(unit => new { unit.BuildingId, unit.Level })
            .ToListAsync();
        var miningRateByBuildingId = miningUnits
            .GroupBy(unit => unit.BuildingId)
            .ToDictionary(group => group.Key, group => group.Sum(unit => GameConstants.MiningRate(unit.Level)));

        var result = new Dictionary<Guid, decimal?>(lotIdList.Count);
        foreach (var lotId in lotIdList)
        {
            if (!lotsById.TryGetValue(lotId, out var lot))
            {
                result[lotId] = null;
                continue;
            }

            var remaining = lot.MaterialQuantity;
            if (!remaining.HasValue || remaining <= 0m)
            {
                result[lotId] = 0m;
                continue;
            }

            var efficiency = MiningScarcityCalculator.ComputeEfficiencyFactor(remaining, lot.OriginalMaterialQuantity);
            var nominalRate = 0m;
            if (buildingByLotId.TryGetValue(lotId, out var buildingId))
            {
                nominalRate = miningRateByBuildingId.GetValueOrDefault(buildingId, 0m);
            }

            if (nominalRate <= 0m)
            {
                result[lotId] = null;
                continue;
            }

            var effectiveRate = nominalRate * efficiency;
            result[lotId] = effectiveRate <= 0m
                ? null
                : Math.Ceiling(remaining.Value / effectiveRate);
        }

        return result;
    }
}

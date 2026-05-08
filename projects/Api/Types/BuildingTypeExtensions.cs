using Api.Data;
using Api.Data.Entities;
using Api.Utilities;
using HotChocolate;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

/// <summary>
/// HotChocolate type extension for the Building entity.
/// Adds computed fields that require additional data lookups (e.g., lot PopulationIndex).
/// </summary>
[ExtendObjectType<Building>]
public sealed class BuildingTypeExtensions
{
    /// <summary>
    /// Location quality score derived from the linked lot (1.0 = city baseline).
    /// Returned as null when the building has no linked lot.
    /// </summary>
    public async Task<decimal?> GetPopulationIndex(
        [Parent] Building building,
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        var lot = await db.BuildingLots
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.BuildingId == building.Id, cancellationToken);

        return lot?.PopulationIndex;
    }

    public async Task<Guid?> GetLotResourceTypeId(
        [Parent] Building building,
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        var lot = await db.BuildingLots
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.BuildingId == building.Id, cancellationToken);

        return lot?.ResourceTypeId;
    }

    public async Task<decimal?> GetLotMaterialQuality(
        [Parent] Building building,
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        var lot = await db.BuildingLots
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.BuildingId == building.Id, cancellationToken);

        return lot?.MaterialQuality;
    }

    public async Task<decimal?> GetLotMaterialQuantity(
        [Parent] Building building,
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        var lot = await db.BuildingLots
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.BuildingId == building.Id, cancellationToken);

        return lot?.MaterialQuantity;
    }

    public async Task<decimal?> GetLotOriginalMaterialQuantity(
        [Parent] Building building,
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        var lot = await db.BuildingLots
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.BuildingId == building.Id, cancellationToken);

        return lot?.OriginalMaterialQuantity;
    }

    /// <summary>
    /// The city's base average rent per m² in local currency.
    /// Useful as a reference for players setting rent on apartment/commercial buildings.
    /// </summary>
    public async Task<decimal?> GetCityReferenceRentPerSqm(
        [Parent] Building building,
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        if (building.Type != BuildingType.Apartment && building.Type != BuildingType.Commercial)
            return null;

        var city = await db.Cities
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == building.CityId, cancellationToken);
        return city?.AverageRentPerSqm;
    }

    /// <summary>
    /// The location-adjusted market rent per m² for this building.
    /// Equals the city reference rate multiplied by the lot's PopulationIndex (location advantage).
    /// This is the rate players should compare their asking rent against.
    /// </summary>
    public async Task<decimal?> GetAdjustedMarketRentPerSqm(
        [Parent] Building building,
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        if (building.Type != BuildingType.Apartment && building.Type != BuildingType.Commercial)
            return null;

        var city = await db.Cities
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == building.CityId, cancellationToken);
        if (city is null || city.AverageRentPerSqm <= 0m) return null;

        var lot = await db.BuildingLots
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.BuildingId == building.Id, cancellationToken);

        var populationIndex = lot?.PopulationIndex > 0m ? lot.PopulationIndex : 1m;
        return city.AverageRentPerSqm * populationIndex;
    }

    /// <summary>
    /// Returns true when there is at least one DEFAULTED loan with this building as collateral.
    /// Used by the dashboard building card to show the "⚠️ Loan Default" badge.
    /// </summary>
    public async Task<bool> GetHasDefaultedCollateralLoan(
        [Parent] Building building,
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        return await db.Loans
            .AsNoTracking()
            .AnyAsync(l => l.CollateralBuildingId == building.Id && l.Status == LoanStatus.Defaulted, cancellationToken);
    }

    public async Task<BuildingMarketValuation> GetMarketValuation(
        [Parent] Building building,
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        return await BuildingMarketValuationCalculator.CalculateAsync(db, building, cancellationToken);
    }
}

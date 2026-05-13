using Api.Data;
using Api.Data.Entities;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

/// <summary>Output type for per-tick rental income history snapshots.</summary>
public sealed class RentalTickSnapshot
{
    public long Tick { get; set; }
    public decimal Revenue { get; set; }
    public decimal Costs { get; set; }
    public decimal OccupancyPercent { get; set; }
    public decimal RentPerSqm { get; set; }
}

/// <summary>Output type for the apartmentBuildingDetail query.</summary>
public sealed class ApartmentBuildingDetail
{
    public Guid BuildingId { get; set; }
    public string BuildingName { get; set; } = string.Empty;
    public string BuildingType { get; set; } = string.Empty;
    public decimal? PricePerSqm { get; set; }
    public decimal? PendingPricePerSqm { get; set; }
    public long? PendingPriceActivationTick { get; set; }
    public decimal? OccupancyPercent { get; set; }
    public decimal? TotalAreaSqm { get; set; }
    public decimal CityAverageRentPerSqm { get; set; }
    public decimal AdjustedMarketRentPerSqm { get; set; }
    public decimal PopulationIndex { get; set; }
    public string CurrencyCode { get; set; } = "EUR";
    public List<RentalTickSnapshot> RevenueHistory { get; set; } = new();
}

/// <summary>Output type for cityRentalMarket query — average rent per type.</summary>
public sealed class CityRentalMarket
{
    public Guid CityId { get; set; }
    public decimal CityAverageRentPerSqm { get; set; }
    public decimal? AverageApartmentRentPerSqm { get; set; }
    public decimal? AverageCommercialRentPerSqm { get; set; }
    public int ActiveApartmentCount { get; set; }
    public int ActiveCommercialCount { get; set; }
}

public sealed partial class Query
{
    /// <summary>
    /// Returns detailed rental property analytics for an APARTMENT or COMMERCIAL building.
    /// Includes current occupancy, area, active/pending rent, city-average benchmark,
    /// and last-100-tick revenue history for sparkline display.
    /// Owner-only: returns null if the building is not owned by the authenticated player.
    /// </summary>
    [Authorize]
    public async Task<ApartmentBuildingDetail?> ApartmentBuildingDetail(
        Guid buildingId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .Include(b => b.Company)
            .Include(b => b.City)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == buildingId);

        if (building is null || building.Company.PlayerId != userId)
            return null;

        if (building.Type != Data.Entities.BuildingType.Apartment
            && building.Type != Data.Entities.BuildingType.Commercial)
            return null;

        // Revenue history — last 100 ticks ordered oldest first for sparkline.
        var history = await db.RentalIncomeRecords
            .Where(r => r.BuildingId == buildingId)
            .OrderByDescending(r => r.Tick)
            .Take(100)
            .OrderBy(r => r.Tick)
            .AsNoTracking()
            .ToListAsync();

        // Load lot to get PopulationIndex.
        var lot = await db.BuildingLots
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.BuildingId == buildingId);

        var populationIndex = lot?.PopulationIndex > 0m ? lot.PopulationIndex : 1m;
        var cityBaseRate = building.City.AverageRentPerSqm;
        var adjustedRate = cityBaseRate * populationIndex;

        return new ApartmentBuildingDetail
        {
            BuildingId = building.Id,
            BuildingName = building.Name,
            BuildingType = building.Type,
            PricePerSqm = building.PricePerSqm,
            PendingPricePerSqm = building.PendingPricePerSqm,
            PendingPriceActivationTick = building.PendingPriceActivationTick,
            OccupancyPercent = building.OccupancyPercent,
            TotalAreaSqm = building.TotalAreaSqm,
            CityAverageRentPerSqm = cityBaseRate,
            AdjustedMarketRentPerSqm = adjustedRate,
            PopulationIndex = populationIndex,
            CurrencyCode = building.City.CurrencyCode ?? "EUR",
            RevenueHistory = history.Select(r => new RentalTickSnapshot
            {
                Tick = r.Tick,
                Revenue = r.Revenue,
                Costs = r.Costs,
                OccupancyPercent = r.OccupancyPercent,
                RentPerSqm = r.RentPerSqm,
            }).ToList(),
        };
    }

    /// <summary>
    /// Returns the rental market summary for a city — average rent per m² by building type.
    /// Used as a benchmark in the building detail UI (city-average badge).
    /// Public query: no authentication required.
    /// </summary>
    public async Task<CityRentalMarket?> CityRentalMarket(
        Guid cityId,
        [Service] AppDbContext db)
    {
        var city = await db.Cities
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cityId);

        if (city is null) return null;

        // Only active (non-null pricePerSqm) buildings count toward the market average.
        var activeProperties = await db.Buildings
            .Where(b => b.CityId == cityId
                        && (b.Type == Data.Entities.BuildingType.Apartment || b.Type == Data.Entities.BuildingType.Commercial)
                        && b.PricePerSqm != null)
            .AsNoTracking()
            .Select(b => new { b.Type, b.PricePerSqm })
            .ToListAsync();

        var aptPrices = activeProperties
            .Where(b => b.Type == Data.Entities.BuildingType.Apartment && b.PricePerSqm.HasValue)
            .Select(b => b.PricePerSqm!.Value)
            .ToList();
        decimal? avgApartment = aptPrices.Count > 0 ? aptPrices.Average() : null;

        var commPrices = activeProperties
            .Where(b => b.Type == Data.Entities.BuildingType.Commercial && b.PricePerSqm.HasValue)
            .Select(b => b.PricePerSqm!.Value)
            .ToList();
        decimal? avgCommercial = commPrices.Count > 0 ? commPrices.Average() : null;

        return new CityRentalMarket
        {
            CityId = cityId,
            CityAverageRentPerSqm = city.AverageRentPerSqm,
            AverageApartmentRentPerSqm = avgApartment,
            AverageCommercialRentPerSqm = avgCommercial,
            ActiveApartmentCount = activeProperties.Count(b => b.Type == Data.Entities.BuildingType.Apartment),
            ActiveCommercialCount = activeProperties.Count(b => b.Type == Data.Entities.BuildingType.Commercial),
        };
    }
}

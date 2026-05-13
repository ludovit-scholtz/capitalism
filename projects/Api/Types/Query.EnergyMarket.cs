using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Returns all active energy spot-market listings for a given city, ordered cheapest first.
    ///
    /// This is a public query — no authentication required — so any player can evaluate
    /// the spot market before deciding to raise their bid price or build a power plant.
    /// </summary>
    public async Task<List<EnergyMarketListingDto>> GetEnergyMarket(
        Guid cityId,
        [Service] AppDbContext db)
    {
        var listings = await db.EnergyListings
            .AsNoTracking()
            .Where(l => l.CityId == cityId && l.IsActive)
            .Include(l => l.Building)
            .Include(l => l.Company)
            .OrderBy(l => l.PricePerKwhLocal)
            .ThenBy(l => l.CreatedAtTick)
            .ToListAsync();

        return listings.Select(l => new EnergyMarketListingDto
        {
            ListingId = l.Id,
            BuildingId = l.BuildingId,
            BuildingName = l.Building.Name,
            CompanyId = l.CompanyId,
            CompanyName = l.Company.Name,
            CityId = l.CityId,
            PlantType = l.Building.PowerPlantType ?? PowerPlantType.Coal,
            PricePerKwhLocal = l.PricePerKwhLocal,
            CapacityKw = l.CapacityKw,
            AvailableKw = l.AvailableKw,
            CreatedAtTick = l.CreatedAtTick,
            CreatedAtUtc = l.CreatedAtUtc,
        }).ToList();
    }
}

/// <summary>DTO for a single active energy spot-market listing.</summary>
public sealed class EnergyMarketListingDto
{
    public Guid ListingId { get; set; }
    public Guid BuildingId { get; set; }
    public string BuildingName { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public Guid CityId { get; set; }
    public string PlantType { get; set; } = string.Empty;
    public decimal PricePerKwhLocal { get; set; }
    public decimal CapacityKw { get; set; }
    public decimal AvailableKw { get; set; }
    public long CreatedAtTick { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

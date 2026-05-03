using Api.Data;
using Api.Data.Entities;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Returns all buildings currently listed for sale across all cities.
    /// Optionally filtered by city and/or building type.
    /// </summary>
    public async Task<List<BuildingMarketListing>> GetBuildingMarket(
        [Service] AppDbContext db,
        Guid? cityId = null,
        string? buildingType = null,
        decimal? maxPrice = null)
    {
        var query = db.Buildings
            .AsNoTracking()
            .Include(b => b.Company)
            .ThenInclude(c => c.Player)
            .Include(b => b.City)
            .Where(b => b.IsForSale && b.AskingPrice.HasValue);

        if (cityId.HasValue)
        {
            query = query.Where(b => b.CityId == cityId.Value);
        }

        if (!string.IsNullOrEmpty(buildingType))
        {
            query = query.Where(b => b.Type == buildingType);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(b => b.AskingPrice <= maxPrice.Value);
        }

        var buildings = await query
            .OrderBy(b => b.AskingPrice)
            .ToListAsync();

        // Load pending offer counts separately (no Cartesian explosion risk)
        var buildingIds = buildings.Select(b => b.Id).ToList();
        var offerCounts = await db.BuildingSaleOffers
            .AsNoTracking()
            .Where(o => buildingIds.Contains(o.BuildingId) && o.Status == BuildingSaleOfferStatus.Pending)
            .GroupBy(o => o.BuildingId)
            .Select(g => new { BuildingId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BuildingId, x => x.Count);

        return buildings.Select(b => new BuildingMarketListing
        {
            Building = b,
            PendingOfferCount = offerCounts.GetValueOrDefault(b.Id, 0),
        }).ToList();
    }

    /// <summary>
    /// Returns all buildings listed for sale by the authenticated player,
    /// together with their pending offers.
    /// </summary>
    [Authorize]
    public async Task<List<BuildingMarketMyListing>> GetMyBuildingListings(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var buildings = await db.Buildings
            .AsNoTracking()
            .Include(b => b.Company)
            .Include(b => b.City)
            .Where(b => b.IsForSale && b.AskingPrice.HasValue && b.Company.PlayerId == userId)
            .OrderByDescending(b => b.AskingPrice)
            .ToListAsync();

        var buildingIds = buildings.Select(b => b.Id).ToList();

        var offers = await db.BuildingSaleOffers
            .AsNoTracking()
            .Include(o => o.BuyerPlayer)
            .Include(o => o.BuyerCompany)
            .Where(o => buildingIds.Contains(o.BuildingId))
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync();

        var offersByBuilding = offers.GroupBy(o => o.BuildingId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return buildings.Select(b => new BuildingMarketMyListing
        {
            Building = b,
            Offers = offersByBuilding.GetValueOrDefault(b.Id, []),
        }).ToList();
    }
}

/// <summary>A building market listing with aggregate offer count.</summary>
public sealed class BuildingMarketListing
{
    public Building Building { get; set; } = null!;
    public int PendingOfferCount { get; set; }
}

/// <summary>The authenticated seller's listing with full offer detail.</summary>
public sealed class BuildingMarketMyListing
{
    public Building Building { get; set; } = null!;
    public List<BuildingSaleOffer> Offers { get; set; } = [];
}

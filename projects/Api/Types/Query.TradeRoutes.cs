using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Engine.Phases;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

/// <summary>
/// GraphQL queries for inter-city trade routes.
/// </summary>
public sealed partial class Query
{
    /// <summary>
    /// Returns all trade routes for the specified company, ordered by creation date descending.
    /// Includes source/destination building and city names for display.
    /// </summary>
    [Authorize]
    public async Task<List<TradeRouteResult>> GetMyTradeRoutes(
        Guid companyId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == userId, CancellationToken.None)
            ?? throw new InvalidOperationException("Player not found.");

        // Only allow the player who owns the company or an admin.
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == companyId, CancellationToken.None);
        if (company is null)
            throw new GraphQLException(ErrorBuilder.New().SetMessage("Company not found.").SetCode("COMPANY_NOT_FOUND").Build());

        if (company.PlayerId != player.Id && player.Role != PlayerRole.Admin)
            throw new GraphQLException(ErrorBuilder.New().SetMessage("Access denied.").SetCode("ACCESS_DENIED").Build());

        var routes = await db.InterCityTradeRoutes
            .AsNoTracking()
            .Where(r => r.CompanyId == companyId)
            .Include(r => r.SourceBuilding).ThenInclude(b => b.City)
            .Include(r => r.DestinationBuilding).ThenInclude(b => b.City)
            .Include(r => r.ProductType)
            .Include(r => r.ResourceType)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(CancellationToken.None);

        return routes.Select(r => MapRouteToResult(r)).ToList();
    }

    /// <summary>
    /// Returns a single trade route by ID.
    /// </summary>
    [Authorize]
    public async Task<TradeRouteResult?> GetTradeRoute(
        Guid routeId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == userId, CancellationToken.None)
            ?? throw new InvalidOperationException("Player not found.");

        var route = await db.InterCityTradeRoutes
            .AsNoTracking()
            .Where(r => r.Id == routeId)
            .Include(r => r.SourceBuilding).ThenInclude(b => b.City)
            .Include(r => r.DestinationBuilding).ThenInclude(b => b.City)
            .Include(r => r.Company)
            .Include(r => r.ProductType)
            .Include(r => r.ResourceType)
            .FirstOrDefaultAsync(CancellationToken.None);

        if (route is null) return null;

        if (route.Company.PlayerId != player.Id && player.Role != PlayerRole.Admin)
            throw new GraphQLException(ErrorBuilder.New().SetMessage("Access denied.").SetCode("ACCESS_DENIED").Build());

        return MapRouteToResult(route);
    }

    /// <summary>
    /// Estimates the shipping cost and transit time for a prospective trade route.
    /// </summary>
    public async Task<TradeRouteEstimate> EstimateTradeRoute(
        Guid sourceBuildingId,
        Guid destinationBuildingId,
        Guid? productTypeId,
        Guid? resourceTypeId,
        decimal quantity,
        [Service] AppDbContext db)
    {
        var sourceBuilding = await db.Buildings.Include(b => b.City).FirstOrDefaultAsync(b => b.Id == sourceBuildingId)
            ?? throw new GraphQLException(ErrorBuilder.New().SetMessage("Source building not found.").SetCode("BUILDING_NOT_FOUND").Build());

        var destBuilding = await db.Buildings.Include(b => b.City).FirstOrDefaultAsync(b => b.Id == destinationBuildingId)
            ?? throw new GraphQLException(ErrorBuilder.New().SetMessage("Destination building not found.").SetCode("BUILDING_NOT_FOUND").Build());

        if (sourceBuilding.CityId == destBuilding.CityId)
            throw new GraphQLException(ErrorBuilder.New().SetMessage("Source and destination must be in different cities.").SetCode("SAME_CITY").Build());

        var resourceTypesById = await db.ResourceTypes.ToDictionaryAsync(r => r.Id);
        var productTypesById = await db.ProductTypes.ToDictionaryAsync(p => p.Id);
        var recipesByProduct = (await db.ProductRecipes.ToListAsync())
            .GroupBy(r => r.ProductTypeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var itemWeight = GlobalExchangeCalculator.ComputeItemWeightPerUnit(
            resourceTypeId, productTypeId, resourceTypesById, productTypesById, recipesByProduct);

        var fuelIndex = destBuilding.City?.FuelPriceIndex ?? 1.0m;

        var shippingCostPerUnit = GlobalExchangeCalculator.ComputeTransitCostPerUnit(
            sourceBuilding.Latitude, sourceBuilding.Longitude,
            destBuilding.Latitude, destBuilding.Longitude,
            itemWeight, fuelIndex);

        var distanceKm = GlobalExchangeCalculator.ComputeDistanceKm(
            sourceBuilding.Latitude, sourceBuilding.Longitude,
            destBuilding.Latitude, destBuilding.Longitude);

        var transitTicks = TradeRoutePhase.ComputeTransitTicks(distanceKm);

        return new TradeRouteEstimate(
            DistanceKm: (decimal)distanceKm,
            TransitTicks: transitTicks,
            ShippingCostPerUnit: shippingCostPerUnit,
            TotalShippingCost: shippingCostPerUnit * quantity);
    }

    private static TradeRouteResult MapRouteToResult(InterCityTradeRoute r) => new(
        Id: r.Id,
        CompanyId: r.CompanyId,
        SourceBuildingId: r.SourceBuildingId,
        SourceBuildingName: r.SourceBuilding?.Name ?? string.Empty,
        SourceCityName: r.SourceBuilding?.City?.Name ?? string.Empty,
        DestinationBuildingId: r.DestinationBuildingId,
        DestinationBuildingName: r.DestinationBuilding?.Name ?? string.Empty,
        DestinationCityName: r.DestinationBuilding?.City?.Name ?? string.Empty,
        ProductTypeId: r.ProductTypeId,
        ProductTypeName: r.ProductType?.Name,
        ResourceTypeId: r.ResourceTypeId,
        ResourceTypeName: r.ResourceType?.Name,
        Quantity: r.Quantity,
        Quality: r.Quality,
        PricePerUnit: r.PricePerUnit,
        ScheduledDepartureTick: r.ScheduledDepartureTick,
        ExpectedArrivalTick: r.ExpectedArrivalTick,
        TransitTicks: r.TransitTicks,
        ShippingCostEstimate: r.ShippingCostEstimate,
        ShippingCostActual: r.ShippingCostActual,
        Status: r.Status,
        FailureReason: r.FailureReason,
        CreatedAtUtc: r.CreatedAtUtc,
        DepartedAtUtc: r.DepartedAtUtc,
        CompletedAtUtc: r.CompletedAtUtc);
}

/// <summary>Result DTO for a trade route query.</summary>
public record TradeRouteResult(
    Guid Id,
    Guid CompanyId,
    Guid SourceBuildingId,
    string SourceBuildingName,
    string SourceCityName,
    Guid DestinationBuildingId,
    string DestinationBuildingName,
    string DestinationCityName,
    Guid? ProductTypeId,
    string? ProductTypeName,
    Guid? ResourceTypeId,
    string? ResourceTypeName,
    decimal Quantity,
    decimal Quality,
    decimal PricePerUnit,
    long ScheduledDepartureTick,
    long ExpectedArrivalTick,
    long TransitTicks,
    decimal ShippingCostEstimate,
    decimal ShippingCostActual,
    string Status,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? DepartedAtUtc,
    DateTime? CompletedAtUtc);

/// <summary>Estimate payload for a prospective trade route.</summary>
public record TradeRouteEstimate(
    decimal DistanceKm,
    long TransitTicks,
    decimal ShippingCostPerUnit,
    decimal TotalShippingCost);

using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Engine.Phases;
using Api.Security;
using Api.Utilities;
using HotChocolate;
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
        Guid? companyId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == userId, CancellationToken.None)
            ?? throw new InvalidOperationException("Player not found.");

        var routesQuery = db.InterCityTradeRoutes
            .AsNoTracking()
            .Include(r => r.SourceBuilding).ThenInclude(b => b.City)
            .Include(r => r.DestinationBuilding).ThenInclude(b => b.City)
            .Include(r => r.ProductType)
            .Include(r => r.ResourceType)
            .AsQueryable();

        if (companyId.HasValue)
        {
            // Only allow the player who owns the company or an admin.
            var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == companyId.Value, CancellationToken.None);
            if (company is null)
                throw new GraphQLException(ErrorBuilder.New().SetMessage("Company not found.").SetCode("COMPANY_NOT_FOUND").Build());

            if (company.PlayerId != player.Id && player.Role != PlayerRole.Admin)
                throw new GraphQLException(ErrorBuilder.New().SetMessage("Access denied.").SetCode("ACCESS_DENIED").Build());

            routesQuery = routesQuery.Where(r => r.CompanyId == companyId.Value);
        }
        else
        {
            var ownCompanyIds = db.Companies
                .AsNoTracking()
                .Where(c => c.PlayerId == player.Id)
                .Select(c => c.Id);
            routesQuery = routesQuery.Where(r => ownCompanyIds.Contains(r.CompanyId));
        }

        var routes = await routesQuery
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
    /// Compatibility alias for cross-city shipment list used by expansion flows.
    /// </summary>
    [Authorize]
    [GraphQLName("getCrossCityShipments")]
    public Task<List<TradeRouteResult>> GetCrossCityShipments(
        Guid? companyId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
        => GetMyTradeRoutes(companyId, db, httpContextAccessor);

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

    /// <summary>
    /// Returns a point-to-point shipping quote between two buildings for the requested product quantity.
    /// The quote always enforces a positive non-zero minimum transit cost per unit.
    /// </summary>
    [Authorize]
    [GraphQLName("shippingCostQuote")]
    public async Task<ShippingCostQuoteResult> GetShippingCostQuote(
        Guid fromBuildingId,
        Guid toBuildingId,
        Guid productTypeId,
        decimal quantity,
        [Service] AppDbContext db)
    {
        if (quantity <= 0m)
            throw new GraphQLException(ErrorBuilder.New().SetMessage("Quantity must be positive.").SetCode("INVALID_QUANTITY").Build());

        var fromBuilding = await db.Buildings
            .Include(b => b.City)
            .FirstOrDefaultAsync(b => b.Id == fromBuildingId)
            ?? throw new GraphQLException(ErrorBuilder.New().SetMessage("Source building not found.").SetCode("BUILDING_NOT_FOUND").Build());

        var toBuilding = await db.Buildings
            .Include(b => b.City)
            .FirstOrDefaultAsync(b => b.Id == toBuildingId)
            ?? throw new GraphQLException(ErrorBuilder.New().SetMessage("Destination building not found.").SetCode("BUILDING_NOT_FOUND").Build());

        var productType = await db.ProductTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productTypeId)
            ?? throw new GraphQLException(ErrorBuilder.New().SetMessage("Product type not found.").SetCode("PRODUCT_TYPE_NOT_FOUND").Build());

        var recipesByProduct = (await db.ProductRecipes
                .AsNoTracking()
                .ToListAsync())
            .GroupBy(r => r.ProductTypeId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var resourceTypesById = await db.ResourceTypes.AsNoTracking().ToDictionaryAsync(r => r.Id);
        var productTypesById = await db.ProductTypes.AsNoTracking().ToDictionaryAsync(p => p.Id);

        var weightKgPerUnit = GlobalExchangeCalculator.ComputeItemWeightPerUnit(
            null,
            productType.Id,
            resourceTypesById,
            productTypesById,
            recipesByProduct);

        var destinationFuelPriceIndex = toBuilding.City?.FuelPriceIndex ?? 1m;
        var costPerUnit = GlobalExchangeCalculator.ComputeTransitCostPerUnit(
            fromBuilding.Latitude,
            fromBuilding.Longitude,
            toBuilding.Latitude,
            toBuilding.Longitude,
            weightKgPerUnit,
            destinationFuelPriceIndex);

        var distanceKm = GlobalExchangeCalculator.ComputeDistanceKm(
            fromBuilding.Latitude,
            fromBuilding.Longitude,
            toBuilding.Latitude,
            toBuilding.Longitude);

        var currencyCode = toBuilding.City?.CurrencyCode ?? "EUR";

        return new ShippingCostQuoteResult(
            DistanceKm: decimal.Round((decimal)distanceKm, 3, MidpointRounding.AwayFromZero),
            WeightKgPerUnit: decimal.Round(weightKgPerUnit, 4, MidpointRounding.AwayFromZero),
            Quantity: quantity,
            CostPerUnit: costPerUnit,
            TotalCost: decimal.Round(costPerUnit * quantity, 2, MidpointRounding.AwayFromZero),
            CurrencyCode: currencyCode);
    }

    /// <summary>
    /// Returns freight estimate for sourcing from an origin building to another city.
    /// Uses the product-definition transit rule: ceil(distance / 500), minimum 1 tick.
    /// </summary>
    [GraphQLName("getLogisticsCostEstimate")]
    public async Task<LogisticsCostEstimateResult> GetLogisticsCostEstimate(
        Guid originBuildingId,
        Guid destinationCityId,
        Guid? resourceTypeId,
        Guid? productTypeId,
        decimal quantity,
        [Service] AppDbContext db)
    {
        if (quantity <= 0m)
            throw new GraphQLException(ErrorBuilder.New().SetMessage("Quantity must be positive.").SetCode("INVALID_QUANTITY").Build());

        var originBuilding = await db.Buildings
            .Include(b => b.City)
            .FirstOrDefaultAsync(b => b.Id == originBuildingId)
            ?? throw new GraphQLException(ErrorBuilder.New().SetMessage("Origin building not found.").SetCode("BUILDING_NOT_FOUND").Build());

        var destinationCity = await db.Cities
            .FirstOrDefaultAsync(c => c.Id == destinationCityId)
            ?? throw new GraphQLException(ErrorBuilder.New().SetMessage("Destination city not found.").SetCode("CITY_NOT_FOUND").Build());

        var resourceTypesById = await db.ResourceTypes.ToDictionaryAsync(r => r.Id);
        var productTypesById = await db.ProductTypes.ToDictionaryAsync(p => p.Id);
        var recipesByProduct = (await db.ProductRecipes.ToListAsync())
            .GroupBy(r => r.ProductTypeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var itemWeight = GlobalExchangeCalculator.ComputeItemWeightPerUnit(
            resourceTypeId, productTypeId, resourceTypesById, productTypesById, recipesByProduct);

        var shippingCostPerUnit = GlobalExchangeCalculator.ComputeTransitCostPerUnit(
            originBuilding.Latitude, originBuilding.Longitude,
            destinationCity.Latitude, destinationCity.Longitude,
            itemWeight, destinationCity.FuelPriceIndex);

        var distanceKm = GlobalExchangeCalculator.ComputeDistanceKm(
            originBuilding.Latitude, originBuilding.Longitude,
            destinationCity.Latitude, destinationCity.Longitude);

        var transitTicks = ComputeCrossCityTransitTicks(distanceKm);
        var currentTick = await db.GameStates.Select(gs => gs.CurrentTick).FirstOrDefaultAsync();

        return new LogisticsCostEstimateResult(
            DistanceKm: (decimal)distanceKm,
            FreightCostPerUnit: shippingCostPerUnit,
            TotalFreightCost: shippingCostPerUnit * quantity,
            TransitTicks: transitTicks,
            EstimatedArrivalTick: currentTick + transitTicks);
    }

    private static long ComputeCrossCityTransitTicks(double distanceKm)
        => Math.Max(1L, (long)Math.Ceiling(distanceKm / 500.0d));

    private static TradeRouteResult MapRouteToResult(InterCityTradeRoute r) => new(
        Id: r.Id,
        CompanyId: r.CompanyId,
        SourceBuildingId: r.SourceBuildingId,
        SourceBuildingName: r.SourceBuilding?.Name ?? string.Empty,
        SourceCityName: r.SourceBuilding?.City?.Name ?? string.Empty,
        SourceCurrencyCode: r.SourceBuilding?.City?.CurrencyCode ?? "EUR",
        DestinationBuildingId: r.DestinationBuildingId,
        DestinationBuildingName: r.DestinationBuilding?.Name ?? string.Empty,
        DestinationCityName: r.DestinationBuilding?.City?.Name ?? string.Empty,
        DestinationCurrencyCode: r.DestinationBuilding?.City?.CurrencyCode ?? "EUR",
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
    string SourceCurrencyCode,
    Guid DestinationBuildingId,
    string DestinationBuildingName,
    string DestinationCityName,
    string DestinationCurrencyCode,
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

/// <summary>Freight estimate payload used by cross-city expansion UX.</summary>
public record LogisticsCostEstimateResult(
    decimal DistanceKm,
    decimal FreightCostPerUnit,
    decimal TotalFreightCost,
    long TransitTicks,
    long EstimatedArrivalTick);

public record ShippingCostQuoteResult(
    decimal DistanceKm,
    decimal WeightKgPerUnit,
    decimal Quantity,
    decimal CostPerUnit,
    decimal TotalCost,
    string CurrencyCode);

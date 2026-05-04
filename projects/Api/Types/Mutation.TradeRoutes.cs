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
/// GraphQL mutations for inter-city trade routes.
/// </summary>
public sealed partial class Mutation
{
    /// <summary>
    /// Creates an inter-city trade route.
    /// Validates:
    ///   - Source and destination are in different cities.
    ///   - Source unit is a B2B_SALES unit with sufficient inventory.
    ///   - Destination unit is a PURCHASE unit.
    ///   - The requesting player owns the source company.
    /// On success, inventory is immediately deducted from the source unit and the
    /// route is placed IN_TRANSIT; the <see cref="TradeRoutePhase"/> delivers it.
    /// </summary>
    [Authorize]
    public async Task<CreateTradeRoutePayload> CreateTradeRoute(
        CreateTradeRouteInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var player = await db.Players
            .FirstOrDefaultAsync(p => p.Id == httpContextAccessor.HttpContext!.User.GetRequiredUserId(), cancellationToken)
            ?? throw new InvalidOperationException("Player not found.");

        // ── Validate company ownership ────────────────────────────────────────────
        var company = await db.Companies
            .Include(c => c.BankAccounts.Where(a => a.ClosedAtUtc == null))
            .FirstOrDefaultAsync(c => c.Id == input.CompanyId, cancellationToken);

        if (company is null)
            return CreateTradeRoutePayload.Fail("Company not found.", "COMPANY_NOT_FOUND");

        if (company.PlayerId != player.Id && player.Role != PlayerRole.Admin)
            return CreateTradeRoutePayload.Fail("You do not own this company.", "ACCESS_DENIED");

        // ── Validate source building + unit ───────────────────────────────────────
        var sourceBuilding = await db.Buildings
            .Include(b => b.City)
            .Include(b => b.Units)
            .FirstOrDefaultAsync(b => b.Id == input.SourceBuildingId && b.CompanyId == company.Id, cancellationToken);

        if (sourceBuilding is null)
            return CreateTradeRoutePayload.Fail("Source building not found or not owned by company.", "BUILDING_NOT_FOUND");

        if (sourceBuilding.IsSuspendedForFunds)
            return CreateTradeRoutePayload.Fail("Source building is suspended for insufficient funds.", "BUILDING_SUSPENDED");

        var sourceUnit = sourceBuilding.Units.FirstOrDefault(u => u.Id == input.SourceBuildingUnitId);
        if (sourceUnit is null || sourceUnit.UnitType != UnitType.B2BSales)
            return CreateTradeRoutePayload.Fail("Source unit is not a valid B2B sales unit.", "INVALID_SOURCE_UNIT");

        // ── Validate destination building + unit ──────────────────────────────────
        var destBuilding = await db.Buildings
            .Include(b => b.City)
            .Include(b => b.Units)
            .FirstOrDefaultAsync(b => b.Id == input.DestinationBuildingId, cancellationToken);

        if (destBuilding is null)
            return CreateTradeRoutePayload.Fail("Destination building not found.", "BUILDING_NOT_FOUND");

        if (sourceBuilding.CityId == destBuilding.CityId)
            return CreateTradeRoutePayload.Fail("Source and destination must be in different cities.", "SAME_CITY");

        var destUnit = destBuilding.Units.FirstOrDefault(u => u.Id == input.DestinationBuildingUnitId);
        if (destUnit is null || destUnit.UnitType != UnitType.Purchase)
            return CreateTradeRoutePayload.Fail("Destination unit is not a valid purchase unit.", "INVALID_DEST_UNIT");

        // ── Validate item type (must specify exactly one of product or resource) ──
        if (input.ProductTypeId is null && input.ResourceTypeId is null)
            return CreateTradeRoutePayload.Fail("Specify either a product type or a resource type.", "MISSING_ITEM_TYPE");

        if (input.ProductTypeId.HasValue && input.ResourceTypeId.HasValue)
            return CreateTradeRoutePayload.Fail("Specify only one of product type or resource type.", "AMBIGUOUS_ITEM_TYPE");

        // ── Check inventory availability in source unit ───────────────────────────
        var inventory = await db.Inventories
            .FirstOrDefaultAsync(i =>
                i.BuildingUnitId == sourceUnit.Id
                && i.ProductTypeId == input.ProductTypeId
                && i.ResourceTypeId == input.ResourceTypeId,
                cancellationToken);

        if (inventory is null || inventory.Quantity < input.Quantity)
            return CreateTradeRoutePayload.Fail("Insufficient inventory in source unit.", "INSUFFICIENT_INVENTORY");

        if (input.Quantity <= 0m)
            return CreateTradeRoutePayload.Fail("Quantity must be positive.", "INVALID_QUANTITY");

        // ── Compute shipping cost ─────────────────────────────────────────────────
        var resourceTypesById = await db.ResourceTypes
            .ToDictionaryAsync(r => r.Id, cancellationToken);
        var productTypesById = await db.ProductTypes
            .ToDictionaryAsync(p => p.Id, cancellationToken);
        var recipesByProduct = (await db.ProductRecipes.ToListAsync(cancellationToken))
            .GroupBy(r => r.ProductTypeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var itemWeight = GlobalExchangeCalculator.ComputeItemWeightPerUnit(
            input.ResourceTypeId, input.ProductTypeId,
            resourceTypesById, productTypesById, recipesByProduct);

        var fuelIndex = destBuilding.City?.FuelPriceIndex ?? 1.0m;
        var shippingCostPerUnit = GlobalExchangeCalculator.ComputeTransitCostPerUnit(
            sourceBuilding.Latitude, sourceBuilding.Longitude,
            destBuilding.Latitude, destBuilding.Longitude,
            itemWeight, fuelIndex);

        var totalShippingCost = shippingCostPerUnit * input.Quantity;

        var distanceKm = GlobalExchangeCalculator.ComputeDistanceKm(
            sourceBuilding.Latitude, sourceBuilding.Longitude,
            destBuilding.Latitude, destBuilding.Longitude);

        var transitTicks = TradeRoutePhase.ComputeTransitTicks(distanceKm);

        // ── Load current game tick ────────────────────────────────────────────────
        var gameState = await db.GameStates.FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Game state not found.");

        var currentTick = gameState.CurrentTick;
        var arrivalTick = currentTick + transitTicks;

        // ── Deduct inventory from source unit ─────────────────────────────────────
        var costRemoved = inventory.SourcingCostTotal > 0m && inventory.Quantity > 0m
            ? inventory.SourcingCostTotal * (input.Quantity / inventory.Quantity)
            : 0m;

        var qualityAtDispatch = inventory.Quality;
        inventory.Quantity -= input.Quantity;
        inventory.SourcingCostTotal = Math.Max(0m, inventory.SourcingCostTotal - costRemoved);

        if (inventory.Quantity <= 0m)
            inventory.SourcingCostTotal = 0m;

        // ── Create the trade route ────────────────────────────────────────────────
        var route = new InterCityTradeRoute
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            SourceBuildingId = sourceBuilding.Id,
            SourceBuildingUnitId = sourceUnit.Id,
            DestinationBuildingId = destBuilding.Id,
            DestinationBuildingUnitId = destUnit.Id,
            ProductTypeId = input.ProductTypeId,
            ResourceTypeId = input.ResourceTypeId,
            Quantity = input.Quantity,
            Quality = qualityAtDispatch,
            SourcingCostTotal = costRemoved,
            PricePerUnit = input.PricePerUnit,
            ScheduledDepartureTick = currentTick,
            ExpectedArrivalTick = arrivalTick,
            TransitTicks = transitTicks,
            ShippingCostEstimate = totalShippingCost,
            ShippingCostActual = 0m,
            Status = TradeRouteStatus.InTransit,
            CreatedAtUtc = DateTime.UtcNow,
            DepartedAtUtc = DateTime.UtcNow,
        };

        db.InterCityTradeRoutes.Add(route);
        await db.SaveChangesAsync(cancellationToken);

        return CreateTradeRoutePayload.Success(route);
    }
}

/// <summary>Input for creating an inter-city trade route.</summary>
public record CreateTradeRouteInput(
    Guid CompanyId,
    Guid SourceBuildingId,
    Guid SourceBuildingUnitId,
    Guid DestinationBuildingId,
    Guid DestinationBuildingUnitId,
    Guid? ProductTypeId,
    Guid? ResourceTypeId,
    decimal Quantity,
    decimal PricePerUnit);

/// <summary>Payload returned by <see cref="Mutation.CreateTradeRoute"/>.</summary>
public sealed class CreateTradeRoutePayload
{
    public bool IsSuccess { get; private init; }
    public string? ErrorCode { get; private init; }
    public string? ErrorMessage { get; private init; }
    public InterCityTradeRoute? Route { get; private init; }

    public static CreateTradeRoutePayload Success(InterCityTradeRoute route) =>
        new() { IsSuccess = true, Route = route };

    public static CreateTradeRoutePayload Fail(string message, string code) =>
        new() { IsSuccess = false, ErrorMessage = message, ErrorCode = code };
}

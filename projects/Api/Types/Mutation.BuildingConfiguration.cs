using System.Globalization;
using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    /// <summary>Queues a building configuration update that becomes active after the required ticks have passed.</summary>
    [Authorize]
    public async Task<BuildingConfigurationPlan> StoreBuildingConfiguration(
        StoreBuildingConfigurationInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var gameState = await db.GameStates.FirstOrDefaultAsync()
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Game state is not initialized.")
                    .SetCode("GAME_STATE_NOT_FOUND")
                    .Build());

        await BuildingConfigurationService.ApplyDuePlansAsync(db, gameState.CurrentTick);

        var building = await db.Buildings
            .Include(candidate => candidate.Company)
            .Include(candidate => candidate.Units)
            .Include(candidate => candidate.PendingConfiguration)
            .ThenInclude(plan => plan!.Units)
            .Include(candidate => candidate.PendingConfiguration)
            .ThenInclude(plan => plan!.Removals)
            .AsSplitQuery()
            .FirstOrDefaultAsync(candidate => candidate.Id == input.BuildingId);

        if (building is null || building.Company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Building not found or you don't own it.")
                    .SetCode("BUILDING_NOT_FOUND")
                    .Build());
        }

        if (!BuildingConfigurationService.GetAllowedUnitTypes(building.Type).Any())
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("This building type does not support editable unit configurations.")
                    .SetCode("BUILDING_CONFIGURATION_NOT_SUPPORTED")
                    .Build());
        }

        var subscriptionEndsAtUtc = await db.Players
            .Where(player => player.Id == userId)
            .Select(player => player.ProSubscriptionEndsAtUtc)
            .FirstOrDefaultAsync();
        var hasActiveProSubscription = ProductAccessService.HasActiveProSubscription(subscriptionEndsAtUtc, DateTime.UtcNow);
        await EnsureSubmittedProductsAreAccessibleAsync(db, building, input.Units, hasActiveProSubscription);
        await ValidateMediaHouseReferencesAsync(db, building, input.Units);
        await ValidateProductTopologyAsync(db, building.Id, input.Units);

        var plan = await BuildingConfigurationService.StoreConfigurationAsync(db, building, input.Units, gameState.CurrentTick);
        await db.SaveChangesAsync();

        return await db.BuildingConfigurationPlans
            .Include(candidate => candidate.Units)
            .Include(candidate => candidate.Removals)
            .FirstAsync(candidate => candidate.Id == plan.Id);
    }

    /// <summary>Cancels a queued building configuration plan, reverting in-progress unit additions using roadmap-aligned rollback timing (10% of the original wait).</summary>
    [Authorize]
    public async Task<BuildingConfigurationPlan> CancelBuildingConfiguration(
        CancelBuildingConfigurationInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var gameState = await db.GameStates.FirstOrDefaultAsync()
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Game state is not initialized.")
                    .SetCode("GAME_STATE_NOT_FOUND")
                    .Build());

        await BuildingConfigurationService.ApplyDuePlansAsync(db, gameState.CurrentTick);

        var building = await db.Buildings
            .Include(candidate => candidate.Company)
            .Include(candidate => candidate.Units)
            .Include(candidate => candidate.PendingConfiguration)
            .ThenInclude(plan => plan!.Units)
            .Include(candidate => candidate.PendingConfiguration)
            .ThenInclude(plan => plan!.Removals)
            .AsSplitQuery()
            .FirstOrDefaultAsync(candidate => candidate.Id == input.BuildingId);

        if (building is null || building.Company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Building not found or you don't own it.")
                    .SetCode("BUILDING_NOT_FOUND")
                    .Build());
        }

        if (building.PendingConfiguration is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("This building does not have a pending configuration plan to cancel.")
                    .SetCode("NO_PENDING_CONFIGURATION")
                    .Build());
        }

        // Cancel by submitting the current active layout, which causes the service to schedule
        // rollback of any in-progress unit additions with 10% of the original wait time.
        var activeUnitInputs = building.Units
            .Select(unit => new BuildingConfigurationUnitInput
            {
                UnitType = unit.UnitType,
                GridX = unit.GridX,
                GridY = unit.GridY,
                LinkUp = unit.LinkUp,
                LinkDown = unit.LinkDown,
                LinkLeft = unit.LinkLeft,
                LinkRight = unit.LinkRight,
                LinkUpLeft = unit.LinkUpLeft,
                LinkUpRight = unit.LinkUpRight,
                LinkDownLeft = unit.LinkDownLeft,
                LinkDownRight = unit.LinkDownRight,
                ResourceTypeId = unit.ResourceTypeId,
                ProductTypeId = unit.ProductTypeId,
                MinPrice = unit.MinPrice,
                MaxPrice = unit.MaxPrice,
                PurchaseSource = unit.PurchaseSource,
                SaleVisibility = unit.SaleVisibility,
                Budget = unit.Budget,
                MediaHouseBuildingId = unit.MediaHouseBuildingId,
                MinQuality = unit.MinQuality,
                BrandScope = unit.BrandScope,
                VendorLockCompanyId = unit.VendorLockCompanyId,
                LockedCityId = unit.LockedCityId,
                IndustryCategory = unit.IndustryCategory,
            })
            .ToList();

        var plan = await BuildingConfigurationService.StoreConfigurationAsync(db, building, activeUnitInputs, gameState.CurrentTick);
        await db.SaveChangesAsync();

        return await db.BuildingConfigurationPlans
            .Include(candidate => candidate.Units)
            .Include(candidate => candidate.Removals)
            .FirstAsync(candidate => candidate.Id == plan.Id);
    }

    // ── Building Validation Helpers ───────────────────────────────────────────────

    private static async Task EnsureSubmittedProductsAreAccessibleAsync(
        AppDbContext db,
        Building building,
        IReadOnlyCollection<BuildingConfigurationUnitInput> submittedUnits,
        bool hasActiveProSubscription)
    {
        var submittedProductIds = submittedUnits
            .Where(unit => unit.ProductTypeId is not null)
            .Select(unit => unit.ProductTypeId!.Value)
            .Distinct()
            .ToList();

        if (submittedProductIds.Count == 0)
        {
            return;
        }

        var productsById = await db.ProductTypes
            .Where(product => submittedProductIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id);

        foreach (var unit in submittedUnits.Where(candidate => candidate.ProductTypeId is not null))
        {
            var productId = unit.ProductTypeId!.Value;
            if (!productsById.TryGetValue(productId, out var product))
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Product not found.")
                        .SetCode("INVALID_PRODUCT")
                        .Build());
            }

            if (!product.IsProOnly
                || hasActiveProSubscription
                || IsRetainingExistingProProduct(building, unit.UnitType, unit.GridX, unit.GridY, productId))
            {
                continue;
            }

            throw ProductAccessService.CreateProAccessException(product.Name);
        }
    }

    private static bool IsRetainingExistingProProduct(Building building, string unitType, int gridX, int gridY, Guid productTypeId)
    {
        return building.Units.Any(unit =>
                   unit.UnitType == unitType
                   && unit.GridX == gridX
                   && unit.GridY == gridY
                   && unit.ProductTypeId == productTypeId)
               || (building.PendingConfiguration?.Units.Any(unit =>
                   unit.UnitType == unitType
                    && unit.GridX == gridX
                    && unit.GridY == gridY
                    && unit.ProductTypeId == productTypeId) ?? false);
    }

    /// <summary>
    /// Validates that products assigned to B2B_SALES units are topologically
    /// reachable within the submitted configuration plan, or are already present
    /// in STORAGE inventory of this building.
    ///
    /// STORAGE units are universal — they accept any product and do not require
    /// a product assignment. Since a STORAGE unit can hold any product, a B2B_SALES
    /// unit may sell a product that is already stocked in a STORAGE unit in this
    /// building, even if no upstream producer is currently configured for that product.
    ///
    /// Rules:
    /// <list type="bullet">
    ///   <item>B2B_SALES: productTypeId must match a MANUFACTURING or PURCHASE unit in the
    ///   submitted plan, OR an existing product stocked in a STORAGE unit of this building.</item>
    /// </list>
    /// </summary>
    private static async Task ValidateProductTopologyAsync(
        AppDbContext db,
        Guid buildingId,
        IReadOnlyCollection<BuildingConfigurationUnitInput> submittedUnits)
    {
        // Gather product IDs configured on MANUFACTURING units in this plan.
        var mfgProductIds = submittedUnits
            .Where(u => u.UnitType == "MANUFACTURING" && u.ProductTypeId.HasValue)
            .Select(u => u.ProductTypeId!.Value)
            .ToHashSet();

        var purchaseProductIds = submittedUnits
            .Where(u => u.UnitType == "PURCHASE" && u.ProductTypeId.HasValue)
            .Select(u => u.ProductTypeId!.Value)
            .ToHashSet();

        // STORAGE units are universal — no product topology validation is applied.
        // However, products already stocked in STORAGE units of this building are
        // valid sources for downstream B2B_SALES (the warehouse holds real goods).

        // Validate B2B_SALES units.
        var b2bUnitsWithProduct = submittedUnits
            .Where(u => u.UnitType == "B2B_SALES" && u.ProductTypeId.HasValue)
            .ToList();

        if (b2bUnitsWithProduct.Count > 0)
        {
            // Allowed products for B2B_SALES:
            //   1. Products from MFG or PURCHASE units in this plan (live producers).
            //   2. Products already stocked in STORAGE units of this building
            //      (universal warehouse goods — the player owns the inventory).
            var allowedFromPlan = mfgProductIds.Union(purchaseProductIds).ToHashSet();

            // Query the IDs of STORAGE units currently in this building so we can
            // fetch their inventory.
            var storageUnitIds = await db.BuildingUnits
                .Where(u => u.BuildingId == buildingId && u.UnitType == "STORAGE")
                .Select(u => u.Id)
                .ToListAsync();

            // Collect product IDs from inventory of those STORAGE units (quantity > 0).
            var stockedInStorage = storageUnitIds.Count > 0
                ? await db.Inventories
                    .Where(inv => inv.BuildingId == buildingId
                        && inv.BuildingUnitId.HasValue
                        && storageUnitIds.Contains(inv.BuildingUnitId!.Value)
                        && inv.ProductTypeId.HasValue
                        && inv.Quantity > 0)
                    .Select(inv => inv.ProductTypeId!.Value)
                    .ToHashSetAsync()
                : [];

            var allowedForB2B = allowedFromPlan.Union(stockedInStorage).ToHashSet();

            foreach (var unit in b2bUnitsWithProduct)
            {
                var pid = unit.ProductTypeId!.Value;
                if (!allowedForB2B.Contains(pid))
                {
                    throw new GraphQLException(
                        ErrorBuilder.New()
                            .SetMessage(
                                "A B2B_SALES unit's product must match a MANUFACTURING or PURCHASE unit in this configuration, or be stocked in a STORAGE unit of this building.")
                            .SetCode("B2B_PRODUCT_NOT_REACHABLE")
                            .Build());
                }
            }
        }
    }

    /// <summary>
    /// Validates that any MediaHouseBuildingId on MARKETING units references an actual
    /// MEDIA_HOUSE building in the same city as the shop being configured.
    /// </summary>
    private static async Task ValidateMediaHouseReferencesAsync(
        AppDbContext db,
        Building building,
        IReadOnlyCollection<BuildingConfigurationUnitInput> submittedUnits)
    {
        var mediaHouseIds = submittedUnits
            .Where(u => u.UnitType == UnitType.Marketing && u.MediaHouseBuildingId.HasValue)
            .Select(u => u.MediaHouseBuildingId!.Value)
            .Distinct()
            .ToList();

        if (mediaHouseIds.Count == 0) return;

        foreach (var mediaHouseId in mediaHouseIds)
        {
            var mediaHouse = await db.Buildings
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == mediaHouseId);

            if (mediaHouse is null)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage($"Media house building {mediaHouseId} not found.")
                        .SetCode("MEDIA_HOUSE_NOT_FOUND")
                        .Build());
            }

            if (mediaHouse.Type != BuildingType.MediaHouse)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage($"Building {mediaHouseId} is not a media house.")
                        .SetCode("BUILDING_NOT_MEDIA_HOUSE")
                        .Build());
            }

            if (mediaHouse.CityId != building.CityId)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("The selected media house must be in the same city as the marketing building.")
                        .SetCode("MEDIA_HOUSE_WRONG_CITY")
                        .Build());
            }
        }
    }
}

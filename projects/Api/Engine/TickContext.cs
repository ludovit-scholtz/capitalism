using Api.Data;
using Api.Data.Entities;

namespace Api.Engine;

/// <summary>
/// Holds all pre-loaded game data for a single tick, indexed for O(1) lookups.
/// Created by <see cref="TickProcessor"/> at the start of each tick.
/// </summary>
public sealed class TickContext
{
    private static decimal RoundInventoryDecimal(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    public required AppDbContext Db { get; init; }
    public required GameState GameState { get; init; }
    public long CurrentTick => GameState.CurrentTick;

    // ── Indexed game data ──

    public Dictionary<Guid, Building> BuildingsById { get; init; } = [];
    public Dictionary<string, List<Building>> BuildingsByType { get; init; } = [];
    public Dictionary<Guid, List<BuildingUnit>> UnitsByBuilding { get; init; } = [];
    public Dictionary<Guid, Dictionary<(int GridX, int GridY), BuildingUnit>> UnitsByBuildingPosition { get; init; } = [];
    public Dictionary<Guid, List<Inventory>> InventoryByUnit { get; init; } = [];
    public Dictionary<Guid, List<Inventory>> InventoryByBuilding { get; init; } = [];
    public Dictionary<Guid, Company> CompaniesById { get; init; } = [];
    public Dictionary<Guid, City> CitiesById { get; init; } = [];
    public Dictionary<Guid, List<CityResource>> ResourcesByCity { get; init; } = [];
    public Dictionary<Guid, ResourceType> ResourceTypesById { get; init; } = [];
    public Dictionary<Guid, ProductType> ProductTypesById { get; init; } = [];
    public Dictionary<Guid, List<ProductRecipe>> RecipesByProduct { get; init; } = [];
    public Dictionary<Guid, List<Brand>> BrandsByCompany { get; init; } = [];
    public List<ExchangeOrder> ActiveExchangeOrders { get; init; } = [];

    // ── Pending mutations ──

    public List<Inventory> NewInventory { get; } = [];

    // ── Helpers ──

    /// <summary>
    /// Gets or creates a unit-level inventory row for the given resource or product.
    /// New rows are tracked in <see cref="NewInventory"/> for later persistence.
    /// </summary>
    public Inventory GetOrCreateUnitInventory(
        Guid buildingId, Guid unitId, Guid? resourceTypeId, Guid? productTypeId)
    {
        if (!InventoryByUnit.TryGetValue(unitId, out var unitInventories))
        {
            unitInventories = [];
            InventoryByUnit[unitId] = unitInventories;
        }

        var existing = unitInventories.FirstOrDefault(i =>
            i.ResourceTypeId == resourceTypeId && i.ProductTypeId == productTypeId);

        if (existing is not null) return existing;

        var inventory = new Inventory
        {
            Id = Guid.NewGuid(),
            BuildingId = buildingId,
            BuildingUnitId = unitId,
            ResourceTypeId = resourceTypeId,
            ProductTypeId = productTypeId,
            Quantity = 0m,
            SourcingCostTotal = 0m,
            Quality = 0.5m
        };

        unitInventories.Add(inventory);
        NewInventory.Add(inventory);

        if (!InventoryByBuilding.TryGetValue(buildingId, out var buildingInventories))
        {
            buildingInventories = [];
            InventoryByBuilding[buildingId] = buildingInventories;
        }
        buildingInventories.Add(inventory);

        return inventory;
    }

    /// <summary>Returns the total quantity stored in a unit across all item types.</summary>
    public decimal GetUnitCurrentLoad(Guid unitId)
    {
        if (!InventoryByUnit.TryGetValue(unitId, out var inventories)) return 0m;
        return inventories.Sum(i => i.Quantity);
    }

    /// <summary>Returns the remaining storage space in a unit based on its level.</summary>
    public decimal GetUnitFreeSpace(BuildingUnit unit)
    {
        var capacity = GameConstants.StorageCapacity(unit.Level);
        return Math.Max(0m, capacity - GetUnitCurrentLoad(unit.Id));
    }

    /// <summary>
    /// Removes quantity from an inventory row and returns the proportional
    /// sourcing cost carried by the withdrawn quantity.
    /// </summary>
    public (decimal Quantity, decimal SourcingCostTotal) WithdrawInventory(
        Inventory inventory,
        decimal requestedQuantity)
    {
        if (requestedQuantity <= 0m || inventory.Quantity <= 0m)
        {
            return (0m, 0m);
        }

        var actualQuantity = Math.Min(inventory.Quantity, requestedQuantity);
        var previousQuantity = inventory.Quantity;
        var costRemoved = previousQuantity > 0m && inventory.SourcingCostTotal > 0m
            ? RoundInventoryDecimal(inventory.SourcingCostTotal * (actualQuantity / previousQuantity))
            : 0m;

        inventory.Quantity = RoundInventoryDecimal(previousQuantity - actualQuantity);
        inventory.SourcingCostTotal = inventory.Quantity <= 0m
            ? 0m
            : Math.Max(0m, RoundInventoryDecimal(inventory.SourcingCostTotal - costRemoved));

        return (RoundInventoryDecimal(actualQuantity), costRemoved);
    }

    /// <summary>
    /// Adds quantity and sourcing cost to an inventory row while blending
    /// quality by quantity when a value is supplied.
    /// </summary>
    public void AddInventory(
        Inventory inventory,
        decimal quantity,
        decimal sourcingCostTotal,
        decimal? quality)
    {
        if (quantity <= 0m)
        {
            return;
        }

        var previousQuantity = inventory.Quantity;
        var newQuantity = RoundInventoryDecimal(previousQuantity + quantity);

        if (quality.HasValue && newQuantity > 0m)
        {
            inventory.Quality = previousQuantity > 0m
                ? RoundInventoryDecimal(((inventory.Quality * previousQuantity) + (quality.Value * quantity)) / newQuantity)
                : RoundInventoryDecimal(quality.Value);
        }

        inventory.Quantity = newQuantity;
        inventory.SourcingCostTotal = RoundInventoryDecimal(inventory.SourcingCostTotal + sourcingCostTotal);
    }

    /// <summary>Returns units that this unit pushes resources TO (outgoing links).</summary>
    public List<BuildingUnit> GetOutgoingLinkedUnits(BuildingUnit unit)
    {
        if (!UnitsByBuildingPosition.TryGetValue(unit.BuildingId, out var posMap))
            return [];

        var neighbors = new List<BuildingUnit>();
        if (unit.LinkRight && posMap.TryGetValue((unit.GridX + 1, unit.GridY), out var right))
            neighbors.Add(right);
        if (unit.LinkLeft && posMap.TryGetValue((unit.GridX - 1, unit.GridY), out var left))
            neighbors.Add(left);
        if (unit.LinkDown && posMap.TryGetValue((unit.GridX, unit.GridY + 1), out var down))
            neighbors.Add(down);
        if (unit.LinkUp && posMap.TryGetValue((unit.GridX, unit.GridY - 1), out var up))
            neighbors.Add(up);
        if (unit.LinkDownRight && posMap.TryGetValue((unit.GridX + 1, unit.GridY + 1), out var downRight))
            neighbors.Add(downRight);
        if (unit.LinkDownLeft && posMap.TryGetValue((unit.GridX - 1, unit.GridY + 1), out var downLeft))
            neighbors.Add(downLeft);
        if (unit.LinkUpRight && posMap.TryGetValue((unit.GridX + 1, unit.GridY - 1), out var upRight))
            neighbors.Add(upRight);
        if (unit.LinkUpLeft && posMap.TryGetValue((unit.GridX - 1, unit.GridY - 1), out var upLeft))
            neighbors.Add(upLeft);

        return neighbors;
    }

    /// <summary>Returns units that push resources INTO this unit (incoming links).</summary>
    public List<BuildingUnit> GetIncomingLinkedUnits(BuildingUnit unit)
    {
        if (!UnitsByBuildingPosition.TryGetValue(unit.BuildingId, out var posMap))
            return [];

        var incoming = new List<BuildingUnit>();
        if (posMap.TryGetValue((unit.GridX - 1, unit.GridY), out var left) && left.LinkRight)
            incoming.Add(left);
        if (posMap.TryGetValue((unit.GridX + 1, unit.GridY), out var right) && right.LinkLeft)
            incoming.Add(right);
        if (posMap.TryGetValue((unit.GridX, unit.GridY - 1), out var up) && up.LinkDown)
            incoming.Add(up);
        if (posMap.TryGetValue((unit.GridX, unit.GridY + 1), out var down) && down.LinkUp)
            incoming.Add(down);
        if (posMap.TryGetValue((unit.GridX - 1, unit.GridY - 1), out var upLeft) && upLeft.LinkDownRight)
            incoming.Add(upLeft);
        if (posMap.TryGetValue((unit.GridX + 1, unit.GridY - 1), out var upRight) && upRight.LinkDownLeft)
            incoming.Add(upRight);
        if (posMap.TryGetValue((unit.GridX - 1, unit.GridY + 1), out var downLeft) && downLeft.LinkUpRight)
            incoming.Add(downLeft);
        if (posMap.TryGetValue((unit.GridX + 1, unit.GridY + 1), out var downRight) && downRight.LinkUpLeft)
            incoming.Add(downRight);

        return incoming;
    }

    /// <summary>Finds the best matching brand for a company and product.</summary>
    public Brand? FindBrand(Guid companyId, Guid? productTypeId, string? industry)
    {
        if (!BrandsByCompany.TryGetValue(companyId, out var brands))
            return null;

        // Prefer product-specific, then category, then company-wide
        if (productTypeId.HasValue)
        {
            var productBrand = brands.FirstOrDefault(b =>
                b.Scope == BrandScope.Product && b.ProductTypeId == productTypeId);
            if (productBrand is not null) return productBrand;
        }

        if (!string.IsNullOrEmpty(industry))
        {
            var categoryBrand = brands.FirstOrDefault(b =>
                b.Scope == BrandScope.Category && b.IndustryCategory == industry);
            if (categoryBrand is not null) return categoryBrand;
        }

        return brands.FirstOrDefault(b => b.Scope == BrandScope.Company);
    }

    /// <summary>Finds or creates a brand for a company and product.</summary>
    public Brand GetOrCreateBrand(Guid companyId, Guid productTypeId, string brandName)
    {
        if (!BrandsByCompany.TryGetValue(companyId, out var brands))
        {
            brands = [];
            BrandsByCompany[companyId] = brands;
        }

        var existing = brands.FirstOrDefault(b =>
            b.Scope == BrandScope.Product && b.ProductTypeId == productTypeId);
        if (existing is not null) return existing;

        var brand = new Brand
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = brandName,
            Scope = BrandScope.Product,
            ProductTypeId = productTypeId,
            Awareness = 0m,
            Quality = 0m
        };
        brands.Add(brand);
        Db.Brands.Add(brand);
        return brand;
    }
}

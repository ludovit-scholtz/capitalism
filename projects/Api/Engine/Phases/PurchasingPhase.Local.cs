using Api.Data.Entities;
using Api.Utilities;

namespace Api.Engine.Phases;

public sealed partial class PurchasingPhase
{
    private static List<(Building Building, BuildingUnit Unit, Inventory Inventory, decimal PricePerUnit, decimal TransitCostPerUnit, decimal DeliveredPricePerUnit)> GetLocalB2BSupplies(
        TickContext context,
        Building destinationBuilding,
        BuildingUnit purchaseUnit,
        Guid? resourceId,
        Guid? productId,
        decimal itemWeightPerUnit,
        decimal maxPrice,
        decimal minQuality,
        decimal fuelPriceIndex = 1.0m,
        decimal commodityShockMultiplier = 1.0m)
    {
        var matchingSupplies = new List<(Building Building, BuildingUnit Unit, Inventory Inventory, decimal PricePerUnit, decimal TransitCostPerUnit, decimal DeliveredPricePerUnit)>();

        // Suppliers are the B2B units of one company in the destination's city: the vendor-locked company, or else the buyer's own.
        // Looked up in an index instead of scanning every unit of the world per purchase unit (#646).
        var supplierCompanyId = purchaseUnit.VendorLockCompanyId ?? destinationBuilding.CompanyId;

        foreach (var (building, unit) in context.GetB2BSellerUnits(destinationBuilding.CityId, supplierCompanyId))
        {
            if (building.Id == destinationBuilding.Id)
                continue;
            if (!context.InventoryByUnit.TryGetValue(unit.Id, out var inventories))
                continue;

            foreach (var inventory in inventories)
            {
                if (inventory.Quantity <= 0m)
                    continue;
                if (resourceId.HasValue && inventory.ResourceTypeId != resourceId)
                    continue;
                if (productId.HasValue && inventory.ProductTypeId != productId)
                    continue;
                if (resourceId is null && productId is null)
                    continue;
                if (inventory.Quality < minQuality)
                    continue;

                var basePrice = unit.MinPrice ?? GetBasePrice(context, inventory.ResourceTypeId, inventory.ProductTypeId);
                var price = resourceId.HasValue ? basePrice * commodityShockMultiplier : basePrice;
                var transitCostPerUnit = ComputeBuildingTransitCostPerUnit(building, destinationBuilding, itemWeightPerUnit, fuelPriceIndex);
                var deliveredPricePerUnit = price + transitCostPerUnit;
                if (price <= 0m || deliveredPricePerUnit > maxPrice)
                    continue;

                matchingSupplies.Add((building, unit, inventory, price, transitCostPerUnit, deliveredPricePerUnit));
            }
        }

        return matchingSupplies
            .OrderBy(supply => supply.DeliveredPricePerUnit)
            .ThenByDescending(supply => supply.Inventory.Quality)
            .ToList();
    }

    private static decimal GetBasePrice(TickContext context, Guid? resourceId, Guid? productId)
    {
        if (productId.HasValue && context.ProductTypesById.TryGetValue(productId.Value, out var productType))
            return productType.BasePrice;

        if (resourceId.HasValue && context.ResourceTypesById.TryGetValue(resourceId.Value, out var resourceType))
            return resourceType.BasePrice;

        return 0m;
    }

    private static decimal ComputeBuildingTransitCostPerUnit(
        Building sourceBuilding,
        Building destinationBuilding,
        decimal itemWeightPerUnit,
        decimal fuelPriceIndex = 1.0m)
    {
        // Delegate to the coordinate overload which applies: max(raw * fuel, minUnit)
        // Consistent with city-to-city path: max(raw * fuel, minCity).
        return GlobalExchangeCalculator.ComputeTransitCostPerUnit(
            sourceBuilding.Latitude,
            sourceBuilding.Longitude,
            destinationBuilding.Latitude,
            destinationBuilding.Longitude,
            itemWeightPerUnit,
            fuelPriceIndex);
    }
}

using Api.Data.Entities;
using Api.Utilities;

namespace Api.Engine.Phases;

/// <summary>
/// Fills PURCHASE units by matching against active exchange sell orders,
/// same-city B2B supply, and/or the city-level global exchange
/// (infinite counterparty).
/// Purchase source is controlled by <see cref="BuildingUnit.PurchaseSource"/>:
/// <list type="bullet">
///   <item><description>LOCAL  – player-placed exchange orders only.</description></item>
///   <item><description>EXCHANGE – global city exchange only (transit-cost-aware).</description></item>
///   <item><description>OPTIMAL (default) – best available price across both sources.</description></item>
/// </list>
/// Purchases respect the unit's MaxPrice (applied to the fully delivered price),
/// PurchaseCapacity, MinQuality, and optional vendor lock. Company cash is
/// debited for each fill.
/// </summary>
public sealed partial class PurchasingPhase : ITickPhase
{
    public string Name => "Purchasing";
    public int Order => 600;

    public Task ProcessAsync(TickContext context)
    {
        // Gather all purchase units across all buildings.
        foreach (var (buildingId, units) in context.UnitsByBuilding)
        {
            if (!context.BuildingsById.TryGetValue(buildingId, out var building))
                continue;
            if (!context.CompaniesById.TryGetValue(building.CompanyId, out var company))
                continue;
            // Skip buildings suspended for insufficient funds (evaluated by OperatingCostPhase).
            if (building.IsSuspendedForFunds) continue;

            foreach (var unit in units)
            {
                if (unit.UnitType != UnitType.Purchase) continue;
                if (context.UnitsUnderUpgrade.Contains(unit.Id)) continue;
                ProcessPurchaseUnit(context, building, unit, company);
            }
        }

        return Task.CompletedTask;
    }

    private static void ProcessPurchaseUnit(
        TickContext context,
        Building building,
        BuildingUnit unit,
        Company company)
    {
        var resourceId = unit.ResourceTypeId;
        var productId = unit.ProductTypeId;
        if (resourceId is null && productId is null) return;

        var capacity = GameConstants.PurchaseCapacity(unit.Level);
        var space = context.GetUnitFreeSpace(unit);
        var maxBuy = Math.Min(capacity, space);
        if (maxBuy <= 0m) return;

        var maxPrice = unit.MaxPrice ?? decimal.MaxValue;
        var minQuality = unit.MinQuality ?? 0m;
        var purchaseSource = unit.PurchaseSource ?? "OPTIMAL";
        var commodityShockMultiplier = context.GetCommodityShockMultiplier(resourceId);
        var fundingAccount = context.GetBuildingFundingAccount(building);
        if (fundingAccount is null)
        {
            return;
        }
        var itemWeightPerUnit = GlobalExchangeCalculator.ComputeItemWeightPerUnit(
            resourceId,
            productId,
            context.ResourceTypesById,
            context.ProductTypesById,
            context.RecipesByProduct);

        var totalBought = 0m;
        var totalSourcingCost = 0m;
        var weightedQualityTotal = 0m;

        // Get destination city fuel price index to scale transport costs.
        var destFuelPriceIndex = context.CitiesById.TryGetValue(building.CityId, out var destCity)
            ? destCity.FuelPriceIndex
            : 1.0m;

        // Phase 1: Player-placed exchange orders (LOCAL or OPTIMAL source).
        if (purchaseSource is "LOCAL" or "OPTIMAL")
        {
            var matchingOrders = context.ActiveExchangeOrders
                .Where(o => o.Side == "SELL"
                            && o.IsActive
                            && o.RemainingQuantity > 0m
                            && (resourceId is null || o.ResourceTypeId == resourceId)
                            && (productId is null || o.ProductTypeId == productId))
                .Where(o =>
                {
                    if (!context.BuildingsById.TryGetValue(o.ExchangeBuildingId, out var exchangeBuilding))
                    {
                        return false;
                    }

                    var adjustedOrderPrice = o.PricePerUnit * commodityShockMultiplier;
                    var deliveredPricePerUnit = adjustedOrderPrice + ComputeBuildingTransitCostPerUnit(exchangeBuilding, building, itemWeightPerUnit, destFuelPriceIndex);
                    return deliveredPricePerUnit <= maxPrice;
                })
                .OrderBy(o =>
                {
                    var exchangeBuilding = context.BuildingsById[o.ExchangeBuildingId];
                    var adjustedOrderPrice = o.PricePerUnit * commodityShockMultiplier;
                    return adjustedOrderPrice + ComputeBuildingTransitCostPerUnit(exchangeBuilding, building, itemWeightPerUnit, destFuelPriceIndex);
                })
                .ToList();

            if (unit.VendorLockCompanyId.HasValue)
            {
                matchingOrders = matchingOrders
                    .Where(o => o.CompanyId == unit.VendorLockCompanyId.Value)
                    .ToList();
            }

            foreach (var order in matchingOrders)
            {
                if (totalBought >= maxBuy) break;

                var want = maxBuy - totalBought;
                var available = order.RemainingQuantity;
                var fill = Math.Min(want, available);
                if (!context.BuildingsById.TryGetValue(order.ExchangeBuildingId, out var exchangeBuilding))
                {
                    continue;
                }

                var adjustedOrderPrice = order.PricePerUnit * commodityShockMultiplier;
                var transitCostPerUnit = ComputeBuildingTransitCostPerUnit(exchangeBuilding, building, itemWeightPerUnit, destFuelPriceIndex);
                var goodsCost = fill * adjustedOrderPrice;
                var shippingCost = fill * transitCostPerUnit;
                var totalDeliveredCost = goodsCost + shippingCost;

                if (fundingAccount.Balance < totalDeliveredCost)
                {
                    var deliveredPricePerUnit = adjustedOrderPrice + transitCostPerUnit;
                    fill = fundingAccount.Balance / deliveredPricePerUnit;
                    fill = Math.Floor(fill * 10000m) / 10000m;
                    goodsCost = fill * adjustedOrderPrice;
                    shippingCost = fill * transitCostPerUnit;
                    totalDeliveredCost = goodsCost + shippingCost;
                }

                if (fill <= 0m) break;

                order.RemainingQuantity -= fill;
                if (order.RemainingQuantity <= 0m) order.IsActive = false;

                fundingAccount.Balance -= totalDeliveredCost;

                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    BuildingId = building.Id,
                    BuildingUnitId = unit.Id,
                    Category = LedgerCategory.PurchasingCost,
                    Description = resourceId.HasValue ? "Purchase: raw material" : "Purchase: product",
                    Amount = -goodsCost,
                    RecordedAtTick = context.CurrentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                    ResourceTypeId = resourceId,
                    ProductTypeId = productId,
                });

                if (shippingCost > 0m)
                {
                    context.Db.LedgerEntries.Add(new LedgerEntry
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = company.Id,
                        BuildingId = building.Id,
                        BuildingUnitId = unit.Id,
                        Category = LedgerCategory.ShippingCost,
                        Description = resourceId.HasValue ? "Shipping: exchange order raw material" : "Shipping: exchange order product",
                        Amount = -shippingCost,
                        RecordedAtTick = context.CurrentTick,
                        RecordedAtUtc = DateTime.UtcNow,
                        ResourceTypeId = resourceId,
                        ProductTypeId = productId,
                    });
                }

                // Credit selling company.
                var sellerAccount = context.GetBuildingFundingAccount(exchangeBuilding);
                if (sellerAccount is not null)
                {
                    sellerAccount.Balance += goodsCost;
                }

                totalBought += fill;
                totalSourcingCost += totalDeliveredCost;
                weightedQualityTotal += fill * 0.7m;
            }
        }

        if (purchaseSource is "LOCAL" or "OPTIMAL" && totalBought < maxBuy)
        {
            var localSupplies = GetLocalB2BSupplies(
                context,
                building,
                unit,
                resourceId,
                productId,
                itemWeightPerUnit,
                maxPrice,
                minQuality,
                destFuelPriceIndex,
                commodityShockMultiplier);

            foreach (var supply in localSupplies)
            {
                if (totalBought >= maxBuy) break;

                var wanted = maxBuy - totalBought;
                var eligibleTickStartQuantity = context.GetTickStartRemainingQuantity(supply.Inventory);
                if (eligibleTickStartQuantity <= 0m)
                    continue;

                var fill = Math.Min(wanted, eligibleTickStartQuantity);
                if (fill <= 0m) continue;

                var sellerIsSameCompany = supply.Building.CompanyId == company.Id;
                decimal goodsCost = fill * supply.PricePerUnit;
                decimal shippingCost = fill * supply.TransitCostPerUnit;
                decimal deliveredCost = goodsCost + shippingCost;
                if (fundingAccount.Balance < shippingCost)
                {
                    fill = fundingAccount.Balance / Math.Max(supply.TransitCostPerUnit, 0.0001m);
                    fill = Math.Floor(fill * 10000m) / 10000m;
                    goodsCost = fill * supply.PricePerUnit;
                    shippingCost = fill * supply.TransitCostPerUnit;
                    deliveredCost = goodsCost + shippingCost;
                }

                if (!sellerIsSameCompany)
                {
                    if (fundingAccount.Balance < deliveredCost)
                    {
                        fill = fundingAccount.Balance / supply.DeliveredPricePerUnit;
                        fill = Math.Floor(fill * 10000m) / 10000m;
                        goodsCost = fill * supply.PricePerUnit;
                        shippingCost = fill * supply.TransitCostPerUnit;
                        deliveredCost = goodsCost + shippingCost;
                    }
                }

                if (fill <= 0m) break;

                var withdrawn = context.WithdrawInventory(supply.Inventory, fill);
                if (withdrawn.Quantity <= 0m) continue;

                if (supply.Inventory.BuildingUnitId.HasValue)
                {
                    context.RecordUnitResourceHistory(
                        supply.Building.Id,
                        supply.Inventory.BuildingUnitId.Value,
                        supply.Inventory.ResourceTypeId,
                        supply.Inventory.ProductTypeId,
                        outflowQuantity: withdrawn.Quantity);
                }

                if (sellerIsSameCompany)
                {
                    // Internal transfer: record as sale for seller and purchase for buyer
                    context.Db.LedgerEntries.Add(new LedgerEntry
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = supply.Building.CompanyId, // Seller
                        BuildingId = supply.Building.Id,
                        BuildingUnitId = supply.Unit.Id,
                        Category = LedgerCategory.Revenue,
                        Description = resourceId.HasValue ? "Internal sale: raw material" : "Internal sale: product",
                        Amount = goodsCost,
                        RecordedAtTick = context.CurrentTick,
                        RecordedAtUtc = DateTime.UtcNow,
                        ResourceTypeId = resourceId,
                        ProductTypeId = productId,
                    });

                    context.Db.LedgerEntries.Add(new LedgerEntry
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = company.Id, // Buyer
                        BuildingId = building.Id,
                        BuildingUnitId = unit.Id,
                        Category = LedgerCategory.PurchasingCost,
                        Description = resourceId.HasValue ? "Internal purchase: raw material" : "Internal purchase: product",
                        Amount = -goodsCost,
                        RecordedAtTick = context.CurrentTick,
                        RecordedAtUtc = DateTime.UtcNow,
                        ResourceTypeId = resourceId,
                        ProductTypeId = productId,
                    });

                    if (shippingCost > 0m)
                    {
                        fundingAccount.Balance -= shippingCost;
                        context.Db.LedgerEntries.Add(new LedgerEntry
                        {
                            Id = Guid.NewGuid(),
                            CompanyId = company.Id,
                            BuildingId = building.Id,
                            BuildingUnitId = unit.Id,
                            Category = LedgerCategory.ShippingCost,
                            Description = resourceId.HasValue ? "Shipping: internal raw material transfer" : "Shipping: internal product transfer",
                            Amount = -shippingCost,
                            RecordedAtTick = context.CurrentTick,
                            RecordedAtUtc = DateTime.UtcNow,
                            ResourceTypeId = resourceId,
                            ProductTypeId = productId,
                        });
                    }

                    totalSourcingCost += deliveredCost;
                }
                else
                {
                    goodsCost = withdrawn.Quantity * supply.PricePerUnit;
                    shippingCost = withdrawn.Quantity * supply.TransitCostPerUnit;
                    deliveredCost = goodsCost + shippingCost;
                    fundingAccount.Balance -= deliveredCost;

                    var sellerAccount = context.GetBuildingFundingAccount(supply.Building);
                    if (sellerAccount is not null)
                    {
                        sellerAccount.Balance += goodsCost;
                    }

                    context.Db.LedgerEntries.Add(new LedgerEntry
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = company.Id,
                        BuildingId = building.Id,
                        BuildingUnitId = unit.Id,
                        Category = LedgerCategory.PurchasingCost,
                        Description = resourceId.HasValue ? "Purchase: local raw material" : "Purchase: local product",
                        Amount = -goodsCost,
                        RecordedAtTick = context.CurrentTick,
                        RecordedAtUtc = DateTime.UtcNow,
                        ResourceTypeId = resourceId,
                        ProductTypeId = productId,
                    });

                    if (shippingCost > 0m)
                    {
                        context.Db.LedgerEntries.Add(new LedgerEntry
                        {
                            Id = Guid.NewGuid(),
                            CompanyId = company.Id,
                            BuildingId = building.Id,
                            BuildingUnitId = unit.Id,
                            Category = LedgerCategory.ShippingCost,
                            Description = resourceId.HasValue ? "Shipping: local raw material" : "Shipping: local product",
                            Amount = -shippingCost,
                            RecordedAtTick = context.CurrentTick,
                            RecordedAtUtc = DateTime.UtcNow,
                            ResourceTypeId = resourceId,
                            ProductTypeId = productId,
                        });
                    }

                    if (context.CompaniesById.TryGetValue(supply.Building.CompanyId, out var sellerCompany)
                        && sellerCompany.PlayerId != company.PlayerId)
                    {
                        var itemName = resourceId.HasValue && context.ResourceTypesById.TryGetValue(resourceId.Value, out var resource)
                            ? resource.Name
                            : (productId.HasValue && context.ProductTypesById.TryGetValue(productId.Value, out var product)
                                ? product.Name
                                : "item");

                        PlayerNotificationService.Add(
                            context.Db,
                            sellerCompany.PlayerId,
                            PlayerNotificationType.B2BSaleFulfilled,
                            "B2B sale fulfilled",
                            $"{company.Name} bought {withdrawn.Quantity:0.####} {itemName} from {supply.Building.Name}.",
                            context.CurrentTick,
                            sellerCompany.Id,
                            supply.Building.Id,
                            supply.Unit.Id,
                            bankAccountId: sellerAccount?.Id);
                    }

                    totalSourcingCost += deliveredCost;
                }

                totalBought += withdrawn.Quantity;
                weightedQualityTotal += withdrawn.Quantity * supply.Inventory.Quality;
            }
        }

        // Phase 2: Global city exchange (infinite counterparty) for EXCHANGE or OPTIMAL source.
        // Only applies to raw resource purchases (not products) in the first version.
        if (purchaseSource is "EXCHANGE" or "OPTIMAL" && resourceId.HasValue && totalBought < maxBuy)
        {
            var remaining = maxBuy - totalBought;
            var (globalBought, globalQuality, globalCost) = BuyFromGlobalExchange(
                context, building, unit, company, fundingAccount, resourceId.Value,
                remaining, maxPrice, minQuality);

            if (globalBought > 0m)
            {
                totalBought += globalBought;
                totalSourcingCost += globalCost;
                weightedQualityTotal += globalBought * globalQuality;
            }
        }

        if (totalBought > 0m)
        {
            var inv = context.GetOrCreateUnitInventory(
                building.Id, unit.Id, resourceId, productId);
            var inventoryQuality = weightedQualityTotal > 0m ? weightedQualityTotal / totalBought : (decimal?)null;
            context.AddInventory(inv, totalBought, totalSourcingCost, inventoryQuality);
            context.RecordUnitResourceHistory(
                building.Id,
                unit.Id,
                resourceId,
                productId,
                inflowQuantity: totalBought);
        }
    }

}

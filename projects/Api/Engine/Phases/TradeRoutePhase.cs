using Api.Data.Entities;
using Api.Engine;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine.Phases;

/// <summary>
/// Processes inter-city trade routes each tick.
/// Routes start in IN_TRANSIT state (inventory is deducted at creation time via mutation).
/// This phase handles delivery: when a route's ExpectedArrivalTick is reached,
/// it tries to push the inventory into the destination purchase unit.
/// If the unit has space the route is DELIVERED (payment settled, shipping cost deducted).
/// If the unit is full the route is FAILED and inventory is returned to the source building.
/// </summary>
public sealed class TradeRoutePhase : ITickPhase
{
    public string Name => "TradeRoutes";

    /// <summary>
    /// Runs after purchasing (600) so that local purchases don't pre-fill destination
    /// units before an arriving inter-city delivery is evaluated.
    /// </summary>
    public int Order => 650;

    public Task ProcessAsync(TickContext context)
    {
        // Load all in-transit routes whose arrival tick has been reached.
        var arrivingRoutes = context.Db.InterCityTradeRoutes
            .Where(r => r.Status == TradeRouteStatus.InTransit
                        && r.ExpectedArrivalTick <= context.CurrentTick)
            .ToList();

        foreach (var route in arrivingRoutes)
        {
            ProcessArrival(context, route);
        }

        return Task.CompletedTask;
    }

    private static void ProcessArrival(TickContext context, InterCityTradeRoute route)
    {
        // Source building must exist.
        if (!context.BuildingsById.TryGetValue(route.SourceBuildingId, out var sourceBuilding))
        {
            FailRoute(context, route, "Source building not found.");
            return;
        }

        // Destination building must exist and not be suspended.
        if (!context.BuildingsById.TryGetValue(route.DestinationBuildingId, out var destBuilding))
        {
            ReturnInventoryToSource(context, route, sourceBuilding);
            FailRoute(context, route, "Destination building not found.");
            return;
        }
        if (destBuilding.IsSuspendedForFunds)
        {
            ReturnInventoryToSource(context, route, sourceBuilding);
            FailRoute(context, route, "Destination building suspended for insufficient funds.");
            return;
        }

        // Destination purchase unit must exist and not be under upgrade.
        if (!context.UnitsByBuilding.TryGetValue(destBuilding.Id, out var destUnits))
        {
            ReturnInventoryToSource(context, route, sourceBuilding);
            FailRoute(context, route, "No units found in destination building.");
            return;
        }
        var destUnit = destUnits.FirstOrDefault(u => u.Id == route.DestinationBuildingUnitId);
        if (destUnit is null || context.UnitsUnderUpgrade.Contains(destUnit.Id))
        {
            ReturnInventoryToSource(context, route, sourceBuilding);
            FailRoute(context, route, "Destination unit unavailable or under upgrade.");
            return;
        }

        // Check if destination unit has sufficient free space.
        var freeSpace = context.GetUnitReceivingSpace(destUnit, route.ResourceTypeId, route.ProductTypeId);
        if (freeSpace < route.Quantity)
        {
            // Partial delivery not supported; return all goods to source.
            ReturnInventoryToSource(context, route, sourceBuilding);
            FailRoute(context, route, $"Destination unit full (free space={freeSpace:F2}, needed={route.Quantity:F2}).");
            return;
        }

        // Deliver inventory to destination unit.
        var destInventory = context.GetOrCreateUnitInventory(
            destBuilding.Id,
            destUnit.Id,
            route.ResourceTypeId,
            route.ProductTypeId);
        context.AddInventory(destInventory, route.Quantity, route.SourcingCostTotal, route.Quality);

        // Record inflow history on the destination unit.
        context.RecordUnitResourceHistory(
            destBuilding.Id, destUnit.Id,
            route.ResourceTypeId, route.ProductTypeId,
            inflowQuantity: route.Quantity);

        // Settle payment: debit buyer, credit seller.
        var totalRevenue = route.Quantity * route.PricePerUnit;
        SettlePayment(context, route, sourceBuilding, destBuilding, totalRevenue);

        // Mark delivered.
        route.Status = TradeRouteStatus.Delivered;
        route.CompletedAtUtc = DateTime.UtcNow;

        EmitDeliveryNotifications(context, route, sourceBuilding, destBuilding);
    }

    private static void ReturnInventoryToSource(TickContext context, InterCityTradeRoute route, Building sourceBuilding)
    {
        if (!context.UnitsByBuilding.TryGetValue(sourceBuilding.Id, out var sourceUnits))
            return;

        var sourceUnit = sourceUnits.FirstOrDefault(u => u.Id == route.SourceBuildingUnitId);
        if (sourceUnit is null)
            return;

        var sourceInventory = context.GetOrCreateUnitInventory(
            sourceBuilding.Id,
            sourceUnit.Id,
            route.ResourceTypeId,
            route.ProductTypeId);
        context.AddInventory(sourceInventory, route.Quantity, route.SourcingCostTotal, route.Quality);

        context.RecordUnitResourceHistory(
            sourceBuilding.Id, sourceUnit.Id,
            route.ResourceTypeId, route.ProductTypeId,
            inflowQuantity: route.Quantity);
    }

    private static void SettlePayment(
        TickContext context,
        InterCityTradeRoute route,
        Building sourceBuilding,
        Building destBuilding,
        decimal totalRevenue)
    {
        if (!context.CompaniesById.TryGetValue(route.CompanyId, out var sellerCompany))
            return;

        // Get destination company for payment.
        if (!context.CompaniesById.TryGetValue(destBuilding.CompanyId, out var buyerCompany))
            return;

        // Attempt to debit buyer for the goods; if insufficient funds, skip payment
        // (goods are still delivered – the buyer receives inventory on credit in this
        // implementation, matching the existing B2B sales phase behaviour).
        var buyerFundingAccount = context.GetBuildingFundingAccount(destBuilding);
        bool paymentSucceeded;
        if (buyerFundingAccount is not null && buyerFundingAccount.Balance >= totalRevenue)
        {
            buyerFundingAccount.Balance -= totalRevenue;
            paymentSucceeded = true;
        }
        else
        {
            paymentSucceeded = CompanyBankingService.TryDebit(
                context.GetCompanyBankAccounts(buyerCompany.Id), totalRevenue);
        }

        // Credit seller only when the buyer was successfully debited.
        if (paymentSucceeded && totalRevenue > 0m)
        {
            var sellerAccount = context.GetBuildingFundingAccount(sourceBuilding);
            if (sellerAccount is not null)
            {
                sellerAccount.Balance += totalRevenue;
            }
            else
            {
                CompanyBankingService.TryCredit(context.GetCompanyBankAccounts(sellerCompany.Id), totalRevenue, null, out _);
            }
        }

        var sellerAccount2 = context.GetBuildingFundingAccount(sourceBuilding);

        // Calculate and deduct actual shipping cost from seller.
        var itemWeight = ComputeItemWeight(context, route.ResourceTypeId, route.ProductTypeId);
        var destCity = context.CitiesById.GetValueOrDefault(destBuilding.CityId);
        var fuelIndex = destCity?.FuelPriceIndex ?? 1.0m;
        var shippingCostPerUnit = GlobalExchangeCalculator.ComputeTransitCostPerUnit(
            sourceBuilding.Latitude, sourceBuilding.Longitude,
            destBuilding.Latitude, destBuilding.Longitude,
            itemWeight, fuelIndex);
        var actualShippingCost = shippingCostPerUnit * route.Quantity * context.GlobalEventTradeRouteMultiplier;

        route.ShippingCostActual = actualShippingCost;

        // Deduct shipping cost from seller account.
        if (sellerAccount2 is not null && sellerAccount2.Balance >= actualShippingCost)
        {
            sellerAccount2.Balance -= actualShippingCost;
        }
        else
        {
            CompanyBankingService.TryDebit(context.GetCompanyBankAccounts(sellerCompany.Id), actualShippingCost);
        }

        // Ledger: buyer purchasing cost.
        if (totalRevenue > 0m)
        {
            context.Db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = buyerCompany.Id,
                BuildingId = destBuilding.Id,
                Category = LedgerCategory.PurchasingCost,
                Description = $"Inter-city trade route delivery from {sellerCompany.Name}",
                Amount = -totalRevenue,
                RecordedAtTick = context.CurrentTick,
                RecordedAtUtc = DateTime.UtcNow,
                ProductTypeId = route.ProductTypeId,
                ResourceTypeId = route.ResourceTypeId,
            });

            // Ledger: seller revenue.
            context.Db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = sellerCompany.Id,
                BuildingId = route.SourceBuildingId,
                Category = LedgerCategory.Revenue,
                Description = $"Inter-city trade route sale to {buyerCompany.Name}",
                Amount = totalRevenue,
                RecordedAtTick = context.CurrentTick,
                RecordedAtUtc = DateTime.UtcNow,
                ProductTypeId = route.ProductTypeId,
                ResourceTypeId = route.ResourceTypeId,
            });
        }

        // Ledger: seller shipping cost.
        if (actualShippingCost > 0m)
        {
            context.Db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = sellerCompany.Id,
                BuildingId = route.SourceBuildingId,
                Category = LedgerCategory.ShippingCost,
                Description = $"Inter-city shipping cost to {destBuilding.Name ?? destCity?.Name ?? "destination"}",
                Amount = -actualShippingCost,
                RecordedAtTick = context.CurrentTick,
                RecordedAtUtc = DateTime.UtcNow,
            });
        }
    }

    private static void FailRoute(TickContext context, InterCityTradeRoute route, string reason)
    {
        route.Status = TradeRouteStatus.Failed;
        route.FailureReason = reason;
        route.CompletedAtUtc = DateTime.UtcNow;
    }

    private static void EmitDeliveryNotifications(
        TickContext context,
        InterCityTradeRoute route,
        Building sourceBuilding,
        Building destBuilding)
    {
        if (!context.CompaniesById.TryGetValue(route.CompanyId, out var sellerCompany))
            return;

        var itemName = route.ResourceTypeId.HasValue && context.ResourceTypesById.TryGetValue(route.ResourceTypeId.Value, out var resource)
            ? resource.Name
            : (route.ProductTypeId.HasValue && context.ProductTypesById.TryGetValue(route.ProductTypeId.Value, out var product)
                ? product.Name
                : "item");

        var shipmentMessage = $"Shipment arrived at {destBuilding.Name}: {route.Quantity:0.####} {itemName} from {sourceBuilding.Name}.";
        PlayerNotificationService.Add(
            context.Db,
            sellerCompany.PlayerId,
            PlayerNotificationType.ShipmentArrived,
            "Shipment arrived",
            shipmentMessage,
            context.CurrentTick,
            sellerCompany.Id,
            destBuilding.Id,
            route.DestinationBuildingUnitId,
            bankAccountId: destBuilding.BankAccountId);

        if (context.CompaniesById.TryGetValue(destBuilding.CompanyId, out var buyerCompany)
            && buyerCompany.PlayerId != sellerCompany.PlayerId)
        {
            PlayerNotificationService.Add(
                context.Db,
                buyerCompany.PlayerId,
                PlayerNotificationType.ShipmentArrived,
                "Shipment arrived",
                $"{sellerCompany.Name} delivered {route.Quantity:0.####} {itemName} to {destBuilding.Name}.",
                context.CurrentTick,
                buyerCompany.Id,
                destBuilding.Id,
                route.DestinationBuildingUnitId,
                bankAccountId: destBuilding.BankAccountId);
        }

        if (route.Quantity <= 0m || route.PricePerUnit <= 0m || route.ShippingCostActual <= 0m)
            return;

        var shippingCostPerUnit = route.ShippingCostActual / route.Quantity;
        var shippingCostShare = shippingCostPerUnit / route.PricePerUnit;
        const decimal marginErosionThreshold = 0.15m;
        if (shippingCostShare <= marginErosionThreshold)
            return;

        PlayerNotificationService.Add(
            context.Db,
            sellerCompany.PlayerId,
            PlayerNotificationType.LogisticsMarginErosion,
            "Logistics cost warning",
            $"Shipping cost reached {(shippingCostShare * 100m):0.#}% of sale price on shipment to {destBuilding.Name}.",
            context.CurrentTick,
            sellerCompany.Id,
            sourceBuilding.Id,
            route.SourceBuildingUnitId,
            bankAccountId: sourceBuilding.BankAccountId);
    }

    private static decimal ComputeItemWeight(
        TickContext context,
        Guid? resourceTypeId,
        Guid? productTypeId)
        => GlobalExchangeCalculator.ComputeItemWeightPerUnit(
            resourceTypeId,
            productTypeId,
            context.ResourceTypesById,
            context.ProductTypesById,
            context.RecipesByProduct);

    /// <summary>
    /// Computes the number of ticks for a cross-city shipment.
    /// Based on road distance: 1 tick per 200 km, minimum 1.
    /// </summary>
    public static long ComputeTransitTicks(double distanceKm)
        => Math.Max(1L, (long)Math.Round(distanceKm / 200.0));
}

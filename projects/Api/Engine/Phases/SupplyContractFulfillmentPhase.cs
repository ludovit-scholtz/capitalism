using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine.Phases;

/// <summary>
/// Executes active long-term supply contracts after mining and before manufacturing.
/// </summary>
public sealed class SupplyContractFulfillmentPhase : ITickPhase
{
    public string Name => "SupplyContractFulfillment";
    public int Order => 620;

    public async Task ProcessAsync(TickContext context)
    {
        var activeContracts = await context.Db.SupplyContracts
            .Where(contract =>
                contract.Status == SupplyContractStatus.Active
                && contract.StartTick <= context.CurrentTick
                && contract.RemainingTicks > 0)
            .ToListAsync();

        foreach (var contract in activeContracts)
        {
            ProcessContract(context, contract);
        }
    }

    private static void ProcessContract(TickContext context, SupplyContract contract)
    {
        if (!context.CompaniesById.TryGetValue(contract.SellerCompanyId, out var sellerCompany)
            || !context.CompaniesById.TryGetValue(contract.BuyerCompanyId, out var buyerCompany))
        {
            contract.Status = SupplyContractStatus.Breached;
            return;
        }

        if (!TryResolveSource(context, contract, out var sourceBuilding, out var sourceUnit, out var sourceInventory))
        {
            ApplyUnderdelivery(context, contract, sellerCompany, buyerCompany, contract.QuantityPerTick, "Source B2B unit or inventory unavailable.");
            FinalizeTick(contract, context.CurrentTick);
            return;
        }

        var buyerPurchaseUnit = ResolveBuyerPurchaseUnit(context, buyerCompany.Id, contract.ResourceTypeId, contract.ProductTypeId);
        if (buyerPurchaseUnit is null)
        {
            ApplyUnderdelivery(context, contract, sellerCompany, buyerCompany, contract.QuantityPerTick, "Buyer has no matching purchase unit.");
            FinalizeTick(contract, context.CurrentTick);
            return;
        }

        if (!context.BuildingsById.TryGetValue(buyerPurchaseUnit.BuildingId, out var buyerBuilding))
        {
            ApplyUnderdelivery(context, contract, sellerCompany, buyerCompany, contract.QuantityPerTick, "Buyer purchase building unavailable.");
            FinalizeTick(contract, context.CurrentTick);
            return;
        }

        var receivingSpace = context.GetUnitReceivingSpace(buyerPurchaseUnit, contract.ResourceTypeId, contract.ProductTypeId);
        var requestedQuantity = contract.QuantityPerTick;
        var deliveredQuantity = Math.Min(requestedQuantity, Math.Min(sourceInventory.Quantity, receivingSpace));

        decimal withdrawnCost = 0m;
        if (deliveredQuantity > 0m)
        {
            var withdrawn = context.WithdrawInventory(sourceInventory, deliveredQuantity);
            deliveredQuantity = withdrawn.Quantity;
            withdrawnCost = withdrawn.SourcingCostTotal;

            var buyerInventory = context.GetOrCreateUnitInventory(
                buyerBuilding.Id,
                buyerPurchaseUnit.Id,
                contract.ResourceTypeId,
                contract.ProductTypeId);
            context.AddInventory(buyerInventory, deliveredQuantity, withdrawnCost, sourceInventory.Quality);
            context.RecordUnitResourceHistory(
                buyerBuilding.Id,
                buyerPurchaseUnit.Id,
                contract.ResourceTypeId,
                contract.ProductTypeId,
                inflowQuantity: deliveredQuantity);
        }

        if (deliveredQuantity > 0m)
        {
            var settlement = decimal.Round(deliveredQuantity * contract.PricePerUnit, 4, MidpointRounding.AwayFromZero);
            var buyerDebited = CompanyBankingService.TryDebit(context.GetCompanyBankAccounts(buyerCompany.Id), settlement);
            if (buyerDebited)
            {
                CompanyBankingService.TryCredit(context.GetCompanyBankAccounts(sellerCompany.Id), settlement, null, out _);

                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = sellerCompany.Id,
                    BuildingId = sourceBuilding.Id,
                    Category = LedgerCategory.SupplyContractRevenue,
                    Description = $"Supply contract delivery to {buyerCompany.Name}",
                    Amount = settlement,
                    RecordedAtTick = context.CurrentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                    ResourceTypeId = contract.ResourceTypeId,
                    ProductTypeId = contract.ProductTypeId,
                });
                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = buyerCompany.Id,
                    BuildingId = buyerBuilding.Id,
                    Category = LedgerCategory.SupplyContractPayment,
                    Description = $"Supply contract purchase from {sellerCompany.Name}",
                    Amount = -settlement,
                    RecordedAtTick = context.CurrentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                    ResourceTypeId = contract.ResourceTypeId,
                    ProductTypeId = contract.ProductTypeId,
                });
            }
        }

        contract.TotalDeliveredQuantity += deliveredQuantity;
        if (!contract.FirstDeliveryNotified && deliveredQuantity > 0m)
        {
            contract.FirstDeliveryNotified = true;
            PlayerNotificationService.Add(
                context.Db,
                sellerCompany.PlayerId,
                PlayerNotificationType.SupplyContractFirstDelivery,
                "First supply contract delivery completed",
                $"{buyerCompany.Name} received the first scheduled shipment.",
                context.CurrentTick,
                sellerCompany.Id,
                relatedEntityType: "SUPPLY_CONTRACT",
                relatedEntityId: contract.Id);
            if (buyerCompany.PlayerId != sellerCompany.PlayerId)
            {
                PlayerNotificationService.Add(
                    context.Db,
                    buyerCompany.PlayerId,
                    PlayerNotificationType.SupplyContractFirstDelivery,
                    "First supply contract delivery completed",
                    $"{sellerCompany.Name} delivered the first scheduled shipment.",
                    context.CurrentTick,
                    buyerCompany.Id,
                    relatedEntityType: "SUPPLY_CONTRACT",
                    relatedEntityId: contract.Id);
            }
        }

        var missed = decimal.Round(Math.Max(0m, requestedQuantity - deliveredQuantity), 4, MidpointRounding.AwayFromZero);
        if (missed > 0m)
        {
            ApplyUnderdelivery(context, contract, sellerCompany, buyerCompany, missed, "Scheduled quantity was only partially delivered.");
        }

        FinalizeTick(contract, context.CurrentTick);
        if (contract.RemainingTicks == 0)
        {
            contract.Status = SupplyContractStatus.Fulfilled;
            contract.CompletedAtTick = context.CurrentTick;
            contract.CompletedAtUtc = DateTime.UtcNow;
            PlayerNotificationService.Add(context.Db, sellerCompany.PlayerId, PlayerNotificationType.SupplyContractFulfilled, "Supply contract fulfilled", "A supply contract reached its final delivery tick.", context.CurrentTick, sellerCompany.Id, relatedEntityType: "SUPPLY_CONTRACT", relatedEntityId: contract.Id);
            if (sellerCompany.PlayerId != buyerCompany.PlayerId)
            {
                PlayerNotificationService.Add(context.Db, buyerCompany.PlayerId, PlayerNotificationType.SupplyContractFulfilled, "Supply contract fulfilled", "A supply contract reached its final delivery tick.", context.CurrentTick, buyerCompany.Id, relatedEntityType: "SUPPLY_CONTRACT", relatedEntityId: contract.Id);
            }
        }
    }

    private static void ApplyUnderdelivery(TickContext context, SupplyContract contract, Company sellerCompany, Company buyerCompany, decimal missedQuantity, string reason)
    {
        contract.TotalUndeliveredQuantity += missedQuantity;
        var penalty = decimal.Round(missedQuantity * contract.PricePerUnit * (contract.PenaltyRatePercent / 100m), 4, MidpointRounding.AwayFromZero);
        if (penalty <= 0m)
        {
            return;
        }

        // Clamp the penalty to the seller's available balance and record only the amount
        // actually paid so an under-funded seller cannot credit the buyer with phantom cash.
        var availablePenaltyFunds = CompanyBankingService.GetAvailableBalance(context.GetCompanyBankAccounts(sellerCompany.Id));
        var penaltyPaid = Math.Min(availablePenaltyFunds, penalty);
        if (penaltyPaid <= 0m || !CompanyBankingService.TryDebit(context.GetCompanyBankAccounts(sellerCompany.Id), penaltyPaid))
        {
            return;
        }

        CompanyBankingService.TryCredit(context.GetCompanyBankAccounts(buyerCompany.Id), penaltyPaid, null, out _);
        contract.TotalPenaltyAmount += penaltyPaid;
        contract.PenaltyCount++;

        context.Db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = sellerCompany.Id,
            Category = LedgerCategory.SupplyContractPenalty,
            Description = $"Under-delivery penalty paid: {reason}",
            Amount = -penaltyPaid,
            RecordedAtTick = context.CurrentTick,
            RecordedAtUtc = DateTime.UtcNow,
            ResourceTypeId = contract.ResourceTypeId,
            ProductTypeId = contract.ProductTypeId,
        });
        context.Db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = buyerCompany.Id,
            Category = LedgerCategory.SupplyContractPenalty,
            Description = $"Under-delivery compensation received: {reason}",
            Amount = penaltyPaid,
            RecordedAtTick = context.CurrentTick,
            RecordedAtUtc = DateTime.UtcNow,
            ResourceTypeId = contract.ResourceTypeId,
            ProductTypeId = contract.ProductTypeId,
        });

        PlayerNotificationService.Add(
            context.Db,
            sellerCompany.PlayerId,
            PlayerNotificationType.SupplyContractPenalty,
            "Supply contract penalty applied",
            $"Penalty applied for under-delivery: {penaltyPaid:N2} {contract.CurrencyCode}.",
            context.CurrentTick,
            sellerCompany.Id,
            relatedEntityType: "SUPPLY_CONTRACT",
            relatedEntityId: contract.Id);
        if (sellerCompany.PlayerId != buyerCompany.PlayerId)
        {
            PlayerNotificationService.Add(
                context.Db,
                buyerCompany.PlayerId,
                PlayerNotificationType.SupplyContractPenalty,
                "Supply contract penalty applied",
                $"Penalty credit received: {penaltyPaid:N2} {contract.CurrencyCode}.",
                context.CurrentTick,
                buyerCompany.Id,
                relatedEntityType: "SUPPLY_CONTRACT",
                relatedEntityId: contract.Id);
        }
    }

    private static void FinalizeTick(SupplyContract contract, long currentTick)
    {
        contract.RemainingTicks = Math.Max(0, contract.RemainingTicks - 1);
        if (contract.RemainingTicks == 0 && contract.Status == SupplyContractStatus.Active)
        {
            contract.Status = SupplyContractStatus.Fulfilled;
        }

        contract.CompletedAtTick ??= contract.Status == SupplyContractStatus.Fulfilled ? currentTick : null;
        contract.CompletedAtUtc ??= contract.Status == SupplyContractStatus.Fulfilled ? DateTime.UtcNow : null;
    }

    private static bool TryResolveSource(TickContext context, SupplyContract contract, out Building sourceBuilding, out BuildingUnit sourceUnit, out Inventory sourceInventory)
    {
        sourceBuilding = null!;
        sourceUnit = null!;
        sourceInventory = null!;

        sourceUnit = context.UnitsByBuilding.Values.SelectMany(units => units).FirstOrDefault(unit => unit.Id == contract.SellerBuildingUnitId && unit.UnitType == UnitType.B2BSales)!;
        if (sourceUnit is null)
        {
            return false;
        }

        if (!context.BuildingsById.TryGetValue(sourceUnit.BuildingId, out var resolvedBuilding))
        {
            return false;
        }

        sourceBuilding = resolvedBuilding;
        sourceInventory = context.GetOrCreateUnitInventory(sourceBuilding.Id, sourceUnit.Id, contract.ResourceTypeId, contract.ProductTypeId);
        return true;
    }

    private static BuildingUnit? ResolveBuyerPurchaseUnit(TickContext context, Guid buyerCompanyId, Guid? resourceTypeId, Guid? productTypeId)
    {
        return context.UnitsByBuilding.Values
            .SelectMany(units => units)
            .Where(unit => unit.UnitType == UnitType.Purchase)
            .FirstOrDefault(unit =>
                context.BuildingsById.TryGetValue(unit.BuildingId, out var building)
                && building.CompanyId == buyerCompanyId
                && (resourceTypeId.HasValue ? unit.ResourceTypeId == resourceTypeId : unit.ProductTypeId == productTypeId));
    }
}

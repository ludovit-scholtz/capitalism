using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using HotChocolate;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    [Authorize]
    public async Task<ContractBidResult> SubmitContractBid(
        SubmitContractBidInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == input.CompanyId && candidate.PlayerId == userId, cancellationToken)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(ObjectAuthorizationService.FriendlyMessage)
                    .SetCode(ObjectAuthorizationService.NotFoundOrNotOwnedCode)
                    .Build());

        var contract = await db.GovernmentContracts
            .Include(candidate => candidate.ProductType)
            .FirstOrDefaultAsync(candidate => candidate.Id == input.ContractId, cancellationToken)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Government contract not found.")
                    .SetCode("CONTRACT_NOT_FOUND")
                    .Build());

        var currentTick = await db.GameStates.AsNoTracking().Select(state => state.CurrentTick).FirstOrDefaultDeterministicAsync(cancellationToken);
        if (contract.Status != GovernmentContractStatus.Open)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Bidding is closed for this contract.")
                    .SetCode("CONTRACT_NOT_OPEN")
                    .Build());
        }

        if (currentTick >= contract.DeadlineTick)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Contract deadline already passed.")
                    .SetCode("CONTRACT_DEADLINE_PASSED")
                    .Build());
        }

        if (input.BidPricePerUnit <= 0m || input.BidPricePerUnit > contract.BudgetCap)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Bid price must be positive and below contract budget cap.")
                    .SetCode("BID_PRICE_INVALID")
                    .Build());
        }

        if (input.EstimatedDeliveryTick <= currentTick)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Estimated delivery tick must be in the future.")
                    .SetCode("DELIVERY_TICK_INVALID")
                    .Build());
        }

        var eligibility = await GovernmentContractService.EvaluateCompanyEligibilityAsync(db, contract, company.Id, cancellationToken);
        if (!eligibility.IsEligible)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(eligibility.ReasonMessage ?? "Company is not eligible for this contract.")
                    .SetCode(eligibility.ReasonCode ?? "CONTRACT_ELIGIBILITY_FAILED")
                    .Build());
        }

        var existingBid = await db.ContractBids.FirstOrDefaultAsync(
            bid => bid.ContractId == contract.Id && bid.CompanyId == company.Id,
            cancellationToken);
        if (existingBid is null)
        {
            existingBid = new ContractBid
            {
                Id = Guid.NewGuid(),
                ContractId = contract.Id,
                CompanyId = company.Id,
                SubmittedAtTick = currentTick,
                SubmittedAtUtc = DateTime.UtcNow,
            };
            db.ContractBids.Add(existingBid);
        }
        else
        {
            existingBid.SubmittedAtTick = currentTick;
            existingBid.SubmittedAtUtc = DateTime.UtcNow;
        }

        existingBid.BidPricePerUnit = decimal.Round(input.BidPricePerUnit, 2, MidpointRounding.AwayFromZero);
        existingBid.EstimatedDeliveryTick = input.EstimatedDeliveryTick;

        await db.SaveChangesAsync(cancellationToken);

        return new ContractBidResult
        {
            Id = existingBid.Id,
            ContractId = existingBid.ContractId,
            CompanyId = existingBid.CompanyId,
            CompanyName = company.Name,
            BidPricePerUnit = existingBid.BidPricePerUnit,
            EstimatedDeliveryTick = existingBid.EstimatedDeliveryTick,
            SubmittedAtTick = existingBid.SubmittedAtTick,
            ContractStatus = contract.Status,
        };
    }

    [Authorize]
    public async Task<ContractFulfillmentResult> FulfillContractShipment(
        FulfillContractShipmentInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var contract = await db.GovernmentContracts
            .Include(item => item.City)
            .Include(item => item.ProductType)
            .Include(item => item.Fulfillment)
            .Include(item => item.Bids)
            .FirstOrDefaultAsync(item => item.Id == input.ContractId, cancellationToken)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Government contract not found.")
                    .SetCode("CONTRACT_NOT_FOUND")
                    .Build());

        if (contract.Status != GovernmentContractStatus.Awarded || !contract.WinnerCompanyId.HasValue)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Contract is not in awarded state.")
                    .SetCode("CONTRACT_NOT_AWARDED")
                    .Build());
        }

        var winnerCompany = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(company => company.Id == contract.WinnerCompanyId.Value && company.PlayerId == userId, cancellationToken)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(ObjectAuthorizationService.FriendlyMessage)
                    .SetCode(ObjectAuthorizationService.NotFoundOrNotOwnedCode)
                    .Build());

        var quantity = decimal.Round(input.Quantity, 4, MidpointRounding.AwayFromZero);
        if (quantity <= 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Shipment quantity must be greater than zero.")
                    .SetCode("INVALID_QUANTITY")
                    .Build());
        }

        var fulfillment = contract.Fulfillment ?? new ContractFulfillment
        {
            Id = Guid.NewGuid(),
            ContractId = contract.Id,
            CompanyId = winnerCompany.Id,
            QuantityDelivered = 0m,
            QuantityRequired = contract.QuantityRequired,
            CreatedAtUtc = DateTime.UtcNow,
        };
        if (contract.Fulfillment is null)
        {
            db.ContractFulfillments.Add(fulfillment);
            contract.Fulfillment = fulfillment;
        }

        var remainingBefore = Math.Max(0m, contract.QuantityRequired - fulfillment.QuantityDelivered);
        var shipmentQuantity = Math.Min(quantity, remainingBefore);
        if (shipmentQuantity <= 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Contract is already fully fulfilled.")
                    .SetCode("CONTRACT_ALREADY_FULFILLED")
                    .Build());
        }

        var eligibleInventories = await db.Inventories
            .Include(inventory => inventory.Building)
            .Where(inventory =>
                inventory.ProductTypeId == contract.ProductTypeId
                && inventory.Quantity > 0m
                && inventory.Building.CompanyId == winnerCompany.Id
                && inventory.Building.CityId == contract.CityId
                && inventory.Building.DestroyedAtUtc == null)
            .OrderByDescending(inventory => inventory.Quantity)
            .ToListAsync(cancellationToken);

        var availableQuantity = eligibleInventories.Sum(inventory => inventory.Quantity);
        if (availableQuantity < shipmentQuantity)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Not enough inventory in the contract city to fulfill this shipment.")
                    .SetCode("INSUFFICIENT_CONTRACT_INVENTORY")
                    .Build());
        }

        var remainingToDeduct = shipmentQuantity;
        foreach (var inventory in eligibleInventories)
        {
            if (remainingToDeduct <= 0m)
            {
                break;
            }

            var deduction = Math.Min(remainingToDeduct, inventory.Quantity);
            inventory.Quantity -= deduction;
            remainingToDeduct -= deduction;
        }

        var currentTick = await db.GameStates.AsNoTracking().Select(state => state.CurrentTick).FirstOrDefaultDeterministicAsync(cancellationToken);
        fulfillment.QuantityDelivered = decimal.Round(fulfillment.QuantityDelivered + shipmentQuantity, 4, MidpointRounding.AwayFromZero);
        fulfillment.LastShipmentTick = currentTick;

        decimal? settledRevenue = null;
        var latePenaltyApplied = false;
        if (fulfillment.QuantityDelivered >= contract.QuantityRequired)
        {
            fulfillment.CompletedAtUtc = DateTime.UtcNow;
            contract.Status = GovernmentContractStatus.Fulfilled;

            var winningBid = contract.Bids.FirstOrDefault(bid => bid.CompanyId == winnerCompany.Id);
            var grossRevenue = decimal.Round((winningBid?.BidPricePerUnit ?? contract.BudgetCap) * contract.QuantityRequired, 2, MidpointRounding.AwayFromZero);
            var netRevenue = grossRevenue;
            if (currentTick > contract.DeadlineTick)
            {
                latePenaltyApplied = true;
                netRevenue = decimal.Round(grossRevenue * (1m - GameConstants.GovernmentContractLatePenaltyRate), 2, MidpointRounding.AwayFromZero);
            }

            var settlementAccount = await CompanyBankingService.EnsurePreferredAccountAsync(db, winnerCompany.Id, contract.City.CurrencyCode);
            settlementAccount.Balance += netRevenue;
            settlementAccount.ConcurrencyToken = Guid.NewGuid();
            settledRevenue = netRevenue;

            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = winnerCompany.Id,
                BankAccountId = settlementAccount.Id,
                Category = LedgerCategory.GovernmentContractRevenue,
                Description = $"Government contract fulfilled: {contract.Title}",
                Amount = netRevenue,
                RecordedAtTick = currentTick,
                RecordedAtUtc = DateTime.UtcNow,
                ProductTypeId = contract.ProductTypeId,
            });

            PlayerNotificationService.Add(
                db,
                winnerCompany.PlayerId,
                PlayerNotificationType.ContractFulfillmentComplete,
                "Government contract completed",
                $"Contract '{contract.Title}' has been fulfilled. Revenue credited: {netRevenue:N2} {contract.City.CurrencyCode}.",
                currentTick,
                winnerCompany.Id,
                severity: PlayerNotificationSeverity.Info,
                relatedEntityType: "GOVERNMENT_CONTRACT",
                relatedEntityId: contract.Id);
        }

        await db.SaveChangesAsync(cancellationToken);

        var fulfillmentPercent = contract.QuantityRequired > 0m
            ? decimal.Round(Math.Clamp(fulfillment.QuantityDelivered / contract.QuantityRequired, 0m, 1m) * 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;

        return new ContractFulfillmentResult
        {
            ContractId = contract.Id,
            Status = contract.Status,
            QuantityDelivered = fulfillment.QuantityDelivered,
            QuantityRequired = contract.QuantityRequired,
            FulfillmentPercent = fulfillmentPercent,
            SettledRevenue = settledRevenue,
            LatePenaltyApplied = latePenaltyApplied,
        };
    }

    [Authorize]
    public async Task<int> GenerateGovernmentContracts(
        GenerateGovernmentContractsInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] GameAdminAuthorizationService gameAdminAuthorizationService,
        CancellationToken cancellationToken)
    {
        await gameAdminAuthorizationService.RequireAdminDashboardAccessAsync(
            db,
            httpContextAccessor.HttpContext!.User,
            cancellationToken);

        var currentTick = await db.GameStates.AsNoTracking().Select(state => state.CurrentTick).FirstOrDefaultDeterministicAsync(cancellationToken);
        var cities = await db.Cities
            .AsNoTracking()
            .Where(city => !input.CityId.HasValue || city.Id == input.CityId.Value)
            .ToListAsync(cancellationToken);
        var productTypes = await db.ProductTypes.AsNoTracking().Where(product => !product.IsProOnly).ToListAsync(cancellationToken);
        if (cities.Count == 0 || productTypes.Count == 0)
        {
            return 0;
        }

        var safeCount = Math.Clamp(input.CountPerCity <= 0 ? 1 : input.CountPerCity, 1, 5);
        var created = 0;
        foreach (var city in cities)
        {
            for (var i = 0; i < safeCount; i++)
            {
                db.GovernmentContracts.Add(GovernmentContractService.CreateContract(city, productTypes, currentTick, i));
                created++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return created;
    }
}

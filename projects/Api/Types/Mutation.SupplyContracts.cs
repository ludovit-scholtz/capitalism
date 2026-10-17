using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    /// <summary>Floor for the early-cancellation fee rate so a contract written with a 0% penalty cannot be exited for free.</summary>
    internal const decimal MinEarlyCancellationFeePercent = 5m;

    [Authorize]
    public async Task<SupplyContractActionResult> ProposeSupplyContract(
        ProposeSupplyContractInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ObjectAuthorizationService objectAuthorization,
        CancellationToken cancellationToken)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var sellerCompany = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(company => company.Id == input.SellerCompanyId, cancellationToken);
        if (sellerCompany is null || sellerCompany.PlayerId != userId)
        {
            throw objectAuthorization.Deny(
                userId,
                "company",
                input.SellerCompanyId,
                sellerCompany is null ? ObjectAuthorizationService.NotFoundReason : ObjectAuthorizationService.NotOwnedReason);
        }

        var buyerCompany = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(company => company.Id == input.BuyerCompanyId, cancellationToken)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Buyer company was not found.")
                    .SetCode("BUYER_NOT_FOUND")
                    .Build());

        var sourceUnit = await db.BuildingUnits
            .Include(unit => unit.Building)
            .FirstOrDefaultAsync(unit =>
                unit.Id == input.SellerBuildingUnitId
                && unit.Building.CompanyId == input.SellerCompanyId
                && unit.UnitType == UnitType.B2BSales,
                cancellationToken)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Seller must select a B2B sales unit owned by the seller company.")
                    .SetCode("INVALID_SELLER_UNIT")
                    .Build());

        if ((input.ResourceTypeId.HasValue && input.ProductTypeId.HasValue) || (!input.ResourceTypeId.HasValue && !input.ProductTypeId.HasValue))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Contract must target either one resource or one product.")
                    .SetCode("INVALID_CONTRACT_ITEM")
                    .Build());
        }

        var quantityPerTick = decimal.Round(input.QuantityPerTick, 4, MidpointRounding.AwayFromZero);
        var pricePerUnit = decimal.Round(input.PricePerUnit, 4, MidpointRounding.AwayFromZero);
        if (quantityPerTick <= 0m || pricePerUnit <= 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Quantity per tick and price per unit must be greater than zero.")
                    .SetCode("INVALID_CONTRACT_VALUES")
                    .Build());
        }

        var durationTicks = Math.Clamp(input.DurationTicks, 25, 500);
        var penaltyRate = Math.Clamp(input.PenaltyRatePercent, 0m, 100m);
        var currentTick = await GetCurrentTickAsync(db);
        var currencyCode = sourceUnit.Building.CityId != Guid.Empty
            ? await db.Cities.Where(city => city.Id == sourceUnit.Building.CityId).Select(city => city.CurrencyCode).FirstAsync(cancellationToken)
            : "EUR";

        var contract = new SupplyContract
        {
            Id = Guid.NewGuid(),
            SellerCompanyId = sellerCompany.Id,
            BuyerCompanyId = buyerCompany.Id,
            SellerBuildingUnitId = sourceUnit.Id,
            ResourceTypeId = input.ResourceTypeId,
            ProductTypeId = input.ProductTypeId,
            QuantityPerTick = quantityPerTick,
            PricePerUnit = pricePerUnit,
            DurationTicks = durationTicks,
            RemainingTicks = durationTicks,
            StartTick = currentTick + 1,
            PenaltyRatePercent = penaltyRate,
            CurrencyCode = currencyCode,
            Status = SupplyContractStatus.Pending,
            CreatedAtTick = currentTick,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.SupplyContracts.Add(contract);

        PlayerNotificationService.Add(
            db,
            buyerCompany.PlayerId,
            PlayerNotificationType.SupplyContractProposed,
            "Supply contract proposed",
            $"{sellerCompany.Name} proposed a long-term supply contract.",
            currentTick,
            buyerCompany.Id,
            relatedEntityType: "SUPPLY_CONTRACT",
            relatedEntityId: contract.Id);

        await SaveContractChangesAsync(db, cancellationToken);

        var populated = await LoadSupplyContractAsync(db, contract.Id, cancellationToken);
        return new SupplyContractActionResult
        {
            Success = true,
            Message = "Contract offer created.",
            Contract = Query.MapSupplyContractCard(populated),
        };
    }

    [Authorize]
    public async Task<SupplyContractActionResult> CounterProposeSupplyContract(
        Guid id,
        CounterProposeSupplyContractInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ObjectAuthorizationService objectAuthorization,
        CancellationToken cancellationToken)
    {
        var contract = await EnsurePendingCounterpartyContractAsync(id, db, httpContextAccessor, objectAuthorization, cancellationToken);

        if (input.QuantityPerTick.HasValue)
        {
            var value = decimal.Round(input.QuantityPerTick.Value, 4, MidpointRounding.AwayFromZero);
            if (value <= 0m)
            {
                throw new GraphQLException(ErrorBuilder.New().SetMessage("Quantity must be greater than zero.").SetCode("INVALID_CONTRACT_VALUES").Build());
            }

            contract.QuantityPerTick = value;
        }

        if (input.PricePerUnit.HasValue)
        {
            var value = decimal.Round(input.PricePerUnit.Value, 4, MidpointRounding.AwayFromZero);
            if (value <= 0m)
            {
                throw new GraphQLException(ErrorBuilder.New().SetMessage("Price must be greater than zero.").SetCode("INVALID_CONTRACT_VALUES").Build());
            }

            contract.PricePerUnit = value;
        }

        if (input.DurationTicks.HasValue)
        {
            contract.DurationTicks = Math.Clamp(input.DurationTicks.Value, 25, 500);
            contract.RemainingTicks = contract.DurationTicks;
        }

        if (input.PenaltyRatePercent.HasValue)
        {
            contract.PenaltyRatePercent = Math.Clamp(input.PenaltyRatePercent.Value, 0m, 100m);
        }

        var currentTick = await GetCurrentTickAsync(db);
        contract.CreatedAtTick = currentTick;
        contract.CreatedAtUtc = DateTime.UtcNow;

        var sellerPlayerId = await db.Companies.Where(company => company.Id == contract.SellerCompanyId).Select(company => company.PlayerId).FirstAsync(cancellationToken);
        var buyerPlayerId = await db.Companies.Where(company => company.Id == contract.BuyerCompanyId).Select(company => company.PlayerId).FirstAsync(cancellationToken);
        var actorId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var targetPlayer = actorId == sellerPlayerId ? buyerPlayerId : sellerPlayerId;
        PlayerNotificationService.Add(
            db,
            targetPlayer,
            PlayerNotificationType.SupplyContractProposed,
            "Supply contract counter proposal",
            "A counter proposal was submitted for your supply contract.",
            currentTick,
            relatedEntityType: "SUPPLY_CONTRACT",
            relatedEntityId: contract.Id);

        await SaveContractChangesAsync(db, cancellationToken);
        var populated = await LoadSupplyContractAsync(db, contract.Id, cancellationToken);
        return new SupplyContractActionResult
        {
            Success = true,
            Message = "Counter proposal submitted.",
            Contract = Query.MapSupplyContractCard(populated),
        };
    }

    [Authorize]
    public async Task<SupplyContractActionResult> AcceptSupplyContract(
        Guid id,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ObjectAuthorizationService objectAuthorization,
        CancellationToken cancellationToken)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var contract = await db.SupplyContracts
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw objectAuthorization.Deny(userId, "supply_contract", id, ObjectAuthorizationService.NotFoundReason);

        var buyerCompany = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(company => company.Id == contract.BuyerCompanyId, cancellationToken);
        if (buyerCompany is null || buyerCompany.PlayerId != userId)
        {
            throw objectAuthorization.Deny(
                userId,
                "company",
                contract.BuyerCompanyId,
                buyerCompany is null ? ObjectAuthorizationService.NotFoundReason : ObjectAuthorizationService.NotOwnedReason);
        }

        if (contract.Status != SupplyContractStatus.Pending)
        {
            throw new GraphQLException(ErrorBuilder.New().SetMessage("Only pending contracts can be accepted.").SetCode("SUPPLY_CONTRACT_NOT_PENDING").Build());
        }

        var hasPurchaseUnit = await db.BuildingUnits
            .Include(unit => unit.Building)
            .AnyAsync(unit =>
                unit.Building.CompanyId == buyerCompany.Id
                && unit.UnitType == UnitType.Purchase
                && (contract.ResourceTypeId.HasValue ? unit.ResourceTypeId == contract.ResourceTypeId : unit.ProductTypeId == contract.ProductTypeId),
                cancellationToken);

        if (!hasPurchaseUnit)
        {
            throw new GraphQLException(ErrorBuilder.New().SetMessage("Buyer company must have a matching purchase unit before accepting.").SetCode("MISSING_PURCHASE_UNIT").Build());
        }

        var currentTick = await GetCurrentTickAsync(db);
        contract.Status = SupplyContractStatus.Active;
        contract.ActivatedAtTick = currentTick;
        contract.ActivatedAtUtc = DateTime.UtcNow;
        contract.StartTick = currentTick + 1;

        var sellerCompany = await db.Companies.AsNoTracking().FirstAsync(company => company.Id == contract.SellerCompanyId, cancellationToken);
        PlayerNotificationService.Add(
            db,
            sellerCompany.PlayerId,
            PlayerNotificationType.SupplyContractAccepted,
            "Supply contract accepted",
            $"{buyerCompany.Name} accepted your supply contract.",
            currentTick,
            contract.SellerCompanyId,
            relatedEntityType: "SUPPLY_CONTRACT",
            relatedEntityId: contract.Id);

        await SaveContractChangesAsync(db, cancellationToken);
        var populated = await LoadSupplyContractAsync(db, contract.Id, cancellationToken);
        return new SupplyContractActionResult
        {
            Success = true,
            Message = "Contract accepted.",
            Contract = Query.MapSupplyContractCard(populated),
        };
    }

    [Authorize]
    public async Task<SupplyContractActionResult> RejectSupplyContract(
        Guid id,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ObjectAuthorizationService objectAuthorization,
        CancellationToken cancellationToken)
    {
        var contract = await EnsurePendingCounterpartyContractAsync(id, db, httpContextAccessor, objectAuthorization, cancellationToken);
        var currentTick = await GetCurrentTickAsync(db);
        contract.Status = SupplyContractStatus.Cancelled;
        contract.CancelledAtTick = currentTick;
        contract.CancelledAtUtc = DateTime.UtcNow;

        var sellerCompany = await db.Companies.AsNoTracking().FirstAsync(company => company.Id == contract.SellerCompanyId, cancellationToken);
        var buyerCompany = await db.Companies.AsNoTracking().FirstAsync(company => company.Id == contract.BuyerCompanyId, cancellationToken);
        var actorId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var targetPlayerId = actorId == sellerCompany.PlayerId ? buyerCompany.PlayerId : sellerCompany.PlayerId;

        PlayerNotificationService.Add(
            db,
            targetPlayerId,
            PlayerNotificationType.SupplyContractRejected,
            "Supply contract rejected",
            "A supply contract offer was rejected.",
            currentTick,
            relatedEntityType: "SUPPLY_CONTRACT",
            relatedEntityId: contract.Id);

        await SaveContractChangesAsync(db, cancellationToken);
        var populated = await LoadSupplyContractAsync(db, contract.Id, cancellationToken);
        return new SupplyContractActionResult
        {
            Success = true,
            Message = "Contract rejected.",
            Contract = Query.MapSupplyContractCard(populated),
        };
    }

    [Authorize]
    public async Task<SupplyContractActionResult> CancelSupplyContract(
        Guid id,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ObjectAuthorizationService objectAuthorization,
        CancellationToken cancellationToken)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var contract = await db.SupplyContracts
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw objectAuthorization.Deny(userId, "supply_contract", id, ObjectAuthorizationService.NotFoundReason);

        // A player who owns both sides acts as the buyer, so the role (and whether a fee applies) never depends on row order.
        var actorCompany = await db.Companies
            .AsNoTracking()
            .Where(company => company.PlayerId == userId && (company.Id == contract.SellerCompanyId || company.Id == contract.BuyerCompanyId))
            .OrderBy(company => company.Id == contract.BuyerCompanyId ? 0 : 1)
            .Select(company => new { company.Id, company.PlayerId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw objectAuthorization.Deny(userId, "supply_contract", id, ObjectAuthorizationService.NotOwnedReason);

        if (contract.Status is SupplyContractStatus.Fulfilled or SupplyContractStatus.Cancelled or SupplyContractStatus.Breached)
        {
            throw new GraphQLException(ErrorBuilder.New().SetMessage("Contract is already closed.").SetCode("SUPPLY_CONTRACT_CLOSED").Build());
        }

        var currentTick = await GetCurrentTickAsync(db);
        var wasActive = contract.Status == SupplyContractStatus.Active;
        var buyerPaysFee = wasActive && actorCompany.Id == contract.BuyerCompanyId;

        // An early cancellation by the buyer costs real money. Settle it before closing the contract so a
        // buyer who cannot pay is rejected instead of walking away for free.
        var feeDebits = new List<(BankAccount Account, decimal Amount)>();
        BankAccount? feeReceiverAccount = null;
        var cancellationFee = 0m;
        if (buyerPaysFee)
        {
            // A player who owns both sides has no counterparty to compensate, so no fee moves between their own companies.
            var sameOwner = await db.Companies
                .AsNoTracking()
                .Where(company => company.Id == contract.BuyerCompanyId || company.Id == contract.SellerCompanyId)
                .Select(company => company.PlayerId)
                .Distinct()
                .CountAsync(cancellationToken) == 1;
            var feeRatePercent = Math.Max(contract.PenaltyRatePercent, MinEarlyCancellationFeePercent);
            cancellationFee = sameOwner
                ? 0m
                : decimal.Round(contract.QuantityPerTick * contract.PricePerUnit * (feeRatePercent / 100m), 4, MidpointRounding.AwayFromZero);
            var accounts = await db.BankAccounts
                .Where(account => account.CompanyId == contract.BuyerCompanyId || account.CompanyId == contract.SellerCompanyId)
                .ToListAsync(cancellationToken);
            var buyerAccounts = accounts.Where(account => account.CompanyId == contract.BuyerCompanyId).ToList();
            var sellerAccounts = accounts.Where(account => account.CompanyId == contract.SellerCompanyId).ToList();
            feeReceiverAccount = CompanyBankingService.FindPreferredAccount(sellerAccounts, contract.CurrencyCode);
            if (cancellationFee > 0m && feeReceiverAccount is null)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("The seller cannot currently receive the early cancellation fee. Please contact the seller or try again later.")
                        .SetCode("SUPPLY_CONTRACT_FEE_RECEIVER_UNAVAILABLE")
                        .Build());
            }

            // Pay across the buyer accounts in the contract currency, in the order TryDebit drains them.
            var remaining = cancellationFee;
            foreach (var account in CompanyBankingService.GetCandidateAccountsForPayment(buyerAccounts, contract.CurrencyCode))
            {
                if (remaining <= 0m)
                {
                    break;
                }

                var take = Math.Min(account.Balance, remaining);
                if (take > 0m)
                {
                    feeDebits.Add((account, take));
                    remaining -= take;
                }
            }

            if (remaining > 0m)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage($"Your {contract.CurrencyCode} accounts cannot cover the {cancellationFee:N2} early cancellation fee.")
                        .SetCode("SUPPLY_CONTRACT_CANCELLATION_FEE_UNPAYABLE")
                        .Build());
            }
        }

        contract.Status = SupplyContractStatus.Cancelled;
        contract.CancelledAtTick = currentTick;
        contract.CancelledAtUtc = DateTime.UtcNow;

        if (feeDebits.Count > 0 && feeReceiverAccount is not null)
        {
            foreach (var (account, amount) in feeDebits)
            {
                account.Balance -= amount;
                account.ConcurrencyToken = Guid.NewGuid();
                db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = contract.BuyerCompanyId,
                    BankAccountId = account.Id,
                    Category = LedgerCategory.SupplyContractPenalty,
                    Description = "Early cancellation fee paid",
                    Amount = -amount,
                    RecordedAtTick = currentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                    ResourceTypeId = contract.ResourceTypeId,
                    ProductTypeId = contract.ProductTypeId,
                });
            }

            feeReceiverAccount.Balance += cancellationFee;
            feeReceiverAccount.ConcurrencyToken = Guid.NewGuid();
            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = contract.SellerCompanyId,
                BankAccountId = feeReceiverAccount.Id,
                Category = LedgerCategory.SupplyContractPenalty,
                Description = "Early cancellation fee received",
                Amount = cancellationFee,
                RecordedAtTick = currentTick,
                RecordedAtUtc = DateTime.UtcNow,
                ResourceTypeId = contract.ResourceTypeId,
                ProductTypeId = contract.ProductTypeId,
            });
        }

        await SaveContractChangesAsync(db, cancellationToken);

        var populated = await LoadSupplyContractAsync(db, contract.Id, cancellationToken);
        return new SupplyContractActionResult
        {
            Success = true,
            Message = "Contract cancelled.",
            Contract = Query.MapSupplyContractCard(populated),
        };
    }

    private static async Task<SupplyContract> EnsurePendingCounterpartyContractAsync(
        Guid contractId,
        AppDbContext db,
        IHttpContextAccessor httpContextAccessor,
        ObjectAuthorizationService objectAuthorization,
        CancellationToken cancellationToken)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var contract = await db.SupplyContracts.FirstOrDefaultAsync(item => item.Id == contractId, cancellationToken)
            ?? throw objectAuthorization.Deny(userId, "supply_contract", contractId, ObjectAuthorizationService.NotFoundReason);
        var actorIsCounterparty = await db.Companies
            .AsNoTracking()
            .AnyAsync(company =>
                company.PlayerId == userId
                && (company.Id == contract.SellerCompanyId || company.Id == contract.BuyerCompanyId),
                cancellationToken);
        if (!actorIsCounterparty)
        {
            throw objectAuthorization.Deny(userId, "supply_contract", contractId, ObjectAuthorizationService.NotOwnedReason);
        }

        if (contract.Status != SupplyContractStatus.Pending)
        {
            throw new GraphQLException(ErrorBuilder.New().SetMessage("Only pending contracts can be changed.").SetCode("SUPPLY_CONTRACT_NOT_PENDING").Build());
        }

        return contract;
    }

    private static async Task<SupplyContract> LoadSupplyContractAsync(AppDbContext db, Guid contractId, CancellationToken cancellationToken)
    {
        return await db.SupplyContracts
            .AsNoTracking()
            .Include(contract => contract.SellerCompany)
            .Include(contract => contract.BuyerCompany)
            .Include(contract => contract.ResourceType)
            .Include(contract => contract.ProductType)
            .FirstAsync(contract => contract.Id == contractId, cancellationToken);
    }

    /// <summary>
    /// Saves a contract status transition. The status is a concurrency token, so when two requests race (for example a double
    /// click on cancel) exactly one wins and the loser gets a retryable error instead of a second fee or a second transition.
    /// </summary>
    private static async Task SaveContractChangesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("The contract or your accounts changed while you were acting on it. Please reload and try again.")
                    .SetCode("CONCURRENT_MODIFICATION")
                    .Build());
        }
    }
}

using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
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

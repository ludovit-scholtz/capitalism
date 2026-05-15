using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    public async Task<List<GovernmentContractCard>> CityGovernmentContracts(
        Guid cityId,
        string? status,
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        var statusFilter = string.IsNullOrWhiteSpace(status) ? GovernmentContractStatus.Open : status.Trim().ToUpperInvariant();

        var contracts = await db.GovernmentContracts
            .AsNoTracking()
            .Include(contract => contract.City)
            .Include(contract => contract.ProductType)
            .Include(contract => contract.WinnerCompany)
            .Include(contract => contract.Bids)
            .Include(contract => contract.Fulfillment)
            .Where(contract => contract.CityId == cityId && contract.Status == statusFilter)
            .OrderBy(contract => contract.DeadlineTick)
            .ToListAsync(cancellationToken);

        return contracts.Select(MapContractCard).ToList();
    }

    [Authorize]
    public async Task<List<ContractBidResult>> MyContractBids(
        Guid companyId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var ownsCompany = await db.Companies.AsNoTracking().AnyAsync(company => company.Id == companyId && company.PlayerId == userId, cancellationToken);
        if (!ownsCompany)
        {
            return [];
        }

        return await db.ContractBids
            .AsNoTracking()
            .Include(bid => bid.Company)
            .Include(bid => bid.Contract)
            .Where(bid => bid.CompanyId == companyId)
            .OrderByDescending(bid => bid.SubmittedAtTick)
            .Select(bid => new ContractBidResult
            {
                Id = bid.Id,
                ContractId = bid.ContractId,
                CompanyId = bid.CompanyId,
                CompanyName = bid.Company.Name,
                BidPricePerUnit = bid.BidPricePerUnit,
                EstimatedDeliveryTick = bid.EstimatedDeliveryTick,
                SubmittedAtTick = bid.SubmittedAtTick,
                ContractStatus = bid.Contract.Status,
            })
            .ToListAsync(cancellationToken);
    }

    [Authorize]
    public async Task<List<GovernmentContractCard>> CompanyContracts(
        Guid companyId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var ownsCompany = await db.Companies.AsNoTracking().AnyAsync(company => company.Id == companyId && company.PlayerId == userId, cancellationToken);
        if (!ownsCompany)
        {
            return [];
        }

        var contractIdsFromBids = await db.ContractBids
            .AsNoTracking()
            .Where(bid => bid.CompanyId == companyId)
            .Select(bid => bid.ContractId)
            .ToListAsync(cancellationToken);

        var contractIds = contractIdsFromBids.ToHashSet();
        var awardedContractIds = await db.GovernmentContracts
            .AsNoTracking()
            .Where(contract => contract.WinnerCompanyId == companyId)
            .Select(contract => contract.Id)
            .ToListAsync(cancellationToken);
        foreach (var contractId in awardedContractIds)
        {
            contractIds.Add(contractId);
        }

        if (contractIds.Count == 0)
        {
            return [];
        }

        var contracts = await db.GovernmentContracts
            .AsNoTracking()
            .Include(contract => contract.City)
            .Include(contract => contract.ProductType)
            .Include(contract => contract.WinnerCompany)
            .Include(contract => contract.Bids)
            .Include(contract => contract.Fulfillment)
            .Where(contract => contractIds.Contains(contract.Id))
            .OrderByDescending(contract => contract.CreatedAtTick)
            .ToListAsync(cancellationToken);

        return contracts.Select(MapContractCard).ToList();
    }

    public async Task<GovernmentContractDetailResult?> ContractDetail(
        Guid contractId,
        Guid? companyId,
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        var contract = await db.GovernmentContracts
            .AsNoTracking()
            .Include(item => item.City)
            .Include(item => item.ProductType)
            .Include(item => item.WinnerCompany)
            .Include(item => item.Bids)
            .Include(item => item.Fulfillment)
            .FirstOrDefaultAsync(item => item.Id == contractId, cancellationToken);
        if (contract is null)
        {
            return null;
        }

        GovernmentContractEligibilityResult? eligibility = null;
        if (companyId.HasValue)
        {
            var status = await GovernmentContractService.EvaluateCompanyEligibilityAsync(db, contract, companyId.Value, cancellationToken);
            eligibility = new GovernmentContractEligibilityResult
            {
                IsEligible = status.IsEligible,
                ReasonCode = status.ReasonCode,
                ReasonMessage = status.ReasonMessage,
                CurrentQualityLevel = status.CurrentQualityLevel,
            };
        }

        return new GovernmentContractDetailResult
        {
            Contract = MapContractCard(contract),
            CompetingBidCount = contract.Bids.Count,
            Eligibility = eligibility,
        };
    }

    private static GovernmentContractCard MapContractCard(GovernmentContract contract)
    {
        var winningBid = contract.WinnerCompanyId.HasValue
            ? contract.Bids.FirstOrDefault(bid => bid.CompanyId == contract.WinnerCompanyId.Value)
            : null;
        var delivered = contract.Fulfillment?.QuantityDelivered ?? 0m;
        var fulfillmentPercent = contract.QuantityRequired > 0m
            ? Math.Round(Math.Clamp(delivered / contract.QuantityRequired, 0m, 1m) * 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;

        return new GovernmentContractCard
        {
            Id = contract.Id,
            CityId = contract.CityId,
            CityName = contract.City.Name,
            CurrencyCode = contract.City.CurrencyCode,
            Title = contract.Title,
            Description = contract.Description,
            ProductTypeId = contract.ProductTypeId,
            ProductName = contract.ProductType.Name,
            QuantityRequired = contract.QuantityRequired,
            MinimumQuality = contract.MinimumQuality,
            BudgetCap = contract.BudgetCap,
            DeadlineTick = contract.DeadlineTick,
            Status = contract.Status,
            WinnerCompanyId = contract.WinnerCompanyId,
            WinnerCompanyName = contract.WinnerCompany?.Name,
            CreatedAtTick = contract.CreatedAtTick,
            BidCount = contract.Bids.Count,
            AwardedBidPricePerUnit = winningBid?.BidPricePerUnit,
            FulfilledQuantity = delivered,
            FulfillmentPercent = fulfillmentPercent,
        };
    }
}

using Api.Data;
using Api.Data.Entities;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    [Authorize]
    public async Task<List<SupplyContractCard>> MyContracts(
        int take,
        int skip,
        string? status,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var companyIds = await db.Companies
            .AsNoTracking()
            .Where(company => company.PlayerId == userId)
            .Select(company => company.Id)
            .ToListAsync(cancellationToken);

        if (companyIds.Count == 0)
        {
            return [];
        }

        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();
        var normalizedTake = Math.Clamp(take, 1, 200);
        var normalizedSkip = Math.Max(0, skip);

        var query = db.SupplyContracts
            .AsNoTracking()
            .Include(contract => contract.SellerCompany)
            .Include(contract => contract.BuyerCompany)
            .Include(contract => contract.ResourceType)
            .Include(contract => contract.ProductType)
            .Where(contract => companyIds.Contains(contract.SellerCompanyId) || companyIds.Contains(contract.BuyerCompanyId));

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            query = query.Where(contract => contract.Status == normalizedStatus);
        }

        var contracts = await query
            .OrderByDescending(contract => contract.CreatedAtTick)
            .Skip(normalizedSkip)
            .Take(normalizedTake)
            .ToListAsync(cancellationToken);

        return contracts.Select(MapSupplyContractCard).ToList();
    }

    [Authorize]
    public async Task<List<SupplyContractCard>> OpenContractOffers(
        Guid cityId,
        Guid? resourceTypeId,
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        var sellerCompanyIds = await db.Buildings
            .AsNoTracking()
            .Where(building => building.CityId == cityId)
            .Select(building => building.CompanyId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (sellerCompanyIds.Count == 0)
        {
            return [];
        }

        var query = db.SupplyContracts
            .AsNoTracking()
            .Include(contract => contract.SellerCompany)
            .Include(contract => contract.BuyerCompany)
            .Include(contract => contract.ResourceType)
            .Include(contract => contract.ProductType)
            .Where(contract =>
                contract.Status == SupplyContractStatus.Pending
                && sellerCompanyIds.Contains(contract.SellerCompanyId));

        if (resourceTypeId.HasValue)
        {
            query = query.Where(contract => contract.ResourceTypeId == resourceTypeId.Value);
        }

        var rows = await query
            .OrderByDescending(contract => contract.CreatedAtTick)
            .Take(200)
            .ToListAsync(cancellationToken);

        return rows.Select(MapSupplyContractCard).ToList();
    }

    internal static SupplyContractCard MapSupplyContractCard(SupplyContract contract)
    {
        return new SupplyContractCard
        {
            Id = contract.Id,
            SellerCompanyId = contract.SellerCompanyId,
            SellerCompanyName = contract.SellerCompany.Name,
            BuyerCompanyId = contract.BuyerCompanyId,
            BuyerCompanyName = contract.BuyerCompany.Name,
            SellerBuildingUnitId = contract.SellerBuildingUnitId,
            ResourceTypeId = contract.ResourceTypeId,
            ResourceTypeName = contract.ResourceType?.Name,
            ProductTypeId = contract.ProductTypeId,
            ProductTypeName = contract.ProductType?.Name,
            QuantityPerTick = contract.QuantityPerTick,
            PricePerUnit = contract.PricePerUnit,
            DurationTicks = contract.DurationTicks,
            RemainingTicks = contract.RemainingTicks,
            StartTick = contract.StartTick,
            PenaltyRatePercent = contract.PenaltyRatePercent,
            CurrencyCode = contract.CurrencyCode,
            Status = contract.Status,
            CreatedAtTick = contract.CreatedAtTick,
            TotalDeliveredQuantity = contract.TotalDeliveredQuantity,
            TotalUndeliveredQuantity = contract.TotalUndeliveredQuantity,
            TotalPenaltyAmount = contract.TotalPenaltyAmount,
            PenaltyCount = contract.PenaltyCount,
        };
    }
}

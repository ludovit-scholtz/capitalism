using Api.Data.Entities;

namespace Api.Utilities;

/// <summary>
/// Computes company equity and quoted share prices for the stock exchange and player portfolios.
/// Profit expectation is intentionally modeled as zero for now; new-company pricing therefore tracks current equity only.
/// </summary>
public static class SharePriceCalculator
{
    public const decimal BidDiscount = 0.01m;
    public const decimal AskPremium = 0.01m;

    public static Dictionary<Guid, decimal> ComputeBaseEquityByCompany(
        IReadOnlyCollection<Company> companies,
        IReadOnlyCollection<Building> buildings,
        IReadOnlyCollection<BuildingLot> ownedLots,
        IReadOnlyCollection<Inventory> inventories)
    {
        var companyIds = companies.Select(company => company.Id).ToHashSet();

        var buildingValueByCompany = buildings
            .Where(building => companyIds.Contains(building.CompanyId))
            .GroupBy(building => building.CompanyId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(WealthCalculator.GetBuildingValue));

        var landValueByCompany = ownedLots
            .Where(lot => lot.OwnerCompanyId.HasValue && companyIds.Contains(lot.OwnerCompanyId.Value))
            .GroupBy(lot => lot.OwnerCompanyId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(WealthCalculator.GetLandValue));

        var buildingCompanyById = buildings
            .Where(building => companyIds.Contains(building.CompanyId))
            .ToDictionary(building => building.Id, building => building.CompanyId);

        var inventoryValueByCompany = inventories
            .Where(inventory => buildingCompanyById.ContainsKey(inventory.BuildingId))
            .GroupBy(inventory => buildingCompanyById[inventory.BuildingId])
            .ToDictionary(
                group => group.Key,
                group => group.Sum(inventory => inventory.Quantity * WealthCalculator.GetItemBasePrice(inventory)));

        return companies.ToDictionary(
            company => company.Id,
            company => company.Cash
                + buildingValueByCompany.GetValueOrDefault(company.Id)
                + landValueByCompany.GetValueOrDefault(company.Id)
                + inventoryValueByCompany.GetValueOrDefault(company.Id));
    }

    public static Dictionary<Guid, decimal> ComputeQuotedSharePriceByCompany(
        IReadOnlyCollection<Company> companies,
        IReadOnlyDictionary<Guid, decimal> baseEquityByCompany,
        IReadOnlyCollection<Shareholding> shareholdings)
    {
        var baseSharePriceByCompany = companies.ToDictionary(
            company => company.Id,
            company => company.TotalSharesIssued > 0m
                ? decimal.Round(baseEquityByCompany.GetValueOrDefault(company.Id) / company.TotalSharesIssued, 4, MidpointRounding.AwayFromZero)
                : 0m);

        var externalPortfolioValueByCompany = shareholdings
            .Where(holding => holding.OwnerCompanyId.HasValue && holding.CompanyId != holding.OwnerCompanyId.Value)
            .GroupBy(holding => holding.OwnerCompanyId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(holding => holding.ShareCount * baseSharePriceByCompany.GetValueOrDefault(holding.CompanyId)));

        return companies.ToDictionary(
            company => company.Id,
            company =>
            {
                if (company.TotalSharesIssued <= 0m)
                {
                    return 0m;
                }

                var equity = baseEquityByCompany.GetValueOrDefault(company.Id) + externalPortfolioValueByCompany.GetValueOrDefault(company.Id);
                return decimal.Round(equity / company.TotalSharesIssued, 4, MidpointRounding.AwayFromZero);
            });
    }

    public static decimal ComputePublicFloat(Company company, IEnumerable<Shareholding> shareholdings)
    {
        var allocatedShares = shareholdings
            .Where(holding => holding.CompanyId == company.Id)
            .Sum(holding => holding.ShareCount);

        return Math.Max(0m, decimal.Round(company.TotalSharesIssued - allocatedShares, 4, MidpointRounding.AwayFromZero));
    }

    public static decimal ComputeBidPrice(decimal sharePrice)
        => decimal.Round(sharePrice * (1m - BidDiscount), 4, MidpointRounding.AwayFromZero);

    public static decimal ComputeAskPrice(decimal sharePrice)
        => decimal.Round(sharePrice * (1m + AskPremium), 4, MidpointRounding.AwayFromZero);
}
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Competitive market intelligence for one city.
    /// Returns per-product ranked sellers with asking price, combined brand quality,
    /// and estimated weekly sales volume based on public sales records.
    /// </summary>
    [Authorize]
    public async Task<MarketIntelligenceResult?> GetMarketIntelligence(
        Guid cityId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        // Keep the endpoint authenticated and ownership-aware with the standard auth pipeline.
        _ = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var city = await db.Cities
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == cityId);
        if (city is null)
        {
            return null;
        }

        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(state => state.CurrentTick)
            .FirstOrDefaultAsync();

        var dataFromTick = Math.Max(0L, currentTick - GameConstants.TicksPerWeek + 1);

        var records = await db.PublicSalesRecords
            .AsNoTracking()
            .Where(record =>
                record.CityId == cityId
                && record.ProductTypeId.HasValue
                && record.Tick >= dataFromTick
                && record.Tick <= currentTick)
            .Select(record => new
            {
                ProductTypeId = record.ProductTypeId!.Value,
                ProductName = record.ProductType != null ? record.ProductType.Name : string.Empty,
                ProductSlug = record.ProductType != null ? record.ProductType.Slug : string.Empty,
                record.CompanyId,
                CompanyName = record.Company.Name,
                record.Tick,
                record.QuantitySold,
                record.PricePerUnit,
            })
            .ToListAsync();

        if (records.Count == 0)
        {
            return new MarketIntelligenceResult
            {
                CityId = city.Id,
                CityName = city.Name,
                DataFromTick = dataFromTick,
                DataToTick = currentTick,
                Products = [],
            };
        }

        var productIds = records
            .Select(record => record.ProductTypeId)
            .Distinct()
            .ToList();
        var companyIds = records
            .Select(record => record.CompanyId)
            .Distinct()
            .ToList();

        var brandQualityByCompanyProduct = (await db.Brands
            .AsNoTracking()
            .Where(brand =>
                brand.ProductTypeId.HasValue
                && productIds.Contains(brand.ProductTypeId.Value)
                && companyIds.Contains(brand.CompanyId))
            .ToListAsync())
            .GroupBy(brand => new { brand.CompanyId, ProductTypeId = brand.ProductTypeId!.Value })
            .ToDictionary(
                group => (group.Key.CompanyId, group.Key.ProductTypeId),
                group =>
                {
                    var best = group
                        .OrderByDescending(brand => brand.Quality + brand.MarketingQuality)
                        .First();
                    return Math.Clamp(
                        1m - (1m - Math.Clamp(best.Quality, 0m, 1m)) * (1m - Math.Clamp(best.MarketingQuality, 0m, 1m)),
                        0m,
                        1m);
                });

        var productRows = records
            .GroupBy(record => new { record.ProductTypeId, record.ProductName, record.ProductSlug })
            .Select(productGroup =>
            {
                var productTotal = productGroup.Sum(record => record.QuantitySold);

                var sellers = productGroup
                    .GroupBy(record => new { record.CompanyId, record.CompanyName })
                    .Select(sellerGroup =>
                    {
                        var latest = sellerGroup
                            .OrderByDescending(record => record.Tick)
                            .ThenByDescending(record => record.PricePerUnit)
                            .First();

                        var weeklySales = sellerGroup.Sum(record => record.QuantitySold);
                        var hasBrandQuality = brandQualityByCompanyProduct.TryGetValue(
                            (sellerGroup.Key.CompanyId, productGroup.Key.ProductTypeId),
                            out var brandQuality);

                        return new MarketIntelligenceSellerRow
                        {
                            CompanyId = sellerGroup.Key.CompanyId,
                            DisplayName = sellerGroup.Key.CompanyName,
                            AskingPricePerUnit = latest.PricePerUnit,
                            BrandQuality = hasBrandQuality ? brandQuality : null,
                            EstimatedWeeklySalesVolume = weeklySales,
                            MarketShare = productTotal > 0m
                                ? Math.Clamp(weeklySales / productTotal, 0m, 1m)
                                : 0m,
                        };
                    })
                    .OrderByDescending(seller => seller.EstimatedWeeklySalesVolume)
                    .ThenBy(seller => seller.AskingPricePerUnit)
                    .ToList();

                for (var index = 0; index < sellers.Count; index++)
                {
                    sellers[index].Rank = index + 1;
                }

                return new MarketIntelligenceProductRow
                {
                    ProductTypeId = productGroup.Key.ProductTypeId,
                    ProductName = productGroup.Key.ProductName,
                    ProductSlug = productGroup.Key.ProductSlug,
                    TotalWeeklySalesVolume = productTotal,
                    Sellers = sellers,
                };
            })
            .OrderByDescending(product => product.TotalWeeklySalesVolume)
            .ThenBy(product => product.ProductName)
            .ToList();

        return new MarketIntelligenceResult
        {
            CityId = city.Id,
            CityName = city.Name,
            DataFromTick = dataFromTick,
            DataToTick = currentTick,
            Products = productRows,
        };
    }
}

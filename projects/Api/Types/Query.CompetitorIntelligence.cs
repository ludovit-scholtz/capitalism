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
    /// Returns quality intelligence for all companies selling a specific product in a given city.
    /// Results are sorted by quality level descending so the market leader appears first.
    /// Includes an <c>isOwnCompany</c> flag so the caller can highlight their own position.
    /// Requires authentication.
    /// </summary>
    [Authorize]
    public async Task<List<CompetitorQualityEntry>> GetCompetitorQualityIntelligence(
        Guid cityId,
        Guid productTypeId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        // Determine the authenticated player's company that has presence in this city.
        // "Presence" means the company owns at least one building in the city.
        var myCompanyId = await db.Buildings
            .AsNoTracking()
            .Where(b => b.CityId == cityId)
            .Join(db.Companies.Where(c => c.PlayerId == userId),
                  b => b.CompanyId, c => c.Id, (b, c) => c.Id)
            .FirstOrDefaultAsync();

        // Find all companies with a PRODUCT-scoped brand for this product type
        // whose buildings are present in the given city.
        var brandEntries = await db.Brands
            .AsNoTracking()
            .Where(b => b.Scope == BrandScope.Product && b.ProductTypeId == productTypeId)
            .Join(db.Buildings.Where(bld => bld.CityId == cityId),
                  brand => brand.CompanyId, bld => bld.CompanyId,
                  (brand, _) => brand)
            .Distinct()
            .Include(b => b.Company)
            .ToListAsync();

        // Deduplicate by company (a company may have multiple PRODUCT brands for the same product
        // if data inconsistency exists — keep the highest quality).
        var bestBrandByCompany = brandEntries
            .GroupBy(b => b.CompanyId)
            .Select(g => g.OrderByDescending(b => b.Quality + b.MarketingQuality).First())
            .ToList();

        var result = bestBrandByCompany
            .Select(brand =>
            {
                var rdQuality = Math.Clamp(brand.Quality, 0m, 1m);
                var mktQuality = Math.Clamp(brand.MarketingQuality, 0m, 1m);
                var combinedQuality = Math.Clamp(1m - (1m - rdQuality) * (1m - mktQuality), 0m, 1m);
                var qualityLevel = Math.Round(combinedQuality * 10m, 1);
                var pricePremiumPct = Math.Round(GameConstants.QualityPricePremiumRate * combinedQuality * 100m, 1);

                return new CompetitorQualityEntry
                {
                    CompanyId = brand.CompanyId,
                    CompanyName = brand.Company.Name,
                    QualityLevel = qualityLevel,
                    PricePremiumPct = pricePremiumPct,
                    IsOwnCompany = myCompanyId != default && brand.CompanyId == myCompanyId,
                };
            })
            .OrderByDescending(e => e.QualityLevel)
            .ToList();

        return result;
    }
}

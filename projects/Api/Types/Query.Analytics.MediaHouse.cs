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
    /// Returns detail for a single MEDIA_HOUSE including content score and city rank.
    /// Spending level and latest-tick revenue are returned only to the owner.
    /// </summary>
    public async Task<MediaHouseDetailResult?> GetMediaHouseDetail(
        Guid buildingId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var building = await db.Buildings
            .Include(b => b.Company)
            .Include(b => b.City)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == buildingId && b.Type == BuildingType.MediaHouse);

        if (building is null)
            return null;
        if (building.Company is null)
            return null;

        var userId = httpContextAccessor.HttpContext?.User.GetUserId();
        var isOwner = userId.HasValue && building.Company.PlayerId == userId.Value;

        var peers = await db.Buildings
            .Where(b => b.CityId == building.CityId
                && b.Type == BuildingType.MediaHouse
                && (b.MediaType ?? string.Empty) == (building.MediaType ?? string.Empty))
            .AsNoTracking()
            .ToListAsync();

        var maxContent = peers.Count > 0 ? peers.Max(b => b.ContentValue) : 0m;
        var contentScore = maxContent > 0m
            ? Math.Round(Math.Clamp(building.ContentValue / maxContent, 0m, 1m) * 100m, 1)
            : 0m;

        var rank = peers
            .OrderByDescending(b => b.ContentValue)
            .ThenBy(b => b.Id)
            .Select((b, index) => new { b.Id, Rank = index + 1 })
            .FirstOrDefault(x => x.Id == building.Id)?.Rank ?? 1;

        decimal? revenueThisTick = null;
        if (isOwner)
        {
            var currentTick = (await db.GameStates.AsNoTracking().FirstOrDefaultDeterministicAsync())?.CurrentTick ?? 0;
            revenueThisTick = await db.LedgerEntries
                .Where(e => e.BuildingId == building.Id
                    && e.Category == LedgerCategory.MediaHouseIncome
                    && e.RecordedAtTick == currentTick)
                .SumAsync(e => e.Amount);
        }

        return new MediaHouseDetailResult
        {
            BuildingId = building.Id,
            BuildingName = building.Name,
            CityId = building.CityId,
            CityName = building.City.Name,
            MediaType = building.MediaType ?? MediaType.Newspaper,
            ContentQualityScore = contentScore,
            AccumulatedContent = building.ContentValue,
            CityRank = rank,
            SpendingLevelPerTick = isOwner ? building.ContentBudgetPerTick : null,
            RevenueThisTick = isOwner ? revenueThisTick : null,
        };
    }

    /// <summary>
    /// Returns brand-impact analytics for a MEDIA_HOUSE building.
    /// Shows how the outlet's content value, content ranking, and advertising income
    /// influence brand awareness and marketing quality across the city.
    /// Requires authentication and ownership of the building.
    /// </summary>
    [Authorize]
    public async Task<MediaHouseAnalyticsResult?> GetMediaHouseAnalytics(
        Guid buildingId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .Include(b => b.Company)
            .Include(b => b.City)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == buildingId);

        if (building is null || building.Company.PlayerId != userId)
            return null;

        if (building.Type != BuildingType.MediaHouse)
            return null;

        // Compute content ranking fraction vs city/type peers.
        var peers = await db.Buildings
            .Where(b => b.CityId == building.CityId
                && b.Type == BuildingType.MediaHouse
                && (b.MediaType ?? string.Empty) == (building.MediaType ?? string.Empty))
            .AsNoTracking()
            .ToListAsync();

        var maxContent = peers.Count > 0 ? peers.Max(b => b.ContentValue) : 0m;
        var contentRankingFraction = (maxContent > 0m)
            ? Math.Clamp(building.ContentValue / maxContent, 0m, 1m)
            : 0m;
        var contentRankingPct = Math.Round(contentRankingFraction * 100m, 1);

        // Effective multiplier seen by advertisers: channel × content ranking.
        var channelMultiplier = MediaType.EffectivenessMultiplier(building.MediaType);
        var effectiveMultiplier = channelMultiplier
            * (GameConstants.ContentRankingBaseMultiplier
               + contentRankingFraction * GameConstants.ContentRankingMarketingBoostRange);

        // Upgrade path: cost and efficiency for the next level.
        var currentLevel = building.Level;
        var isMaxLevel = currentLevel >= GameConstants.MaxMediaHouseLevel;
        var currentEfficiencyPct = (int)Math.Round(GameConstants.MediaHouseContentEfficiency(currentLevel) * 100m);
        var nextEfficiencyPct = isMaxLevel
            ? currentEfficiencyPct
            : (int)Math.Round(GameConstants.MediaHouseContentEfficiency(currentLevel + 1) * 100m);
        decimal? upgradeCostEur = isMaxLevel ? null : GameConstants.MediaHouseUpgradeCost(currentLevel);
        int? upgradeTimeTicks = isMaxLevel ? null : GameConstants.MediaHouseUpgradeTicks(currentLevel);

        // Advertising income: last 100 ticks of MediaHouseIncome ledger entries for this building.
        var incomeHistory = await db.LedgerEntries
            .Where(e => e.BuildingId == buildingId && e.Category == LedgerCategory.MediaHouseIncome)
            .OrderByDescending(e => e.RecordedAtTick)
            .Take(100)
            .Select(e => new MediaHouseIncomeEntry
            {
                Tick = e.RecordedAtTick,
                Amount = e.Amount,
                Description = e.Description ?? string.Empty,
            })
            .ToListAsync();

        incomeHistory = [.. incomeHistory.OrderBy(e => e.Tick)];

        var totalIncomeLast100 = incomeHistory.Sum(e => e.Amount);
        var avgIncomePerTick = incomeHistory.Count > 0
            ? totalIncomeLast100 / incomeHistory.Count
            : 0m;

        // Brands linked to marketing campaigns that use this media house.
        var linkedCampaigns = await db.BuildingUnits
            .Where(u => u.MediaHouseBuildingId == buildingId
                && u.UnitType == UnitType.Marketing
                && u.Budget > 0m)
            .Include(u => u.Building)
            .AsNoTracking()
            .ToListAsync();

        var advertiserCompanyIds = linkedCampaigns.Select(u => u.Building.CompanyId).Distinct().ToList();
        var brandEffectRows = new List<MediaHouseBrandEffectRow>();

        foreach (var advertiserId in advertiserCompanyIds)
        {
            var advertiserBrands = await db.Brands
                .Where(br => br.CompanyId == advertiserId)
                .AsNoTracking()
                .ToListAsync();

            foreach (var brand in advertiserBrands)
            {
                var productName = brand.ProductTypeId.HasValue
                    ? (await db.ProductTypes.FindAsync(brand.ProductTypeId.Value))?.Name ?? "Product"
                    : brand.Scope == BrandScope.Category
                        ? brand.IndustryCategory ?? "Category"
                        : brand.Name;

                brandEffectRows.Add(new MediaHouseBrandEffectRow
                {
                    CompanyId = advertiserId,
                    CompanyName = (await db.Companies.FindAsync(advertiserId))?.Name ?? "Unknown",
                    BrandScope = brand.Scope ?? "PRODUCT",
                    ProductName = productName,
                    BrandAwareness = Math.Round(brand.Awareness * 100m, 1),
                    MarketingQuality = Math.Round(brand.MarketingQuality * 100m, 1),
                    EffectivenessMultiplierApplied = Math.Round(effectiveMultiplier, 3),
                });
            }
        }

        // Strategic guidance.
        string strategyRating;
        string strategyTip;
        if (contentRankingPct >= 80m)
        {
            strategyRating = "DOMINANT";
            strategyTip = $"Your {building.MediaType} outlet ranks in the top tier with {contentRankingPct:F0}% content ranking. Advertisers using your outlet receive a ×{effectiveMultiplier:F2} combined multiplier — the highest available in this category. Maintain investment to protect your lead.";
        }
        else if (contentRankingPct >= 50m)
        {
            strategyRating = "COMPETITIVE";
            strategyTip = $"Your outlet holds a strong {contentRankingPct:F0}% content ranking. Keep investing in content to push toward dominant rank and unlock the full ×{channelMultiplier * 1.5m:F2} channel bonus for advertisers.";
        }
        else if (contentRankingPct >= 20m)
        {
            strategyRating = "GROWING";
            strategyTip = $"Content ranking is at {contentRankingPct:F0}%. Advertisers using your outlet receive ×{effectiveMultiplier:F2} — good but outperformed by the market leader. Increase the content budget to grow faster.";
        }
        else
        {
            strategyRating = "EARLY_STAGE";
            strategyTip = $"At {contentRankingPct:F0}% content ranking the outlet is in early growth. Set a consistent content budget to accumulate content value and build competitive reach. Level upgrades also improve conversion efficiency from {currentEfficiencyPct}% toward {nextEfficiencyPct}%.";
        }

        return new MediaHouseAnalyticsResult
        {
            BuildingId = building.Id,
            BuildingName = building.Name,
            MediaType = building.MediaType ?? "NEWSPAPER",
            Level = currentLevel,
            ContentValue = building.ContentValue,
            ContentRankingPct = contentRankingPct,
            ChannelMultiplier = channelMultiplier,
            EffectiveMultiplier = Math.Round(effectiveMultiplier, 3),
            CurrentEfficiencyPct = currentEfficiencyPct,
            NextLevelEfficiencyPct = nextEfficiencyPct,
            IsMaxLevel = isMaxLevel,
            UpgradeCostEur = upgradeCostEur,
            UpgradeTimeTicks = upgradeTimeTicks,
            MaxLevel = GameConstants.MaxMediaHouseLevel,
            TotalIncomeLast100Ticks = totalIncomeLast100,
            AvgIncomePerTick = Math.Round(avgIncomePerTick, 2),
            IncomeHistory = incomeHistory,
            AdvertiserCount = advertiserCompanyIds.Count,
            BrandEffects = brandEffectRows,
            StrategyRating = strategyRating,
            StrategyTip = strategyTip,
        };
    }
}

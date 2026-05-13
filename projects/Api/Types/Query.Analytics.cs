using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Returns the city-level power balance: total supply from all power plants,
    /// total demand from all consuming buildings, the reserve margin, and a
    /// human-readable status string (BALANCED, CONSTRAINED, or CRITICAL).
    ///
    /// This query is public (no auth required) so players can assess a city
    /// before purchasing a lot.
    /// </summary>
    public async Task<CityPowerBalance> GetCityPowerBalance(Guid cityId, [Service] AppDbContext db)
    {
        var buildings = await db.Buildings
            .Where(b => b.CityId == cityId)
            .ToListAsync();

        var powerPlants = buildings.Where(b => b.Type == Data.Entities.BuildingType.PowerPlant).ToList();
        var consumers = buildings.Where(b => b.Type != Data.Entities.BuildingType.PowerPlant).ToList();

        var totalSupplyMw = powerPlants.Sum(plant =>
            plant.PowerOutput > 0m
                ? plant.PowerOutput!.Value
                : GameConstants.DefaultPowerOutputMw(plant.PowerPlantType));

        var totalDemandMw = consumers.Sum(building =>
            building.PowerConsumption > 0m
                ? building.PowerConsumption
                : GameConstants.PowerDemandMw(building.Type, building.Level));

        var reserveMw = totalSupplyMw - totalDemandMw;
        var reservePercent = totalDemandMw > 0m
            ? decimal.Round(reserveMw / totalDemandMw * 100m, 1, MidpointRounding.AwayFromZero)
            : 100m;

        string status;
        if (totalDemandMw == 0m || reserveMw >= 0m)
            status = "BALANCED";
        else if (totalSupplyMw >= totalDemandMw * 0.5m)
            status = "CONSTRAINED";
        else
            status = "CRITICAL";

        var powerPlantSummaries = powerPlants.Select(p => new PowerPlantSummary
        {
            BuildingId = p.Id,
            BuildingName = p.Name,
            PlantType = p.PowerPlantType ?? Data.Entities.PowerPlantType.Coal,
            OutputMw = p.PowerOutput > 0m
                ? p.PowerOutput!.Value
                : GameConstants.DefaultPowerOutputMw(p.PowerPlantType),
            PowerStatus = p.PowerStatus,
        }).ToList();

        return new CityPowerBalance
        {
            CityId = cityId,
            TotalSupplyMw = totalSupplyMw,
            TotalDemandMw = totalDemandMw,
            ReserveMw = reserveMw,
            ReservePercent = reservePercent,
            Status = status,
            PowerPlants = powerPlantSummaries,
            PowerPlantCount = powerPlants.Count,
            ConsumerBuildingCount = consumers.Count,
        };
    }

    /// <summary>
    /// Compatibility query for energy-grid status naming used by the product definition.
    /// Returns city supply/demand in kW with currently constrained/offline buildings.
    /// </summary>
    public async Task<EnergyGridStatus> GetEnergyGridStatus(Guid cityId, [Service] AppDbContext db)
    {
        var buildings = await db.Buildings
            .AsNoTracking()
            .Where(b => b.CityId == cityId)
            .ToListAsync();

        var powerPlants = buildings.Where(b => b.Type == Data.Entities.BuildingType.PowerPlant).ToList();
        var consumers = buildings.Where(b => b.Type != Data.Entities.BuildingType.PowerPlant).ToList();

        var totalSupplyMw = powerPlants.Sum(plant =>
            plant.PowerOutput > 0m
                ? plant.PowerOutput!.Value
                : GameConstants.DefaultPowerOutputMw(plant.PowerPlantType));

        var totalDemandMw = consumers.Sum(building =>
            building.PowerConsumption > 0m
                ? building.PowerConsumption
                : GameConstants.PowerDemandMw(building.Type, building.Level));

        var offlineBuildings = consumers
            .Where(b => b.PowerStatus is Data.Entities.PowerStatus.Offline or Data.Entities.PowerStatus.Constrained)
            .Select(b => new OfflineEnergyBuilding
            {
                BuildingId = b.Id,
                CompanyId = b.CompanyId,
                BuildingName = b.Name,
                PowerStatus = b.PowerStatus,
                PowerPriority = b.PowerPriority,
            })
            .OrderByDescending(b => b.PowerPriority)
            .ThenBy(b => b.BuildingName)
            .ToList();

        return new EnergyGridStatus
        {
            CityId = cityId,
            TotalSupplyKw = decimal.Round(totalSupplyMw * 1000m, 2, MidpointRounding.AwayFromZero),
            TotalDemandKw = decimal.Round(totalDemandMw * 1000m, 2, MidpointRounding.AwayFromZero),
            SurplusOrDeficitKw = decimal.Round((totalSupplyMw - totalDemandMw) * 1000m, 2, MidpointRounding.AwayFromZero),
            OfflineBuildings = offlineBuildings,
        };
    }

    /// <summary>
    /// Alias of <see cref="GetEnergyGridStatus"/> for city-level energy overview consumers.
    /// </summary>
    public Task<EnergyGridStatus> GetCityEnergyOverview(Guid cityId, [Service] AppDbContext db)
        => GetEnergyGridStatus(cityId, db);

    /// <summary>
    /// Returns per-building energy state and city aggregate context for the owning player.
    /// </summary>
    [Authorize]
    public async Task<BuildingEnergyStatus> GetBuildingEnergyStatus(
        Guid buildingId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .AsNoTracking()
            .Include(b => b.Company)
            .FirstOrDefaultAsync(b => b.Id == buildingId);

        if (building is null || building.Company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(ObjectAuthorizationService.FriendlyMessage)
                    .SetCode(ObjectAuthorizationService.NotFoundOrNotOwnedCode)
                    .Build());
        }

        var grid = await GetEnergyGridStatus(building.CityId, db);
        var hasPowerPlants = grid.TotalSupplyKw > 0m;

        return new BuildingEnergyStatus
        {
            BuildingId = building.Id,
            CityId = building.CityId,
            PowerStatus = building.PowerStatus,
            PowerPriority = building.PowerPriority,
            PowerDemandKw = decimal.Round(building.PowerConsumption * 1000m, 2, MidpointRounding.AwayFromZero),
            CitySupplyKw = grid.TotalSupplyKw,
            CityDemandKw = grid.TotalDemandKw,
            Source = building.PowerStatus == Data.Entities.PowerStatus.Offline
                ? "NO_POWER"
                : hasPowerPlants ? "CITY_GRID" : "LEGACY_GRID",
            CostPerTickLocal = 0m,
        };
    }

    /// <summary>
    /// Returns the authenticated player's first-sale mission status.
    /// The mission tracks the player's onboarding sales shop from initial configuration
    /// through to the moment a real public-sales record is created in the simulation.
    ///
    /// Phase values:
    ///   NO_SHOP          — onboarding is not yet complete (no shop building tracked).
    ///   CONFIGURE_SHOP   — shop exists but has readiness blockers preventing the first sale.
    ///   AWAITING_FIRST_SALE — shop is fully configured; waiting for the simulation to record a sale.
    ///   FIRST_SALE_RECORDED — a real PublicSalesRecord exists for this shop; mission complete.
    ///   ALREADY_COMPLETED   — the first-sale milestone was previously acknowledged and persisted.
    ///
    /// The <see cref="FirstSaleMissionStatus.Blockers"/> list explains WHY the shop is not ready
    /// (only relevant when phase is CONFIGURE_SHOP).
    /// </summary>
    [Authorize]
    public async Task<FirstSaleMissionStatus> GetFirstSaleMission(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var player = await db.Players
            .Include(p => p.Companies)
            .FirstOrDefaultAsync(p => p.Id == userId);

        if (player is null)
            return new FirstSaleMissionStatus { Phase = FirstSaleMissionPhase.NoShop };

        // Already acknowledged by the player
        if (player.OnboardingFirstSaleCompletedAtUtc is not null)
            return new FirstSaleMissionStatus { Phase = FirstSaleMissionPhase.AlreadyCompleted };

        // No shop being tracked
        if (player.OnboardingShopBuildingId is null)
            return new FirstSaleMissionStatus { Phase = FirstSaleMissionPhase.NoShop };

        var shopBuilding = await db.Buildings
            .Include(b => b.Units)
            .FirstOrDefaultAsync(b => b.Id == player.OnboardingShopBuildingId);

        if (shopBuilding is null)
            return new FirstSaleMissionStatus { Phase = FirstSaleMissionPhase.NoShop };

        // Check if a real sale has already happened for this shop
        var firstSaleRecord = await db.PublicSalesRecords
            .Include(r => r.ProductType)
            .Where(r => r.BuildingId == shopBuilding.Id && r.QuantitySold > 0m)
            .OrderBy(r => r.Tick)
            .FirstOrDefaultDeterministicAsync();

        if (firstSaleRecord is not null)
        {
            return new FirstSaleMissionStatus
            {
                Phase = FirstSaleMissionPhase.FirstSaleRecorded,
                ShopBuildingId = shopBuilding.Id,
                ShopName = shopBuilding.Name,
                FirstSaleRevenue = firstSaleRecord.Revenue,
                FirstSaleProductName = firstSaleRecord.ProductType?.Name,
                FirstSaleTick = firstSaleRecord.Tick,
                FirstSaleQuantity = firstSaleRecord.QuantitySold,
                FirstSalePricePerUnit = firstSaleRecord.PricePerUnit,
            };
        }

        // Shop exists — compute blockers
        var blockers = new List<string>();

        if (shopBuilding.IsUnderConstruction)
        {
            blockers.Add(FirstSaleMissionBlocker.BuildingUnderConstruction);
        }

        var publicSalesUnit = shopBuilding.Units
            .FirstOrDefault(u => string.Equals(u.UnitType, UnitType.PublicSales, StringComparison.Ordinal));

        if (publicSalesUnit is null)
        {
            blockers.Add(FirstSaleMissionBlocker.PublicSalesUnitMissing);
        }
        else
        {
            if (publicSalesUnit.MinPrice is null or <= 0m)
                blockers.Add(FirstSaleMissionBlocker.PriceNotSet);

            // Check inventory in the shop's public-sales unit
            var hasInventory = await db.Inventories
                .AnyAsync(inv => inv.BuildingUnitId == publicSalesUnit.Id && inv.Quantity > 0m);

            if (!hasInventory)
                blockers.Add(FirstSaleMissionBlocker.NoInventory);
        }

        var phase = blockers.Count == 0
            ? FirstSaleMissionPhase.AwaitingFirstSale
            : FirstSaleMissionPhase.ConfigureShop;

        return new FirstSaleMissionStatus
        {
            Phase = phase,
            ShopBuildingId = shopBuilding.Id,
            ShopName = shopBuilding.Name,
            Blockers = blockers,
        };
    }

    /// <summary>
    /// Lists all MEDIA_HOUSE buildings in a city. Public — no auth required.
    /// Includes channel type (NEWSPAPER, RADIO, TV), owner company name, effectiveness multiplier,
    /// content ranking (percentage relative to top outlet in same city+category), and whether the
    /// outlet is government-owned.
    /// Results are sorted so that the authenticated player's own media houses appear first (within
    /// each category), followed by all others sorted by content ranking descending.
    /// The optional <paramref name="ownerCompanyId"/> parameter (the player's current company)
    /// is used for the "player-first" ordering; it is safe to omit.
    /// </summary>
    public async Task<List<CityMediaHouseInfo>> GetCityMediaHouses(
        Guid cityId,
        Guid? ownerCompanyId,
        string? category,
        [Service] AppDbContext db)
    {
        var normalizedCategory = string.IsNullOrWhiteSpace(category)
            ? null
            : category.Trim().ToUpperInvariant();

        var mediaHouses = await db.Buildings
            .Where(b => b.CityId == cityId
                && b.Type == Data.Entities.BuildingType.MediaHouse
                && (normalizedCategory == null || (b.MediaType ?? string.Empty) == normalizedCategory))
            .Include(b => b.Company)
            .Include(b => b.City)
            .AsNoTracking()
            .ToListAsync();

        // Compute per-category content ranking.
        // Within a city the top ContentValue for each media type = 100 %; all others are proportional.
        var maxContentByType = mediaHouses
            .GroupBy(b => b.MediaType ?? string.Empty)
            .ToDictionary(g => g.Key, g => g.Max(b => b.ContentValue));

        var infos = mediaHouses.Select(b =>
        {
            var maxContent = maxContentByType.TryGetValue(b.MediaType ?? string.Empty, out var max) ? max : 0m;
            var ranking = (maxContent > 0m) ? Math.Round(b.ContentValue / maxContent * 100m, 1) : 0m;

            return new CityMediaHouseInfo
            {
                Id = b.Id,
                Name = b.Name,
                CityId = b.CityId,
                CityName = b.City.Name,
                MediaType = b.MediaType,
                OwnerCompanyId = b.CompanyId,
                OwnerCompanyName = b.Company.Name,
                EffectivenessMultiplier = Data.Entities.MediaType.EffectivenessMultiplier(b.MediaType),
                PowerStatus = b.PowerStatus,
                IsUnderConstruction = b.IsUnderConstruction,
                ContentRanking = ranking,
                ContentValue = b.ContentValue,
                ContentBudgetPerTick = b.ContentBudgetPerTick,
                IsGovernmentOwned = b.IsGovernmentOwned,
            };
        }).ToList();

        // Sort: player-owned outlets first (if ownerCompanyId provided), then by ContentRanking desc.
        return infos
            .OrderBy(mh => ownerCompanyId.HasValue && mh.OwnerCompanyId == ownerCompanyId.Value ? 0 : 1)
            .ThenByDescending(mh => mh.ContentRanking)
            .ThenBy(mh => mh.Name)
            .ToList();
    }

    /// <summary>
    /// Returns brand awareness and quality metrics for products of a company. Requires authentication.
    /// Consumers: building-detail marketing unit configuration, analytics dashboards.
    /// Delegates to companyBrands — use that query for brand awareness reads.
    /// </summary>
    [Authorize]
    public async Task<List<ResearchBrandState>> GetCompanyMarketingStats(
        Guid companyId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == companyId && c.PlayerId == userId);
        if (company is null) return [];

        var brands = await db.Brands.Where(b => b.CompanyId == companyId).ToListAsync();
        var productTypeIds = brands.Where(b => b.ProductTypeId.HasValue).Select(b => b.ProductTypeId!.Value).Distinct().ToList();
        var productTypes = await db.ProductTypes.Where(pt => productTypeIds.Contains(pt.Id)).ToDictionaryAsync(pt => pt.Id);

        var ownBudgets = await db.ProductResearchBudgets
            .Where(rb => rb.CompanyId == companyId && productTypeIds.Contains(rb.ProductTypeId))
            .ToDictionaryAsync(rb => rb.ProductTypeId);
        var maxBudgets = await db.ProductResearchBudgets
            .Where(rb => productTypeIds.Contains(rb.ProductTypeId))
            .GroupBy(rb => rb.ProductTypeId)
            .Select(g => new { g.Key, Max = g.Max(rb => rb.AccumulatedBudget) })
            .ToDictionaryAsync(x => x.Key, x => x.Max);

        // Load EUR→USD rate to convert the EUR-denominated baseQualityBudget to USD.
        // AccumulatedBudget and MaxCompetitorBudget are already stored in USD.
        var fxRates = await Utilities.FxRateHelper.BuildEurRatesLookupAsync(db, ["USD"]);
        var usdEurRate = Utilities.FxRateHelper.GetEurRate(fxRates, "USD");

        return brands.Select(b =>
        {
            var pt = b.ProductTypeId.HasValue ? productTypes.GetValueOrDefault(b.ProductTypeId.Value) : null;
            decimal? accBudget = b.ProductTypeId.HasValue && ownBudgets.TryGetValue(b.ProductTypeId.Value, out var rb) ? rb.AccumulatedBudget : null;
            decimal? baseBudget = pt is not null ? Engine.GameConstants.ResearchBaseQualityBudget(pt.BasePrice) * usdEurRate : null;
            decimal? maxBudget = b.ProductTypeId.HasValue && maxBudgets.TryGetValue(b.ProductTypeId.Value, out var mb) ? mb : null;
            var rdQuality = Math.Clamp(b.Quality, 0m, 1m);
            var mktQuality = Math.Clamp(b.MarketingQuality, 0m, 1m);
            var combined = Math.Clamp(1m - (1m - rdQuality) * (1m - mktQuality), 0m, 1m);
            return new ResearchBrandState
            {
                Id = b.Id,
                CompanyId = b.CompanyId,
                Name = b.Name,
                Scope = b.Scope,
                ProductTypeId = b.ProductTypeId,
                ProductName = pt?.Name,
                IndustryCategory = b.IndustryCategory,
                Awareness = b.Awareness,
                Quality = b.Quality,
                MarketingQuality = mktQuality,
                CombinedBrandQuality = combined,
                MarketingEfficiencyMultiplier = b.MarketingEfficiencyMultiplier,
                AccumulatedResearchBudget = accBudget,
                BaseResearchBudget = baseBudget,
                MaxCompetitorBudget = maxBudget,
            };
        }).ToList();
    }
}

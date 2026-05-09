using Api.Data.Entities;

namespace Api.Engine.Phases;

/// <summary>
/// Sells products from PUBLIC_SALES units to the city population.
/// Demand is driven by city population, price competitiveness, product quality,
/// and brand awareness. When multiple sellers compete in the same city for the
/// same product, total city demand is shared proportionally based on each
/// seller's competitiveness score (price index × quality × brand).
/// A public-sell index then limits each offer to 0-50% of its current stock
/// based on market saturation, competition, quality, brand, and lot location,
/// while a separate price index (0-1) can reduce sales all the way to zero for
/// extreme markups.
/// Revenue is credited to the owning company.
/// Runs early so that sales consume inventory produced in prior ticks.
/// </summary>
public sealed partial class PublicSalesPhase : ITickPhase
{
    public string Name => "PublicSales";
    public int Order => 200;

    /// <summary>
    /// Collects all potential sales offers across all shops in a city, then
    /// distributes city-level demand proportionally among competing sellers.
    /// </summary>
    public async Task ProcessAsync(TickContext context)
    {
        if (!context.BuildingsByType.TryGetValue(BuildingType.SalesShop, out var shops))
            return;

        // ── Phase 1: Gather every seller's offer for each (city, product) pair ──
        var offers = new List<SalesOffer>();

        foreach (var building in shops)
        {
            if (!context.UnitsByBuilding.TryGetValue(building.Id, out var units))
                continue;
            if (!context.CitiesById.TryGetValue(building.CityId, out var city))
                continue;
            if (!context.CompaniesById.TryGetValue(building.CompanyId, out var company))
                continue;

            var efficiency = TickContext.GetPowerEfficiency(building);
            if (efficiency <= 0m) continue;

            // Skip buildings suspended for insufficient funds (evaluated by OperatingCostPhase).
            if (building.IsSuspendedForFunds) continue;

            foreach (var unit in units)
            {
                if (unit.UnitType != UnitType.PublicSales) continue;
                if (context.UnitsUnderUpgrade.Contains(unit.Id)) continue;
                if (!context.InventoryByUnit.TryGetValue(unit.Id, out var inventories))
                    continue;

                context.LotsByBuildingId.TryGetValue(building.Id, out var lot);

                foreach (var inv in inventories)
                {
                    if (inv.Quantity <= 0m) continue;

                    Guid? itemId = inv.ProductTypeId ?? inv.ResourceTypeId;
                    if (itemId is null) continue;

                    decimal basePrice;
                    string? industry = null;
                    string? productName = null;
                    var priceElasticity = PublicSalesPricingModel.DefaultPriceElasticity;
                    decimal? brandAwareness = null;
                    if (inv.ProductTypeId.HasValue && context.ProductTypesById.TryGetValue(inv.ProductTypeId.Value, out var pt))
                    {
                        basePrice = pt.BasePrice;
                        industry = pt.Industry;
                        productName = pt.Name;
                        priceElasticity = pt.PriceElasticity;
                    }
                    else if (inv.ResourceTypeId.HasValue && context.ResourceTypesById.TryGetValue(inv.ResourceTypeId.Value, out var rt))
                    {
                        basePrice = rt.BasePrice;
                    }
                    else
                    {
                        continue;
                    }

                    // BasePrice is stored in EUR. Convert to the city's local currency so that
                    // price-index comparisons (price vs basePrice) are in the same unit of account.
                    // Without this, a Prague seller pricing at 1 500 CZK would compare against a
                    // 45 EUR base price and appear ~33× overpriced, killing all demand.
                    var cityFxRate = context.GetCityFxRate(city);
                    var localBasePrice = basePrice * cityFxRate;

                    var price = unit.MinPrice ?? localBasePrice;
                    if (price <= 0m) price = localBasePrice;

                    var populationIndex = lot?.PopulationIndex > 0m ? lot.PopulationIndex : 1m;
                    var priceIndex = PublicSalesPricingModel.ComputePriceIndex(localBasePrice, price, priceElasticity);
                    var qualityMultiplier = Math.Max(0.15m, inv.Quality);
                    var qualityDemandFactor = ComputeQualityDemandFactor(inv.Quality);

                    var brand = context.FindCombinedBrand(building.CompanyId, inv.ProductTypeId, industry);
                    brandAwareness = Math.Clamp(brand?.Awareness ?? 0m, 0m, 1m);
                    var brandQuality = ComputeCombinedBrandQuality(brand);
                    var brandFactor = ComputeBrandFactor(brandAwareness.Value, brandQuality);

                    // Competitiveness score determines market-share allocation.
                    // PopulationIndex represents foot traffic / location advantage.
                    var competitiveness = priceIndex * qualityMultiplier * brandFactor * populationIndex;
                    if (competitiveness <= 0m) continue;

                    offers.Add(new SalesOffer
                    {
                        CityId = building.CityId,
                        ItemId = itemId.Value,
                        Building = building,
                        Unit = unit,
                        City = city,
                        Company = company,
                        Lot = lot,
                        Inventory = inv,
                        BasePrice = localBasePrice,
                        Price = price,
                        Industry = industry,
                        ProductName = productName,
                        PopulationIndex = populationIndex,
                        PriceElasticity = priceElasticity,
                        PriceIndex = priceIndex,
                        QualityDemandFactor = qualityDemandFactor,
                        BrandAwareness = brandAwareness.Value,
                        BrandQuality = brandQuality,
                        BrandFactor = brandFactor,
                        Competitiveness = competitiveness,
                        CurrentStock = inv.Quantity,
                        MaxCanSell = inv.Quantity,
                    });
                }
            }
        }

        // ── Phase 2: Distribute demand per (city, product) among competing sellers ──
        var grouped = offers.GroupBy(o => (o.CityId, o.ItemId));

        // Track actual sales per unit to enforce sales capacity across products.
        var unitSoldTotals = new Dictionary<Guid, decimal>();

        // Pre-compute unit sales capacity to avoid redundant recalculations.
        var unitCapacityCache = new Dictionary<Guid, decimal>();

        foreach (var group in grouped)
        {
            var groupList = group.ToList();
            var firstOffer = groupList[0];
            var city = firstOffer.City;
            var itemId = firstOffer.ItemId;
            var trendKey = (city.Id, itemId);

            // ── Market trend factor ──────────────────────────────────────────────
            // Load or create the persisted trend state for this (city, product) pair.
            // The trend factor is a demand multiplier in [0.5, 1.5].
            if (!context.TrendStatesByKey.TryGetValue(trendKey, out var trendState))
            {
                trendState = new MarketTrendState
                {
                    Id = Guid.NewGuid(),
                    CityId = city.Id,
                    ItemId = itemId,
                    TrendFactor = GameConstants.TrendNeutral,
                    LastUpdatedTick = context.CurrentTick,
                };
                context.Db.MarketTrendStates.Add(trendState);
                context.TrendStatesByKey[trendKey] = trendState;
            }

            var trendFactor = Math.Clamp(trendState.TrendFactor, GameConstants.TrendMin, GameConstants.TrendMax);

            // ── Bounded random variation ─────────────────────────────────────────
            // Apply a deterministic (tick + city + item seeded) ±TrendRandomAmplitude
            // variation so identical products do not always produce identical sales.
            // The seed is stable per (tick, city, item) combination for reproducibility.
            var randomSeed = (int)(
                (context.CurrentTick * 2654435761L)
                ^ ((long)city.Id.GetHashCode() * 104395301L)
                ^ ((long)itemId.GetHashCode() * 40503L)) & int.MaxValue;
            var rng = new Random(randomSeed);
            var randomMultiplier = 1m + (decimal)(rng.NextDouble() * 2.0 - 1.0)
                * GameConstants.TrendRandomAmplitude;
            randomMultiplier = Math.Clamp(
                randomMultiplier,
                1m - GameConstants.TrendRandomAmplitude,
                1m + GameConstants.TrendRandomAmplitude);

            // City-level base demand for this product (population-driven, no location bias).
            // Salary purchasing power uses a blended signal:
            //   – static wage level (BaseSalaryPerManhour) — city baseline
            //   – dynamic recent spending (actual LaborCost ledger sum for past 10 ticks)
            // This directly implements the ROADMAP requirement:
            // "game currency collected by salaries in past 10 ticks".
            context.RecentSalaryByCity.TryGetValue(city.Id, out var recentSalary);
            var salaryFactor = PublicSalesPricingModel.ComputeBlendedSalaryFactor(
                city.BaseSalaryPerManhour, recentSalary, city.Population);

            // ── Seasonal demand multiplier ────────────────────────────────────────
            // The seasonal multiplier adjusts city demand based on the current
            // game-year quarter (Q1=Jan–Mar, Q2=Apr–Jun, Q3=Jul–Sep, Q4=Oct–Dec).
            // This implements the ROADMAP "Seasonal Demand" mechanic.
            // Products without a DemandSeasonality row default to 1.0× (neutral).
            var seasonalMultiplier = 1.0m;
            var seasonalEventMultiplier = context.GetSeasonalDemandEventMultiplier(city.Id);
            if (firstOffer.Inventory?.ProductTypeId.HasValue == true
                && context.SeasonalityByProductTypeId.TryGetValue(
                    firstOffer.Inventory.ProductTypeId!.Value, out var seasonality))
            {
                var quarterIndex = (int)((context.CurrentTick / GameConstants.TicksPerQuarter) % 4);
                seasonalMultiplier = seasonality.GetMultiplierForQuarter(quarterIndex);
            }

            // Apply trend and random multipliers to the base city demand.
            var cityBaseDemand = city.Population * GameConstants.BaseDemandPerCapita
                * salaryFactor
                * trendFactor
                * randomMultiplier
                * seasonalMultiplier
                * seasonalEventMultiplier
                * context.EconomicCycleIntensity;
            if (cityBaseDemand <= 0m)
                continue;

            var totalCurrentStock = groupList.Sum(o => o.CurrentStock);
            var saturationFactor = ComputeSaturationFactor(cityBaseDemand, totalCurrentStock);
            var maxPopulationIndex = Math.Max(1m, groupList.Max(o => o.PopulationIndex));
            var weightedDemandAttractiveness = totalCurrentStock > 0m
                ? groupList.Sum(offer => offer.CurrentStock * ComputeDemandAttractiveness(offer, maxPopulationIndex)) / totalCurrentStock
                : 0m;
            var effectiveCityDemand = cityBaseDemand * ComputeMarketDemandFactor(saturationFactor, weightedDemandAttractiveness);
            if (effectiveCityDemand <= 0m)
                continue;

            // Total competitiveness of all sellers (used for market-share split).
            var totalCompetitiveness = groupList.Sum(o => o.Competitiveness);
            if (totalCompetitiveness <= 0m)
                continue;

            // Track group-level totals for trend evolution after all offers are processed.
            decimal groupTotalSold = 0m;
            decimal groupTotalCapacity = 0m;

            foreach (var offer in groupList)
            {
                // Market share: each seller's fraction of city demand based on competitiveness.
                // For a single seller, marketShare = 1.0 and demand = effectiveCityDemand.
                // For multiple sellers, the effective demand is proportionally split based on
                // each seller's competitiveness so stronger offers win a larger share.
                var marketShare = offer.Competitiveness / totalCompetitiveness;
                var demand = effectiveCityDemand * marketShare;

                // Enforce unit-level sales capacity.
                unitSoldTotals.TryGetValue(offer.Unit.Id, out var unitSoldSoFar);

                if (!unitCapacityCache.TryGetValue(offer.Unit.Id, out var salesCapacity))
                {
                    salesCapacity = GameConstants.SalesCapacity(offer.Unit.Level)
                        * TickContext.GetPowerEfficiency(offer.Building);
                    unitCapacityCache[offer.Unit.Id] = salesCapacity;
                }

                groupTotalCapacity += salesCapacity;

                var remainingCapacity = Math.Max(0m, salesCapacity - unitSoldSoFar);
                if (remainingCapacity <= 0m)
                    continue;

                var locationFactor = ComputeLocationFactor(offer.PopulationIndex, maxPopulationIndex);
                var competitionFactor = ComputeCompetitionFactor(marketShare);
                var publicSellIndex = ComputePublicSellIndex(
                    saturationFactor,
                    offer.QualityDemandFactor,
                    offer.BrandFactor,
                    locationFactor,
                    competitionFactor);
                var stockTurnoverCap = offer.CurrentStock * 0.5m * publicSellIndex * offer.PriceIndex;
                if (stockTurnoverCap <= 0m)
                    continue;

                var sold = Math.Min(
                    demand,
                    Math.Min(stockTurnoverCap, Math.Min(offer.MaxCanSell, remainingCapacity)));
                sold = Math.Max(0m, Math.Floor(sold * 10000m) / 10000m);
                if (sold <= 0m) continue;

                groupTotalSold += sold;

                // Determine the funding account that will receive the revenue.
                var fundingAccount = context.GetBuildingFundingAccount(offer.Building);
                if (fundingAccount is null)
                {
                    // Cannot allocate revenue without a destination account; skip this sale.
                    continue;
                }

                // Record ledger entry with the correct bank account ID.
                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = offer.Company.Id,
                    BuildingId = offer.Building.Id,
                    BuildingUnitId = offer.Unit.Id,
                    BankAccountId = fundingAccount.Id,
                    Category = LedgerCategory.Revenue,
                    Description = offer.ProductName is not null ? $"Public sales: {offer.ProductName}" : "Public sales",
                    Amount = sold * offer.Price,
                    RecordedAtTick = context.CurrentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                    ProductTypeId = offer.Inventory.ProductTypeId,
                    ResourceTypeId = offer.Inventory.ResourceTypeId,
                });

                // Record sales snapshot including the active trend factor for analytics.
                context.Db.PublicSalesRecords.Add(new PublicSalesRecord
                {
                    Id = Guid.NewGuid(),
                    BuildingUnitId = offer.Unit.Id,
                    BuildingId = offer.Building.Id,
                    CompanyId = offer.Company.Id,
                    CityId = offer.Building.CityId,
                    ProductTypeId = offer.Inventory.ProductTypeId,
                    ResourceTypeId = offer.Inventory.ResourceTypeId,
                    Tick = context.CurrentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                    QuantitySold = sold,
                    PricePerUnit = offer.Price,
                    Revenue = sold * offer.Price,
                    Demand = demand,
                    SalesCapacity = salesCapacity,
                    TrendFactor = trendFactor,
                });

                context.RecordUnitResourceHistory(
                    offer.Building.Id,
                    offer.Unit.Id,
                    offer.Inventory.ResourceTypeId,
                    offer.Inventory.ProductTypeId,
                    outflowQuantity: sold);
                context.WithdrawInventory(offer.Inventory, sold);
                fundingAccount.Balance += sold * offer.Price;
                unitSoldTotals[offer.Unit.Id] = unitSoldSoFar + sold;
            }

            // ── Trend evolution ──────────────────────────────────────────────────
            // Update the trend factor based on how well this group performed.
            // groupTotalCapacity > 0 guard prevents division-by-zero for groups that
            // had no capacity available (e.g. all units at max capacity).
            var groupUtilisation = groupTotalCapacity > 0m
                ? Math.Clamp(groupTotalSold / groupTotalCapacity, 0m, 1m)
                : 0m;

            decimal updatedTrendFactor;
            if (groupUtilisation >= GameConstants.TrendStrongUtilisationThreshold)
            {
                // Strong sales → trend rises toward TrendMax.
                updatedTrendFactor = Math.Clamp(
                    trendFactor + GameConstants.TrendRiseRate,
                    GameConstants.TrendMin, GameConstants.TrendMax);
            }
            else if (groupUtilisation < GameConstants.TrendWeakUtilisationThreshold
                     && totalCurrentStock > effectiveCityDemand)
            {
                // Weak sales AND ample supply (not supply-constrained) → trend falls toward TrendMin.
                updatedTrendFactor = Math.Clamp(
                    trendFactor - GameConstants.TrendFallRate,
                    GameConstants.TrendMin, GameConstants.TrendMax);
            }
            else
            {
                // Moderate performance → decay toward neutral (1.0).
                var gap = GameConstants.TrendNeutral - trendFactor;
                updatedTrendFactor = Math.Clamp(
                    trendFactor + gap * GameConstants.TrendDecayFraction,
                    GameConstants.TrendMin, GameConstants.TrendMax);
            }

            trendState.TrendFactor = updatedTrendFactor;
            trendState.LastUpdatedTick = context.CurrentTick;

            // ── Passive brand awareness from public sales ────────────────────────
            // Each company selling in this city receives a small brand awareness
            // gain or decay depending on how their product quality compares to
            // the city average quality for this product.
            // - Quality > city average OR only seller → small awareness gain
            // - Quality < city average → small awareness decay
            // This incentivises R&D investment and rewards market leadership
            // without requiring dedicated marketing spend.
            var sellersInGroup = groupList.Count;
            var cityAvgQuality = groupList.Sum(o => o.CurrentStock) > 0m
                ? groupList.Sum(o => o.Inventory.Quality * o.CurrentStock) / groupList.Sum(o => o.CurrentStock)
                : groupList.Average(o => o.Inventory.Quality);

            foreach (var offer in groupList)
            {
                if (offer.Inventory.ProductTypeId is null || offer.Industry is null)
                    continue;

                var brand = context.FindBrand(offer.Building.CompanyId, offer.Inventory.ProductTypeId, offer.Industry)
                    ?? context.GetOrCreateBrand(offer.Building.CompanyId, offer.Inventory.ProductTypeId.Value,
                        offer.ProductName ?? offer.Industry);

                var quality = offer.Inventory.Quality;
                if (sellersInGroup == 1 || quality > cityAvgQuality)
                {
                    brand.Awareness = Math.Clamp(brand.Awareness + GameConstants.PassiveBrandAwarenessGainRate, 0m, 1m);
                }
                else if (quality < cityAvgQuality)
                {
                    brand.Awareness = Math.Clamp(brand.Awareness - GameConstants.PassiveBrandAwarenessDecayRate, 0m, 1m);
                }
            }
        }
    }

}

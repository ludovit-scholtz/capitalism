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
    private static readonly IReadOnlyList<(string Name, string Slug, string Category, decimal BasePrice, decimal WeightPerUnit, string UnitName, string UnitSymbol, string Description, string Icon, string BackgroundColor, string AccentColor)> CanonicalResourceCatalog =
    [
        ("Wood", "wood", "ORGANIC", 10m, 5m, "Ton", "t", "Harvested timber used in furniture, packaging, and construction.", "🪵", "#8B5A2B", "#D4A373"),
        ("Iron Ore", "iron-ore", "MINERAL", 25m, 10m, "Ton", "t", "Raw iron ore used to smelt metal components, fasteners, and structural goods.", "⛏️", "#6B7280", "#9CA3AF"),
        ("Coal", "coal", "MINERAL", 8m, 8m, "Ton", "t", "Industrial fuel used in heat-intensive processing, metallurgy, and battery chemistry.", "🪨", "#1F2937", "#4B5563"),
        ("Gold", "gold", "MINERAL", 500m, 0.1m, "Kilogram", "kg", "Precious conductive metal used in premium electronics and contact surfaces.", "🥇", "#B45309", "#F59E0B"),
        ("Chemical Minerals", "chemical-minerals", "MINERAL", 30m, 3m, "Ton", "t", "Industrial mineral feedstock used in chemicals, coatings, polymers, and medicine.", "🧪", "#7C3AED", "#A78BFA"),
        ("Cotton", "cotton", "ORGANIC", 15m, 1m, "Ton", "t", "Soft natural fibre used in healthcare textiles, insulation, and consumer fabric goods.", "🧵", "#E5E7EB", "#94A3B8"),
        ("Grain", "grain", "ORGANIC", 5m, 2m, "Ton", "t", "Agricultural staple milled into flour and processed into packaged foods.", "🌾", "#CA8A04", "#FCD34D"),
        ("Silicon", "silicon", "MINERAL", 40m, 2m, "Kilogram", "kg", "High-purity mineral used for wafers, glass, and electronics manufacturing.", "💠", "#0EA5E9", "#67E8F9"),
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<(string ResourceSlug, decimal Abundance)>> CanonicalCityResourceFocus
        = new Dictionary<string, IReadOnlyList<(string ResourceSlug, decimal Abundance)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Bratislava"] = [("wood", 0.7m), ("grain", 0.6m), ("iron-ore", 0.4m), ("chemical-minerals", 0.3m)],
            ["Prague"] = [("wood", 0.7m), ("grain", 0.6m), ("coal", 0.6m), ("silicon", 0.3m)],
            ["Vienna"] = [("wood", 0.7m), ("grain", 0.6m), ("cotton", 0.5m), ("gold", 0.1m)],
            ["New York"] = [("wood", 0.7m), ("grain", 0.6m), ("silicon", 0.5m), ("coal", 0.4m), ("iron-ore", 0.3m)],
            ["London"] = [("wood", 0.7m), ("grain", 0.6m), ("cotton", 0.4m), ("gold", 0.2m), ("coal", 0.5m)],
            ["Beijing"] = [("wood", 0.7m), ("grain", 0.6m), ("coal", 0.8m), ("iron-ore", 0.7m), ("silicon", 0.6m)],
            ["Delhi"] = [("wood", 0.7m), ("grain", 0.6m), ("chemical-minerals", 0.5m), ("cotton", 0.7m), ("iron-ore", 0.4m)],
        };

    /// <summary>Lists building lots for a city, including ownership and availability state.</summary>
    public async Task<List<BuildingLot>> GetCityLots(Guid cityId, [Service] AppDbContext db)
    {
        await EnsureCanonicalResourceCatalogAsync(db);
        await EnsureCanonicalCityResourceFocusAsync(cityId, db);

        var currentTick = await db.GameStates
            .Select(state => (long?)state.CurrentTick)
            .FirstOrDefaultAsync() ?? 0L;

        // Read-time self-heal for local/stale databases: mine lots must always have
        // resource, quality, and quantity, and each city must keep per-resource mine coverage.
        await LandService.EnsureMinimumAvailableLotsAsync(db, currentTick, [cityId]);
        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync();
        }

        return await db.BuildingLots
            .Include(lot => lot.OwnerCompany)
            .Include(lot => lot.Building)
            .Include(lot => lot.ResourceType)
            .Where(lot => lot.CityId == cityId)
            .OrderBy(lot => lot.District)
            .ThenBy(lot => lot.Name)
            .ToListAsync();
    }

    private static async Task EnsureCanonicalResourceCatalogAsync(AppDbContext db)
    {
        var existingSlugs = await db.ResourceTypes
            .Select(resource => resource.Slug)
            .ToListAsync();
        var existingSlugSet = existingSlugs
            .Where(slug => !string.IsNullOrWhiteSpace(slug))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var addedAny = false;
        foreach (var seed in CanonicalResourceCatalog)
        {
            if (existingSlugSet.Contains(seed.Slug))
            {
                continue;
            }

            db.ResourceTypes.Add(new ResourceType
            {
                Id = Guid.NewGuid(),
                Name = seed.Name,
                Slug = seed.Slug,
                Category = seed.Category,
                BasePrice = seed.BasePrice,
                WeightPerUnit = seed.WeightPerUnit,
                UnitName = seed.UnitName,
                UnitSymbol = seed.UnitSymbol,
                Description = seed.Description,
                ImageUrl = CreateEmojiImageDataUrl(seed.Icon, seed.BackgroundColor, seed.AccentColor),
            });
            existingSlugSet.Add(seed.Slug);
            addedAny = true;
        }

        if (addedAny)
        {
            await db.SaveChangesAsync();
        }
    }

    private static string CreateEmojiImageDataUrl(string emoji, string backgroundColor, string accentColor)
    {
        var svg = $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 128 128'><defs><linearGradient id='g' x1='0%' y1='0%' x2='100%' y2='100%'><stop offset='0%' stop-color='{backgroundColor}'/><stop offset='100%' stop-color='{accentColor}'/></linearGradient></defs><rect width='128' height='128' rx='24' fill='url(#g)'/><text x='64' y='78' font-size='56' text-anchor='middle'>{emoji}</text></svg>";
        return $"data:image/svg+xml;utf8,{Uri.EscapeDataString(svg)}";
    }

    private static async Task EnsureCanonicalCityResourceFocusAsync(Guid cityId, AppDbContext db)
    {
        var city = await db.Cities
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == cityId);
        if (city is null || !CanonicalCityResourceFocus.TryGetValue(city.Name, out var expectedResources))
        {
            return;
        }

        var resourcesBySlug = await db.ResourceTypes
            .ToDictionaryAsync(resource => resource.Slug, StringComparer.OrdinalIgnoreCase);
        var existingResourceIds = await db.CityResources
            .Where(cityResource => cityResource.CityId == cityId)
            .Select(cityResource => cityResource.ResourceTypeId)
            .ToListAsync();
        var existingResourceIdSet = existingResourceIds.ToHashSet();

        var addedAny = false;
        foreach (var (resourceSlug, abundance) in expectedResources)
        {
            if (!resourcesBySlug.TryGetValue(resourceSlug, out var resource) || existingResourceIdSet.Contains(resource.Id))
            {
                continue;
            }

            db.CityResources.Add(new CityResource
            {
                Id = Guid.NewGuid(),
                CityId = cityId,
                ResourceTypeId = resource.Id,
                Abundance = abundance,
            });
            existingResourceIdSet.Add(resource.Id);
            addedAny = true;
        }

        if (addedAny)
        {
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Gets a single building lot by ID.</summary>
    public async Task<BuildingLot?> GetLot(Guid id, [Service] AppDbContext db)
    {
        return await db.BuildingLots
            .Include(lot => lot.OwnerCompany)
            .Include(lot => lot.Building)
            .Include(lot => lot.ResourceType)
            .FirstOrDefaultAsync(lot => lot.Id == id);
    }

    /// <summary>
    /// Returns product marketplace listings from active player SELL exchange orders.
    /// Covers finished goods, intermediate goods, and any product types on the exchange.
    /// This query is public and does not require authentication.
    /// </summary>
    public async Task<List<GlobalExchangeProductListing>> GetGlobalExchangeProductListings(
        Guid? productTypeId,
        [Service] AppDbContext db)
    {
        var ordersQuery = db.ExchangeOrders
            .Where(o => o.Side == "SELL" && o.IsActive && o.RemainingQuantity > 0m
                        && o.ProductTypeId.HasValue && !o.ResourceTypeId.HasValue)
            .Include(o => o.Company)
            .Include(o => o.ExchangeBuilding)
            .AsQueryable();

        if (productTypeId.HasValue)
            ordersQuery = ordersQuery.Where(o => o.ProductTypeId == productTypeId.Value);

        var orders = await ordersQuery
            .OrderBy(o => o.ProductTypeId)
            .ThenBy(o => o.PricePerUnit)
            .ToListAsync();

        var cityIds = orders.Select(o => o.ExchangeBuilding.CityId).Distinct().ToList();
        var cities = await db.Cities
            .Where(c => cityIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);

        var productTypeIds = orders.Select(o => o.ProductTypeId!.Value).Distinct().ToList();
        var productTypes = await db.ProductTypes
            .Where(p => productTypeIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        return orders
            .Select(o =>
            {
                cities.TryGetValue(o.ExchangeBuilding.CityId, out var city);
                productTypes.TryGetValue(o.ProductTypeId!.Value, out var product);
                return new GlobalExchangeProductListing
                {
                    OrderId = o.Id,
                    ProductTypeId = o.ProductTypeId!.Value,
                    ProductName = product?.Name ?? string.Empty,
                    ProductSlug = product?.Slug ?? string.Empty,
                    ProductIndustry = product?.Industry ?? string.Empty,
                    UnitSymbol = product?.UnitSymbol ?? string.Empty,
                    UnitName = product?.UnitName ?? string.Empty,
                    BasePrice = product?.BasePrice ?? 0m,
                    PricePerUnit = o.PricePerUnit,
                    RemainingQuantity = o.RemainingQuantity,
                    SellerCityId = city?.Id ?? Guid.Empty,
                    SellerCityName = city?.Name ?? string.Empty,
                    SellerCompanyId = o.CompanyId,
                    SellerCompanyName = o.Company.Name,
                    CreatedAtUtc = o.CreatedAtUtc,
                };
            })
            .OrderBy(l => l.ProductName)
            .ThenBy(l => l.PricePerUnit)
            .ToList();
    }

    /// <summary>
    /// Returns city-level global exchange offers for raw materials, including
    /// quality and estimated transit cost into the destination city.
    /// </summary>
    public async Task<List<GlobalExchangeOffer>> GetGlobalExchangeOffers(
        Guid destinationCityId,
        Guid? resourceTypeId,
        [Service] AppDbContext db)
    {
        var destinationCity = await db.Cities.FirstOrDefaultAsync(city => city.Id == destinationCityId);
        if (destinationCity is null)
        {
            return [];
        }

        // Get FX rate so all exchange prices are shown in the destination city's local currency.
        var fxRate = await ComputeForexRateAsync(db, "EUR", destinationCity.CurrencyCode);
        var fuelPriceIndex = destinationCity.FuelPriceIndex;
        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(state => state.CurrentTick)
            .FirstOrDefaultAsync();

        var cities = await db.Cities
            .Include(city => city.Resources)
            .OrderBy(city => city.Name)
            .ToListAsync();

        var resourceQuery = db.ResourceTypes.AsQueryable();
        if (resourceTypeId.HasValue)
        {
            resourceQuery = resourceQuery.Where(resource => resource.Id == resourceTypeId.Value);
        }

        var resources = await resourceQuery
            .OrderBy(resource => resource.Name)
            .ToListAsync();

        var activeCommodityEvents = await db.MarketEvents
            .Where(me => me.EventType == MarketEventType.CommodityShock
                && me.StartsAtTick <= currentTick
                && me.ExpiresAtTick >= currentTick
                && me.AffectedResourceTypeId.HasValue)
            .ToListAsync();

        var commodityMultiplierByResourceId = activeCommodityEvents
            .GroupBy(me => me.AffectedResourceTypeId!.Value)
            .ToDictionary(
                group => group.Key,
                group => Math.Clamp(
                    group.Aggregate(1m, (acc, next) => acc * next.MagnitudeMultiplier),
                    GameConstants.MarketEventMultiplierMin,
                    GameConstants.MarketEventMultiplierMax));

        return cities
            .SelectMany(city => resources.Select(resource =>
            {
                var abundance = city.Resources
                    .FirstOrDefault(entry => entry.ResourceTypeId == resource.Id)?.Abundance
                    ?? GlobalExchangeCalculator.DefaultMissingAbundance;
                var commodityMultiplier = commodityMultiplierByResourceId.GetValueOrDefault(resource.Id, 1m);
                var exchangePrice = GlobalExchangeCalculator.ComputeExchangePrice(city, resource, abundance, fxRate)
                    * commodityMultiplier;
                var transitCost = GlobalExchangeCalculator.ComputeTransitCostPerUnit(city, destinationCity, resource, fxRate, fuelPriceIndex);

                return new GlobalExchangeOffer
                {
                    CityId = city.Id,
                    CityName = city.Name,
                    ResourceTypeId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceSlug = resource.Slug,
                    UnitSymbol = resource.UnitSymbol,
                    LocalAbundance = decimal.Round(abundance, 4, MidpointRounding.AwayFromZero),
                    ExchangePricePerUnit = exchangePrice,
                    EstimatedQuality = GlobalExchangeCalculator.ComputeExchangeQuality(abundance),
                    QualityMin = GlobalExchangeCalculator.ComputeExchangeQualityBand(abundance).min,
                    QualityMax = GlobalExchangeCalculator.ComputeExchangeQualityBand(abundance).max,
                    TransitCostPerUnit = transitCost,
                    DeliveredPricePerUnit = exchangePrice + transitCost,
                    DistanceKm = decimal.Round(
                        (decimal)GlobalExchangeCalculator.ComputeDistanceKm(
                            city.Latitude,
                            city.Longitude,
                            destinationCity.Latitude,
                            destinationCity.Longitude),
                        1,
                        MidpointRounding.AwayFromZero),
                    FuelPriceIndex = fuelPriceIndex,
                    AskPriceHistory = BuildResourceAskPriceHistory(currentTick, exchangePrice, city.Id, resource.Id),
                };
            }))
            .OrderBy(offer => offer.DeliveredPricePerUnit)
            .ThenByDescending(offer => offer.EstimatedQuality)
            .ThenBy(offer => offer.CityName)
            .ToList();
    }

    private static List<ResourceAskPricePoint> BuildResourceAskPriceHistory(
        long currentTick,
        decimal currentAskPrice,
        Guid cityId,
        Guid resourceId)
    {
        const int historyWindow = 50;
        var seed = Math.Abs(HashCode.Combine(cityId, resourceId));

        var ticks = new List<long>(historyWindow);
        var factors = new List<decimal>(historyWindow);

        for (var offset = historyWindow - 1; offset >= 0; offset--)
        {
            var tick = currentTick - offset;
            if (tick < 0)
            {
                continue;
            }

            var waveA = (decimal)Math.Sin((tick + seed) * 0.17d);
            var waveB = (decimal)Math.Cos((tick + seed * 0.5d) * 0.07d);
            var seasonal = 1m + waveA * 0.025m + waveB * 0.015m;
            var drift = 1m + ((seed % 11) - 5) * 0.0005m * (historyWindow - 1 - offset);

            ticks.Add(tick);
            factors.Add(Math.Max(0.5m, seasonal * drift));
        }

        if (ticks.Count == 0)
        {
            return [];
        }

        var anchorFactor = factors[^1] <= 0m ? 1m : factors[^1];

        return ticks
            .Select((tick, index) => new ResourceAskPricePoint
            {
                Tick = tick,
                AskPricePerUnit = decimal.Round(
                    currentAskPrice * (factors[index] / anchorFactor),
                    2,
                    MidpointRounding.AwayFromZero),
            })
            .ToList();
    }
}

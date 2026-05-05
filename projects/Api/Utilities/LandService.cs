using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Microsoft.EntityFrameworkCore;

namespace Api.Utilities;

/// <summary>
/// Creates and reappraises land so buildings always attach to real map parcels.
/// </summary>
public static partial class LandService
{
    private sealed record CityResourceProfile(CityResource CityResource, bool IsNativeInCity);

    private const decimal MinPopulationIndex = 0.35m;
    private const decimal MaxPopulationIndex = 1.85m;
    private const double NeighborhoodRadiusKm = 1.5d;
    private const decimal MineLotMinPrice = 20_000_000m;
    private const decimal MineLotMaxPrice = 200_000_000m;

    /// <summary>
    /// Strategic multiplier applied to the quality-discounted spot value of the raw material
    /// deposit when computing the asking price of a mine lot.
    /// Rationale: mining land is a long-term strategic asset — the price reflects not just the
    /// current spot value of the deposit but also extraction rights, scarcity, and future
    /// production advantage. A multiplier of 100 means the land price equals 100× the
    /// total spot value of the extractable reserve, which puts typical mine lots in the
    /// $20M–$200M band depending on resource type, deposit quality, and estimated quantity.
    /// Non-mine lots with no resource data are unaffected (ComputeResourcePremium returns 0).
    /// </summary>
    public const decimal ResourcePremiumCaptureRate = 100m;

    public static async Task EnsureMinimumAvailableLotsAsync(
        AppDbContext db,
        long currentTick,
        IEnumerable<Guid>? cityIds = null)
    {
        var cityIdSet = cityIds?.Distinct().ToHashSet();

        var cities = await db.Cities
            .Where(city => cityIdSet == null || cityIdSet.Contains(city.Id))
            .ToListAsync();

        var lots = await db.BuildingLots
            .Include(lot => lot.ResourceType)
            .Where(lot => cityIdSet == null || cityIdSet.Contains(lot.CityId))
            .ToListAsync();

        var buildings = await db.Buildings
            .Where(building => cityIdSet == null || cityIdSet.Contains(building.CityId))
            .ToListAsync();

        var cityResources = await db.CityResources
            .Include(cr => cr.ResourceType)
            .Where(cr => cityIdSet == null || cityIdSet.Contains(cr.CityId))
            .ToListAsync();

        var allResources = await db.ResourceTypes
            .OrderBy(resource => resource.Name)
            .ToListAsync();

        var cityResourcesByCity = new Dictionary<Guid, IReadOnlyList<CityResourceProfile>>();
        foreach (var city in cities)
        {
            var byResourceId = cityResources
                .Where(cr => cr.CityId == city.Id)
                .ToDictionary(cr => cr.ResourceTypeId);

            var resolvedResources = new List<CityResourceProfile>(allResources.Count);
            foreach (var resource in allResources)
            {
                if (byResourceId.TryGetValue(resource.Id, out var cityResource))
                {
                    resolvedResources.Add(new CityResourceProfile(cityResource, true));
                    continue;
                }

                // Some cities have partial CityResources seed data. For mine-lot coverage,
                // synthesize a neutral fallback profile so every resource stays available.
                resolvedResources.Add(new CityResourceProfile(new CityResource
                {
                    CityId = city.Id,
                    ResourceTypeId = resource.Id,
                    ResourceType = resource,
                    Abundance = 0.5m,
                }, false));
            }

            cityResourcesByCity[city.Id] = resolvedResources;
        }

        // Load EUR-based FX rates so lot prices are expressed in the city's local currency.
        // EUR/EUR = 1 is always added as a baseline so EUR-currency cities are unaffected.
        var fxRateRows = await db.FxRates
            .Where(r => r.BaseCurrencyCode == "EUR")
            .ToListAsync();
        var fxRatesByCurrency = fxRateRows
            .GroupBy(r => r.QuoteCurrencyCode)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.FetchedAtUtc).First().Rate);
        fxRatesByCurrency["EUR"] = 1m;

        EnsureMinimumAvailableLots(
            db,
            cities,
            lots,
            buildings,
            currentTick,
            fxRatesByCurrency,
            cityResourcesByCity);
    }

    private static void EnsureMinimumAvailableLots(
        AppDbContext db,
        IReadOnlyCollection<City> cities,
        IReadOnlyCollection<BuildingLot> existingLots,
        IReadOnlyCollection<Building> buildings,
        long currentTick,
        Dictionary<string, decimal>? fxRatesByCurrency = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<CityResourceProfile>>? cityResourcesByCity = null)
    {
        foreach (var city in cities)
        {
            var cityFxRate = fxRatesByCurrency?.GetValueOrDefault(city.CurrencyCode, 1m) ?? 1m;
            var cityBuildings = buildings.Where(building => building.CityId == city.Id).ToList();
            var cityLots = existingLots.Where(lot => lot.CityId == city.Id).ToList();
            var cityResources = cityResourcesByCity?.GetValueOrDefault(city.Id) ?? [];

            // Every mine lot must expose a concrete deposit payload for the buy-building UX.
            if (cityResources.Count > 0)
            {
                EnsureMineDepositData(cityLots, cityResources, city, cityBuildings, currentTick, cityFxRate);
            }

            foreach (var buildingType in BuildingType.All)
            {
                if (buildingType == BuildingType.Mine && cityResources.Count > 0)
                {
                    EnsurePerResourceMineCoverage(
                        db,
                        city,
                        cityLots,
                        cityBuildings,
                        cityResources,
                        currentTick,
                        cityFxRate);
                }

                var availableCount = cityLots.Count(lot => lot.OwnerCompanyId == null && SupportsBuildingType(lot, buildingType));
                var missingCount = Math.Max(0, GameConstants.MinimumAvailableLotsPerBuildingType - availableCount);

                for (var offset = 0; offset < missingCount; offset++)
                {
                    var sequence = cityLots.Count + 1;
                    BuildingLot generatedLot;
                    if (buildingType == BuildingType.Mine && cityResources.Count > 0)
                    {
                        var resource = cityResources[(sequence - 1) % cityResources.Count];
                        generatedLot = CreateGeneratedMineLot(city, resource, sequence, cityBuildings, currentTick, cityFxRate);
                    }
                    else
                    {
                        generatedLot = CreateGeneratedLot(city, buildingType, sequence, cityBuildings, currentTick, cityFxRate);
                    }

                    db.BuildingLots.Add(generatedLot);
                    cityLots.Add(generatedLot);
                }
            }

            foreach (var lot in cityLots)
            {
                RefreshLandState(lot, city, cityBuildings, currentTick, cityFxRate);

                if (lot.BuildingId is not Guid buildingId)
                {
                    continue;
                }

                var building = cityBuildings.FirstOrDefault(candidate => candidate.Id == buildingId);
                if (building is null)
                {
                    continue;
                }

                // Land coordinates are authoritative. Keep attached buildings pinned to the parcel.
                building.Latitude = lot.Latitude;
                building.Longitude = lot.Longitude;
            }
        }
    }

    public static void RefreshLandState(
        BuildingLot lot,
        City city,
        IReadOnlyCollection<Building> cityBuildings,
        long currentTick,
        decimal cityFxRate = 1m)
    {
        // Detect lots whose BasePrice was set before FX-rate scaling was introduced.
        // Condition: unowned lot in a high-FX-rate city (rate > HighFxRateThreshold) with an
        // unusually low BasePrice that looks EUR-anchored (< EurAnchoredLotBasePriceThreshold).
        // For EUR cities (fxRate ≈ 1) this condition is never true, so manually-seeded
        // Bratislava/Vienna lots keep their precise seeded prices.
        // For CZK (fxRate ≈ 25), any EUR-anchored lot below the threshold gets self-healed.
        // For INR (fxRate ≈ 90) and other high-rate currencies the same logic applies.
        var looksEurAnchored = cityFxRate > GameConstants.HighFxRateThreshold
            && lot.BasePrice > 0m
            && lot.BasePrice < GameConstants.EurAnchoredLotBasePriceThreshold;

        if (lot.BasePrice <= 0m)
        {
            // BasePrice not set — prefer existing Price as anchor (handles manually-created test lots
            // that set only Price), otherwise compute from scratch with FX scaling.
            lot.BasePrice = lot.Price > 0m
                ? lot.Price
                : ComputeBasePrice(city, FirstSuitableType(lot), ComputeDistanceKmToCityCenter(lot, city), cityFxRate);
        }
        else if (lot.OwnerCompanyId == null && looksEurAnchored)
        {
            // Self-heal: unowned lot in a high-FX-rate city with a suspiciously low BasePrice
            // that was generated before FX-rate scaling was introduced. Reprice it.
            lot.BasePrice = ComputeBasePrice(city, FirstSuitableType(lot), ComputeDistanceKmToCityCenter(lot, city), cityFxRate);
        }

        lot.PopulationIndex = ComputePopulationIndex(lot, city, cityBuildings, currentTick);
        // Price = appraised land value + raw-material deposit premium (when applicable).
        // The land component fluctuates with population index; the resource premium is fixed
        // by the deposit characteristics and gives mine lots a price above the base land value.
        lot.Price = ComputeAppraisedPrice(lot.BasePrice, lot.PopulationIndex)
                  + ComputeResourcePremium(lot.ResourceType, lot.MaterialQuality, lot.MaterialQuantity);
    }

    /// <summary>
    /// Computes the resource-deposit premium added to the asking price of a lot.
    /// Formula: quantity × resourceBasePrice × quality × <see cref="ResourcePremiumCaptureRate"/>.
    /// Returns zero when any required value is missing or non-positive.
    /// </summary>
    public static decimal ComputeResourcePremium(
        ResourceType? resourceType,
        decimal? materialQuality,
        decimal? materialQuantity)
    {
        if (resourceType is null || materialQuality is null or <= 0m || materialQuantity is null or <= 0m)
        {
            return 0m;
        }

        var premium = materialQuantity.Value * resourceType.BasePrice * materialQuality.Value * ResourcePremiumCaptureRate;
        var clamped = Clamp(premium, MineLotMinPrice, MineLotMaxPrice);
        return decimal.Round(clamped, 2, MidpointRounding.AwayFromZero);
    }

}

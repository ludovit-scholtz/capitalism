using Api.Data;
using Api.Data.Entities;
using Api.Engine;

namespace Api.Utilities;

public static partial class LandService
{
    private static void EnsurePerResourceMineCoverage(
        AppDbContext db,
        City city,
        List<BuildingLot> cityLots,
        IReadOnlyCollection<Building> cityBuildings,
        IReadOnlyList<CityResourceProfile> cityResources,
        long currentTick,
        decimal cityFxRate)
    {
        var availableMineLots = cityLots
            .Where(lot => lot.OwnerCompanyId == null && SupportsBuildingType(lot, BuildingType.Mine))
            .ToList();

        foreach (var resource in cityResources)
        {
            var hasCoverage = availableMineLots.Any(lot =>
                lot.ResourceTypeId == resource.CityResource.ResourceTypeId
                && lot.MaterialQuality is > 0m
                && lot.MaterialQuantity is > 0m);

            if (hasCoverage)
            {
                continue;
            }

            var sequence = cityLots.Count + 1;
            var generatedLot = CreateGeneratedMineLot(city, resource, sequence, cityBuildings, currentTick, cityFxRate);
            db.BuildingLots.Add(generatedLot);
            cityLots.Add(generatedLot);
            availableMineLots.Add(generatedLot);
        }
    }

    private static void EnsureMineDepositData(
        List<BuildingLot> cityLots,
        IReadOnlyList<CityResourceProfile> cityResources,
        City city,
        IReadOnlyCollection<Building> cityBuildings,
        long currentTick,
        decimal cityFxRate)
    {
        var cityResourceById = cityResources.ToDictionary(profile => profile.CityResource.ResourceTypeId);

        var mineLotsMissingDeposit = cityLots
            .Where(lot => SupportsBuildingType(lot, BuildingType.Mine)
                && lot.OwnerCompanyId == null
                // Exclude depleted lots — they have OriginalMaterialQuantity set, meaning
                // they previously held a deposit that was intentionally extracted to zero.
                // Re-assigning them here would bypass the ResourceReplenishmentPhase cycle.
                && !lot.OriginalMaterialQuantity.HasValue
                && (lot.ResourceTypeId is null
                    || lot.ResourceType is null
                    || lot.MaterialQuality is null or <= 0m
                    || lot.MaterialQuantity is null or <= 0m))
            .ToList();

        foreach (var lot in mineLotsMissingDeposit)
        {
            var resourceIndex = Math.Abs(HashCode.Combine(lot.Id, city.Id)) % cityResources.Count;
            var resource = cityResources[resourceIndex];
            ApplyMineDepositProfile(lot, resource);
            RefreshLandState(lot, city, cityBuildings, currentTick, cityFxRate);
        }

        // Enforce quality bands for all mine lots with deposit data:
        // native city resource => 50%-100%, fallback resource => 0%-50%.
        var mineLotsWithDeposit = cityLots
            .Where(lot => SupportsBuildingType(lot, BuildingType.Mine)
                && lot.OwnerCompanyId == null
                && lot.ResourceTypeId is not null
                && lot.ResourceType is not null
                && lot.MaterialQuality is > 0m
                && lot.MaterialQuantity is > 0m)
            .ToList();

        foreach (var lot in mineLotsWithDeposit)
        {
            var resourceTypeId = lot.ResourceTypeId!.Value;
            if (!cityResourceById.TryGetValue(resourceTypeId, out var profile))
            {
                continue;
            }

            var quality = lot.MaterialQuality!.Value;
            if (IsQualityInExpectedBand(quality, profile.IsNativeInCity))
            {
                continue;
            }

            ApplyMineDepositProfile(lot, profile);
            RefreshLandState(lot, city, cityBuildings, currentTick, cityFxRate);
        }
    }

    private static BuildingLot CreateGeneratedMineLot(
        City city,
        CityResourceProfile cityResource,
        int sequence,
        IReadOnlyCollection<Building> cityBuildings,
        long currentTick,
        decimal cityFxRate)
    {
        var lot = CreateGeneratedLot(city, BuildingType.Mine, sequence, cityBuildings, currentTick, cityFxRate);
        lot.Name = $"{cityResource.CityResource.ResourceType.Name} Deposit {sequence:00}";
        lot.Description = $"Procedurally generated extraction parcel in {city.Name} with a mapped {cityResource.CityResource.ResourceType.Name} reserve.";
        ApplyMineDepositProfile(lot, cityResource);
        RefreshLandState(lot, city, cityBuildings, currentTick, cityFxRate);
        return lot;
    }

    private static void ApplyMineDepositProfile(BuildingLot lot, CityResourceProfile cityResourceProfile)
    {
        var cityResource = cityResourceProfile.CityResource;
        var abundance = Clamp(cityResource.Abundance, 0.2m, 1.0m);
        var resource = cityResource.ResourceType;

        // Randomized quality bands:
        // native city resources => 50%-100%, fallback resources => 0%-50%.
        var quality = cityResourceProfile.IsNativeInCity
            ? RandomInRange(0.5m, 1.0m)
            : RandomInRange(0.0m, 0.5m);
        var targetPremium = Clamp(MineLotMinPrice + ((MineLotMaxPrice - MineLotMinPrice) * abundance * 0.65m), MineLotMinPrice, MineLotMaxPrice);

        var denom = Math.Max(resource.BasePrice * quality * ResourcePremiumCaptureRate, 0.0001m);
        var quantity = decimal.Round(targetPremium / denom, 2, MidpointRounding.AwayFromZero);

        lot.ResourceTypeId = resource.Id;
        lot.ResourceType = resource;
        lot.MaterialQuality = quality;
        lot.MaterialQuantity = Math.Max(quantity, 1m);
    }

    private static bool IsQualityInExpectedBand(decimal quality, bool isNativeInCity)
    {
        if (isNativeInCity)
        {
            return quality >= 0.5m && quality <= 1.0m;
        }

        return quality >= 0m && quality <= 0.5m;
    }

    private static decimal RandomInRange(decimal minInclusive, decimal maxInclusive)
    {
        if (maxInclusive <= minInclusive)
        {
            return minInclusive;
        }

        var sample = (decimal)Random.Shared.NextDouble();
        var value = minInclusive + ((maxInclusive - minInclusive) * sample);
        return decimal.Round(Clamp(value, minInclusive, maxInclusive), 4, MidpointRounding.AwayFromZero);
    }

    public static decimal ComputePopulationIndex(
        BuildingLot lot,
        City city,
        IReadOnlyCollection<Building> cityBuildings,
        long currentTick)
    {
        var distanceKm = ComputeDistanceKmToCityCenter(lot, city);
        var distanceScore = Clamp(1.2m - (decimal)(distanceKm / 10d), 0.2m, 1.2m);
        var cityPopulationScore = Clamp(city.Population / 1_500_000m, 0.2m, 1.25m);

        var nearbyDemandDrivers = cityBuildings
            .Where(building => building.Type is BuildingType.Apartment or BuildingType.Commercial)
            .Select(building => new
            {
                Building = building,
                DistanceKm = GlobalExchangeCalculator.ComputeDistanceKm(
                    lot.Latitude,
                    lot.Longitude,
                    building.Latitude,
                    building.Longitude)
            })
            .Where(entry => entry.DistanceKm <= NeighborhoodRadiusKm)
            .Select(entry =>
            {
                var occupancy = entry.Building.OccupancyPercent.HasValue
                    ? Clamp(entry.Building.OccupancyPercent.Value / 100m, 0m, 1.2m)
                    : 0.55m;
                var proximityWeight = 1m / (1m + (decimal)entry.DistanceKm);
                return occupancy * proximityWeight;
            })
            .ToList();

        var neighborhoodScore = nearbyDemandDrivers.Count > 0
            ? Clamp(nearbyDemandDrivers.Average(), 0.15m, 1.25m)
            : 0.35m;

        var dailyTick = currentTick / 24;
        var jitterSeed = Math.Abs(HashCode.Combine(lot.Id, dailyTick));
        var jitter = (jitterSeed % 1000) / 1000m;

        var rawScore = 0.30m
            + (distanceScore * 0.40m)
            + (cityPopulationScore * 0.20m)
            + (neighborhoodScore * 0.20m)
            + (jitter * 0.10m);

        return Clamp(decimal.Round(rawScore, 4, MidpointRounding.AwayFromZero), MinPopulationIndex, MaxPopulationIndex);
    }

    public static decimal ComputeAppraisedPrice(decimal basePrice, decimal populationIndex)
    {
        if (basePrice <= 0m)
        {
            return 0m;
        }

        var multiplier = 0.6m + (Clamp(populationIndex, MinPopulationIndex, MaxPopulationIndex) * 0.4m);
        return decimal.Round(basePrice * multiplier, 2, MidpointRounding.AwayFromZero);
    }

    private static BuildingLot CreateGeneratedLot(
        City city,
        string buildingType,
        int sequence,
        IReadOnlyCollection<Building> cityBuildings,
        long currentTick,
        decimal cityFxRate = 1m)
    {
        var radiusKm = PreferredRadiusKm(buildingType, sequence);
        var angleDegrees = Math.Abs(HashCode.Combine(city.Id, buildingType, sequence)) % 360;
        var angleRadians = angleDegrees * (Math.PI / 180d);

        var latitude = city.Latitude + KmToLatitudeDelta(radiusKm * Math.Cos(angleRadians));
        var longitude = city.Longitude + KmToLongitudeDelta(radiusKm * Math.Sin(angleRadians), city.Latitude);
        var basePrice = ComputeBasePrice(city, buildingType, radiusKm, cityFxRate);

        var lot = new BuildingLot
        {
            Id = Guid.NewGuid(),
            CityId = city.Id,
            Name = $"{FormatBuildingType(buildingType)} Land {sequence:00}",
            Description = $"Procedurally generated {FormatBuildingType(buildingType).ToLowerInvariant()} parcel in {city.Name}.",
            District = DistrictForBuildingType(buildingType),
            Latitude = latitude,
            Longitude = longitude,
            SuitableTypes = buildingType,
            BasePrice = basePrice,
            Price = basePrice,
            PopulationIndex = 1m,
            ConcurrencyToken = Guid.NewGuid(),
        };

        RefreshLandState(lot, city, cityBuildings, currentTick, cityFxRate);
        return lot;
    }

    private static decimal ComputeBasePrice(City city, string? buildingType, double radiusKm, decimal fxRate = 1m)
    {
        var typeBasePrice = buildingType switch
        {
            BuildingType.Mine => 70_000m,
            BuildingType.Factory => 85_000m,
            BuildingType.SalesShop => 95_000m,
            BuildingType.ResearchDevelopment => 120_000m,
            BuildingType.Apartment => 140_000m,
            BuildingType.Commercial => 135_000m,
            BuildingType.MediaHouse => 165_000m,
            BuildingType.Bank => 180_000m,
            BuildingType.Exchange => 170_000m,
            BuildingType.PowerPlant => 150_000m,
            _ => 100_000m,
        };

        var cityMultiplier = 0.85m
            + Clamp(city.AverageRentPerSqm / 40m, 0.10m, 0.70m)
            + Clamp(city.Population / 2_500_000m, 0.05m, 0.60m);
        var radiusDiscount = Clamp(1.15m - ((decimal)radiusKm / 12m), 0.75m, 1.15m);

        return decimal.Round(typeBasePrice * cityMultiplier * radiusDiscount * fxRate, 2, MidpointRounding.AwayFromZero);
    }

    private static double PreferredRadiusKm(string buildingType, int sequence)
    {
        var baseRadius = buildingType switch
        {
            BuildingType.SalesShop => 0.8d,
            BuildingType.Apartment => 1.0d,
            BuildingType.Commercial => 1.2d,
            BuildingType.Bank => 1.3d,
            BuildingType.MediaHouse => 1.4d,
            BuildingType.ResearchDevelopment => 2.2d,
            BuildingType.Exchange => 2.6d,
            BuildingType.Factory => 3.0d,
            BuildingType.PowerPlant => 4.5d,
            BuildingType.Mine => 5.5d,
            _ => 2.5d,
        };

        return baseRadius + ((sequence - 1) % 5) * 0.8d;
    }

    private static string DistrictForBuildingType(string buildingType)
    {
        return buildingType switch
        {
            BuildingType.SalesShop => "Retail District",
            BuildingType.Apartment => "Residential Quarter",
            BuildingType.Commercial => "Business District",
            BuildingType.Bank => "Financial District",
            BuildingType.MediaHouse => "Media Quarter",
            BuildingType.ResearchDevelopment => "Innovation Park",
            BuildingType.Exchange => "Trade Zone",
            BuildingType.Factory => "Industrial Zone",
            BuildingType.PowerPlant => "Utility Belt",
            BuildingType.Mine => "Extraction Belt",
            _ => "Mixed District",
        };
    }

    private static string FormatBuildingType(string buildingType)
    {
        return buildingType.Replace('_', ' ')
            .ToLowerInvariant()
            .Replace("research development", "research & development")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => char.ToUpperInvariant(segment[0]) + segment[1..])
            .Aggregate((left, right) => $"{left} {right}");
    }

    private static bool SupportsBuildingType(BuildingLot lot, string buildingType)
    {
        return lot.SuitableTypes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(buildingType, StringComparer.OrdinalIgnoreCase);
    }

    private static string? FirstSuitableType(BuildingLot lot)
    {
        return lot.SuitableTypes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
    }

    private static double ComputeDistanceKmToCityCenter(BuildingLot lot, City city)
    {
        return GlobalExchangeCalculator.ComputeDistanceKm(lot.Latitude, lot.Longitude, city.Latitude, city.Longitude);
    }

    private static double KmToLatitudeDelta(double km)
    {
        return km / 110.574d;
    }

    private static double KmToLongitudeDelta(double km, double latitude)
    {
        var latitudeRadians = latitude * (Math.PI / 180d);
        var divisor = 111.320d * Math.Cos(latitudeRadians);
        return divisor == 0d ? 0d : km / divisor;
    }

    private static decimal Clamp(decimal value, decimal min, decimal max)
    {
        return Math.Min(Math.Max(value, min), max);
    }
}

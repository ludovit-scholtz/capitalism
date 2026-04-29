using System.Security.Cryptography;
using System.Text;
using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public sealed partial class AppDbInitializer
{
    private async Task SeedBuildingLotsAsync()
    {
        var bratislava = await dbContext.Cities.FirstAsync(c => c.Name == "Bratislava");
        var resources = await dbContext.ResourceTypes.ToDictionaryAsync(r => r.Slug);

        // Bratislava building lots across different districts.
        // Coordinates are spread around the city center (48.1486, 17.1077).
        //
        // BasePrice is the pure land anchor value (no resource premium).
        // LandService.RefreshLandState is called below to compute the dynamic PopulationIndex
        // and the final Price = ComputeAppraisedPrice(basePrice, populationIndex) + resourcePremium.
        // This means mine lots with raw-material deposits will always have Price > BasePrice.
        var lotsToSeed = new List<BuildingLot>
        {
            // Industrial Zone (eastern outskirts)
            // Low population index: these lots are near logistics hubs but away from residential areas.
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ba-industrial-1"),
                CityId = bratislava.Id,
                Name = "Industrial Plot A1",
                Description = "Large industrial plot near the eastern logistics corridor. Sits above an Iron Ore deposit (18,000t at 72% quality).",
                District = "Industrial Zone",
                Latitude = 48.1520, Longitude = 17.1250,
                PopulationIndex = 0.65m,
                BasePrice = 75_000m,
                Price = 75_000m,  // will be recomputed below
                SuitableTypes = "FACTORY,MINE",
                ResourceTypeId = resources.TryGetValue("iron-ore", out var ironOre) ? ironOre.Id : null,
                ResourceType = resources.TryGetValue("iron-ore", out var ironOreNav) ? ironOreNav : null,
                MaterialQuality = 0.72m,
                MaterialQuantity = 18_000m
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ba-industrial-2"),
                CityId = bratislava.Id,
                Name = "Industrial Plot A2",
                Description = "Adjacent to major rail freight terminal. Sits above a Chemical Minerals deposit (12,000t at 55% quality).",
                District = "Industrial Zone",
                Latitude = 48.1540, Longitude = 17.1280,
                PopulationIndex = 0.60m,
                BasePrice = 65_000m,
                Price = 65_000m,  // will be recomputed below
                SuitableTypes = "FACTORY,MINE",
                ResourceTypeId = resources.TryGetValue("chemical-minerals", out var chem) ? chem.Id : null,
                ResourceType = resources.TryGetValue("chemical-minerals", out var chemNav) ? chemNav : null,
                MaterialQuality = 0.55m,
                MaterialQuantity = 12_000m
            },
            // Premium gold deposit site (upper range of mine pricing, ~$130M)
            // Gold: 3,200 kg x 500 EUR/kg x 82% quality x captureRate(100) = 131,200,000 EUR
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ba-mine-gold-1"),
                CityId = bratislava.Id,
                Name = "Carpathian Gold Seam",
                Description = "Rare high-grade gold deposit in the Carpathian foothills north of Bratislava. Geological surveys confirm 3,200 kg of recoverable gold at 82% purity - one of the richest seams in Central Europe.",
                District = "Extraction Belt",
                Latitude = 48.1740, Longitude = 17.0950,
                PopulationIndex = 0.42m,
                BasePrice = 80_000m,
                Price = 80_000m,  // will be recomputed below - resource premium approx. $131M
                SuitableTypes = "MINE",
                ResourceTypeId = resources.TryGetValue("gold", out var gold) ? gold.Id : null,
                ResourceType = resources.TryGetValue("gold", out var goldNav) ? goldNav : null,
                MaterialQuality = 0.82m,
                MaterialQuantity = 3_200m
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ba-industrial-3"),
                CityId = bratislava.Id,
                Name = "Factory Site B1",
                Description = "Modern industrial park with good power grid access. Suitable for energy-intensive production.",
                District = "Industrial Zone",
                Latitude = 48.1500, Longitude = 17.1300,
                PopulationIndex = 0.72m,
                BasePrice = 90_000m,
                Price = 90_000m,
                SuitableTypes = "FACTORY,POWER_PLANT"
            },
            // Commercial District (city center)
            // High population index: these lots are in the heart of the city with dense foot traffic.
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ba-commercial-1"),
                CityId = bratislava.Id,
                Name = "High Street Retail Space",
                Description = "Prime storefront on the main pedestrian avenue. High foot traffic and visibility.",
                District = "Commercial District",
                Latitude = 48.1450, Longitude = 17.1070,
                PopulationIndex = 1.85m,
                BasePrice = 120_000m,
                Price = 120_000m,
                SuitableTypes = "SALES_SHOP,COMMERCIAL"
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ba-commercial-2"),
                CityId = bratislava.Id,
                Name = "Market Square Shop",
                Description = "Corner lot facing the historic market square. Excellent for retail with tourist exposure.",
                District = "Commercial District",
                Latitude = 48.1440, Longitude = 17.1090,
                PopulationIndex = 2.10m,
                BasePrice = 150_000m,
                Price = 150_000m,
                SuitableTypes = "SALES_SHOP,COMMERCIAL"
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ba-commercial-3"),
                CityId = bratislava.Id,
                Name = "Shopping Boulevard Unit",
                Description = "Mid-range retail space on a busy commercial boulevard with steady local traffic.",
                District = "Commercial District",
                Latitude = 48.1460, Longitude = 17.1050,
                PopulationIndex = 1.60m,
                BasePrice = 100_000m,
                Price = 100_000m,
                SuitableTypes = "SALES_SHOP"
            },
            // Business Park (northern area)
            // Moderate-to-high population index: professional district with daytime footfall.
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ba-business-1"),
                CityId = bratislava.Id,
                Name = "Innovation Campus Office",
                Description = "Modern office complex in the technology business park. Perfect for R&D operations.",
                District = "Business Park",
                Latitude = 48.1560, Longitude = 17.1100,
                PopulationIndex = 1.20m,
                BasePrice = 130_000m,
                Price = 130_000m,
                SuitableTypes = "RESEARCH_DEVELOPMENT,BANK"
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ba-business-2"),
                CityId = bratislava.Id,
                Name = "Financial Center Suite",
                Description = "Premium office space in the financial district. Ideal for banking and exchange operations.",
                District = "Business Park",
                Latitude = 48.1570, Longitude = 17.1060,
                PopulationIndex = 1.40m,
                BasePrice = 200_000m,
                Price = 200_000m,
                SuitableTypes = "BANK,EXCHANGE"
            },
            // Residential Quarter (western area)
            // Steady population index: consistent local demand from residents.
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ba-residential-1"),
                CityId = bratislava.Id,
                Name = "Riverside Apartment Block",
                Description = "Scenic residential plot overlooking the Danube. Strong rental demand from young professionals.",
                District = "Residential Quarter",
                Latitude = 48.1400, Longitude = 17.1000,
                PopulationIndex = 1.05m,
                BasePrice = 110_000m,
                Price = 110_000m,
                SuitableTypes = "APARTMENT"
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ba-residential-2"),
                CityId = bratislava.Id,
                Name = "Suburban Housing Site",
                Description = "Affordable residential lot in a growing suburban neighborhood. Good long-term rental potential.",
                District = "Residential Quarter",
                Latitude = 48.1380, Longitude = 17.0950,
                PopulationIndex = 0.88m,
                BasePrice = 70_000m,
                Price = 70_000m,
                SuitableTypes = "APARTMENT"
            },
            // Media & Cultural District (south-central)
            // Moderate population index: near cultural venues with evening and weekend activity.
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ba-media-1"),
                CityId = bratislava.Id,
                Name = "Broadcast Tower Complex",
                Description = "Purpose-built media complex near the cultural center. Ideal for newspaper, radio, or TV operations.",
                District = "Media District",
                Latitude = 48.1420, Longitude = 17.1120,
                PopulationIndex = 1.25m,
                BasePrice = 140_000m,
                Price = 140_000m,
                SuitableTypes = "MEDIA_HOUSE"
            },
            // Energy Zone (south-eastern outskirts)
            // Low population index: far from residential areas; access to grid infrastructure.
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ba-energy-1"),
                CityId = bratislava.Id,
                Name = "Power Generation Site",
                Description = "Large plot with grid connection capacity for power generation. Zoned for energy infrastructure.",
                District = "Energy Zone",
                Latitude = 48.1350, Longitude = 17.1200,
                PopulationIndex = 0.52m,
                BasePrice = 160_000m,
                Price = 160_000m,
                SuitableTypes = "POWER_PLANT"
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ba-energy-2"),
                CityId = bratislava.Id,
                Name = "Utility Substation Plot",
                Description = "Secondary energy plot suitable for smaller power plants or supplementary generation.",
                District = "Energy Zone",
                Latitude = 48.1360, Longitude = 17.1230,
                PopulationIndex = 0.55m,
                BasePrice = 100_000m,
                Price = 100_000m,
                SuitableTypes = "POWER_PLANT,FACTORY"
            }
        };

        dbContext.BuildingLots.AddRange(lotsToSeed);

        // Apply resource premium: Price = appraised land value + resource deposit premium.
        // This runs in the seeder so every fresh database starts with correct prices.
        // The tick engine recalculates prices on every tick using the same formula.
        foreach (var lot in lotsToSeed)
        {
            var resourcePremium = LandService.ComputeResourcePremium(
                lot.ResourceType, lot.MaterialQuality, lot.MaterialQuantity);
            if (resourcePremium > 0m)
            {
                var appraisedLandValue = LandService.ComputeAppraisedPrice(lot.BasePrice, lot.PopulationIndex);
                lot.Price = appraisedLandValue + resourcePremium;
            }
        }
    }

    /// <summary>
    /// Idempotent upgrade: adds the Carpathian Gold Seam lot to Bratislava if it was not
    /// present when the database was first seeded (e.g., pre-mining-premium databases).
    /// Safe to call on every startup - no-op when the lot already exists.
    /// </summary>
    private async Task EnsureCarpathianGoldSeamLotAsync()
    {
        var goldLotId = CreateDeterministicGuid("lot:ba-mine-gold-1");
        if (await dbContext.BuildingLots.AnyAsync(l => l.Id == goldLotId))
            return;

        var bratislava = await dbContext.Cities.FirstOrDefaultAsync(c => c.Name == "Bratislava");
        if (bratislava == null) return;

        var gold = await dbContext.ResourceTypes.FirstOrDefaultAsync(r => r.Slug == "gold");
        var lot = new BuildingLot
        {
            Id = goldLotId,
            CityId = bratislava.Id,
            Name = "Carpathian Gold Seam",
            Description = "Rare high-grade gold deposit in the Carpathian foothills north of Bratislava. Geological surveys confirm 3,200 kg of recoverable gold at 82% purity - one of the richest seams in Central Europe.",
            District = "Extraction Belt",
            Latitude = 48.1740, Longitude = 17.0950,
            PopulationIndex = 0.42m,
            BasePrice = 80_000m,
            Price = 80_000m,
            SuitableTypes = "MINE",
            ResourceTypeId = gold?.Id,
            ResourceType = gold,
            MaterialQuality = 0.82m,
            MaterialQuantity = 3_200m
        };

        var resourcePremium = LandService.ComputeResourcePremium(lot.ResourceType, lot.MaterialQuality, lot.MaterialQuantity);
        if (resourcePremium > 0m)
        {
            var appraisedLandValue = LandService.ComputeAppraisedPrice(lot.BasePrice, lot.PopulationIndex);
            lot.Price = appraisedLandValue + resourcePremium;
        }

        dbContext.BuildingLots.Add(lot);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Idempotent upgrade: ensures Bratislava has mining lots for all 8 resource types.
    /// Wood, Coal, Cotton, Grain, and Silicon lots are added if missing.
    /// </summary>
    private async Task EnsureBratislavaMiningLotsAsync()
    {
        var bratislava = await dbContext.Cities.FirstOrDefaultAsync(c => c.Name == "Bratislava");
        if (bratislava == null) return;

        var resources = await dbContext.ResourceTypes.ToDictionaryAsync(r => r.Slug);

        var mineLotSeeds = new[]
        {
            // Wood: 15,000t at 65% quality → ~97,500 EUR premium
            // 15000 × 10 EUR × 0.65 × 100 = 9,750,000 EUR
            (Key: "lot:ba-mine-wood-1", Resource: "wood", Name: "Carpathian Timber Reserve",
             Desc: "Extensive managed forest on the forested slopes north of Bratislava. Timber reserve with 15,000 tonnes of recoverable wood at 65% yield.",
             Lat: 48.2120, Lon: 17.0840, Quality: 0.65m, Quantity: 15_000m),
            // Coal: 20,000t at 60% quality → 9,600,000 EUR premium
            // 20000 × 8 EUR × 0.60 × 100 = 9,600,000 EUR
            (Key: "lot:ba-mine-coal-1", Resource: "coal", Name: "Záhorie Coal Field",
             Desc: "Shallow coal deposit in the Záhorie lowlands west of Bratislava. Estimated 20,000 tonnes of recoverable coal at 60% grade.",
             Lat: 48.1200, Lon: 16.9600, Quality: 0.60m, Quantity: 20_000m),
            // Cotton: 10,000t at 70% quality → 10,500,000 EUR premium
            // 10000 × 15 EUR × 0.70 × 100 = 10,500,000 EUR
            (Key: "lot:ba-mine-cotton-1", Resource: "cotton", Name: "Danube Lowland Cotton Fields",
             Desc: "Fertile alluvial plains south of the city along the Danube. Agricultural cotton estate with 10,000 tonnes of harvestable cotton at 70% quality.",
             Lat: 48.0850, Lon: 17.0600, Quality: 0.70m, Quantity: 10_000m),
            // Grain: 25,000t at 55% quality → 6,875,000 EUR premium
            // 25000 × 5 EUR × 0.55 × 100 = 6,875,000 EUR
            (Key: "lot:ba-mine-grain-1", Resource: "grain", Name: "Bratislava Grain Belt",
             Desc: "Rich agricultural flatlands south-east of the city. Major grain-growing estate with 25,000 tonnes of harvestable grain at 55% premium quality.",
             Lat: 48.0640, Lon: 17.2100, Quality: 0.55m, Quantity: 25_000m),
            // Silicon: 5,000kg at 75% quality → 15,000,000 EUR premium
            // 5000 × 40 EUR × 0.75 × 100 = 15,000,000 EUR
            (Key: "lot:ba-mine-silicon-1", Resource: "silicon", Name: "Small Carpathian Quartz Vein",
             Desc: "High-purity quartz vein in the Small Carpathians. Silicon-bearing deposit estimated at 5,000 kg of semiconductor-grade material at 75% purity.",
             Lat: 48.2350, Lon: 17.1480, Quality: 0.75m, Quantity: 5_000m),
        };

        foreach (var seed in mineLotSeeds)
        {
            var lotId = CreateDeterministicGuid(seed.Key);
            if (await dbContext.BuildingLots.AnyAsync(l => l.Id == lotId))
                continue;

            var resource = resources.TryGetValue(seed.Resource, out var r) ? r : null;
            var lot = new BuildingLot
            {
                Id = lotId,
                CityId = bratislava.Id,
                Name = seed.Name,
                Description = seed.Desc,
                District = "Extraction Belt",
                Latitude = seed.Lat, Longitude = seed.Lon,
                PopulationIndex = 0.40m,
                BasePrice = 70_000m,
                Price = 70_000m,
                SuitableTypes = "MINE",
                ResourceTypeId = resource?.Id,
                ResourceType = resource,
                MaterialQuality = seed.Quality,
                MaterialQuantity = seed.Quantity
            };

            var resourcePremium = LandService.ComputeResourcePremium(lot.ResourceType, lot.MaterialQuality, lot.MaterialQuantity);
            if (resourcePremium > 0m)
            {
                var appraisedLandValue = LandService.ComputeAppraisedPrice(lot.BasePrice, lot.PopulationIndex);
                lot.Price = appraisedLandValue + resourcePremium;
            }

            dbContext.BuildingLots.Add(lot);
        }

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Idempotent upgrade: ensures Prague has mining lots covering all 8 resource types.
    /// Prague center: 50.0755, 14.4378. Currency: CZK.
    /// </summary>
    private async Task EnsurePragueMiningLotsAsync()
    {
        var prague = await dbContext.Cities.FirstOrDefaultAsync(c => c.Name == "Prague");
        if (prague == null) return;

        var resources = await dbContext.ResourceTypes.ToDictionaryAsync(r => r.Slug);

        var mineLotSeeds = new[]
        {
            // Iron Ore: 16,000t at 68% quality → 27,200,000 EUR × FX(CZK)
            (Key: "lot:pr-mine-iron-1", Resource: "iron-ore", Name: "Bohemian Iron Ore Deposit",
             Desc: "Significant iron ore deposit in the Bohemian highlands east of Prague. 16,000 tonnes at 68% purity, well-suited for steelmaking.",
             Lat: 50.0420, Lon: 14.5870, Quality: 0.68m, Quantity: 16_000m),
            // Coal: 22,000t at 62% quality → 10,912,000 EUR × FX
            (Key: "lot:pr-mine-coal-1", Resource: "coal", Name: "Central Bohemia Coal Basin",
             Desc: "Productive coal seam in the central Bohemian basin. 22,000 tonnes of recoverable coal at 62% grade — historically mined region.",
             Lat: 50.0980, Lon: 14.3200, Quality: 0.62m, Quantity: 22_000m),
            // Gold: 2,800kg at 78% quality → 109,200,000 EUR × FX
            (Key: "lot:pr-mine-gold-1", Resource: "gold", Name: "Jílové u Prahy Gold Vein",
             Desc: "Famous medieval gold-mining district south of Prague. 2,800 kg of recoverable gold at 78% purity — historically one of Bohemia's richest gold seams.",
             Lat: 49.9170, Lon: 14.4920, Quality: 0.78m, Quantity: 2_800m),
            // Wood: 18,000t at 60% quality → 10,800,000 EUR × FX
            (Key: "lot:pr-mine-wood-1", Resource: "wood", Name: "Šumava Timber Tract",
             Desc: "Managed forest estate on the outskirts of Prague's green belt. 18,000 tonnes of harvestable timber at 60% yield.",
             Lat: 50.1380, Lon: 14.2800, Quality: 0.60m, Quantity: 18_000m),
            // Chemical Minerals: 14,000t at 58% quality → 24,360,000 EUR × FX
            (Key: "lot:pr-mine-chemical-1", Resource: "chemical-minerals", Name: "Bohemian Chemical Mineral Site",
             Desc: "Industrial chemical mineral deposit in the greater Prague region. 14,000 tonnes of extractable feedstock at 58% quality.",
             Lat: 50.0550, Lon: 14.6200, Quality: 0.58m, Quantity: 14_000m),
            // Cotton: 11,000t at 65% quality → 10,725,000 EUR × FX
            (Key: "lot:pr-mine-cotton-1", Resource: "cotton", Name: "Elbe Lowland Cotton Estate",
             Desc: "Fertile agricultural land along the Elbe river valley. 11,000 tonnes of harvestable cotton at 65% quality.",
             Lat: 50.1150, Lon: 14.5600, Quality: 0.65m, Quantity: 11_000m),
            // Grain: 28,000t at 52% quality → 7,280,000 EUR × FX
            (Key: "lot:pr-mine-grain-1", Resource: "grain", Name: "Central Bohemia Grain Fields",
             Desc: "Expansive grain farming estate on central Bohemian plains. 28,000 tonnes of agricultural grain at 52% premium quality.",
             Lat: 49.9900, Lon: 14.2500, Quality: 0.52m, Quantity: 28_000m),
            // Silicon: 6,000kg at 72% quality → 17,280,000 EUR × FX
            (Key: "lot:pr-mine-silicon-1", Resource: "silicon", Name: "Krkonoše Quartz Deposit",
             Desc: "High-purity quartz seam in the Krkonoše mountain foothills. 6,000 kg of semiconductor-grade silicon at 72% purity.",
             Lat: 50.1700, Lon: 14.3900, Quality: 0.72m, Quantity: 6_000m),
        };

        await EnsureMiningLotsForCityAsync(prague, resources, mineLotSeeds);
    }

    /// <summary>
    /// Idempotent upgrade: ensures Vienna has mining lots covering all 8 resource types.
    /// Vienna center: 48.2082, 16.3738. Currency: EUR.
    /// </summary>
    private async Task EnsureViennaMiningLotsAsync()
    {
        var vienna = await dbContext.Cities.FirstOrDefaultAsync(c => c.Name == "Vienna");
        if (vienna == null) return;

        var resources = await dbContext.ResourceTypes.ToDictionaryAsync(r => r.Slug);

        var mineLotSeeds = new[]
        {
            // Iron Ore: 15,000t at 65% quality → 24,375,000 EUR
            (Key: "lot:vi-mine-iron-1", Resource: "iron-ore", Name: "Vienna Basin Iron Ore Site",
             Desc: "Iron ore deposit in the Vienna basin north-east of the city. 15,000 tonnes at 65% purity, strategically located near rail infrastructure.",
             Lat: 48.2480, Lon: 16.5200, Quality: 0.65m, Quantity: 15_000m),
            // Coal: 18,000t at 58% quality → 8,352,000 EUR
            (Key: "lot:vi-mine-coal-1", Resource: "coal", Name: "Lower Austria Coal Seam",
             Desc: "Coal seam in the rolling hills west of Vienna. 18,000 tonnes of recoverable coal at 58% grade.",
             Lat: 48.1650, Lon: 16.1800, Quality: 0.58m, Quantity: 18_000m),
            // Gold: 3,500kg at 80% quality → 140,000,000 EUR
            (Key: "lot:vi-mine-gold-1", Resource: "gold", Name: "Alpine Gold Vein",
             Desc: "Gold-bearing vein in the Alpine foothills south of Vienna. 3,500 kg of recoverable gold at 80% purity — one of Austria's premium precious metal deposits.",
             Lat: 48.0850, Lon: 16.2100, Quality: 0.80m, Quantity: 3_500m),
            // Wood: 20,000t at 70% quality → 14,000,000 EUR
            (Key: "lot:vi-mine-wood-1", Resource: "wood", Name: "Vienna Woods Timber Reserve",
             Desc: "Managed forest in the famous Vienna Woods (Wienerwald) west of the city. 20,000 tonnes of sustainable timber at 70% yield.",
             Lat: 48.2200, Lon: 16.1600, Quality: 0.70m, Quantity: 20_000m),
            // Chemical Minerals: 13,000t at 62% quality → 24,180,000 EUR
            (Key: "lot:vi-mine-chemical-1", Resource: "chemical-minerals", Name: "Pannonian Basin Chemical Site",
             Desc: "Chemical mineral deposit in the Pannonian basin east of Vienna. 13,000 tonnes of industrial feedstock at 62% quality.",
             Lat: 48.1980, Lon: 16.6400, Quality: 0.62m, Quantity: 13_000m),
            // Cotton: 12,000t at 68% quality → 12,240,000 EUR
            (Key: "lot:vi-mine-cotton-1", Resource: "cotton", Name: "Danube Agricultural Estate",
             Desc: "Premium cotton farming estate on the fertile Danube plains east of Vienna. 12,000 tonnes of harvestable cotton at 68% quality.",
             Lat: 48.1500, Lon: 16.5800, Quality: 0.68m, Quantity: 12_000m),
            // Grain: 30,000t at 58% quality → 8,700,000 EUR
            (Key: "lot:vi-mine-grain-1", Resource: "grain", Name: "Marchfeld Grain Estate",
             Desc: "The legendary Marchfeld grain basin north-east of Vienna. 30,000 tonnes of premium grain at 58% quality — Austria's breadbasket.",
             Lat: 48.2750, Lon: 16.6900, Quality: 0.58m, Quantity: 30_000m),
            // Silicon: 7,000kg at 76% quality → 21,280,000 EUR
            (Key: "lot:vi-mine-silicon-1", Resource: "silicon", Name: "Alpine Quartz Deposit",
             Desc: "High-purity quartz seam in the Alpine foothills south-west of Vienna. 7,000 kg of semiconductor-grade silicon at 76% purity.",
             Lat: 48.0650, Lon: 16.2700, Quality: 0.76m, Quantity: 7_000m),
        };

        await EnsureMiningLotsForCityAsync(vienna, resources, mineLotSeeds);
    }

    /// <summary>
    /// Helper: adds missing mine lots for a city from the seed list. Each lot is keyed
    /// by a deterministic GUID so the operation is safe to call repeatedly on startup.
    /// </summary>
    private async Task EnsureMiningLotsForCityAsync(
        City city,
        Dictionary<string, ResourceType> resources,
        IEnumerable<(string Key, string Resource, string Name, string Desc, double Lat, double Lon, decimal Quality, decimal Quantity)> seeds)
    {
        foreach (var seed in seeds)
        {
            var lotId = CreateDeterministicGuid(seed.Key);
            if (await dbContext.BuildingLots.AnyAsync(l => l.Id == lotId))
                continue;

            var resource = resources.TryGetValue(seed.Resource, out var r) ? r : null;
            var lot = new BuildingLot
            {
                Id = lotId,
                CityId = city.Id,
                Name = seed.Name,
                Description = seed.Desc,
                District = "Extraction Belt",
                Latitude = seed.Lat, Longitude = seed.Lon,
                PopulationIndex = 0.40m,
                BasePrice = 70_000m,
                Price = 70_000m,
                SuitableTypes = "MINE",
                ResourceTypeId = resource?.Id,
                ResourceType = resource,
                MaterialQuality = seed.Quality,
                MaterialQuantity = seed.Quantity
            };

            var resourcePremium = LandService.ComputeResourcePremium(lot.ResourceType, lot.MaterialQuality, lot.MaterialQuantity);
            if (resourcePremium > 0m)
            {
                var appraisedLandValue = LandService.ComputeAppraisedPrice(lot.BasePrice, lot.PopulationIndex);
                lot.Price = appraisedLandValue + resourcePremium;
            }

            dbContext.BuildingLots.Add(lot);
        }

        await dbContext.SaveChangesAsync();
    }

    private static Guid CreateDeterministicGuid(string key)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(hash);
    }

    private async Task SafelyApplyMigrationsAsync()
    {
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync();
            await RepairLegacyTextColumnsAsync();
            return;
        }

        await dbContext.Database.EnsureCreatedAsync();
    }

    internal static bool ShouldRepairSchemaArtifact(string migrationId, IReadOnlySet<string>? pendingMigrations)
    {
        if (pendingMigrations is null || !pendingMigrations.Contains(migrationId))
        {
            return true;
        }

        return false;
    }

    private async Task RepairLegacyTextColumnsAsync()
    {
        if (!dbContext.Database.IsNpgsql())
        {
            return;
        }

        // Legacy SQLite-generated migrations created many UUID/decimal columns as TEXT.
        // Keep this idempotent repair in startup so fresh and legacy PostgreSQL databases boot safely.
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            DO $$
            DECLARE
                col RECORD;
                numeric_columns text[] := ARRAY[
                    'TaxRate',
                    'AverageRentPerSqm',
                    'BaseSalaryPerManhour',
                    'Abundance',
                    'BasePrice',
                    'Price',
                    'PopulationIndex',
                    'MaterialQuality',
                    'MaterialQuantity',
                    'WeightPerUnit',
                    'BasicLaborHours',
                    'EnergyConsumptionMwh',
                    'OutputQuantity',
                    'PriceElasticity',
                    'Quantity',
                    'Quality',
                    'Awareness',
                    'MarketingQuality',
                    'MarketingEfficiencyMultiplier',
                    'ContentValue',
                    'ContentBudgetPerTick',
                    'AccumulatedBudget',
                    'Budget',
                    'MinPrice',
                    'MaxPrice',
                    'MinQuality',
                    'AskingPrice',
                    'PricePerUnit',
                    'QuantitySold',
                    'RemainingQuantity',
                    'Amount',
                    'Demand',
                    'Revenue',
                    'SalesCapacity',
                    'TrendFactor',
                    'SharePrice',
                    'ShareCount',
                    'TotalValue',
                    'TotalSharesIssued',
                    'DividendPayoutRatio',
                    'SalaryMultiplier',
                    'PersonalTaxReserve',
                    'SourcingCostTotal',
                    'ConsumedQuantity',
                    'ProducedQuantity',
                    'InflowQuantity',
                    'OutflowQuantity',
                    'AccruedInterest',
                    'LendingInterestRatePercent',
                    'DepositInterestRatePercent',
                    'TotalDeposits',
                    'CentralBankDebt',
                    'PowerOutput',
                    'PowerConsumption',
                    'TotalAreaSqm',
                    'PricePerSqm',
                    'PendingPricePerSqm',
                    'OccupancyPercent',
                    'ConstructionCost',
                    'InterestRate',
                    'OriginalPrincipal',
                    'RemainingPrincipal',
                    'PaymentAmount',
                    'AccumulatedPenalty',
                    'CollateralAppraisedValue',
                    'AnnualInterestRatePercent',
                    'MaxPrincipalPerLoan',
                    'TotalCapacity',
                    'UsedCapacity',
                    'AmountPerShare',
                    'TotalAmount',
                    'ConfigureGuideBasePrice',
                    'ConfigureGuideTargetPrice'
                ];
            BEGIN
                BEGIN
                    EXECUTE 'ALTER TABLE "Brands" ADD COLUMN IF NOT EXISTS "MarketingQuality" numeric(18,4) NOT NULL DEFAULT 0';
                EXCEPTION WHEN OTHERS THEN
                    NULL;
                END;

                BEGIN
                    EXECUTE 'ALTER TABLE "Brands" ADD COLUMN IF NOT EXISTS "MarketingEfficiencyMultiplier" numeric(18,4) NOT NULL DEFAULT 1';
                EXCEPTION WHEN OTHERS THEN
                    NULL;
                END;

                BEGIN
                    EXECUTE 'ALTER TABLE "Cities" ADD COLUMN IF NOT EXISTS "FuelPriceIndex" numeric NOT NULL DEFAULT 1.0';
                    EXECUTE 'UPDATE "Cities" SET "FuelPriceIndex" = 0.95 WHERE "Name" = ''Prague''';
                    EXECUTE 'UPDATE "Cities" SET "FuelPriceIndex" = 1.05 WHERE "Name" = ''Vienna''';
                    EXECUTE 'UPDATE "Cities" SET "FuelPriceIndex" = 0.80 WHERE "Name" = ''New York''';
                    EXECUTE 'UPDATE "Cities" SET "FuelPriceIndex" = 1.25 WHERE "Name" = ''London''';
                    EXECUTE 'UPDATE "Cities" SET "FuelPriceIndex" = 0.70 WHERE "Name" = ''Beijing''';
                    EXECUTE 'UPDATE "Cities" SET "FuelPriceIndex" = 0.65 WHERE "Name" = ''Delhi''';
                EXCEPTION WHEN OTHERS THEN
                    NULL;
                END;

                BEGIN
                    EXECUTE 'ALTER TABLE "Buildings" ADD COLUMN IF NOT EXISTS "DispatchTargetPercent" integer NOT NULL DEFAULT 100';
                    EXECUTE 'ALTER TABLE "Buildings" ADD COLUMN IF NOT EXISTS "FuelReserveMwh" numeric NOT NULL DEFAULT 0';
                EXCEPTION WHEN OTHERS THEN
                    NULL;
                END;

                FOR col IN
                    SELECT c.table_name, c.column_name
                    FROM information_schema.columns c
                    WHERE c.table_schema = 'public'
                        AND c.data_type = 'text'
                        AND c.table_name <> '__EFMigrationsHistory'
                        AND (
                            c.column_name = 'Id'
                            OR (c.column_name LIKE '%Id' AND c.column_name <> 'MigrationId')
                        )
                LOOP
                    BEGIN
                        EXECUTE format(
                            'ALTER TABLE %I ALTER COLUMN %I DROP DEFAULT',
                            col.table_name,
                            col.column_name
                        );
                    EXCEPTION WHEN OTHERS THEN
                        NULL;
                    END;

                    BEGIN
                        EXECUTE format(
                            'ALTER TABLE %I ALTER COLUMN %I TYPE uuid USING NULLIF(%I, '''')::uuid',
                            col.table_name,
                            col.column_name,
                            col.column_name
                        );
                    EXCEPTION WHEN OTHERS THEN
                        NULL;
                    END;
                END LOOP;

                FOR col IN
                    SELECT c.table_name, c.column_name
                    FROM information_schema.columns c
                    WHERE c.table_schema = 'public'
                        AND c.data_type = 'text'
                        AND c.column_name = ANY(numeric_columns)
                LOOP
                    BEGIN
                        EXECUTE format(
                            'ALTER TABLE %I ALTER COLUMN %I DROP DEFAULT',
                            col.table_name,
                            col.column_name
                        );
                    EXCEPTION WHEN OTHERS THEN
                        NULL;
                    END;

                    BEGIN
                        EXECUTE format(
                            'ALTER TABLE %I ALTER COLUMN %I TYPE numeric(18,4) USING NULLIF(%I, '''')::numeric(18,4)',
                            col.table_name,
                            col.column_name,
                            col.column_name
                        );
                    EXCEPTION WHEN OTHERS THEN
                        NULL;
                    END;
                END LOOP;
            END $$;
            """
        );
    }
}

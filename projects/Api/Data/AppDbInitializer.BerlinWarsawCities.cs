using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public sealed partial class AppDbInitializer
{
    /// <summary>
    /// Seeds curated building lots for Berlin (EUR) and Warsaw (PLN).
    /// Lots are spread around each city centre with realistic district names and prices
    /// expressed in the local city currency, using GPS coordinates within validated bounds.
    /// </summary>
    private async Task SeedBerlinWarsawLotsAsync()
    {
        var resources = await dbContext.ResourceTypes.ToDictionaryAsync(r => r.Slug);
        var berlin  = await dbContext.Cities.FirstAsync(c => c.Name == "Berlin");
        var warsaw  = await dbContext.Cities.FirstAsync(c => c.Name == "Warsaw");

        var lots = new List<BuildingLot>();

        // ── Berlin (EUR) ──────────────────────────────────────────────────────────
        // City centre: 52.5200, 13.4050
        // Validated bounds: lat [52.3, 52.7], lon [13.2, 13.8]
        lots.AddRange(new[]
        {
            // Industrial — coal deposit (dominant Berlin resource at 0.8 abundance)
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:be-industrial-1"),
                CityId = berlin.Id,
                Name = "Spandau Industrial Park",
                Description = "Large industrial site in the western Spandau borough with canal access. Coal deposit (16,000t at 75% quality).",
                District = "Industrial Zone",
                Latitude = 52.5358, Longitude = 13.2450,
                PopulationIndex = 0.60m,
                BasePrice = 1_800_000m,
                Price = 1_800_000m,
                SuitableTypes = "FACTORY,MINE",
                ResourceTypeId = resources.TryGetValue("coal", out var beCoal) ? beCoal.Id : null,
                ResourceType = resources.TryGetValue("coal", out var beCoalNav) ? beCoalNav : null,
                MaterialQuality = 0.75m,
                MaterialQuantity = 16_000m
            },
            // Industrial — iron-ore deposit (0.7 abundance)
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:be-industrial-2"),
                CityId = berlin.Id,
                Name = "Tempelhof Manufacturing Quarter",
                Description = "Former airfield logistics zone repurposed for heavy manufacturing. Iron Ore deposit (20,000t at 70% quality).",
                District = "Industrial Zone",
                Latitude = 52.4737, Longitude = 13.4017,
                PopulationIndex = 0.65m,
                BasePrice = 2_200_000m,
                Price = 2_200_000m,
                SuitableTypes = "FACTORY,MINE",
                ResourceTypeId = resources.TryGetValue("iron-ore", out var beIron) ? beIron.Id : null,
                ResourceType = resources.TryGetValue("iron-ore", out var beIronNav) ? beIronNav : null,
                MaterialQuality = 0.70m,
                MaterialQuantity = 20_000m
            },
            // Factory-only starter lot (affordable, no resource premium)
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:be-factory-1"),
                CityId = berlin.Id,
                Name = "Lichtenberg Factory Site",
                Description = "Modern light industrial plot in the Lichtenberg district, with good rail links. Ideal for a starter factory.",
                District = "Industrial Zone",
                Latitude = 52.5124, Longitude = 13.4833,
                PopulationIndex = 0.70m,
                BasePrice = 950_000m,
                Price = 950_000m,
                SuitableTypes = "FACTORY,POWER_PLANT"
            },
            // Commercial — retail / sales shop
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:be-commercial-1"),
                CityId = berlin.Id,
                Name = "Kurfürstendamm Retail Flagship",
                Description = "Premium ground-floor retail space on the famous Ku'damm shopping boulevard. Maximum foot traffic.",
                District = "Commercial District",
                Latitude = 52.5027, Longitude = 13.3320,
                PopulationIndex = 1.35m,
                BasePrice = 5_000_000m,
                Price = 5_000_000m,
                SuitableTypes = "SALES_SHOP,EXCHANGE,MEDIA_HOUSE"
            },
            // Commercial — bank / office
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:be-commercial-2"),
                CityId = berlin.Id,
                Name = "Potsdamer Platz Business Tower",
                Description = "Prime financial and commercial district plot in the heart of Potsdamer Platz.",
                District = "Commercial District",
                Latitude = 52.5096, Longitude = 13.3759,
                PopulationIndex = 1.20m,
                BasePrice = 4_200_000m,
                Price = 4_200_000m,
                SuitableTypes = "BANK,COMMERCIAL,SALES_SHOP"
            },
            // Residential
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:be-residential-1"),
                CityId = berlin.Id,
                Name = "Prenzlauer Berg Apartment Block",
                Description = "Sought-after residential site in the Prenzlauer Berg district. Strong rental demand from young professionals.",
                District = "Residential Quarter",
                Latitude = 52.5389, Longitude = 13.4275,
                PopulationIndex = 1.22m,
                BasePrice = 3_800_000m,
                Price = 3_800_000m,
                SuitableTypes = "APARTMENT"
            },
            // Energy
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:be-energy-1"),
                CityId = berlin.Id,
                Name = "Brandenburg Energy Park",
                Description = "Large energy site on the outskirts of Berlin near the A10 ring road, suitable for coal or solar generation.",
                District = "Energy Zone",
                Latitude = 52.3812, Longitude = 13.6200,
                PopulationIndex = 0.40m,
                BasePrice = 1_500_000m,
                Price = 1_500_000m,
                SuitableTypes = "POWER_PLANT,FACTORY"
            },
        });

        // ── Warsaw (PLN) ───────────────────────────────────────────────────────────
        // City centre: 52.2297, 21.0122
        // Validated bounds: lat [52.0, 52.4], lon [20.7, 21.3]
        // FX note: prices expressed in PLN (≈ 4.25 PLN per EUR)
        lots.AddRange(new[]
        {
            // Industrial — grain deposit (dominant Warsaw resource at 0.8 abundance)
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:wa-industrial-1"),
                CityId = warsaw.Id,
                Name = "Praga North Grain Processing Plant",
                Description = "Large agricultural processing site on the eastern bank of the Vistula. Grain deposit (22,000t at 80% quality).",
                District = "Industrial Zone",
                Latitude = 52.2629, Longitude = 21.0450,
                PopulationIndex = 0.60m,
                BasePrice = 8_000_000m,
                Price = 8_000_000m,
                SuitableTypes = "FACTORY,MINE",
                ResourceTypeId = resources.TryGetValue("grain", out var waGrain) ? waGrain.Id : null,
                ResourceType = resources.TryGetValue("grain", out var waGrainNav) ? waGrainNav : null,
                MaterialQuality = 0.80m,
                MaterialQuantity = 22_000m
            },
            // Industrial — wood deposit (0.7 abundance)
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:wa-industrial-2"),
                CityId = warsaw.Id,
                Name = "Wola Timber & Industry Zone",
                Description = "Revitalised industrial quarter in Wola with rail freight terminal access. Wood deposit (18,000t at 70% quality).",
                District = "Industrial Zone",
                Latitude = 52.2346, Longitude = 20.9732,
                PopulationIndex = 0.65m,
                BasePrice = 9_500_000m,
                Price = 9_500_000m,
                SuitableTypes = "FACTORY,MINE",
                ResourceTypeId = resources.TryGetValue("wood", out var waWood) ? waWood.Id : null,
                ResourceType = resources.TryGetValue("wood", out var waWoodNav) ? waWoodNav : null,
                MaterialQuality = 0.70m,
                MaterialQuantity = 18_000m
            },
            // Factory-only starter lot (affordable, no resource premium)
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:wa-factory-1"),
                CityId = warsaw.Id,
                Name = "Ursus Factory Site",
                Description = "Former tractor-factory plot repurposed for modern light manufacturing. Good motorway access.",
                District = "Industrial Zone",
                Latitude = 52.1935, Longitude = 20.8902,
                PopulationIndex = 0.68m,
                BasePrice = 4_500_000m,
                Price = 4_500_000m,
                SuitableTypes = "FACTORY,POWER_PLANT"
            },
            // Commercial — retail / sales shop
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:wa-commercial-1"),
                CityId = warsaw.Id,
                Name = "Nowy Świat Retail Flagship",
                Description = "Premium storefront on Nowy Świat, Warsaw's most prestigious shopping street.",
                District = "Commercial District",
                Latitude = 52.2347, Longitude = 21.0169,
                PopulationIndex = 1.30m,
                BasePrice = 22_000_000m,
                Price = 22_000_000m,
                SuitableTypes = "SALES_SHOP,EXCHANGE,MEDIA_HOUSE"
            },
            // Commercial — bank / office
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:wa-commercial-2"),
                CityId = warsaw.Id,
                Name = "Warsaw Financial Centre Plot",
                Description = "Central business district site suitable for banking and financial operations.",
                District = "Commercial District",
                Latitude = 52.2286, Longitude = 20.9996,
                PopulationIndex = 1.18m,
                BasePrice = 18_000_000m,
                Price = 18_000_000m,
                SuitableTypes = "BANK,COMMERCIAL,SALES_SHOP"
            },
            // Residential
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:wa-residential-1"),
                CityId = warsaw.Id,
                Name = "Mokotów Apartment Estate",
                Description = "Desirable residential site in the affluent Mokotów district, close to parks and schools.",
                District = "Residential Quarter",
                Latitude = 52.1938, Longitude = 21.0147,
                PopulationIndex = 1.15m,
                BasePrice = 16_000_000m,
                Price = 16_000_000m,
                SuitableTypes = "APARTMENT"
            },
            // Energy
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:wa-energy-1"),
                CityId = warsaw.Id,
                Name = "Białołęka Power Site",
                Description = "Northern Warsaw energy zone with coal infrastructure. Ideal for new power plant installation.",
                District = "Energy Zone",
                Latitude = 52.3310, Longitude = 21.0240,
                PopulationIndex = 0.42m,
                BasePrice = 6_000_000m,
                Price = 6_000_000m,
                SuitableTypes = "POWER_PLANT,FACTORY"
            },
        });

        // Compute final prices (base land appraisal + resource premium) before persisting.
        foreach (var lot in lots)
        {
            var resourcePremium = LandService.ComputeResourcePremium(
                lot.ResourceType, lot.MaterialQuality, lot.MaterialQuantity);
            if (resourcePremium > 0m)
            {
                var appraisedLandValue = LandService.ComputeAppraisedPrice(lot.BasePrice, lot.PopulationIndex);
                lot.Price = appraisedLandValue + resourcePremium;
            }
        }

        dbContext.BuildingLots.AddRange(lots);
    }

    /// <summary>
    /// Idempotent: ensures curated building lots for Berlin and Warsaw exist.
    /// Called at startup so existing deployments get the lots without a full reseed.
    /// </summary>
    private async Task EnsureBerlinWarsawLotsAsync()
    {
        var berlin = await dbContext.Cities.FirstOrDefaultAsync(c => c.Name == "Berlin");
        var warsaw = await dbContext.Cities.FirstOrDefaultAsync(c => c.Name == "Warsaw");

        if (berlin is null || warsaw is null)
        {
            return;
        }

        var firstLotId = CreateDeterministicGuid("lot:be-industrial-1");
        if (await dbContext.BuildingLots.AnyAsync(l => l.Id == firstLotId))
        {
            return;
        }

        await SeedBerlinWarsawLotsAsync();
    }
}

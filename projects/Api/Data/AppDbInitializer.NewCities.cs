using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public sealed partial class AppDbInitializer
{
    /// <summary>
    /// Seeds building lots for New York, London, Beijing, and Delhi.
    /// Lots are spread around each city centre with realistic district names and prices
    /// expressed in the local city currency.
    /// </summary>
    private async Task SeedNewCityLotsAsync()
    {
        var resources = await dbContext.ResourceTypes.ToDictionaryAsync(r => r.Slug);
        var newYork  = await dbContext.Cities.FirstAsync(c => c.Name == "New York");
        var london   = await dbContext.Cities.FirstAsync(c => c.Name == "London");
        var beijing  = await dbContext.Cities.FirstAsync(c => c.Name == "Beijing");
        var delhi    = await dbContext.Cities.FirstAsync(c => c.Name == "Delhi");

        var lots = new List<BuildingLot>();

        // ── New York (USD) ─────────────────────────────────────────────────────
        // City centre: 40.7128, -74.0060
        lots.AddRange(new[]
        {
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ny-industrial-1"),
                CityId = newYork.Id,
                Name = "Brooklyn Industrial Park",
                Description = "Large industrial complex in Brooklyn with rail access. Sits above a Silicon deposit (8,000 kg at 65% quality).",
                District = "Industrial Zone",
                Latitude = 40.6782, Longitude = -73.9442,
                PopulationIndex = 0.70m,
                BasePrice = 2_500_000m,
                Price = 2_500_000m,
                SuitableTypes = "FACTORY,MINE",
                ResourceTypeId = resources.TryGetValue("silicon", out var nySilicon) ? nySilicon.Id : null,
                ResourceType = resources.TryGetValue("silicon", out var nySiliconNav) ? nySiliconNav : null,
                MaterialQuality = 0.65m,
                MaterialQuantity = 8_000m
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ny-industrial-2"),
                CityId = newYork.Id,
                Name = "Queens Manufacturing District",
                Description = "Mid-sized manufacturing plot near JFK logistics hub. Iron Ore deposit below (12,000t at 55% quality).",
                District = "Industrial Zone",
                Latitude = 40.7282, Longitude = -73.7949,
                PopulationIndex = 0.65m,
                BasePrice = 2_000_000m,
                Price = 2_000_000m,
                SuitableTypes = "FACTORY,MINE",
                ResourceTypeId = resources.TryGetValue("iron-ore", out var nyIron) ? nyIron.Id : null,
                ResourceType = resources.TryGetValue("iron-ore", out var nyIronNav) ? nyIronNav : null,
                MaterialQuality = 0.55m,
                MaterialQuantity = 12_000m
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ny-commercial-1"),
                CityId = newYork.Id,
                Name = "Manhattan Retail Flagship",
                Description = "Prime ground-floor retail space on Fifth Avenue. Maximum foot traffic and brand visibility.",
                District = "Commercial District",
                Latitude = 40.7549, Longitude = -73.9840,
                PopulationIndex = 1.40m,
                BasePrice = 8_000_000m,
                Price = 8_000_000m,
                SuitableTypes = "SALES_SHOP,EXCHANGE,MEDIA_HOUSE"
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ny-commercial-2"),
                CityId = newYork.Id,
                Name = "Midtown Business Plaza",
                Description = "High-rise office and commercial block in midtown Manhattan.",
                District = "Commercial District",
                Latitude = 40.7580, Longitude = -73.9855,
                PopulationIndex = 1.30m,
                BasePrice = 6_500_000m,
                Price = 6_500_000m,
                SuitableTypes = "SALES_SHOP,COMMERCIAL,BANK"
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ny-residential-1"),
                CityId = newYork.Id,
                Name = "Upper East Side Apartments",
                Description = "Luxury residential plot overlooking Central Park. High rental yields.",
                District = "Residential Quarter",
                Latitude = 40.7736, Longitude = -73.9566,
                PopulationIndex = 1.25m,
                BasePrice = 7_000_000m,
                Price = 7_000_000m,
                SuitableTypes = "APARTMENT"
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ny-energy-1"),
                CityId = newYork.Id,
                Name = "Staten Island Power Site",
                Description = "Waterfront energy plot suitable for power generation.",
                District = "Energy Zone",
                Latitude = 40.5795, Longitude = -74.1502,
                PopulationIndex = 0.50m,
                BasePrice = 3_000_000m,
                Price = 3_000_000m,
                SuitableTypes = "POWER_PLANT,FACTORY"
            },
        });

        // ── London (GBP) ────────────────────────────────────────────────────────
        // City centre: 51.5074, -0.1278
        lots.AddRange(new[]
        {
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ld-industrial-1"),
                CityId = london.Id,
                Name = "East London Industrial Estate",
                Description = "Large industrial plot east of the city with canal access. Coal deposit (14,000t at 60% quality).",
                District = "Industrial Zone",
                Latitude = 51.5155, Longitude = 0.0087,
                PopulationIndex = 0.60m,
                BasePrice = 1_800_000m,
                Price = 1_800_000m,
                SuitableTypes = "FACTORY,MINE",
                ResourceTypeId = resources.TryGetValue("coal", out var ldCoal) ? ldCoal.Id : null,
                ResourceType = resources.TryGetValue("coal", out var ldCoalNav) ? ldCoalNav : null,
                MaterialQuality = 0.60m,
                MaterialQuantity = 14_000m
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ld-industrial-2"),
                CityId = london.Id,
                Name = "Canary Wharf Logistics Hub",
                Description = "Former docklands site redeveloped for light manufacturing and storage.",
                District = "Industrial Zone",
                Latitude = 51.5035, Longitude = -0.0187,
                PopulationIndex = 0.70m,
                BasePrice = 2_200_000m,
                Price = 2_200_000m,
                SuitableTypes = "FACTORY"
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ld-commercial-1"),
                CityId = london.Id,
                Name = "Oxford Street Shopfront",
                Description = "Flagship retail space on London's busiest shopping street.",
                District = "Commercial District",
                Latitude = 51.5152, Longitude = -0.1415,
                PopulationIndex = 1.35m,
                BasePrice = 5_500_000m,
                Price = 5_500_000m,
                SuitableTypes = "SALES_SHOP,EXCHANGE,MEDIA_HOUSE"
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ld-commercial-2"),
                CityId = london.Id,
                Name = "City of London Office Block",
                Description = "Premium financial district plot suitable for banking operations.",
                District = "Commercial District",
                Latitude = 51.5127, Longitude = -0.0924,
                PopulationIndex = 1.20m,
                BasePrice = 4_800_000m,
                Price = 4_800_000m,
                SuitableTypes = "BANK,COMMERCIAL,SALES_SHOP"
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ld-residential-1"),
                CityId = london.Id,
                Name = "Kensington Residential Block",
                Description = "Prime residential site in upmarket Kensington.",
                District = "Residential Quarter",
                Latitude = 51.4994, Longitude = -0.1922,
                PopulationIndex = 1.20m,
                BasePrice = 4_500_000m,
                Price = 4_500_000m,
                SuitableTypes = "APARTMENT"
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:ld-energy-1"),
                CityId = london.Id,
                Name = "Thames Estuary Power Site",
                Description = "Riverside plot ideal for power generation, strong wind access.",
                District = "Energy Zone",
                Latitude = 51.5000, Longitude = 0.5500,
                PopulationIndex = 0.40m,
                BasePrice = 2_000_000m,
                Price = 2_000_000m,
                SuitableTypes = "POWER_PLANT"
            },
        });

        // ── Beijing (CNY) ────────────────────────────────────────────────────────
        // City centre: 39.9042, 116.4074
        lots.AddRange(new[]
        {
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:bj-industrial-1"),
                CityId = beijing.Id,
                Name = "Shunyi Heavy Industry Zone",
                Description = "Large industrial plot in the Shunyi manufacturing corridor. Iron Ore deposit (25,000t at 70% quality).",
                District = "Industrial Zone",
                Latitude = 40.1289, Longitude = 116.6549,
                PopulationIndex = 0.60m,
                BasePrice = 8_000_000m,
                Price = 8_000_000m,
                SuitableTypes = "FACTORY,MINE",
                ResourceTypeId = resources.TryGetValue("iron-ore", out var bjIron) ? bjIron.Id : null,
                ResourceType = resources.TryGetValue("iron-ore", out var bjIronNav) ? bjIronNav : null,
                MaterialQuality = 0.70m,
                MaterialQuantity = 25_000m
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:bj-industrial-2"),
                CityId = beijing.Id,
                Name = "Tongzhou Tech Manufacturing",
                Description = "Silicon-focused fabrication facility site. Silicon deposit (10,000 kg at 80% quality).",
                District = "Industrial Zone",
                Latitude = 39.9027, Longitude = 116.6580,
                PopulationIndex = 0.65m,
                BasePrice = 9_000_000m,
                Price = 9_000_000m,
                SuitableTypes = "FACTORY,MINE",
                ResourceTypeId = resources.TryGetValue("silicon", out var bjSilicon) ? bjSilicon.Id : null,
                ResourceType = resources.TryGetValue("silicon", out var bjSiliconNav) ? bjSiliconNav : null,
                MaterialQuality = 0.80m,
                MaterialQuantity = 10_000m
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:bj-commercial-1"),
                CityId = beijing.Id,
                Name = "Wangfujing Retail Street",
                Description = "Premium retail space on Wangfujing, Beijing's premier shopping boulevard.",
                District = "Commercial District",
                Latitude = 39.9150, Longitude = 116.4115,
                PopulationIndex = 1.30m,
                BasePrice = 18_000_000m,
                Price = 18_000_000m,
                SuitableTypes = "SALES_SHOP,EXCHANGE,MEDIA_HOUSE"
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:bj-commercial-2"),
                CityId = beijing.Id,
                Name = "CBD Financial Tower Site",
                Description = "Central Business District plot ideal for banking and financial operations.",
                District = "Commercial District",
                Latitude = 39.9093, Longitude = 116.4551,
                PopulationIndex = 1.20m,
                BasePrice = 15_000_000m,
                Price = 15_000_000m,
                SuitableTypes = "BANK,COMMERCIAL,SALES_SHOP"
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:bj-residential-1"),
                CityId = beijing.Id,
                Name = "Chaoyang Residential Complex",
                Description = "High-density residential development in the Chaoyang district.",
                District = "Residential Quarter",
                Latitude = 39.9215, Longitude = 116.4432,
                PopulationIndex = 1.15m,
                BasePrice = 12_000_000m,
                Price = 12_000_000m,
                SuitableTypes = "APARTMENT"
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:bj-energy-1"),
                CityId = beijing.Id,
                Name = "Yanqing Energy Park",
                Description = "Large energy site in Yanqing with coal access and wind potential.",
                District = "Energy Zone",
                Latitude = 40.4564, Longitude = 116.0121,
                PopulationIndex = 0.40m,
                BasePrice = 5_000_000m,
                Price = 5_000_000m,
                SuitableTypes = "POWER_PLANT,FACTORY"
            },
        });

        // ── Delhi (INR) ──────────────────────────────────────────────────────────
        // City centre: 28.6139, 77.2090
        lots.AddRange(new[]
        {
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:dl-industrial-1"),
                CityId = delhi.Id,
                Name = "Okhla Industrial Estate",
                Description = "Major industrial area south of Delhi. Chemical Minerals deposit (18,000t at 50% quality).",
                District = "Industrial Zone",
                Latitude = 28.5355, Longitude = 77.2741,
                PopulationIndex = 0.65m,
                BasePrice = 80_000_000m,
                Price = 80_000_000m,
                SuitableTypes = "FACTORY,MINE",
                ResourceTypeId = resources.TryGetValue("chemical-minerals", out var dlChem) ? dlChem.Id : null,
                ResourceType = resources.TryGetValue("chemical-minerals", out var dlChemNav) ? dlChemNav : null,
                MaterialQuality = 0.50m,
                MaterialQuantity = 18_000m
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:dl-industrial-2"),
                CityId = delhi.Id,
                Name = "Noida Manufacturing Hub",
                Description = "Large-scale textile and cotton processing site. Cotton deposit (20,000t at 70% quality).",
                District = "Industrial Zone",
                Latitude = 28.5355, Longitude = 77.3910,
                PopulationIndex = 0.60m,
                BasePrice = 60_000_000m,
                Price = 60_000_000m,
                SuitableTypes = "FACTORY,MINE",
                ResourceTypeId = resources.TryGetValue("cotton", out var dlCotton) ? dlCotton.Id : null,
                ResourceType = resources.TryGetValue("cotton", out var dlCottonNav) ? dlCottonNav : null,
                MaterialQuality = 0.70m,
                MaterialQuantity = 20_000m
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:dl-commercial-1"),
                CityId = delhi.Id,
                Name = "Connaught Place Retail",
                Description = "Heritage commercial hub at the heart of New Delhi.",
                District = "Commercial District",
                Latitude = 28.6315, Longitude = 77.2167,
                PopulationIndex = 1.25m,
                BasePrice = 200_000_000m,
                Price = 200_000_000m,
                SuitableTypes = "SALES_SHOP,EXCHANGE,MEDIA_HOUSE"
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:dl-commercial-2"),
                CityId = delhi.Id,
                Name = "Nehru Place IT Business Hub",
                Description = "Technology and finance district plot with high footfall.",
                District = "Commercial District",
                Latitude = 28.5491, Longitude = 77.2515,
                PopulationIndex = 1.10m,
                BasePrice = 150_000_000m,
                Price = 150_000_000m,
                SuitableTypes = "BANK,COMMERCIAL,SALES_SHOP"
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:dl-residential-1"),
                CityId = delhi.Id,
                Name = "South Delhi Residential Colony",
                Description = "High-density housing colony in South Delhi with strong rental demand.",
                District = "Residential Quarter",
                Latitude = 28.5245, Longitude = 77.1855,
                PopulationIndex = 1.20m,
                BasePrice = 120_000_000m,
                Price = 120_000_000m,
                SuitableTypes = "APARTMENT"
            },
            new BuildingLot
            {
                Id = CreateDeterministicGuid("lot:dl-energy-1"),
                CityId = delhi.Id,
                Name = "Badarpur Power Site",
                Description = "Former thermal power station site, suitable for new power plant installation.",
                District = "Energy Zone",
                Latitude = 28.5033, Longitude = 77.3044,
                PopulationIndex = 0.45m,
                BasePrice = 40_000_000m,
                Price = 40_000_000m,
                SuitableTypes = "POWER_PLANT,FACTORY"
            },
        });

        dbContext.BuildingLots.AddRange(lots);

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
    }
}

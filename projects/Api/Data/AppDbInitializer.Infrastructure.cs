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
                    'Budget',
                    'MinPrice',
                    'MaxPrice',
                    'MinQuality',
                    'AskingPrice',
                    'PricePerUnit',
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

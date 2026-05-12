using System.Security.Cryptography;
using System.Text;
using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Api.Data;

public sealed partial class AppDbInitializer
{

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
            // Iron Ore: 15,000t at 65% quality -> 24,375,000 EUR
            (Key: "lot:vi-mine-iron-1", Resource: "iron-ore", Name: "Vienna Basin Iron Ore Site",
             Desc: "Iron ore deposit in the Vienna basin north-east of the city. 15,000 tonnes at 65% purity, near rail infrastructure.",
             Lat: 48.2480, Lon: 16.5200, Quality: 0.65m, Quantity: 15_000m),
            // Coal: 32,000t at 62% quality -> 15,872,000 EUR (32000 x 8 x 0.62 x 100)
            (Key: "lot:vi-mine-coal-1", Resource: "coal", Name: "Lower Austria Coal Seam",
             Desc: "Coal seam in the rolling hills west of Vienna. 32,000 tonnes of recoverable coal at 62% grade.",
             Lat: 48.1650, Lon: 16.1800, Quality: 0.62m, Quantity: 32_000m),
            // Gold: 3,500kg at 80% quality -> 140,000,000 EUR
            (Key: "lot:vi-mine-gold-1", Resource: "gold", Name: "Alpine Gold Vein",
             Desc: "Gold-bearing vein in the Alpine foothills south of Vienna. 3,500 kg of recoverable gold at 80% purity -- one of Austria's premium precious metal deposits.",
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
            await dbContext.GetService<IHistoryRepository>().CreateIfNotExistsAsync();
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

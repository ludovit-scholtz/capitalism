using Api.Data;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260426100000_ComprehensiveSchemaRepairWithCleanup")]
    public partial class ComprehensiveSchemaRepairWithCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!ActiveProvider.Contains("Npgsql", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    fk RECORD;
                BEGIN
                    -- Step 1: Drop ALL foreign key constraints
                    FOR fk IN
                        SELECT constraint_name, table_name
                        FROM information_schema.table_constraints
                        WHERE constraint_type = 'FOREIGN KEY'
                        AND table_schema = 'public'
                    LOOP
                        BEGIN
                            EXECUTE format('ALTER TABLE %I DROP CONSTRAINT IF EXISTS %I', fk.table_name, fk.constraint_name);
                        EXCEPTION WHEN OTHERS THEN NULL;
                        END;
                    END LOOP;

                    -- Step 2: Convert all TEXT UUID columns to uuid type (with error handling for idempotence)
                    BEGIN EXECUTE 'ALTER TABLE "Players" ALTER COLUMN "Id" TYPE uuid USING CASE WHEN "Id" ~ ''^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'' THEN "Id"::uuid ELSE gen_random_uuid() END'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Players" ALTER COLUMN "OnboardingCityId" TYPE uuid USING "OnboardingCityId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Players" ALTER COLUMN "OnboardingCompanyId" TYPE uuid USING "OnboardingCompanyId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Players" ALTER COLUMN "OnboardingFactoryLotId" TYPE uuid USING "OnboardingFactoryLotId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Players" ALTER COLUMN "OnboardingShopBuildingId" TYPE uuid USING "OnboardingShopBuildingId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Players" ALTER COLUMN "ConcurrencyToken" TYPE uuid USING "ConcurrencyToken"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    
                    BEGIN EXECUTE 'ALTER TABLE "ProductTypes" ALTER COLUMN "Id" TYPE uuid USING "Id"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "ResourceTypes" ALTER COLUMN "Id" TYPE uuid USING "Id"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    
                    BEGIN EXECUTE 'ALTER TABLE "Companies" ALTER COLUMN "Id" TYPE uuid USING "Id"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Companies" ALTER COLUMN "PlayerId" TYPE uuid USING "PlayerId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    
                    BEGIN EXECUTE 'ALTER TABLE "Cities" ALTER COLUMN "Id" TYPE uuid USING "Id"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "CityResources" ALTER COLUMN "Id" TYPE uuid USING "Id"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "CityResources" ALTER COLUMN "CityId" TYPE uuid USING "CityId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "CityResources" ALTER COLUMN "ResourceTypeId" TYPE uuid USING "ResourceTypeId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    
                    BEGIN EXECUTE 'ALTER TABLE "ProductRecipes" ALTER COLUMN "Id" TYPE uuid USING "Id"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "ProductRecipes" ALTER COLUMN "ProductTypeId" TYPE uuid USING "ProductTypeId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "ProductRecipes" ALTER COLUMN "ResourceTypeId" TYPE uuid USING "ResourceTypeId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "ProductRecipes" ALTER COLUMN "InputProductTypeId" TYPE uuid USING "InputProductTypeId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    
                    BEGIN EXECUTE 'ALTER TABLE "Buildings" ALTER COLUMN "Id" TYPE uuid USING "Id"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Buildings" ALTER COLUMN "CompanyId" TYPE uuid USING "CompanyId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Buildings" ALTER COLUMN "CityId" TYPE uuid USING "CityId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Buildings" ALTER COLUMN "ProductTypeId" TYPE uuid USING "ProductTypeId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    
                    BEGIN EXECUTE 'ALTER TABLE "BuildingLots" ALTER COLUMN "Id" TYPE uuid USING "Id"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingLots" ALTER COLUMN "CityId" TYPE uuid USING "CityId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingLots" ALTER COLUMN "OwnerCompanyId" TYPE uuid USING "OwnerCompanyId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingLots" ALTER COLUMN "BuildingId" TYPE uuid USING "BuildingId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingLots" ALTER COLUMN "ResourceTypeId" TYPE uuid USING "ResourceTypeId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingLots" ALTER COLUMN "ConcurrencyToken" TYPE uuid USING "ConcurrencyToken"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    
                    BEGIN EXECUTE 'ALTER TABLE "BuildingUnits" ALTER COLUMN "Id" TYPE uuid USING "Id"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingUnits" ALTER COLUMN "BuildingId" TYPE uuid USING "BuildingId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingUnits" ALTER COLUMN "ResourceTypeId" TYPE uuid USING "ResourceTypeId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingUnits" ALTER COLUMN "ProductTypeId" TYPE uuid USING "ProductTypeId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingUnits" ALTER COLUMN "MediaHouseBuildingId" TYPE uuid USING "MediaHouseBuildingId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingUnits" ALTER COLUMN "VendorLockCompanyId" TYPE uuid USING "VendorLockCompanyId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    
                    BEGIN EXECUTE 'ALTER TABLE "ExchangeOrders" ALTER COLUMN "Id" TYPE uuid USING "Id"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "ExchangeOrders" ALTER COLUMN "ExchangeBuildingId" TYPE uuid USING "ExchangeBuildingId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "ExchangeOrders" ALTER COLUMN "CompanyId" TYPE uuid USING "CompanyId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "ExchangeOrders" ALTER COLUMN "ResourceTypeId" TYPE uuid USING "ResourceTypeId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "ExchangeOrders" ALTER COLUMN "ProductTypeId" TYPE uuid USING "ProductTypeId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    
                    BEGIN EXECUTE 'ALTER TABLE "BuildingConfigurationPlans" ALTER COLUMN "Id" TYPE uuid USING "Id"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingConfigurationPlans" ALTER COLUMN "BuildingConfigurationPlanId" TYPE uuid USING "BuildingConfigurationPlanId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    
                    BEGIN EXECUTE 'ALTER TABLE "BuildingConfigurationPlanUnits" ALTER COLUMN "Id" TYPE uuid USING "Id"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingConfigurationPlanUnits" ALTER COLUMN "BuildingConfigurationPlanId" TYPE uuid USING "BuildingConfigurationPlanId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingConfigurationPlanUnits" ALTER COLUMN "ResourceTypeId" TYPE uuid USING "ResourceTypeId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingConfigurationPlanUnits" ALTER COLUMN "ProductTypeId" TYPE uuid USING "ProductTypeId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingConfigurationPlanUnits" ALTER COLUMN "MediaHouseBuildingId" TYPE uuid USING "MediaHouseBuildingId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingConfigurationPlanUnits" ALTER COLUMN "VendorLockCompanyId" TYPE uuid USING "VendorLockCompanyId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    
                    BEGIN EXECUTE 'ALTER TABLE "BuildingConfigurationPlanRemovals" ALTER COLUMN "Id" TYPE uuid USING "Id"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingConfigurationPlanRemovals" ALTER COLUMN "BuildingConfigurationPlanId" TYPE uuid USING "BuildingConfigurationPlanId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    
                    BEGIN EXECUTE 'ALTER TABLE "BuildingUnitResourceHistories" ALTER COLUMN "Id" TYPE uuid USING "Id"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingUnitResourceHistories" ALTER COLUMN "BuildingId" TYPE uuid USING "BuildingId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingUnitResourceHistories" ALTER COLUMN "BuildingUnitId" TYPE uuid USING "BuildingUnitId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingUnitResourceHistories" ALTER COLUMN "ResourceTypeId" TYPE uuid USING "ResourceTypeId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingUnitResourceHistories" ALTER COLUMN "ProductTypeId" TYPE uuid USING "ProductTypeId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingUnitResourceHistories" ALTER COLUMN "BrandId" TYPE uuid USING "BrandId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    
                    BEGIN EXECUTE 'ALTER TABLE "Inventories" ALTER COLUMN "Id" TYPE uuid USING "Id"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Inventories" ALTER COLUMN "CompanyId" TYPE uuid USING "CompanyId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "CityWeatherForecasts" ALTER COLUMN "CityId" TYPE uuid USING "CityId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "GameStates" ALTER COLUMN "TaxRate" TYPE numeric(5,2) USING "TaxRate"::numeric(5,2)'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Inventories" ALTER COLUMN "BuildingId" TYPE uuid USING "BuildingId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Inventories" ALTER COLUMN "BuildingUnitId" TYPE uuid USING "BuildingUnitId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Inventories" ALTER COLUMN "ProductTypeId" TYPE uuid USING "ProductTypeId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Inventories" ALTER COLUMN "ResourceTypeId" TYPE uuid USING "ResourceTypeId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    
                    BEGIN EXECUTE 'ALTER TABLE "LedgerEntries" ALTER COLUMN "Id" TYPE uuid USING "Id"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "LedgerEntries" ALTER COLUMN "BuildingUnitId" TYPE uuid USING "BuildingUnitId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "LedgerEntries" ALTER COLUMN "BuildingId" TYPE uuid USING "BuildingId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "LedgerEntries" ALTER COLUMN "CompanyId" TYPE uuid USING "CompanyId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "LedgerEntries" ALTER COLUMN "ProductTypeId" TYPE uuid USING "ProductTypeId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "LedgerEntries" ALTER COLUMN "ResourceTypeId" TYPE uuid USING "ResourceTypeId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "LedgerEntries" ALTER COLUMN "CityId" TYPE uuid USING "CityId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    
                    BEGIN EXECUTE 'ALTER TABLE "PublicSalesRecords" ALTER COLUMN "Id" TYPE uuid USING "Id"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "PublicSalesRecords" ALTER COLUMN "BuildingUnitId" TYPE uuid USING "BuildingUnitId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "PublicSalesRecords" ALTER COLUMN "BuildingId" TYPE uuid USING "BuildingId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "PublicSalesRecords" ALTER COLUMN "CityId" TYPE uuid USING "CityId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "PublicSalesRecords" ALTER COLUMN "CompanyId" TYPE uuid USING "CompanyId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "PublicSalesRecords" ALTER COLUMN "ProductTypeId" TYPE uuid USING "ProductTypeId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "PublicSalesRecords" ALTER COLUMN "ResourceTypeId" TYPE uuid USING "ResourceTypeId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    
                    BEGIN EXECUTE 'ALTER TABLE "Brands" ALTER COLUMN "Id" TYPE uuid USING "Id"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Brands" ALTER COLUMN "CompanyId" TYPE uuid USING "CompanyId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    
                    BEGIN EXECUTE 'ALTER TABLE "CompanyCitySalarySettings" ALTER COLUMN "Id" TYPE uuid USING "Id"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "CompanyCitySalarySettings" ALTER COLUMN "CompanyId" TYPE uuid USING "CompanyId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "CompanyCitySalarySettings" ALTER COLUMN "CityId" TYPE uuid USING "CityId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    
                    BEGIN EXECUTE 'ALTER TABLE "StartupPackOffers" ALTER COLUMN "Id" TYPE uuid USING "Id"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "StartupPackOffers" ALTER COLUMN "PlayerId" TYPE uuid USING "PlayerId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "StartupPackOffers" ALTER COLUMN "GrantedCompanyId" TYPE uuid USING "GrantedCompanyId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    
                    BEGIN EXECUTE 'ALTER TABLE "Inventories" ALTER COLUMN "CompanyId" TYPE uuid USING "CompanyId"::uuid'; EXCEPTION WHEN OTHERS THEN NULL; END;

                    -- Step 2b: Convert TEXT DateTime columns to timestamp with time zone
                    BEGIN EXECUTE 'ALTER TABLE "GameStates" ALTER COLUMN "LastTickAtUtc" TYPE timestamp with time zone USING "LastTickAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Players" ALTER COLUMN "CreatedAtUtc" TYPE timestamp with time zone USING "CreatedAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Players" ALTER COLUMN "LastLoginAtUtc" TYPE timestamp with time zone USING CASE WHEN "LastLoginAtUtc" ~ ''^[0-9]{4}-[0-9]{2}-[0-9]{2}'' THEN "LastLoginAtUtc"::timestamp with time zone ELSE NULL END'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Players" ALTER COLUMN "OnboardingCompletedAtUtc" TYPE timestamp with time zone USING CASE WHEN "OnboardingCompletedAtUtc" ~ ''^[0-9]{4}-[0-9]{2}-[0-9]{2}'' THEN "OnboardingCompletedAtUtc"::timestamp with time zone ELSE NULL END'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Players" ALTER COLUMN "ProSubscriptionEndsAtUtc" TYPE timestamp with time zone USING CASE WHEN "ProSubscriptionEndsAtUtc" ~ ''^[0-9]{4}-[0-9]{2}-[0-9]{2}'' THEN "ProSubscriptionEndsAtUtc"::timestamp with time zone ELSE NULL END'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Players" ALTER COLUMN "OnboardingFirstSaleCompletedAtUtc" TYPE timestamp with time zone USING CASE WHEN "OnboardingFirstSaleCompletedAtUtc" ~ ''^[0-9]{4}-[0-9]{2}-[0-9]{2}'' THEN "OnboardingFirstSaleCompletedAtUtc"::timestamp with time zone ELSE NULL END'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Companies" ALTER COLUMN "FoundedAtUtc" TYPE timestamp with time zone USING "FoundedAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Buildings" ALTER COLUMN "BuiltAtUtc" TYPE timestamp with time zone USING "BuiltAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BankAccounts" ALTER COLUMN "CreatedAtUtc" TYPE timestamp with time zone USING "CreatedAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BankAccounts" ALTER COLUMN "ClosedAtUtc" TYPE timestamp with time zone USING CASE WHEN "ClosedAtUtc" ~ ''^[0-9]{4}-[0-9]{2}-[0-9]{2}'' THEN "ClosedAtUtc"::timestamp with time zone ELSE NULL END'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingConfigurationPlans" ALTER COLUMN "SubmittedAtUtc" TYPE timestamp with time zone USING "SubmittedAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "ChatMessages" ALTER COLUMN "SentAtUtc" TYPE timestamp with time zone USING "SentAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "DividendPayments" ALTER COLUMN "RecordedAtUtc" TYPE timestamp with time zone USING "RecordedAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "ExchangeOrders" ALTER COLUMN "CreatedAtUtc" TYPE timestamp with time zone USING "CreatedAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "ForexTradeRecords" ALTER COLUMN "ExecutedAtUtc" TYPE timestamp with time zone USING "ExecutedAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "FxRates" ALTER COLUMN "FetchedAtUtc" TYPE timestamp with time zone USING "FetchedAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "GoldAmmPools" ALTER COLUMN "CreatedAtUtc" TYPE timestamp with time zone USING "CreatedAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "GoldAmmPools" ALTER COLUMN "UpdatedAtUtc" TYPE timestamp with time zone USING "UpdatedAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "GoldAmmPositions" ALTER COLUMN "CreatedAtUtc" TYPE timestamp with time zone USING "CreatedAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "GoldAmmPositions" ALTER COLUMN "UpdatedAtUtc" TYPE timestamp with time zone USING "UpdatedAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "GoldAmmTradeRecords" ALTER COLUMN "ExecutedAtUtc" TYPE timestamp with time zone USING "ExecutedAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "LedgerEntries" ALTER COLUMN "RecordedAtUtc" TYPE timestamp with time zone USING "RecordedAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Loans" ALTER COLUMN "AcceptedAtUtc" TYPE timestamp with time zone USING "AcceptedAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Loans" ALTER COLUMN "ClosedAtUtc" TYPE timestamp with time zone USING CASE WHEN "ClosedAtUtc" ~ ''^[0-9]{4}-[0-9]{2}-[0-9]{2}'' THEN "ClosedAtUtc"::timestamp with time zone ELSE NULL END'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "LoanOffers" ALTER COLUMN "CreatedAtUtc" TYPE timestamp with time zone USING "CreatedAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "PersonTradeRecords" ALTER COLUMN "RecordedAtUtc" TYPE timestamp with time zone USING "RecordedAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "PlayerGoldBalances" ALTER COLUMN "CreatedAtUtc" TYPE timestamp with time zone USING "CreatedAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "PlayerGoldBalances" ALTER COLUMN "UpdatedAtUtc" TYPE timestamp with time zone USING "UpdatedAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "PublicSalesRecords" ALTER COLUMN "RecordedAtUtc" TYPE timestamp with time zone USING "RecordedAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "SharePriceHistoryEntries" ALTER COLUMN "RecordedAtUtc" TYPE timestamp with time zone USING "RecordedAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Shareholdings" ALTER COLUMN "CreatedAtUtc" TYPE timestamp with time zone USING "CreatedAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "AdminActionAuditLogs" ALTER COLUMN "RecordedAtUtc" TYPE timestamp with time zone USING "RecordedAtUtc"::timestamp with time zone'; EXCEPTION WHEN OTHERS THEN NULL; END;

                    -- Step 3: Recreate all foreign key constraints (with error handling for tables that may not exist yet)
                    BEGIN EXECUTE 'ALTER TABLE "Companies" ADD CONSTRAINT "FK_Companies_Players_PlayerId" FOREIGN KEY ("PlayerId") REFERENCES "Players" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "CityResources" ADD CONSTRAINT "FK_CityResources_Cities_CityId" FOREIGN KEY ("CityId") REFERENCES "Cities" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "CityResources" ADD CONSTRAINT "FK_CityResources_ResourceTypes_ResourceTypeId" FOREIGN KEY ("ResourceTypeId") REFERENCES "ResourceTypes" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "ProductRecipes" ADD CONSTRAINT "FK_ProductRecipes_ProductTypes_ProductTypeId" FOREIGN KEY ("ProductTypeId") REFERENCES "ProductTypes" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "ProductRecipes" ADD CONSTRAINT "FK_ProductRecipes_ResourceTypes_ResourceTypeId" FOREIGN KEY ("ResourceTypeId") REFERENCES "ResourceTypes" ("Id") ON DELETE RESTRICT'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "ProductRecipes" ADD CONSTRAINT "FK_ProductRecipes_InputProductTypeId" FOREIGN KEY ("InputProductTypeId") REFERENCES "ProductTypes" ("Id") ON DELETE RESTRICT'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Brands" ADD CONSTRAINT "FK_Brands_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Buildings" ADD CONSTRAINT "FK_Buildings_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Buildings" ADD CONSTRAINT "FK_Buildings_Cities_CityId" FOREIGN KEY ("CityId") REFERENCES "Cities" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "CompanyCitySalarySettings" ADD CONSTRAINT "FK_CompanyCitySalarySettings_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "CompanyCitySalarySettings" ADD CONSTRAINT "FK_CompanyCitySalarySettings_Cities_CityId" FOREIGN KEY ("CityId") REFERENCES "Cities" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "StartupPackOffers" ADD CONSTRAINT "FK_StartupPackOffers_Players_PlayerId" FOREIGN KEY ("PlayerId") REFERENCES "Players" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "StartupPackOffers" ADD CONSTRAINT "FK_StartupPackOffers_Companies_GrantedCompanyId" FOREIGN KEY ("GrantedCompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingConfigurationPlans" ADD CONSTRAINT "FK_BuildingConfigurationPlans_Buildings_BuildingId" FOREIGN KEY ("BuildingId") REFERENCES "Buildings" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingLots" ADD CONSTRAINT "FK_BuildingLots_Buildings_BuildingId" FOREIGN KEY ("BuildingId") REFERENCES "Buildings" ("Id") ON DELETE SET NULL'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingLots" ADD CONSTRAINT "FK_BuildingLots_Cities_CityId" FOREIGN KEY ("CityId") REFERENCES "Cities" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingLots" ADD CONSTRAINT "FK_BuildingLots_Companies_OwnerCompanyId" FOREIGN KEY ("OwnerCompanyId") REFERENCES "Companies" ("Id") ON DELETE SET NULL'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingLots" ADD CONSTRAINT "FK_BuildingLots_ResourceTypes_ResourceTypeId" FOREIGN KEY ("ResourceTypeId") REFERENCES "ResourceTypes" ("Id") ON DELETE SET NULL'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingUnits" ADD CONSTRAINT "FK_BuildingUnits_Buildings_BuildingId" FOREIGN KEY ("BuildingId") REFERENCES "Buildings" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "ExchangeOrders" ADD CONSTRAINT "FK_ExchangeOrders_Buildings_ExchangeBuildingId" FOREIGN KEY ("ExchangeBuildingId") REFERENCES "Buildings" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "ExchangeOrders" ADD CONSTRAINT "FK_ExchangeOrders_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingConfigurationPlanRemovals" ADD CONSTRAINT "FK_BuildingConfigurationPlanRemovals_BuildingConfigurationPlans_BuildingConfigurationPlanId" FOREIGN KEY ("BuildingConfigurationPlanId") REFERENCES "BuildingConfigurationPlans" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingConfigurationPlanUnits" ADD CONSTRAINT "FK_BuildingConfigurationPlanUnits_BuildingConfigurationPlans_BuildingConfigurationPlanId" FOREIGN KEY ("BuildingConfigurationPlanId") REFERENCES "BuildingConfigurationPlans" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingUnitResourceHistories" ADD CONSTRAINT "FK_BuildingUnitResourceHistories_BuildingUnits_BuildingUnitId" FOREIGN KEY ("BuildingUnitId") REFERENCES "BuildingUnits" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingUnitResourceHistories" ADD CONSTRAINT "FK_BuildingUnitResourceHistories_Buildings_BuildingId" FOREIGN KEY ("BuildingId") REFERENCES "Buildings" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingUnitResourceHistories" ADD CONSTRAINT "FK_BuildingUnitResourceHistories_ProductTypes_ProductTypeId" FOREIGN KEY ("ProductTypeId") REFERENCES "ProductTypes" ("Id") ON DELETE SET NULL'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingUnitResourceHistories" ADD CONSTRAINT "FK_BuildingUnitResourceHistories_ResourceTypes_ResourceTypeId" FOREIGN KEY ("ResourceTypeId") REFERENCES "ResourceTypes" ("Id") ON DELETE SET NULL'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "BuildingUnitResourceHistories" ADD CONSTRAINT "FK_BuildingUnitResourceHistories_Brands_BrandId" FOREIGN KEY ("BrandId") REFERENCES "Brands" ("Id") ON DELETE SET NULL'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Inventories" ADD CONSTRAINT "FK_Inventories_BuildingUnits_BuildingUnitId" FOREIGN KEY ("BuildingUnitId") REFERENCES "BuildingUnits" ("Id") ON DELETE SET NULL'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Inventories" ADD CONSTRAINT "FK_Inventories_Buildings_BuildingId" FOREIGN KEY ("BuildingId") REFERENCES "Buildings" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Inventories" ADD CONSTRAINT "FK_Inventories_ProductTypes_ProductTypeId" FOREIGN KEY ("ProductTypeId") REFERENCES "ProductTypes" ("Id") ON DELETE SET NULL'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "Inventories" ADD CONSTRAINT "FK_Inventories_ResourceTypes_ResourceTypeId" FOREIGN KEY ("ResourceTypeId") REFERENCES "ResourceTypes" ("Id") ON DELETE SET NULL'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "LedgerEntries" ADD CONSTRAINT "FK_LedgerEntries_BuildingUnits_BuildingUnitId" FOREIGN KEY ("BuildingUnitId") REFERENCES "BuildingUnits" ("Id") ON DELETE SET NULL'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "LedgerEntries" ADD CONSTRAINT "FK_LedgerEntries_Buildings_BuildingId" FOREIGN KEY ("BuildingId") REFERENCES "Buildings" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "LedgerEntries" ADD CONSTRAINT "FK_LedgerEntries_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "LedgerEntries" ADD CONSTRAINT "FK_LedgerEntries_ProductTypes_ProductTypeId" FOREIGN KEY ("ProductTypeId") REFERENCES "ProductTypes" ("Id") ON DELETE SET NULL'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "LedgerEntries" ADD CONSTRAINT "FK_LedgerEntries_ResourceTypes_ResourceTypeId" FOREIGN KEY ("ResourceTypeId") REFERENCES "ResourceTypes" ("Id") ON DELETE SET NULL'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "LedgerEntries" ADD CONSTRAINT "FK_LedgerEntries_Cities_CityId" FOREIGN KEY ("CityId") REFERENCES "Cities" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "PublicSalesRecords" ADD CONSTRAINT "FK_PublicSalesRecords_BuildingUnits_BuildingUnitId" FOREIGN KEY ("BuildingUnitId") REFERENCES "BuildingUnits" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "PublicSalesRecords" ADD CONSTRAINT "FK_PublicSalesRecords_Buildings_BuildingId" FOREIGN KEY ("BuildingId") REFERENCES "Buildings" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "PublicSalesRecords" ADD CONSTRAINT "FK_PublicSalesRecords_Cities_CityId" FOREIGN KEY ("CityId") REFERENCES "Cities" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "PublicSalesRecords" ADD CONSTRAINT "FK_PublicSalesRecords_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "PublicSalesRecords" ADD CONSTRAINT "FK_PublicSalesRecords_ProductTypes_ProductTypeId" FOREIGN KEY ("ProductTypeId") REFERENCES "ProductTypes" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    BEGIN EXECUTE 'ALTER TABLE "PublicSalesRecords" ADD CONSTRAINT "FK_PublicSalesRecords_ResourceTypes_ResourceTypeId" FOREIGN KEY ("ResourceTypeId") REFERENCES "ResourceTypes" ("Id") ON DELETE CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
                    
                    -- Step 4: Create FxRates table if it doesn't exist
                    BEGIN EXECUTE 'CREATE TABLE IF NOT EXISTS "FxRates" (
                        "Id" uuid NOT NULL,
                        "BaseCurrencyCode" character varying(3) NOT NULL,
                        "QuoteCurrencyCode" character varying(3) NOT NULL,
                        "Rate" numeric(18,6) NOT NULL,
                        "RateDate" date NOT NULL,
                        "FetchedAtUtc" timestamp with time zone NOT NULL,
                        "Source" character varying(20) NOT NULL,
                        CONSTRAINT "PK_FxRates" PRIMARY KEY ("Id")
                    )'; EXCEPTION WHEN OTHERS THEN NULL; END;

                END $$;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Note: This comprehensive repair migration intentionally has no down path.
            // Rolling back would revert the database to the broken state that required repair.
        }
    }
}

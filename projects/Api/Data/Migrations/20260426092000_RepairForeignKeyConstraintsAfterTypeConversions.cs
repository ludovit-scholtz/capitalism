using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RepairForeignKeyConstraintsAfterTypeConversions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration repairs foreign key constraints after UUID and decimal type conversions.
            // Many FK constraints were defined with TEXT column types and became invalid when those
            // columns were converted to uuid type. This migration drops and recreates all FKs to ensure
            // they align with the corrected column types.

            if (!ActiveProvider.Contains("Npgsql", System.StringComparison.OrdinalIgnoreCase))
                return;

            migrationBuilder.Sql(
                @"
DO $$
DECLARE
    constraint_record RECORD;
BEGIN
    -- Drop all foreign key constraints to reset them
    FOR constraint_record IN
        SELECT constraint_name, table_name
        FROM information_schema.table_constraints
        WHERE constraint_type = 'FOREIGN KEY'
        AND table_schema = 'public'
    LOOP
        EXECUTE format('ALTER TABLE %I DROP CONSTRAINT IF EXISTS %I', 
                      constraint_record.table_name, 
                      constraint_record.constraint_name);
    END LOOP;
END $$;
                ");

            // Re-create all foreign key constraints with correct column types
            // These are extracted from the original migration 20260330191942_AddBuildingLotRawMaterialFields

            // Companies -> Players
            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Players_PlayerId",
                table: "Companies",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // CityResources -> City
            migrationBuilder.AddForeignKey(
                name: "FK_CityResources_Cities_CityId",
                table: "CityResources",
                column: "CityId",
                principalTable: "Cities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // CityResources -> ResourceTypes
            migrationBuilder.AddForeignKey(
                name: "FK_CityResources_ResourceTypes_ResourceTypeId",
                table: "CityResources",
                column: "ResourceTypeId",
                principalTable: "ResourceTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // ProductRecipes -> ProductTypes (product)
            migrationBuilder.AddForeignKey(
                name: "FK_ProductRecipes_ProductTypes_ProductTypeId",
                table: "ProductRecipes",
                column: "ProductTypeId",
                principalTable: "ProductTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // ProductRecipes -> ResourceTypes (resource)
            migrationBuilder.AddForeignKey(
                name: "FK_ProductRecipes_ResourceTypes_ResourceTypeId",
                table: "ProductRecipes",
                column: "ResourceTypeId",
                principalTable: "ResourceTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Brands -> Companies
            migrationBuilder.AddForeignKey(
                name: "FK_Brands_Companies_CompanyId",
                table: "Brands",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Buildings -> Companies
            migrationBuilder.AddForeignKey(
                name: "FK_Buildings_Companies_CompanyId",
                table: "Buildings",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Buildings -> Cities
            migrationBuilder.AddForeignKey(
                name: "FK_Buildings_Cities_CityId",
                table: "Buildings",
                column: "CityId",
                principalTable: "Cities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // CompanyCitySalarySettings -> Companies
            migrationBuilder.AddForeignKey(
                name: "FK_CompanyCitySalarySettings_Companies_CompanyId",
                table: "CompanyCitySalarySettings",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // CompanyCitySalarySettings -> Cities
            migrationBuilder.AddForeignKey(
                name: "FK_CompanyCitySalarySettings_Cities_CityId",
                table: "CompanyCitySalarySettings",
                column: "CityId",
                principalTable: "Cities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // StartupPackOffers -> Players
            migrationBuilder.AddForeignKey(
                name: "FK_StartupPackOffers_Players_PlayerId",
                table: "StartupPackOffers",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // StartupPackOffers -> Companies
            migrationBuilder.AddForeignKey(
                name: "FK_StartupPackOffers_Companies_GrantedCompanyId",
                table: "StartupPackOffers",
                column: "GrantedCompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // BuildingConfigurationPlans -> Buildings
            migrationBuilder.AddForeignKey(
                name: "FK_BuildingConfigurationPlans_Buildings_BuildingId",
                table: "BuildingConfigurationPlans",
                column: "BuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // BuildingLots -> Buildings
            migrationBuilder.AddForeignKey(
                name: "FK_BuildingLots_Buildings_BuildingId",
                table: "BuildingLots",
                column: "BuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // BuildingLots -> Cities
            migrationBuilder.AddForeignKey(
                name: "FK_BuildingLots_Cities_CityId",
                table: "BuildingLots",
                column: "CityId",
                principalTable: "Cities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // BuildingLots -> Companies (OwnerCompanyId)
            migrationBuilder.AddForeignKey(
                name: "FK_BuildingLots_Companies_OwnerCompanyId",
                table: "BuildingLots",
                column: "OwnerCompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // BuildingLots -> ResourceTypes
            migrationBuilder.AddForeignKey(
                name: "FK_BuildingLots_ResourceTypes_ResourceTypeId",
                table: "BuildingLots",
                column: "ResourceTypeId",
                principalTable: "ResourceTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // BuildingUnits -> Buildings
            migrationBuilder.AddForeignKey(
                name: "FK_BuildingUnits_Buildings_BuildingId",
                table: "BuildingUnits",
                column: "BuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // ExchangeOrders -> Buildings (ExchangeBuildingId)
            migrationBuilder.AddForeignKey(
                name: "FK_ExchangeOrders_Buildings_ExchangeBuildingId",
                table: "ExchangeOrders",
                column: "ExchangeBuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // ExchangeOrders -> Companies
            migrationBuilder.AddForeignKey(
                name: "FK_ExchangeOrders_Companies_CompanyId",
                table: "ExchangeOrders",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // BuildingConfigurationPlanRemovals -> BuildingConfigurationPlans
            migrationBuilder.AddForeignKey(
                name: "FK_BuildingConfigurationPlanRemovals_BuildingConfigurationPlans_BuildingConfigurationPlanId",
                table: "BuildingConfigurationPlanRemovals",
                column: "BuildingConfigurationPlanId",
                principalTable: "BuildingConfigurationPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // BuildingConfigurationPlanUnits -> BuildingConfigurationPlans
            migrationBuilder.AddForeignKey(
                name: "FK_BuildingConfigurationPlanUnits_BuildingConfigurationPlans_BuildingConfigurationPlanId",
                table: "BuildingConfigurationPlanUnits",
                column: "BuildingConfigurationPlanId",
                principalTable: "BuildingConfigurationPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // BuildingUnitResourceHistories -> BuildingUnits
            migrationBuilder.AddForeignKey(
                name: "FK_BuildingUnitResourceHistories_BuildingUnits_BuildingUnitId",
                table: "BuildingUnitResourceHistories",
                column: "BuildingUnitId",
                principalTable: "BuildingUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // BuildingUnitResourceHistories -> Buildings
            migrationBuilder.AddForeignKey(
                name: "FK_BuildingUnitResourceHistories_Buildings_BuildingId",
                table: "BuildingUnitResourceHistories",
                column: "BuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // BuildingUnitResourceHistories -> ProductTypes
            migrationBuilder.AddForeignKey(
                name: "FK_BuildingUnitResourceHistories_ProductTypes_ProductTypeId",
                table: "BuildingUnitResourceHistories",
                column: "ProductTypeId",
                principalTable: "ProductTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // BuildingUnitResourceHistories -> ResourceTypes
            migrationBuilder.AddForeignKey(
                name: "FK_BuildingUnitResourceHistories_ResourceTypes_ResourceTypeId",
                table: "BuildingUnitResourceHistories",
                column: "ResourceTypeId",
                principalTable: "ResourceTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Inventories -> BuildingUnits
            migrationBuilder.AddForeignKey(
                name: "FK_Inventories_BuildingUnits_BuildingUnitId",
                table: "Inventories",
                column: "BuildingUnitId",
                principalTable: "BuildingUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Inventories -> Buildings
            migrationBuilder.AddForeignKey(
                name: "FK_Inventories_Buildings_BuildingId",
                table: "Inventories",
                column: "BuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Inventories -> ProductTypes
            migrationBuilder.AddForeignKey(
                name: "FK_Inventories_ProductTypes_ProductTypeId",
                table: "Inventories",
                column: "ProductTypeId",
                principalTable: "ProductTypes",
                principalColumn: "Id");

            // Inventories -> ResourceTypes
            migrationBuilder.AddForeignKey(
                name: "FK_Inventories_ResourceTypes_ResourceTypeId",
                table: "Inventories",
                column: "ResourceTypeId",
                principalTable: "ResourceTypes",
                principalColumn: "Id");

            // LedgerEntries -> BuildingUnits
            migrationBuilder.AddForeignKey(
                name: "FK_LedgerEntries_BuildingUnits_BuildingUnitId",
                table: "LedgerEntries",
                column: "BuildingUnitId",
                principalTable: "BuildingUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // LedgerEntries -> Buildings
            migrationBuilder.AddForeignKey(
                name: "FK_LedgerEntries_Buildings_BuildingId",
                table: "LedgerEntries",
                column: "BuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // LedgerEntries -> Companies
            migrationBuilder.AddForeignKey(
                name: "FK_LedgerEntries_Companies_CompanyId",
                table: "LedgerEntries",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // LedgerEntries -> ProductTypes
            migrationBuilder.AddForeignKey(
                name: "FK_LedgerEntries_ProductTypes_ProductTypeId",
                table: "LedgerEntries",
                column: "ProductTypeId",
                principalTable: "ProductTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // LedgerEntries -> ResourceTypes
            migrationBuilder.AddForeignKey(
                name: "FK_LedgerEntries_ResourceTypes_ResourceTypeId",
                table: "LedgerEntries",
                column: "ResourceTypeId",
                principalTable: "ResourceTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // PublicSalesRecords -> BuildingUnits
            migrationBuilder.AddForeignKey(
                name: "FK_PublicSalesRecords_BuildingUnits_BuildingUnitId",
                table: "PublicSalesRecords",
                column: "BuildingUnitId",
                principalTable: "BuildingUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // PublicSalesRecords -> Buildings
            migrationBuilder.AddForeignKey(
                name: "FK_PublicSalesRecords_Buildings_BuildingId",
                table: "PublicSalesRecords",
                column: "BuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // PublicSalesRecords -> Cities
            migrationBuilder.AddForeignKey(
                name: "FK_PublicSalesRecords_Cities_CityId",
                table: "PublicSalesRecords",
                column: "CityId",
                principalTable: "Cities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // PublicSalesRecords -> Companies
            migrationBuilder.AddForeignKey(
                name: "FK_PublicSalesRecords_Companies_CompanyId",
                table: "PublicSalesRecords",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // PublicSalesRecords -> ProductTypes
            migrationBuilder.AddForeignKey(
                name: "FK_PublicSalesRecords_ProductTypes_ProductTypeId",
                table: "PublicSalesRecords",
                column: "ProductTypeId",
                principalTable: "ProductTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // PublicSalesRecords -> ResourceTypes
            migrationBuilder.AddForeignKey(
                name: "FK_PublicSalesRecords_ResourceTypes_ResourceTypeId",
                table: "PublicSalesRecords",
                column: "ResourceTypeId",
                principalTable: "ResourceTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Note: Rollback is intentionally not implemented for this repair migration.
            // Rolling back foreign key repairs would revert to the broken state from which
            // the repairs were needed. The forward migration path is the only supported direction.
        }
    }
}

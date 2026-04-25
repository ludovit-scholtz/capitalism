using Api.Data;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260426090000_RepairLegacyTextGuidColumnTypes")]
    public partial class RepairLegacyTextGuidColumnTypes : Migration
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
                    target record;
                BEGIN
                    FOR target IN
                        SELECT *
                        FROM (VALUES
                            ('Players', 'Id'),
                            ('Players', 'OnboardingCityId'),
                            ('Players', 'OnboardingCompanyId'),
                            ('Players', 'OnboardingFactoryLotId'),
                            ('Players', 'OnboardingShopBuildingId'),
                            ('Players', 'ConcurrencyToken'),
                            ('ProductTypes', 'Id'),
                            ('ResourceTypes', 'Id'),
                            ('Companies', 'Id'),
                            ('Companies', 'PlayerId'),
                            ('City', 'Id'),
                            ('CityResources', 'Id'),
                            ('CityResources', 'CityId'),
                            ('CityResources', 'ResourceTypeId'),
                            ('ProductRecipes', 'Id'),
                            ('ProductRecipes', 'ProductTypeId'),
                            ('ProductRecipes', 'ResourceTypeId'),
                            ('ProductRecipes', 'InputProductTypeId'),
                            ('Buildings', 'Id'),
                            ('Buildings', 'CompanyId'),
                            ('Buildings', 'CityId'),
                            ('Buildings', 'ProductTypeId'),
                            ('BuildingLots', 'Id'),
                            ('BuildingLots', 'CityId'),
                            ('BuildingLots', 'OwnerCompanyId'),
                            ('BuildingLots', 'BuildingId'),
                            ('BuildingLots', 'ResourceTypeId'),
                            ('BuildingLots', 'ConcurrencyToken'),
                            ('BuildingUnits', 'Id'),
                            ('BuildingUnits', 'BuildingId'),
                            ('BuildingUnits', 'ResourceTypeId'),
                            ('BuildingUnits', 'ProductTypeId'),
                            ('BuildingUnits', 'MediaHouseBuildingId'),
                            ('BuildingUnits', 'VendorLockCompanyId'),
                            ('ExchangeOrders', 'Id'),
                            ('ExchangeOrders', 'ExchangeBuildingId'),
                            ('ExchangeOrders', 'CompanyId'),
                            ('ExchangeOrders', 'ResourceTypeId'),
                            ('ExchangeOrders', 'ProductTypeId'),
                            ('BuildingConfigurationPlans', 'Id'),
                            ('BuildingConfigurationPlans', 'BuildingConfigurationPlanId'),
                            ('BuildingConfigurationPlanUnits', 'Id'),
                            ('BuildingConfigurationPlanUnits', 'BuildingConfigurationPlanId'),
                            ('BuildingConfigurationPlanUnits', 'ResourceTypeId'),
                            ('BuildingConfigurationPlanUnits', 'ProductTypeId'),
                            ('BuildingConfigurationPlanUnits', 'MediaHouseBuildingId'),
                            ('BuildingConfigurationPlanUnits', 'VendorLockCompanyId'),
                            ('BuildingConfigurationPlanRemovals', 'Id'),
                            ('BuildingConfigurationPlanRemovals', 'BuildingConfigurationPlanId'),
                            ('BuildingUnitResourceHistories', 'Id'),
                            ('BuildingUnitResourceHistories', 'BuildingId'),
                            ('BuildingUnitResourceHistories', 'BuildingUnitId'),
                            ('BuildingUnitResourceHistories', 'ResourceTypeId'),
                            ('BuildingUnitResourceHistories', 'ProductTypeId'),
                            ('BuildingUnitResourceHistories', 'BrandId'),
                            ('Inventories', 'Id'),
                            ('Inventories', 'CompanyId'),
                            ('Inventories', 'BuildingId'),
                            ('Inventories', 'BuildingUnitId'),
                            ('Inventories', 'ProductTypeId'),
                            ('Inventories', 'ResourceTypeId'),
                            ('LedgerEntries', 'Id'),
                            ('LedgerEntries', 'BuildingUnitId'),
                            ('LedgerEntries', 'BuildingId'),
                            ('LedgerEntries', 'CompanyId'),
                            ('LedgerEntries', 'CityId')
                        ) AS t(table_name, column_name)
                    LOOP
                        IF EXISTS (
                            SELECT 1
                            FROM information_schema.columns c
                            WHERE c.table_schema = 'public'
                              AND c.table_name = target.table_name
                              AND c.column_name = target.column_name
                              AND c.data_type IN ('text', 'character varying', 'character')) THEN

                            -- Drop dependent objects (constraints, indexes)
                            -- that reference this column
                            
                            EXECUTE format(
                                'ALTER TABLE %I ALTER COLUMN %I DROP DEFAULT',
                                target.table_name,
                                target.column_name);

                            -- Convert TEXT to uuid
                            EXECUTE format(
                                'ALTER TABLE %I ALTER COLUMN %I TYPE uuid USING (%I::uuid)',
                                target.table_name,
                                target.column_name,
                                target.column_name);
                        END IF;
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down migration not supported for schema repair
        }
    }
}

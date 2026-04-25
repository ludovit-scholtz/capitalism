using Api.Data;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260426091000_RepairLegacyTextDecimalColumnTypes")]
    public partial class RepairLegacyTextDecimalColumnTypes : Migration
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
                            ('City', 'AverageRentPerSqm'),
                            ('City', 'BaseSalaryPerManhour'),
                            ('GameState', 'TaxRate'),
                            ('ProductType', 'BasePrice'),
                            ('ProductType', 'OutputQuantity'),
                            ('ProductType', 'EnergyConsumptionMwh'),
                            ('ProductType', 'BasicLaborHours'),
                            ('ResourceType', 'BasePrice'),
                            ('ResourceType', 'WeightPerUnit'),
                            ('Company', 'Cash'),
                            ('CityResources', 'Abundance'),
                            ('ProductRecipe', 'Quantity'),
                            ('Brand', 'Awareness'),
                            ('Brand', 'Quality'),
                            ('Building', 'PowerConsumption'),
                            ('BuildingLot', 'AskingPrice'),
                            ('BuildingLot', 'PricePerSqm'),
                            ('BuildingLot', 'OccupancyPercent'),
                            ('BuildingLot', 'TotalAreaSqm'),
                            ('BuildingLot', 'PowerOutput'),
                            ('BuildingUnit', 'BasePrice'),
                            ('BuildingUnit', 'CurrentQuality'),
                            ('BuildingUnit', 'MinPrice'),
                            ('BuildingUnit', 'AskingPrice'),
                            ('BuildingUnit', 'StockTurnoverTarget'),
                            ('ExchangeOrder', 'PricePerUnit'),
                            ('ExchangeOrder', 'QuantityRemaining'),
                            ('ExchangeOrder', 'QuantityFilled'),
                            ('BuildingConfigurationPlanUnit', 'BasePrice'),
                            ('BuildingConfigurationPlanUnit', 'CurrentQuality'),
                            ('BuildingConfigurationPlanUnit', 'MinPrice'),
                            ('BuildingConfigurationPlanUnit', 'AskingPrice'),
                            ('BuildingConfigurationPlanUnit', 'StockTurnoverTarget'),
                            ('Inventory', 'Quantity'),
                            ('Inventory', 'Quality'),
                            ('LedgerEntry', 'Amount')
                        ) AS t(table_name, column_name)
                    LOOP
                        IF EXISTS (
                            SELECT 1
                            FROM information_schema.columns c
                            WHERE c.table_schema = 'public'
                              AND c.table_name = target.table_name
                              AND c.column_name = target.column_name
                              AND c.data_type = 'text') THEN

                            EXECUTE format(
                                'ALTER TABLE %I ALTER COLUMN %I DROP DEFAULT',
                                target.table_name,
                                target.column_name);

                            -- Convert TEXT to numeric(18,4) for proper decimal storage
                            EXECUTE format(
                                'ALTER TABLE %I ALTER COLUMN %I TYPE numeric(18,4) USING (%I::numeric)',
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

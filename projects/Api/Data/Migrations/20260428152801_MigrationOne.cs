using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MigrationOne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql(
                    """
                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1
                            FROM information_schema.columns
                            WHERE table_schema = 'public'
                              AND table_name = 'Cities'
                              AND column_name = 'FuelPriceIndex') THEN
                            ALTER TABLE "Cities" ALTER COLUMN "FuelPriceIndex" DROP DEFAULT;
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM information_schema.columns
                            WHERE table_schema = 'public'
                              AND table_name = 'Buildings'
                              AND column_name = 'FuelReserveMwh') THEN
                            ALTER TABLE "Buildings" ALTER COLUMN "FuelReserveMwh" DROP DEFAULT;
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM information_schema.columns
                            WHERE table_schema = 'public'
                              AND table_name = 'Buildings'
                              AND column_name = 'DispatchTargetPercent') THEN
                            ALTER TABLE "Buildings" ALTER COLUMN "DispatchTargetPercent" DROP DEFAULT;
                        END IF;
                    END $$;
                    """);
                return;
            }

            migrationBuilder.AlterColumn<decimal>(
                name: "FuelPriceIndex",
                table: "Cities",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldDefaultValue: 1.0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "FuelReserveMwh",
                table: "Buildings",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<int>(
                name: "DispatchTargetPercent",
                table: "Buildings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 100);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql(
                    """
                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1
                            FROM information_schema.columns
                            WHERE table_schema = 'public'
                              AND table_name = 'Cities'
                              AND column_name = 'FuelPriceIndex') THEN
                            ALTER TABLE "Cities" ALTER COLUMN "FuelPriceIndex" SET DEFAULT 1.0;
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM information_schema.columns
                            WHERE table_schema = 'public'
                              AND table_name = 'Buildings'
                              AND column_name = 'FuelReserveMwh') THEN
                            ALTER TABLE "Buildings" ALTER COLUMN "FuelReserveMwh" SET DEFAULT 0;
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM information_schema.columns
                            WHERE table_schema = 'public'
                              AND table_name = 'Buildings'
                              AND column_name = 'DispatchTargetPercent') THEN
                            ALTER TABLE "Buildings" ALTER COLUMN "DispatchTargetPercent" SET DEFAULT 100;
                        END IF;
                    END $$;
                    """);
                return;
            }

            migrationBuilder.AlterColumn<decimal>(
                name: "FuelPriceIndex",
                table: "Cities",
                type: "numeric",
                nullable: false,
                defaultValue: 1.0m,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "FuelReserveMwh",
                table: "Buildings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<int>(
                name: "DispatchTargetPercent",
                table: "Buildings",
                type: "integer",
                nullable: false,
                defaultValue: 100,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCompanyCash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cash",
                table: "Companies");

            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql(
                    """
                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1
                            FROM pg_constraint
                            WHERE conname = 'FK_BankAccounts_Companies_CompanyId1') THEN
                            ALTER TABLE "BankAccounts" DROP CONSTRAINT "FK_BankAccounts_Companies_CompanyId1";
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM pg_indexes
                            WHERE schemaname = 'public'
                              AND indexname = 'IX_BankAccounts_CompanyId1') THEN
                            DROP INDEX "IX_BankAccounts_CompanyId1";
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM information_schema.columns
                            WHERE table_schema = 'public'
                              AND table_name = 'BankAccounts'
                              AND column_name = 'CompanyId1') THEN
                            ALTER TABLE "BankAccounts" DROP COLUMN "CompanyId1";
                        END IF;
                    END $$;
                    """);

                return;
            }

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId1",
                table: "BankAccounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_CompanyId1",
                table: "BankAccounts",
                column: "CompanyId1");

            migrationBuilder.AddForeignKey(
                name: "FK_BankAccounts_Companies_CompanyId1",
                table: "BankAccounts",
                column: "CompanyId1",
                principalTable: "Companies",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankAccounts_Companies_CompanyId1",
                table: "BankAccounts");

            migrationBuilder.DropIndex(
                name: "IX_BankAccounts_CompanyId1",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "CompanyId1",
                table: "BankAccounts");

            migrationBuilder.AddColumn<decimal>(
                name: "Cash",
                table: "Companies",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }
    }
}

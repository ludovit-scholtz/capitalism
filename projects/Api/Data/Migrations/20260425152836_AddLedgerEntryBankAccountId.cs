using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerEntryBankAccountId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BankAccountId",
                table: "LedgerEntries",
                type: "uuid",
                nullable: true);

                        migrationBuilder.Sql(@"
UPDATE ""LedgerEntries"" AS le
SET ""BankAccountId"" = b.""BankAccountId""
FROM ""Buildings"" AS b
WHERE le.""BankAccountId"" IS NULL
    AND le.""BuildingId"" = b.""Id""
    AND b.""BankAccountId"" IS NOT NULL;
");

                        migrationBuilder.Sql(@"
WITH ranked_accounts AS (
        SELECT
                ba.""Id"",
                ba.""CompanyId"",
                ROW_NUMBER() OVER (
                        PARTITION BY ba.""CompanyId""
                        ORDER BY
                                ba.""IsBaseCapitalDeposit"" ASC,
                                (ba.""BankBuildingId"" IS NOT NULL) ASC,
                                ba.""Balance"" DESC,
                                ba.""CreatedAtUtc"" ASC
                ) AS rn
        FROM ""BankAccounts"" AS ba
        WHERE ba.""CompanyId"" IS NOT NULL
            AND ba.""ClosedAtUtc"" IS NULL
)
UPDATE ""LedgerEntries"" AS le
SET ""BankAccountId"" = ra.""Id""
FROM ranked_accounts AS ra
WHERE le.""BankAccountId"" IS NULL
    AND le.""CompanyId"" = ra.""CompanyId""
    AND ra.rn = 1;
");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_BankAccountId",
                table: "LedgerEntries",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_CompanyId_BankAccountId_RecordedAtTick",
                table: "LedgerEntries",
                columns: new[] { "CompanyId", "BankAccountId", "RecordedAtTick" });

            migrationBuilder.AddForeignKey(
                name: "FK_LedgerEntries_BankAccounts_BankAccountId",
                table: "LedgerEntries",
                column: "BankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LedgerEntries_BankAccounts_BankAccountId",
                table: "LedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_BankAccountId",
                table: "LedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_CompanyId_BankAccountId_RecordedAtTick",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "BankAccountId",
                table: "LedgerEntries");
        }
    }
}

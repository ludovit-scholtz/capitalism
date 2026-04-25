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
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql(
                    """
                    DO $$
                    DECLARE
                        bank_account_id_type TEXT;
                    BEGIN
                        SELECT pg_catalog.format_type(a.atttypid, a.atttypmod)
                        INTO bank_account_id_type
                        FROM pg_attribute a
                        JOIN pg_class c ON c.oid = a.attrelid
                        JOIN pg_namespace n ON n.oid = c.relnamespace
                        WHERE n.nspname = 'public'
                          AND c.relname = 'BankAccounts'
                          AND a.attname = 'Id'
                          AND a.attnum > 0
                          AND NOT a.attisdropped;

                        IF bank_account_id_type IS NULL THEN
                            RAISE EXCEPTION 'BankAccounts.Id column not found while adding LedgerEntries.BankAccountId';
                        END IF;

                        EXECUTE format(
                            'ALTER TABLE "LedgerEntries" ADD COLUMN IF NOT EXISTS "BankAccountId" %s NULL',
                            CASE WHEN bank_account_id_type = 'uuid' THEN 'uuid' ELSE 'text' END);
                    END $$;
                    """);

                migrationBuilder.Sql(@"
UPDATE ""LedgerEntries"" AS le
SET ""BankAccountId"" = b.""BankAccountId""
FROM ""Buildings"" AS b
WHERE le.""BankAccountId"" IS NULL
    AND le.""BuildingId""::text = b.""Id""::text
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
            AND le.""CompanyId""::text = ra.""CompanyId""::text
    AND ra.rn = 1;
");

                migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_LedgerEntries_BankAccountId\" ON \"LedgerEntries\" (\"BankAccountId\");");
                migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_LedgerEntries_CompanyId_BankAccountId_RecordedAtTick\" ON \"LedgerEntries\" (\"CompanyId\", \"BankAccountId\", \"RecordedAtTick\");");

                migrationBuilder.Sql(
                    """
                    DO $$
                    BEGIN
                        IF NOT EXISTS (
                            SELECT 1
                            FROM pg_constraint
                            WHERE conname = 'FK_LedgerEntries_BankAccounts_BankAccountId') THEN
                            ALTER TABLE "LedgerEntries"
                                ADD CONSTRAINT "FK_LedgerEntries_BankAccounts_BankAccountId"
                                FOREIGN KEY ("BankAccountId") REFERENCES "BankAccounts" ("Id") ON DELETE SET NULL;
                        END IF;
                    END $$;
                    """);

                return;
            }

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

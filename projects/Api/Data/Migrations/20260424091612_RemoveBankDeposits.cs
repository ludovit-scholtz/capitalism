using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBankDeposits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_BankAccounts_CompanyId\";");

                migrationBuilder.Sql(
                    """
                    DO $$
                    DECLARE
                        building_id_type TEXT;
                    BEGIN
                        SELECT pg_catalog.format_type(a.atttypid, a.atttypmod)
                        INTO building_id_type
                        FROM pg_attribute a
                        JOIN pg_class c ON c.oid = a.attrelid
                        JOIN pg_namespace n ON n.oid = c.relnamespace
                        WHERE n.nspname = 'public'
                          AND c.relname = 'Buildings'
                          AND a.attname = 'Id'
                          AND a.attnum > 0
                          AND NOT a.attisdropped;

                        IF building_id_type IS NULL THEN
                            RAISE EXCEPTION 'Buildings.Id column not found while adding BankAccounts.BankBuildingId';
                        END IF;

                        EXECUTE format(
                            'ALTER TABLE "BankAccounts" ADD COLUMN IF NOT EXISTS "BankBuildingId" %s NULL',
                            CASE WHEN building_id_type = 'uuid' THEN 'uuid' ELSE 'text' END);
                    END $$;
                    """);

                migrationBuilder.Sql("ALTER TABLE \"BankAccounts\" ADD COLUMN IF NOT EXISTS \"ClosedAtTick\" bigint NULL;");
                migrationBuilder.Sql("ALTER TABLE \"BankAccounts\" ADD COLUMN IF NOT EXISTS \"ClosedAtUtc\" timestamp with time zone NULL;");
                migrationBuilder.Sql("ALTER TABLE \"BankAccounts\" ADD COLUMN IF NOT EXISTS \"DepositInterestRatePercent\" numeric(8,4) NULL;");
                migrationBuilder.Sql("ALTER TABLE \"BankAccounts\" ADD COLUMN IF NOT EXISTS \"DepositedAtTick\" bigint NULL;");
                migrationBuilder.Sql("ALTER TABLE \"BankAccounts\" ADD COLUMN IF NOT EXISTS \"IsBaseCapitalDeposit\" boolean NOT NULL DEFAULT FALSE;");
                migrationBuilder.Sql("ALTER TABLE \"BankAccounts\" ADD COLUMN IF NOT EXISTS \"TotalInterestPaid\" numeric(18,4) NOT NULL DEFAULT 0;");

                migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_BankAccounts_BankBuildingId_ClosedAtUtc\" ON \"BankAccounts\" (\"BankBuildingId\", \"ClosedAtUtc\");");
                migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_BankAccounts_CompanyId_BankBuildingId_ClosedAtUtc\" ON \"BankAccounts\" (\"CompanyId\", \"BankBuildingId\", \"ClosedAtUtc\");");

                migrationBuilder.Sql(
                    """
                    DO $$
                    BEGIN
                        IF NOT EXISTS (
                            SELECT 1
                            FROM pg_constraint
                            WHERE conname = 'FK_BankAccounts_Buildings_BankBuildingId') THEN
                            ALTER TABLE "BankAccounts"
                                ADD CONSTRAINT "FK_BankAccounts_Buildings_BankBuildingId"
                                FOREIGN KEY ("BankBuildingId") REFERENCES "Buildings" ("Id") ON DELETE CASCADE;
                        END IF;
                    END $$;
                    """);

                migrationBuilder.Sql(GetBankDepositBackfillSql(isDown: false));
                migrationBuilder.Sql("DROP TABLE IF EXISTS \"BankDeposits\";");
                return;
            }

            migrationBuilder.DropIndex(
                name: "IX_BankAccounts_CompanyId",
                table: "BankAccounts");

            migrationBuilder.AddColumn<Guid>(
                name: "BankBuildingId",
                table: "BankAccounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ClosedAtTick",
                table: "BankAccounts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAtUtc",
                table: "BankAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DepositInterestRatePercent",
                table: "BankAccounts",
                type: "numeric(8,4)",
                precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DepositedAtTick",
                table: "BankAccounts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBaseCapitalDeposit",
                table: "BankAccounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalInterestPaid",
                table: "BankAccounts",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_BankBuildingId_ClosedAtUtc",
                table: "BankAccounts",
                columns: new[] { "BankBuildingId", "ClosedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_CompanyId_BankBuildingId_ClosedAtUtc",
                table: "BankAccounts",
                columns: new[] { "CompanyId", "BankBuildingId", "ClosedAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_BankAccounts_Buildings_BankBuildingId",
                table: "BankAccounts",
                column: "BankBuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql(GetBankDepositBackfillSql(isDown: false));

            migrationBuilder.DropTable(
                name: "BankDeposits");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankDeposits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BankBuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepositorCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DepositInterestRatePercent = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    DepositedAtTick = table.Column<long>(type: "bigint", nullable: false),
                    DepositedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsBaseCapital = table.Column<bool>(type: "boolean", nullable: false),
                    TotalInterestPaid = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    WithdrawnAtTick = table.Column<long>(type: "bigint", nullable: true),
                    WithdrawnAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankDeposits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankDeposits_Buildings_BankBuildingId",
                        column: x => x.BankBuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankDeposits_Companies_DepositorCompanyId",
                        column: x => x.DepositorCompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_CompanyId",
                table: "BankAccounts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_BankDeposits_BankBuildingId_IsActive",
                table: "BankDeposits",
                columns: new[] { "BankBuildingId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_BankDeposits_DepositorCompanyId_IsActive",
                table: "BankDeposits",
                columns: new[] { "DepositorCompanyId", "IsActive" });

            migrationBuilder.Sql(GetBankDepositBackfillSql(isDown: true));

            migrationBuilder.DropForeignKey(
                name: "FK_BankAccounts_Buildings_BankBuildingId",
                table: "BankAccounts");

            migrationBuilder.DropIndex(
                name: "IX_BankAccounts_BankBuildingId_ClosedAtUtc",
                table: "BankAccounts");

            migrationBuilder.DropIndex(
                name: "IX_BankAccounts_CompanyId_BankBuildingId_ClosedAtUtc",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "BankBuildingId",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "ClosedAtTick",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "ClosedAtUtc",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "DepositInterestRatePercent",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "DepositedAtTick",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "IsBaseCapitalDeposit",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "TotalInterestPaid",
                table: "BankAccounts");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_CompanyId",
                table: "BankAccounts",
                column: "CompanyId");
        }

        private string GetBankDepositBackfillSql(bool isDown)
        {
            if (isDown)
            {
                return ActiveProvider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase)
                    ?
                        """
                        INSERT INTO "BankDeposits" ("Id", "BankBuildingId", "DepositorCompanyId", "Amount", "DepositInterestRatePercent", "IsBaseCapital", "IsActive", "DepositedAtTick", "DepositedAtUtc", "WithdrawnAtTick", "WithdrawnAtUtc", "TotalInterestPaid")
                        SELECT account."Id",
                               account."BankBuildingId",
                               account."CompanyId",
                               account."Balance",
                               COALESCE(
                                   CASE
                                       WHEN account."DepositInterestRatePercent" IS NULL THEN NULL
                                       WHEN account."DepositInterestRatePercent"::text ~ '^\s*[-+]?\d+(\.\d+)?\s*$' THEN (account."DepositInterestRatePercent"::text)::numeric
                                       ELSE NULL
                                   END,
                                   0
                               ),
                               CASE
                                   WHEN account."IsBaseCapitalDeposit" IS NULL THEN FALSE
                                   WHEN LOWER(TRIM(account."IsBaseCapitalDeposit"::text)) IN ('1', 'true', 't', 'yes', 'y') THEN TRUE
                                   WHEN LOWER(TRIM(account."IsBaseCapitalDeposit"::text)) IN ('0', 'false', 'f', 'no', 'n') THEN FALSE
                                   ELSE FALSE
                               END,
                               account."ClosedAtUtc" IS NULL,
                               COALESCE(account."DepositedAtTick", 0),
                               COALESCE(
                                   CASE
                                       WHEN account."CreatedAtUtc" IS NULL THEN NULL
                                       WHEN account."CreatedAtUtc"::text ~ '^\s*\d{4}-\d{2}-\d{2}([ T]\d{2}:\d{2}:\d{2}(\.\d+)?)?([+-]\d{2}(:?\d{2})?|Z)?\s*$' THEN (account."CreatedAtUtc"::text)::timestamp with time zone
                                       ELSE NULL
                                   END,
                                   CURRENT_TIMESTAMP
                               ),
                               account."ClosedAtTick",
                               CASE
                                   WHEN account."ClosedAtUtc" IS NULL THEN NULL
                                   WHEN account."ClosedAtUtc"::text ~ '^\s*\d{4}-\d{2}-\d{2}([ T]\d{2}:\d{2}:\d{2}(\.\d+)?)?([+-]\d{2}(:?\d{2})?|Z)?\s*$' THEN (account."ClosedAtUtc"::text)::timestamp with time zone
                                   ELSE NULL
                               END,
                               account."TotalInterestPaid"
                        FROM "BankAccounts" AS account
                        WHERE account."BankBuildingId" IS NOT NULL
                          AND account."CompanyId" IS NOT NULL
                          AND NOT EXISTS (
                              SELECT 1
                              FROM "BankDeposits" AS legacy
                              WHERE legacy."Id" = account."Id"
                          );
                        """
                    :
                        """
                        INSERT INTO "BankDeposits" ("Id", "BankBuildingId", "DepositorCompanyId", "Amount", "DepositInterestRatePercent", "IsBaseCapital", "IsActive", "DepositedAtTick", "DepositedAtUtc", "WithdrawnAtTick", "WithdrawnAtUtc", "TotalInterestPaid")
                        SELECT account."Id",
                               account."BankBuildingId",
                               account."CompanyId",
                               COALESCE(
                                   CASE
                                       WHEN account."Balance" IS NULL THEN NULL
                                       WHEN account."Balance"::text ~ '^\s*[-+]?\d+(\.\d+)?\s*$' THEN (account."Balance"::text)::numeric
                                       ELSE NULL
                                   END,
                                   0
                               ),
                               COALESCE(
                                   CASE
                                       WHEN account."DepositInterestRatePercent" IS NULL THEN NULL
                                       WHEN account."DepositInterestRatePercent"::text ~ '^\s*[-+]?\d+(\.\d+)?\s*$' THEN (account."DepositInterestRatePercent"::text)::numeric
                                       ELSE NULL
                                   END,
                                   0
                               ),
                               CASE
                                   WHEN account."IsBaseCapitalDeposit" IS NULL THEN FALSE
                                   WHEN LOWER(TRIM(account."IsBaseCapitalDeposit"::text)) IN ('1', 'true', 't', 'yes', 'y') THEN TRUE
                                   WHEN LOWER(TRIM(account."IsBaseCapitalDeposit"::text)) IN ('0', 'false', 'f', 'no', 'n') THEN FALSE
                                   ELSE FALSE
                               END,
                               account."ClosedAtUtc" IS NULL,
                               COALESCE(account."DepositedAtTick", 0),
                               COALESCE(
                                   CASE
                                       WHEN account."CreatedAtUtc" IS NULL THEN NULL
                                       WHEN account."CreatedAtUtc"::text ~ '^\s*\d{4}-\d{2}-\d{2}([ T]\d{2}:\d{2}:\d{2}(\.\d+)?)?([+-]\d{2}(:?\d{2})?|Z)?\s*$' THEN (account."CreatedAtUtc"::text)::timestamp with time zone
                                       ELSE NULL
                                   END,
                                   CURRENT_TIMESTAMP
                               ),
                               account."ClosedAtTick",
                               CASE
                                   WHEN account."ClosedAtUtc" IS NULL THEN NULL
                                   WHEN account."ClosedAtUtc"::text ~ '^\s*\d{4}-\d{2}-\d{2}([ T]\d{2}:\d{2}:\d{2}(\.\d+)?)?([+-]\d{2}(:?\d{2})?|Z)?\s*$' THEN (account."ClosedAtUtc"::text)::timestamp with time zone
                                   ELSE NULL
                               END,
                               COALESCE(
                                   CASE
                                       WHEN account."TotalInterestPaid" IS NULL THEN NULL
                                       WHEN account."TotalInterestPaid"::text ~ '^\s*[-+]?\d+(\.\d+)?\s*$' THEN (account."TotalInterestPaid"::text)::numeric
                                       ELSE NULL
                                   END,
                                   0
                               )
                        FROM "BankAccounts" AS account
                        WHERE account."BankBuildingId" IS NOT NULL
                          AND account."CompanyId" IS NOT NULL
                          AND NOT EXISTS (
                              SELECT 1
                              FROM "BankDeposits" AS legacy
                                                            WHERE legacy."Id"::text = account."Id"::text
                          );
                        """;
            }

            return ActiveProvider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase)
                ?
                    """
                    INSERT INTO "BankAccounts" ("Id", "AccountNumber", "CurrencyCode", "Balance", "CompanyId", "IsGovernmentAccount", "CreatedAtUtc", "PlayerId", "BankBuildingId", "DepositInterestRatePercent", "DepositedAtTick", "IsBaseCapitalDeposit", "ClosedAtTick", "ClosedAtUtc", "TotalInterestPaid")
                    SELECT legacy."Id",
                           printf('%016d', 9200000000000000 + ROW_NUMBER() OVER (ORDER BY legacy."Id")),
                           city."CurrencyCode",
                           legacy."Amount",
                           legacy."DepositorCompanyId",
                           0,
                           legacy."DepositedAtUtc",
                           NULL,
                           legacy."BankBuildingId",
                           legacy."DepositInterestRatePercent",
                           legacy."DepositedAtTick",
                           legacy."IsBaseCapital",
                           legacy."WithdrawnAtTick",
                           legacy."WithdrawnAtUtc",
                           legacy."TotalInterestPaid"
                    FROM "BankDeposits" AS legacy
                    INNER JOIN "Buildings" AS bank ON bank."Id" = legacy."BankBuildingId"
                    INNER JOIN "Cities" AS city ON city."Id" = bank."CityId"
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM "BankAccounts" AS existing
                        WHERE existing."Id" = legacy."Id"
                    );
                    """
                :
                    """
                    INSERT INTO "BankAccounts" ("Id", "AccountNumber", "CurrencyCode", "Balance", "CompanyId", "IsGovernmentAccount", "CreatedAtUtc", "PlayerId", "BankBuildingId", "DepositInterestRatePercent", "DepositedAtTick", "IsBaseCapitalDeposit", "ClosedAtTick", "ClosedAtUtc", "TotalInterestPaid")
                    SELECT CASE
                               WHEN legacy."Id"::text ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$' THEN (legacy."Id"::text)::uuid
                               ELSE (
                                   SUBSTRING(MD5('bank-deposit-' || legacy."Id"::text), 1, 8) || '-' ||
                                   SUBSTRING(MD5('bank-deposit-' || legacy."Id"::text), 9, 4) || '-' ||
                                   SUBSTRING(MD5('bank-deposit-' || legacy."Id"::text), 13, 4) || '-' ||
                                   SUBSTRING(MD5('bank-deposit-' || legacy."Id"::text), 17, 4) || '-' ||
                                   SUBSTRING(MD5('bank-deposit-' || legacy."Id"::text), 21, 12)
                               )::uuid
                           END,
                           LPAD((9200000000000000 + ROW_NUMBER() OVER (ORDER BY legacy."Id"))::text, 16, '0'),
                           city."CurrencyCode",
                           COALESCE(
                               CASE
                                   WHEN legacy."Amount" IS NULL THEN NULL
                                   WHEN legacy."Amount"::text ~ '^\s*[-+]?\d+(\.\d+)?\s*$' THEN (legacy."Amount"::text)::numeric
                                   ELSE NULL
                               END,
                               0
                           ),
                           CASE
                               WHEN legacy."DepositorCompanyId" IS NULL THEN NULL
                               WHEN legacy."DepositorCompanyId"::text ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$' THEN (legacy."DepositorCompanyId"::text)::uuid
                               ELSE NULL
                           END,
                           FALSE,
                           COALESCE(
                               CASE
                                   WHEN legacy."DepositedAtUtc" IS NULL THEN NULL
                                   WHEN legacy."DepositedAtUtc"::text ~ '^\s*\d{4}-\d{2}-\d{2}([ T]\d{2}:\d{2}:\d{2}(\.\d+)?)?([+-]\d{2}(:?\d{2})?|Z)?\s*$' THEN (legacy."DepositedAtUtc"::text)::timestamp with time zone
                                   ELSE NULL
                               END,
                               CURRENT_TIMESTAMP
                           ),
                           NULL,
                           CASE
                               WHEN legacy."BankBuildingId" IS NULL THEN NULL
                               WHEN legacy."BankBuildingId"::text ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$' THEN (legacy."BankBuildingId"::text)::uuid
                               ELSE NULL
                           END,
                           COALESCE(
                               CASE
                                   WHEN legacy."DepositInterestRatePercent" IS NULL THEN NULL
                                   WHEN legacy."DepositInterestRatePercent"::text ~ '^\s*[-+]?\d+(\.\d+)?\s*$' THEN (legacy."DepositInterestRatePercent"::text)::numeric
                                   ELSE NULL
                               END,
                               0
                           ),
                           legacy."DepositedAtTick",
                           CASE
                               WHEN legacy."IsBaseCapital" IS NULL THEN FALSE
                               WHEN LOWER(TRIM(legacy."IsBaseCapital"::text)) IN ('1', 'true', 't', 'yes', 'y') THEN TRUE
                               WHEN LOWER(TRIM(legacy."IsBaseCapital"::text)) IN ('0', 'false', 'f', 'no', 'n') THEN FALSE
                               ELSE FALSE
                           END,
                           legacy."WithdrawnAtTick",
                           CASE
                               WHEN legacy."WithdrawnAtUtc" IS NULL THEN NULL
                               WHEN legacy."WithdrawnAtUtc"::text ~ '^\s*\d{4}-\d{2}-\d{2}([ T]\d{2}:\d{2}:\d{2}(\.\d+)?)?([+-]\d{2}(:?\d{2})?|Z)?\s*$' THEN (legacy."WithdrawnAtUtc"::text)::timestamp with time zone
                               ELSE NULL
                           END,
                           COALESCE(
                               CASE
                                   WHEN legacy."TotalInterestPaid" IS NULL THEN NULL
                                   WHEN legacy."TotalInterestPaid"::text ~ '^\s*[-+]?\d+(\.\d+)?\s*$' THEN (legacy."TotalInterestPaid"::text)::numeric
                                   ELSE NULL
                               END,
                               0
                           )
                    FROM "BankDeposits" AS legacy
                    INNER JOIN "Buildings" AS bank ON bank."Id"::text = legacy."BankBuildingId"::text
                    INNER JOIN "Cities" AS city ON city."Id"::text = bank."CityId"::text
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM "BankAccounts" AS existing
                        WHERE existing."Id"::text = legacy."Id"::text
                    );
                    """;
        }
    }
}

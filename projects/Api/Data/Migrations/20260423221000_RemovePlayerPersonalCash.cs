using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovePlayerPersonalCash : Migration
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
                        player_id_type TEXT;
                    BEGIN
                        SELECT pg_catalog.format_type(a.atttypid, a.atttypmod)
                        INTO player_id_type
                        FROM pg_attribute a
                        JOIN pg_class c ON c.oid = a.attrelid
                        JOIN pg_namespace n ON n.oid = c.relnamespace
                        WHERE n.nspname = 'public'
                          AND c.relname = 'Players'
                          AND a.attname = 'Id'
                          AND a.attnum > 0
                          AND NOT a.attisdropped;

                        IF player_id_type IS NULL THEN
                            RAISE EXCEPTION 'Players.Id column not found while adding BankAccounts.PlayerId';
                        END IF;

                        EXECUTE format(
                            'ALTER TABLE "BankAccounts" ADD COLUMN IF NOT EXISTS "PlayerId" %s NULL',
                            CASE WHEN player_id_type = 'uuid' THEN 'uuid' ELSE 'text' END);
                    END $$;
                    """);

                migrationBuilder.Sql(GetPlayerSettlementAccountBackfillSql(isDown: false));

                migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_BankAccounts_PlayerId_CurrencyCode\" ON \"BankAccounts\" (\"PlayerId\", \"CurrencyCode\");");

                migrationBuilder.Sql(
                    """
                    DO $$
                    BEGIN
                        IF NOT EXISTS (
                            SELECT 1
                            FROM pg_constraint
                            WHERE conname = 'FK_BankAccounts_Players_PlayerId') THEN
                            ALTER TABLE "BankAccounts"
                                ADD CONSTRAINT "FK_BankAccounts_Players_PlayerId"
                                FOREIGN KEY ("PlayerId") REFERENCES "Players" ("Id") ON DELETE SET NULL;
                        END IF;
                    END $$;
                    """);

                migrationBuilder.Sql("ALTER TABLE \"Players\" DROP COLUMN IF EXISTS \"PersonalCash\";");
                return;
            }

            migrationBuilder.AddColumn<Guid>(
                name: "PlayerId",
                table: "BankAccounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(GetPlayerSettlementAccountBackfillSql(isDown: false));

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_PlayerId_CurrencyCode",
                table: "BankAccounts",
                columns: new[] { "PlayerId", "CurrencyCode" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BankAccounts_Players_PlayerId",
                table: "BankAccounts",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.DropColumn(
                name: "PersonalCash",
                table: "Players");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PersonalCash",
                table: "Players",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(GetPlayerSettlementAccountBackfillSql(isDown: true));

            migrationBuilder.DropForeignKey(
                name: "FK_BankAccounts_Players_PlayerId",
                table: "BankAccounts");

            migrationBuilder.DropIndex(
                name: "IX_BankAccounts_PlayerId_CurrencyCode",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "PlayerId",
                table: "BankAccounts");
        }

        private string GetPlayerSettlementAccountBackfillSql(bool isDown)
        {
            if (isDown)
            {
                return
                    """
                    UPDATE "Players"
                    SET "PersonalCash" = COALESCE((
                        SELECT "Balance"
                        FROM "BankAccounts"
                                                WHERE "PlayerId"::text = "Players"."Id"::text
                          AND "CurrencyCode" = 'EUR'
                        LIMIT 1
                    ), 0)
                    """;
            }

            return ActiveProvider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase)
                ?
                    """
                    INSERT INTO "BankAccounts" ("Id", "AccountNumber", "CurrencyCode", "Balance", "CompanyId", "IsGovernmentAccount", "CreatedAtUtc", "PlayerId")
                    SELECT p."Id",
                           printf('%016d', 9000000000000000 + ROW_NUMBER() OVER (ORDER BY p."Id")),
                           'EUR',
                           p."PersonalCash",
                           NULL,
                           0,
                           CURRENT_TIMESTAMP,
                           p."Id"
                    FROM "Players" p
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM "BankAccounts" existing
                                                WHERE existing."PlayerId"::text = p."Id"::text
                          AND existing."CurrencyCode" = 'EUR'
                    )
                    """
                :
                    """
                    DO $$
                    DECLARE
                        player_id_type TEXT;
                        bank_account_id_type TEXT;
                    BEGIN
                        SELECT pg_catalog.format_type(a.atttypid, a.atttypmod)
                        INTO player_id_type
                        FROM pg_attribute a
                        JOIN pg_class c ON c.oid = a.attrelid
                        JOIN pg_namespace n ON n.oid = c.relnamespace
                        WHERE n.nspname = 'public'
                          AND c.relname = 'Players'
                          AND a.attname = 'Id'
                          AND a.attnum > 0
                          AND NOT a.attisdropped;

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

                        IF player_id_type IS NULL OR bank_account_id_type IS NULL THEN
                            RAISE EXCEPTION 'Players.Id or BankAccounts.Id column not found during PersonalCash migration backfill';
                        END IF;

                        IF bank_account_id_type = 'uuid' AND player_id_type = 'uuid' THEN
                            EXECUTE '
                                INSERT INTO "BankAccounts" ("Id", "AccountNumber", "CurrencyCode", "Balance", "CompanyId", "IsGovernmentAccount", "CreatedAtUtc", "PlayerId")
                                SELECT p."Id",
                                       LPAD((9000000000000000 + ROW_NUMBER() OVER (ORDER BY p."Id"))::text, 16, ''0''),
                                       ''EUR'',
                                       COALESCE(
                                           CASE
                                               WHEN p."PersonalCash" IS NULL THEN NULL
                                               WHEN p."PersonalCash"::text ~ ''^\s*[-+]?\d+(\.\d+)?\s*$'' THEN (p."PersonalCash"::text)::numeric
                                               ELSE NULL
                                           END,
                                           0
                                       ),
                                       NULL,
                                       FALSE,
                                       CURRENT_TIMESTAMP,
                                       p."Id"
                                FROM "Players" p
                                WHERE NOT EXISTS (
                                    SELECT 1
                                    FROM "BankAccounts" existing
                                    WHERE existing."PlayerId"::text = p."Id"::text
                                      AND existing."CurrencyCode" = ''EUR''
                                )';
                        ELSIF bank_account_id_type = 'uuid' THEN
                            EXECUTE '
                                INSERT INTO "BankAccounts" ("Id", "AccountNumber", "CurrencyCode", "Balance", "CompanyId", "IsGovernmentAccount", "CreatedAtUtc", "PlayerId")
                                SELECT (
                                           SUBSTRING(MD5(''player-settlement-'' || p."Id"::text), 1, 8) || ''-'' ||
                                           SUBSTRING(MD5(''player-settlement-'' || p."Id"::text), 9, 4) || ''-'' ||
                                           SUBSTRING(MD5(''player-settlement-'' || p."Id"::text), 13, 4) || ''-'' ||
                                           SUBSTRING(MD5(''player-settlement-'' || p."Id"::text), 17, 4) || ''-'' ||
                                           SUBSTRING(MD5(''player-settlement-'' || p."Id"::text), 21, 12)
                                       )::uuid,
                                       LPAD((9000000000000000 + ROW_NUMBER() OVER (ORDER BY p."Id"))::text, 16, ''0''),
                                       ''EUR'',
                                       COALESCE(
                                           CASE
                                               WHEN p."PersonalCash" IS NULL THEN NULL
                                               WHEN p."PersonalCash"::text ~ ''^\s*[-+]?\d+(\.\d+)?\s*$'' THEN (p."PersonalCash"::text)::numeric
                                               ELSE NULL
                                           END,
                                           0
                                       ),
                                       NULL,
                                       FALSE,
                                       CURRENT_TIMESTAMP,
                                       p."Id"
                                FROM "Players" p
                                WHERE NOT EXISTS (
                                    SELECT 1
                                    FROM "BankAccounts" existing
                                    WHERE existing."PlayerId"::text = p."Id"::text
                                      AND existing."CurrencyCode" = ''EUR''
                                )';
                        ELSE
                            EXECUTE '
                                INSERT INTO "BankAccounts" ("Id", "AccountNumber", "CurrencyCode", "Balance", "CompanyId", "IsGovernmentAccount", "CreatedAtUtc", "PlayerId")
                                SELECT p."Id",
                                       LPAD((9000000000000000 + ROW_NUMBER() OVER (ORDER BY p."Id"))::text, 16, ''0''),
                                       ''EUR'',
                                       COALESCE(
                                           CASE
                                               WHEN p."PersonalCash" IS NULL THEN NULL
                                               WHEN p."PersonalCash"::text ~ ''^\s*[-+]?\d+(\.\d+)?\s*$'' THEN (p."PersonalCash"::text)::numeric
                                               ELSE NULL
                                           END,
                                           0
                                       ),
                                       NULL,
                                       FALSE,
                                       CURRENT_TIMESTAMP,
                                       p."Id"
                                FROM "Players" p
                                WHERE NOT EXISTS (
                                    SELECT 1
                                    FROM "BankAccounts" existing
                                    WHERE existing."PlayerId"::text = p."Id"::text
                                      AND existing."CurrencyCode" = ''EUR''
                                )';
                        END IF;
                    END $$;
                    """;
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovePlayerCurrencyBalances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql(
                    """
                    CREATE TABLE IF NOT EXISTS "PlayerCurrencyBalances" (
                        "Id" TEXT NOT NULL,
                        "PlayerId" TEXT NOT NULL,
                        "CurrencyCode" TEXT NOT NULL,
                        "Balance" TEXT NOT NULL DEFAULT '0',
                        "CreatedAtUtc" TEXT NOT NULL,
                        "UpdatedAtUtc" TEXT NOT NULL,
                        PRIMARY KEY ("Id")
                    );
                    """);
            }

            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                // Legacy databases may already have this table dropped; create an empty compatibility shell
                // so the backfill SQL below stays idempotent and startup-safe.
                migrationBuilder.Sql(
                    """
                    CREATE TABLE IF NOT EXISTS "PlayerCurrencyBalances" (
                        "Id" uuid NOT NULL,
                        "PlayerId" uuid NOT NULL,
                        "CurrencyCode" character varying(3) NOT NULL,
                        "Balance" numeric(18,4) NOT NULL DEFAULT 0,
                        "CreatedAtUtc" timestamp with time zone NOT NULL,
                        "UpdatedAtUtc" timestamp with time zone NOT NULL,
                        CONSTRAINT "PK_PlayerCurrencyBalances" PRIMARY KEY ("Id")
                    );
                    """);
            }

            migrationBuilder.Sql(GetPlayerCurrencyBalanceBackfillSql(isDown: false));
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"PlayerCurrencyBalances\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerCurrencyBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerCurrencyBalances", x => x.Id);
                    table.CheckConstraint("CK_PlayerCurrencyBalances_Balance_NonNegative", "\"Balance\" >= 0");
                    table.ForeignKey(
                        name: "FK_PlayerCurrencyBalances_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCurrencyBalances_PlayerId_CurrencyCode",
                table: "PlayerCurrencyBalances",
                columns: new[] { "PlayerId", "CurrencyCode" },
                unique: true);

                        migrationBuilder.Sql(GetPlayerCurrencyBalanceBackfillSql(isDown: true));
        }

                private string GetPlayerCurrencyBalanceBackfillSql(bool isDown)
                {
                        if (isDown)
                        {
                                return
                                        """
                                        INSERT INTO "PlayerCurrencyBalances" ("Id", "PlayerId", "CurrencyCode", "Balance", "CreatedAtUtc", "UpdatedAtUtc")
                                        SELECT ba."Id",
                                                     ba."PlayerId",
                                                     ba."CurrencyCode",
                                                 COALESCE(
                                                     CASE
                                                         WHEN ba."Balance" IS NULL THEN NULL
                                                         WHEN ba."Balance"::text ~ '^\s*[-+]?\d+(\.\d+)?\s*$' THEN (ba."Balance"::text)::numeric
                                                         ELSE NULL
                                                     END,
                                                     0
                                                 ),
                                                         COALESCE(
                                                             CASE
                                                                 WHEN ba."CreatedAtUtc" IS NULL THEN NULL
                                                                 WHEN ba."CreatedAtUtc"::text ~ '^\s*\d{4}-\d{2}-\d{2}([ T]\d{2}:\d{2}:\d{2}(\.\d+)?)?([+-]\d{2}(:?\d{2})?|Z)?\s*$' THEN (ba."CreatedAtUtc"::text)::timestamp with time zone
                                                                 ELSE NULL
                                                             END,
                                                             CURRENT_TIMESTAMP
                                                         ),
                                                     CURRENT_TIMESTAMP
                                        FROM "BankAccounts" ba
                                        WHERE ba."PlayerId" IS NOT NULL
                                            AND ba."CurrencyCode" <> 'EUR'
                                        """;
                        }

                        return ActiveProvider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase)
                                ?
                                        """
                                        UPDATE "BankAccounts"
                                        SET "Balance" = "Balance" + (
                                                SELECT legacy."Balance"
                                                FROM "PlayerCurrencyBalances" AS legacy
                                                WHERE legacy."PlayerId" = "BankAccounts"."PlayerId"
                                                    AND legacy."CurrencyCode" = "BankAccounts"."CurrencyCode"
                                        )
                                        WHERE "PlayerId" IS NOT NULL
                                            AND EXISTS (
                                                    SELECT 1
                                                    FROM "PlayerCurrencyBalances" AS legacy
                                                    WHERE legacy."PlayerId" = "BankAccounts"."PlayerId"
                                                        AND legacy."CurrencyCode" = "BankAccounts"."CurrencyCode"
                                            );

                                        INSERT INTO "BankAccounts" ("Id", "AccountNumber", "CurrencyCode", "Balance", "CompanyId", "IsGovernmentAccount", "CreatedAtUtc", "PlayerId")
                                        SELECT legacy."Id",
                                                     printf('%016d', 9100000000000000 + ROW_NUMBER() OVER (ORDER BY legacy."PlayerId", legacy."CurrencyCode")),
                                                     legacy."CurrencyCode",
                                                     legacy."Balance",
                                                     NULL,
                                                     0,
                                                     legacy."CreatedAtUtc",
                                                     legacy."PlayerId"
                                        FROM "PlayerCurrencyBalances" AS legacy
                                        WHERE NOT EXISTS (
                                                SELECT 1
                                                FROM "BankAccounts" AS existing
                                                WHERE existing."PlayerId" = legacy."PlayerId"
                                                    AND existing."CurrencyCode" = legacy."CurrencyCode"
                                        );
                                        """
                                :
                                        """
                                        UPDATE "BankAccounts" AS existing
                                        SET "Balance" =
                                            COALESCE(
                                                CASE
                                                    WHEN existing."Balance" IS NULL THEN NULL
                                                    WHEN existing."Balance"::text ~ '^\s*[-+]?\d+(\.\d+)?\s*$' THEN (existing."Balance"::text)::numeric
                                                    ELSE NULL
                                                END,
                                                0
                                            )
                                            +
                                            COALESCE(
                                                CASE
                                                    WHEN legacy."Balance" IS NULL THEN NULL
                                                    WHEN legacy."Balance"::text ~ '^\s*[-+]?\d+(\.\d+)?\s*$' THEN (legacy."Balance"::text)::numeric
                                                    ELSE NULL
                                                END,
                                                0
                                            )
                                        FROM "PlayerCurrencyBalances" AS legacy
                                        WHERE existing."PlayerId"::text = legacy."PlayerId"::text
                                            AND existing."CurrencyCode" = legacy."CurrencyCode";

                                        INSERT INTO "BankAccounts" ("Id", "AccountNumber", "CurrencyCode", "Balance", "CompanyId", "IsGovernmentAccount", "CreatedAtUtc", "PlayerId")
                                        SELECT legacy."Id",
                                                     LPAD((9100000000000000 + ROW_NUMBER() OVER (ORDER BY legacy."PlayerId", legacy."CurrencyCode"))::text, 16, '0'),
                                                     legacy."CurrencyCode",
                                                 COALESCE(
                                                     CASE
                                                         WHEN legacy."Balance" IS NULL THEN NULL
                                                         WHEN legacy."Balance"::text ~ '^\s*[-+]?\d+(\.\d+)?\s*$' THEN (legacy."Balance"::text)::numeric
                                                         ELSE NULL
                                                     END,
                                                     0
                                                 ),
                                                     NULL,
                                                     FALSE,
                                                         COALESCE(
                                                             CASE
                                                                 WHEN legacy."CreatedAtUtc" IS NULL THEN NULL
                                                                 WHEN legacy."CreatedAtUtc"::text ~ '^\s*\d{4}-\d{2}-\d{2}([ T]\d{2}:\d{2}:\d{2}(\.\d+)?)?([+-]\d{2}(:?\d{2})?|Z)?\s*$' THEN (legacy."CreatedAtUtc"::text)::timestamp with time zone
                                                                 ELSE NULL
                                                             END,
                                                             CURRENT_TIMESTAMP
                                                         ),
                                                     legacy."PlayerId"
                                        FROM "PlayerCurrencyBalances" AS legacy
                                        WHERE NOT EXISTS (
                                                SELECT 1
                                                FROM "BankAccounts" AS existing
                                            WHERE existing."PlayerId"::text = legacy."PlayerId"::text
                                                    AND existing."CurrencyCode" = legacy."CurrencyCode"
                                        );
                                        """;
                }
    }
}

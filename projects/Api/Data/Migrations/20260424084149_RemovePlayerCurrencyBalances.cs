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
            migrationBuilder.Sql(GetPlayerCurrencyBalanceBackfillSql(isDown: false));

            migrationBuilder.DropTable(
                name: "PlayerCurrencyBalances");
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
                                                     ba."Balance",
                                                     ba."CreatedAtUtc",
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
                                        SET "Balance" = existing."Balance" + legacy."Balance"
                                        FROM "PlayerCurrencyBalances" AS legacy
                                        WHERE existing."PlayerId" = legacy."PlayerId"
                                            AND existing."CurrencyCode" = legacy."CurrencyCode";

                                        INSERT INTO "BankAccounts" ("Id", "AccountNumber", "CurrencyCode", "Balance", "CompanyId", "IsGovernmentAccount", "CreatedAtUtc", "PlayerId")
                                        SELECT legacy."Id",
                                                     LPAD((9100000000000000 + ROW_NUMBER() OVER (ORDER BY legacy."PlayerId", legacy."CurrencyCode"))::text, 16, '0'),
                                                     legacy."CurrencyCode",
                                                     legacy."Balance",
                                                     NULL,
                                                     FALSE,
                                                     legacy."CreatedAtUtc",
                                                     legacy."PlayerId"
                                        FROM "PlayerCurrencyBalances" AS legacy
                                        WHERE NOT EXISTS (
                                                SELECT 1
                                                FROM "BankAccounts" AS existing
                                                WHERE existing."PlayerId" = legacy."PlayerId"
                                                    AND existing."CurrencyCode" = legacy."CurrencyCode"
                                        );
                                        """;
                }
    }
}

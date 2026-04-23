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
                        WHERE "PlayerId" = "Players"."Id"
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
                        WHERE existing."PlayerId" = p."Id"
                          AND existing."CurrencyCode" = 'EUR'
                    )
                    """
                :
                    """
                    INSERT INTO "BankAccounts" ("Id", "AccountNumber", "CurrencyCode", "Balance", "CompanyId", "IsGovernmentAccount", "CreatedAtUtc", "PlayerId")
                    SELECT p."Id",
                           LPAD((9000000000000000 + ROW_NUMBER() OVER (ORDER BY p."Id"))::text, 16, '0'),
                           'EUR',
                           p."PersonalCash",
                           NULL,
                           FALSE,
                           CURRENT_TIMESTAMP,
                           p."Id"
                    FROM "Players" p
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM "BankAccounts" existing
                        WHERE existing."PlayerId" = p."Id"
                          AND existing."CurrencyCode" = 'EUR'
                    )
                    """;
        }
    }
}

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
                               COALESCE(account."DepositInterestRatePercent", 0),
                               account."IsBaseCapitalDeposit",
                               CASE WHEN account."ClosedAtUtc" IS NULL THEN 1 ELSE 0 END,
                               COALESCE(account."DepositedAtTick", 0),
                               account."CreatedAtUtc",
                               account."ClosedAtTick",
                               account."ClosedAtUtc",
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
                               account."Balance",
                               COALESCE(account."DepositInterestRatePercent", 0),
                               account."IsBaseCapitalDeposit",
                               account."ClosedAtUtc" IS NULL,
                               COALESCE(account."DepositedAtTick", 0),
                               account."CreatedAtUtc",
                               account."ClosedAtTick",
                               account."ClosedAtUtc",
                               account."TotalInterestPaid"
                        FROM "BankAccounts" AS account
                        WHERE account."BankBuildingId" IS NOT NULL
                          AND account."CompanyId" IS NOT NULL
                          AND NOT EXISTS (
                              SELECT 1
                              FROM "BankDeposits" AS legacy
                              WHERE legacy."Id" = account."Id"
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
                    SELECT legacy."Id",
                           LPAD((9200000000000000 + ROW_NUMBER() OVER (ORDER BY legacy."Id"))::text, 16, '0'),
                           city."CurrencyCode",
                           legacy."Amount",
                           legacy."DepositorCompanyId",
                           FALSE,
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
                    """;
        }
    }
}

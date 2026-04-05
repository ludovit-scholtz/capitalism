using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStockExchangeAccountsAndDividends : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActiveAccountType",
                table: "Players",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ActiveCompanyId",
                table: "Players",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PersonalCash",
                table: "Players",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DividendPayoutRatio",
                table: "Companies",
                type: "TEXT",
                precision: 8,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalSharesIssued",
                table: "Companies",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "DividendPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipientPlayerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RecipientCompanyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ShareCount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    AmountPerShare = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    GameYear = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordedAtTick = table.Column<long>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DividendPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DividendPayments_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DividendPayments_Companies_RecipientCompanyId",
                        column: x => x.RecipientCompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DividendPayments_Players_RecipientPlayerId",
                        column: x => x.RecipientPlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Shareholdings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerPlayerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OwnerCompanyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ShareCount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shareholdings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shareholdings_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Shareholdings_Companies_OwnerCompanyId",
                        column: x => x.OwnerCompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Shareholdings_Players_OwnerPlayerId",
                        column: x => x.OwnerPlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DividendPayments_CompanyId_GameYear",
                table: "DividendPayments",
                columns: new[] { "CompanyId", "GameYear" });

            migrationBuilder.CreateIndex(
                name: "IX_DividendPayments_RecipientCompanyId",
                table: "DividendPayments",
                column: "RecipientCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_DividendPayments_RecipientPlayerId_RecordedAtTick",
                table: "DividendPayments",
                columns: new[] { "RecipientPlayerId", "RecordedAtTick" });

            migrationBuilder.CreateIndex(
                name: "IX_Shareholdings_CompanyId_OwnerCompanyId",
                table: "Shareholdings",
                columns: new[] { "CompanyId", "OwnerCompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_Shareholdings_CompanyId_OwnerPlayerId",
                table: "Shareholdings",
                columns: new[] { "CompanyId", "OwnerPlayerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Shareholdings_OwnerCompanyId",
                table: "Shareholdings",
                column: "OwnerCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Shareholdings_OwnerPlayerId",
                table: "Shareholdings",
                column: "OwnerPlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DividendPayments");

            migrationBuilder.DropTable(
                name: "Shareholdings");

            migrationBuilder.DropColumn(
                name: "ActiveAccountType",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "ActiveCompanyId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "PersonalCash",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "DividendPayoutRatio",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "TotalSharesIssued",
                table: "Companies");
        }
    }
}

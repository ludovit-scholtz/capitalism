using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBankDeposits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var boolType = ActiveProvider.Contains("Npgsql", System.StringComparison.OrdinalIgnoreCase)
                ? "boolean"
                : "INTEGER";

            migrationBuilder.AddColumn<bool>(
                name: "BaseCapitalDeposited",
                table: "Buildings",
                type: boolType,
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "DepositInterestRatePercent",
                table: "Buildings",
                type: "TEXT",
                precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LendingInterestRatePercent",
                table: "Buildings",
                type: "TEXT",
                precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalDeposits",
                table: "Buildings",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "BankDeposits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BankBuildingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DepositorCompanyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DepositInterestRatePercent = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    IsBaseCapital = table.Column<bool>(type: boolType, nullable: false),
                    IsActive = table.Column<bool>(type: boolType, nullable: false),
                    DepositedAtTick = table.Column<long>(type: "INTEGER", nullable: false),
                    DepositedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    WithdrawnAtTick = table.Column<long>(type: "INTEGER", nullable: true),
                    WithdrawnAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TotalInterestPaid = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false)
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
                name: "IX_BankDeposits_BankBuildingId_IsActive",
                table: "BankDeposits",
                columns: new[] { "BankBuildingId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_BankDeposits_DepositorCompanyId_IsActive",
                table: "BankDeposits",
                columns: new[] { "DepositorCompanyId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankDeposits");

            migrationBuilder.DropColumn(
                name: "BaseCapitalDeposited",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "DepositInterestRatePercent",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "LendingInterestRatePercent",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "TotalDeposits",
                table: "Buildings");
        }
    }
}

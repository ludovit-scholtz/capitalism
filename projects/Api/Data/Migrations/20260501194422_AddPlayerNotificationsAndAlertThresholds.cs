using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerNotificationsAndAlertThresholds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DueSoonAlertForPaymentTick",
                table: "Loans",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLowInventoryAlertActive",
                table: "BuildingUnits",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LowInventoryAlertThreshold",
                table: "BuildingUnits",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AlertMinBalanceThreshold",
                table: "BankAccounts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLowBalanceAlertActive",
                table: "BankAccounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PlayerNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtTick = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: true),
                    BuildingUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    LoanId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerNotifications_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Loans_DueSoonAlertForPaymentTick",
                table: "Loans",
                column: "DueSoonAlertForPaymentTick");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerNotifications_CreatedAtUtc",
                table: "PlayerNotifications",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerNotifications_PlayerId_IsRead_CreatedAtTick",
                table: "PlayerNotifications",
                columns: new[] { "PlayerId", "IsRead", "CreatedAtTick" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerNotifications");

            migrationBuilder.DropIndex(
                name: "IX_Loans_DueSoonAlertForPaymentTick",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "DueSoonAlertForPaymentTick",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "IsLowInventoryAlertActive",
                table: "BuildingUnits");

            migrationBuilder.DropColumn(
                name: "LowInventoryAlertThreshold",
                table: "BuildingUnits");

            migrationBuilder.DropColumn(
                name: "AlertMinBalanceThreshold",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "IsLowBalanceAlertActive",
                table: "BankAccounts");
        }
    }
}

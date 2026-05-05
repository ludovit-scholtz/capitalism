using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBankDepositRateHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PendingDepositInterestRatePercent",
                table: "Buildings",
                type: "numeric(8,4)",
                precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PendingDepositRateEffectiveTick",
                table: "Buildings",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BankDepositRateHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BankBuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousRatePercent = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    NewRatePercent = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    EffectiveTick = table.Column<long>(type: "bigint", nullable: false),
                    EffectiveUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledAtTick = table.Column<long>(type: "bigint", nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AffectedDepositCount = table.Column<int>(type: "integer", nullable: false),
                    ChangedByPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsApplied = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankDepositRateHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankDepositRateHistories_Buildings_BankBuildingId",
                        column: x => x.BankBuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankDepositRateHistories_Players_ChangedByPlayerId",
                        column: x => x.ChangedByPlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankDepositRateHistories_BankBuildingId",
                table: "BankDepositRateHistories",
                column: "BankBuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_BankDepositRateHistories_BankBuildingId_IsApplied",
                table: "BankDepositRateHistories",
                columns: new[] { "BankBuildingId", "IsApplied" });

            migrationBuilder.CreateIndex(
                name: "IX_BankDepositRateHistories_ChangedByPlayerId",
                table: "BankDepositRateHistories",
                column: "ChangedByPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_BankDepositRateHistories_EffectiveTick",
                table: "BankDepositRateHistories",
                column: "EffectiveTick");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankDepositRateHistories");

            migrationBuilder.DropColumn(
                name: "PendingDepositInterestRatePercent",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "PendingDepositRateEffectiveTick",
                table: "Buildings");
        }
    }
}

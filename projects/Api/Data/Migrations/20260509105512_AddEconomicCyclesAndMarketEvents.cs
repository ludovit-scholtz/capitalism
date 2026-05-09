using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEconomicCyclesAndMarketEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EconomicCycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Phase = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    PhaseStartedTick = table.Column<long>(type: "bigint", nullable: false),
                    ExpectedDurationTicks = table.Column<int>(type: "integer", nullable: false),
                    IntensityFactor = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    RecessionWarningSentForTick = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EconomicCycles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AffectedResourceTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    AffectedCityId = table.Column<Guid>(type: "uuid", nullable: true),
                    MagnitudeMultiplier = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    StartsAtTick = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAtTick = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketEvents_Cities_AffectedCityId",
                        column: x => x.AffectedCityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MarketEvents_ResourceTypes_AffectedResourceTypeId",
                        column: x => x.AffectedResourceTypeId,
                        principalTable: "ResourceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EconomicCycles_PhaseStartedTick",
                table: "EconomicCycles",
                column: "PhaseStartedTick");

            migrationBuilder.CreateIndex(
                name: "IX_MarketEvents_AffectedCityId_ExpiresAtTick",
                table: "MarketEvents",
                columns: new[] { "AffectedCityId", "ExpiresAtTick" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketEvents_AffectedResourceTypeId_ExpiresAtTick",
                table: "MarketEvents",
                columns: new[] { "AffectedResourceTypeId", "ExpiresAtTick" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketEvents_EventType_StartsAtTick_ExpiresAtTick",
                table: "MarketEvents",
                columns: new[] { "EventType", "StartsAtTick", "ExpiresAtTick" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EconomicCycles");

            migrationBuilder.DropTable(
                name: "MarketEvents");
        }
    }
}

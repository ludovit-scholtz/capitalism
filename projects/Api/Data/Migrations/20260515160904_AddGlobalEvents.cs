using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GlobalEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    StartTick = table.Column<long>(type: "bigint", nullable: false),
                    DurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    AffectedCityId = table.Column<Guid>(type: "uuid", nullable: true),
                    OperatingCostMultiplier = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    TradeRouteMultiplier = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    RdMultiplier = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    MineEfficiencyMultiplier = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TriggeredByAdminId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GlobalEvents_Cities_AffectedCityId",
                        column: x => x.AffectedCityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GlobalEvents_AffectedCityId",
                table: "GlobalEvents",
                column: "AffectedCityId");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalEvents_EventType_StartTick",
                table: "GlobalEvents",
                columns: new[] { "EventType", "StartTick" });

            migrationBuilder.CreateIndex(
                name: "IX_GlobalEvents_IsActive_StartTick",
                table: "GlobalEvents",
                columns: new[] { "IsActive", "StartTick" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GlobalEvents");
        }
    }
}

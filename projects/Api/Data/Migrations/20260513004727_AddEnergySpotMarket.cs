using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEnergySpotMarket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MaxEnergyBidPrice",
                table: "Buildings",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EnergyListings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CityId = table.Column<Guid>(type: "uuid", nullable: false),
                    PricePerKwhLocal = table.Column<decimal>(type: "numeric", nullable: false),
                    CapacityKw = table.Column<decimal>(type: "numeric", nullable: false),
                    AvailableKw = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtTick = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnergyListings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnergyListings_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnergyListings_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnergyListings_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnergyListings_BuildingId",
                table: "EnergyListings",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_EnergyListings_CityId",
                table: "EnergyListings",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_EnergyListings_CompanyId",
                table: "EnergyListings",
                column: "CompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnergyListings");

            migrationBuilder.DropColumn(
                name: "MaxEnergyBidPrice",
                table: "Buildings");
        }
    }
}

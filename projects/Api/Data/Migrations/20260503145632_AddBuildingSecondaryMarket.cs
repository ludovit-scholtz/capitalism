using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildingSecondaryMarket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BuildingSaleOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferedPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NegotiationNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildingSaleOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildingSaleOffers_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BuildingSaleOffers_Companies_BuyerCompanyId",
                        column: x => x.BuyerCompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BuildingSaleOffers_Players_BuyerPlayerId",
                        column: x => x.BuyerPlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuildingSaleOffers_BuildingId",
                table: "BuildingSaleOffers",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingSaleOffers_BuildingId_Status",
                table: "BuildingSaleOffers",
                columns: new[] { "BuildingId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BuildingSaleOffers_BuyerCompanyId",
                table: "BuildingSaleOffers",
                column: "BuyerCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingSaleOffers_BuyerPlayerId",
                table: "BuildingSaleOffers",
                column: "BuyerPlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BuildingSaleOffers");
        }
    }
}

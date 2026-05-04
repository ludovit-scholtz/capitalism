using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInterCityTradeRoutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterCityTradeRoutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceBuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceBuildingUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationBuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationBuildingUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    Quality = table.Column<decimal>(type: "numeric", nullable: false),
                    SourcingCostTotal = table.Column<decimal>(type: "numeric", nullable: false),
                    PricePerUnit = table.Column<decimal>(type: "numeric", nullable: false),
                    ScheduledDepartureTick = table.Column<long>(type: "bigint", nullable: false),
                    ExpectedArrivalTick = table.Column<long>(type: "bigint", nullable: false),
                    TransitTicks = table.Column<long>(type: "bigint", nullable: false),
                    ShippingCostEstimate = table.Column<decimal>(type: "numeric", nullable: false),
                    ShippingCostActual = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DepartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterCityTradeRoutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterCityTradeRoutes_BuildingUnits_DestinationBuildingUnitId",
                        column: x => x.DestinationBuildingUnitId,
                        principalTable: "BuildingUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InterCityTradeRoutes_BuildingUnits_SourceBuildingUnitId",
                        column: x => x.SourceBuildingUnitId,
                        principalTable: "BuildingUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InterCityTradeRoutes_Buildings_DestinationBuildingId",
                        column: x => x.DestinationBuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InterCityTradeRoutes_Buildings_SourceBuildingId",
                        column: x => x.SourceBuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InterCityTradeRoutes_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InterCityTradeRoutes_ProductTypes_ProductTypeId",
                        column: x => x.ProductTypeId,
                        principalTable: "ProductTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InterCityTradeRoutes_ResourceTypes_ResourceTypeId",
                        column: x => x.ResourceTypeId,
                        principalTable: "ResourceTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterCityTradeRoutes_CompanyId",
                table: "InterCityTradeRoutes",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_InterCityTradeRoutes_DestinationBuildingId",
                table: "InterCityTradeRoutes",
                column: "DestinationBuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_InterCityTradeRoutes_DestinationBuildingUnitId",
                table: "InterCityTradeRoutes",
                column: "DestinationBuildingUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_InterCityTradeRoutes_ProductTypeId",
                table: "InterCityTradeRoutes",
                column: "ProductTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_InterCityTradeRoutes_ResourceTypeId",
                table: "InterCityTradeRoutes",
                column: "ResourceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_InterCityTradeRoutes_SourceBuildingId",
                table: "InterCityTradeRoutes",
                column: "SourceBuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_InterCityTradeRoutes_SourceBuildingUnitId",
                table: "InterCityTradeRoutes",
                column: "SourceBuildingUnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterCityTradeRoutes");
        }
    }
}

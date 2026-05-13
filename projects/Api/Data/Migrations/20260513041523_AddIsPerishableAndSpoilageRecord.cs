using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsPerishableAndSpoilageRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPerishable",
                table: "ProductTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "InventorySpoilageRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantitySpoiled = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    QualityAtSpoilage = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    EstimatedLossValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RecordedAtTick = table.Column<long>(type: "bigint", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventorySpoilageRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventorySpoilageRecords_BuildingUnits_BuildingUnitId",
                        column: x => x.BuildingUnitId,
                        principalTable: "BuildingUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InventorySpoilageRecords_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventorySpoilageRecords_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventorySpoilageRecords_ProductTypes_ProductTypeId",
                        column: x => x.ProductTypeId,
                        principalTable: "ProductTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventorySpoilageRecords_BuildingId_RecordedAtTick",
                table: "InventorySpoilageRecords",
                columns: new[] { "BuildingId", "RecordedAtTick" });

            migrationBuilder.CreateIndex(
                name: "IX_InventorySpoilageRecords_BuildingUnitId",
                table: "InventorySpoilageRecords",
                column: "BuildingUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySpoilageRecords_CompanyId_RecordedAtTick",
                table: "InventorySpoilageRecords",
                columns: new[] { "CompanyId", "RecordedAtTick" });

            migrationBuilder.CreateIndex(
                name: "IX_InventorySpoilageRecords_ProductTypeId",
                table: "InventorySpoilageRecords",
                column: "ProductTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventorySpoilageRecords");

            migrationBuilder.DropColumn(
                name: "IsPerishable",
                table: "ProductTypes");
        }
    }
}

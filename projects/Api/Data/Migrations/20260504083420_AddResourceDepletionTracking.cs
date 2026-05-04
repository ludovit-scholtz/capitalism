using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceDepletionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OriginalMaterialQuantity",
                table: "BuildingLots",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MineDepletionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceTypeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OriginalQuantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DepletedAtTick = table.Column<long>(type: "bigint", nullable: false),
                    DepletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MineDepletionRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourceReplenishmentSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CityId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastReplenishmentTick = table.Column<long>(type: "bigint", nullable: false),
                    NextReplenishmentTick = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceReplenishmentSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceReplenishmentSchedules_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MineDepletionRecords_CompanyId",
                table: "MineDepletionRecords",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_MineDepletionRecords_DepletedAtTick",
                table: "MineDepletionRecords",
                column: "DepletedAtTick");

            migrationBuilder.CreateIndex(
                name: "IX_MineDepletionRecords_LotId",
                table: "MineDepletionRecords",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceReplenishmentSchedules_CityId",
                table: "ResourceReplenishmentSchedules",
                column: "CityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceReplenishmentSchedules_NextReplenishmentTick",
                table: "ResourceReplenishmentSchedules",
                column: "NextReplenishmentTick");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MineDepletionRecords");

            migrationBuilder.DropTable(
                name: "ResourceReplenishmentSchedules");

            migrationBuilder.DropColumn(
                name: "OriginalMaterialQuantity",
                table: "BuildingLots");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaHouseUnitCampaignPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdvertisingActive",
                table: "Buildings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "MediaHouseUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CampaignBudgetPerTick = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BrandQualityBoostPerTick = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LaborCostPerTick = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EnergyCostPerTick = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaHouseUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaHouseUnits_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaHouseUnits_Companies_TargetCompanyId",
                        column: x => x.TargetCompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BrandQualityRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaHouseUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAtTick = table.Column<long>(type: "bigint", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BoostApplied = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    CampaignBudgetSpent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LaborCostSpent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EnergyCostSpent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandQualityRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrandQualityRecords_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BrandQualityRecords_Companies_TargetCompanyId",
                        column: x => x.TargetCompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BrandQualityRecords_MediaHouseUnits_MediaHouseUnitId",
                        column: x => x.MediaHouseUnitId,
                        principalTable: "MediaHouseUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrandQualityRecords_BuildingId",
                table: "BrandQualityRecords",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_BrandQualityRecords_BuildingId_RecordedAtTick",
                table: "BrandQualityRecords",
                columns: new[] { "BuildingId", "RecordedAtTick" });

            migrationBuilder.CreateIndex(
                name: "IX_BrandQualityRecords_MediaHouseUnitId",
                table: "BrandQualityRecords",
                column: "MediaHouseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_BrandQualityRecords_TargetCompanyId",
                table: "BrandQualityRecords",
                column: "TargetCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaHouseUnits_BuildingId",
                table: "MediaHouseUnits",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaHouseUnits_TargetCompanyId",
                table: "MediaHouseUnits",
                column: "TargetCompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrandQualityRecords");

            migrationBuilder.DropTable(
                name: "MediaHouseUnits");

            migrationBuilder.DropColumn(
                name: "IsAdvertisingActive",
                table: "Buildings");
        }
    }
}

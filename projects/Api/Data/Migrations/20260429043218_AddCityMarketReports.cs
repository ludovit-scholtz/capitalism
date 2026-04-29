using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCityMarketReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CityMarketReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TickFrom = table.Column<long>(type: "bigint", nullable: false),
                    TickTo = table.Column<long>(type: "bigint", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MasterNewsEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReportDataJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CityMarketReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CityMarketReports_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CityMarketReports_CityId_ReportType_TickFrom",
                table: "CityMarketReports",
                columns: new[] { "CityId", "ReportType", "TickFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CityMarketReports_GeneratedAtUtc",
                table: "CityMarketReports",
                column: "GeneratedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CityMarketReports");
        }
    }
}

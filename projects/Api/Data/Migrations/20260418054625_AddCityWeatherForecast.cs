using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCityWeatherForecast : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CityWeatherForecasts",
                columns: table => new
                {
                    CityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tick = table.Column<long>(type: "bigint", nullable: false),
                    WindPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    SolarPercent = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CityWeatherForecasts", x => new { x.CityId, x.Tick });
                    table.ForeignKey(
                        name: "FK_CityWeatherForecasts_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CityWeatherForecasts_CityId_Tick",
                table: "CityWeatherForecasts",
                columns: new[] { "CityId", "Tick" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CityWeatherForecasts");
        }
    }
}

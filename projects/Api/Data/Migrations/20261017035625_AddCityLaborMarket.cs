using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCityLaborMarket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CityLaborMarkets",
                columns: table => new
                {
                    CityId = table.Column<Guid>(type: "uuid", nullable: false),
                    LaborPool = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    LaborDemand = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Utilization = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    UnemploymentRate = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    WageIndex = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    StaffingFillRate = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    AverageSalaryMultiplier = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    UpdatedAtTick = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CityLaborMarkets", x => x.CityId);
                    table.ForeignKey(
                        name: "FK_CityLaborMarkets_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CityLaborMarkets");
        }
    }
}

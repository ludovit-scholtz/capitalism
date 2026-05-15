using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCityUnlockRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CityUnlockRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CityId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequiredNetWorthUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CityUnlockRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CityUnlockRequirements_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyCityUnlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnlockedAtTick = table.Column<long>(type: "bigint", nullable: false),
                    UnlockedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyCityUnlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyCityUnlocks_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyCityUnlocks_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CityUnlockRequirements_CityId",
                table: "CityUnlockRequirements",
                column: "CityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCityUnlocks_CityId",
                table: "CompanyCityUnlocks",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCityUnlocks_CompanyId_CityId",
                table: "CompanyCityUnlocks",
                columns: new[] { "CompanyId", "CityId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CityUnlockRequirements");

            migrationBuilder.DropTable(
                name: "CompanyCityUnlocks");
        }
    }
}

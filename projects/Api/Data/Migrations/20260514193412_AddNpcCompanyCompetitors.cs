using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNpcCompanyCompetitors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NpcCompanies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeCityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Archetype = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DifficultyLevel = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcCompanies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcCompanies_Cities_HomeCityId",
                        column: x => x.HomeCityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NpcCompanies_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcDecisionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tick = table.Column<long>(type: "bigint", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcDecisionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcDecisionLogs_NpcCompanies_NpcCompanyId",
                        column: x => x.NpcCompanyId,
                        principalTable: "NpcCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NpcCompanies_CompanyId",
                table: "NpcCompanies",
                column: "CompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NpcCompanies_HomeCityId_IsActive",
                table: "NpcCompanies",
                columns: new[] { "HomeCityId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_NpcDecisionLogs_NpcCompanyId_Tick",
                table: "NpcDecisionLogs",
                columns: new[] { "NpcCompanyId", "Tick" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NpcDecisionLogs");

            migrationBuilder.DropTable(
                name: "NpcCompanies");
        }
    }
}

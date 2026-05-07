using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralProgramEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReferralCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatorPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsageCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferralCodes_Players_CreatorPlayerId",
                        column: x => x.CreatorPlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReferralRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferralCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferredPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisteredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferralRegistrations_Players_ReferredPlayerId",
                        column: x => x.ReferredPlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReferralRegistrations_ReferralCodes_ReferralCodeId",
                        column: x => x.ReferralCodeId,
                        principalTable: "ReferralCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReferralCodes_Code",
                table: "ReferralCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferralCodes_CreatorPlayerId",
                table: "ReferralCodes",
                column: "CreatorPlayerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferralRegistrations_ReferralCodeId_ReferredPlayerId",
                table: "ReferralRegistrations",
                columns: new[] { "ReferralCodeId", "ReferredPlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferralRegistrations_ReferredPlayerId",
                table: "ReferralRegistrations",
                column: "ReferredPlayerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReferralRegistrations");

            migrationBuilder.DropTable(
                name: "ReferralCodes");
        }
    }
}

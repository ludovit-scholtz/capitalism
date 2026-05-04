using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerBadgesAndRankSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerAchievementBadges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BadgeType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    UnlockedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UnlockedAtTick = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerAchievementBadges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerAchievementBadges_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerRankSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotTick = table.Column<long>(type: "bigint", nullable: false),
                    SnapshotUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeaderboardRank = table.Column<int>(type: "integer", nullable: false),
                    WealthUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PercentileRank = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    PositionChange = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerRankSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerRankSnapshots_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAchievementBadges_PlayerId",
                table: "PlayerAchievementBadges",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAchievementBadges_PlayerId_BadgeType",
                table: "PlayerAchievementBadges",
                columns: new[] { "PlayerId", "BadgeType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerRankSnapshots_PlayerId",
                table: "PlayerRankSnapshots",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerRankSnapshots_PlayerId_SnapshotTick",
                table: "PlayerRankSnapshots",
                columns: new[] { "PlayerId", "SnapshotTick" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerAchievementBadges");

            migrationBuilder.DropTable(
                name: "PlayerRankSnapshots");
        }
    }
}

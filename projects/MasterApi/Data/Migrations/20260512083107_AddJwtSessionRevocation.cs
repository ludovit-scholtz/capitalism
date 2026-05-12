using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MasterApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJwtSessionRevocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SessionRevokedBeforeUtc",
                table: "PlayerAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MasterPlayerSessions",
                columns: table => new
                {
                    Jti = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PlayerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenIpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedReason = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterPlayerSessions", x => x.Jti);
                    table.ForeignKey(
                        name: "FK_MasterPlayerSessions_PlayerAccounts_PlayerAccountId",
                        column: x => x.PlayerAccountId,
                        principalTable: "PlayerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MasterRevokedTokens",
                columns: table => new
                {
                    Jti = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PlayerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterRevokedTokens", x => x.Jti);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MasterPlayerSessions_ExpiresAtUtc",
                table: "MasterPlayerSessions",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MasterPlayerSessions_PlayerAccountId_LastSeenAtUtc",
                table: "MasterPlayerSessions",
                columns: new[] { "PlayerAccountId", "LastSeenAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MasterRevokedTokens_ExpiresAtUtc",
                table: "MasterRevokedTokens",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MasterRevokedTokens_PlayerAccountId_RevokedAtUtc",
                table: "MasterRevokedTokens",
                columns: new[] { "PlayerAccountId", "RevokedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MasterPlayerSessions");

            migrationBuilder.DropTable(
                name: "MasterRevokedTokens");

            migrationBuilder.DropColumn(
                name: "SessionRevokedBeforeUtc",
                table: "PlayerAccounts");
        }
    }
}

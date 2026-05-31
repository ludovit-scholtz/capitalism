using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MasterApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscordAccountLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscordLinkCode",
                table: "PlayerAccounts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DiscordLinkCodeExpiresAtUtc",
                table: "PlayerAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordUserId",
                table: "PlayerAccounts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordUsername",
                table: "PlayerAccounts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAccounts_DiscordUserId",
                table: "PlayerAccounts",
                column: "DiscordUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerAccounts_DiscordUserId",
                table: "PlayerAccounts");

            migrationBuilder.DropColumn(
                name: "DiscordLinkCode",
                table: "PlayerAccounts");

            migrationBuilder.DropColumn(
                name: "DiscordLinkCodeExpiresAtUtc",
                table: "PlayerAccounts");

            migrationBuilder.DropColumn(
                name: "DiscordUserId",
                table: "PlayerAccounts");

            migrationBuilder.DropColumn(
                name: "DiscordUsername",
                table: "PlayerAccounts");
        }
    }
}

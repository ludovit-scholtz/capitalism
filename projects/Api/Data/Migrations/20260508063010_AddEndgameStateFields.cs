using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEndgameStateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "GameEnded",
                table: "GameStates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "GameEndedAtUtc",
                table: "GameStates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GameStartedAtUtc",
                table: "GameStates",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "WinnerCompanyName",
                table: "GameStates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WinnerDisplayName",
                table: "GameStates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WinnerPlayerId",
                table: "GameStates",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GameEnded",
                table: "GameStates");

            migrationBuilder.DropColumn(
                name: "GameEndedAtUtc",
                table: "GameStates");

            migrationBuilder.DropColumn(
                name: "GameStartedAtUtc",
                table: "GameStates");

            migrationBuilder.DropColumn(
                name: "WinnerCompanyName",
                table: "GameStates");

            migrationBuilder.DropColumn(
                name: "WinnerDisplayName",
                table: "GameStates");

            migrationBuilder.DropColumn(
                name: "WinnerPlayerId",
                table: "GameStates");
        }
    }
}

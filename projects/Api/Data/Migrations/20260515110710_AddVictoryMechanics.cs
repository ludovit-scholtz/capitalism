using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVictoryMechanics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ShardState",
                table: "GameStates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "WinnerNetWorth",
                table: "GameStates",
                type: "numeric(28,2)",
                precision: 28,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VictoryNewsletters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WinnerPlayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    WinnerDisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WinnerCompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WinnerNetWorthUsd = table.Column<decimal>(type: "numeric(28,2)", precision: 28, scale: 2, nullable: false),
                    Top10RankingsJson = table.Column<string>(type: "text", nullable: false),
                    TotalFxTradeCount = table.Column<int>(type: "integer", nullable: false),
                    TotalFxVolumeUsd = table.Column<decimal>(type: "numeric(28,2)", precision: 28, scale: 2, nullable: false),
                    TotalProductsSold = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ActiveCitiesCount = table.Column<int>(type: "integer", nullable: false),
                    GameDurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VictoryNewsletters", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VictoryNewsletters");

            migrationBuilder.DropColumn(
                name: "ShardState",
                table: "GameStates");

            migrationBuilder.DropColumn(
                name: "WinnerNetWorth",
                table: "GameStates");
        }
    }
}

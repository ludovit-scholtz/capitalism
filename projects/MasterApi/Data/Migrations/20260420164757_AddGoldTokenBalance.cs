using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MasterApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGoldTokenBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GoldTokenBalance",
                table: "PlayerAccounts",
                type: "numeric(18,8)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "GoldTokenTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    BalanceBefore = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    AdminEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldTokenTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoldTokenTransactions_PlayerAccounts_PlayerAccountId",
                        column: x => x.PlayerAccountId,
                        principalTable: "PlayerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoldTokenTransactions_CreatedAtUtc",
                table: "GoldTokenTransactions",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_GoldTokenTransactions_PlayerAccountId",
                table: "GoldTokenTransactions",
                column: "PlayerAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoldTokenTransactions");

            migrationBuilder.DropColumn(
                name: "GoldTokenBalance",
                table: "PlayerAccounts");
        }
    }
}

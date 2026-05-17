using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MasterApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGoldTransferRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoldTokenDepositRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Network = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AssetId = table.Column<long>(type: "bigint", nullable: false),
                    DepositAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SenderAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedByEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AdminNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldTokenDepositRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoldTokenDepositRequests_PlayerAccounts_PlayerAccountId",
                        column: x => x.PlayerAccountId,
                        principalTable: "PlayerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GoldTokenWithdrawalRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Network = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AssetId = table.Column<long>(type: "bigint", nullable: false),
                    DestinationAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedByEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AdminNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldTokenWithdrawalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoldTokenWithdrawalRequests_PlayerAccounts_PlayerAccountId",
                        column: x => x.PlayerAccountId,
                        principalTable: "PlayerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoldTokenDepositRequests_PlayerAccountId_RequestedAtUtc",
                table: "GoldTokenDepositRequests",
                columns: new[] { "PlayerAccountId", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GoldTokenDepositRequests_Status_RequestedAtUtc",
                table: "GoldTokenDepositRequests",
                columns: new[] { "Status", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GoldTokenWithdrawalRequests_PlayerAccountId_RequestedAtUtc",
                table: "GoldTokenWithdrawalRequests",
                columns: new[] { "PlayerAccountId", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GoldTokenWithdrawalRequests_Status_RequestedAtUtc",
                table: "GoldTokenWithdrawalRequests",
                columns: new[] { "Status", "RequestedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoldTokenDepositRequests");

            migrationBuilder.DropTable(
                name: "GoldTokenWithdrawalRequests");
        }
    }
}

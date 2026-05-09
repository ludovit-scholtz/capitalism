using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStockLimitOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LimitOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockSymbol = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Side = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    LimitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    FilledQuantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OwnerPlayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    SettlementBankAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservedCashRemaining = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAtTick = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtTick = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LimitOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LimitOrders_BankAccounts_SettlementBankAccountId",
                        column: x => x.SettlementBankAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LimitOrders_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LimitOrders_Companies_OwnerCompanyId",
                        column: x => x.OwnerCompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LimitOrders_Players_OwnerPlayerId",
                        column: x => x.OwnerPlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LimitOrderExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockSymbol = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BuyOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ExecutedAtTick = table.Column<long>(type: "bigint", nullable: false),
                    ExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LimitOrderExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LimitOrderExecutions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LimitOrderExecutions_LimitOrders_BuyOrderId",
                        column: x => x.BuyOrderId,
                        principalTable: "LimitOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LimitOrderExecutions_LimitOrders_SellOrderId",
                        column: x => x.SellOrderId,
                        principalTable: "LimitOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LimitOrderExecutions_BuyOrderId",
                table: "LimitOrderExecutions",
                column: "BuyOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LimitOrderExecutions_CompanyId_ExecutedAtTick",
                table: "LimitOrderExecutions",
                columns: new[] { "CompanyId", "ExecutedAtTick" });

            migrationBuilder.CreateIndex(
                name: "IX_LimitOrderExecutions_SellOrderId",
                table: "LimitOrderExecutions",
                column: "SellOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LimitOrderExecutions_StockSymbol_ExecutedAtTick",
                table: "LimitOrderExecutions",
                columns: new[] { "StockSymbol", "ExecutedAtTick" });

            migrationBuilder.CreateIndex(
                name: "IX_LimitOrders_CompanyId_Status_Side_LimitPrice_CreatedAtTick",
                table: "LimitOrders",
                columns: new[] { "CompanyId", "Status", "Side", "LimitPrice", "CreatedAtTick" });

            migrationBuilder.CreateIndex(
                name: "IX_LimitOrders_OwnerCompanyId_Status",
                table: "LimitOrders",
                columns: new[] { "OwnerCompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LimitOrders_OwnerPlayerId_Status",
                table: "LimitOrders",
                columns: new[] { "OwnerPlayerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LimitOrders_SettlementBankAccountId",
                table: "LimitOrders",
                column: "SettlementBankAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LimitOrderExecutions");

            migrationBuilder.DropTable(
                name: "LimitOrders");
        }
    }
}

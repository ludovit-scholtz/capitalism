using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGoldAmmPools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoldAmmPools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    FiatReserve = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GoldReserve = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    TotalLiquidityShares = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldAmmPools", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GoldAmmPositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LiquidityShares = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    FiatProvided = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GoldProvided = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldAmmPositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoldAmmPositions_GoldAmmPools_PoolId",
                        column: x => x.PoolId,
                        principalTable: "GoldAmmPools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoldAmmPositions_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GoldAmmTradeRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    InputAmount = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    OutputAmount = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    FeeAmount = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    ImpliedPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ExecutedAtTick = table.Column<long>(type: "bigint", nullable: false),
                    ExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldAmmTradeRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoldAmmTradeRecords_GoldAmmPools_PoolId",
                        column: x => x.PoolId,
                        principalTable: "GoldAmmPools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoldAmmTradeRecords_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerGoldBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerGoldBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerGoldBalances_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.CheckConstraint("CK_PlayerGoldBalances_Balance_NonNegative", "\"Balance\" >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoldAmmPools_CurrencyCode",
                table: "GoldAmmPools",
                column: "CurrencyCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoldAmmPositions_PoolId_PlayerId",
                table: "GoldAmmPositions",
                columns: new[] { "PoolId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoldAmmPositions_PlayerId",
                table: "GoldAmmPositions",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_GoldAmmTradeRecords_PlayerId_ExecutedAtTick",
                table: "GoldAmmTradeRecords",
                columns: new[] { "PlayerId", "ExecutedAtTick" });

            migrationBuilder.CreateIndex(
                name: "IX_GoldAmmTradeRecords_PoolId",
                table: "GoldAmmTradeRecords",
                column: "PoolId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerGoldBalances_PlayerId",
                table: "PlayerGoldBalances",
                column: "PlayerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "GoldAmmTradeRecords");
            migrationBuilder.DropTable(name: "GoldAmmPositions");
            migrationBuilder.DropTable(name: "PlayerGoldBalances");
            migrationBuilder.DropTable(name: "GoldAmmPools");
        }
    }
}

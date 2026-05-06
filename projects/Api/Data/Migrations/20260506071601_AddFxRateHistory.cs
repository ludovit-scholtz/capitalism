using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFxRateHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FxRateHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    QuoteCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    MidRate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    BuyRate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    SellRate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    GameTick = table.Column<long>(type: "bigint", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FxRateHistories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FxRateHistories_BaseCurrencyCode_QuoteCurrencyCode_GameTick",
                table: "FxRateHistories",
                columns: new[] { "BaseCurrencyCode", "QuoteCurrencyCode", "GameTick" });

            migrationBuilder.CreateIndex(
                name: "IX_FxRateHistories_GameTick",
                table: "FxRateHistories",
                column: "GameTick");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FxRateHistories");
        }
    }
}

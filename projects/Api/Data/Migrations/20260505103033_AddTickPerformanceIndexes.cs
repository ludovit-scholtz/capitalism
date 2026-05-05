using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTickPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_Category_RecordedAtTick",
                table: "LedgerEntries",
                columns: new[] { "Category", "RecordedAtTick" });

            migrationBuilder.CreateIndex(
                name: "IX_InterCityTradeRoutes_Status_ExpectedArrivalTick",
                table: "InterCityTradeRoutes",
                columns: new[] { "Status", "ExpectedArrivalTick" });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeOrders_IsActive",
                table: "ExchangeOrders",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_Category_RecordedAtTick",
                table: "LedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_InterCityTradeRoutes_Status_ExpectedArrivalTick",
                table: "InterCityTradeRoutes");

            migrationBuilder.DropIndex(
                name: "IX_ExchangeOrders_IsActive",
                table: "ExchangeOrders");
        }
    }
}

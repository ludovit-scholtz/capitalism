using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MigrationOne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "FuelPriceIndex",
                table: "Cities",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldDefaultValue: 1.0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "FuelReserveMwh",
                table: "Buildings",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<int>(
                name: "DispatchTargetPercent",
                table: "Buildings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 100);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "FuelPriceIndex",
                table: "Cities",
                type: "numeric",
                nullable: false,
                defaultValue: 1.0m,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "FuelReserveMwh",
                table: "Buildings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<int>(
                name: "DispatchTargetPercent",
                table: "Buildings",
                type: "integer",
                nullable: false,
                defaultValue: 100,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}

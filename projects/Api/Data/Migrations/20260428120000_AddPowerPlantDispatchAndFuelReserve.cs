using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPowerPlantDispatchAndFuelReserve : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql(
                    "ALTER TABLE \"Buildings\" ADD COLUMN IF NOT EXISTS \"DispatchTargetPercent\" integer NOT NULL DEFAULT 100;");
                migrationBuilder.Sql(
                    "ALTER TABLE \"Buildings\" ADD COLUMN IF NOT EXISTS \"FuelReserveMwh\" numeric NOT NULL DEFAULT 0;");
                return;
            }

            migrationBuilder.AddColumn<int>(
                name: "DispatchTargetPercent",
                table: "Buildings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<decimal>(
                name: "FuelReserveMwh",
                table: "Buildings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DispatchTargetPercent",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "FuelReserveMwh",
                table: "Buildings");
        }
    }
}

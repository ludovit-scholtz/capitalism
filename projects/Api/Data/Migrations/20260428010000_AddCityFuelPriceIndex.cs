using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCityFuelPriceIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql(
                    "ALTER TABLE \"Cities\" ADD COLUMN IF NOT EXISTS \"FuelPriceIndex\" numeric NOT NULL DEFAULT 1.0;");

                // Set realistic per-city fuel price indexes based on local market conditions.
                // These values reflect relative fuel costs: London has high fuel taxes, Delhi and
                // Beijing have subsidised / cheaper fuel, New York has historically cheap US fuel.
                migrationBuilder.Sql("UPDATE \"Cities\" SET \"FuelPriceIndex\" = 0.95 WHERE \"Name\" = 'Prague';");
                migrationBuilder.Sql("UPDATE \"Cities\" SET \"FuelPriceIndex\" = 1.05 WHERE \"Name\" = 'Vienna';");
                migrationBuilder.Sql("UPDATE \"Cities\" SET \"FuelPriceIndex\" = 0.80 WHERE \"Name\" = 'New York';");
                migrationBuilder.Sql("UPDATE \"Cities\" SET \"FuelPriceIndex\" = 1.25 WHERE \"Name\" = 'London';");
                migrationBuilder.Sql("UPDATE \"Cities\" SET \"FuelPriceIndex\" = 0.70 WHERE \"Name\" = 'Beijing';");
                migrationBuilder.Sql("UPDATE \"Cities\" SET \"FuelPriceIndex\" = 0.65 WHERE \"Name\" = 'Delhi';");
                // Bratislava stays at the default 1.0 (EUR baseline).
                return;
            }

            migrationBuilder.AddColumn<decimal>(
                name: "FuelPriceIndex",
                table: "Cities",
                type: "numeric",
                nullable: false,
                defaultValue: 1.0m);

            migrationBuilder.Sql("UPDATE \"Cities\" SET \"FuelPriceIndex\" = 0.95 WHERE \"Name\" = 'Prague';");
            migrationBuilder.Sql("UPDATE \"Cities\" SET \"FuelPriceIndex\" = 1.05 WHERE \"Name\" = 'Vienna';");
            migrationBuilder.Sql("UPDATE \"Cities\" SET \"FuelPriceIndex\" = 0.80 WHERE \"Name\" = 'New York';");
            migrationBuilder.Sql("UPDATE \"Cities\" SET \"FuelPriceIndex\" = 1.25 WHERE \"Name\" = 'London';");
            migrationBuilder.Sql("UPDATE \"Cities\" SET \"FuelPriceIndex\" = 0.70 WHERE \"Name\" = 'Beijing';");
            migrationBuilder.Sql("UPDATE \"Cities\" SET \"FuelPriceIndex\" = 0.65 WHERE \"Name\" = 'Delhi';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FuelPriceIndex",
                table: "Cities");
        }
    }
}

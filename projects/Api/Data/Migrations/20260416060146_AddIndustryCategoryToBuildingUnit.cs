using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIndustryCategoryToBuildingUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IndustryCategory",
                table: "BuildingUnits",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndustryCategory",
                table: "BuildingConfigurationPlanUnits",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IndustryCategory",
                table: "BuildingUnits");

            migrationBuilder.DropColumn(
                name: "IndustryCategory",
                table: "BuildingConfigurationPlanUnits");
        }
    }
}

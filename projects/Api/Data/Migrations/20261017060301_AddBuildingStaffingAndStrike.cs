using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildingStaffingAndStrike : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "StaffingFactor",
                table: "Buildings",
                type: "numeric(8,4)",
                precision: 8,
                scale: 4,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<long>(
                name: "StrikeStartedAtTick",
                table: "Buildings",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "StrikeUntilTick",
                table: "Buildings",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StaffingFactor",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "StrikeStartedAtTick",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "StrikeUntilTick",
                table: "Buildings");
        }
    }
}

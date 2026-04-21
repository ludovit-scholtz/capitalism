using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaHouseContentBudgetPerTick : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use IF NOT EXISTS to safely recover databases where the schema-repair code
            // pre-created this column before the migration was applied (42701 duplicate-column
            // startup failure documented in copilot instructions — "never pre-create the next column").
            migrationBuilder.Sql(
                "ALTER TABLE \"Buildings\" ADD COLUMN IF NOT EXISTS \"ContentBudgetPerTick\" numeric");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentBudgetPerTick",
                table: "Buildings");
        }
    }
}

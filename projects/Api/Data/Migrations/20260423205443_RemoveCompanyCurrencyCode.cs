using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCompanyCurrencyCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql("ALTER TABLE \"Companies\" DROP COLUMN \"CurrencyCode\";");
                return;
            }

            migrationBuilder.Sql("ALTER TABLE \"Companies\" DROP COLUMN IF EXISTS \"CurrencyCode\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql("ALTER TABLE \"Companies\" ADD COLUMN \"CurrencyCode\" TEXT NOT NULL DEFAULT 'EUR';");
                return;
            }

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "Companies",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "EUR");
        }
    }
}

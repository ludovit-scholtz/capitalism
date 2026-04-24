using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyCurrencyCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.AddColumn<string>(
                    name: "CurrencyCode",
                    table: "Companies",
                    type: "TEXT",
                    maxLength: 3,
                    nullable: false,
                    defaultValue: "EUR");

                migrationBuilder.Sql(@"
                    UPDATE ""Companies""
                    SET ""CurrencyCode"" = COALESCE(
                        (SELECT ci.""CurrencyCode""
                         FROM ""Buildings"" b
                         JOIN ""Cities"" ci ON ci.""Id"" = b.""CityId""
                         WHERE b.""CompanyId"" = ""Companies"".""Id""
                         LIMIT 1),
                        'EUR'
                    )
                ");
                return;
            }

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "Companies",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "EUR");

            // Backfill existing companies: set CurrencyCode to the currency of the first city
            // where the company has a building. Falls back to EUR if none found.
            migrationBuilder.Sql(@"
                UPDATE ""Companies"" c
                SET ""CurrencyCode"" = COALESCE(
                    (SELECT ci.""CurrencyCode""
                     FROM ""Buildings"" b
                     JOIN ""Cities"" ci ON ci.""Id"" = b.""CityId""
                     WHERE b.""CompanyId"" = c.""Id""
                     LIMIT 1),
                    'EUR'
                )
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "Companies");
        }
    }
}

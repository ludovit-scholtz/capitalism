using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCityWeatherForecast : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql"))
            {
                migrationBuilder.Sql(
                    """
                    DO $$
                    DECLARE
                        city_id_type TEXT;
                    BEGIN
                        SELECT pg_catalog.format_type(a.atttypid, a.atttypmod)
                        INTO city_id_type
                        FROM pg_attribute a
                        JOIN pg_class c ON c.oid = a.attrelid
                        JOIN pg_namespace n ON n.oid = c.relnamespace
                        WHERE n.nspname = 'public'
                          AND c.relname = 'Cities'
                          AND a.attname = 'Id'
                          AND a.attnum > 0
                          AND NOT a.attisdropped;

                        IF city_id_type IS NULL THEN
                            RAISE EXCEPTION 'Cities.Id column not found while creating CityWeatherForecasts';
                        END IF;

                        IF city_id_type = 'uuid' THEN
                            EXECUTE '
                                CREATE TABLE IF NOT EXISTS "CityWeatherForecasts" (
                                    "CityId" uuid NOT NULL,
                                    "Tick" bigint NOT NULL,
                                    "WindPercent" numeric NOT NULL,
                                    "SolarPercent" numeric NOT NULL,
                                    CONSTRAINT "PK_CityWeatherForecasts" PRIMARY KEY ("CityId", "Tick"),
                                    CONSTRAINT "FK_CityWeatherForecasts_Cities_CityId"
                                        FOREIGN KEY ("CityId") REFERENCES "Cities" ("Id") ON DELETE CASCADE
                                )';
                        ELSE
                            EXECUTE '
                                CREATE TABLE IF NOT EXISTS "CityWeatherForecasts" (
                                    "CityId" text NOT NULL,
                                    "Tick" bigint NOT NULL,
                                    "WindPercent" numeric NOT NULL,
                                    "SolarPercent" numeric NOT NULL,
                                    CONSTRAINT "PK_CityWeatherForecasts" PRIMARY KEY ("CityId", "Tick"),
                                    CONSTRAINT "FK_CityWeatherForecasts_Cities_CityId"
                                        FOREIGN KEY ("CityId") REFERENCES "Cities" ("Id") ON DELETE CASCADE
                                )';
                        END IF;

                        EXECUTE 'CREATE INDEX IF NOT EXISTS "IX_CityWeatherForecasts_CityId_Tick" ON "CityWeatherForecasts" ("CityId", "Tick")';
                    END $$;
                    """);

                return;
            }

            migrationBuilder.CreateTable(
                name: "CityWeatherForecasts",
                columns: table => new
                {
                    CityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tick = table.Column<long>(type: "bigint", nullable: false),
                    WindPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    SolarPercent = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CityWeatherForecasts", x => new { x.CityId, x.Tick });
                    table.ForeignKey(
                        name: "FK_CityWeatherForecasts_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CityWeatherForecasts_CityId_Tick",
                table: "CityWeatherForecasts",
                columns: new[] { "CityId", "Tick" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CityWeatherForecasts");
        }
    }
}

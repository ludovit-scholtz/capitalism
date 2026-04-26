using Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260426123000_RepairMissingForexTradeRecordsTable")]
    public partial class RepairMissingForexTradeRecordsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!ActiveProvider.Contains("Npgsql", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    CREATE TABLE IF NOT EXISTS "ForexTradeRecords" (
                        "Id" uuid NOT NULL,
                        "PlayerId" uuid NOT NULL,
                        "FromCurrencyCode" character varying(3) NOT NULL,
                        "ToCurrencyCode" character varying(3) NOT NULL,
                        "FromAmount" numeric(18,4) NOT NULL,
                        "ToAmount" numeric(18,4) NOT NULL,
                        "FeeAmount" numeric(18,4) NOT NULL,
                        "Rate" numeric(18,6) NOT NULL,
                        "ExecutedAtTick" bigint NOT NULL,
                        "ExecutedAtUtc" timestamp with time zone NOT NULL,
                        CONSTRAINT "PK_ForexTradeRecords" PRIMARY KEY ("Id")
                    );

                    CREATE INDEX IF NOT EXISTS "IX_ForexTradeRecords_PlayerId_ExecutedAtTick"
                        ON "ForexTradeRecords" ("PlayerId", "ExecutedAtTick");

                    BEGIN
                        ALTER TABLE "ForexTradeRecords"
                            ADD CONSTRAINT "FK_ForexTradeRecords_Players_PlayerId"
                            FOREIGN KEY ("PlayerId") REFERENCES "Players" ("Id") ON DELETE CASCADE;
                    EXCEPTION
                        WHEN duplicate_object THEN NULL;
                        WHEN OTHERS THEN NULL;
                    END;
                END $$;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Repair migration: no down path to avoid dropping trade history data.
        }
    }
}

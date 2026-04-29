using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RepairMissingManualMigrationArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql"))
            {
                migrationBuilder.Sql(PostgresRepairSql);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }

        private const string PostgresRepairSql = """
            ALTER TABLE "Brands" ADD COLUMN IF NOT EXISTS "MarketingEfficiencyMultiplier" numeric(7,4) NOT NULL DEFAULT 1;
            ALTER TABLE "Brands" ADD COLUMN IF NOT EXISTS "MarketingQuality" numeric(5,4) NOT NULL DEFAULT 0;
            ALTER TABLE "Cities" ADD COLUMN IF NOT EXISTS "FuelPriceIndex" numeric NOT NULL DEFAULT 1.0;
            ALTER TABLE "Buildings" ADD COLUMN IF NOT EXISTS "DispatchTargetPercent" integer NOT NULL DEFAULT 100;
            ALTER TABLE "Buildings" ADD COLUMN IF NOT EXISTS "FuelReserveMwh" numeric NOT NULL DEFAULT 0;

            UPDATE "Cities" SET "FuelPriceIndex" = 0.95 WHERE "Name" = 'Prague';
            UPDATE "Cities" SET "FuelPriceIndex" = 1.05 WHERE "Name" = 'Vienna';
            UPDATE "Cities" SET "FuelPriceIndex" = 0.80 WHERE "Name" = 'New York';
            UPDATE "Cities" SET "FuelPriceIndex" = 1.25 WHERE "Name" = 'London';
            UPDATE "Cities" SET "FuelPriceIndex" = 0.70 WHERE "Name" = 'Beijing';
            UPDATE "Cities" SET "FuelPriceIndex" = 0.65 WHERE "Name" = 'Delhi';

            CREATE TABLE IF NOT EXISTS "FxRates" (
                "Id" uuid NOT NULL,
                "BaseCurrencyCode" character varying(3) NOT NULL,
                "QuoteCurrencyCode" character varying(3) NOT NULL,
                "Rate" numeric(18,6) NOT NULL,
                "RateDate" date NOT NULL,
                "FetchedAtUtc" timestamp with time zone NOT NULL,
                "Source" character varying(20) NOT NULL,
                CONSTRAINT "PK_FxRates" PRIMARY KEY ("Id")
            );
            CREATE INDEX IF NOT EXISTS "IX_FxRates_BaseCurrencyCode_QuoteCurrencyCode_RateDate"
                ON "FxRates" ("BaseCurrencyCode", "QuoteCurrencyCode", "RateDate");

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

            CREATE TABLE IF NOT EXISTS "PlayerGoldBalances" (
                "Id" uuid NOT NULL,
                "PlayerId" uuid NOT NULL,
                "Balance" numeric(18,8) NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_PlayerGoldBalances" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_PlayerGoldBalances_PlayerId" ON "PlayerGoldBalances" ("PlayerId");

            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM pg_constraint
                    WHERE conname = 'CK_PlayerGoldBalances_Balance_NonNegative'
                ) THEN
                    ALTER TABLE "PlayerGoldBalances"
                    ADD CONSTRAINT "CK_PlayerGoldBalances_Balance_NonNegative" CHECK ("Balance" >= 0);
                END IF;
            END $$;

            CREATE TABLE IF NOT EXISTS "GoldAmmPools" (
                "Id" uuid NOT NULL,
                "CurrencyCode" character varying(3) NOT NULL,
                "FiatReserve" numeric(18,4) NOT NULL,
                "GoldReserve" numeric(18,8) NOT NULL,
                "TotalLiquidityShares" numeric(18,8) NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_GoldAmmPools" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_GoldAmmPools_CurrencyCode" ON "GoldAmmPools" ("CurrencyCode");

            CREATE TABLE IF NOT EXISTS "GoldAmmPositions" (
                "Id" uuid NOT NULL,
                "PoolId" uuid NOT NULL,
                "PlayerId" uuid NOT NULL,
                "LiquidityShares" numeric(18,8) NOT NULL,
                "FiatProvided" numeric(18,4) NOT NULL,
                "GoldProvided" numeric(18,8) NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_GoldAmmPositions" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_GoldAmmPositions_PoolId_PlayerId"
                ON "GoldAmmPositions" ("PoolId", "PlayerId");

            CREATE TABLE IF NOT EXISTS "GoldAmmTradeRecords" (
                "Id" uuid NOT NULL,
                "PlayerId" uuid NOT NULL,
                "PoolId" uuid NOT NULL,
                "Direction" character varying(20) NOT NULL,
                "CurrencyCode" character varying(3) NOT NULL,
                "InputAmount" numeric(18,8) NOT NULL,
                "OutputAmount" numeric(18,8) NOT NULL,
                "FeeAmount" numeric(18,8) NOT NULL,
                "ImpliedPrice" numeric(18,4) NOT NULL,
                "ExecutedAtTick" bigint NOT NULL,
                "ExecutedAtUtc" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_GoldAmmTradeRecords" PRIMARY KEY ("Id")
            );
            CREATE INDEX IF NOT EXISTS "IX_GoldAmmTradeRecords_PlayerId_ExecutedAtTick"
                ON "GoldAmmTradeRecords" ("PlayerId", "ExecutedAtTick");
            """;
    }
}

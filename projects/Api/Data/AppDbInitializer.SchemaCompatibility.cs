using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

/// <summary>
/// Repairs known additive schema drift caused by legacy databases that were bootstrapped
/// without EF migration history and later baselined as if every migration had already run.
/// </summary>
public sealed partial class AppDbInitializer
{
    private async Task RepairKnownLegacySchemaDriftAsync()
    {
        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        var dialect = GetSchemaDialect();
        var connection = dbContext.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        try
        {
            if (!await TableExistsAsync(connection, dialect, "Buildings"))
            {
                return;
            }

            IReadOnlySet<string>? pendingMigrations = null;
            if (dialect.IsPostgres && await MigrationsHistoryTableExistsAsync(connection))
            {
                pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync())
                    .ToHashSet(StringComparer.Ordinal);
            }

            if (await TableExistsAsync(connection, dialect, "Players")
                && ShouldRepairSchemaArtifact("20260413222338_AddPersonalTaxReserve", pendingMigrations))
            {
                await EnsureColumnAsync(connection, dialect, "Players", "PersonalTaxReserve", dialect.RequiredDecimalDefaultZero);
            }

            if (ShouldRepairSchemaArtifact("20260415025150_AddProductResearchBudget", pendingMigrations)
                && !await TableExistsAsync(connection, dialect, "ProductResearchBudgets"))
            {
                await ExecuteNonQueryAsync(connection, dialect.CreateProductResearchBudgetsTableSql);
            }

            if (ShouldRepairSchemaArtifact("20260415025150_AddProductResearchBudget", pendingMigrations))
            {
                await EnsureIndexAsync(
                    connection,
                    dialect,
                    "ProductResearchBudgets",
                    "IX_ProductResearchBudgets_CompanyId",
                    dialect.CreateProductResearchBudgetsCompanyIndexSql);
                await EnsureIndexAsync(
                    connection,
                    dialect,
                    "ProductResearchBudgets",
                    "IX_ProductResearchBudgets_ProductTypeId",
                    dialect.CreateProductResearchBudgetsProductIndexSql);
            }

            if (ShouldRepairSchemaArtifact("20260415204838_AddBankDeposits", pendingMigrations))
            {
                await EnsureColumnAsync(connection, dialect, "Buildings", "BaseCapitalDeposited", dialect.RequiredBooleanDefaultFalse);
                await EnsureColumnAsync(connection, dialect, "Buildings", "DepositInterestRatePercent", dialect.NullableInterestRate);
                await EnsureColumnAsync(connection, dialect, "Buildings", "LendingInterestRatePercent", dialect.NullableInterestRate);
                await EnsureColumnAsync(connection, dialect, "Buildings", "TotalDeposits", dialect.RequiredMoneyDefaultZero);
            }

            if (ShouldRepairSchemaArtifact("20260417045454_AddBankCentralBankDebt", pendingMigrations))
            {
                await EnsureColumnAsync(connection, dialect, "Buildings", "CentralBankDebt", dialect.RequiredDecimalDefaultZero);
            }

            if (ShouldRepairSchemaArtifact("20260421054822_AddMediaHouseContentValue", pendingMigrations))
            {
                await EnsureColumnAsync(connection, dialect, "Buildings", "ContentValue", dialect.RequiredDecimalDefaultZero);
                await EnsureColumnAsync(connection, dialect, "Buildings", "IsGovernmentOwned", dialect.RequiredBooleanDefaultFalse);
            }

            if (ShouldRepairSchemaArtifact("20260421070000_AddMediaHouseContentBudgetPerTick", pendingMigrations))
            {
                await EnsureColumnAsync(connection, dialect, "Buildings", "ContentBudgetPerTick", dialect.NullableDecimal);
            }

            if (ShouldRepairSchemaArtifact("20260415204838_AddBankDeposits", pendingMigrations)
                && await TableExistsAsync(connection, dialect, "BankDeposits"))
            {
                await EnsureColumnAsync(connection, dialect, "BankDeposits", "TotalInterestPaid", dialect.RequiredDecimal4DefaultZero);
            }
            else if (ShouldRepairSchemaArtifact("20260415204838_AddBankDeposits", pendingMigrations))
            {
                await ExecuteNonQueryAsync(connection, dialect.CreateBankDepositsTableSql);
            }

            if (ShouldRepairSchemaArtifact("20260415204838_AddBankDeposits", pendingMigrations))
            {
                await EnsureIndexAsync(
                    connection,
                    dialect,
                    "BankDeposits",
                    "IX_BankDeposits_BankBuildingId_IsActive",
                    dialect.CreateBankDepositsByBankIndexSql);
                await EnsureIndexAsync(
                    connection,
                    dialect,
                    "BankDeposits",
                    "IX_BankDeposits_DepositorCompanyId_IsActive",
                    dialect.CreateBankDepositsByDepositorIndexSql);
            }

            if (ShouldRepairSchemaArtifact("20260416060146_AddIndustryCategoryToBuildingUnit", pendingMigrations)
                && await TableExistsAsync(connection, dialect, "BuildingUnits"))
            {
                await EnsureColumnAsync(connection, dialect, "BuildingUnits", "IndustryCategory", dialect.NullableShortText);
            }

            if (ShouldRepairSchemaArtifact("20260416060146_AddIndustryCategoryToBuildingUnit", pendingMigrations)
                && await TableExistsAsync(connection, dialect, "BuildingConfigurationPlanUnits"))
            {
                await EnsureColumnAsync(connection, dialect, "BuildingConfigurationPlanUnits", "IndustryCategory", dialect.NullableShortText);
            }

            if (ShouldRepairSchemaArtifact("20260417135125_AddLoanCollateral", pendingMigrations)
                && await TableExistsAsync(connection, dialect, "Loans"))
            {
                await EnsureColumnAsync(connection, dialect, "Loans", "CollateralAppraisedValue", dialect.NullableDecimal);
                await EnsureColumnAsync(connection, dialect, "Loans", "CollateralBuildingId", dialect.NullableGuid);
                await EnsureIndexAsync(
                    connection,
                    dialect,
                    "Loans",
                    "IX_Loans_CollateralBuildingId",
                    dialect.CreateLoansCollateralIndexSql);

                if (dialect.IsPostgres)
                {
                    await EnsurePostgresConstraintAsync(
                        connection,
                        "FK_Loans_Buildings_CollateralBuildingId",
                        dialect.CreateLoansCollateralForeignKeySql);
                }
            }

            await RepairLegacyPostgresStoreTypesAsync(connection, dialect);

            // Ensure Cities.CurrencyCode column exists (added in AddCityCurrencyCode migration).
            if (ShouldRepairSchemaArtifact("20260420034843_AddCityCurrencyCode", pendingMigrations)
                && await TableExistsAsync(connection, dialect, "Cities"))
            {
                if (!await ColumnExistsAsync(connection, dialect, "Cities", "CurrencyCode"))
                {
                    // Add CurrencyCode with default EUR; existing cities get EUR until seeding updates them.
                    await ExecuteNonQueryAsync(connection,
                        dialect.IsPostgres
                            ? "ALTER TABLE \"Cities\" ADD COLUMN \"CurrencyCode\" character varying(3) NOT NULL DEFAULT 'EUR'"
                            : "ALTER TABLE \"Cities\" ADD COLUMN \"CurrencyCode\" TEXT NOT NULL DEFAULT 'EUR'");

                    // Update the known cities to their correct currency codes.
                    await ExecuteNonQueryAsync(connection,
                        "UPDATE \"Cities\" SET \"CurrencyCode\" = 'CZK' WHERE \"Name\" = 'Prague'");
                }
            }

            // Ensure CityWeatherForecasts table exists (added in AddCityWeatherForecast migration).
            if (ShouldRepairSchemaArtifact("20260418054625_AddCityWeatherForecast", pendingMigrations)
                && !await TableExistsAsync(connection, dialect, "CityWeatherForecasts"))
            {
                if (dialect.IsPostgres)
                {
                    await ExecuteNonQueryAsync(connection,
                        """
                        CREATE TABLE IF NOT EXISTS "CityWeatherForecasts" (
                            "CityId" uuid NOT NULL,
                            "Tick" bigint NOT NULL,
                            "WindPercent" numeric NOT NULL,
                            "SolarPercent" numeric NOT NULL,
                            CONSTRAINT "PK_CityWeatherForecasts" PRIMARY KEY ("CityId", "Tick"),
                            CONSTRAINT "FK_CityWeatherForecasts_Cities_CityId" FOREIGN KEY ("CityId") REFERENCES "Cities" ("Id") ON DELETE CASCADE
                        )
                        """);
                    await ExecuteNonQueryAsync(connection,
                        "CREATE INDEX IF NOT EXISTS \"IX_CityWeatherForecasts_CityId_Tick\" ON \"CityWeatherForecasts\" (\"CityId\", \"Tick\")");
                }
                else
                {
                    await ExecuteNonQueryAsync(connection,
                        """
                        CREATE TABLE IF NOT EXISTS "CityWeatherForecasts" (
                            "CityId" TEXT NOT NULL,
                            "Tick" INTEGER NOT NULL,
                            "WindPercent" TEXT NOT NULL,
                            "SolarPercent" TEXT NOT NULL,
                            CONSTRAINT "PK_CityWeatherForecasts" PRIMARY KEY ("CityId", "Tick"),
                            CONSTRAINT "FK_CityWeatherForecasts_Cities_CityId" FOREIGN KEY ("CityId") REFERENCES "Cities" ("Id") ON DELETE CASCADE
                        )
                        """);
                }
            }

            // Ensure FxRates table exists (added in AddFxRates migration).
            if (ShouldRepairSchemaArtifact("20260420100000_AddFxRates", pendingMigrations)
                && !await TableExistsAsync(connection, dialect, "FxRates"))
            {
                if (dialect.IsPostgres)
                {
                    await ExecuteNonQueryAsync(connection,
                        """
                        CREATE TABLE IF NOT EXISTS "FxRates" (
                            "Id" uuid NOT NULL,
                            "BaseCurrencyCode" character varying(3) NOT NULL,
                            "QuoteCurrencyCode" character varying(3) NOT NULL,
                            "Rate" numeric(18,6) NOT NULL,
                            "RateDate" date NOT NULL,
                            "FetchedAtUtc" timestamp with time zone NOT NULL,
                            "Source" character varying(20) NOT NULL,
                            CONSTRAINT "PK_FxRates" PRIMARY KEY ("Id")
                        )
                        """);
                    await ExecuteNonQueryAsync(connection,
                        "CREATE INDEX IF NOT EXISTS \"IX_FxRates_BaseCurrencyCode_QuoteCurrencyCode_RateDate\" ON \"FxRates\" (\"BaseCurrencyCode\", \"QuoteCurrencyCode\", \"RateDate\")");
                }
                else
                {
                    await ExecuteNonQueryAsync(connection,
                        """
                        CREATE TABLE IF NOT EXISTS "FxRates" (
                            "Id" TEXT NOT NULL,
                            "BaseCurrencyCode" TEXT NOT NULL,
                            "QuoteCurrencyCode" TEXT NOT NULL,
                            "Rate" TEXT NOT NULL,
                            "RateDate" TEXT NOT NULL,
                            "FetchedAtUtc" TEXT NOT NULL,
                            "Source" TEXT NOT NULL,
                            CONSTRAINT "PK_FxRates" PRIMARY KEY ("Id")
                        )
                        """);
                }
            }

            // Ensure ForexTradeRecords table exists (added in AddForexExchangeMvp migration).
            if (ShouldRepairSchemaArtifact("20260420110000_AddForexExchangeMvp", pendingMigrations)
                && !await TableExistsAsync(connection, dialect, "ForexTradeRecords"))
            {
                if (dialect.IsPostgres)
                {
                    await ExecuteNonQueryAsync(connection,
                        """
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
                            "ExecutedAtUtc" timestamp with time zone NOT NULL DEFAULT now(),
                            CONSTRAINT "PK_ForexTradeRecords" PRIMARY KEY ("Id"),
                            CONSTRAINT "FK_ForexTradeRecords_Players_PlayerId" FOREIGN KEY ("PlayerId") REFERENCES "Players" ("Id") ON DELETE CASCADE
                        )
                        """);
                    await ExecuteNonQueryAsync(connection,
                        "CREATE INDEX IF NOT EXISTS \"IX_ForexTradeRecords_PlayerId_ExecutedAtTick\" ON \"ForexTradeRecords\" (\"PlayerId\", \"ExecutedAtTick\")");
                }
                else
                {
                    await ExecuteNonQueryAsync(connection,
                        """
                        CREATE TABLE IF NOT EXISTS "ForexTradeRecords" (
                            "Id" TEXT NOT NULL,
                            "PlayerId" TEXT NOT NULL,
                            "FromCurrencyCode" TEXT NOT NULL,
                            "ToCurrencyCode" TEXT NOT NULL,
                            "FromAmount" TEXT NOT NULL,
                            "ToAmount" TEXT NOT NULL,
                            "FeeAmount" TEXT NOT NULL,
                            "Rate" TEXT NOT NULL,
                            "ExecutedAtTick" INTEGER NOT NULL,
                            "ExecutedAtUtc" TEXT NOT NULL,
                            CONSTRAINT "PK_ForexTradeRecords" PRIMARY KEY ("Id"),
                            CONSTRAINT "FK_ForexTradeRecords_Players_PlayerId" FOREIGN KEY ("PlayerId") REFERENCES "Players" ("Id") ON DELETE CASCADE
                        )
                        """);
                }
            }

            // Ensure Brands.MarketingQuality column exists (added in AddBrandMarketingQuality migration).
            if (ShouldRepairSchemaArtifact("20260421090000_AddBrandMarketingQuality", pendingMigrations)
                && await TableExistsAsync(connection, dialect, "Brands"))
            {
                await EnsureColumnAsync(connection, dialect, "Brands", "MarketingQuality", dialect.RequiredDecimalDefaultZero);
            }

            // Ensure BankAccounts table and building funding columns exist
            // (added in AddBankAccountsAndBuildingFunding migration).
            if (ShouldRepairSchemaArtifact("20260423025526_AddBankAccountsAndBuildingFunding", pendingMigrations))
            {
                await EnsureColumnAsync(connection, dialect, "Buildings", "BankAccountId", dialect.NullableGuid);
                await EnsureColumnAsync(connection, dialect, "Buildings", "IsSuspendedForFunds", dialect.RequiredBooleanDefaultFalse);
                await EnsureColumnAsync(connection, dialect, "Buildings", "SuspendedReason",
                    dialect.IsPostgres ? "character varying(200)" : "TEXT");

                if (!await TableExistsAsync(connection, dialect, "BankAccounts"))
                {
                    if (dialect.IsPostgres)
                    {
                        await ExecuteNonQueryAsync(connection,
                            """
                            CREATE TABLE IF NOT EXISTS "BankAccounts" (
                                "Id" uuid NOT NULL,
                                "AccountNumber" character varying(16) NOT NULL,
                                "CurrencyCode" character varying(3) NOT NULL,
                                "Balance" numeric(18,2) NOT NULL DEFAULT 0,
                                "CompanyId" uuid NULL,
                                "IsGovernmentAccount" boolean NOT NULL DEFAULT FALSE,
                                "CreatedAtUtc" timestamp with time zone NOT NULL,
                                CONSTRAINT "PK_BankAccounts" PRIMARY KEY ("Id"),
                                CONSTRAINT "FK_BankAccounts_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE SET NULL
                            )
                            """);
                    }
                    else
                    {
                        await ExecuteNonQueryAsync(connection,
                            """
                            CREATE TABLE IF NOT EXISTS "BankAccounts" (
                                "Id" TEXT NOT NULL,
                                "AccountNumber" TEXT NOT NULL,
                                "CurrencyCode" TEXT NOT NULL,
                                "Balance" TEXT NOT NULL DEFAULT '0',
                                "CompanyId" TEXT NULL,
                                "IsGovernmentAccount" INTEGER NOT NULL DEFAULT 0,
                                "CreatedAtUtc" TEXT NOT NULL,
                                CONSTRAINT "PK_BankAccounts" PRIMARY KEY ("Id")
                            )
                            """);
                    }
                }

                await EnsureIndexAsync(
                    connection,
                    dialect,
                    "BankAccounts",
                    "IX_BankAccounts_AccountNumber",
                    dialect.IsPostgres
                        ? "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_BankAccounts_AccountNumber\" ON \"BankAccounts\" (\"AccountNumber\")"
                        : "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_BankAccounts_AccountNumber\" ON \"BankAccounts\" (\"AccountNumber\")");

                await EnsureIndexAsync(
                    connection,
                    dialect,
                    "BankAccounts",
                    "IX_BankAccounts_CompanyId",
                    dialect.IsPostgres
                        ? "CREATE INDEX IF NOT EXISTS \"IX_BankAccounts_CompanyId\" ON \"BankAccounts\" (\"CompanyId\")"
                        : "CREATE INDEX IF NOT EXISTS \"IX_BankAccounts_CompanyId\" ON \"BankAccounts\" (\"CompanyId\")");

                await EnsureIndexAsync(
                    connection,
                    dialect,
                    "Buildings",
                    "IX_Buildings_BankAccountId",
                    dialect.IsPostgres
                        ? "CREATE INDEX IF NOT EXISTS \"IX_Buildings_BankAccountId\" ON \"Buildings\" (\"BankAccountId\")"
                        : "CREATE INDEX IF NOT EXISTS \"IX_Buildings_BankAccountId\" ON \"Buildings\" (\"BankAccountId\")");
            }

            if (ShouldRepairSchemaArtifact("20260423221000_RemovePlayerPersonalCash", pendingMigrations)
                && await TableExistsAsync(connection, dialect, "Players")
                && await TableExistsAsync(connection, dialect, "BankAccounts"))
            {
                await EnsureColumnAsync(connection, dialect, "BankAccounts", "PlayerId", dialect.NullableGuid);
                await EnsureIndexAsync(
                    connection,
                    dialect,
                    "BankAccounts",
                    "IX_BankAccounts_PlayerId_CurrencyCode",
                    "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_BankAccounts_PlayerId_CurrencyCode\" ON \"BankAccounts\" (\"PlayerId\", \"CurrencyCode\")");

                if (dialect.IsPostgres)
                {
                    await EnsurePostgresConstraintAsync(
                        connection,
                        "FK_BankAccounts_Players_PlayerId",
                        "ALTER TABLE \"BankAccounts\" ADD CONSTRAINT \"FK_BankAccounts_Players_PlayerId\" FOREIGN KEY (\"PlayerId\") REFERENCES \"Players\" (\"Id\") ON DELETE SET NULL");
                }

                if (await ColumnExistsAsync(connection, dialect, "Players", "PersonalCash"))
                {
                    await ExecuteNonQueryAsync(
                        connection,
                        dialect.IsPostgres
                            ?
                                """
                                INSERT INTO "BankAccounts" ("Id", "AccountNumber", "CurrencyCode", "Balance", "CompanyId", "IsGovernmentAccount", "CreatedAtUtc", "PlayerId")
                                SELECT p."Id",
                                       LPAD((9000000000000000 + ROW_NUMBER() OVER (ORDER BY p."Id"))::text, 16, '0'),
                                       'EUR',
                                       p."PersonalCash",
                                       NULL,
                                       FALSE,
                                       CURRENT_TIMESTAMP,
                                       p."Id"
                                FROM "Players" p
                                WHERE NOT EXISTS (
                                    SELECT 1
                                    FROM "BankAccounts" existing
                                    WHERE existing."PlayerId" = p."Id"
                                      AND existing."CurrencyCode" = 'EUR'
                                )
                                """
                            :
                                """
                                INSERT INTO "BankAccounts" ("Id", "AccountNumber", "CurrencyCode", "Balance", "CompanyId", "IsGovernmentAccount", "CreatedAtUtc", "PlayerId")
                                SELECT p."Id",
                                       printf('%016d', 9000000000000000 + ROW_NUMBER() OVER (ORDER BY p."Id")),
                                       'EUR',
                                       p."PersonalCash",
                                       NULL,
                                       0,
                                       CURRENT_TIMESTAMP,
                                       p."Id"
                                FROM "Players" p
                                WHERE NOT EXISTS (
                                    SELECT 1
                                    FROM "BankAccounts" existing
                                    WHERE existing."PlayerId" = p."Id"
                                      AND existing."CurrencyCode" = 'EUR'
                                )
                                """);
                }
            }

                        if (ShouldRepairSchemaArtifact("20260424084149_RemovePlayerCurrencyBalances", pendingMigrations)
                                && await TableExistsAsync(connection, dialect, "PlayerCurrencyBalances")
                                && await TableExistsAsync(connection, dialect, "BankAccounts"))
                        {
                                await ExecuteNonQueryAsync(
                                        connection,
                                        dialect.IsPostgres
                                                ?
                                                        """
                                                        UPDATE "BankAccounts" AS existing
                                                        SET "Balance" = existing."Balance" + legacy."Balance"
                                                        FROM "PlayerCurrencyBalances" AS legacy
                                                        WHERE existing."PlayerId" = legacy."PlayerId"
                                                            AND existing."CurrencyCode" = legacy."CurrencyCode";

                                                        INSERT INTO "BankAccounts" ("Id", "AccountNumber", "CurrencyCode", "Balance", "CompanyId", "IsGovernmentAccount", "CreatedAtUtc", "PlayerId")
                                                        SELECT legacy."Id",
                                                                     LPAD((9100000000000000 + ROW_NUMBER() OVER (ORDER BY legacy."PlayerId", legacy."CurrencyCode"))::text, 16, '0'),
                                                                     legacy."CurrencyCode",
                                                                     legacy."Balance",
                                                                     NULL,
                                                                     FALSE,
                                                                     legacy."CreatedAtUtc",
                                                                     legacy."PlayerId"
                                                        FROM "PlayerCurrencyBalances" AS legacy
                                                        WHERE NOT EXISTS (
                                                                SELECT 1
                                                                FROM "BankAccounts" AS existing
                                                                WHERE existing."PlayerId" = legacy."PlayerId"
                                                                    AND existing."CurrencyCode" = legacy."CurrencyCode"
                                                        );

                                                        DROP TABLE IF EXISTS "PlayerCurrencyBalances";
                                                        """
                                                :
                                                        """
                                                        UPDATE "BankAccounts"
                                                        SET "Balance" = "Balance" + (
                                                                SELECT legacy."Balance"
                                                                FROM "PlayerCurrencyBalances" AS legacy
                                                                WHERE legacy."PlayerId" = "BankAccounts"."PlayerId"
                                                                    AND legacy."CurrencyCode" = "BankAccounts"."CurrencyCode"
                                                        )
                                                        WHERE "PlayerId" IS NOT NULL
                                                            AND EXISTS (
                                                                    SELECT 1
                                                                    FROM "PlayerCurrencyBalances" AS legacy
                                                                    WHERE legacy."PlayerId" = "BankAccounts"."PlayerId"
                                                                        AND legacy."CurrencyCode" = "BankAccounts"."CurrencyCode"
                                                            );

                                                        INSERT INTO "BankAccounts" ("Id", "AccountNumber", "CurrencyCode", "Balance", "CompanyId", "IsGovernmentAccount", "CreatedAtUtc", "PlayerId")
                                                        SELECT legacy."Id",
                                                                     printf('%016d', 9100000000000000 + ROW_NUMBER() OVER (ORDER BY legacy."PlayerId", legacy."CurrencyCode")),
                                                                     legacy."CurrencyCode",
                                                                     legacy."Balance",
                                                                     NULL,
                                                                     0,
                                                                     legacy."CreatedAtUtc",
                                                                     legacy."PlayerId"
                                                        FROM "PlayerCurrencyBalances" AS legacy
                                                        WHERE NOT EXISTS (
                                                                SELECT 1
                                                                FROM "BankAccounts" AS existing
                                                                WHERE existing."PlayerId" = legacy."PlayerId"
                                                                    AND existing."CurrencyCode" = legacy."CurrencyCode"
                                                        );

                                                        DROP TABLE IF EXISTS "PlayerCurrencyBalances";
                                                        """);
                        }

                        if (ShouldRepairSchemaArtifact("20260424091612_RemoveBankDeposits", pendingMigrations)
                            && await TableExistsAsync(connection, dialect, "BankAccounts"))
                        {
                            await EnsureColumnAsync(connection, dialect, "BankAccounts", "BankBuildingId", dialect.NullableGuid);
                            await EnsureColumnAsync(connection, dialect, "BankAccounts", "DepositInterestRatePercent", dialect.NullableInterestRate);
                            await EnsureColumnAsync(connection, dialect, "BankAccounts", "DepositedAtTick", dialect.IsPostgres ? "bigint" : "INTEGER");
                            await EnsureColumnAsync(connection, dialect, "BankAccounts", "ClosedAtTick", dialect.IsPostgres ? "bigint" : "INTEGER");
                            await EnsureColumnAsync(connection, dialect, "BankAccounts", "ClosedAtUtc", dialect.IsPostgres ? "timestamp with time zone" : "TEXT");
                            await EnsureColumnAsync(connection, dialect, "BankAccounts", "IsBaseCapitalDeposit", dialect.RequiredBooleanDefaultFalse);
                            await EnsureColumnAsync(connection, dialect, "BankAccounts", "TotalInterestPaid", dialect.RequiredDecimal4DefaultZero);

                            await EnsureIndexAsync(
                                connection,
                                dialect,
                                "BankAccounts",
                                "IX_BankAccounts_BankBuildingId_ClosedAtUtc",
                                "CREATE INDEX IF NOT EXISTS \"IX_BankAccounts_BankBuildingId_ClosedAtUtc\" ON \"BankAccounts\" (\"BankBuildingId\", \"ClosedAtUtc\")");
                            await EnsureIndexAsync(
                                connection,
                                dialect,
                                "BankAccounts",
                                "IX_BankAccounts_CompanyId_BankBuildingId_ClosedAtUtc",
                                "CREATE INDEX IF NOT EXISTS \"IX_BankAccounts_CompanyId_BankBuildingId_ClosedAtUtc\" ON \"BankAccounts\" (\"CompanyId\", \"BankBuildingId\", \"ClosedAtUtc\")");

                            if (dialect.IsPostgres)
                            {
                                await EnsurePostgresConstraintAsync(
                                    connection,
                                    "FK_BankAccounts_Buildings_BankBuildingId",
                                    "ALTER TABLE \"BankAccounts\" ADD CONSTRAINT \"FK_BankAccounts_Buildings_BankBuildingId\" FOREIGN KEY (\"BankBuildingId\") REFERENCES \"Buildings\" (\"Id\") ON DELETE CASCADE");
                            }

                            if (await TableExistsAsync(connection, dialect, "BankDeposits"))
                            {
                                await ExecuteNonQueryAsync(
                                    connection,
                                    dialect.IsPostgres
                                        ?
                                            """
                                            INSERT INTO "BankAccounts" ("Id", "AccountNumber", "CurrencyCode", "Balance", "CompanyId", "IsGovernmentAccount", "CreatedAtUtc", "PlayerId", "BankBuildingId", "DepositInterestRatePercent", "DepositedAtTick", "IsBaseCapitalDeposit", "ClosedAtTick", "ClosedAtUtc", "TotalInterestPaid")
                                            SELECT legacy."Id",
                                                   LPAD((9200000000000000 + ROW_NUMBER() OVER (ORDER BY legacy."Id"))::text, 16, '0'),
                                                   city."CurrencyCode",
                                                   legacy."Amount",
                                                   legacy."DepositorCompanyId",
                                                   FALSE,
                                                   legacy."DepositedAtUtc",
                                                   NULL,
                                                   legacy."BankBuildingId",
                                                   legacy."DepositInterestRatePercent",
                                                   legacy."DepositedAtTick",
                                                   legacy."IsBaseCapital",
                                                   legacy."WithdrawnAtTick",
                                                   legacy."WithdrawnAtUtc",
                                                   legacy."TotalInterestPaid"
                                            FROM "BankDeposits" AS legacy
                                            INNER JOIN "Buildings" AS bank ON bank."Id" = legacy."BankBuildingId"
                                            INNER JOIN "Cities" AS city ON city."Id" = bank."CityId"
                                            WHERE NOT EXISTS (
                                                SELECT 1
                                                FROM "BankAccounts" AS existing
                                                WHERE existing."Id" = legacy."Id"
                                            );

                                            DROP TABLE IF EXISTS "BankDeposits";
                                            """
                                        :
                                            """
                                            INSERT INTO "BankAccounts" ("Id", "AccountNumber", "CurrencyCode", "Balance", "CompanyId", "IsGovernmentAccount", "CreatedAtUtc", "PlayerId", "BankBuildingId", "DepositInterestRatePercent", "DepositedAtTick", "IsBaseCapitalDeposit", "ClosedAtTick", "ClosedAtUtc", "TotalInterestPaid")
                                            SELECT legacy."Id",
                                                   printf('%016d', 9200000000000000 + ROW_NUMBER() OVER (ORDER BY legacy."Id")),
                                                   city."CurrencyCode",
                                                   legacy."Amount",
                                                   legacy."DepositorCompanyId",
                                                   0,
                                                   legacy."DepositedAtUtc",
                                                   NULL,
                                                   legacy."BankBuildingId",
                                                   legacy."DepositInterestRatePercent",
                                                   legacy."DepositedAtTick",
                                                   legacy."IsBaseCapital",
                                                   legacy."WithdrawnAtTick",
                                                   legacy."WithdrawnAtUtc",
                                                   legacy."TotalInterestPaid"
                                            FROM "BankDeposits" AS legacy
                                            INNER JOIN "Buildings" AS bank ON bank."Id" = legacy."BankBuildingId"
                                            INNER JOIN "Cities" AS city ON city."Id" = bank."CityId"
                                            WHERE NOT EXISTS (
                                                SELECT 1
                                                FROM "BankAccounts" AS existing
                                                WHERE existing."Id" = legacy."Id"
                                            );

                                            DROP TABLE IF EXISTS "BankDeposits";
                                            """);
                            }
                        }
        }
        finally
        {
            if (!wasOpen)
            {
                await connection.CloseAsync();
            }
        }
    }

    private SchemaDialect GetSchemaDialect()
    {
        var providerName = dbContext.Database.ProviderName ?? string.Empty;
        var isPostgres = providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)
            || providerName.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase);

        return isPostgres ? SchemaDialect.ForPostgres : SchemaDialect.ForSqlite;
    }

    private async Task EnsureColumnAsync(DbConnection connection, SchemaDialect dialect, string tableName, string columnName, string columnDefinition)
    {
        if (await ColumnExistsAsync(connection, dialect, tableName, columnName))
        {
            return;
        }

        await ExecuteNonQueryAsync(
            connection,
            $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {columnDefinition}");
    }

    private async Task EnsureIndexAsync(DbConnection connection, SchemaDialect dialect, string tableName, string indexName, string createIndexSql)
    {
        if (!await TableExistsAsync(connection, dialect, tableName) || await IndexExistsAsync(connection, dialect, tableName, indexName))
        {
            return;
        }

        await ExecuteNonQueryAsync(connection, createIndexSql);
    }

    private async Task EnsurePostgresConstraintAsync(DbConnection connection, string constraintName, string createConstraintSql)
    {
        if (await PostgresConstraintExistsAsync(connection, constraintName))
        {
            return;
        }

        await ExecuteNonQueryAsync(connection, createConstraintSql);
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, SchemaDialect dialect, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = dialect.IsPostgres
            ? "SELECT COUNT(1) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = @tableName"
            : "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = @tableName";

        AddParameter(command, "@tableName", tableName);
        return Convert.ToInt64(await command.ExecuteScalarAsync() ?? 0L) > 0;
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, SchemaDialect dialect, string tableName, string columnName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = dialect.IsPostgres
            ? "SELECT COUNT(1) FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @tableName AND column_name = @columnName"
            : $"SELECT COUNT(1) FROM pragma_table_info('{tableName.Replace("'", "''")}') WHERE name = @columnName";

        if (dialect.IsPostgres)
        {
            AddParameter(command, "@tableName", tableName);
        }
        AddParameter(command, "@columnName", columnName);
        return Convert.ToInt64(await command.ExecuteScalarAsync() ?? 0L) > 0;
    }

    private static async Task<bool> IndexExistsAsync(DbConnection connection, SchemaDialect dialect, string tableName, string indexName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = dialect.IsPostgres
            ? "SELECT COUNT(1) FROM pg_indexes WHERE schemaname = 'public' AND tablename = @tableName AND indexname = @indexName"
            : "SELECT COUNT(1) FROM sqlite_master WHERE type = 'index' AND tbl_name = @tableName AND name = @indexName";

        AddParameter(command, "@tableName", tableName);
        AddParameter(command, "@indexName", indexName);
        return Convert.ToInt64(await command.ExecuteScalarAsync() ?? 0L) > 0;
    }

    private static async Task<bool> PostgresConstraintExistsAsync(DbConnection connection, string constraintName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM pg_constraint WHERE conname = @constraintName";
        AddParameter(command, "@constraintName", constraintName);
        return Convert.ToInt64(await command.ExecuteScalarAsync() ?? 0L) > 0;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static async Task ExecuteNonQueryAsync(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private sealed record SchemaDialect(
        bool IsPostgres,
        string RequiredBooleanDefaultFalse,
        string NullableInterestRate,
        string RequiredMoneyDefaultZero,
        string RequiredDecimalDefaultZero,
        string RequiredDecimal4DefaultZero,
        string NullableShortText,
        string NullableDecimal,
        string NullableGuid,
        string CreateProductResearchBudgetsTableSql,
        string CreateProductResearchBudgetsCompanyIndexSql,
        string CreateProductResearchBudgetsProductIndexSql,
        string CreateBankDepositsTableSql,
        string CreateBankDepositsByBankIndexSql,
        string CreateBankDepositsByDepositorIndexSql,
        string CreateLoansCollateralIndexSql,
        string CreateLoansCollateralForeignKeySql)
    {
        public static readonly SchemaDialect ForPostgres = new(
            IsPostgres: true,
            RequiredBooleanDefaultFalse: "boolean NOT NULL DEFAULT FALSE",
            NullableInterestRate: "numeric(8,4)",
            RequiredMoneyDefaultZero: "numeric(18,2) NOT NULL DEFAULT 0",
            RequiredDecimalDefaultZero: "numeric NOT NULL DEFAULT 0",
            RequiredDecimal4DefaultZero: "numeric(18,4) NOT NULL DEFAULT 0",
            NullableShortText: "character varying(50)",
            NullableDecimal: "numeric",
            NullableGuid: "uuid",
            CreateProductResearchBudgetsTableSql:
                """
                CREATE TABLE IF NOT EXISTS "ProductResearchBudgets" (
                    "Id" uuid NOT NULL,
                    "CompanyId" uuid NOT NULL,
                    "ProductTypeId" uuid NOT NULL,
                    "AccumulatedBudget" numeric NOT NULL,
                    CONSTRAINT "PK_ProductResearchBudgets" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_ProductResearchBudgets_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_ProductResearchBudgets_ProductTypes_ProductTypeId" FOREIGN KEY ("ProductTypeId") REFERENCES "ProductTypes" ("Id") ON DELETE CASCADE
                )
                """,
            CreateProductResearchBudgetsCompanyIndexSql:
                "CREATE INDEX IF NOT EXISTS \"IX_ProductResearchBudgets_CompanyId\" ON \"ProductResearchBudgets\" (\"CompanyId\")",
            CreateProductResearchBudgetsProductIndexSql:
                "CREATE INDEX IF NOT EXISTS \"IX_ProductResearchBudgets_ProductTypeId\" ON \"ProductResearchBudgets\" (\"ProductTypeId\")",
            CreateBankDepositsTableSql:
                """
                CREATE TABLE IF NOT EXISTS "BankDeposits" (
                    "Id" uuid NOT NULL,
                    "BankBuildingId" uuid NOT NULL,
                    "DepositorCompanyId" uuid NOT NULL,
                    "Amount" numeric(18,2) NOT NULL,
                    "DepositInterestRatePercent" numeric(8,4) NOT NULL,
                    "IsBaseCapital" boolean NOT NULL,
                    "IsActive" boolean NOT NULL,
                    "DepositedAtTick" bigint NOT NULL,
                    "DepositedAtUtc" timestamp with time zone NOT NULL,
                    "WithdrawnAtTick" bigint NULL,
                    "WithdrawnAtUtc" timestamp with time zone NULL,
                    "TotalInterestPaid" numeric(18,4) NOT NULL DEFAULT 0,
                    CONSTRAINT "PK_BankDeposits" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_BankDeposits_Buildings_BankBuildingId" FOREIGN KEY ("BankBuildingId") REFERENCES "Buildings" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_BankDeposits_Companies_DepositorCompanyId" FOREIGN KEY ("DepositorCompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT
                )
                """,
            CreateBankDepositsByBankIndexSql:
                "CREATE INDEX IF NOT EXISTS \"IX_BankDeposits_BankBuildingId_IsActive\" ON \"BankDeposits\" (\"BankBuildingId\", \"IsActive\")",
            CreateBankDepositsByDepositorIndexSql:
                "CREATE INDEX IF NOT EXISTS \"IX_BankDeposits_DepositorCompanyId_IsActive\" ON \"BankDeposits\" (\"DepositorCompanyId\", \"IsActive\")",
            CreateLoansCollateralIndexSql:
                "CREATE INDEX IF NOT EXISTS \"IX_Loans_CollateralBuildingId\" ON \"Loans\" (\"CollateralBuildingId\")",
            CreateLoansCollateralForeignKeySql:
                "ALTER TABLE \"Loans\" ADD CONSTRAINT \"FK_Loans_Buildings_CollateralBuildingId\" FOREIGN KEY (\"CollateralBuildingId\") REFERENCES \"Buildings\" (\"Id\")");

        public static readonly SchemaDialect ForSqlite = new(
            IsPostgres: false,
            RequiredBooleanDefaultFalse: "INTEGER NOT NULL DEFAULT 0",
            NullableInterestRate: "TEXT",
            RequiredMoneyDefaultZero: "TEXT NOT NULL DEFAULT 0",
            RequiredDecimalDefaultZero: "TEXT NOT NULL DEFAULT 0",
            RequiredDecimal4DefaultZero: "TEXT NOT NULL DEFAULT 0",
            NullableShortText: "TEXT",
            NullableDecimal: "TEXT",
            NullableGuid: "TEXT",
            CreateProductResearchBudgetsTableSql:
                """
                CREATE TABLE IF NOT EXISTS "ProductResearchBudgets" (
                    "Id" TEXT NOT NULL,
                    "CompanyId" TEXT NOT NULL,
                    "ProductTypeId" TEXT NOT NULL,
                    "AccumulatedBudget" TEXT NOT NULL,
                    CONSTRAINT "PK_ProductResearchBudgets" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_ProductResearchBudgets_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_ProductResearchBudgets_ProductTypes_ProductTypeId" FOREIGN KEY ("ProductTypeId") REFERENCES "ProductTypes" ("Id") ON DELETE CASCADE
                )
                """,
            CreateProductResearchBudgetsCompanyIndexSql:
                "CREATE INDEX IF NOT EXISTS \"IX_ProductResearchBudgets_CompanyId\" ON \"ProductResearchBudgets\" (\"CompanyId\")",
            CreateProductResearchBudgetsProductIndexSql:
                "CREATE INDEX IF NOT EXISTS \"IX_ProductResearchBudgets_ProductTypeId\" ON \"ProductResearchBudgets\" (\"ProductTypeId\")",
            CreateBankDepositsTableSql:
                """
                CREATE TABLE IF NOT EXISTS "BankDeposits" (
                    "Id" TEXT NOT NULL,
                    "BankBuildingId" TEXT NOT NULL,
                    "DepositorCompanyId" TEXT NOT NULL,
                    "Amount" TEXT NOT NULL,
                    "DepositInterestRatePercent" TEXT NOT NULL,
                    "IsBaseCapital" INTEGER NOT NULL,
                    "IsActive" INTEGER NOT NULL,
                    "DepositedAtTick" INTEGER NOT NULL,
                    "DepositedAtUtc" TEXT NOT NULL,
                    "WithdrawnAtTick" INTEGER NULL,
                    "WithdrawnAtUtc" TEXT NULL,
                    "TotalInterestPaid" TEXT NOT NULL DEFAULT 0,
                    CONSTRAINT "PK_BankDeposits" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_BankDeposits_Buildings_BankBuildingId" FOREIGN KEY ("BankBuildingId") REFERENCES "Buildings" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_BankDeposits_Companies_DepositorCompanyId" FOREIGN KEY ("DepositorCompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT
                )
                """,
            CreateBankDepositsByBankIndexSql:
                "CREATE INDEX IF NOT EXISTS \"IX_BankDeposits_BankBuildingId_IsActive\" ON \"BankDeposits\" (\"BankBuildingId\", \"IsActive\")",
            CreateBankDepositsByDepositorIndexSql:
                "CREATE INDEX IF NOT EXISTS \"IX_BankDeposits_DepositorCompanyId_IsActive\" ON \"BankDeposits\" (\"DepositorCompanyId\", \"IsActive\")",
            CreateLoansCollateralIndexSql:
                "CREATE INDEX IF NOT EXISTS \"IX_Loans_CollateralBuildingId\" ON \"Loans\" (\"CollateralBuildingId\")",
            CreateLoansCollateralForeignKeySql: string.Empty);
    }
}
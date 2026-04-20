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

            if (await TableExistsAsync(connection, dialect, "Players"))
            {
                await EnsureColumnAsync(connection, dialect, "Players", "PersonalTaxReserve", dialect.RequiredDecimalDefaultZero);
            }

            if (!await TableExistsAsync(connection, dialect, "ProductResearchBudgets"))
            {
                await ExecuteNonQueryAsync(connection, dialect.CreateProductResearchBudgetsTableSql);
            }

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

            await EnsureColumnAsync(connection, dialect, "Buildings", "BaseCapitalDeposited", dialect.RequiredBooleanDefaultFalse);
            await EnsureColumnAsync(connection, dialect, "Buildings", "DepositInterestRatePercent", dialect.NullableInterestRate);
            await EnsureColumnAsync(connection, dialect, "Buildings", "LendingInterestRatePercent", dialect.NullableInterestRate);
            await EnsureColumnAsync(connection, dialect, "Buildings", "TotalDeposits", dialect.RequiredMoneyDefaultZero);
            await EnsureColumnAsync(connection, dialect, "Buildings", "CentralBankDebt", dialect.RequiredDecimalDefaultZero);

            if (await TableExistsAsync(connection, dialect, "BankDeposits"))
            {
                await EnsureColumnAsync(connection, dialect, "BankDeposits", "TotalInterestPaid", dialect.RequiredDecimal4DefaultZero);
            }
            else
            {
                await ExecuteNonQueryAsync(connection, dialect.CreateBankDepositsTableSql);
            }

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

            if (await TableExistsAsync(connection, dialect, "BuildingUnits"))
            {
                await EnsureColumnAsync(connection, dialect, "BuildingUnits", "IndustryCategory", dialect.NullableShortText);
            }

            if (await TableExistsAsync(connection, dialect, "BuildingConfigurationPlanUnits"))
            {
                await EnsureColumnAsync(connection, dialect, "BuildingConfigurationPlanUnits", "IndustryCategory", dialect.NullableShortText);
            }

            if (await TableExistsAsync(connection, dialect, "Loans"))
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

            // Ensure CityWeatherForecasts table exists (added in AddCityWeatherForecast migration).
            if (!await TableExistsAsync(connection, dialect, "CityWeatherForecasts"))
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
using Api.Configuration;
using Api.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Tests;

public sealed class DatabaseMigrationBootstrapTests
{
    private const string PreBankingMigration = "20260415025150_AddProductResearchBudget";

    [Fact]
    public async Task StartupWithLegacyDatabaseMissingHistory_RepairsBankingSchemaBeforeBaselining()
    {
        var dbPath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(dbPath);

            await using (var legacyCtx = new AppDbContext(options))
            {
                await legacyCtx.Database.MigrateAsync(PreBankingMigration);
                await DropMigrationHistoryAsync(legacyCtx);
            }

            await using (var upgradeCtx = new AppDbContext(options))
            {
                await CreateInitializer(upgradeCtx).InitializeAsync();
            }

            await using (var verifyCtx = new AppDbContext(options))
            {
                await AssertBankingEraArtifactsExistAsync(verifyCtx);
                await AssertMigrationHistoryCountAsync(verifyCtx, verifyCtx.Database.GetMigrations().Count());
            }

            await using (var restartCtx = new AppDbContext(options))
            {
                await CreateInitializer(restartCtx).InitializeAsync();
            }
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task StartupWithMisbaselinedDatabase_RepairsMissingBankingEraArtifacts()
    {
        var dbPath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(dbPath);

            await using (var legacyCtx = new AppDbContext(options))
            {
                await legacyCtx.Database.MigrateAsync(PreBankingMigration);
                await ReplaceMigrationHistoryWithCurrentHeadAsync(legacyCtx);
            }

            await using (var upgradeCtx = new AppDbContext(options))
            {
                await CreateInitializer(upgradeCtx).InitializeAsync();
            }

            await using var verifyCtx = new AppDbContext(options);
            await AssertBankingEraArtifactsExistAsync(verifyCtx);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    private static DbContextOptions<AppDbContext> CreateOptions(string dbPath) =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

    private static AppDbInitializer CreateInitializer(AppDbContext dbContext) =>
        new(
            dbContext,
            Options.Create(new SeedDataOptions
            {
                AdminEmail = "admin@migration-test.local",
                AdminDisplayName = "Migration Test Admin",
                AdminPassword = "TestPassword123!"
            }));

    private static async Task DropMigrationHistoryAsync(AppDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS \"__EFMigrationsHistory\"");
    }

    private static async Task ReplaceMigrationHistoryWithCurrentHeadAsync(AppDbContext dbContext)
    {
        var productVersion =
            typeof(DbContext).Assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), inherit: false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                .SingleOrDefault()
                ?.InformationalVersion
            ?? "unknown";

        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM \"__EFMigrationsHistory\"");

        var connection = dbContext.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        try
        {
            foreach (var migrationId in dbContext.Database.GetMigrations())
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES (@migrationId, @productVersion)";

                AddParameter(command, "@migrationId", migrationId);
                AddParameter(command, "@productVersion", productVersion);
                await command.ExecuteNonQueryAsync();
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

    private static async Task AssertBankingEraArtifactsExistAsync(AppDbContext dbContext)
    {
        await AssertColumnExistsAsync(dbContext, "Buildings", "BaseCapitalDeposited");
        await AssertColumnExistsAsync(dbContext, "Buildings", "DepositInterestRatePercent");
        await AssertColumnExistsAsync(dbContext, "Buildings", "LendingInterestRatePercent");
        await AssertColumnExistsAsync(dbContext, "Buildings", "TotalDeposits");
        await AssertColumnExistsAsync(dbContext, "Buildings", "CentralBankDebt");

        await AssertTableExistsAsync(dbContext, "BankDeposits");
        await AssertColumnExistsAsync(dbContext, "BankDeposits", "TotalInterestPaid");
        await AssertIndexExistsAsync(dbContext, "BankDeposits", "IX_BankDeposits_BankBuildingId_IsActive");
        await AssertIndexExistsAsync(dbContext, "BankDeposits", "IX_BankDeposits_DepositorCompanyId_IsActive");

        await AssertColumnExistsAsync(dbContext, "BuildingUnits", "IndustryCategory");
        await AssertColumnExistsAsync(dbContext, "BuildingConfigurationPlanUnits", "IndustryCategory");

        await AssertColumnExistsAsync(dbContext, "Loans", "CollateralAppraisedValue");
        await AssertColumnExistsAsync(dbContext, "Loans", "CollateralBuildingId");
        await AssertIndexExistsAsync(dbContext, "Loans", "IX_Loans_CollateralBuildingId");
    }

    private static async Task AssertTableExistsAsync(AppDbContext dbContext, string tableName)
    {
        var exists = await ExecuteScalarLongAsync(
            dbContext,
            "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = @tableName",
            ("@tableName", tableName));

        Assert.True(exists > 0, $"Expected table '{tableName}' to exist.");
    }

    private static async Task AssertColumnExistsAsync(AppDbContext dbContext, string tableName, string columnName)
    {
        var exists = await ExecuteScalarLongAsync(
            dbContext,
            $"SELECT COUNT(1) FROM pragma_table_info('{tableName.Replace("'", "''")}') WHERE name = @columnName",
            ("@columnName", columnName));

        Assert.True(exists > 0, $"Expected column '{tableName}.{columnName}' to exist.");
    }

    private static async Task AssertIndexExistsAsync(AppDbContext dbContext, string tableName, string indexName)
    {
        var exists = await ExecuteScalarLongAsync(
            dbContext,
            "SELECT COUNT(1) FROM sqlite_master WHERE type = 'index' AND tbl_name = @tableName AND name = @indexName",
            ("@tableName", tableName),
            ("@indexName", indexName));

        Assert.True(exists > 0, $"Expected index '{indexName}' on table '{tableName}' to exist.");
    }

    private static async Task AssertMigrationHistoryCountAsync(AppDbContext dbContext, int expectedCount)
    {
        var actualCount = await ExecuteScalarLongAsync(dbContext, "SELECT COUNT(1) FROM \"__EFMigrationsHistory\"");
        Assert.Equal(expectedCount, (int)actualCount);
    }

    private static async Task<long> ExecuteScalarLongAsync(
        AppDbContext dbContext,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        var connection = dbContext.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            foreach (var (name, value) in parameters)
            {
                AddParameter(command, name, value);
            }

            return Convert.ToInt64(await command.ExecuteScalarAsync() ?? 0L);
        }
        finally
        {
            if (!wasOpen)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string CreateDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"capitalism-migration-bootstrap-{Guid.NewGuid():N}.db");

    private static void DeleteDatabaseFiles(string dbPath)
    {
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var path = dbPath + suffix;
            if (File.Exists(path))
            {
                TryDeleteFileWithRetry(path);
            }
        }
    }

    private static void TryDeleteFileWithRetry(string path)
    {
        const int maxAttempts = 10;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                Thread.Sleep(100);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
    }
}
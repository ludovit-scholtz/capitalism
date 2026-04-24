using Api.Configuration;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace Api.Tests;

public sealed class DatabaseMigrationBootstrapTests
{
    private const string PreBankingMigration = "20260415025150_AddProductResearchBudget";
    private const string AddMediaHouseContentBudgetPerTickMigration = "20260421070000_AddMediaHouseContentBudgetPerTick";
    private const string LegacySqlitePostgresTailMigration = "20260417135125_AddLoanCollateral";
    private const string PreCompanyCurrencyRemovalMigration = "20260423025526_AddBankAccountsAndBuildingFunding";
    private const string PrePlayerPersonalCashRemovalMigration = "20260423205443_RemoveCompanyCurrencyCode";
    private const string RemovePlayerPersonalCashMigration = "20260423221000_RemovePlayerPersonalCash";

    [Fact]
    public void ShouldRepairSchemaArtifact_SkipsPendingPostgresNativeMigration()
    {
        var pendingMigrations = new HashSet<string>(StringComparer.Ordinal)
        {
            AddMediaHouseContentBudgetPerTickMigration
        };

        var shouldRepair = AppDbInitializer.ShouldRepairSchemaArtifact(
            AddMediaHouseContentBudgetPerTickMigration,
            pendingMigrations);

        Assert.False(shouldRepair);
    }

    [Fact]
    public void ShouldRepairSchemaArtifact_KeepsHydratedLegacySqliteTailRepairEnabled()
    {
        var pendingMigrations = new HashSet<string>(StringComparer.Ordinal)
        {
            LegacySqlitePostgresTailMigration
        };

        var shouldRepair = AppDbInitializer.ShouldRepairSchemaArtifact(
            LegacySqlitePostgresTailMigration,
            pendingMigrations);

        Assert.True(shouldRepair);
    }

    [Fact]
    public void ShouldRepairSchemaArtifact_SkipsPendingPlayerPersonalCashRemovalMigration()
    {
        var pendingMigrations = new HashSet<string>(StringComparer.Ordinal)
        {
            RemovePlayerPersonalCashMigration
        };

        var shouldRepair = AppDbInitializer.ShouldRepairSchemaArtifact(
            RemovePlayerPersonalCashMigration,
            pendingMigrations);

        Assert.False(shouldRepair);
    }

    [Fact]
    public void ShouldRepairSchemaArtifact_AllowsCompletedPlayerPersonalCashRemovalRepair()
    {
        var shouldRepair = AppDbInitializer.ShouldRepairSchemaArtifact(
            RemovePlayerPersonalCashMigration,
            new HashSet<string>(StringComparer.Ordinal));

        Assert.True(shouldRepair);
    }

    /// <summary>
    /// Regression test for the "ContentBudgetPerTick duplicate-column" startup failure
    /// (42701: column already exists).
    ///
    /// Scenario: a previous build's repair code pre-created the Buildings.ContentBudgetPerTick
    /// column on a PostgreSQL database before the migration was applied, leaving the DB in a
    /// broken state (column exists, migration still pending).  On the SQLite test path the same
    /// broken state is simulated: column manually added, migration record removed from history.
    /// Startup must succeed and the column must remain in place.
    ///
    /// On PostgreSQL (production) the recovery relies on the migration using
    /// IF NOT EXISTS so MigrateAsync can apply it without raising 42701.
    /// On SQLite (tests) the recovery goes through the EnsureCreated + repair + baseline path
    /// where EnsureColumnAsync is idempotent, so the pre-created column is silently accepted.
    /// </summary>
    [Fact]
    public async Task StartupWithPreCreatedContentBudgetColumn_SucceedsWithoutDuplicateColumnError()
    {
        var dbPath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(dbPath);

            // Step 1: Build a database at the schema one migration before ContentBudgetPerTick.
            await using (var legacyCtx = new AppDbContext(options))
            {
                await legacyCtx.Database.MigrateAsync("20260421054822_AddMediaHouseContentValue");
            }

            // Step 2: Manually add the ContentBudgetPerTick column — simulating what the
            // schema-repair bug did: repair code ran before MigrateAsync, pre-creating
            // the column while the migration was still pending in __EFMigrationsHistory.
            await using (var damagedCtx = new AppDbContext(options))
            {
                await damagedCtx.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE \"Buildings\" ADD COLUMN \"ContentBudgetPerTick\" TEXT");
            }

            // Step 3: Restart onto the new build.  Must complete without throwing a
            // duplicate-column error (42701 on PostgreSQL, or equivalent on SQLite).
            await using (var upgradeCtx = new AppDbContext(options))
            {
                var exception = await Record.ExceptionAsync(
                    () => CreateInitializer(upgradeCtx).InitializeAsync());

                Assert.Null(exception);
            }

            // Step 4: Verify the column is still present after startup.
            await using var verifyCtx = new AppDbContext(options);
            await AssertColumnExistsAsync(verifyCtx, "Buildings", "ContentBudgetPerTick");
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

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

    [Fact]
    public async Task UpgradeFromPreRemovalSchema_DropsCompanyCurrencyColumn()
    {
        var dbPath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(dbPath);
            var migrationOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                .Options;

            await using (var legacyCtx = new AppDbContext(options))
            {
                await legacyCtx.Database.MigrateAsync(PreCompanyCurrencyRemovalMigration);
                await AssertColumnExistsAsync(legacyCtx, "Companies", "CurrencyCode");
            }

            // SQLite test startup intentionally uses EnsureCreated + baseline instead of replaying
            // migrations, so validate the actual migration step directly here.
            await using (var upgradeCtx = new AppDbContext(migrationOptions))
            {
                await upgradeCtx.Database.MigrateAsync();
            }

            await using var verifyCtx = new AppDbContext(options);
            await AssertColumnMissingAsync(verifyCtx, "Companies", "CurrencyCode");
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task StartupWithLegacyDatabaseMissingHistory_BackfillsPlayerSettlementAccountFromPersonalCash()
    {
        var dbPath = CreateDatabasePath();
        var playerId = Guid.NewGuid();
        const decimal expectedBalance = 12_345.67m;

        try
        {
            var options = CreateOptions(dbPath);

            await using (var legacyCtx = new AppDbContext(options))
            {
                await legacyCtx.Database.MigrateAsync(PrePlayerPersonalCashRemovalMigration);
                await SeedLegacyPlayerPersonalCashAsync(legacyCtx, playerId, expectedBalance);
                await DropMigrationHistoryAsync(legacyCtx);
            }

            await using (var upgradeCtx = new AppDbContext(options))
            {
                await CreateInitializer(upgradeCtx).InitializeAsync();
            }

            await using var verifyCtx = new AppDbContext(options);
            await AssertColumnExistsAsync(verifyCtx, "BankAccounts", "PlayerId");
            await AssertPlayerSettlementBalanceAsync(verifyCtx, playerId, expectedBalance);
            await AssertMigrationHistoryCountAsync(verifyCtx, verifyCtx.Database.GetMigrations().Count());
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task StartupWithMisbaselinedDatabase_BackfillsPlayerSettlementAccountFromPersonalCash()
    {
        var dbPath = CreateDatabasePath();
        var playerId = Guid.NewGuid();
        const decimal expectedBalance = 4_321.09m;

        try
        {
            var options = CreateOptions(dbPath);

            await using (var legacyCtx = new AppDbContext(options))
            {
                await legacyCtx.Database.MigrateAsync(PrePlayerPersonalCashRemovalMigration);
                await SeedLegacyPlayerPersonalCashAsync(legacyCtx, playerId, expectedBalance);
                await ReplaceMigrationHistoryWithCurrentHeadAsync(legacyCtx);
            }

            await using (var upgradeCtx = new AppDbContext(options))
            {
                await CreateInitializer(upgradeCtx).InitializeAsync();
            }

            await using var verifyCtx = new AppDbContext(options);
            await AssertColumnExistsAsync(verifyCtx, "BankAccounts", "PlayerId");
            await AssertPlayerSettlementBalanceAsync(verifyCtx, playerId, expectedBalance);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task UpgradeFromPreRemovalSchema_MovesPersonalCashIntoPlayerSettlementAccountAndDropsColumn()
    {
        var dbPath = CreateDatabasePath();
        var playerId = Guid.NewGuid();
        const decimal expectedBalance = 9_876.54m;

        try
        {
            var options = CreateOptions(dbPath);
            var migrationOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                .Options;

            await using (var legacyCtx = new AppDbContext(options))
            {
                await legacyCtx.Database.MigrateAsync(PrePlayerPersonalCashRemovalMigration);
                await AssertColumnExistsAsync(legacyCtx, "Players", "PersonalCash");
                await SeedLegacyPlayerPersonalCashAsync(legacyCtx, playerId, expectedBalance);
            }

            await using (var upgradeCtx = new AppDbContext(migrationOptions))
            {
                await upgradeCtx.Database.MigrateAsync();
            }

            await using var verifyCtx = new AppDbContext(options);
            await AssertColumnMissingAsync(verifyCtx, "Players", "PersonalCash");
            await AssertColumnExistsAsync(verifyCtx, "BankAccounts", "PlayerId");
            await AssertPlayerSettlementBalanceAsync(verifyCtx, playerId, expectedBalance);
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
            }),
            TestHelpers.CreateFallbackNbsService());

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

    private static async Task AssertColumnMissingAsync(AppDbContext dbContext, string tableName, string columnName)
    {
        var exists = await ExecuteScalarLongAsync(
            dbContext,
            $"SELECT COUNT(1) FROM pragma_table_info('{tableName.Replace("'", "''")}') WHERE name = @columnName",
            ("@columnName", columnName));

        Assert.Equal(0, exists);
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

    private static async Task AssertPlayerSettlementBalanceAsync(AppDbContext dbContext, Guid playerId, decimal expectedBalance)
    {
        var actualBalance = await dbContext.BankAccounts
            .Where(account => account.PlayerId == playerId && account.CurrencyCode == "EUR")
            .Select(account => account.Balance)
            .SingleAsync();

        Assert.Equal(expectedBalance, actualBalance);
    }

    private static async Task SeedLegacyPlayerPersonalCashAsync(AppDbContext dbContext, Guid playerId, decimal personalCash)
    {
        dbContext.Players.Add(new Player
        {
            Id = playerId,
            Email = $"legacy-{playerId:N}@migration-test.local",
            DisplayName = "Legacy Personal Cash Player",
            PasswordHash = "seeded-hash",
            Role = PlayerRole.Player,
            ActiveAccountType = AccountContextType.Person,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"Players\" SET \"PersonalCash\" = {personalCash} WHERE \"Id\" = {playerId}");
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
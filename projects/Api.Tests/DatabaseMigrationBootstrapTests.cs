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
    private const string PrePlayerCurrencyBalanceRemovalMigration = "20260423221000_RemovePlayerPersonalCash";
    private const string RemovePlayerCurrencyBalancesMigration = "20260424084149_RemovePlayerCurrencyBalances";
    private const string PreBankDepositRemovalMigration = "20260423221000_RemovePlayerPersonalCash";
    private const string RemoveBankDepositsMigration = "20260424091612_RemoveBankDeposits";

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

    [Fact]
    public void ShouldRepairSchemaArtifact_SkipsPendingPlayerCurrencyBalancesRemovalMigration()
    {
        var pendingMigrations = new HashSet<string>(StringComparer.Ordinal)
        {
            RemovePlayerCurrencyBalancesMigration
        };

        var shouldRepair = AppDbInitializer.ShouldRepairSchemaArtifact(
            RemovePlayerCurrencyBalancesMigration,
            pendingMigrations);

        Assert.False(shouldRepair);
    }

    [Fact]
    public void ShouldRepairSchemaArtifact_AllowsCompletedPlayerCurrencyBalancesRemovalRepair()
    {
        var shouldRepair = AppDbInitializer.ShouldRepairSchemaArtifact(
            RemovePlayerCurrencyBalancesMigration,
            new HashSet<string>(StringComparer.Ordinal));

        Assert.True(shouldRepair);
    }

    [Fact]
    public void ShouldRepairSchemaArtifact_SkipsPendingBankDepositsRemovalMigration()
    {
        var pendingMigrations = new HashSet<string>(StringComparer.Ordinal)
        {
            RemoveBankDepositsMigration
        };

        var shouldRepair = AppDbInitializer.ShouldRepairSchemaArtifact(
            RemoveBankDepositsMigration,
            pendingMigrations);

        Assert.False(shouldRepair);
    }

    [Fact]
    public void ShouldRepairSchemaArtifact_AllowsCompletedBankDepositsRemovalRepair()
    {
        var shouldRepair = AppDbInitializer.ShouldRepairSchemaArtifact(
            RemoveBankDepositsMigration,
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

    [Fact]
    public async Task StartupWithLegacyDatabaseMissingHistory_BackfillsPlayerCurrencyBalancesIntoPlayerBankAccounts()
    {
        var dbPath = CreateDatabasePath();
        var playerId = Guid.NewGuid();
        const string currencyCode = "CZK";
        const decimal expectedBalance = 123_456.78m;

        try
        {
            var options = CreateOptions(dbPath);

            await using (var legacyCtx = new AppDbContext(options))
            {
                await legacyCtx.Database.MigrateAsync(PrePlayerCurrencyBalanceRemovalMigration);
                await SeedLegacyPlayerCurrencyBalanceAsync(legacyCtx, playerId, currencyCode, expectedBalance);
                await DropMigrationHistoryAsync(legacyCtx);
            }

            await using (var upgradeCtx = new AppDbContext(options))
            {
                await CreateInitializer(upgradeCtx).InitializeAsync();
            }

            await using var verifyCtx = new AppDbContext(options);
            await AssertPlayerTrackedBalanceAsync(verifyCtx, playerId, currencyCode, expectedBalance);
            await AssertTableMissingAsync(verifyCtx, "PlayerCurrencyBalances");
            await AssertMigrationHistoryCountAsync(verifyCtx, verifyCtx.Database.GetMigrations().Count());
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task StartupWithMisbaselinedDatabase_BackfillsPlayerCurrencyBalancesIntoPlayerBankAccounts()
    {
        var dbPath = CreateDatabasePath();
        var playerId = Guid.NewGuid();
        const string currencyCode = "USD";
        const decimal expectedBalance = 4_567.89m;

        try
        {
            var options = CreateOptions(dbPath);

            await using (var legacyCtx = new AppDbContext(options))
            {
                await legacyCtx.Database.MigrateAsync(PrePlayerCurrencyBalanceRemovalMigration);
                await SeedLegacyPlayerCurrencyBalanceAsync(legacyCtx, playerId, currencyCode, expectedBalance);
                await ReplaceMigrationHistoryWithCurrentHeadAsync(legacyCtx);
            }

            await using (var upgradeCtx = new AppDbContext(options))
            {
                await CreateInitializer(upgradeCtx).InitializeAsync();
            }

            await using var verifyCtx = new AppDbContext(options);
            await AssertPlayerTrackedBalanceAsync(verifyCtx, playerId, currencyCode, expectedBalance);
            await AssertTableMissingAsync(verifyCtx, "PlayerCurrencyBalances");
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task UpgradeFromPreRemovalSchema_MovesPlayerCurrencyBalancesIntoPlayerBankAccountsAndDropsTable()
    {
        var dbPath = CreateDatabasePath();
        var playerId = Guid.NewGuid();
        const string currencyCode = "GBP";
        const decimal expectedBalance = 7_654.32m;

        try
        {
            var options = CreateOptions(dbPath);
            var migrationOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                .Options;

            await using (var legacyCtx = new AppDbContext(options))
            {
                await legacyCtx.Database.MigrateAsync(PrePlayerCurrencyBalanceRemovalMigration);
                await SeedLegacyPlayerCurrencyBalanceAsync(legacyCtx, playerId, currencyCode, expectedBalance);
                await AssertTableExistsAsync(legacyCtx, "PlayerCurrencyBalances");
            }

            await using (var upgradeCtx = new AppDbContext(migrationOptions))
            {
                await upgradeCtx.Database.MigrateAsync();
            }

            await using var verifyCtx = new AppDbContext(options);
            await AssertTableMissingAsync(verifyCtx, "PlayerCurrencyBalances");
            await AssertPlayerTrackedBalanceAsync(verifyCtx, playerId, currencyCode, expectedBalance);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task StartupWithLegacyDatabaseMissingHistory_BackfillsBankDepositsIntoBankAccountsAndDropsTable()
    {
        var dbPath = CreateDatabasePath();
        const decimal expectedBalance = 654_321.98m;
        const decimal expectedRate = 4.25m;
        const long depositedAtTick = 73;
        const decimal expectedInterestPaid = 123.45m;

        try
        {
            var options = CreateOptions(dbPath);

            Guid depositId;
            Guid depositorCompanyId;
            Guid bankBuildingId;

            await using (var legacyCtx = new AppDbContext(options))
            {
                await legacyCtx.Database.MigrateAsync(PreBankDepositRemovalMigration);
                (depositId, depositorCompanyId, bankBuildingId) = await SeedLegacyBankDepositAsync(
                    legacyCtx,
                    currencyCode: "CZK",
                    amount: expectedBalance,
                    depositInterestRatePercent: expectedRate,
                    isBaseCapital: false,
                    depositedAtTick: depositedAtTick,
                    withdrawnAtTick: null,
                    totalInterestPaid: expectedInterestPaid);
                await DropMigrationHistoryAsync(legacyCtx);
            }

            await using (var upgradeCtx = new AppDbContext(options))
            {
                await CreateInitializer(upgradeCtx).InitializeAsync();
            }

            await using var verifyCtx = new AppDbContext(options);
            await AssertMigratedBankDepositAccountAsync(
                verifyCtx,
                depositId,
                depositorCompanyId,
                bankBuildingId,
                expectedCurrencyCode: "CZK",
                expectedBalance: expectedBalance,
                expectedInterestRatePercent: expectedRate,
                expectedIsBaseCapital: false,
                expectedDepositedAtTick: depositedAtTick,
                expectedClosedAtTick: null,
                expectedClosedAtUtc: null,
                expectedInterestPaid: expectedInterestPaid);
            await AssertTableMissingAsync(verifyCtx, "BankDeposits");
            await AssertMigrationHistoryCountAsync(verifyCtx, verifyCtx.Database.GetMigrations().Count());
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task StartupWithMisbaselinedDatabase_BackfillsBankDepositsIntoBankAccountsAndDropsTable()
    {
        var dbPath = CreateDatabasePath();
        const decimal expectedBalance = 9_999.11m;
        const decimal expectedRate = 3.5m;
        const long depositedAtTick = 12;

        try
        {
            var options = CreateOptions(dbPath);

            Guid depositId;
            Guid depositorCompanyId;
            Guid bankBuildingId;

            await using (var legacyCtx = new AppDbContext(options))
            {
                await legacyCtx.Database.MigrateAsync(PreBankDepositRemovalMigration);
                (depositId, depositorCompanyId, bankBuildingId) = await SeedLegacyBankDepositAsync(
                    legacyCtx,
                    currencyCode: "USD",
                    amount: expectedBalance,
                    depositInterestRatePercent: expectedRate,
                    isBaseCapital: true,
                    depositedAtTick: depositedAtTick,
                    withdrawnAtTick: null,
                    totalInterestPaid: 0m);
                await ReplaceMigrationHistoryWithCurrentHeadAsync(legacyCtx);
            }

            await using (var upgradeCtx = new AppDbContext(options))
            {
                await CreateInitializer(upgradeCtx).InitializeAsync();
            }

            await using var verifyCtx = new AppDbContext(options);
            await AssertMigratedBankDepositAccountAsync(
                verifyCtx,
                depositId,
                depositorCompanyId,
                bankBuildingId,
                expectedCurrencyCode: "USD",
                expectedBalance: expectedBalance,
                expectedInterestRatePercent: expectedRate,
                expectedIsBaseCapital: true,
                expectedDepositedAtTick: depositedAtTick,
                expectedClosedAtTick: null,
                expectedClosedAtUtc: null,
                expectedInterestPaid: 0m);
            await AssertTableMissingAsync(verifyCtx, "BankDeposits");
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task UpgradeFromPreRemovalSchema_MovesBankDepositsIntoBankAccountsAndDropsTable()
    {
        var dbPath = CreateDatabasePath();
        const decimal expectedBalance = 44_001.25m;
        const decimal expectedRate = 2.75m;
        const long depositedAtTick = 188;
        const long withdrawnAtTick = 222;
        var withdrawnAtUtc = new DateTime(2026, 04, 24, 9, 30, 0, DateTimeKind.Utc);
        const decimal expectedInterestPaid = 88.12m;

        try
        {
            var options = CreateOptions(dbPath);
            var migrationOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                .Options;

            Guid depositId;
            Guid depositorCompanyId;
            Guid bankBuildingId;

            await using (var legacyCtx = new AppDbContext(options))
            {
                await legacyCtx.Database.MigrateAsync(PreBankDepositRemovalMigration);
                (depositId, depositorCompanyId, bankBuildingId) = await SeedLegacyBankDepositAsync(
                    legacyCtx,
                    currencyCode: "GBP",
                    amount: expectedBalance,
                    depositInterestRatePercent: expectedRate,
                    isBaseCapital: false,
                    depositedAtTick: depositedAtTick,
                    withdrawnAtTick: withdrawnAtTick,
                    withdrawnAtUtc: withdrawnAtUtc,
                    totalInterestPaid: expectedInterestPaid);
                await AssertTableExistsAsync(legacyCtx, "BankDeposits");
            }

            await using (var upgradeCtx = new AppDbContext(migrationOptions))
            {
                await upgradeCtx.Database.MigrateAsync();
            }

            await using var verifyCtx = new AppDbContext(options);
            await AssertTableMissingAsync(verifyCtx, "BankDeposits");
            await AssertMigratedBankDepositAccountAsync(
                verifyCtx,
                depositId,
                depositorCompanyId,
                bankBuildingId,
                expectedCurrencyCode: "GBP",
                expectedBalance: expectedBalance,
                expectedInterestRatePercent: expectedRate,
                expectedIsBaseCapital: false,
                expectedDepositedAtTick: depositedAtTick,
                expectedClosedAtTick: withdrawnAtTick,
                expectedClosedAtUtc: withdrawnAtUtc,
                expectedInterestPaid: expectedInterestPaid);
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

        await AssertColumnExistsAsync(dbContext, "BankAccounts", "BankBuildingId");
        await AssertColumnExistsAsync(dbContext, "BankAccounts", "DepositInterestRatePercent");
        await AssertColumnExistsAsync(dbContext, "BankAccounts", "DepositedAtTick");
        await AssertColumnExistsAsync(dbContext, "BankAccounts", "ClosedAtTick");
        await AssertColumnExistsAsync(dbContext, "BankAccounts", "ClosedAtUtc");
        await AssertColumnExistsAsync(dbContext, "BankAccounts", "IsBaseCapitalDeposit");
        await AssertColumnExistsAsync(dbContext, "BankAccounts", "TotalInterestPaid");
        await AssertIndexExistsAsync(dbContext, "BankAccounts", "IX_BankAccounts_BankBuildingId_ClosedAtUtc");
        await AssertIndexExistsAsync(dbContext, "BankAccounts", "IX_BankAccounts_CompanyId_BankBuildingId_ClosedAtUtc");
        await AssertTableMissingAsync(dbContext, "BankDeposits");

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

    private static async Task AssertTableMissingAsync(AppDbContext dbContext, string tableName)
    {
        var exists = await ExecuteScalarLongAsync(
            dbContext,
            "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = @tableName",
            ("@tableName", tableName));

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

    private static async Task AssertPlayerTrackedBalanceAsync(AppDbContext dbContext, Guid playerId, string currencyCode, decimal expectedBalance)
    {
        var actualBalance = await dbContext.BankAccounts
            .Where(account => account.PlayerId == playerId && account.CurrencyCode == currencyCode)
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

    private static async Task SeedLegacyPlayerCurrencyBalanceAsync(AppDbContext dbContext, Guid playerId, string currencyCode, decimal balance)
    {
        dbContext.Players.Add(new Player
        {
            Id = playerId,
            Email = $"legacy-fx-{playerId:N}@migration-test.local",
            DisplayName = "Legacy Forex Balance Player",
            PasswordHash = "seeded-hash",
            Role = PlayerRole.Player,
            ActiveAccountType = AccountContextType.Person,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "PlayerCurrencyBalances" (
                "Id" TEXT NOT NULL,
                "PlayerId" TEXT NOT NULL,
                "CurrencyCode" TEXT NOT NULL,
                "Balance" TEXT NOT NULL DEFAULT '0',
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "PK_PlayerCurrencyBalances" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_PlayerCurrencyBalances_Players_PlayerId" FOREIGN KEY ("PlayerId") REFERENCES "Players" ("Id") ON DELETE CASCADE
            )
            """);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_PlayerCurrencyBalances_PlayerId_CurrencyCode\" ON \"PlayerCurrencyBalances\" (\"PlayerId\", \"CurrencyCode\")");

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"PlayerCurrencyBalances\" (\"Id\", \"PlayerId\", \"CurrencyCode\", \"Balance\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES ({Guid.NewGuid()}, {playerId}, {currencyCode}, {balance}, {DateTime.UtcNow}, {DateTime.UtcNow})");
    }

    private static async Task<(Guid DepositId, Guid DepositorCompanyId, Guid BankBuildingId)> SeedLegacyBankDepositAsync(
        AppDbContext dbContext,
        string currencyCode,
        decimal amount,
        decimal depositInterestRatePercent,
        bool isBaseCapital,
        long depositedAtTick,
        long? withdrawnAtTick,
        DateTime? withdrawnAtUtc = null,
        decimal totalInterestPaid = 0m)
    {
        var player = new Player
        {
            Id = Guid.NewGuid(),
            Email = $"legacy-bank-{Guid.NewGuid():N}@migration-test.local",
            DisplayName = "Legacy Bank Deposit Player",
            PasswordHash = "seeded-hash",
            Role = PlayerRole.Player,
            ActiveAccountType = AccountContextType.Person,
            CreatedAtUtc = DateTime.UtcNow,
        };

        var cities = new[]
        {
            new City { Id = Guid.NewGuid(), Name = "Bratislava", CountryCode = "SK", CurrencyCode = "EUR", Latitude = 48.1486, Longitude = 17.1077, Population = 475_000, AverageRentPerSqm = 14m, BaseSalaryPerManhour = 18m },
            new City { Id = Guid.NewGuid(), Name = "Prague", CountryCode = "CZ", CurrencyCode = "CZK", Latitude = 50.0755, Longitude = 14.4378, Population = 1_350_000, AverageRentPerSqm = 18m, BaseSalaryPerManhour = 22m },
            new City { Id = Guid.NewGuid(), Name = "Vienna", CountryCode = "AT", CurrencyCode = "EUR", Latitude = 48.2082, Longitude = 16.3738, Population = 1_900_000, AverageRentPerSqm = 22m, BaseSalaryPerManhour = 28m },
            new City { Id = Guid.NewGuid(), Name = "New York", CountryCode = "US", CurrencyCode = "USD", Latitude = 40.7128, Longitude = -74.0060, Population = 8_336_000, AverageRentPerSqm = 55m, BaseSalaryPerManhour = 35m },
            new City { Id = Guid.NewGuid(), Name = "London", CountryCode = "GB", CurrencyCode = "GBP", Latitude = 51.5074, Longitude = -0.1278, Population = 8_982_000, AverageRentPerSqm = 62m, BaseSalaryPerManhour = 32m },
            new City { Id = Guid.NewGuid(), Name = "Beijing", CountryCode = "CN", CurrencyCode = "CNY", Latitude = 39.9042, Longitude = 116.4074, Population = 21_540_000, AverageRentPerSqm = 30m, BaseSalaryPerManhour = 20m },
            new City { Id = Guid.NewGuid(), Name = "Delhi", CountryCode = "IN", CurrencyCode = "INR", Latitude = 28.6139, Longitude = 77.2090, Population = 32_000_000, AverageRentPerSqm = 8m, BaseSalaryPerManhour = 6m },
        };
        var city = cities.First(c => c.CurrencyCode == currencyCode);

        var bankCompany = new Company
        {
            Id = Guid.NewGuid(),
            Name = $"Legacy Bank {currencyCode}",
            PlayerId = player.Id,
            FoundedAtTick = 1,
            FoundedAtUtc = DateTime.UtcNow,
        };

        var depositorCompany = new Company
        {
            Id = Guid.NewGuid(),
            Name = $"Legacy Depositor {currencyCode}",
            PlayerId = player.Id,
            FoundedAtTick = 1,
            FoundedAtUtc = DateTime.UtcNow,
        };

        var bankBuilding = new Building
        {
            Id = Guid.NewGuid(),
            Name = $"Legacy Bank Building {currencyCode}",
            Type = BuildingType.Bank,
            CompanyId = bankCompany.Id,
            CityId = city.Id,
            BuiltAtUtc = DateTime.UtcNow,
            TotalAreaSqm = 100m,
        };

        dbContext.Add(player);
        dbContext.AddRange(cities);
        await dbContext.SaveChangesAsync();

        await InsertLegacyCompanyRowAsync(dbContext, bankCompany.Id, bankCompany.PlayerId, bankCompany.Name);
        await InsertLegacyCompanyRowAsync(dbContext, depositorCompany.Id, depositorCompany.PlayerId, depositorCompany.Name);

        dbContext.Add(bankBuilding);
        await dbContext.SaveChangesAsync();

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "PlayerCurrencyBalances" (
                "Id" TEXT NOT NULL,
                "PlayerId" TEXT NOT NULL,
                "CurrencyCode" TEXT NOT NULL,
                "Balance" TEXT NOT NULL DEFAULT '0',
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "PK_PlayerCurrencyBalances" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_PlayerCurrencyBalances_Players_PlayerId" FOREIGN KEY ("PlayerId") REFERENCES "Players" ("Id") ON DELETE CASCADE
            )
            """);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_PlayerCurrencyBalances_PlayerId_CurrencyCode\" ON \"PlayerCurrencyBalances\" (\"PlayerId\", \"CurrencyCode\")");

        var depositId = Guid.NewGuid();
        var depositedAtUtc = new DateTime(2026, 04, 24, 8, 0, 0, DateTimeKind.Utc);

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"BankDeposits\" (\"Id\", \"BankBuildingId\", \"DepositorCompanyId\", \"Amount\", \"DepositInterestRatePercent\", \"IsBaseCapital\", \"IsActive\", \"DepositedAtTick\", \"DepositedAtUtc\", \"WithdrawnAtTick\", \"WithdrawnAtUtc\", \"TotalInterestPaid\") VALUES ({depositId}, {bankBuilding.Id}, {depositorCompany.Id}, {amount}, {depositInterestRatePercent}, {isBaseCapital}, {withdrawnAtUtc is null}, {depositedAtTick}, {depositedAtUtc}, {withdrawnAtTick}, {withdrawnAtUtc}, {totalInterestPaid})");

        return (depositId, depositorCompany.Id, bankBuilding.Id);
    }

    private static async Task InsertLegacyCompanyRowAsync(
        AppDbContext dbContext,
        Guid companyId,
        Guid playerId,
        string name)
    {
        var companyColumns = await GetTableColumnsAsync(dbContext, "Companies");
        var available = new HashSet<string>(companyColumns, StringComparer.OrdinalIgnoreCase);

        var valuesByColumn = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = companyId,
            ["PlayerId"] = playerId,
            ["Name"] = name,
            ["Cash"] = 0m,
            ["CurrencyCode"] = "EUR",
            ["FoundedAtUtc"] = DateTime.UtcNow,
            ["FoundedAtTick"] = 1L,
            ["DividendPayoutRatio"] = 0.2m,
            ["TotalSharesIssued"] = 10_000m,
        };

        var targetColumns = valuesByColumn.Keys
            .Where(column => available.Contains(column))
            .ToList();

        if (!targetColumns.Contains("Id", StringComparer.OrdinalIgnoreCase)
            || !targetColumns.Contains("PlayerId", StringComparer.OrdinalIgnoreCase)
            || !targetColumns.Contains("Name", StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Legacy Companies table is missing required columns for test seeding.");
        }

        var connection = dbContext.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();

            var columnSql = string.Join(", ", targetColumns.Select(column => $"\"{column}\""));
            var parameterSql = string.Join(", ", targetColumns.Select((_, index) => $"@p{index}"));
            command.CommandText = $"INSERT INTO \"Companies\" ({columnSql}) VALUES ({parameterSql})";

            for (var index = 0; index < targetColumns.Count; index++)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@p{index}";
                parameter.Value = valuesByColumn[targetColumns[index]];
                command.Parameters.Add(parameter);
            }

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (!wasOpen)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<List<string>> GetTableColumnsAsync(AppDbContext dbContext, string tableName)
    {
        var columns = new List<string>();

        var connection = dbContext.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info('{tableName.Replace("'", "''")}')";

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(1));
            }
        }
        finally
        {
            if (!wasOpen)
            {
                await connection.CloseAsync();
            }
        }

        return columns;
    }

    private static async Task AssertMigratedBankDepositAccountAsync(
        AppDbContext dbContext,
        Guid depositId,
        Guid expectedCompanyId,
        Guid expectedBankBuildingId,
        string expectedCurrencyCode,
        decimal expectedBalance,
        decimal expectedInterestRatePercent,
        bool expectedIsBaseCapital,
        long expectedDepositedAtTick,
        long? expectedClosedAtTick,
        DateTime? expectedClosedAtUtc,
        decimal expectedInterestPaid)
    {
        var migratedAccount = await dbContext.BankAccounts.SingleAsync(account => account.Id == depositId);

        Assert.Equal(expectedCompanyId, migratedAccount.CompanyId);
        Assert.Equal(expectedBankBuildingId, migratedAccount.BankBuildingId);
        Assert.Equal(expectedCurrencyCode, migratedAccount.CurrencyCode);
        Assert.Equal(expectedBalance, migratedAccount.Balance);
        Assert.Equal(expectedInterestRatePercent, migratedAccount.DepositInterestRatePercent);
        Assert.Equal(expectedIsBaseCapital, migratedAccount.IsBaseCapitalDeposit);
        Assert.Equal(expectedDepositedAtTick, migratedAccount.DepositedAtTick);
        Assert.Equal(expectedClosedAtTick, migratedAccount.ClosedAtTick);
        Assert.Equal(expectedClosedAtUtc, migratedAccount.ClosedAtUtc);
        Assert.Equal(expectedInterestPaid, migratedAccount.TotalInterestPaid);
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
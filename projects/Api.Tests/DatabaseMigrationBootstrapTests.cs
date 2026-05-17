using Api.Configuration;
using Api.Data;
using Api.Data.Migrations;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace Api.Tests;

public sealed class DatabaseMigrationBootstrapTests
{
    [Fact]
    public void AllMigrationClasses_HaveEfMetadata()
    {
        var migrationsWithoutMetadata = typeof(AppDbContext).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false } && typeof(Migration).IsAssignableFrom(t))
            .Where(t => t.GetCustomAttribute<MigrationAttribute>() is null
                || t.GetCustomAttribute<DbContextAttribute>() is null)
            .Select(t => t.FullName)
            .OrderBy(name => name)
            .ToList();

        Assert.Empty(migrationsWithoutMetadata);
    }

    [Fact]
    public void ShouldRepairSchemaArtifact_SkipsPendingMigration()
    {
        var migrationId = "20260421070000_AddMediaHouseContentBudgetPerTick";
        var pendingMigrations = new HashSet<string>(StringComparer.Ordinal)
        {
            migrationId
        };

        var shouldRepair = AppDbInitializer.ShouldRepairSchemaArtifact(migrationId, pendingMigrations);

        Assert.False(shouldRepair);
    }

    [Fact]
    public void ShouldRepairSchemaArtifact_AllowsAppliedMigration()
    {
        var migrationId = "20260421070000_AddMediaHouseContentBudgetPerTick";
        var shouldRepair = AppDbInitializer.ShouldRepairSchemaArtifact(
            migrationId,
            new HashSet<string>(StringComparer.Ordinal));

        Assert.True(shouldRepair);
    }

    [Fact]
    public void AddCityScopedChatChannels_UsesConditionalLegacyChatForeignKeyDrop()
    {
        var migration = new AddCityScopedChatChannels();
        var migrationBuilder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var upMethod = typeof(AddCityScopedChatChannels).GetMethod(
            "Up",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(upMethod);

        upMethod!.Invoke(migration, [migrationBuilder]);

        Assert.DoesNotContain(
            migrationBuilder.Operations.OfType<DropForeignKeyOperation>(),
            operation => operation.Table == "ChatMessages"
                && operation.Name == "FK_ChatMessages_Players_PlayerId");

        var sqlOperation = Assert.Single(
            migrationBuilder.Operations.OfType<SqlOperation>(),
            operation => operation.Sql.Contains(
                "FK_ChatMessages_Players_PlayerId",
                StringComparison.Ordinal));

        Assert.Contains("DROP CONSTRAINT IF EXISTS", sqlOperation.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void AddCityScopedChatChannels_ConvertsLegacyAuthorPlayerIdToUuidBeforeBackfill()
    {
        var migration = new AddCityScopedChatChannels();
        var migrationBuilder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var upMethod = typeof(AddCityScopedChatChannels).GetMethod(
            "Up",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(upMethod);

        upMethod!.Invoke(migration, [migrationBuilder]);

        var operations = migrationBuilder.Operations.ToList();
        var conversionIndex = operations.FindIndex(
            operation => operation is SqlOperation sql
                && sql.Sql.Contains("ALTER COLUMN \"AuthorPlayerId\" TYPE uuid", StringComparison.Ordinal));
        var backfillIndex = operations.FindIndex(
            operation => operation is SqlOperation sql
                && sql.Sql.Contains("SET \"AuthorDisplayName\"", StringComparison.Ordinal));

        Assert.True(conversionIndex >= 0, "Expected a PostgreSQL AuthorPlayerId uuid conversion step.");
        Assert.True(backfillIndex >= 0, "Expected the AuthorDisplayName backfill SQL step.");
        Assert.True(
            conversionIndex < backfillIndex,
            "The AuthorPlayerId uuid conversion must run before the AuthorDisplayName backfill.");
    }

    [Fact]
    public async Task InitializeAsync_InMemory_IsIdempotent()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"migration-bootstrap-{Guid.NewGuid():N}")
            .Options;

        var seedOptions = Options.Create(new SeedDataOptions
        {
            AdminEmail = "admin@migration-test.local",
            AdminDisplayName = "Migration Test Admin",
            AdminPassword = "TestPassword123!"
        });

        await using (var firstRunContext = new AppDbContext(options))
        {
            var initializer = new AppDbInitializer(firstRunContext, seedOptions, TestHelpers.CreateFallbackNbsService());
            await initializer.InitializeAsync();
        }

        await using (var secondRunContext = new AppDbContext(options))
        {
            var initializer = new AppDbInitializer(secondRunContext, seedOptions, TestHelpers.CreateFallbackNbsService());
            await initializer.InitializeAsync();

            Assert.True(await secondRunContext.Players.AnyAsync());
            Assert.True(await secondRunContext.Cities.AnyAsync());
            Assert.True(await secondRunContext.ResourceTypes.AnyAsync());
            Assert.True(await secondRunContext.ProductTypes.AnyAsync());
        }
    }

    [Fact]
    public async Task InitializeAsync_BackfillsMissingPlnFxRate_WhenOtherRatesAlreadyExist()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"migration-bootstrap-fx-{Guid.NewGuid():N}")
            .Options;

        var seedOptions = Options.Create(new SeedDataOptions
        {
            AdminEmail = "admin@migration-test.local",
            AdminDisplayName = "Migration Test Admin",
            AdminPassword = "TestPassword123!"
        });

        await using (var firstRunContext = new AppDbContext(options))
        {
            var initializer = new AppDbInitializer(firstRunContext, seedOptions, TestHelpers.CreateFallbackNbsService());
            await initializer.InitializeAsync();
        }

        await using (var mutateContext = new AppDbContext(options))
        {
            var plnRates = await mutateContext.FxRates
                .Where(rate => rate.BaseCurrencyCode == "EUR" && rate.QuoteCurrencyCode == "PLN")
                .ToListAsync();

            Assert.NotEmpty(plnRates);

            mutateContext.FxRates.RemoveRange(plnRates);
            await mutateContext.SaveChangesAsync();
        }

        await using (var secondRunContext = new AppDbContext(options))
        {
            var initializer = new AppDbInitializer(secondRunContext, seedOptions, TestHelpers.CreateFallbackNbsService());
            await initializer.InitializeAsync();

            var plnRate = await secondRunContext.FxRates
                .AsNoTracking()
                .Where(rate => rate.BaseCurrencyCode == "EUR" && rate.QuoteCurrencyCode == "PLN")
                .OrderByDescending(rate => rate.RateDate)
                .FirstOrDefaultAsync();

            Assert.NotNull(plnRate);
            Assert.True(plnRate.Rate > 0m);
        }
    }
}

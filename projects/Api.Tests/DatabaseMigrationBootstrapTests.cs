using Api.Configuration;
using Api.Data;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Tests;

public sealed class DatabaseMigrationBootstrapTests
{
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
}

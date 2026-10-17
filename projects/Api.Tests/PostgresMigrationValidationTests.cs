using System.Data;
using Api.Configuration;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Api.Tests;

/// <summary>
/// Validates the schema upgrade path and concurrency behaviour against a real PostgreSQL server (EF InMemory cannot): create a
/// database at the schema before the labor-market migrations, run the real startup initializer on the new build, and check the
/// new tables/columns/defaults, idempotent restarts and the supply-contract concurrency token. Opt in via
/// CAPITALISM_TEST_POSTGRES (see <see cref="PostgresFactAttribute"/>).
/// </summary>
public sealed class PostgresMigrationValidationTests
{
    /// <summary>The first migration of the labor market work (#603); the schema before it is the "previous" schema.</summary>
    private const string FirstLaborMarketMigrationSuffix = "_AddCityLaborMarket";

    private static string NewDatabaseName() => $"cap_migtest_{Guid.NewGuid():N}";

    private static string ConnectionStringFor(string database)
        => new NpgsqlConnectionStringBuilder(PostgresFactAttribute.ConnectionString) { Database = database, Pooling = false }.ConnectionString;

    private static AppDbContext CreateContext(string database)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionStringFor(database)).Options);

    private static async Task DropDatabaseAsync(string database)
    {
        await using var context = CreateContext(database);
        await context.Database.EnsureDeletedAsync();
    }

    private static async Task<DataTable> ColumnsAsync(string database, string table)
    {
        await using var connection = new NpgsqlConnection(ConnectionStringFor(database));
        await connection.OpenAsync();
        return await connection.GetSchemaAsync("Columns", [null, "public", table, null]);
    }

    private static async Task<bool> TableExistsAsync(string database, string table)
    {
        await using var connection = new NpgsqlConnection(ConnectionStringFor(database));
        await connection.OpenAsync();
        var tables = await connection.GetSchemaAsync("Tables", [null, "public", table]);
        return tables.Rows.Count > 0;
    }

    private static IOptions<SeedDataOptions> SeedOptions() => Options.Create(new SeedDataOptions
    {
        AdminEmail = "admin@migration-test.local",
        AdminDisplayName = "Migration Test Admin",
        AdminPassword = "TestPassword123!",
    });

    private static async Task MigrateToPreviousSchemaAsync(string database)
    {
        await using var context = CreateContext(database);
        var migrations = context.Database.GetMigrations().ToList();
        var firstLaborMarket = migrations.FindIndex(id => id.EndsWith(FirstLaborMarketMigrationSuffix, StringComparison.Ordinal));
        Assert.True(firstLaborMarket > 0, $"Migration *{FirstLaborMarketMigrationSuffix} not found; update the previous-schema lookup if migrations were renamed or squashed.");
        await context.GetService<IRelationalDatabaseCreator>().CreateAsync();
        await context.GetService<IMigrator>().MigrateAsync(migrations[firstLaborMarket - 1]);
    }

    private static async Task InitializeAsync(string database)
    {
        await using var context = CreateContext(database);
        await new AppDbInitializer(context, SeedOptions(), TestHelpers.CreateFallbackNbsService()).InitializeAsync();
    }

    [PostgresFact]
    public async Task UpgradeFromThePreviousSchema_AddsLaborMarketTablesAndColumnsWithSafeDefaults()
    {
        var database = NewDatabaseName();
        try
        {
            await MigrateToPreviousSchemaAsync(database);
            Assert.False(await TableExistsAsync(database, "CityLaborMarkets"));
            var oldColumns = (await ColumnsAsync(database, "Buildings")).Rows.Cast<DataRow>().Select(r => (string)r["column_name"]).ToHashSet();
            Assert.DoesNotContain("StaffingFactor", oldColumns);

            await InitializeAsync(database); // the real startup path on the new build

            Assert.True(await TableExistsAsync(database, "CityLaborMarkets"));
            var columns = (await ColumnsAsync(database, "Buildings")).Rows.Cast<DataRow>().ToDictionary(r => (string)r["column_name"]);
            Assert.Equal("NO", columns["StaffingFactor"]["is_nullable"]);
            // NOT NULL with default 1: PostgreSQL backfills every pre-existing building row with 1 when the column is added.
            Assert.StartsWith("1", (string)columns["StaffingFactor"]["column_default"]);
            Assert.Equal("YES", columns["StrikeStartedAtTick"]["is_nullable"]);
            Assert.Equal("YES", columns["StrikeUntilTick"]["is_nullable"]);

            await using var verify = CreateContext(database);
            Assert.Empty(await verify.Database.GetPendingMigrationsAsync());
            var seededBuildings = await verify.Buildings.AsNoTracking().ToListAsync();
            Assert.All(seededBuildings, building =>
            {
                Assert.Equal(1m, building.StaffingFactor);
                Assert.Null(building.StrikeUntilTick);
            });

            var city = await verify.Cities.OrderBy(c => c.Name).FirstAsync();
            verify.CityLaborMarkets.Add(new CityLaborMarket { CityId = city.Id, LaborPool = 10m, WageIndex = 1.2m, UpdatedAtTick = 3 });
            await verify.SaveChangesAsync();
            Assert.Equal(1.2m, (await verify.CityLaborMarkets.AsNoTracking().SingleAsync(m => m.CityId == city.Id)).WageIndex);
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [PostgresFact]
    public async Task RestartingTheInitializerOnAnUpgradedDatabase_IsIdempotent()
    {
        var database = NewDatabaseName();
        try
        {
            await MigrateToPreviousSchemaAsync(database);
            await InitializeAsync(database);
            int cityCount;
            await using (var before = CreateContext(database))
            {
                cityCount = await before.Cities.CountAsync();
            }

            await InitializeAsync(database); // "restart"

            await using var verify = CreateContext(database);
            Assert.Equal(cityCount, await verify.Cities.CountAsync());
            Assert.Empty(await verify.Database.GetPendingMigrationsAsync());
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [PostgresFact]
    public async Task ASupplyContractStatusTransition_ByTwoConcurrentWriters_HasExactlyOneWinnerOnPostgres()
    {
        var database = NewDatabaseName();
        try
        {
            await MigrateToPreviousSchemaAsync(database);
            await InitializeAsync(database);
            Guid contractId;
            await using (var seed = CreateContext(database))
            {
                var city = await seed.Cities.OrderBy(c => c.Name).FirstAsync();
                var resource = await seed.ResourceTypes.OrderBy(r => r.Name).FirstAsync();
                var sellerPlayer = new Player { Id = Guid.NewGuid(), Email = "seller@pg.test", DisplayName = "S", PasswordHash = "x" };
                var buyerPlayer = new Player { Id = Guid.NewGuid(), Email = "buyer@pg.test", DisplayName = "B", PasswordHash = "x" };
                var seller = new Company { Id = Guid.NewGuid(), PlayerId = sellerPlayer.Id, Name = "Seller" };
                var buyer = new Company { Id = Guid.NewGuid(), PlayerId = buyerPlayer.Id, Name = "Buyer" };
                var building = new Building { Id = Guid.NewGuid(), CompanyId = seller.Id, CityId = city.Id, Type = BuildingType.Factory, Name = "P", Latitude = city.Latitude, Longitude = city.Longitude, BuiltAtUtc = DateTime.UtcNow };
                var unit = new BuildingUnit { Id = Guid.NewGuid(), BuildingId = building.Id, UnitType = UnitType.B2BSales, GridX = 0, GridY = 0, Level = 1, ResourceTypeId = resource.Id };
                var contract = new SupplyContract
                {
                    Id = Guid.NewGuid(), SellerCompanyId = seller.Id, BuyerCompanyId = buyer.Id, SellerBuildingUnitId = unit.Id,
                    ResourceTypeId = resource.Id, QuantityPerTick = 10m, PricePerUnit = 5m, PenaltyRatePercent = 10m,
                    Status = SupplyContractStatus.Pending, CurrencyCode = city.CurrencyCode,
                };
                seed.Players.AddRange(sellerPlayer, buyerPlayer);
                seed.Companies.AddRange(seller, buyer);
                seed.Buildings.Add(building);
                seed.BuildingUnits.Add(unit);
                seed.SupplyContracts.Add(contract);
                await seed.SaveChangesAsync();
                contractId = contract.Id;
            }

            // Several rounds; in each, two writers that both read the contract as Pending are released at the same moment.
            for (var round = 0; round < 5; round++)
            {
                await using var writerA = CreateContext(database);
                await using var writerB = CreateContext(database);
                var a = await writerA.SupplyContracts.FirstAsync(c => c.Id == contractId);
                var b = await writerB.SupplyContracts.FirstAsync(c => c.Id == contractId);
                a.Status = a.Status == SupplyContractStatus.Pending ? SupplyContractStatus.Active : SupplyContractStatus.Pending;
                b.Status = SupplyContractStatus.Cancelled;
                var gate = new TaskCompletionSource();

                async Task<bool> SaveAsync(AppDbContext context)
                {
                    await gate.Task;
                    try
                    {
                        await context.SaveChangesAsync();
                        return true;
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        return false;
                    }
                }

                var saves = new[] { SaveAsync(writerA), SaveAsync(writerB) };
                gate.SetResult();
                var results = await Task.WhenAll(saves);

                Assert.Equal(1, results.Count(saved => saved));
                // Put the contract back for the next round regardless of who won.
                await using var reset = CreateContext(database);
                var contract = await reset.SupplyContracts.FirstAsync(c => c.Id == contractId);
                contract.Status = SupplyContractStatus.Pending;
                await reset.SaveChangesAsync();
            }
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }
}

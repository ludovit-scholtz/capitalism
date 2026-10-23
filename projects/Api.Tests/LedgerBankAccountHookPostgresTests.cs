using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Api.Tests;

/// <summary>
/// The <c>SaveChanges</c> hook that binds every new ledger row to a bank account owned by the same company only runs on relational
/// providers, so it is pinned on real PostgreSQL (opt-in locally, runs in CI). Written before the hook was changed to detect changes
/// once per save instead of up to three times (#646): it must keep binding, keep rejecting foreign accounts and keep persisting
/// ordinary property changes made in the same save.
/// </summary>
public sealed class LedgerBankAccountHookPostgresTests
{
    private sealed class PostgresFactory(string connectionString) : ApiWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ApplyBaseConfiguration(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<DbContextOptions>();
                services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
            });
        }
    }

    private sealed record World(Guid CompanyId, Guid AccountId, Guid OtherCompanyId, Guid OtherAccountId, Guid BuildingId, Guid BuildingAccountId);

    private static async Task WithWorldAsync(Func<PostgresFactory, World, Task> test)
    {
        var database = $"cap_ledgerhook_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(PostgresFactAttribute.ConnectionString) { Database = database, Pooling = true }.ConnectionString;
        try
        {
            await using (var creator = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options))
            {
                await creator.GetService<IRelationalDatabaseCreator>().CreateAsync();
            }

            await using var factory = new PostgresFactory(connectionString);
            using var client = factory.CreateClient(); // boots the host: migrations and seed data
            World world;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                world = await SeedWorldAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
            }

            await test(factory, world);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var cleanup = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString).Options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private static Company NewCompany(AppDbContext db, string name)
    {
        var player = new Player
        {
            Id = Guid.NewGuid(), Email = $"ledger-hook-{Guid.NewGuid():N}@test.local", DisplayName = name, PasswordHash = "mock",
            Role = PlayerRole.Player, ActiveAccountType = AccountContextType.Company, CreatedAtUtc = DateTime.UtcNow,
            OnboardingCompletedAtUtc = DateTime.UtcNow,
        };
        var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = name, FoundedAtUtc = DateTime.UtcNow, FoundedAtTick = 1 };
        player.ActiveCompanyId = company.Id;
        db.Players.Add(player);
        db.Companies.Add(company);
        return company;
    }

    private static BankAccount NewAccount(Guid companyId, string number, bool baseCapital = false, DateTime? createdAtUtc = null)
        => new()
        {
            Id = Guid.NewGuid(), CompanyId = companyId, CurrencyCode = "EUR", Balance = 100m, AccountNumber = number,
            CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow, IsBaseCapitalDeposit = baseCapital,
        };

    private static async Task<World> SeedWorldAsync(AppDbContext db)
    {
        var city = await db.Cities.OrderBy(c => c.Name).FirstAsync();
        var company = NewCompany(db, "Hook Co");
        var other = NewCompany(db, "Other Co");
        var account = NewAccount(company.Id, "9100000000000001");
        var otherAccount = NewAccount(other.Id, "9100000000000002");
        var buildingAccount = NewAccount(company.Id, "9100000000000003");
        db.BankAccounts.AddRange(account, otherAccount, buildingAccount);
        var building = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, Type = BuildingType.SalesShop, Name = "Hook Shop",
            Latitude = city.Latitude, Longitude = city.Longitude, BuiltAtUtc = DateTime.UtcNow, BankAccountId = buildingAccount.Id,
        };
        db.Buildings.Add(building);
        await db.SaveChangesAsync();
        return new World(company.Id, account.Id, other.Id, otherAccount.Id, building.Id, buildingAccount.Id);
    }

    private static LedgerEntry Entry(Guid companyId, Guid? buildingId = null, Guid? accountId = null)
        => new()
        {
            Id = Guid.NewGuid(), CompanyId = companyId, BuildingId = buildingId, BankAccountId = accountId, Category = LedgerCategory.Revenue,
            Description = "hook test", Amount = 10m, RecordedAtTick = 5, RecordedAtUtc = DateTime.UtcNow,
        };

    private static async Task<LedgerEntry> ReloadAsync(PostgresFactory factory, Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().LedgerEntries.AsNoTracking().SingleAsync(e => e.Id == id);
    }

    [PostgresFact]
    public async Task EntryWithABuilding_IsBoundToTheBuildingsAccount()
    {
        await WithWorldAsync(async (factory, world) =>
        {
            var entry = Entry(world.CompanyId, buildingId: world.BuildingId);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.LedgerEntries.Add(entry);
                await db.SaveChangesAsync();
            }

            Assert.Equal(world.BuildingAccountId, (await ReloadAsync(factory, entry.Id)).BankAccountId);
        });
    }

    [PostgresFact]
    public async Task EntryWithoutABuilding_IsBoundToThePreferredActiveCompanyAccount()
    {
        await WithWorldAsync(async (factory, world) =>
        {
            Guid preferredAccountId;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                // Base-capital deposits are the least preferred; among the rest the oldest wins.
                var baseCapital = NewAccount(world.CompanyId, "9100000000000010", baseCapital: true, createdAtUtc: DateTime.UtcNow.AddYears(-3));
                var preferred = NewAccount(world.CompanyId, "9100000000000011", createdAtUtc: DateTime.UtcNow.AddYears(-2));
                db.BankAccounts.AddRange(baseCapital, preferred);
                await db.SaveChangesAsync();
                // The three accounts of the world are newer than the one above, so the preferred one is the oldest regular account.
                preferredAccountId = preferred.Id;
            }

            var entry = Entry(world.CompanyId);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.LedgerEntries.Add(entry);
                await db.SaveChangesAsync();
            }

            Assert.Equal(preferredAccountId, (await ReloadAsync(factory, entry.Id)).BankAccountId);
        });
    }

    [PostgresFact]
    public async Task EntryOfACompanyWithoutAnAccount_GetsAProvisionedEurAccount()
    {
        await WithWorldAsync(async (factory, _) =>
        {
            Guid companyId;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                companyId = NewCompany(db, "Accountless Co").Id;
                await db.SaveChangesAsync();
            }

            var entry = Entry(companyId);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.LedgerEntries.Add(entry);
                await db.SaveChangesAsync();
            }

            var saved = await ReloadAsync(factory, entry.Id);
            await using var verify = factory.Services.CreateAsyncScope();
            var account = await verify.ServiceProvider.GetRequiredService<AppDbContext>().BankAccounts.AsNoTracking().SingleAsync(a => a.Id == saved.BankAccountId);
            Assert.Equal(companyId, account.CompanyId);
            Assert.Equal("EUR", account.CurrencyCode);
        });
    }

    [PostgresFact]
    public async Task EntryPointingAtAnotherCompanysAccount_IsRejectedAndNothingIsSaved()
    {
        await WithWorldAsync(async (factory, world) =>
        {
            var entry = Entry(world.CompanyId, accountId: world.OtherAccountId);
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.LedgerEntries.Add(entry);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());

            Assert.Contains("not owned by CompanyId", ex.Message);
            await using var verify = factory.Services.CreateAsyncScope();
            Assert.False(await verify.ServiceProvider.GetRequiredService<AppDbContext>().LedgerEntries.AnyAsync(e => e.Id == entry.Id));
        });
    }

    [PostgresFact]
    public async Task OrdinaryPropertyChanges_AreStillSavedAlongsideNewLedgerRows()
    {
        await WithWorldAsync(async (factory, world) =>
        {
            var entry = Entry(world.CompanyId, buildingId: world.BuildingId);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var account = await db.BankAccounts.SingleAsync(a => a.Id == world.AccountId);
                var company = await db.Companies.SingleAsync(c => c.Id == world.CompanyId);
                account.Balance += 55m; // changed by assignment only: found by change detection
                company.Name = "Renamed Hook Co";
                db.LedgerEntries.Add(entry);
                await db.SaveChangesAsync();
            }

            await using var verify = factory.Services.CreateAsyncScope();
            var verifyDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(155m, (await verifyDb.BankAccounts.AsNoTracking().SingleAsync(a => a.Id == world.AccountId)).Balance);
            Assert.Equal("Renamed Hook Co", (await verifyDb.Companies.AsNoTracking().SingleAsync(c => c.Id == world.CompanyId)).Name);
            Assert.Equal(world.BuildingAccountId, (await ReloadAsync(factory, entry.Id)).BankAccountId);
        });
    }

    [PostgresFact]
    public async Task ModifiedLegacyRowWithoutAnAccount_GetsItsAccountPersisted()
    {
        await WithWorldAsync(async (factory, world) =>
        {
            var entry = Entry(world.CompanyId, buildingId: world.BuildingId);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.LedgerEntries.Add(entry);
                await db.SaveChangesAsync();
                // Simulate a row written before the account column was always filled.
                await db.LedgerEntries.Where(e => e.Id == entry.Id).ExecuteUpdateAsync(set => set.SetProperty(e => e.BankAccountId, (Guid?)null));
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var legacy = await db.LedgerEntries.SingleAsync(e => e.Id == entry.Id);
                Assert.Null(legacy.BankAccountId);
                legacy.Description = "touched"; // modified, so the hook sees it and assigns the account
                await db.SaveChangesAsync();
            }

            var saved = await ReloadAsync(factory, entry.Id);
            Assert.Equal("touched", saved.Description);
            Assert.Equal(world.BuildingAccountId, saved.BankAccountId);
        });
    }
}

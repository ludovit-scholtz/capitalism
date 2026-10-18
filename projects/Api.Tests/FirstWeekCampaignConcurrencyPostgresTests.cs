using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Engine.Campaign;
using Api.Tests.Infrastructure;
using Capitalism.Shared.Tutorial;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Api.Tests;

/// <summary>
/// #602: the reward claim is protected by a unique (player, goal) index and the account's concurrency token. EF InMemory enforces
/// neither, so "simultaneous claims have one winner, pay once, and different goals claimed at once all pay" is proven on real
/// PostgreSQL (opt-in locally, runs in CI).
/// </summary>
public sealed class FirstWeekCampaignConcurrencyPostgresTests
{
    private const string ClaimMutation = "mutation($input: ClaimCampaignRewardInput!) { claimCampaignReward(input: $input) { claimed } }";

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

    private sealed record Seeded(string Token, Guid CompanyId, Guid AccountId);

    private static async Task WithDatabaseAsync(Func<PostgresFactory, HttpClient, Task> test)
    {
        var database = $"cap_campaign_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(PostgresFactAttribute.ConnectionString) { Database = database, Pooling = true }.ConnectionString;
        try
        {
            await using (var creator = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options))
            {
                await creator.GetService<IRelationalDatabaseCreator>().CreateAsync();
            }

            await using var factory = new PostgresFactory(connectionString);
            using var client = factory.CreateClient();
            await test(factory, client);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var cleanup = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString).Options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    /// <summary>A player onboarded <paramref name="hoursAgo"/> hours ago with two buildings and the first-sale milestone: goals 1 and 2 are claimable once day 2 has come.</summary>
    private static async Task<Seeded> SeedAsync(PostgresFactory factory, HttpClient client, int hoursAgo)
    {
        var email = $"campaign-pg-{Guid.NewGuid():N}@example.com";
        var reg = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email, displayName = "Race", password = "TestPass123!" } });
        var token = reg.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var player = await db.Players.FirstAsync(p => p.Email == email);
        player.OnboardingCompletedAtUtc = DateTime.UtcNow.AddHours(-hoursAgo);
        var city = await db.Cities.OrderBy(c => c.Name).FirstAsync();
        var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "Race Co", FoundedAtUtc = DateTime.UtcNow };
        var account = new BankAccount
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CurrencyCode = "USD", Balance = 1_000m,
            AccountNumber = "7100000000000001", CreatedAtUtc = DateTime.UtcNow,
        };
        player.ActiveCompanyId = company.Id;
        db.Companies.Add(company);
        db.BankAccounts.Add(account);
        for (var i = 0; i < 2; i++)
        {
            db.Buildings.Add(new Building
            {
                Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, Type = BuildingType.Factory, Name = $"Race Plant {i}",
                Latitude = city.Latitude, Longitude = city.Longitude, BuiltAtUtc = DateTime.UtcNow,
            });
        }

        db.TutorialProgresses.Add(new TutorialProgress
        {
            Id = Guid.NewGuid(), PlayerId = player.Id, Milestone = TutorialMilestoneCodes.FirstResourceSold, IsCompleted = true, CompletedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return new Seeded(token, company.Id, account.Id);
    }

    private static async Task<bool> ClaimedAsync(HttpClient client, string token, string goalCode, Task gate)
    {
        await gate;
        var result = await TestHelpers.ExecuteGraphQlAsync(client, ClaimMutation, new { input = new { goalCode } }, token);
        return result.TryGetProperty("data", out var data)
               && data.ValueKind == JsonValueKind.Object
               && data.GetProperty("claimCampaignReward").GetProperty("claimed").GetBoolean();
    }

    [PostgresFact]
    public async Task SimultaneousClaimsOfTheSameGoal_HaveOneWinner_AndPayExactlyOnce()
    {
        await WithDatabaseAsync(async (factory, client) =>
        {
            var seeded = await SeedAsync(factory, client, hoursAgo: 2);
            var gate = new TaskCompletionSource();

            var claims = Enumerable.Range(0, 4).Select(_ => ClaimedAsync(client, seeded.Token, FirstWeekCampaignCatalog.FirstSale, gate.Task)).ToArray();
            gate.SetResult();
            var winners = (await Task.WhenAll(claims)).Count(claimed => claimed);

            Assert.Equal(1, winners);
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(3_000m, (await db.BankAccounts.AsNoTracking().SingleAsync(a => a.Id == seeded.AccountId)).Balance); // 1,000 + one reward
            Assert.Single(await db.PlayerCampaignClaims.AsNoTracking().ToListAsync());
            Assert.Single(await db.LedgerEntries.AsNoTracking().Where(e => e.CompanyId == seeded.CompanyId && e.Category == LedgerCategory.CampaignReward).ToListAsync());
        });
    }

    [PostgresFact]
    public async Task SimultaneousClaimsOfDifferentGoalsOnTheSameAccount_AllPay()
    {
        await WithDatabaseAsync(async (factory, client) =>
        {
            var seeded = await SeedAsync(factory, client, hoursAgo: 30); // day 2: goals 1 and 2 are claimable
            var gate = new TaskCompletionSource();

            var claims = new[]
            {
                ClaimedAsync(client, seeded.Token, FirstWeekCampaignCatalog.FirstSale, gate.Task),
                ClaimedAsync(client, seeded.Token, FirstWeekCampaignCatalog.SecondBuilding, gate.Task),
            };
            gate.SetResult();
            var results = await Task.WhenAll(claims);

            Assert.All(results, claimed => Assert.True(claimed)); // a balance-token conflict between them is retried, not surfaced as an error
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1_000m + 2_000m + 4_000m, (await db.BankAccounts.AsNoTracking().SingleAsync(a => a.Id == seeded.AccountId)).Balance);
            Assert.Equal(2, await db.PlayerCampaignClaims.AsNoTracking().CountAsync());
        });
    }
}

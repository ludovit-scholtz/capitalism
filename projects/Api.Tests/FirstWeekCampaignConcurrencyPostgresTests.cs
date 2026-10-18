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
/// #602: the reward claim is protected by a unique (player, goal) index. EF InMemory does not enforce unique indexes, so the
/// "simultaneous claims have exactly one winner and pay once" requirement is proven on real PostgreSQL (opt-in, runs in CI).
/// </summary>
public sealed class FirstWeekCampaignConcurrencyPostgresTests
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

    [PostgresFact]
    public async Task SimultaneousClaimsOfTheSameGoal_HaveOneWinner_AndPayExactlyOnce()
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
            var email = $"campaign-pg-{Guid.NewGuid():N}@example.com";
            var reg = await TestHelpers.ExecuteGraphQlAsync(
                client,
                "mutation($input: RegisterInput!) { register(input: $input) { token } }",
                new { input = new { email, displayName = "Race", password = "TestPass123!" } });
            var token = reg.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
            Guid companyId;
            Guid accountId;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var player = await db.Players.FirstAsync(p => p.Email == email);
                player.OnboardingCompletedAtUtc = DateTime.UtcNow.AddHours(-2);
                var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "Race Co", FoundedAtUtc = DateTime.UtcNow };
                var account = new BankAccount
                {
                    Id = Guid.NewGuid(), CompanyId = company.Id, CurrencyCode = "USD", Balance = 1_000m,
                    AccountNumber = "7100000000000001", CreatedAtUtc = DateTime.UtcNow,
                };
                player.ActiveCompanyId = company.Id;
                db.Companies.Add(company);
                db.BankAccounts.Add(account);
                db.TutorialProgresses.Add(new TutorialProgress
                {
                    Id = Guid.NewGuid(), PlayerId = player.Id, Milestone = TutorialMilestoneCodes.FirstResourceSold, IsCompleted = true, CompletedAtUtc = DateTime.UtcNow,
                });
                await db.SaveChangesAsync();
                companyId = company.Id;
                accountId = account.Id;
            }

            const string mutation = "mutation($input: ClaimCampaignRewardInput!) { claimCampaignReward(input: $input) { claimed } }";
            var variables = new { input = new { goalCode = FirstWeekCampaignCatalog.FirstSale } };
            var gate = new TaskCompletionSource();
            async Task<bool> ClaimAsync()
            {
                await gate.Task;
                var result = await TestHelpers.ExecuteGraphQlAsync(client, mutation, variables, token);
                return result.TryGetProperty("data", out var data)
                       && data.ValueKind == System.Text.Json.JsonValueKind.Object
                       && data.GetProperty("claimCampaignReward").GetProperty("claimed").GetBoolean();
            }

            var claims = new[] { ClaimAsync(), ClaimAsync(), ClaimAsync(), ClaimAsync() };
            gate.SetResult();
            var winners = (await Task.WhenAll(claims)).Count(claimed => claimed);

            Assert.Equal(1, winners);

            await using var verifyScope = factory.Services.CreateAsyncScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(3_000m, (await verifyDb.BankAccounts.AsNoTracking().SingleAsync(a => a.Id == accountId)).Balance); // paid exactly once
            Assert.Single(await verifyDb.PlayerCampaignClaims.AsNoTracking().ToListAsync());
            Assert.Single(await verifyDb.LedgerEntries.AsNoTracking().Where(e => e.CompanyId == companyId && e.Category == LedgerCategory.CampaignReward).ToListAsync());
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var cleanup = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString).Options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }
}

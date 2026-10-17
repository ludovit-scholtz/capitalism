using Api.Data;
using Api.Engine;
using Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// Tick benchmark against a real PostgreSQL (EF InMemory cannot judge context load and save cost). Opt in via
/// CAPITALISM_TEST_POSTGRES; scale via TICK_BENCH_SCALE (ci | medium | full, default medium); optional budget via
/// TICK_BENCH_BUDGET_MS (fails when the average tick exceeds it). Prints the per-phase breakdown including the
/// "(context load)" and "(save changes)" entries.
/// </summary>
public sealed class TickPerformancePostgresBenchmarkTests(ITestOutputHelper output)
{
    private const int Seed = 600;
    private const int MeasuredTicks = 3;

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
    public async Task FullTick_OnPostgres_ReportsWhereTheTimeGoes()
    {
        var scale = Environment.GetEnvironmentVariable("TICK_BENCH_SCALE")?.ToLowerInvariant() switch
        {
            "full" => TickBenchmarkScale.Full,
            "ci" => TickBenchmarkScale.Ci,
            _ => TickBenchmarkScale.Medium,
        };
        var database = $"cap_bench_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(PostgresFactAttribute.ConnectionString) { Database = database, Pooling = true }.ConnectionString;

        try
        {
            await using (var creator = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options))
            {
                await creator.GetService<IRelationalDatabaseCreator>().CreateAsync(); // the host then applies the real migrations
            }

            await using var factory = new PostgresFactory(connectionString);
            using var client = factory.CreateClient(); // boots the host: migrations + seed data on PostgreSQL
            await using (var seedScope = factory.Services.CreateAsyncScope())
            {
                await TickBenchmarkWorld.SeedAsync(seedScope.ServiceProvider.GetRequiredService<AppDbContext>(), scale, Seed);
            }

            await RunTickAsync(factory, new TickTimingRecorder()); // warm-up
            var recorder = new TickTimingRecorder(capacity: MeasuredTicks, phaseBudgetMs: 100);
            for (var i = 0; i < MeasuredTicks; i++)
            {
                await RunTickAsync(factory, recorder);
            }

            var summary = recorder.Summarize();
            output.WriteLine($"PostgreSQL scale: {scale.Players} players, {scale.Buildings} buildings, {scale.Units} units");
            output.WriteLine($"Measured {summary.SampledTicks} ticks: avg {summary.AverageTotalMs:F0} ms, max {summary.MaxTotalMs} ms");
            foreach (var phase in summary.Phases.Take(12))
            {
                output.WriteLine($"  {phase.Name,-32} avg {phase.AverageMs,8:F0} ms   max {phase.MaxMs,6} ms");
            }

            if (long.TryParse(Environment.GetEnvironmentVariable("TICK_BENCH_BUDGET_MS"), out var budgetMs))
            {
                Assert.True(summary.AverageTotalMs <= budgetMs, $"Average tick {summary.AverageTotalMs:F0} ms exceeds {budgetMs} ms on PostgreSQL.");
            }
            else
            {
                Assert.True(summary.AverageTotalMs > 0);
            }
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var cleanup = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString).Options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private static async Task RunTickAsync(ApiWebApplicationFactory factory, TickTimingRecorder recorder)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetRequiredService<IEnumerable<ITickPhase>>();
        await new TickProcessor(db, phases, NullLogger<TickProcessor>.Instance, recorder).ProcessTickAsync();
    }
}

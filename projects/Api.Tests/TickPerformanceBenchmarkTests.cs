using Api.Data;
using Api.Engine;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// Tick performance gate. Seeds a deterministic world, processes ticks through the real pipeline and fails when the
/// measured tick exceeds the budget. Runs a 1/50 slice by default; set TICK_BENCH_SCALE=medium (2,000 buildings) or full to benchmark the
/// product requirement (1,000 players, 20,000 buildings, 500,000 units) and TICK_BENCH_BUDGET_MS to override the budget.
/// </summary>
public sealed class TickPerformanceBenchmarkTests(ITestOutputHelper output)
{
    private const int Seed = 600;
    private const int MeasuredTicks = 3;

    /// <summary>Budget for the CI slice. Roughly 4x the measured ~0.8 s baseline so shared CI runners do not flake; it catches large regressions only. Tighten alongside the optimisation work in #608.</summary>
    private const long CiBudgetMs = 3_000;

    private const double Tolerance = 0.25;

    [Fact]
    public async Task FullTick_OnSeededWorld_StaysWithinBudget()
    {
        var scaleName = Environment.GetEnvironmentVariable("TICK_BENCH_SCALE");
        var full = string.Equals(scaleName, "full", StringComparison.OrdinalIgnoreCase);
        var scale = full
            ? TickBenchmarkScale.Full
            : string.Equals(scaleName, "medium", StringComparison.OrdinalIgnoreCase) ? TickBenchmarkScale.Medium : TickBenchmarkScale.Ci;
        var budgetMs = long.TryParse(Environment.GetEnvironmentVariable("TICK_BENCH_BUDGET_MS"), out var overridden)
            ? overridden
            : full ? 1_000 : CiBudgetMs;

        await using var factory = new ApiWebApplicationFactory();
        using var _ = factory.CreateClient(); // boots the host and database seed data
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            await TickBenchmarkWorld.SeedAsync(seedScope.ServiceProvider.GetRequiredService<AppDbContext>(), scale, Seed);
        }

        var recorder = new TickTimingRecorder(capacity: MeasuredTicks, phaseBudgetMs: budgetMs / 10);
        await RunTickAsync(factory, recorder); // warm-up: JIT, EF model and query caches
        var warmup = recorder.Summarize();
        recorder = new TickTimingRecorder(capacity: MeasuredTicks, phaseBudgetMs: budgetMs / 10);
        for (var i = 0; i < MeasuredTicks; i++)
        {
            await RunTickAsync(factory, recorder);
        }

        var summary = recorder.Summarize();
        output.WriteLine($"Scale: {scale.Players} players, {scale.Buildings} buildings, {scale.Units} units (seed {Seed})");
        output.WriteLine($"Warm-up tick: {warmup.LastTotalMs} ms; measured {summary.SampledTicks} ticks: avg {summary.AverageTotalMs:F0} ms, max {summary.MaxTotalMs} ms, budget {budgetMs} ms (+{Tolerance:P0})");
        foreach (var phase in summary.Phases.Take(10))
        {
            output.WriteLine($"  {phase.Name,-32} avg {phase.AverageMs,8:F0} ms   max {phase.MaxMs,6} ms{(phase.AboveBudget ? "  <-- above phase budget" : string.Empty)}");
        }

        Assert.True(
            TickBudget.IsWithinBudget((long)summary.AverageTotalMs, budgetMs, Tolerance),
            $"Average tick {summary.AverageTotalMs:F0} ms exceeds budget {budgetMs} ms (+{Tolerance:P0}). Slowest phases: "
            + TickBudget.DescribeOffenders(summary.Phases.ToDictionary(p => p.Name, p => (long)p.AverageMs), budgetMs / 10));
    }

    private static async Task RunTickAsync(ApiWebApplicationFactory factory, TickTimingRecorder recorder)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetRequiredService<IEnumerable<ITickPhase>>();
        await new TickProcessor(db, phases, new NullLogger<TickProcessor>(), recorder).ProcessTickAsync();
    }
}

using Api.Engine;

namespace Api.Tests;

public sealed class TickTimingRecorderTests
{
    private static IReadOnlyDictionary<string, long> Phases(params (string Name, long Ms)[] items)
        => items.ToDictionary(i => i.Name, i => i.Ms);

    [Fact]
    public void Summarize_ReturnsLastAverageAndMaxPerPhase_SortedBySlowestFirst()
    {
        var recorder = new TickTimingRecorder(capacity: 10);
        recorder.Record(1, 30, Phases(("Fast", 5), ("Slow", 25)));
        recorder.Record(2, 50, Phases(("Fast", 7), ("Slow", 43)));

        var summary = recorder.Summarize();

        Assert.Equal(2, summary.SampledTicks);
        Assert.Equal(2, summary.LastTick);
        Assert.Equal(50, summary.LastTotalMs);
        Assert.Equal(40, summary.AverageTotalMs);
        Assert.Equal(50, summary.MaxTotalMs);
        Assert.Equal(["Slow", "Fast"], summary.Phases.Select(p => p.Name).ToArray());
        var slow = summary.Phases[0];
        Assert.Equal(43, slow.LastMs);
        Assert.Equal(34, slow.AverageMs);
        Assert.Equal(43, slow.MaxMs);
    }

    [Fact]
    public void Record_KeepsOnlyTheMostRecentCapacityTicks()
    {
        var recorder = new TickTimingRecorder(capacity: 2);
        recorder.Record(1, 100, Phases(("A", 100)));
        recorder.Record(2, 10, Phases(("A", 10)));
        recorder.Record(3, 20, Phases(("A", 20)));

        var summary = recorder.Summarize();

        Assert.Equal(2, summary.SampledTicks);
        Assert.Equal(20, summary.MaxTotalMs); // tick 1 (100 ms) was evicted
        Assert.Equal(15, summary.Phases.Single().AverageMs);
    }

    [Fact]
    public void Summarize_WithNoSamples_IsEmpty()
    {
        var summary = new TickTimingRecorder().Summarize();

        Assert.Equal(0, summary.SampledTicks);
        Assert.Empty(summary.Phases);
    }

    [Fact]
    public void Summarize_FlagsPhasesAboveTheirBudget()
    {
        var recorder = new TickTimingRecorder(capacity: 5, phaseBudgetMs: 20);
        recorder.Record(1, 45, Phases(("Under", 5), ("Over", 40)));

        var summary = recorder.Summarize();

        Assert.False(summary.Phases.Single(p => p.Name == "Under").AboveBudget);
        Assert.True(summary.Phases.Single(p => p.Name == "Over").AboveBudget);
    }

    [Theory]
    [InlineData(900, 1000, 0.1, true)]    // within budget
    [InlineData(1050, 1000, 0.1, true)]   // over budget but inside the 10% tolerance
    [InlineData(1101, 1000, 0.1, false)]  // beyond tolerance
    [InlineData(0, 1000, 0.1, true)]
    public void TickBudget_IsWithinBudget_AppliesTolerance(long measuredMs, long budgetMs, double tolerance, bool expected)
    {
        Assert.Equal(expected, TickBudget.IsWithinBudget(measuredMs, budgetMs, tolerance));
    }

    [Fact]
    public void TickBudget_Describe_ListsPhasesOverBudgetSlowestFirst()
    {
        var phases = Phases(("A", 10), ("B", 300), ("C", 150));

        var message = TickBudget.DescribeOffenders(phases, perPhaseBudgetMs: 100);

        Assert.StartsWith("B=300ms", message);
        Assert.Contains("C=150ms", message);
        Assert.DoesNotContain("A=", message);
    }

    [Fact]
    public async Task ProcessTick_RecordsContextLoadAndSaveAsOwnEntries_SoTheDashboardShowsWhereTheTimeGoes()
    {
        await using var factory = new Api.Tests.Infrastructure.ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        await using var scope = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateAsyncScope(factory.Services);
        var db = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Api.Data.AppDbContext>(scope.ServiceProvider);
        var phases = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IEnumerable<ITickPhase>>(scope.ServiceProvider);
        var recorder = new TickTimingRecorder();

        await new TickProcessor(db, phases, Microsoft.Extensions.Logging.Abstractions.NullLogger<TickProcessor>.Instance, recorder).ProcessTickAsync();

        var names = recorder.Summarize().Phases.Select(p => p.Name).ToList();
        Assert.Contains(TickProcessor.ContextLoadEntryName, names);
        Assert.Contains(TickProcessor.SaveEntryName, names);
    }
}

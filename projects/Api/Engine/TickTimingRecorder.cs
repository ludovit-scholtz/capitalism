namespace Api.Engine;

/// <summary>Aggregated duration of one tick phase over the recorded window.</summary>
public sealed record TickPhaseTimingSummary(string Name, long LastMs, double AverageMs, long MaxMs, bool AboveBudget);

/// <summary>Aggregated tick timings over the recorded window, slowest phase first.</summary>
public sealed record TickTimingSummary(
    int SampledTicks,
    long LastTick,
    long LastTotalMs,
    double AverageTotalMs,
    long MaxTotalMs,
    long PhaseBudgetMs,
    IReadOnlyList<TickPhaseTimingSummary> Phases);

/// <summary>
/// Thread-safe ring buffer of the most recent tick timings (total and per phase). Registered as a singleton so the
/// tick engine can record and the read-only operations query can summarize without touching the database.
/// </summary>
public sealed class TickTimingRecorder(int capacity = 60, long phaseBudgetMs = TickBudget.DefaultPhaseBudgetMs)
{
    private readonly object gate = new();
    private readonly Queue<Sample> samples = new();

    private sealed record Sample(long Tick, long TotalMs, IReadOnlyDictionary<string, long> PhaseMs);

    public void Record(long tick, long totalMs, IReadOnlyDictionary<string, long> phaseMs)
    {
        lock (gate)
        {
            samples.Enqueue(new Sample(tick, totalMs, new Dictionary<string, long>(phaseMs)));
            while (samples.Count > Math.Max(1, capacity))
            {
                samples.Dequeue();
            }
        }
    }

    public TickTimingSummary Summarize()
    {
        Sample[] snapshot;
        lock (gate)
        {
            snapshot = samples.ToArray();
        }

        if (snapshot.Length == 0)
        {
            return new TickTimingSummary(0, 0, 0, 0, 0, phaseBudgetMs, []);
        }

        var last = snapshot[^1];
        var phases = snapshot
            .SelectMany(s => s.PhaseMs.Keys)
            .Distinct()
            .Select(name =>
            {
                var values = snapshot.Where(s => s.PhaseMs.ContainsKey(name)).Select(s => s.PhaseMs[name]).ToArray();
                var average = values.Average();
                return new TickPhaseTimingSummary(
                    name,
                    last.PhaseMs.GetValueOrDefault(name),
                    average,
                    values.Max(),
                    average > phaseBudgetMs && !TickProcessor.IsInfrastructureEntry(name));
            })
            .OrderByDescending(p => p.AverageMs)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ToList();

        return new TickTimingSummary(
            snapshot.Length,
            last.Tick,
            last.TotalMs,
            snapshot.Average(s => s.TotalMs),
            snapshot.Max(s => s.TotalMs),
            phaseBudgetMs,
            phases);
    }
}

/// <summary>Budget comparison helpers shared by the tick performance benchmark and diagnostics.</summary>
public static class TickBudget
{
    /// <summary>Phases slower than this are logged as slow by the tick engine and flagged in the operations dashboard.</summary>
    public const long DefaultPhaseBudgetMs = 100;

    /// <summary>True when <paramref name="measuredMs"/> is within <paramref name="budgetMs"/> plus the relative tolerance.</summary>
    public static bool IsWithinBudget(long measuredMs, long budgetMs, double tolerance)
        => measuredMs <= budgetMs * (1d + tolerance);

    /// <summary>Lists phases over <paramref name="perPhaseBudgetMs"/>, slowest first, e.g. "B=300ms, C=150ms".</summary>
    public static string DescribeOffenders(IReadOnlyDictionary<string, long> phaseMs, long perPhaseBudgetMs)
        => string.Join(
            ", ",
            phaseMs.Where(kv => kv.Value > perPhaseBudgetMs)
                .OrderByDescending(kv => kv.Value)
                .Select(kv => $"{kv.Key}={kv.Value}ms"));
}

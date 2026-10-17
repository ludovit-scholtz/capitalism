using Api.Data;
using Api.Engine;
using Api.Security;
using HotChocolate.Authorization;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Returns per-phase tick durations (last / average / max) over the most recent ticks processed by this
    /// server process, slowest phase first. Read-only and served from memory; requires admin dashboard access.
    /// </summary>
    [Authorize]
    public async Task<TickTimingsResult> GetTickPhaseTimings(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] GameAdminAuthorizationService gameAdminAuthorizationService,
        [Service] TickTimingRecorder timingRecorder)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HTTP context is required for tick phase timings.");
        await gameAdminAuthorizationService.RequireAdminDashboardAccessAsync(db, httpContext.User, httpContext.RequestAborted);

        var summary = timingRecorder.Summarize();
        return new TickTimingsResult
        {
            SampledTicks = summary.SampledTicks,
            LastTick = summary.LastTick,
            LastTotalMs = summary.LastTotalMs,
            AverageTotalMs = summary.AverageTotalMs,
            MaxTotalMs = summary.MaxTotalMs,
            PhaseBudgetMs = summary.PhaseBudgetMs,
            Phases = summary.Phases
                .Select(p => new TickPhaseTimingItem
                {
                    Name = p.Name,
                    LastMs = p.LastMs,
                    AverageMs = p.AverageMs,
                    MaxMs = p.MaxMs,
                    AboveBudget = p.AboveBudget,
                })
                .ToList(),
        };
    }
}

/// <summary>Per-phase tick timing summary for the admin Operations Dashboard.</summary>
public sealed class TickTimingsResult
{
    public int SampledTicks { get; set; }
    public long LastTick { get; set; }
    public long LastTotalMs { get; set; }
    public double AverageTotalMs { get; set; }
    public long MaxTotalMs { get; set; }
    public long PhaseBudgetMs { get; set; }
    public List<TickPhaseTimingItem> Phases { get; set; } = [];
}

public sealed class TickPhaseTimingItem
{
    public string Name { get; set; } = string.Empty;
    public long LastMs { get; set; }
    public double AverageMs { get; set; }
    public long MaxMs { get; set; }
    public bool AboveBudget { get; set; }
}

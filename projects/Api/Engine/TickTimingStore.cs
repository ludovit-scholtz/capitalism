using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine;

/// <summary>
/// Persists the last <see cref="RetainedTicks"/> tick timing breakdowns (#646) so a restart, or the nightly benchmark's fresh process,
/// does not lose the history the Operations dashboard shows. The in-memory <see cref="TickTimingRecorder"/> stays the read path.
/// </summary>
public sealed class TickTimingStore(AppDbContext db)
{
    /// <summary>How many ticks of history are kept; matches the recorder's default window.</summary>
    public const int RetainedTicks = 60;

    /// <summary>Stores one tick's timings and prunes everything older than <see cref="RetainedTicks"/> ticks.</summary>
    public async Task SaveAsync(long tick, long totalMs, IReadOnlyDictionary<string, long> phaseMs, CancellationToken ct = default)
    {
        var existing = await db.TickTimingRecords.FirstOrDefaultAsync(r => r.Tick == tick, ct);
        var json = JsonSerializer.Serialize(phaseMs);
        if (existing is null)
        {
            db.TickTimingRecords.Add(new TickTimingRecord { Tick = tick, TotalMs = totalMs, PhaseMsJson = json, RecordedAtUtc = DateTime.UtcNow });
        }
        else
        {
            existing.TotalMs = totalMs;
            existing.PhaseMsJson = json;
            existing.RecordedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        var oldestKept = tick - RetainedTicks + 1;
        if (db.Database.IsRelational())
        {
            await db.TickTimingRecords.Where(r => r.Tick < oldestKept).ExecuteDeleteAsync(ct);
        }
        else
        {
            db.TickTimingRecords.RemoveRange(await db.TickTimingRecords.Where(r => r.Tick < oldestKept).ToListAsync(ct));
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>The stored breakdowns, oldest first, ready to seed the recorder.</summary>
    public async Task<IReadOnlyList<(long Tick, long TotalMs, IReadOnlyDictionary<string, long> PhaseMs)>> LoadRecentAsync(CancellationToken ct = default)
    {
        var rows = await db.TickTimingRecords.AsNoTracking()
            .OrderByDescending(r => r.Tick)
            .Take(RetainedTicks)
            .ToListAsync(ct);

        return rows
            .OrderBy(r => r.Tick)
            .Select(r => (r.Tick, r.TotalMs, Parse(r.PhaseMsJson)))
            .ToList();
    }

    private static IReadOnlyDictionary<string, long> Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, long>>(json) ?? [];
        }
        catch (JsonException)
        {
            return new Dictionary<string, long>();
        }
    }
}

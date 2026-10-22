using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine;

/// <summary>
/// Generates and manages rolling 50-tick weather forecasts for each city.
///
/// <para><b>Wind model:</b> random walk starting at 50 %, ±2–5 % per tick, clamped to [0, 100].
/// The seed is deterministic: hash(cityId, tick) so forecasts are reproducible.</para>
///
/// <para><b>Solar model:</b> deterministic sine-wave over a 24-tick day.  Output is 0 during
/// night hours (ticks 18–5 of the day) and peaks at tick 12 (noon).  A small noise term
/// (±5 %) is layered on top without exceeding [0, 100].</para>
/// </summary>
public static class WeatherService
{
    /// <summary>Number of future ticks kept in the rolling forecast window.</summary>
    public const int ForecastWindow = 50;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the current-tick weather snapshot for every city that has forecast rows.
    /// Returns a dictionary keyed by CityId containing the nearest tick ≥ currentTick.
    /// Cities with no forecast rows return a neutral snapshot (50 % wind, 60 % solar).
    /// </summary>
    public static async Task<Dictionary<Guid, WeatherSnapshot>> LoadCurrentSnapshotsAsync(
        AppDbContext db,
        IEnumerable<Guid> cityIds,
        long currentTick,
        CancellationToken ct = default)
    {
        var cityIdList = cityIds.ToList();
        if (cityIdList.Count == 0) return [];

        // Load the earliest future-or-current row for each city in a single query.
        var rows = await db.CityWeatherForecasts
            .Where(f => cityIdList.Contains(f.CityId) && f.Tick >= currentTick)
            .GroupBy(f => f.CityId)
            .Select(g => g.OrderBy(f => f.Tick).First())
            .ToListAsync(ct);

        var result = new Dictionary<Guid, WeatherSnapshot>(cityIdList.Count);
        var byCity = rows.ToDictionary(r => r.CityId);

        foreach (var id in cityIdList)
        {
            if (byCity.TryGetValue(id, out var row))
                result[id] = new WeatherSnapshot { WindPercent = row.WindPercent, SolarPercent = row.SolarPercent };
            else
                result[id] = new WeatherSnapshot { WindPercent = 50m, SolarPercent = 60m };
        }

        return result;
    }

    /// <summary>
    /// Advances the rolling forecast window for all supplied cities:
    /// removes rows whose tick is strictly less than <paramref name="currentTick"/> and
    /// appends the rows that bring each city's forecast to <c>currentTick + <see cref="ForecastWindow"/> - 1 + (cadenceTicks - 1)</c>.
    /// Also seeds a fresh window for cities that have no rows yet.
    /// </summary>
    /// <param name="cadenceTicks">
    /// Ticks between runs (1 = every tick, one row per run). The extra rows keep the forecast at least <see cref="ForecastWindow"/> ticks
    /// ahead on every tick between two runs, and the wind random walk is the same as when it advanced every tick, so a cadence changes
    /// when the table is touched and not what any tick sees.
    /// </param>
    public static async Task AdvanceForecastsAsync(
        AppDbContext db,
        IList<Guid> cityIds,
        long currentTick,
        int cadenceTicks = 1,
        CancellationToken ct = default)
    {
        if (cityIds.Count == 0) return;

        // Prune expired rows (ticks strictly before current).
        if (db.Database.IsRelational())
        {
            await db.CityWeatherForecasts
                .Where(f => cityIds.Contains(f.CityId) && f.Tick < currentTick)
                .ExecuteDeleteAsync(ct);
        }
        else
        {
            var expiredRows = await db.CityWeatherForecasts
                .Where(f => cityIds.Contains(f.CityId) && f.Tick < currentTick)
                .ToListAsync(ct);
            if (expiredRows.Count > 0)
            {
                db.CityWeatherForecasts.RemoveRange(expiredRows);
            }
        }

        // One read of the (city, tick, wind) triples replaces the per-city lookups: the latest row of each city is the base for
        // its next wind value, and a city without rows is seeded below.
        var existing = await db.CityWeatherForecasts
            .AsNoTracking()
            .Where(f => cityIds.Contains(f.CityId))
            .Select(f => new { f.CityId, f.Tick, f.WindPercent })
            .ToListAsync(ct);

        var latestByCity = existing
            .GroupBy(f => f.CityId)
            .ToDictionary(g => g.Key, g => g.MaxBy(f => f.Tick)!);

        // For cities that already have rows: extend the walk after the latest tick up to the target horizon (at least one row).
        var extraTicks = Math.Max(1, cadenceTicks) - 1;
        var targetTick = currentTick + ForecastWindow - 1 + extraTicks;
        var newRows = new List<CityWeatherForecast>(latestByCity.Count * (extraTicks + 1));
        foreach (var (cityId, latest) in latestByCity)
        {
            var wind = latest.WindPercent;
            var rowCount = Math.Max(1L, targetTick - latest.Tick);
            for (var nextTick = latest.Tick + 1; nextTick <= latest.Tick + rowCount; nextTick++)
            {
                wind = ComputeNextWind(wind, cityId, nextTick);
                newRows.Add(new CityWeatherForecast
                {
                    CityId = cityId,
                    Tick = nextTick,
                    WindPercent = wind,
                    SolarPercent = ComputeSolar(nextTick),
                });
            }
        }

        if (newRows.Count > 0)
        {
            db.CityWeatherForecasts.AddRange(newRows);
        }

        // Seed fresh 50-tick window for any cities with no rows.
        var needsSeed = cityIds.Where(id => !latestByCity.ContainsKey(id)).ToList();
        foreach (var cityId in needsSeed)
        {
            var seeded = SeedForecast(cityId, currentTick, ForecastWindow + extraTicks);
            db.CityWeatherForecasts.AddRange(seeded);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Generates <paramref name="count"/> forecast rows for a city starting at <paramref name="startTick"/>.
    /// </summary>
    public static List<CityWeatherForecast> SeedForecast(Guid cityId, long startTick, int count)
    {
        var rows = new List<CityWeatherForecast>(count);
        decimal wind = 50m;
        for (int i = 0; i < count; i++)
        {
            var tick = startTick + i;
            wind = ComputeNextWind(wind, cityId, tick);
            rows.Add(new CityWeatherForecast
            {
                CityId = cityId,
                Tick = tick,
                WindPercent = wind,
                SolarPercent = ComputeSolar(tick),
            });
        }
        return rows;
    }

    // ── Internal computation ──────────────────────────────────────────────────

    /// <summary>
    /// Advances wind by a deterministic-but-varied delta in [2, 5] and clamps to [0, 100].
    /// The seed (cityId xor tick) ensures each city has independent variation.
    /// </summary>
    private static decimal ComputeNextWind(decimal previous, Guid cityId, long tick)
    {
        unchecked
        {
            var seed = (long)(uint)cityId.GetHashCode() ^ tick * 2_654_435_761L;
            seed = (seed ^ (seed >> 30)) * -4658895341701367673L;
            seed ^= seed >> 27;
            var raw = (double)(seed & 0x7FFFFFFFL) / (double)0x7FFFFFFFL; // [0, 1)
            var delta = 2m + (decimal)(raw * 3.0);   // [2, 5)
            var sign = (seed >> 32 & 1) == 0 ? 1m : -1m;
            return Math.Clamp(previous + sign * delta, 0m, 100m);
        }
    }

    /// <summary>
    /// Solar irradiance modelled as a sine wave over a 24-tick day.
    /// Night hours (ticks 18–23 and 0–5) return 0.
    /// A small deterministic noise (±5 %) is applied on top.
    /// </summary>
    private static decimal ComputeSolar(long tick)
    {
        var hourOfDay = (int)(tick % 24);
        if (hourOfDay < 6 || hourOfDay >= 19) return 0m;

        // Sine wave: 0 at hour 6, peak at hour 12-13, 0 at hour 19.
        // Map [6, 19) → [0, π].
        var angle = Math.PI * (hourOfDay - 6) / 13.0;
        var sineValue = (decimal)Math.Sin(angle);       // [0, 1]
        var rawPercent = sineValue * 100m;

        // Small deterministic noise based on tick.
        unchecked
        {
            var noise = (decimal)((tick * 1_234_567L ^ tick >> 17) % 11) - 5m; // [-5, 5]
            return Math.Clamp(rawPercent + noise, 0m, 100m);
        }
    }
}

using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine.Phases;

/// <summary>
/// Captures FX rate snapshots at each tick so the frontend can render historical rate charts.
/// Stores buy, mid, and sell prices for all EUR-based currency pairs.
/// The spread is ±0.5% around the mid-market rate (realistic retail forex spread).
/// Retains a rolling 24-month history window (≈ 17,520 ticks × pairs).
/// Order = 1160 — runs after RankHistoryPhase (1150), near the end of each tick.
/// </summary>
public sealed class FxRateHistoryPhase : ITickPhase
{
    public string Name => "FxRateHistory";
    public int Order => 1160;

    /// <summary>Spread half-width: 0.5% around mid rate.</summary>
    private const decimal SpreadHalfPercent = 0.005m;

    /// <summary>Maximum history records retained per currency pair (≈24 months at 1 record/tick).</summary>
    private const int MaxRecordsPerPair = 17_520;

    /// <summary>Snapshot every tick (1 record per tick per pair).</summary>
    public async Task ProcessAsync(TickContext context)
    {
        var db = context.Db;
        var ct = CancellationToken.None;

        // Load current EUR-based rates.
        var currentRates = await db.FxRates
            .AsNoTracking()
            .Where(r => r.BaseCurrencyCode == "EUR")
            .GroupBy(r => r.QuoteCurrencyCode)
            .Select(g => new
            {
                QuoteCurrencyCode = g.Key,
                Rate = g.OrderByDescending(r => r.RateDate).Select(r => r.Rate).First()
            })
            .ToListAsync(ct);

        if (currentRates.Count == 0)
            return;

        var now = DateTime.UtcNow;
        var tick = context.CurrentTick;

        // Insert snapshots for each pair.
        var snapshots = currentRates.Select(r => new FxRateHistory
        {
            Id = Guid.NewGuid(),
            BaseCurrencyCode = "EUR",
            QuoteCurrencyCode = r.QuoteCurrencyCode,
            MidRate = r.Rate,
            BuyRate = Math.Round(r.Rate * (1m + SpreadHalfPercent), 6),
            SellRate = Math.Round(r.Rate * (1m - SpreadHalfPercent), 6),
            GameTick = tick,
            CapturedAtUtc = now
        }).ToList();

        db.FxRateHistories.AddRange(snapshots);

        // Prune oldest records to keep the rolling window bounded.
        // We only prune when the tick count is a multiple of 100 to avoid per-tick DB reads.
        // Use a single bulk-delete across all pairs rather than N per-pair queries.
        if (tick % 100 == 0)
        {
            var cutoff = tick - MaxRecordsPerPair;
            var oldRecords = await db.FxRateHistories
                .Where(h => h.BaseCurrencyCode == "EUR" && h.GameTick < cutoff)
                .ToListAsync(ct);

            if (oldRecords.Count > 0)
                db.FxRateHistories.RemoveRange(oldRecords);
        }
    }
}

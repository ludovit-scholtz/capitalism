using Api.Data;
using Api.Engine;
using Api.Security;
using HotChocolate;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using Shared.Economy;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Returns per-tick extraction history for a mine building, ordered by tick descending.
    /// </summary>
    /// <param name="buildingId">The mine building ID.</param>
    /// <param name="days">Number of game days of history to return (default 30, max 90).</param>
    [GraphQLName("getMineExtractionHistory")]
    [Authorize]
    public async Task<List<MineExtractionHistoryRecord>> GetMineExtractionHistory(
        Guid buildingId,
        int days,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var effectiveDays = Math.Clamp(days <= 0 ? 30 : days, 1, 90);
        var maxRecords = effectiveDays * GameConstants.TicksPerDay; // e.g. 30 × 24 = 720

        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .AsNoTracking()
            .Include(b => b.Company)
            .FirstOrDefaultAsync(b => b.Id == buildingId);

        if (building is null || building.Company.PlayerId != userId)
            return [];

        var records = await db.MineExtractionRecords
            .AsNoTracking()
            .Where(r => r.BuildingId == buildingId)
            .OrderByDescending(r => r.Tick)
            .Take(maxRecords)
            .ToListAsync();

        return records.Select(r => new MineExtractionHistoryRecord
        {
            Tick = r.Tick,
            ExtractedAmount = r.ExtractedAmount,
            EfficiencyPercent = r.EfficiencyPercent,
            ReserveRemaining = r.ReserveRemaining,
        }).ToList();
    }

    /// <summary>
    /// Returns a linear depletion forecast for a mine building based on the rolling average extraction rate.
    /// </summary>
    /// <param name="buildingId">The mine building ID.</param>
    [GraphQLName("getMineDepletionForecast")]
    [Authorize]
    public async Task<MineDepletionForecast?> GetMineDepletionForecast(
        Guid buildingId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .AsNoTracking()
            .Include(b => b.Company)
            .FirstOrDefaultAsync(b => b.Id == buildingId);

        if (building is null || building.Company.PlayerId != userId)
            return null;

        // Load current lot status.
        var lot = await db.BuildingLots
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.BuildingId == buildingId);

        var currentReserve = lot?.MaterialQuantity;
        var originalReserve = lot?.OriginalMaterialQuantity ?? lot?.MaterialQuantity;

        if (currentReserve is null || currentReserve <= 0m)
        {
            return new MineDepletionForecast
            {
                CurrentReserve = currentReserve ?? 0m,
                OriginalReserve = originalReserve,
                AverageExtractionRatePerTick = 0m,
                DepletionTick = null,
                Critical5PctTick = null,
                Critical20PctTick = null,
                EstimatedGameDaysRemaining = 0m,
            };
        }

        // Use last 7 game days (168 ticks) for rolling average.
        var rollingWindowTicks = 7 * GameConstants.TicksPerDay;
        var recent = await db.MineExtractionRecords
            .AsNoTracking()
            .Where(r => r.BuildingId == buildingId)
            .OrderByDescending(r => r.Tick)
            .Take(rollingWindowTicks)
            .ToListAsync();

        if (recent.Count == 0)
        {
            return new MineDepletionForecast
            {
                CurrentReserve = currentReserve,
                OriginalReserve = originalReserve,
                AverageExtractionRatePerTick = null,
                DepletionTick = null,
                Critical5PctTick = null,
                Critical20PctTick = null,
                EstimatedGameDaysRemaining = null,
            };
        }

        var averageRate = recent.Average(r => r.ExtractedAmount);

        if (averageRate <= 0m)
        {
            return new MineDepletionForecast
            {
                CurrentReserve = currentReserve,
                OriginalReserve = originalReserve,
                AverageExtractionRatePerTick = 0m,
                DepletionTick = null,
                Critical5PctTick = null,
                Critical20PctTick = null,
                EstimatedGameDaysRemaining = null,
            };
        }

        // Get the current tick from the game state.
        var gameState = await db.GameStates.AsNoTracking().FirstOrDefaultAsync();
        var currentTick = gameState?.CurrentTick ?? 0L;

        var ticksToDepletion = (long)Math.Ceiling((double)(currentReserve.Value / averageRate));
        var depletionTick = currentTick + ticksToDepletion;
        var estimatedDays = (decimal)ticksToDepletion / GameConstants.TicksPerDay;

        long? critical20PctTick = null;
        long? critical5PctTick = null;

        if (originalReserve.HasValue && originalReserve > 0m)
        {
            var reserve20Pct = originalReserve.Value * 0.20m;
            var reserve5Pct = originalReserve.Value * 0.05m;

            if (currentReserve.Value > reserve20Pct)
            {
                var ticksTo20 = (long)Math.Ceiling((double)((currentReserve.Value - reserve20Pct) / averageRate));
                critical20PctTick = currentTick + ticksTo20;
            }

            if (currentReserve.Value > reserve5Pct)
            {
                var ticksTo5 = (long)Math.Ceiling((double)((currentReserve.Value - reserve5Pct) / averageRate));
                critical5PctTick = currentTick + ticksTo5;
            }
        }

        return new MineDepletionForecast
        {
            CurrentReserve = currentReserve,
            OriginalReserve = originalReserve,
            AverageExtractionRatePerTick = averageRate,
            DepletionTick = depletionTick,
            Critical5PctTick = critical5PctTick,
            Critical20PctTick = critical20PctTick,
            EstimatedGameDaysRemaining = estimatedDays,
        };
    }
}

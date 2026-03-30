using Api.Engine;

namespace Api.Utilities;

/// <summary>
/// Canonical in-game time calculations derived from the roadmap tick model.
/// </summary>
public static class GameTime
{
    public static readonly DateTime GameStartUtc = new(
        GameConstants.GameStartYear,
        1,
        1,
        0,
        0,
        0,
        DateTimeKind.Utc);

    public static DateTime GetInGameTimeUtc(long currentTick)
    {
        return GameStartUtc.AddHours(Math.Max(currentTick, 0L));
    }

    public static int GetGameYear(long currentTick)
    {
        return GameConstants.GameStartYear + (int)(Math.Max(currentTick, 0L) / GameConstants.TicksPerYear);
    }

    public static long GetStartTickForGameYear(int gameYear)
    {
        return Math.Max((long)(gameYear - GameConstants.GameStartYear) * GameConstants.TicksPerYear, 0L);
    }

    public static long GetEndTickForGameYear(int gameYear)
    {
        return GetStartTickForGameYear(gameYear) + GameConstants.TicksPerYear - 1L;
    }

    public static long GetIncomeTaxDueTickForGameYear(int gameYear)
    {
        return GetEndTickForGameYear(gameYear) + 1L;
    }

    public static long GetNextTaxTick(long currentTick, int taxCycleTicks)
    {
        var cycleTicks = taxCycleTicks > 0 ? taxCycleTicks : GameConstants.TicksPerYear;
        var safeTick = Math.Max(currentTick, 0L);
        var cyclesCompleted = safeTick / cycleTicks;
        var currentCycleStart = cyclesCompleted * cycleTicks;

        return safeTick == currentCycleStart
            ? currentCycleStart + cycleTicks
            : (cyclesCompleted + 1L) * cycleTicks;
    }

    public static decimal ComputeEstimatedIncomeTax(decimal taxableIncome, decimal taxRatePercent)
    {
        if (taxableIncome <= 0m || taxRatePercent <= 0m)
        {
            return 0m;
        }

        return decimal.Round(
            taxableIncome * (taxRatePercent / 100m),
            2,
            MidpointRounding.AwayFromZero);
    }
}
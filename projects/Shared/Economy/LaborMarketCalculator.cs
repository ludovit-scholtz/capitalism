namespace Shared.Economy;

/// <summary>Computed state of one city's labor market for a tick.</summary>
/// <param name="LaborPool">Worker-hours per tick the city can supply to simulated companies.</param>
/// <param name="LaborDemand">Worker-hours per tick that all active units in the city require.</param>
/// <param name="Utilization">Demand divided by pool (above 1 means unfilled positions).</param>
/// <param name="UnemploymentRate">Share of the pool that is idle, 0..1.</param>
/// <param name="WageIndex">Market multiplier applied to the city's base wage (wage pressure).</param>
/// <param name="StaffingFillRate">Share of required labor that can be hired, 0..1.</param>
public sealed record LaborMarketState(
    decimal LaborPool,
    decimal LaborDemand,
    decimal Utilization,
    decimal UnemploymentRate,
    decimal WageIndex,
    decimal StaffingFillRate);

/// <summary>
/// Pure formulas of the per-city labor market. One tick is one in-game hour, so labor demand (hours required per tick) is
/// compared with a pool derived from the city population. All constants live here so both APIs and tests share them.
/// </summary>
public static class LaborMarketCalculator
{
    /// <summary>Share of the population that is part of the labor force.</summary>
    public const decimal LaborParticipationRate = 0.45m;

    /// <summary>
    /// Share of the labor force that is reachable by companies in the simulation. Real cities employ far more people than
    /// the game models, so only this slice competes for the hours that player and NPC units require.
    /// </summary>
    public const decimal SimulatedLaborShare = 0.005m;

    /// <summary>Below this utilization wages soften slightly (high unemployment lowers wage pressure).</summary>
    public const decimal SlackUtilization = 0.30m;

    /// <summary>Above this utilization employers compete for scarce workers and wages rise.</summary>
    public const decimal TightUtilization = 0.85m;

    /// <summary>Upper bound for stored utilization so absurd demand/pool ratios cannot overflow the database column.</summary>
    public const decimal MaxUtilization = 100m;

    public const decimal MinWageIndex = 0.90m;
    public const decimal MaxWageIndex = 1.50m;

    /// <summary>A company paying less than this share of the city average is at risk of strikes.</summary>
    public const decimal StrikePayRatioThreshold = 0.85m;

    public static decimal ComputeLaborPool(int population)
        => population <= 0
            ? 0m
            : decimal.Round(population * LaborParticipationRate * SimulatedLaborShare, 4, MidpointRounding.AwayFromZero);

    public static LaborMarketState Compute(int population, decimal laborDemand)
    {
        var demand = Math.Max(0m, laborDemand);
        var pool = ComputeLaborPool(population);

        if (pool <= 0m)
        {
            // A city without people cannot staff anything: no demand is a neutral market, any demand is unfillable.
            return demand <= 0m
                ? new LaborMarketState(0m, 0m, 0m, 0m, 1m, 1m)
                : new LaborMarketState(0m, demand, 2m, 0m, MaxWageIndex, 0m);
        }

        var utilization = decimal.Round(Math.Min(demand / pool, MaxUtilization), 4, MidpointRounding.AwayFromZero);
        var unemployment = decimal.Round(Math.Max(0m, 1m - utilization), 4, MidpointRounding.AwayFromZero);
        var fillRate = demand <= pool ? 1m : decimal.Round(pool / demand, 4, MidpointRounding.AwayFromZero);

        return new LaborMarketState(pool, demand, utilization, unemployment, ComputeWageIndex(utilization), fillRate);
    }

    /// <summary>
    /// Wage pressure: neutral in the comfortable middle, up to -10% when workers are idle, up to +50% when the city is
    /// fully employed or over-demanded.
    /// </summary>
    public static decimal ComputeWageIndex(decimal utilization)
    {
        var u = Math.Max(0m, utilization);
        decimal index;
        if (u < SlackUtilization)
        {
            index = 1m - ((SlackUtilization - u) / SlackUtilization * (1m - MinWageIndex));
        }
        else if (u <= TightUtilization)
        {
            index = 1m;
        }
        else
        {
            index = 1m + ((u - TightUtilization) / (1m - TightUtilization) * (MaxWageIndex - 1m));
        }

        return decimal.Round(Math.Clamp(index, MinWageIndex, MaxWageIndex), 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Company pay relative to the city reference, which is the demand-weighted average salary but never below the city's
    /// baseline wage (multiplier 1): otherwise a sole employer, or a city where everybody underpays, could pay almost nothing
    /// and still look "average".
    /// </summary>
    public static decimal ComputePayRatio(decimal companySalaryMultiplier, decimal citySalaryAverage)
        => decimal.Round(companySalaryMultiplier / Math.Max(citySalaryAverage, 1m), 4, MidpointRounding.AwayFromZero);

    public static bool IsStrikeProne(decimal payRatio) => payRatio < StrikePayRatioThreshold;

    /// <summary>Highest chance per tick that a strike-prone building walks out.</summary>
    public const decimal MaxStrikeProbabilityPerTick = 0.15m;

    /// <summary>Pay ratio at which a running strike is considered settled.</summary>
    public const decimal StrikeSettledPayRatio = 0.95m;

    /// <summary>
    /// Output never drops below this share because of labor alone: workers also commute in from the surrounding region, so
    /// a tiny or empty city cannot lock its employers out for good.
    /// </summary>
    public const decimal MinStaffingFactor = 0.20m;

    public const int MinStrikeDurationTicks = 6;
    public const int StrikeDurationSpanTicks = 6;

    /// <summary>
    /// Share of the required labor a company actually hires. A city shortage (<paramref name="cityFillRate"/>) hits
    /// everyone; pay relative to the city average matters in proportion to how tight the market is: in a slack market low
    /// pay barely costs staff, in a tight one it costs staff one-for-one, and paying above average wins a larger share of the
    /// scarce workers.
    /// </summary>
    public static decimal ComputeCompanyStaffing(decimal cityFillRate, decimal utilization, decimal payRatio)
    {
        var tightness = Math.Clamp(utilization, 0m, 1m);
        var payFactor = payRatio < 1m
            ? 1m - ((1m - payRatio) * tightness)
            : 1m + ((payRatio - 1m) * tightness);
        return decimal.Round(Math.Clamp(Math.Clamp(cityFillRate, 0m, 1m) * payFactor, MinStaffingFactor, 1m), 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>Chance per tick that a building whose pay trails the city average goes on strike (0 at or above the threshold).</summary>
    public static decimal ComputeStrikeProbability(decimal payRatio)
    {
        if (payRatio >= StrikePayRatioThreshold)
        {
            return 0m;
        }

        var shortfall = (StrikePayRatioThreshold - Math.Max(0m, payRatio)) / StrikePayRatioThreshold;
        return decimal.Round(Math.Min(MaxStrikeProbabilityPerTick, shortfall * 0.25m), 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Deterministic pseudo-random number in [0, 1) derived from the tick, the building and a salt (FNV-1a with a final
    /// avalanche), so strikes are reproducible from game state and need no stored seed.
    /// </summary>
    public static double Roll(long tick, Guid buildingId, int salt)
    {
        // Explicit little-endian layout so the roll is identical on every host.
        Span<byte> bytes = stackalloc byte[8 + 16 + 4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(bytes[..8], tick);
        buildingId.TryWriteBytes(bytes.Slice(8, 16), bigEndian: false, out _);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(24, 4), salt);

        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offset;
        foreach (var b in bytes)
        {
            hash ^= b;
            hash *= prime;
        }

        hash ^= hash >> 33;
        hash *= 0xff51afd7ed558ccdUL;
        hash ^= hash >> 33;
        hash *= 0xc4ceb9fe1a85ec53UL;
        hash ^= hash >> 33;
        return (hash >> 11) / (double)(1UL << 53);
    }

    public static int ComputeStrikeDurationTicks(double roll)
        => MinStrikeDurationTicks + (int)Math.Floor(Math.Clamp(roll, 0d, 0.999999d) * StrikeDurationSpanTicks);

    /// <summary>A strike ends when its time is up or the company has fixed its pay.</summary>
    public static bool ShouldStrikeEnd(long currentTick, long strikeUntilTick, decimal payRatio)
        => currentTick >= strikeUntilTick || payRatio >= StrikeSettledPayRatio;

    /// <summary>A "good employer" pays at least this much more than the reference (rivals or baseline wage, whichever is higher).</summary>
    public const decimal GoodEmployerPayRatio = 1.05m;

    /// <summary>
    /// True when <paramref name="salaryMultiplier"/> beats the reference by <see cref="GoodEmployerPayRatio"/>. The reference is
    /// the average pay of the company's rivals in the city, never below the baseline wage (multiplier 1), so a sole employer
    /// qualifies by clearly out-paying the baseline and cannot dilute the bar by being part of the average itself.
    /// </summary>
    public static bool IsGoodEmployer(decimal salaryMultiplier, decimal rivalsAverageMultiplier)
        => salaryMultiplier >= Math.Max(rivalsAverageMultiplier, 1m) * GoodEmployerPayRatio;
}

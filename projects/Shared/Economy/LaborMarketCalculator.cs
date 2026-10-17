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

        var utilization = decimal.Round(demand / pool, 4, MidpointRounding.AwayFromZero);
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

    /// <summary>Company pay relative to the demand-weighted city average; 1 when no average exists yet.</summary>
    public static decimal ComputePayRatio(decimal companySalaryMultiplier, decimal citySalaryAverage)
        => citySalaryAverage <= 0m
            ? 1m
            : decimal.Round(companySalaryMultiplier / citySalaryAverage, 4, MidpointRounding.AwayFromZero);

    public static bool IsStrikeProne(decimal payRatio) => payRatio < StrikePayRatioThreshold;
}

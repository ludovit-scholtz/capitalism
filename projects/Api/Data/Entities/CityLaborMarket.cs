namespace Api.Data.Entities;

/// <summary>
/// Per-city labor market state, recomputed every tick by the labor market phase from the city population and the
/// labor hours required by all active units in the city. One row per city.
/// </summary>
public sealed class CityLaborMarket
{
    public Guid CityId { get; set; }
    public City City { get; set; } = null!;

    /// <summary>Worker-hours per tick the city can supply to simulated companies.</summary>
    public decimal LaborPool { get; set; }

    /// <summary>Worker-hours per tick that all active units in the city require.</summary>
    public decimal LaborDemand { get; set; }

    /// <summary>Demand divided by pool; above 1 means unfilled positions.</summary>
    public decimal Utilization { get; set; }

    /// <summary>Share of the pool that is idle, 0..1.</summary>
    public decimal UnemploymentRate { get; set; }

    /// <summary>Market multiplier on the city's base wage (wage pressure).</summary>
    public decimal WageIndex { get; set; } = 1m;

    /// <summary>Share of required labor that can be hired, 0..1.</summary>
    public decimal StaffingFillRate { get; set; } = 1m;

    /// <summary>Labor-demand-weighted average of companies' salary multipliers in this city.</summary>
    public decimal AverageSalaryMultiplier { get; set; } = 1m;

    public long UpdatedAtTick { get; set; }
}

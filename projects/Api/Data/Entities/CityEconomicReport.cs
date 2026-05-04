namespace Api.Data.Entities;

/// <summary>
/// Stores economic health metrics for a city at the end of each tax cycle.
/// Up to 10 historical reports are kept per city (FIFO). Older reports are pruned.
/// </summary>
public sealed class CityEconomicReport
{
    public Guid Id { get; set; }

    public Guid CityId { get; set; }
    public City City { get; set; } = null!;

    /// <summary>The tick at which the tax cycle ended that triggered this report.</summary>
    public long TaxCycleEnd { get; set; }

    /// <summary>Total salary payments made by all companies in this city during the cycle.</summary>
    public decimal TotalSalaries { get; set; }

    /// <summary>Total public-sales revenue earned by all companies in this city during the cycle.</summary>
    public decimal TotalPublicRevenue { get; set; }

    /// <summary>Number of distinct companies with at least one active building in the city.</summary>
    public int ActiveCompanies { get; set; }

    /// <summary>Total power demand (MW) from all non-power-plant buildings in the city.</summary>
    public decimal TotalPowerConsumption { get; set; }

    /// <summary>Total power supply (MW) from all power-plant buildings in the city.</summary>
    public decimal TotalPowerSupply { get; set; }

    /// <summary>Average product quality index (0-1) across all public-sales records in the cycle.</summary>
    public decimal AverageProductQuality { get; set; }

    /// <summary>
    /// Composite economic health index on a 0-100 scale.
    /// Computed as: 0.4 * salaryScore + 0.3 * revenueScore + 0.15 * powerScore + 0.15 * qualityScore
    /// </summary>
    public decimal EconomicIndex { get; set; }

    public DateTime ComputedAtUtc { get; set; } = DateTime.UtcNow;
}

namespace Api.Data.Entities;

/// <summary>
/// Per-tick rental income record for APARTMENT and COMMERCIAL buildings.
/// Written by <see cref="Engine.Phases.RentPhase"/> each tick to enable historical
/// sparkline charts in the building detail view.
/// Records older than 100 ticks are pruned automatically (rolling window).
/// </summary>
public sealed class RentalIncomeRecord
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The apartment or commercial building that generated the income.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>Game tick at which this rental income was collected.</summary>
    public long Tick { get; set; }

    /// <summary>Gross rent income credited to the company bank account on this tick.</summary>
    public decimal Revenue { get; set; }

    /// <summary>Operating costs deducted this tick (maintenance at breakeven occupancy).</summary>
    public decimal Costs { get; set; }

    /// <summary>Occupancy percentage (0–100) at the time this record was written.</summary>
    public decimal OccupancyPercent { get; set; }

    /// <summary>Rent per m² applied on this tick.</summary>
    public decimal RentPerSqm { get; set; }

    /// <summary>ISO 4217 currency code of the city where the building is located.</summary>
    public string CurrencyCode { get; set; } = string.Empty;
}

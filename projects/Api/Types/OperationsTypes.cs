namespace Api.Types;

/// <summary>
/// Aggregated operations statistics for the admin Operations Dashboard.
/// Shows money inflow and outflow by category over the last 100 ticks.
/// </summary>
public sealed class OperationsStatisticsResult
{
    public long CurrentTick { get; set; }

    public int WindowTicks { get; set; } = 100;

    /// <summary>Money inflow items (revenue sources).</summary>
    public List<OperationsMoneyFlowItem> InflowItems { get; set; } = [];

    /// <summary>Money outflow items (expense categories).</summary>
    public List<OperationsMoneyFlowItem> OutflowItems { get; set; } = [];

    /// <summary>Total revenue across all inflow categories in the window.</summary>
    public decimal TotalInflow { get; set; }

    /// <summary>Total expenses across all outflow categories in the window.</summary>
    public decimal TotalOutflow { get; set; }

    /// <summary>Net money flow (inflow − outflow) in the window.</summary>
    public decimal NetFlow { get; set; }

    /// <summary>Total active player count on this server.</summary>
    public int TotalPlayerCount { get; set; }

    /// <summary>Total company count across all players.</summary>
    public int TotalCompanyCount { get; set; }

    /// <summary>Total building count.</summary>
    public int TotalBuildingCount { get; set; }
}

/// <summary>One money flow line item in the operations statistics view.</summary>
public sealed class OperationsMoneyFlowItem
{
    /// <summary>Internal ledger category or synthetic identifier.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Human-readable label for display.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Absolute amount (always positive, regardless of direction).</summary>
    public decimal Amount { get; set; }

    /// <summary>Percentage of the total inflow or outflow this item represents.</summary>
    public decimal Percentage { get; set; }

    /// <summary>Number of ledger entries contributing to this total.</summary>
    public int EntryCount { get; set; }
}

/// <summary>
/// Per-product analytics row for the Operations Dashboard analytics table.
/// Aggregates production, sales, costs, and market data across all companies.
/// </summary>
public sealed class AdminProductAnalyticsRow
{
    public Guid ProductTypeId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public decimal BasePrice { get; set; }

    // ── Production ────────────────────────────────────────────────────────
    /// <summary>Total units produced across all factories in the window.</summary>
    public decimal TotalProduced { get; set; }
    /// <summary>Number of active manufacturing units producing this product.</summary>
    public int ActiveManufacturerCount { get; set; }

    // ── Sales ─────────────────────────────────────────────────────────────
    /// <summary>Total units sold to consumers across all shops in the window.</summary>
    public decimal TotalSold { get; set; }
    /// <summary>Total revenue from public sales in the window.</summary>
    public decimal TotalRevenue { get; set; }
    /// <summary>Average selling price per unit across all shops.</summary>
    public decimal? AvgSellingPrice { get; set; }
    /// <summary>Average market price per unit across all public sales records in the window.</summary>
    public decimal? AvgMarketPrice { get; set; }
    /// <summary>Total demand volume in the window, used as market-size proxy.</summary>
    public decimal MarketSize { get; set; }
    /// <summary>Number of active public-sales units selling this product.</summary>
    public int ActiveSellerCount { get; set; }

    // ── Costs ─────────────────────────────────────────────────────────────
    /// <summary>Total purchasing cost of raw materials for this product in the window.</summary>
    public decimal TotalMaterialCost { get; set; }
    /// <summary>Total labor cost incurred by manufacturing units in the window.</summary>
    public decimal TotalLaborCost { get; set; }
    /// <summary>Total energy cost incurred by manufacturing units in the window.</summary>
    public decimal TotalEnergyCost { get; set; }
    /// <summary>Total cost = materials + labor + energy.</summary>
    public decimal TotalCost { get; set; }

    // ── Market ────────────────────────────────────────────────────────────
    /// <summary>Estimated market saturation: sellers vs available city demand (0–1, where 1 = fully saturated).</summary>
    public decimal MarketSaturation { get; set; }
    /// <summary>Total marketing spend on brands associated with this product.</summary>
    public decimal TotalMarketingSpend { get; set; }
    /// <summary>Total research spend associated with this product.</summary>
    public decimal TotalResearchSpend { get; set; }
    /// <summary>Number of cities where this product is actively sold.</summary>
    public int ActiveCityCount { get; set; }
}

/// <summary>Optional filters for adminProductAnalytics.</summary>
public sealed class AdminProductAnalyticsInput
{
    public Guid? CompanyId { get; set; }
    public Guid? ProductTypeId { get; set; }
    public Guid? CityId { get; set; }
    /// <summary>Window size in ticks; clamped to 1..720 (30 days).</summary>
    public int? WindowTicks { get; set; }
}

/// <summary>Result returned by the adminProductAnalytics query.</summary>
public sealed class AdminProductAnalyticsResult
{
    public int WindowTicks { get; set; }
    public long CurrentTick { get; set; }
    public List<AdminProductAnalyticsRow> Rows { get; set; } = [];
}

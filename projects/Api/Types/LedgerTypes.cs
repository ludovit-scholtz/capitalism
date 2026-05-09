namespace Api.Types;

/// <summary>Top-level ledger summary for a company.</summary>
public sealed class CompanyLedgerSummary
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public int GameYear { get; set; }
    public bool IsCurrentGameYear { get; set; }
    public decimal CurrentCash { get; set; }
    /// <summary>ISO 4217 currency code of the company's primary operating city. Defaults to "EUR".</summary>
    public string PrimaryCurrencyCode { get; set; } = "EUR";
    /// <summary>Display symbol for the primary currency (e.g. "€", "Kč").</summary>
    public string PrimaryCurrencySymbol => Mutation.GetCurrencySymbol(PrimaryCurrencyCode);
    /// <summary>True when the company has buildings in multiple cities with different currencies.</summary>
    public bool HasMixedCurrencies { get; set; }
    // Income Statement
    public decimal TotalRevenue { get; set; }
    public decimal TotalMediaHouseIncome { get; set; }
    public decimal TotalPurchasingCosts { get; set; }
    public decimal TotalShippingCosts { get; set; }
    public decimal TotalLaborCosts { get; set; }
    public decimal TotalEnergyCosts { get; set; }
    public decimal TotalMarketingCosts { get; set; }
    public decimal TotalTaxPaid { get; set; }
    public decimal TotalOtherCosts { get; set; }
    public decimal TaxableIncome { get; set; }
    public decimal EstimatedIncomeTax { get; set; }
    public decimal NetIncome { get; set; }
    // Banking income/expense (income statement)
    public decimal TotalDepositInterestReceived { get; set; }
    public decimal TotalDepositInterestPaid { get; set; }
    public decimal TotalLoanInterestIncome { get; set; }
    public decimal TotalLoanInterestExpense { get; set; }
    // Balance Sheet
    public decimal PropertyValue { get; set; }
    public decimal PropertyAppreciation { get; set; }
    public decimal BuildingValue { get; set; }
    public decimal InventoryValue { get; set; }
    public decimal TotalDepositsPlaced { get; set; }
    public decimal TotalAssets { get; set; }
    public decimal TotalPropertyPurchases { get; set; }
    public decimal TotalStockPurchaseCashOut { get; set; }
    public decimal TotalStockSaleCashIn { get; set; }
    // Cash Flow
    public decimal CashFromOperations { get; set; }
    public decimal CashFromInvestments { get; set; }
    public decimal CashFromBanking { get; set; }
    public long FirstRecordedTick { get; set; }
    public long LastRecordedTick { get; set; }
    public long IncomeTaxDueAtTick { get; set; }
    public DateTime IncomeTaxDueGameTimeUtc { get; set; }
    public int IncomeTaxDueGameYear { get; set; }
    public bool IsIncomeTaxSettled { get; set; }
    public List<BuildingLedgerSummary> BuildingSummaries { get; set; } = [];
    public List<CompanyLedgerHistoryYear> History { get; set; } = [];
}


public sealed class CompanyLedgerHistoryYear
{
    public int GameYear { get; set; }
    public bool IsCurrentGameYear { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalLaborCosts { get; set; }
    public decimal TotalEnergyCosts { get; set; }
    public decimal NetIncome { get; set; }
    public decimal TotalTaxPaid { get; set; }
    public decimal TaxableIncome { get; set; }
    public decimal EstimatedIncomeTax { get; set; }
    public long FirstRecordedTick { get; set; }
    public long LastRecordedTick { get; set; }
}

public sealed class BuildingLedgerSummary
{
    public Guid BuildingId { get; set; }
    public string BuildingName { get; set; } = string.Empty;
    public string BuildingType { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Costs { get; set; }
    /// <summary>ISO 4217 currency code of the city where this building is located.</summary>
    public string CurrencyCode { get; set; } = "EUR";
    /// <summary>Display symbol for the building's city currency (e.g. "€", "Kč").</summary>
    public string CurrencySymbol => Mutation.GetCurrencySymbol(CurrencyCode);
}

public sealed class CompanyCityFinancialSummary
{
    public Guid CityId { get; set; }
    public string CityName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "EUR";
    public string CurrencySymbol => Mutation.GetCurrencySymbol(CurrencyCode);
    public decimal Revenue { get; set; }
    public decimal Costs { get; set; }
    public decimal Profit { get; set; }
    public List<CityRevenueTrendPoint> RevenueTrend { get; set; } = [];
}

public sealed class CityRevenueTrendPoint
{
    public long Tick { get; set; }
    public decimal Revenue { get; set; }
}

public sealed class BuildingFinancialTimeline
{
    public Guid BuildingId { get; set; }
    public string BuildingName { get; set; } = string.Empty;
    public long DataFromTick { get; set; }
    public long DataToTick { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalCosts { get; set; }
    public decimal TotalProfit { get; set; }
    public List<BuildingFinancialTickSnapshot> Timeline { get; set; } = [];
}

public sealed class BuildingFinancialTickSnapshot
{
    public long Tick { get; set; }
    public decimal Sales { get; set; }
    public decimal Costs { get; set; }
    public decimal Profit { get; set; }
}

/// <summary>Per-tick P&amp;L analytics for a power plant building.</summary>
public sealed class PowerPlantAnalytics
{
    public Guid BuildingId { get; set; }
    public string BuildingName { get; set; } = string.Empty;
    public string PlantType { get; set; } = string.Empty;
    public decimal CurrentOutputMw { get; set; }
    /// <summary>Player-set dispatch target (0–100%). Returned from the database at query time.</summary>
    public int DispatchTargetPercent { get; set; }
    /// <summary>Current fuel reserve in MWh (COAL/GAS plants). Always 0 for renewable/nuclear.</summary>
    public decimal FuelReserveMwh { get; set; }
    /// <summary>
    /// Maximum fuel reserve capacity in MWh — sum of (FuelReserveCapacityPerUnitLevel × level)
    /// for all installed FUEL_PURCHASE units.  0 when no FUEL_PURCHASE units are installed.
    /// </summary>
    public decimal MaxFuelReserveMwh { get; set; }
    /// <summary>
    /// Reserve fill level expressed as an integer 0–100 percentage.
    /// 100 = tank full; 0 = empty.  0 when MaxFuelReserveMwh is 0 (no FP units).
    /// </summary>
    public int FuelReservePercent { get; set; }
    /// <summary>
    /// Maximum fuel procurement per tick (in MWh) from all installed FUEL_PURCHASE units at 100% dispatch.
    /// This is the raw unit capacity before dispatch scaling.
    /// </summary>
    public decimal FuelPurchaseCapacityMwhPerTick { get; set; }
    /// <summary>
    /// Maximum additional MW contributed by ENERGY_PRODUCING units when the reserve is full.
    /// Shows the player how much generation capacity they are leaving unused if the reserve is low.
    /// </summary>
    public decimal EnergyProducingCapacityMw { get; set; }
    /// <summary>
    /// Estimated MW of output currently constrained because the fuel reserve is too low to
    /// satisfy all ENERGY_PRODUCING units this tick.
    /// = max(0, EnergyProducingCapacityMw − current reserve).
    /// 0 for non-thermal plants; 0 when the reserve is sufficient.
    /// <para>
    /// The calculation uses a 1:1 MWh-to-MW ratio (1 MWh of stored fuel reserve supports 1 MW
    /// of ENERGY_PRODUCING output capacity).  This is an intentional game-balance simplification
    /// that keeps thermal reserve planning intuitive for players: if the reserve drops below
    /// the total EP capacity (in MW), the shortfall directly indicates the lost MW of output.
    /// </para>
    /// </summary>
    public decimal FuelConstrainedOutputMw { get; set; }
    /// <summary>
    /// Human-readable label describing the fuel type used by this plant, e.g. "Coal" or "Natural Gas".
    /// Empty string for non-thermal plants.
    /// </summary>
    public string FuelTypeLabel { get; set; } = string.Empty;
    /// <summary>
    /// Base fuel cost in EUR per MWh for this plant type (before city FuelPriceIndex scaling).
    /// Useful for the frontend to display per-fuel-type cost guidance.
    /// 0 for non-thermal plants.
    /// </summary>
    public decimal FuelCostPerMwhEur { get; set; }
    public long DataFromTick { get; set; }
    public long DataToTick { get; set; }
    public decimal TotalSurplusIncome { get; set; }
    public decimal TotalGridFines { get; set; }
    public decimal TotalOperatingCosts { get; set; }
    /// <summary>Total fuel procurement costs over the analytics window (COAL/GAS plants only).</summary>
    public decimal TotalFuelCosts { get; set; }
    public decimal TotalNetProfit { get; set; }
    public List<PowerPlantTickSnapshot> Timeline { get; set; } = [];
}

/// <summary>Per-tick snapshot for the power plant P&amp;L timeline.</summary>
public sealed class PowerPlantTickSnapshot
{
    public long Tick { get; set; }
    public decimal SurplusIncome { get; set; }
    public decimal GridFine { get; set; }
    public decimal OperatingCosts { get; set; }
    /// <summary>Fuel procurement cost for this tick (COAL/GAS plants only).</summary>
    public decimal FuelCosts { get; set; }
    public decimal NetProfit { get; set; }
}

public sealed class LedgerEntryResult
{
    public Guid Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public long RecordedAtTick { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public Guid? BuildingId { get; set; }
    public string? BuildingName { get; set; }
    public string? BuildingType { get; set; }
    public Guid? BuildingUnitId { get; set; }
    public Guid? ProductTypeId { get; set; }
    public string? ProductName { get; set; }
    public Guid? ResourceTypeId { get; set; }
    public string? ResourceName { get; set; }
    /// <summary>ISO 4217 currency code for this entry (from the building's city, or "EUR" for company-level entries).</summary>
    public string CurrencyCode { get; set; } = "EUR";
    /// <summary>Display symbol for the entry's currency (e.g. "€", "Kč").</summary>
    public string CurrencySymbol => Mutation.GetCurrencySymbol(CurrencyCode);
    public string? EventTag { get; set; }
    public string? EventDescription { get; set; }
}

public sealed class PublicSalesAnalytics
{
    public Guid BuildingUnitId { get; set; }
    public Guid BuildingId { get; set; }
    public string BuildingName { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    /// <summary>Product type ID being tracked by this unit. Null when no product has been configured or sold.</summary>
    public Guid? ProductTypeId { get; set; }
    /// <summary>Display name of the product being sold. Null when no product data is available.</summary>
    public string? ProductName { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalQuantitySold { get; set; }
    public decimal AveragePricePerUnit { get; set; }
    public decimal CurrentSalesCapacity { get; set; }
    public long DataFromTick { get; set; }
    public long DataToTick { get; set; }
    public List<SalesTickSnapshot> RevenueHistory { get; set; } = [];
    public List<MarketShareEntry> MarketShare { get; set; } = [];
    public List<PriceTickSnapshot> PriceHistory { get; set; } = [];
    /// <summary>
    /// Revenue trend direction comparing the most-recent 5 ticks vs the prior 5 ticks.
    /// Values: UP | FLAT | DOWN | NO_DATA (fewer than 2 ticks of history).
    /// </summary>
    public string TrendDirection { get; set; } = "NO_DATA";
    /// <summary>
    /// Demand/elasticity signal derived from recent sales data.
    /// Values: NO_DATA | SUPPLY_CONSTRAINED | STRONG | MODERATE | WEAK
    /// </summary>
    public string DemandSignal { get; set; } = "NO_DATA";
    /// <summary>Short player-facing recommended action based on current market conditions.</summary>
    public string ActionHint { get; set; } = string.Empty;
    /// <summary>Average demand signal across recent ticks (0-1 ratio vs sales capacity).</summary>
    public decimal RecentUtilization { get; set; }
    /// <summary>
    /// Price elasticity index approximated from the demand model.
    /// Defined as (dQ/Q) / (dP/P) at the current price point.
    /// Negative values indicate normal goods (higher price → lower demand).
    /// Typical range: -3.0 (very elastic) to 0.0 (perfectly inelastic).
    /// Null when insufficient data or no base price available.
    /// </summary>
    public decimal? ElasticityIndex { get; set; }
    /// <summary>
    /// Fraction of estimated total city demand that was unserved (demand > total sold by all players).
    /// 0 = all demand satisfied. 1 = all demand unmet. Null when no demand data available.
    /// </summary>
    public decimal? UnmetDemandShare { get; set; }
    /// <summary>Population index of the building lot (1.0 = city average). Higher is better for sales.</summary>
    public decimal? PopulationIndex { get; set; }
    /// <summary>Current inventory quality of the product in this unit (0.0–1.0). Affects demand directly.</summary>
    public decimal? InventoryQuality { get; set; }
    /// <summary>Brand awareness of the selling company for this product (0.0–1.0). Null if no brand set.</summary>
    public decimal? BrandAwareness { get; set; }
    /// <summary>
    /// Combined brand quality score (0.0–1.0) for this product. Blends R&amp;D research quality and
    /// marketing-driven prestige. Higher quality amplifies the brand demand factor by up to 50%.
    /// Null when no brand data is available.
    /// </summary>
    public decimal? BrandQuality { get; set; }
    /// <summary>
    /// Total gross profit across the analytics window (TotalRevenue - QuantitySold × BasePrice).
    /// Positive when selling above the product base price. Null when base price is unavailable.
    /// </summary>
    public decimal? TotalProfit { get; set; }
    /// <summary>Per-tick gross profit history, ordered by tick ascending. Null when base price is unavailable.</summary>
    public List<ProfitTickSnapshot>? ProfitHistory { get; set; }
    /// <summary>
    /// Ordered list of demand drivers (positive and negative) explaining the current sales outcome.
    /// Each entry carries a factor name, impact direction, score, and player-facing description.
    /// </summary>
    public List<DemandDriverEntry> DemandDrivers { get; set; } = [];
    /// <summary>
    /// Current market trend factor for this product in this city.
    /// Range [0.5, 1.5]; 1.0 = neutral. Values above 1.0 indicate a hot market
    /// (trend is boosting demand); values below 1.0 indicate a cold market.
    /// Null when no trend state exists yet (first tick).
    /// </summary>
    public decimal? TrendFactor { get; set; }
    /// <summary>ISO 4217 currency code for the city where this unit is located (e.g. "EUR", "CZK").</summary>
    public string CityCurrencyCode { get; set; } = "EUR";
    /// <summary>
    /// City-average reference price for the product expressed in the city's local currency.
    /// Computed as product.BasePrice × cityFxRate. Used as the minimum recommended price
    /// and as the pricing-guidance benchmark shown in the sales unit editor.
    /// Null when no product is configured on this unit.
    /// </summary>
    public decimal? CityAveragePrice { get; set; }
    /// <summary>
    /// Seasonal demand outlook for this public sales unit.
    /// Null when no DemandSeasonality data exists for the product (defaults to 1.0× demand).
    /// </summary>
    public SeasonalOutlook? SeasonalOutlook { get; set; }
}

public sealed class ProfitTickSnapshot
{
    public long Tick { get; set; }
    /// <summary>Gross profit for the tick (revenue − quantity × basePrice).</summary>
    public decimal Profit { get; set; }
    /// <summary>Gross margin percentage (0–100+). Null when basePrice is zero.</summary>
    public decimal? GrossMarginPct { get; set; }
}

/// <summary>
/// Explains a single demand-influencing factor for a public sales unit.
/// Factors: PRICE | QUALITY | BRAND | LOCATION | SATURATION | COMPETITION
/// Impact:  POSITIVE | NEUTRAL | NEGATIVE
/// Score:   0.0 – 1.0 (strength of the factor, regardless of direction).
/// </summary>
public sealed class DemandDriverEntry
{
    /// <summary>Factor identifier: PRICE | QUALITY | BRAND | LOCATION | SATURATION | COMPETITION</summary>
    public string Factor { get; set; } = string.Empty;
    /// <summary>POSITIVE | NEUTRAL | NEGATIVE</summary>
    public string Impact { get; set; } = string.Empty;
    /// <summary>Strength of the factor (0.0–1.0).</summary>
    public decimal Score { get; set; }
    /// <summary>Short player-facing description of this driver.</summary>
    public string Description { get; set; } = string.Empty;
}

public sealed class SalesTickSnapshot
{
    public long Tick { get; set; }
    public decimal Revenue { get; set; }
    public decimal QuantitySold { get; set; }
}

public sealed class PriceTickSnapshot
{
    public long Tick { get; set; }
    public decimal PricePerUnit { get; set; }
}

public sealed class MarketShareEntry
{
    /// <summary>Company name, or "Unmet Demand" for unsatisfied city demand.</summary>
    public string Label { get; set; } = string.Empty;
    public Guid? CompanyId { get; set; }
    public decimal Share { get; set; }
    /// <summary>True when this entry represents unserved/unmet market demand, not an actual seller.</summary>
    public bool IsUnmet { get; set; }
}

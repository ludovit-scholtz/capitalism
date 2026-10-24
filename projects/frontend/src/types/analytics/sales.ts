export interface SalesTickSnapshot {
  tick: number
  revenue: number
  quantitySold: number
}

export interface PriceTickSnapshot {
  tick: number
  pricePerUnit: number
}

export interface ProfitTickSnapshot {
  tick: number
  /** Gross profit for the tick (revenue − quantity × basePrice). */
  profit: number
  /** Gross margin percentage (0–100+). Null when basePrice is zero. */
  grossMarginPct: number | null
}

/** Explains one demand-influencing factor for a public sales unit. */
export interface DemandDriverEntry {
  /** PRICE | QUALITY | BRAND | LOCATION | SATURATION | COMPETITION */
  factor: string
  /** POSITIVE | NEUTRAL | NEGATIVE */
  impact: string
  /** Strength of the factor (0.0–1.0). */
  score: number
  /** Short player-facing description. */
  description: string
}

export interface MarketShareEntry {
  label: string
  companyId: string | null
  share: number
  /** True when this entry represents unserved market demand, not an actual seller. */
  isUnmet: boolean
}

export interface QuarterForecast {
  /** 0=Q1, 1=Q2, 2=Q3, 3=Q4 */
  quarterIndex: number
  /** e.g. "Q2 (Apr–Jun)" */
  label: string
  /** Demand multiplier for this quarter */
  multiplier: number
  /** Whether this is the currently active quarter */
  isCurrent: boolean
  /** GREEN | YELLOW | ORANGE | RED */
  colorCode: string
}

export interface SeasonalOutlook {
  /** Current quarter index: 0=Q1, 1=Q2, 2=Q3, 3=Q4 */
  currentQuarterIndex: number
  /** Label for current quarter e.g. "Q1 (Jan–Mar)" */
  currentQuarterLabel: string
  /** Demand multiplier active this quarter (e.g. 1.5, 0.8) */
  currentMultiplier: number
  /** HIGH | MODERATE | BELOW_AVERAGE | LOW */
  demandLevel: string
  /** Per-quarter demand forecast for Q1–Q4 */
  quarterForecasts: QuarterForecast[]
  /** Contextual callout text for this product and season */
  callout: string
}

export interface PublicSalesAnalytics {
  buildingUnitId: string
  buildingId: string
  buildingName: string
  cityName: string
  /** Product type ID being tracked. Null when no product configured or sold. */
  productTypeId: string | null
  /** Display name of the product being sold. Null when no product data available. */
  productName: string | null
  totalRevenue: number
  totalQuantitySold: number
  averagePricePerUnit: number
  currentSalesCapacity: number
  dataFromTick: number
  dataToTick: number
  revenueHistory: SalesTickSnapshot[]
  marketShare: MarketShareEntry[]
  priceHistory: PriceTickSnapshot[]
  /**
   * Revenue trend vs prior 5-tick window.
   * UP | FLAT | DOWN | NO_DATA
   */
  trendDirection: string
  /** NO_DATA | SUPPLY_CONSTRAINED | STRONG | MODERATE | WEAK */
  demandSignal: string
  actionHint: string
  recentUtilization: number
  /** Price elasticity index ≤ 0; more negative = more elastic (price-sensitive). Null if insufficient data. */
  elasticityIndex: number | null
  /** Fraction of city demand that went unserved (0–1). Null when no data. */
  unmetDemandShare: number | null
  /** Population index of the building lot (1.0 = city average). */
  populationIndex: number | null
  /** Current inventory quality (0–1). */
  inventoryQuality: number | null
  /** Brand awareness for this product (0–1). */
  brandAwareness: number | null
  /**
   * Combined brand quality score (0–1): blends R&D research quality and marketing prestige.
   * Higher quality amplifies brand demand factor by up to 50%. Null when no brand data available.
   */
  brandQuality: number | null
  /** Total gross profit (revenue − quantity × basePrice). Null when base price unavailable. */
  totalProfit: number | null
  /** Per-tick gross profit history, ordered by tick ascending. Null when base price unavailable. */
  profitHistory: ProfitTickSnapshot[] | null
  /** Structured demand driver explanations: price, quality, brand, location factors. */
  demandDrivers: DemandDriverEntry[]
  /**
   * Current market-trend factor for this product in this city.
   * Range [0.5, 1.5]; 1.0 = neutral. > 1.0 = hot market, < 1.0 = cold market.
   * Null when no trend state exists yet (first tick).
   */
  trendFactor: number | null
  /** ISO 4217 currency code for the city where this unit is located (e.g. "EUR", "CZK"). */
  cityCurrencyCode: string
  /**
   * City-average reference price for the product in the city's local currency (basePrice × fxRate).
   * Used as the minimum recommended price and the pricing-guidance benchmark.
   * Null when no product is configured on this unit.
   */
  cityAveragePrice: number | null
  /**
   * Seasonal demand outlook for this public sales unit.
   * Null when no DemandSeasonality data exists for the product (defaults to 1.0× demand).
   */
  seasonalOutlook: SeasonalOutlook | null
  /**
   * City-wide market clearing price for this product (weighted average across all sellers)
   * in the last 100 ticks, expressed in the city's local currency.
   * Used by the price recommendation badge: green ≤ market, amber 10–30% above, red >30%.
   * Null when no city-wide sales data exists yet.
   */
  cityMarketClearingPrice: number | null
}

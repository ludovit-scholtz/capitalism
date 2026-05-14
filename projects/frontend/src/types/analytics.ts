export interface PowerPlantTickSnapshot {
  tick: number
  surplusIncome: number
  gridFine: number
  operatingCosts: number
  /** Fuel procurement costs this tick (COAL/GAS plants only). */
  fuelCosts: number
  /** Spot-market energy sales revenue this tick. */
  spotMarketRevenue: number
  netProfit: number
}

/** An active energy spot-market listing (power plant selling surplus capacity). */
export interface EnergyMarketListing {
  listingId: string
  buildingId: string
  buildingName: string
  companyId: string
  companyName: string
  cityId: string
  plantType: string
  pricePerKwhLocal: number
  capacityKw: number
  availableKw: number
  createdAtTick: number
  createdAtUtc: string
}

export interface PowerPlantAnalytics {
  buildingId: string
  buildingName: string
  plantType: string
  currentOutputMw: number
  /** Dispatch target percentage (0–100). 100 = full output. */
  dispatchTargetPercent: number
  /** Current fuel reserve in MWh (thermal plants only). */
  fuelReserveMwh: number
  /** Maximum fuel reserve capacity in MWh based on installed FUEL_PURCHASE units. 0 for non-thermal. */
  maxFuelReserveMwh: number
  /** Reserve fill level as 0–100 integer percent. 0 when no FP units installed. */
  fuelReservePercent: number
  /** Max fuel procurement per tick (MWh) from all FP units at 100% dispatch. 0 for non-thermal. */
  fuelPurchaseCapacityMwhPerTick: number
  /** Max additional MW from ENERGY_PRODUCING units at full reserve. 0 for non-thermal. */
  energyProducingCapacityMw: number
  /** MW of EP capacity unused because reserve is too low. 0 when reserve is sufficient or no EP units. */
  fuelConstrainedOutputMw: number
  /** Human-readable fuel type label, e.g. "Coal" or "Natural Gas". Empty for non-thermal. */
  fuelTypeLabel: string
  /** Base fuel cost in EUR/MWh for this plant type (before city price-index scaling). 0 for non-thermal. */
  fuelCostPerMwhEur: number
  dataFromTick: number
  dataToTick: number
  totalSurplusIncome: number
  totalGridFines: number
  totalOperatingCosts: number
  /** Total fuel procurement costs across the analytics window (COAL/GAS only). */
  totalFuelCosts: number
  /** Total energy spot-market sales revenue across the analytics window. */
  totalSpotMarketRevenue: number
  totalNetProfit: number
  /** Active energy spot-market listing for this power plant (null if none). */
  activeListing: EnergyMarketListing | null
  timeline: PowerPlantTickSnapshot[]
}

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

/** Per-tick cost and production snapshot for a MANUFACTURING unit. */
export interface UnitProductTickSnapshot {
  tick: number
  /** Labor cost charged on this tick. */
  laborCost: number
  /** Energy cost charged on this tick. */
  energyCost: number
  /** Total operating cost (labor + energy) for this tick. */
  totalCost: number
  /** Quantity of the product produced on this tick. */
  quantityProduced: number
  /**
   * Estimated revenue = quantityProduced × product.basePrice.
   * Null when base price is unavailable.
   */
  estimatedRevenue: number | null
  /**
   * Estimated profit = estimatedRevenue − totalCost.
   * Null when base price is unavailable.
   */
  estimatedProfit: number | null
}

/**
 * Product-level analytics for a MANUFACTURING unit.
 * Shows cost history, production quantity, and estimated economics per tick.
 */
export interface UnitProductAnalytics {
  buildingUnitId: string
  /** The unit type (e.g. MANUFACTURING). */
  unitType: string
  /** Product type ID being produced. Null when no product is configured. */
  productTypeId: string | null
  /** Display name of the product being produced. Null when no product data is available. */
  productName: string | null
  /** First tick in the analytics window (oldest data). */
  dataFromTick: number
  /** Last tick in the analytics window (most recent data). */
  dataToTick: number
  /** Total labor + energy cost over the analytics window. */
  totalCost: number
  /** Total units produced over the analytics window. */
  totalQuantityProduced: number
  /**
   * Estimated total revenue = totalQuantityProduced × product.basePrice.
   * Null when base price is unavailable.
   */
  estimatedRevenue: number | null
  /**
   * Estimated total profit = estimatedRevenue − totalCost.
   * Null when base price is unavailable.
   */
  estimatedProfit: number | null
  /** Per-tick snapshots ordered by tick ascending. */
  snapshots: UnitProductTickSnapshot[]
  /** ISO 4217 currency code for the city where this unit is located (e.g. "EUR", "CZK"). */
  cityCurrencyCode: string
}

/**
 * Research brand state returned by the companyBrands query.
 * Represents a brand entity accumulated by R&D research (product quality)
 * and marketing activity (brand awareness).
 */
export interface ResearchBrandState {
  id: string
  companyId: string
  name: string
  /** PRODUCT | CATEGORY | COMPANY */
  scope: string
  productTypeId: string | null
  productName: string | null
  industryCategory: string | null
  /** 0.0–1.0: Driven by marketing unit spend. Higher = stronger brand recognition with customers. */
  awareness: number
  /** 0.0–1.0: Driven by PRODUCT_QUALITY R&D. Higher = better manufactured output quality. */
  quality: number
  /** 0.0–1.0: Accumulated from sustained marketing spend. Decays when investment stops. */
  marketingQuality: number
  /**
   * 0.0–1.0: Combined brand quality blending R&D quality and marketing prestige.
   * This is the value that amplifies sales demand (up to 50% bonus at quality = 1.0).
   */
  combinedBrandQuality: number
  /**
   * Accumulated R&D research budget (game currency) invested into this product.
   * Grows each tick by fraction of PRODUCT_QUALITY unit operating costs; decays 0.1%/tick.
   * Null when no research has been performed for this product yet.
   */
  accumulatedResearchBudget?: number | null
  /**
   * Budget required to reach 100% quality when uncontested (product base-price × 1 000, min 5 000).
   * Null when not a product-scoped brand.
   */
  baseResearchBudget?: number | null
  /**
   * Highest research budget across all companies researching this same product.
   * Your quality = your budget / max(this, baseResearchBudget).
   * Null when no research exists globally for this product.
   */
  maxCompetitorBudget?: number | null
  /**
   * ≥ 1.0: Driven by BRAND_QUALITY R&D. A value of 1.5 means each unit of marketing budget
   * produces 50% more brand awareness than baseline. Does NOT directly grant awareness.
   */
  marketingEfficiencyMultiplier: number
}

/** Per-product/per-city analytics row for the campaign analytics dashboard. */
export interface CampaignAnalyticsRow {
  buildingUnitId: string
  buildingId: string
  buildingName: string
  productName: string | null
  productTypeId: string | null
  cityName: string

  // Brand metrics
  brandAwareness: number | null
  brandQuality: number | null
  marketingQuality: number | null

  // Pricing metrics
  currentPrice: number | null
  basePrice: number | null
  priceIndex: number | null
  pricePremiumPct: number | null

  // Recent performance
  revenueLastTicks: number
  quantityLastTicks: number
  utilizationRate: number
  trendDirection: string
  trendFactor: number | null
  demandSignal: string

  // Demand insight
  topPositiveFactor: string | null
  topNegativeFactor: string | null

  // Campaign ROI
  marketingSpendLastTicks: number | null
  brandRevenueBoost: number | null
  campaignImpact: string

  // Strategic insights
  brandVsPriceBalance: string
  recommendation: string
  cityCurrencyCode: string
}

/** Aggregated campaign analytics result for a company. */
export interface CampaignAnalyticsResult {
  companyId: string
  windowTicks: number
  totalRevenue: number
  totalMarketingSpend: number
  bestPerformingCity: string | null
  bestPerformingProduct: string | null
  globalRecommendation: string
  rows: CampaignAnalyticsRow[]
}

/** Seller row for one product in the city-level market intelligence dashboard. */
export interface MarketIntelligenceSellerRow {
  rank: number
  companyId: string
  displayName: string
  askingPricePerUnit: number
  brandQuality: number | null
  estimatedWeeklySalesVolume: number
  marketShare: number
}

/** Per-product city market intelligence summary. */
export interface MarketIntelligenceProductRow {
  productTypeId: string
  productName: string
  productSlug: string
  totalWeeklySalesVolume: number
  sellers: MarketIntelligenceSellerRow[]
}

/** City-level competitive market intelligence for the last in-game week. */
export interface MarketIntelligenceResult {
  cityId: string
  cityName: string
  dataFromTick: number
  dataToTick: number
  products: MarketIntelligenceProductRow[]
}

/** Advertising income entry for a media house */
export interface MediaHouseIncomeEntry {
  tick: number
  amount: number
  description: string
}

/** Brand effect row showing how a campaign through this media house impacts a brand */
export interface MediaHouseBrandEffectRow {
  companyId: string
  companyName: string
  /** PRODUCT | CATEGORY | COMPANY */
  brandScope: string
  productName: string
  /** 0–100 */
  brandAwareness: number
  /** 0–100 */
  marketingQuality: number
  effectivenessMultiplierApplied: number
}

/** Full analytics result for a MEDIA_HOUSE building */
export interface MediaHouseAnalyticsResult {
  buildingId: string
  buildingName: string
  /** NEWSPAPER | RADIO | TV */
  mediaType: string
  level: number
  contentValue: number
  contentRankingPct: number
  channelMultiplier: number
  effectiveMultiplier: number
  currentEfficiencyPct: number
  nextLevelEfficiencyPct: number
  isMaxLevel: boolean
  upgradeCostEur: number | null
  upgradeTimeTicks: number | null
  maxLevel: number
  totalIncomeLast100Ticks: number
  avgIncomePerTick: number
  incomeHistory: MediaHouseIncomeEntry[]
  advertiserCount: number
  brandEffects: MediaHouseBrandEffectRow[]
  /** DOMINANT | COMPETITIVE | GROWING | EARLY_STAGE */
  strategyRating: string
  strategyTip: string
}

export interface MediaHouseBoostHistoryPoint {
  tick: number
  boost: number
}

export interface MediaHouseUnitState {
  id: string
  targetCompanyId: string
  targetCompanyName: string
  mediaType: string
  campaignBudgetPerTick: number
  brandQualityBoostPerTick: number
  isActive: boolean
  laborCostPerTick: number
  energyCostPerTick: number
}

export interface MediaHouseStatsResult {
  buildingId: string
  currentBoostDelivered: number
  campaignCostThisTaxCycle: number
  estimatedSalesImpact: number
  boostHistory: MediaHouseBoostHistoryPoint[]
  units: MediaHouseUnitState[]
}

// ── Market Dashboard types ────────────────────────────────────────────────

export interface MarketPriceResult {
  cityId: string
  productTypeId: string
  productName: string
  clearingPrice: number
  totalVolume: number
  totalRevenue: number
  sellerCount: number
  currencyCode: string
  fromTick: number
  toTick: number
}

export interface MarketPriceHistoryPoint {
  tick: number
  clearingPrice: number
  totalVolume: number
  totalRevenue: number
  sellerCount: number
}

export interface ProductDemandEntry {
  productTypeId: string
  productName: string
  industry: string
  totalDemand: number
  totalQuantitySold: number
  satisfactionRate: number
  averageClearingPrice: number
  totalRevenue: number
  sellerCount: number
  topCompetitorCompanyName: string | null
  topCompetitorMarketSharePercent: number
}

export interface CityDemandSummaryResult {
  cityId: string
  cityName: string
  currencyCode: string
  fromTick: number
  toTick: number
  products: ProductDemandEntry[]
}

export interface RentalTickSnapshot {
  tick: number
  revenue: number
  occupancyPercent: number
  rentPerSqm: number
}

export interface ApartmentBuildingDetail {
  buildingId: string
  occupancyPercent: number
  totalAreaSqm: number
  rentPerSqm: number | null
  cityAverageRentPerSqm: number
  currencyCode: string
  revenueHistory: RentalTickSnapshot[]
}

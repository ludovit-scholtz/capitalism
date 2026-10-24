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

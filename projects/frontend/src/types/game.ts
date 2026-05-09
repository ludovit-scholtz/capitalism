import type { AccountContextType } from './auth'

export interface StockExchangeListing {
  companyId: string
  companyName: string
  primaryCityName: string
  primaryIndustry: string
  totalSharesIssued: number
  publicFloatShares: number
  sharePrice: number
  dailyChangePercent: number
  marketValue: number
  bidPrice: number
  askPrice: number
  dividendPayoutRatio: number
  playerOwnedShares: number
  controlledCompanyOwnedShares: number
  combinedControlledOwnershipRatio: number
  canClaimControl: boolean
  canMerge: boolean
}

export interface StockExchangePriceHistoryPoint {
  companyId: string
  tick: number
  price: number
  recordedAtUtc: string
}

export interface ShareTradeResult {
  companyId: string
  companyName: string
  accountType: AccountContextType
  accountCompanyId: string | null
  accountName: string
  shareCount: number
  pricePerShare: number
  totalValue: number
  /** Amount reserved for taxes (only set on personal-account sells). */
  taxReserved: number
  ownedShareCount: number
  publicFloatShares: number
  personalCash: number
  /** Personal tax reserve after this trade. */
  personalTaxReserve: number
  companyCash: number | null
}

/** Matches backend PlayerRanking response */
export interface PlayerRanking {
  playerId: string
  displayName: string
  personalAccountName?: string
  totalWealth: number
  /** Total wealth normalized to USD for cross-currency leaderboard comparison */
  totalWealthUsd: number
  personalCash: number
  sharesValue: number
  companyCount: number
  badgeTypes?: string[]
}

/** Matches backend CompanyRanking response */
export interface CompanyRanking {
  companyId: string
  companyName: string
  playerId: string
  ownerDisplayName: string
  ownerPersonalAccountName?: string
  totalWealth: number
  /** Total wealth normalized to USD for cross-currency leaderboard comparison */
  totalWealthUsd: number
  /** ISO 4217 currency code of the company's home currency (e.g. "EUR", "CZK", "USD") */
  currencyCode: string
  cash: number
  buildingValue: number
  inventoryValue: number
  buildingCount: number
}

/** Matches backend GameState entity */
export interface GameState {
  currentTick: number
  lastTickAtUtc: string
  tickIntervalSeconds: number
  taxCycleTicks: number
  taxRate: number
  currentGameYear: number
  currentGameTimeUtc: string
  ticksPerDay: number
  ticksPerYear: number
  nextTaxTick: number
  nextTaxGameTimeUtc: string
  nextTaxGameYear: number
  /** Current game-year quarter index: 0=Q1, 1=Q2, 2=Q3, 3=Q4 */
  currentQuarter: number
  /** Human-readable quarter label e.g. "Q1" or "Q4" */
  currentQuarterLabel: string
  gameEnded?: boolean
  winnerPlayerId?: string | null
  winnerDisplayName?: string | null
  winnerCompanyName?: string | null
  gameEndedAtUtc?: string | null
}

export interface RealWorldWealth {
  id: string
  rank: number
  name: string
  wealthUsd: number
}

export interface EndgameStatus {
  gameEnded: boolean
  winnerPlayerId: string | null
  winnerDisplayName: string | null
  winnerCompanyName: string | null
  gameEndedAtUtc: string | null
  winningThresholdUsd: number
  topRealWorldRichest: RealWorldWealth[]
}

/** Matches backend ScheduledActionSummary — a pending player action waiting for tick resolution. */
export interface ScheduledActionSummary {
  id: string
  actionType: string
  buildingId: string
  buildingName: string
  buildingType: string
  submittedAtUtc: string
  submittedAtTick: number
  appliesAtTick: number
  ticksRemaining: number
  totalTicksRequired: number
}

/**
 * First-sale mission status returned by the firstSaleMission query.
 * Tracks the post-onboarding mission from shop configuration through to the first real public sale.
 */
export interface FirstSaleMission {
  /**
   * Current phase of the first-sale mission.
   * Values: NO_SHOP | CONFIGURE_SHOP | AWAITING_FIRST_SALE | FIRST_SALE_RECORDED | ALREADY_COMPLETED
   */
  phase: 'NO_SHOP' | 'CONFIGURE_SHOP' | 'AWAITING_FIRST_SALE' | 'FIRST_SALE_RECORDED' | 'ALREADY_COMPLETED'
  /** The onboarding sales shop building ID being tracked (null when phase is NO_SHOP). */
  shopBuildingId: string | null
  /** Display name of the onboarding sales shop. */
  shopName: string | null
  /**
   * Blocker codes explaining why the shop is not yet ready.
   * Values: BUILDING_UNDER_CONSTRUCTION | PUBLIC_SALES_UNIT_MISSING | PRICE_NOT_SET | NO_INVENTORY
   */
  blockers: string[]
  /** Revenue from the first recorded sale. */
  firstSaleRevenue: number | null
  /** Name of the product sold in the first sale. */
  firstSaleProductName: string | null
  /** Game tick at which the first sale occurred. */
  firstSaleTick: number | null
  /** Quantity sold in the first sale. */
  firstSaleQuantity: number | null
  /** Price per unit in the first sale. */
  firstSalePricePerUnit: number | null
}

// ── Inter-city trade routes ──────────────────────────────────────────────────

export type TradeRouteStatus = 'SCHEDULED' | 'IN_TRANSIT' | 'DELIVERED' | 'FAILED'

export interface TradeRouteResult {
  id: string
  companyId: string
  sourceBuildingId: string
  sourceBuildingName: string
  sourceCityName: string
  sourceCurrencyCode: string
  destinationBuildingId: string
  destinationBuildingName: string
  destinationCityName: string
  destinationCurrencyCode: string
  productTypeId: string | null
  productTypeName: string | null
  resourceTypeId: string | null
  resourceTypeName: string | null
  quantity: number
  quality: number
  pricePerUnit: number
  scheduledDepartureTick: number
  expectedArrivalTick: number
  transitTicks: number
  shippingCostEstimate: number
  shippingCostActual: number
  status: TradeRouteStatus
  failureReason: string | null
  createdAtUtc: string
  departedAtUtc: string | null
  completedAtUtc: string | null
}

export interface TradeRouteEstimate {
  distanceKm: number
  transitTicks: number
  shippingCostPerUnit: number
  totalShippingCost: number
}

export interface CreateTradeRouteInput {
  companyId: string
  sourceBuildingId: string
  sourceBuildingUnitId: string
  destinationBuildingId: string
  destinationBuildingUnitId: string
  productTypeId?: string | null
  resourceTypeId?: string | null
  quantity: number
  pricePerUnit: number
}

export interface CreateTradeRoutePayload {
  isSuccess: boolean
  errorCode: string | null
  errorMessage: string | null
  route: TradeRouteResult | null
}

// ─── Tutorial ────────────────────────────────────────────────────────────────

export interface TutorialMilestoneStatus {
  milestone: string
  isCompleted: boolean
  completedAtUtc: string | null
  bountyAwarded: boolean
  bountyAwardedAtUtc: string | null
  bountyPoints?: number | null
}

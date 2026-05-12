/**
 * Shared GraphQL API mock for Capitalism 5 Playwright tests.
 *
 * Usage:
 *   import { setupMockApi, makePlayer } from './helpers/mock-api'
 *   test('my test', async ({ page }) => {
 *     const state = setupMockApi(page)
 *     await page.goto('/')
 *   })
 */

import type { Page } from '@playwright/test'

export const GOVERNMENT_PLAYER_EMAIL = 'government@capitalism.game'
const MOCK_BUILDING_BASE_VALUE = 75_000

export type MockPlayer = {
  id: string
  email: string
  password: string
  displayName: string
  personalAccountName?: string
  role: 'PLAYER' | 'ADMIN'
  isInvisibleInChat: boolean
  createdAtUtc: string
  lastLoginAtUtc: string | null
  personalCash: number
  personalTaxReserve: number
  activeAccountType: 'PERSON' | 'COMPANY'
  activeCompanyId: string | null
  onboardingCompletedAtUtc: string | null
  onboardingCurrentStep: string | null
  onboardingIndustry: string | null
  onboardingCityId: string | null
  onboardingCompanyId: string | null
  onboardingFactoryLotId: string | null
  onboardingShopBuildingId: string | null
  onboardingFirstSaleCompletedAtUtc: string | null
  appliedReferralCode: string | null
  proSubscriptionEndsAtUtc: string | null
  interestPayments: MockPersonalInterestPayment[]
  dividendPayments: MockDividendPayment[]
  stockTrades: MockPersonTradeRecord[]
  companies: MockCompany[]
}

export type MockPersonalInterestPayment = {
  id: string
  companyId: string
  companyName: string
  bankBuildingId: string | null
  bankBuildingName: string | null
  amount: number
  recordedAtTick: number
  recordedAtUtc: string
  currencyCode: string
  description: string
}

export type MockDividendPayment = {
  id: string
  companyId: string
  companyName: string
  shareCount: number
  amountPerShare: number
  totalAmount: number
  gameYear: number
  recordedAtTick: number
  recordedAtUtc: string
  description: string
}

export type MockPersonTradeRecord = {
  id: string
  companyId: string
  companyName: string
  direction: 'BUY' | 'SELL'
  shareCount: number
  pricePerShare: number
  totalValue: number
  recordedAtTick: number
  recordedAtUtc: string
}
export type MockLedgerSummary = {
  companyId: string
  companyName: string
  gameYear?: number
  isCurrentGameYear?: boolean
  currentCash: number
  primaryCurrencyCode?: string
  primaryCurrencySymbol?: string
  hasMixedCurrencies?: boolean
  totalRevenue: number
  totalMediaHouseIncome?: number
  totalPurchasingCosts: number
  totalShippingCosts?: number
  totalLaborCosts: number
  totalEnergyCosts: number
  totalMarketingCosts: number
  totalTaxPaid: number
  totalOtherCosts: number
  taxableIncome?: number
  estimatedIncomeTax?: number
  netIncome: number
  propertyValue: number
  propertyAppreciation: number
  buildingValue: number
  inventoryValue: number
  totalAssets: number
  totalPropertyPurchases: number
  totalStockPurchaseCashOut?: number
  totalStockSaleCashIn?: number
  cashFromOperations: number
  cashFromInvestments: number
  firstRecordedTick: number
  lastRecordedTick: number
  incomeTaxDueAtTick?: number
  incomeTaxDueGameTimeUtc?: string
  incomeTaxDueGameYear?: number
  isIncomeTaxSettled?: boolean
  history?: MockLedgerHistoryYear[]
  buildingSummaries: Array<{
    buildingId: string
    buildingName: string
    buildingType: string
    revenue: number
    costs: number
    currencyCode?: string
    currencySymbol?: string
  }>
}

export type MockLedgerHistoryYear = {
  gameYear: number
  isCurrentGameYear: boolean
  totalRevenue: number
  totalLaborCosts: number
  totalEnergyCosts: number
  netIncome: number
  totalTaxPaid: number
  taxableIncome: number
  estimatedIncomeTax: number
  firstRecordedTick: number
  lastRecordedTick: number
}

export type MockLedgerEntry = {
  id: string
  category: string
  description: string
  amount: number
  recordedAtTick: number
  buildingId: string | null
  buildingName: string | null
  buildingUnitId: string | null
  productTypeId: string | null
  productName: string | null
  resourceTypeId: string | null
  resourceName: string | null
  currencyCode?: string
  currencySymbol?: string
  eventTag?: string | null
  eventDescription?: string | null
}

export type MockCompany = {
  id: string
  playerId: string
  name: string
  cash: number
  totalSharesIssued?: number
  dividendPayoutRatio?: number
  foundedAtUtc: string
  foundedAtTick?: number
  citySalaryMultipliers?: Record<string, number>
  buildings: MockBuilding[]
}

export type MockShareholding = {
  companyId: string
  ownerPlayerId: string | null
  ownerCompanyId: string | null
  shareCount: number
}

export type MockStockPriceHistoryPoint = {
  companyId: string
  tick: number
  price: number
  recordedAtUtc: string
}

export type MockLimitOrder = {
  id: string
  companyId: string
  stockSymbol: string
  side: 'BUY' | 'SELL'
  limitPrice: number
  quantity: number
  filledQuantity: number
  status: 'OPEN' | 'PARTIALLY_FILLED' | 'FILLED' | 'CANCELLED'
  ownerPlayerId: string | null
  ownerCompanyId: string | null
  createdAtTick: number
  updatedAtTick: number
}

export type MockLimitOrderExecution = {
  id: string
  companyId: string
  stockSymbol: string
  price: number
  quantity: number
  executedAtTick: number
  executedAtUtc: string
}

export type MockDividendProposal = {
  id: string
  companyId: string
  stockSymbol: string
  proposedByAccountId: string
  proposedByAccountType: 'PERSON' | 'COMPANY'
  dividendPerShare: number
  totalPayout: number
  status: string
  outcome: string
  proposedAtTick: number
  votingOpenTick: number
  votingCloseTick: number
  settledAtTick: number | null
}

export type MockDividendVote = {
  id: string
  proposalId: string
  voterAccountId: string
  voterAccountType: 'PERSON' | 'COMPANY'
  sharesVoted: number
  voteChoice: 'FOR' | 'AGAINST'
  castAtTick: number
}

export type MockBuilding = {
  id: string
  companyId: string
  cityId: string
  type: string
  name: string
  latitude: number
  longitude: number
  level: number
  powerConsumption: number
  isForSale: boolean
  askingPrice?: number | null
  listedAtUtc?: string | null
  pricePerSqm?: number | null
  occupancyPercent?: number | null
  totalAreaSqm?: number | null
  pendingPricePerSqm?: number | null
  pendingPriceActivationTick?: number | null
  powerPlantType?: string | null
  powerOutput?: number | null
  /** Power supply status: POWERED | CONSTRAINED | OFFLINE */
  powerStatus?: string
  mediaType?: string | null
  interestRate?: number | null
  builtAtUtc?: string
  /** True while the building is still under construction */
  isUnderConstruction?: boolean
  /** Tick number when construction finishes; null if not under construction */
  constructionCompletesAtTick?: number | null
  /** Cash cost charged for construction */
  constructionCost?: number
  /** Accumulated content value for MEDIA_HOUSE buildings */
  contentValue?: number
  /** Per-tick content spending budget for MEDIA_HOUSE buildings */
  contentBudgetPerTick?: number | null
  /** True when the building is a government-seeded media outlet (not player-owned) */
  isGovernmentOwned?: boolean
  /** True when at least one campaign unit is actively running in this media house. */
  isAdvertisingActive?: boolean
  marketValuation?: {
    landValue: number
    structureValue: number
    unitsValue: number
    totalValue: number
    minimumSalePrice: number
    currencyCode: string
  } | null
  /** True when the building is suspended due to insufficient bank account funds */
  isSuspendedForFunds?: boolean
  /** Machine-readable suspension reason: null | 'MISSING_BANK_ACCOUNT' | 'INSUFFICIENT_FUNDS:<amount>' */
  suspendedReason?: string | null
  /** Dispatch target percentage for POWER_PLANT buildings (0–100). Default 100. */
  dispatchTargetPercent?: number
  /** Current fuel reserve for thermal plants (MWh). */
  fuelReserveMwh?: number
  /** City base average rent per m² (APARTMENT/COMMERCIAL only) */
  cityReferenceRentPerSqm?: number | null
  /** Location-adjusted market rent per m² = city rate × PopulationIndex (APARTMENT/COMMERCIAL only) */
  adjustedMarketRentPerSqm?: number | null
  /** Lot PopulationIndex (APARTMENT/COMMERCIAL only) */
  populationIndex?: number | null
  /** Remaining mine deposit quantity (MINE only). Null for non-mine buildings. */
  lotMaterialQuantity?: number | null
  /** Original mine deposit quantity when first seeded (MINE only). Null for non-mine buildings. */
  lotOriginalMaterialQuantity?: number | null
  /** Mine deposit resource type ID (MINE only). Null for non-mine buildings. */
  lotResourceTypeId?: string | null
  /** Mine deposit material quality 0..1 (MINE only). Null for non-mine buildings. */
  lotMaterialQuality?: number | null
  /** UTC timestamp when building was destroyed (by loan default); null for active buildings. */
  destroyedAtUtc?: string | null
  /** True when this building is locked as loan collateral. */
  isCollateralized?: boolean
  /** Foreclosure countdown in ticks for defaulted collateral buildings. */
  foreclosureTicksRemaining?: number | null
  units: MockBuildingUnit[]
  pendingConfiguration: MockBuildingConfigurationPlan | null
}

export type MockUnitInventoryItem = {
  id?: string
  resourceTypeId?: string | null
  productTypeId?: string | null
  quantity: number
  quality?: number | null
  sourcingCostTotal?: number | null
}

export type MockUnitResourceHistoryPoint = {
  buildingUnitId?: string
  resourceTypeId?: string | null
  productTypeId?: string | null
  tick: number
  inflowQuantity?: number
  outflowQuantity?: number
  consumedQuantity?: number
  producedQuantity?: number
}

export type MockBuildingUnit = {
  id: string
  buildingId: string
  unitType: string
  gridX: number
  gridY: number
  level: number
  linkUp: boolean
  linkDown: boolean
  linkLeft: boolean
  linkRight: boolean
  linkUpLeft: boolean
  linkUpRight: boolean
  linkDownLeft: boolean
  linkDownRight: boolean
  resourceTypeId?: string | null
  productTypeId?: string | null
  minPrice?: number | null
  maxPrice?: number | null
  purchaseSource?: string | null
  saleVisibility?: string | null
  budget?: number | null
  mediaHouseBuildingId?: string | null
  minQuality?: number | null
  brandScope?: string | null
  vendorLockCompanyId?: string | null
  lockedCityId?: string | null
  industryCategory?: string | null
  inventoryQuantity?: number | null
  inventoryQuality?: number | null
  inventorySourcingCostTotal?: number | null
  inventoryItems?: MockUnitInventoryItem[]
  resourceHistory?: MockUnitResourceHistoryPoint[]
}

export type MockBuildingConfigurationPlanUnit = MockBuildingUnit & {
  startedAtTick: number
  appliesAtTick: number
  ticksRequired: number
  isChanged: boolean
  isReverting: boolean
}

export type MockBuildingConfigurationPlanRemoval = {
  id: string
  gridX: number
  gridY: number
  startedAtTick: number
  appliesAtTick: number
  ticksRequired: number
  isReverting: boolean
}

export type MockBuildingConfigurationPlan = {
  id: string
  buildingId: string
  submittedAtUtc: string
  submittedAtTick: number
  appliesAtTick: number
  totalTicksRequired: number
  blockReason?: string | null
  units: MockBuildingConfigurationPlanUnit[]
  removals: MockBuildingConfigurationPlanRemoval[]
}

export type MockCity = {
  id: string
  name: string
  countryCode: string
  currencyCode?: string
  latitude: number
  longitude: number
  population: number
  averageRentPerSqm: number
  baseSalaryPerManhour: number
  resources: { resourceType: { id: string; name: string; slug: string; category: string }; abundance: number }[]
}

export type MockFxRate = {
  baseCurrencyCode: string
  quoteCurrencyCode: string
  rate: number
  rateDate: string
  source: 'NBS' | 'FALLBACK'
  quoteCurrencySymbol: string
}

export type MockGoldAmmPosition = {
  id: string
  poolId: string
  currencyCode: string
  liquidityShares: number
  sharePercent: number
  claimableFiat: number
  claimableGold: number
  fiatProvided: number
  goldProvided: number
}

export type MockGoldAmmPool = {
  id: string
  currencyCode: string
  currencySymbol: string
  fiatReserve: number
  goldReserve: number
  totalLiquidityShares: number
  impliedGoldPrice: number
  myPosition: MockGoldAmmPosition | null
}

export type MockGoldAmmTradeRecord = {
  id: string
  playerId: string
  poolId: string
  direction: string
  currencyCode: string
  inputAmount: number
  outputAmount: number
  feeAmount: number
  impliedPrice: number
  executedAtTick: number
  executedAtUtc: string
}

export type MockGoldBalance = {
  balance: number
  blockedInPools: number
  availableBalance: number
}

export type MockBuildingLot = {
  id: string
  cityId: string
  name: string
  description: string
  district: string
  latitude: number
  longitude: number
  populationIndex: number
  basePrice: number
  price: number
  suitableTypes: string
  ownerCompanyId: string | null
  buildingId: string | null
  ownerCompany: { id: string; name: string } | null
  building: {
    id: string
    name: string
    type: string
    isUnderConstruction?: boolean
    constructionCompletesAtTick?: number | null
    constructionCost?: number
    isForSale?: boolean
    askingPrice?: number | null
  } | null
  resourceType: { id: string; name: string; slug: string } | null
  materialQuality: number | null
  materialQuantity: number | null
  originalMaterialQuantity?: number | null
}

export type MockResourceType = {
  id: string
  name: string
  slug: string
  category: string
  basePrice: number
  weightPerUnit: number
  unitName: string
  unitSymbol: string
  imageUrl?: string | null
  description: string | null
}

export type MockProductType = {
  id: string
  name: string
  slug: string
  industry: string
  basePrice: number
  baseCraftTicks: number
  outputQuantity?: number
  energyConsumptionMwh?: number
  basicLaborHours?: number
  unitName?: string
  unitSymbol?: string
  imageUrl?: string | null
  isProOnly: boolean
  isUnlockedForCurrentPlayer?: boolean
  description: string | null
  recipes: {
    resourceType?: { id: string; name: string; slug?: string; unitName?: string; unitSymbol?: string } | null
    inputProductType?: { id: string; name: string; slug: string; unitName?: string; unitSymbol?: string } | null
    quantity: number
  }[]
}

export type MockProductExchangeListing = {
  orderId: string
  productTypeId: string
  productName: string
  productSlug: string
  productIndustry: string
  unitSymbol: string
  unitName: string
  basePrice: number
  pricePerUnit: number
  remainingQuantity: number
  sellerCityId: string
  sellerCityName: string
  sellerCompanyId: string
  sellerCompanyName: string
  createdAtUtc: string
}

export type MockChatMessage = {
  id: string
  playerId: string
  message: string
  sentAtUtc: string
}

export type MockResearchBrandState = {
  id: string
  companyId: string
  name: string
  scope: 'PRODUCT' | 'CATEGORY' | 'COMPANY'
  productTypeId: string | null
  productName: string | null
  industryCategory: string | null
  awareness: number
  quality: number
  /** ≥ 1.0: driven by BRAND_QUALITY R&D. >1.0 = marketing budget is more effective. */
  marketingEfficiencyMultiplier: number
  /** Accumulated R&D research budget for this product (game currency). Null if none. */
  accumulatedResearchBudget?: number | null
  /** Budget for 100% quality when uncontested. Null if not a product brand. */
  baseResearchBudget?: number | null
  /** Highest competitor budget for this product globally. Null if no research exists. */
  maxCompetitorBudget?: number | null
}

export type MockPublicSalesRecord = {
  id: string
  buildingId: string
  companyId: string
  productTypeName: string | null
  tick: number
  quantitySold: number
  pricePerUnit: number
  revenue: number
}

export type MockPublicSalesAnalytics = {
  buildingUnitId: string
  buildingId: string
  buildingName: string
  cityName: string
  productTypeId?: string | null
  productName?: string | null
  totalRevenue: number
  totalQuantitySold: number
  averagePricePerUnit: number
  currentSalesCapacity: number
  dataFromTick: number
  dataToTick: number
  demandSignal: string
  actionHint: string
  recentUtilization: number
  trendDirection?: string
  revenueHistory: Array<{ tick: number; revenue: number; quantitySold: number }>
  priceHistory: Array<{ tick: number; pricePerUnit: number }>
  marketShare: Array<{ label: string; companyId: string | null; share: number; isUnmet: boolean }>
  elasticityIndex: number | null
  unmetDemandShare: number | null
  populationIndex: number | null
  inventoryQuality: number | null
  brandAwareness: number | null
  totalProfit: number | null
  profitHistory: Array<{ tick: number; profit: number; grossMarginPct: number | null }> | null
  demandDrivers: Array<{ factor: string; impact: string; score: number; description: string }>
  trendFactor?: number | null
  /** ISO 4217 currency code for the city where this unit is located (e.g. "EUR", "CZK"). */
  cityCurrencyCode?: string
  /** City-average reference price for the product in the city's local currency. Null when no product configured. */
  cityAveragePrice?: number | null
  /** City-wide market clearing price (weighted avg across all sellers, last 100 ticks). Null when no city-wide data. */
  cityMarketClearingPrice?: number | null
  /** Seasonal demand outlook. Null when no DemandSeasonality data for the product. */
  seasonalOutlook?: MockSeasonalOutlook | null
}

export type MockSeasonalOutlook = {
  currentQuarterIndex: number
  currentQuarterLabel: string
  currentMultiplier: number
  demandLevel: string
  callout: string
  quarterForecasts: Array<{
    quarterIndex: number
    label: string
    multiplier: number
    isCurrent: boolean
    colorCode: string
  }>
}

export type MockMarketIntelligenceSeller = {
  rank: number
  companyId: string
  displayName: string
  askingPricePerUnit: number
  brandQuality: number | null
  estimatedWeeklySalesVolume: number
  marketShare: number
}

export type MockMarketIntelligenceProduct = {
  productTypeId: string
  productName: string
  productSlug: string
  totalWeeklySalesVolume: number
  sellers: MockMarketIntelligenceSeller[]
}

export type MockMarketIntelligenceResult = {
  cityId: string
  cityName: string
  dataFromTick: number
  dataToTick: number
  products: MockMarketIntelligenceProduct[]
}

export type MockUnitProductAnalytics = {
  buildingUnitId: string
  unitType: string
  productTypeId?: string | null
  productName?: string | null
  dataFromTick: number
  dataToTick: number
  totalCost: number
  totalQuantityProduced: number
  estimatedRevenue: number | null
  estimatedProfit: number | null
  /** ISO 4217 currency code for the city where this unit is located (e.g. "EUR", "CZK"). */
  cityCurrencyCode?: string
  snapshots: Array<{
    tick: number
    laborCost: number
    energyCost: number
    totalCost: number
    quantityProduced: number
    estimatedRevenue: number | null
    estimatedProfit: number | null
  }>
}

export type MockBuildingFinancialTimeline = {
  buildingId: string
  buildingName: string
  dataFromTick: number
  dataToTick: number
  totalSales: number
  totalCosts: number
  totalProfit: number
  timeline: Array<{ tick: number; sales: number; costs: number; profit: number }>
}

export type MockLoanOffer = {
  id: string
  bankBuildingId: string
  bankBuildingName: string
  cityId: string
  cityName: string
  lenderCompanyId: string
  lenderCompanyName: string
  annualInterestRatePercent: number
  maxPrincipalPerLoan: number
  totalCapacity: number
  usedCapacity: number
  remainingCapacity: number
  durationTicks: number
  isActive: boolean
  createdAtTick: number
  createdAtUtc: string
}

export type MockLoan = {
  id: string
  loanOfferId: string
  borrowerCompanyId: string
  borrowerCompanyName: string
  lenderCompanyId: string
  lenderCompanyName: string
  bankBuildingId: string
  bankBuildingName: string
  loanCurrencyCode?: string
  originalPrincipal: number
  remainingPrincipal: number
  annualInterestRatePercent: number
  durationTicks: number
  startTick: number
  dueTick: number
  nextPaymentTick: number
  paymentAmount: number
  paymentsMade: number
  totalPayments: number
  status: 'ACTIVE' | 'OVERDUE' | 'DEFAULTED' | 'REPAID'
  missedPayments: number
  accumulatedPenalty: number
  defaultedAtTick?: number | null
  acceptedAtUtc: string
  closedAtUtc: string | null
  collateralBuildingId?: string | null
  collateralBuildingName?: string | null
  collateralAppraisedValue?: number | null
  collateralListingPrice?: number | null
  collateralListingCurrencyCode?: string | null
}

export type MockCollateralBuilding = {
  buildingId: string
  buildingName: string
  buildingType: string
  level: number
  appraisedValue: number
  maxBorrowable: number
  existingSecuredExposure: number
  remainingBorrowingCapacity: number
  currencyCode?: string
  isEligible: boolean
  ineligibilityReason: string | null
}

export type MockBankDeposit = {
  id: string
  bankBuildingId: string
  bankBuildingName: string
  depositorCompanyId: string
  depositorCompanyName: string
  amount: number
  depositInterestRatePercent: number
  isBaseCapital: boolean
  isActive: boolean
  depositedAtTick: number
  depositedAtUtc: string
  totalInterestPaid: number
}

export type MockBankInfo = {
  bankBuildingId: string
  bankBuildingName: string
  cityId: string
  cityName: string
  lenderCompanyId: string
  lenderCompanyName: string
  depositInterestRatePercent: number
  lendingInterestRatePercent: number
  totalDeposits: number
  lendableCapacity: number
  outstandingLoanPrincipal: number
  availableLendingCapacity: number
  baseCapitalDeposited: boolean
  // Liquidity / central-bank fields
  centralBankDebt: number
  centralBankInterestRatePercent: number
  reserveRequirement: number
  availableCash: number
  reserveShortfall: number
  liquidityStatus: 'HEALTHY' | 'PRESSURED' | 'CRITICAL'
  // Currency fields
  cityCurrencyCode?: string
  cityCurrencySymbol?: string
  baseCapitalRequirement?: number
}

export type MockGameNewsLocalization = {
  locale: string
  title: string
  summary: string
  htmlContent: string
}

export type MockGameNewsEntry = {
  id: string
  entryType: 'NEWS' | 'CHANGELOG'
  status: 'DRAFT' | 'PUBLISHED'
  targetServerKey: string | null
  createdByEmail: string
  updatedByEmail: string
  createdAtUtc: string
  updatedAtUtc: string
  publishedAtUtc: string | null
  localizations: MockGameNewsLocalization[]
  readByPlayerIds: string[]
}

export type MockGlobalGameAdminGrant = {
  id: string
  email: string
  grantedByEmail: string
  grantedAtUtc: string
  updatedAtUtc: string
}

export type MockGameAdminMoneyInflowSummary = {
  category: string
  amount: number
  description: string
}

export type MockGameAdminShippingCostSummary = {
  companyId: string
  companyName: string
  amount: number
  entryCount: number
}

export type MockGameAdminMultiAccountAlert = {
  reason: string
  exposureAmount: number
  confidenceScore: number
  supportingEntityType: string
  supportingEntityName: string
  primaryPlayerId: string
  relatedPlayerId: string
}

export type MockGameAdminAuditLog = {
  id: string
  adminActorPlayerId: string
  adminActorEmail: string
  adminActorDisplayName: string
  effectivePlayerId: string
  effectivePlayerEmail: string
  effectivePlayerDisplayName: string
  effectiveAccountType: 'PERSON' | 'COMPANY'
  effectiveCompanyId: string | null
  effectiveCompanyName: string | null
  graphQlOperationName: string | null
  mutationSummary: string
  responseStatusCode: number
  recordedAtUtc: string
}

export type MockImpersonationSession = {
  adminActorUserId: string
  effectiveUserId: string
  effectiveAccountType: 'PERSON' | 'COMPANY'
  effectiveCompanyId: string | null
}

export type MockBuildingLayoutTemplate = {
  id: string
  ownerPlayerId: string
  name: string
  description: string | null
  buildingType: string
  unitsJson: string
  updatedAtUtc: string
}

export type MockWeatherTick = {
  tick: number
  windPercent: number
  solarPercent: number
}

export type MockCityWeatherForecast = {
  cityId: string
  currentWindPercent: number
  currentSolarPercent: number
  forecast: MockWeatherTick[]
}

export type MockCityEconomicReport = {
  id: string
  cityId: string
  taxCycleEnd: number
  totalSalaries: number
  totalPublicRevenue: number
  activeCompanies: number
  totalPowerConsumption: number
  totalPowerSupply: number
  averageProductQuality: number
  economicIndex: number
  computedAtUtc: string
}

export type MockCityMediaHouseInfo = {
  id: string
  name: string
  cityId: string
  cityName: string
  mediaType: string | null
  ownerCompanyId: string
  ownerCompanyName: string
  effectivenessMultiplier: number
  powerStatus: string
  isUnderConstruction: boolean
  contentRanking: number
  contentValue: number
  contentBudgetPerTick: number | null
  isGovernmentOwned: boolean
}

export type MockEconomicCycle = {
  id: string
  phase: 'EXPANSION' | 'PEAK' | 'RECESSION' | 'TROUGH'
  phaseStartedTick: number
  expectedDurationTicks: number
  intensityFactor: number
  phaseEndTick: number
  ticksRemaining: number
}

export type MockActiveMarketEvent = {
  id: string
  eventType: 'COMMODITY_SHOCK' | 'INTEREST_RATE_CHANGE' | 'SEASONAL_DEMAND_SURGE'
  title: string
  description: string
  magnitudeMultiplier: number
  startsAtTick: number
  expiresAtTick: number
  ticksRemaining: number
  affectedResourceTypeId: string | null
  affectedResourceName: string | null
  affectedResourceSlug: string | null
  affectedCityId: string | null
  affectedCityName: string | null
}

export type MockEconomicHistoryPoint = {
  tick: number
  phase: 'EXPANSION' | 'PEAK' | 'RECESSION' | 'TROUGH'
  intensityFactor: number
}

export type MockState = {
  serverKey: string
  players: MockPlayer[]
  shareholdings: MockShareholding[]
  cities: MockCity[]
  buildingLots: MockBuildingLot[]
  resourceTypes: MockResourceType[]
  productTypes: MockProductType[]
  currentUserId: string | null
  currentToken: string | null
  gameState: { currentTick: number; lastTickAtUtc: string; tickIntervalSeconds: number; taxCycleTicks: number; taxRate: number }
  economicCycle: MockEconomicCycle | null
  activeMarketEvents: MockActiveMarketEvent[]
  economicHistory: MockEconomicHistoryPoint[]
  endgameStatus: {
    gameEnded: boolean
    winnerPlayerId: string | null
    winnerDisplayName: string | null
    winnerCompanyName: string | null
    gameEndedAtUtc: string | null
    winningThresholdUsd: number
    topRealWorldRichest: Array<{ id: string; rank: number; name: string; wealthUsd: number }>
  }
  cityWeatherForecasts: Record<string, MockCityWeatherForecast>
  stockPriceHistory: Record<string, MockStockPriceHistoryPoint[]>
  stockLimitOrders: MockLimitOrder[]
  stockLimitOrderExecutions: MockLimitOrderExecution[]
  dividendProposals: MockDividendProposal[]
  dividendVotes: MockDividendVote[]
  ledgerData: Record<string, MockLedgerSummary>
  drillDownData: Record<string, MockLedgerEntry[]>
  /** Research brand states keyed by companyId for the companyBrands query. */
  researchBrands: Record<string, MockResearchBrandState[]>
  /** Public sales records for first-sale milestone detection. */
  publicSalesRecords: MockPublicSalesRecord[]
  /** Public sales analytics by unit ID */
  publicSalesAnalytics: Record<string, MockPublicSalesAnalytics>
  /** Unit product analytics by unit ID (for MANUFACTURING units) */
  unitProductAnalytics: Record<string, MockUnitProductAnalytics>
  /** Campaign analytics result keyed by companyId */
  campaignAnalytics: Record<string, object | null>
  /** Competitive market intelligence keyed by cityId. */
  marketIntelligenceByCity: Record<string, MockMarketIntelligenceResult | null>
  /** Building financial history keyed by building ID */
  buildingFinancialTimelines: Record<string, MockBuildingFinancialTimeline>
  /** Loan offers available in the marketplace */
  loanOffers: MockLoanOffer[]
  /** Active loans for the current player's companies */
  myLoans: MockLoan[]
  /** Collateral building eligibility summaries for the current player */
  collateralBuildings: MockCollateralBuilding[]
  /** Bank deposits (by depositor companies of the current player) */
  myDeposits: MockBankDeposit[]
  /** All banks visible in the marketplace */
  allBanks: MockBankInfo[]
  /** Mock procurement preview response keyed by unit ID. If null/missing, returns a default GLOBAL_EXCHANGE preview. */
  procurementPreviews: Record<string, object | null>
  /** Mock sourcing candidates response keyed by unit ID. If null/missing, returns default candidates. */
  sourcingCandidates: Record<string, object[] | null>
  /** Mock unit upgrade info response keyed by unit ID. If null/missing, returns a default upgradable Manufacturing/level-1 response. */
  unitUpgradeInfoOverrides: Record<string, object | null>
  /** If set to a unit ID, scheduleUnitUpgrade will return INSUFFICIENT_FUNDS for that unit. */
  upgradeInsufficientFundsUnitId: string | null
  /** If set to a unit ID, scheduleUnitUpgrade will return MAX_CONCURRENT_UPGRADES for that unit. */
  upgradeMaxConcurrentUnitId: string | null
  /** If set to a unit ID, scheduleUnitUpgrade will return UNIT_ALREADY_UPGRADING for that unit. */
  upgradeAlreadyUpgradingUnitId: string | null
  /** Active player SELL exchange orders for products (the globalExchangeProductListings query). */
  productExchangeListings: MockProductExchangeListing[]
  chatMessages: MockChatMessage[]
  rootAdminEmails: string[]
  globalGameAdminGrants: MockGlobalGameAdminGrant[]
  gameNewsEntries: MockGameNewsEntry[]
  adminMoneyInflowSummaries: MockGameAdminMoneyInflowSummary[]
  adminShippingCostSummaries: MockGameAdminShippingCostSummary[]
  adminMultiAccountAlerts: MockGameAdminMultiAccountAlert[]
  adminAuditLogs: MockGameAdminAuditLog[]
  impersonationSession: MockImpersonationSession | null
  buildingLayouts: MockBuildingLayoutTemplate[]
  /** When set, the next StoreBuildingConfiguration call returns this string as a CONTRADICTORY_LINK error. */
  forceBuildingConfigError: string | null
  /**
   * Per-unit last-tick movement overrides keyed by unit ID.
   * When set, the buildingUnitInventorySummaries response will include lastTickInflow and lastTickOutflow
   * from this map instead of the defaults (both null).
   */
  unitLastTickMovement: Record<string, { lastTickInflow: number; lastTickOutflow: number }>
  /** FX rates returned by the fxRates query. Keyed by quoteCurrencyCode. */
  fxRates: MockFxRate[]
  /** Historical FX rate snapshots returned by the fxRateHistory query. */
  fxRateHistorySnapshots: {
    baseCurrencyCode: string
    quoteCurrencyCode: string
    midRate: number
    buyRate: number
    sellRate: number
    gameTick: number
    capturedAtUtc: string
  }[]
  /** Player currency balances (non-EUR). Populated by executeForexSwap. */
  playerCurrencyBalances: { currencyCode: string; currencySymbol: string; balance: number }[]
  /** Forex trade history entries for the authenticated player. */
  forexTradeHistory: {
    id: string
    fromCurrencyCode: string
    toCurrencyCode: string
    fromAmount: number
    toAmount: number
    feeAmount: number
    rate: number
    executedAtTick: number
    executedAtUtc: string
    fromCurrencySymbol: string
    toCurrencySymbol: string
  }[]
  /** Bank statement rows returned by the bankStatement query (keyed by companyId). */
  bankStatementRows: Record<
    string,
    Array<{
      id: string
      recordedAtTick: number
      recordedAtUtc: string
      description: string
      category: string
      amount: number
      runningBalance: number
      buildingId: string | null
      buildingName: string | null
    }>
  >
  /** Personal bank statement rows returned when bankStatement is called with a personal accountId. */
  personalBankStatementRows: Array<{
    id: string
    recordedAtTick: number
    recordedAtUtc: string
    description: string
    category: string
    amount: number
    runningBalance: number
    buildingId: string | null
    buildingName: string | null
  }>
  /** Media houses returned by cityMediaHouses query, keyed by cityId. */
  cityMediaHouses: Record<string, MockCityMediaHouseInfo[]>
  /** City economic reports returned by getCityEconomicReport query, keyed by cityId. */
  cityEconomicReports?: Record<string, MockCityEconomicReport[]>
  /** Building bank account info keyed by buildingId. */
  buildingBankAccounts: Record<
    string,
    {
      hasBankAccount: boolean
      bankAccountId: string | null
      accountNumber: string | null
      balance: number | null
      alertMinBalanceThreshold?: number | null
      isSuspendedForFunds: boolean
      suspendedReason: string | null
      currencyCode: string
    }
  >
  /** Player notifications shown in the navbar bell panel. */
  playerNotifications: Array<{
    id: string
    type: string
    title: string
    message: string
    isRead: boolean
    createdAtTick: number
    createdAtUtc: string
    companyId?: string | null
    buildingId?: string | null
    buildingUnitId?: string | null
    bankAccountId?: string | null
    loanId?: string | null
  }>
  /** Player's company bank accounts returned by the myBankAccounts query. */
  myBankAccounts: Array<{
    id: string
    accountNumber: string
    currencyCode: string
    currencySymbol: string
    balance: number
    companyId: string | null
    companyName: string | null
    ownerType?: 'PERSON' | 'COMPANY'
    ownerDisplayName?: string
    bankBuildingId?: string | null
    cityId?: string | null
    isDepositAccount?: boolean
  }>
  /** Gold AMM pools for the Gold AMM exchange. */
  goldAmmPools: MockGoldAmmPool[]
  /** Player gold token balance for the Gold AMM exchange. */
  goldBalance: MockGoldBalance
  /** Recent gold AMM swap trade records returned by goldAmmSwapHistory. */
  goldAmmSwapHistory: MockGoldAmmTradeRecord[]
  /** City market reports returned by the cityMarketReports query. */
  marketReports: Array<{
    id: string
    cityId: string
    cityName: string
    reportType: 'WEEKLY' | 'MONTHLY'
    tickFrom: number
    tickTo: number
    totalRevenue: number
    totalQuantitySold: number
    uniqueProducts: number
    topProducts: Array<{
      productTypeId: string
      productName: string
      industry: string
      totalRevenue: number
      totalQuantitySold: number
      averagePricePerUnit: number
      basePrice: number
      grossMarginPct: number
      sellerCount: number
    }>
  }>
  /**
   * Supply chain diagram data keyed by buildingId.
   * When set, buildingSupplyChain query returns this data instead of auto-generated data.
   */
  supplyChainData: Record<
    string,
    {
      buildingId: string
      buildingName: string
      buildingType: string
      units: Array<{
        buildingUnitId: string
        unitType: string
        gridX: number
        gridY: number
        level: number
        status: string
        idleTicks: number
        fillPercent: number
        resourceTypeId: string | null
        productTypeId: string | null
        resourceOrProductName: string | null
        estimatedTransitCost: number | null
      }>
      links: Array<{
        fromUnitId: string
        toUnitId: string
        direction: string
        estimatedTransitCost: number
      }>
      healthScore: 'GREEN' | 'YELLOW' | 'RED'
      healthReason: string
      criticalUnitIds: string[]
      warningUnitIds: string[]
    }
  >
  /** Buildings listed for sale on the secondary market (for buildingMarket query). */
  buildingMarketListings: MockBuildingMarketListing[]
  /** My building listings with offers (for myBuildingListings query). */
  myBuildingListings: MockBuildingMarketMyListing[]
  /** Inter-city trade routes returned by myTradeRoutes query. */
  tradeRoutes: MockTradeRoute[]
  /** Prerequisites for launching an additional company (additionalCompanyPrerequisites query). */
  additionalCompanyPrerequisites: {
    allRequirementsMet: boolean
    companyCount: number
    underMaxCap: boolean
    hasExistingCompany: boolean
    companyAgeTicks: number
    companyAgeRequirementMet: boolean
    ticksUntilAgeRequirementMet: number
    netIncomeInWindow: number
    profitabilityRequirementMet: boolean
    personalBalanceUsd: number
    balanceRequirementMet: boolean
  } | null
  /** Tutorial milestone completion state for the current player. */
  tutorialProgress: Array<{
    milestone: string
    isCompleted: boolean
    completedAtUtc: string | null
    bountyAwarded: boolean
    bountyAwardedAtUtc: string | null
    bountyPoints?: number | null
  }>
  /** Achievement badges for a player (keyed by playerId). */
  playerBadges: Record<
    string,
    Array<{
      id: string
      badgeType: string
      rarity: string
      unlockCondition: string
      unlockedAtUtc: string
      unlockedAtTick: number
    }>
  >
  /** Rank snapshots for a player (keyed by playerId). */
  playerRankSnapshots: Record<
    string,
    Array<{
      snapshotTick: number
      snapshotUtc: string
      leaderboardRank: number
      wealthUsd: number
      percentileRank: number
      positionChange: number | null
    }>
  >
  /** Raw reset token => player email mapping for forgot/reset password endpoint mocks. */
  passwordResetTokens: Record<string, string>
  /** Mine extraction records returned by getMineExtractionHistory query. */
  mineExtractionRecords?: Array<{
    tick: number
    extractedAmount: number
    efficiencyPercent: number
    reserveRemaining: number
  }>
  /** Mine depletion forecast returned by getMineDepletionForecast query. */
  mineDepletionForecast?: {
    averageExtractionRatePerTick: number | null
    depletionTick: number | null
    critical5PctTick: number | null
    critical20PctTick: number | null
    estimatedGameDaysRemaining: number | null
    currentReserve: number | null
    originalReserve: number | null
  } | null
  mineExtractionIntelligence?: {
    currentTick: number
    burnRatePerTick: number | null
    burnRatePerDay: number | null
    expectedDepletionTick: number | null
    qualityDecayInflectionTick: number | null
    estimatedGameDaysRemaining: number | null
    currentReserve: number | null
    originalReserve: number | null
    dailyExtraction: Array<{
      dayIndex: number
      extractedAmount: number
      efficiencyPercent: number
      reserveRemaining: number
    }>
  } | null
  /** Mock marketOverview data returned by the marketOverview query. Keyed by cityId, or use '__all__' for all-city results. */
  marketOverviewByCityId: Record<string, MockMarketDemandSummary | null>
  /** Mock marketPriceHistory data, keyed by productTypeId. Returns an empty array when not set. */
  marketPriceHistoryByProductId: Record<string, MockMarketPriceHistoryPoint[]>
}

export interface MockMarketDemandSummary {
  cityId: string
  cityName: string
  currencyCode: string
  fromTick: number
  toTick: number
  products: Array<{
    productTypeId: string
    productName: string
    industry: string
    totalDemand: number
    totalQuantitySold: number
    satisfactionRate: number
    averageClearingPrice: number
    totalRevenue: number
    sellerCount: number
  }>
}

export interface MockMarketPriceHistoryPoint {
  tick: number
  clearingPrice: number
  totalVolume: number
  totalRevenue: number
  sellerCount: number
}

export interface MockBuildingMarketListing {
  pendingOfferCount: number
  building: {
    id: string
    name: string
    type: string
    isForSale: boolean
    askingPrice: number | null
    listedAtUtc: string | null
    level: number
    isCollateralized?: boolean
    foreclosureTicksRemaining?: number | null
    city: { id: string; name: string; currencyCode: string; countryCode: string }
    company: { id: string; name: string; player: { displayName: string } }
  }
}

export interface MockBuildingMarketOffer {
  id: string
  offerVersion: string
  offeredPrice: number
  status: 'PENDING' | 'ACCEPTED' | 'REJECTED'
  negotiationNote: string | null
  createdAtUtc: string
  resolvedAtUtc: string | null
  buyerPlayer: { displayName: string }
  buyerCompany: { id: string; name: string }
}

export interface MockBuildingMarketMyListing {
  building: {
    id: string
    name: string
    type: string
    isForSale: boolean
    askingPrice: number | null
    listedAtUtc: string | null
    level: number
    isCollateralized?: boolean
    foreclosureTicksRemaining?: number | null
    city: { id: string; name: string; currencyCode: string }
    company: { id: string; name: string }
  }
  offers: MockBuildingMarketOffer[]
}

export interface MockTradeRoute {
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
  status: 'SCHEDULED' | 'IN_TRANSIT' | 'DELIVERED' | 'FAILED'
  failureReason: string | null
  createdAtUtc: string
  departedAtUtc: string | null
  completedAtUtc: string | null
}

const mockStateByPage = new WeakMap<Page, MockState>()

const PERSONAL_STARTING_CASH = 200000
const STARTER_FOUNDER_CONTRIBUTION = 200000
const DEFAULT_IPO_RAISE_TARGET = 400000
const DEFAULT_COMPANY_SHARE_COUNT = 10000
const DEFAULT_DIVIDEND_PAYOUT_RATIO = 0.2
const GAME_START_YEAR = 2000
const TICKS_PER_DAY = 24
const TICKS_PER_YEAR = 24 * 365
const TICKS_PER_QUARTER = TICKS_PER_YEAR / 4

function normalizeMockBankAccount(
  account: {
    id: string
    accountNumber: string
    currencyCode: string
    currencySymbol: string
    balance: number
    companyId: string | null
    companyName: string | null
    ownerType?: 'PERSON' | 'COMPANY'
    ownerDisplayName?: string
    bankBuildingId?: string | null
    cityId?: string | null
    isDepositAccount?: boolean
  },
  defaultPersonalName: string,
) {
  const ownerType = account.ownerType ?? (account.companyId ? 'COMPANY' : 'PERSON')
  const ownerDisplayName = account.ownerDisplayName ?? account.companyName ?? defaultPersonalName
  // isDepositAccount: explicit flag if provided, otherwise infer from bankBuildingId being set
  const isDepositAccount = account.isDepositAccount ?? (account.bankBuildingId != null && account.bankBuildingId !== '')
  return {
    ...account,
    ownerType,
    ownerDisplayName,
    isDepositAccount,
  }
}

function resolveIpoSelection(raiseTarget?: number) {
  switch (raiseTarget ?? DEFAULT_IPO_RAISE_TARGET) {
    case 400000:
      return { raiseTarget: 400000, founderOwnershipRatio: 0.5 }
    case 600000:
      return { raiseTarget: 600000, founderOwnershipRatio: 0.3333 }
    case 800000:
      return { raiseTarget: 800000, founderOwnershipRatio: 0.25 }
    default:
      return { raiseTarget: DEFAULT_IPO_RAISE_TARGET, founderOwnershipRatio: 0.5 }
  }
}

function getCompanyTotalShares(company: MockCompany) {
  return company.totalSharesIssued ?? DEFAULT_COMPANY_SHARE_COUNT
}

function getCompanyDividendPayoutRatio(company: MockCompany) {
  return company.dividendPayoutRatio ?? DEFAULT_DIVIDEND_PAYOUT_RATIO
}

function getCompanyAssetBaseValue(company: MockCompany) {
  const baseValues: Record<string, number> = {
    MINE: 250000,
    FACTORY: 200000,
    SALES_SHOP: 150000,
    RESEARCH_DEVELOPMENT: 300000,
    APARTMENT: 400000,
    COMMERCIAL: 350000,
    MEDIA_HOUSE: 500000,
    BANK: 600000,
    EXCHANGE: 450000,
    POWER_PLANT: 350000,
  }

  return company.buildings.reduce((total, building) => total + (baseValues[building.type] ?? 0) * building.level, 0)
}

function computeMockSharePrice(company: MockCompany) {
  return Number(Math.max((company.cash + getCompanyAssetBaseValue(company)) / getCompanyTotalShares(company), 1).toFixed(2))
}

function stockSymbolForCompany(companyId: string): string {
  return `CMP-${companyId.replaceAll('-', '').toUpperCase()}`
}

function deriveMockPrimaryIndustry(state: MockState, company: MockCompany): string {
  const productIndustryById = new Map(state.productTypes.map((product) => [product.id, product.industry]))
  const industryUsage = new Map<string, number>()

  for (const building of company.buildings) {
    for (const unit of building.units ?? []) {
      if (unit.productTypeId) {
        const industry = productIndustryById.get(unit.productTypeId)
        if (industry) {
          industryUsage.set(industry, (industryUsage.get(industry) ?? 0) + 1)
        }
      }

      for (const inventoryItem of unit.inventoryItems ?? []) {
        if (inventoryItem.productTypeId) {
          const industry = productIndustryById.get(inventoryItem.productTypeId)
          if (industry) {
            industryUsage.set(industry, (industryUsage.get(industry) ?? 0) + 1)
          }
        }
      }
    }
  }

  const topIndustry = [...industryUsage.entries()].sort((left, right) => right[1] - left[1] || left[0].localeCompare(right[0]))[0]?.[0]

  if (topIndustry) {
    return topIndustry
  }

  const firstBuildingType = company.buildings[0]?.type
  if (firstBuildingType === 'FACTORY' || firstBuildingType === 'MINE' || firstBuildingType === 'SALES_SHOP') {
    return 'FURNITURE'
  }

  if (firstBuildingType === 'BANK' || firstBuildingType === 'EXCHANGE') {
    return 'FINANCE'
  }

  return 'DIVERSIFIED'
}

function isGovernmentCompany(state: MockState, company: MockCompany) {
  const owner = state.players.find((player) => player.id === company.playerId)
  return owner?.email === GOVERNMENT_PLAYER_EMAIL
}

function getPlayerControlledCompanyIds(state: MockState, playerId: string) {
  return new Set(
    state.players
      .flatMap((player) => player.companies)
      .filter((company) => company.playerId === playerId)
      .map((company) => company.id),
  )
}

function getPublicFloatShares(state: MockState, company: MockCompany) {
  const issued = getCompanyTotalShares(company)
  const allocated = state.shareholdings.filter((holding) => holding.companyId === company.id).reduce((total, holding) => total + holding.shareCount, 0)

  return Number(Math.max(issued - allocated, 0).toFixed(4))
}

function getOrCreateShareholding(state: MockState, companyId: string, ownerPlayerId: string | null, ownerCompanyId: string | null) {
  let holding = state.shareholdings.find((candidate) => candidate.companyId === companyId && candidate.ownerPlayerId === ownerPlayerId && candidate.ownerCompanyId === ownerCompanyId)

  if (!holding) {
    holding = { companyId, ownerPlayerId, ownerCompanyId, shareCount: 0 }
    state.shareholdings.push(holding)
  }

  return holding
}

function getCombinedControlledOwnershipRatio(state: MockState, playerId: string, company: MockCompany) {
  const controlledCompanyIds = getPlayerControlledCompanyIds(state, playerId)
  const controlledShares = state.shareholdings
    .filter((holding) => holding.companyId === company.id && (holding.ownerPlayerId === playerId || (holding.ownerCompanyId ? controlledCompanyIds.has(holding.ownerCompanyId) : false)))
    .reduce((total, holding) => total + holding.shareCount, 0)

  return Number((controlledShares / getCompanyTotalShares(company)).toFixed(4))
}

function appendMockStockPriceHistory(state: MockState, companyId: string, price: number) {
  const existing = state.stockPriceHistory[companyId] ?? []
  const point: MockStockPriceHistoryPoint = {
    companyId,
    tick: state.gameState.currentTick,
    price,
    recordedAtUtc: new Date().toISOString(),
  }
  state.stockPriceHistory[companyId] = [...existing.filter((candidate) => candidate.tick !== point.tick), point].sort((left, right) => left.tick - right.tick)
}

function ensureMockLedgerSummary(state: MockState, company: MockCompany): MockLedgerSummary {
  const existing = state.ledgerData[company.id]
  if (existing) {
    return existing
  }

  const baseValues: Record<string, number> = {
    MINE: 250000,
    FACTORY: 200000,
    SALES_SHOP: 150000,
    RESEARCH_DEVELOPMENT: 300000,
    APARTMENT: 400000,
    COMMERCIAL: 350000,
    MEDIA_HOUSE: 500000,
    BANK: 600000,
    EXCHANGE: 450000,
    POWER_PLANT: 350000,
  }
  const buildingValue = company.buildings.reduce((sum, building) => sum + (baseValues[building.type] ?? 0) * building.level, 0)
  const summary: MockLedgerSummary = {
    companyId: company.id,
    companyName: company.name,
    gameYear: computeMockGameYear(state.gameState.currentTick),
    isCurrentGameYear: true,
    currentCash: company.cash,
    totalRevenue: 0,
    totalPurchasingCosts: 0,
    totalLaborCosts: 0,
    totalEnergyCosts: 0,
    totalMarketingCosts: 0,
    totalTaxPaid: 0,
    totalOtherCosts: 0,
    taxableIncome: 0,
    estimatedIncomeTax: 0,
    netIncome: 0,
    propertyValue: 0,
    propertyAppreciation: 0,
    buildingValue,
    inventoryValue: 0,
    totalAssets: company.cash + buildingValue,
    totalPropertyPurchases: 0,
    totalStockPurchaseCashOut: 0,
    totalStockSaleCashIn: 0,
    cashFromOperations: 0,
    cashFromInvestments: 0,
    firstRecordedTick: 0,
    lastRecordedTick: 0,
    history: [],
    buildingSummaries: [],
  }
  state.ledgerData[company.id] = summary
  return summary
}

function recordMockCompanyStockLedgerEntry(state: MockState, company: MockCompany, category: 'STOCK_PURCHASE' | 'STOCK_SALE', description: string, amount: number) {
  const summary = ensureMockLedgerSummary(state, company)
  const currentTick = state.gameState.currentTick
  const drillKey = `${company.id}:${category}`
  const existing = state.drillDownData[drillKey] ?? []

  state.drillDownData[drillKey] = [
    {
      id: `${category.toLowerCase()}-${existing.length + 1}-${currentTick}`,
      category,
      description,
      amount,
      recordedAtTick: currentTick,
      buildingId: null,
      buildingName: null,
      buildingUnitId: null,
      productTypeId: null,
      productName: null,
      resourceTypeId: null,
      resourceName: null,
    },
    ...existing,
  ]

  if (category === 'STOCK_PURCHASE') {
    summary.totalStockPurchaseCashOut = Number((summary.totalStockPurchaseCashOut + Math.abs(amount)).toFixed(2))
  } else {
    summary.totalStockSaleCashIn = Number((summary.totalStockSaleCashIn + amount).toFixed(2))
  }

  summary.currentCash = company.cash
  summary.cashFromInvestments = Number((summary.totalStockSaleCashIn - summary.totalPropertyPurchases - summary.totalStockPurchaseCashOut).toFixed(2))
  summary.totalAssets = Number((company.cash + summary.propertyValue + summary.buildingValue + summary.inventoryValue).toFixed(2))
  summary.firstRecordedTick = summary.firstRecordedTick === 0 ? currentTick : Math.min(summary.firstRecordedTick, currentTick)
  summary.lastRecordedTick = Math.max(summary.lastRecordedTick, currentTick)
}

function computeMockGameYear(currentTick: number) {
  return GAME_START_YEAR + Math.floor(Math.max(currentTick, 0) / TICKS_PER_YEAR)
}

function computeMockInGameTimeUtc(currentTick: number) {
  const gameStart = new Date(Date.UTC(GAME_START_YEAR, 0, 1, 0, 0, 0))
  gameStart.setUTCHours(gameStart.getUTCHours() + Math.max(currentTick, 0))
  return gameStart.toISOString()
}

function computeMockNextTaxTick(currentTick: number, taxCycleTicks: number) {
  const cycleTicks = taxCycleTicks > 0 ? taxCycleTicks : TICKS_PER_YEAR
  const safeTick = Math.max(currentTick, 0)
  const cyclesCompleted = Math.floor(safeTick / cycleTicks)
  const currentCycleStart = cyclesCompleted * cycleTicks
  return safeTick === currentCycleStart ? currentCycleStart + cycleTicks : (cyclesCompleted + 1) * cycleTicks
}

function buildMockGameStatePayload(gameState: MockState['gameState']) {
  const currentGameYear = computeMockGameYear(gameState.currentTick)
  const nextTaxTick = computeMockNextTaxTick(gameState.currentTick, gameState.taxCycleTicks)
  const currentQuarter = Math.floor(gameState.currentTick / TICKS_PER_QUARTER) % 4
  const quarterLabels = ['Q1', 'Q2', 'Q3', 'Q4']

  return {
    ...gameState,
    currentGameYear,
    currentGameTimeUtc: computeMockInGameTimeUtc(gameState.currentTick),
    ticksPerDay: TICKS_PER_DAY,
    ticksPerYear: TICKS_PER_YEAR,
    nextTaxTick,
    nextTaxGameTimeUtc: computeMockInGameTimeUtc(nextTaxTick),
    nextTaxGameYear: computeMockGameYear(nextTaxTick),
    currentQuarter,
    currentQuarterLabel: quarterLabels[currentQuarter] ?? 'Q1',
  }
}

function buildDefaultCityWeatherForecast(cityId: string, currentTick: number): MockCityWeatherForecast {
  const forecast = Array.from({ length: 50 }, (_, index) => {
    const tick = currentTick + index
    const windPercent = 55 + ((index % 5) - 2) * 4
    const solarPercent = Math.max(0, 85 - Math.abs((index % 12) - 6) * 12)

    return {
      tick,
      windPercent: Math.max(0, Math.min(100, windPercent)),
      solarPercent: Math.max(0, Math.min(100, solarPercent)),
    }
  })

  return {
    cityId,
    currentWindPercent: forecast[0]?.windPercent ?? 55,
    currentSolarPercent: forecast[0]?.solarPercent ?? 85,
    forecast,
  }
}

function buildMockLedgerHistoryYear(summary: MockLedgerSummary, currentGameYear: number): MockLedgerHistoryYear {
  const gameYear = summary.gameYear ?? currentGameYear
  const taxableIncome =
    summary.taxableIncome ??
    Math.max(
      summary.totalRevenue -
        summary.totalPurchasingCosts -
        (summary.totalShippingCosts ?? 0) -
        summary.totalLaborCosts -
        summary.totalEnergyCosts -
        summary.totalMarketingCosts -
        summary.totalOtherCosts,
      0,
    )

  return {
    gameYear,
    isCurrentGameYear: summary.isCurrentGameYear ?? gameYear === currentGameYear,
    totalRevenue: summary.totalRevenue,
    totalLaborCosts: summary.totalLaborCosts,
    totalEnergyCosts: summary.totalEnergyCosts,
    netIncome: summary.netIncome,
    totalTaxPaid: summary.totalTaxPaid,
    taxableIncome,
    estimatedIncomeTax: summary.estimatedIncomeTax ?? summary.totalTaxPaid,
    firstRecordedTick: summary.firstRecordedTick,
    lastRecordedTick: summary.lastRecordedTick,
  }
}

function buildMockLedgerSummaryPayload(summary: MockLedgerSummary, gameState: MockState['gameState']) {
  const currentGameYear = computeMockGameYear(gameState.currentTick)
  const gameYear = summary.gameYear ?? currentGameYear
  const incomeTaxDueAtTick = summary.incomeTaxDueAtTick ?? (gameYear - GAME_START_YEAR + 1) * TICKS_PER_YEAR

  return {
    ...summary,
    gameYear,
    isCurrentGameYear: summary.isCurrentGameYear ?? gameYear === currentGameYear,
    primaryCurrencyCode: summary.primaryCurrencyCode ?? 'EUR',
    primaryCurrencySymbol: summary.primaryCurrencySymbol ?? '€',
    hasMixedCurrencies: summary.hasMixedCurrencies ?? false,
    totalStockPurchaseCashOut: summary.totalStockPurchaseCashOut ?? 0,
    totalStockSaleCashIn: summary.totalStockSaleCashIn ?? 0,
    totalShippingCosts: summary.totalShippingCosts ?? 0,
    totalMediaHouseIncome: summary.totalMediaHouseIncome ?? 0,
    taxableIncome:
      summary.taxableIncome ??
      Math.max(
        summary.totalRevenue -
          summary.totalPurchasingCosts -
          (summary.totalShippingCosts ?? 0) -
          summary.totalLaborCosts -
          summary.totalEnergyCosts -
          summary.totalMarketingCosts -
          summary.totalOtherCosts,
        0,
      ),
    estimatedIncomeTax: summary.estimatedIncomeTax ?? summary.totalTaxPaid,
    incomeTaxDueAtTick,
    incomeTaxDueGameTimeUtc: summary.incomeTaxDueGameTimeUtc ?? computeMockInGameTimeUtc(incomeTaxDueAtTick),
    incomeTaxDueGameYear: summary.incomeTaxDueGameYear ?? computeMockGameYear(incomeTaxDueAtTick),
    isIncomeTaxSettled: summary.isIncomeTaxSettled ?? gameYear < currentGameYear,
    history: summary.history ?? [buildMockLedgerHistoryYear(summary, currentGameYear)],
    buildingSummaries: (summary.buildingSummaries ?? []).map((b) => ({
      ...b,
      currencyCode: b.currencyCode ?? 'EUR',
      currencySymbol: b.currencySymbol ?? '€',
    })),
  }
}

function buildMockBuildingFinancialTimeline(state: MockState, buildingId: string, limit = 100): MockBuildingFinancialTimeline | null {
  const explicitTimeline = state.buildingFinancialTimelines[buildingId]
  if (explicitTimeline) {
    return explicitTimeline
  }

  const building = state.players
    .flatMap((player) => player.companies)
    .flatMap((company) => company.buildings)
    .find((candidate) => candidate.id === buildingId)

  if (!building) {
    return null
  }

  const safeLimit = Math.max(1, limit)
  const dataToTick = state.gameState.currentTick
  const dataFromTick = Math.max(0, dataToTick - (safeLimit - 1))

  return {
    buildingId,
    buildingName: building.name,
    dataFromTick,
    dataToTick,
    totalSales: 0,
    totalCosts: 0,
    totalProfit: 0,
    timeline: Array.from({ length: dataToTick - dataFromTick + 1 }, (_, index) => ({
      tick: dataFromTick + index,
      sales: 0,
      costs: 0,
      profit: 0,
    })),
  }
}

function cloneUnit(unit: MockBuildingUnit): MockBuildingUnit {
  return {
    ...unit,
    inventoryItems: unit.inventoryItems?.map((item) => ({ ...item })) ?? undefined,
    resourceHistory: unit.resourceHistory?.map((entry) => ({ ...entry })) ?? undefined,
  }
}

function getMockUnitResourceHistory(unit: MockBuildingUnit) {
  return (unit.resourceHistory ?? []).map((entry) => ({
    buildingUnitId: entry.buildingUnitId ?? unit.id,
    resourceTypeId: entry.resourceTypeId ?? null,
    productTypeId: entry.productTypeId ?? null,
    tick: entry.tick,
    inflowQuantity: entry.inflowQuantity ?? 0,
    outflowQuantity: entry.outflowQuantity ?? 0,
    consumedQuantity: entry.consumedQuantity ?? 0,
    producedQuantity: entry.producedQuantity ?? 0,
  }))
}

function computeDistanceKm(latitudeA: number, longitudeA: number, latitudeB: number, longitudeB: number) {
  const earthRadiusKm = 6371
  const deltaLatitude = ((latitudeB - latitudeA) * Math.PI) / 180
  const deltaLongitude = ((longitudeB - longitudeA) * Math.PI) / 180
  const originLatitude = (latitudeA * Math.PI) / 180
  const destinationLatitude = (latitudeB * Math.PI) / 180

  const haversine = Math.sin(deltaLatitude / 2) ** 2 + Math.cos(originLatitude) * Math.cos(destinationLatitude) * Math.sin(deltaLongitude / 2) ** 2
  return earthRadiusKm * (2 * Math.atan2(Math.sqrt(haversine), Math.sqrt(1 - haversine)))
}

function computeMockExchangePrice(basePrice: number, abundance: number, averageRentPerSqm: number, fxRate = 1) {
  const scarcityMultiplier = 1.55 - Math.min(Math.max(abundance, 0), 1) * 0.75
  const cityMultiplier = 0.95 + averageRentPerSqm / 100
  return Number((basePrice * scarcityMultiplier * cityMultiplier * fxRate).toFixed(2))
}

function computeMockExchangeQuality(abundance: number) {
  return Number(Math.min(Math.max(0.35 + abundance * 0.6, 0.35), 0.95).toFixed(4))
}

function computeMockExchangeQualityBand(abundance: number): { min: number; max: number } {
  const normalizedAbundance = Math.min(Math.max(abundance, 0), 1)
  const centralQuality = 0.35 + normalizedAbundance * 0.6
  const bandWidth = 0.05 + (1 - normalizedAbundance) * 0.15
  const halfBand = bandWidth / 2
  const min = Number(Math.min(Math.max(centralQuality - halfBand, 0.05), 0.99).toFixed(4))
  const max = Number(Math.min(Math.max(centralQuality + halfBand, 0.05), 0.99).toFixed(4))
  return { min, max }
}

function computeMockTransitCost(weightPerUnit: number, distanceKm: number, fxRate = 1) {
  const rawCost = distanceKm * Math.max(weightPerUnit, 0.1) * 0.0025
  const eurCost = Math.max(rawCost, 0.05)
  return Number(Math.max(eurCost * fxRate, 0.01).toFixed(2))
}

function buildMockAskPriceHistory(currentTick: number, exchangePricePerUnit: number, seed: string) {
  const history: Array<{ tick: number; askPricePerUnit: number }> = []
  const seedValue = [...seed].reduce((sum, char) => sum + char.charCodeAt(0), 0)
  const window = 50

  for (let offset = window - 1; offset >= 0; offset--) {
    const tick = currentTick - offset
    if (tick < 0) continue

    const waveA = Math.sin((tick + seedValue) * 0.17)
    const waveB = Math.cos((tick + seedValue * 0.5) * 0.07)
    const factor = 1 + waveA * 0.025 + waveB * 0.015
    history.push({
      tick,
      askPricePerUnit: Number((exchangePricePerUnit * Math.max(0.7, factor)).toFixed(2)),
    })
  }

  if (history.length > 0) {
    const last = history[history.length - 1]
    if (last) {
      last.askPricePerUnit = Number(exchangePricePerUnit.toFixed(2))
    }
  }

  return history
}

function getMockUnitCapacity(unit: MockBuildingUnit) {
  const level = unit.level
  switch (unit.unitType) {
    case 'STORAGE':
      // Storage units hold 10× the base capacity (mirrors GameConstants.StorageUnitHoldingCapacity)
      return level >= 4 ? 10000 : level === 3 ? 5000 : level === 2 ? 2500 : 1000
    case 'MINING':
    case 'B2B_SALES':
    case 'PURCHASE':
    case 'MANUFACTURING':
    case 'BRANDING':
    case 'PUBLIC_SALES':
      return level >= 4 ? 1000 : level === 3 ? 500 : level === 2 ? 250 : 100
    default:
      return 0
  }
}

function getMockUnitInventoryItems(unit: MockBuildingUnit) {
  if (unit.inventoryItems && unit.inventoryItems.length > 0) {
    return unit.inventoryItems.map((item, index) => ({
      id: item.id ?? `${unit.id}-inventory-${index}`,
      buildingUnitId: unit.id,
      resourceTypeId: item.resourceTypeId ?? null,
      productTypeId: item.productTypeId ?? null,
      quantity: item.quantity,
      quality: item.quality ?? unit.inventoryQuality ?? 0.5,
      sourcingCostTotal: item.sourcingCostTotal ?? 0,
      sourcingCostPerUnit: item.quantity > 0 ? Number(((item.sourcingCostTotal ?? 0) / item.quantity).toFixed(2)) : 0,
    }))
  }

  if ((unit.inventoryQuantity ?? 0) <= 0) {
    return []
  }

  return [
    {
      id: `${unit.id}-inventory-legacy`,
      buildingUnitId: unit.id,
      resourceTypeId: unit.resourceTypeId ?? null,
      productTypeId: unit.productTypeId ?? null,
      quantity: unit.inventoryQuantity ?? 0,
      quality: unit.inventoryQuality ?? 0.5,
      sourcingCostTotal: unit.inventorySourcingCostTotal ?? 0,
      sourcingCostPerUnit: (unit.inventoryQuantity ?? 0) > 0 ? Number(((unit.inventorySourcingCostTotal ?? 0) / (unit.inventoryQuantity ?? 0)).toFixed(2)) : 0,
    },
  ]
}

function areUnitsEquivalent(currentUnit: MockBuildingUnit | undefined, nextUnit: MockBuildingUnit): boolean {
  if (!currentUnit) {
    return false
  }

  return (
    currentUnit.unitType === nextUnit.unitType &&
    currentUnit.gridX === nextUnit.gridX &&
    currentUnit.gridY === nextUnit.gridY &&
    currentUnit.linkUp === nextUnit.linkUp &&
    currentUnit.linkDown === nextUnit.linkDown &&
    currentUnit.linkLeft === nextUnit.linkLeft &&
    currentUnit.linkRight === nextUnit.linkRight &&
    currentUnit.linkUpLeft === nextUnit.linkUpLeft &&
    currentUnit.linkUpRight === nextUnit.linkUpRight &&
    currentUnit.linkDownLeft === nextUnit.linkDownLeft &&
    currentUnit.linkDownRight === nextUnit.linkDownRight &&
    (currentUnit.resourceTypeId ?? null) === (nextUnit.resourceTypeId ?? null) &&
    (currentUnit.productTypeId ?? null) === (nextUnit.productTypeId ?? null) &&
    (currentUnit.minPrice ?? null) === (nextUnit.minPrice ?? null) &&
    (currentUnit.maxPrice ?? null) === (nextUnit.maxPrice ?? null) &&
    (currentUnit.purchaseSource ?? null) === (nextUnit.purchaseSource ?? null) &&
    (currentUnit.saleVisibility ?? null) === (nextUnit.saleVisibility ?? null) &&
    (currentUnit.budget ?? null) === (nextUnit.budget ?? null) &&
    (currentUnit.mediaHouseBuildingId ?? null) === (nextUnit.mediaHouseBuildingId ?? null) &&
    (currentUnit.minQuality ?? null) === (nextUnit.minQuality ?? null) &&
    (currentUnit.brandScope ?? null) === (nextUnit.brandScope ?? null) &&
    (currentUnit.vendorLockCompanyId ?? null) === (nextUnit.vendorLockCompanyId ?? null) &&
    (currentUnit.lockedCityId ?? null) === (nextUnit.lockedCityId ?? null) &&
    (currentUnit.industryCategory ?? null) === (nextUnit.industryCategory ?? null)
  )
}

function arePendingUnitsEquivalent(currentUnit: MockBuildingConfigurationPlanUnit | undefined, nextUnit: MockBuildingUnit): boolean {
  if (!currentUnit) {
    return false
  }

  return areUnitsEquivalent(currentUnit, nextUnit)
}

function calculateCancelTicks(baseTicks: number): number {
  return Math.max(Math.ceil(baseTicks * 0.1), 1)
}

function buildPlanSummary(plan: MockBuildingConfigurationPlan, currentTick: number): MockBuildingConfigurationPlan {
  const remainingTicks = Math.max(
    0,
    ...plan.units.filter((unit) => unit.isChanged).map((unit) => Math.max(unit.appliesAtTick - currentTick, 0)),
    ...plan.removals.map((removal) => Math.max(removal.appliesAtTick - currentTick, 0)),
  )

  return {
    ...plan,
    appliesAtTick: currentTick + remainingTicks,
    totalTicksRequired: remainingTicks,
  }
}

function calculateUnitTicks(currentUnit: MockBuildingUnit | undefined, nextUnit: MockBuildingUnit): number {
  if (!currentUnit) {
    return 3
  }

  if (currentUnit.unitType !== nextUnit.unitType) {
    return 3
  }

  if (
    currentUnit.linkUp !== nextUnit.linkUp ||
    currentUnit.linkDown !== nextUnit.linkDown ||
    currentUnit.linkLeft !== nextUnit.linkLeft ||
    currentUnit.linkRight !== nextUnit.linkRight ||
    currentUnit.linkUpLeft !== nextUnit.linkUpLeft ||
    currentUnit.linkUpRight !== nextUnit.linkUpRight ||
    currentUnit.linkDownLeft !== nextUnit.linkDownLeft ||
    currentUnit.linkDownRight !== nextUnit.linkDownRight
  ) {
    return 1
  }

  if (
    (currentUnit.resourceTypeId ?? null) !== (nextUnit.resourceTypeId ?? null) ||
    (currentUnit.productTypeId ?? null) !== (nextUnit.productTypeId ?? null) ||
    (currentUnit.minPrice ?? null) !== (nextUnit.minPrice ?? null) ||
    (currentUnit.maxPrice ?? null) !== (nextUnit.maxPrice ?? null) ||
    (currentUnit.purchaseSource ?? null) !== (nextUnit.purchaseSource ?? null) ||
    (currentUnit.saleVisibility ?? null) !== (nextUnit.saleVisibility ?? null) ||
    (currentUnit.budget ?? null) !== (nextUnit.budget ?? null) ||
    (currentUnit.mediaHouseBuildingId ?? null) !== (nextUnit.mediaHouseBuildingId ?? null) ||
    (currentUnit.minQuality ?? null) !== (nextUnit.minQuality ?? null) ||
    (currentUnit.brandScope ?? null) !== (nextUnit.brandScope ?? null) ||
    (currentUnit.vendorLockCompanyId ?? null) !== (nextUnit.vendorLockCompanyId ?? null) ||
    (currentUnit.lockedCityId ?? null) !== (nextUnit.lockedCityId ?? null) ||
    (currentUnit.industryCategory ?? null) !== (nextUnit.industryCategory ?? null)
  ) {
    return 1
  }

  return 0
}

function applyDueBuildingUpgrades(state: MockState): void {
  for (const player of state.players) {
    for (const company of player.companies) {
      for (const building of company.buildings) {
        if (!building.pendingConfiguration) {
          continue
        }

        const liveUnits = new Map(building.units.map((unit) => [`${unit.gridX},${unit.gridY}`, unit]))

        for (const removal of building.pendingConfiguration.removals.filter((candidate) => candidate.appliesAtTick <= state.gameState.currentTick)) {
          liveUnits.delete(`${removal.gridX},${removal.gridY}`)
        }

        for (const unit of building.pendingConfiguration.units.filter((candidate) => candidate.isChanged && candidate.appliesAtTick <= state.gameState.currentTick)) {
          liveUnits.set(`${unit.gridX},${unit.gridY}`, cloneUnit(unit))
          unit.isChanged = false
          unit.isReverting = false
          unit.startedAtTick = state.gameState.currentTick
          unit.appliesAtTick = state.gameState.currentTick
          unit.ticksRequired = 0
        }

        building.units = Array.from(liveUnits.values()).sort((left, right) => left.gridY - right.gridY || left.gridX - right.gridX)

        building.pendingConfiguration.removals = building.pendingConfiguration.removals.filter((candidate) => candidate.appliesAtTick > state.gameState.currentTick)

        if (!building.pendingConfiguration.units.some((unit) => unit.isChanged) && building.pendingConfiguration.removals.length === 0) {
          building.pendingConfiguration = null
          continue
        }

        building.pendingConfiguration = buildPlanSummary(building.pendingConfiguration, state.gameState.currentTick)
      }
    }
  }
}

// ── Factory functions ────────────────────────────────────────────────────────

function computeAvailableCash(player: MockPlayer): number {
  return player.personalCash - (player.personalTaxReserve ?? 0)
}

export function makePlayer(overrides?: Partial<MockPlayer>): MockPlayer {
  const player: MockPlayer = {
    id: 'player-1',
    email: 'player@test.com',
    password: 'TestPass1!',
    displayName: 'Test Player',
    role: 'PLAYER',
    isInvisibleInChat: false,
    createdAtUtc: '2026-01-01T00:00:00Z',
    lastLoginAtUtc: null,
    personalCash: PERSONAL_STARTING_CASH,
    personalTaxReserve: 0,
    activeAccountType: 'PERSON',
    activeCompanyId: null,
    onboardingCompletedAtUtc: null,
    onboardingCurrentStep: null,
    onboardingIndustry: null,
    onboardingCityId: null,
    onboardingCompanyId: null,
    onboardingFactoryLotId: null,
    onboardingShopBuildingId: null,
    onboardingFirstSaleCompletedAtUtc: null,
    appliedReferralCode: null,
    proSubscriptionEndsAtUtc: null,
    interestPayments: [],
    dividendPayments: [],
    stockTrades: [],
    companies: [],
    ...overrides,
  }

  return player
}

function applyImplicitCompanyAccountContext(player: MockPlayer) {
  if (player.companies.length === 0) {
    return
  }

  const activeCompanyExists = player.activeCompanyId ? player.companies.some((company) => company.id === player.activeCompanyId) : false

  if (player.activeAccountType === 'COMPANY' && activeCompanyExists) {
    return
  }

  const firstCompany = player.companies[0]
  if (!firstCompany) {
    return
  }

  player.activeAccountType = 'COMPANY'
  player.activeCompanyId = firstCompany.id
}

export function makeAdminPlayer(overrides?: Partial<MockPlayer>): MockPlayer {
  return makePlayer({
    id: 'admin-1',
    email: 'admin@test.com',
    displayName: 'Admin',
    role: 'ADMIN',
    ...overrides,
  })
}

const woodResource: MockResourceType = {
  id: 'res-wood',
  name: 'Wood',
  slug: 'wood',
  category: 'ORGANIC',
  basePrice: 10,
  weightPerUnit: 5,
  unitName: 'Ton',
  unitSymbol: 't',
  imageUrl: null,
  description: 'Harvested timber.',
}

const grainResource: MockResourceType = {
  id: 'res-grain',
  name: 'Grain',
  slug: 'grain',
  category: 'ORGANIC',
  basePrice: 5,
  weightPerUnit: 2,
  unitName: 'Ton',
  unitSymbol: 't',
  imageUrl: null,
  description: 'Cereal crops.',
}

const chemResource: MockResourceType = {
  id: 'res-chem',
  name: 'Chemical Minerals',
  slug: 'chemical-minerals',
  category: 'MINERAL',
  basePrice: 30,
  weightPerUnit: 3,
  unitName: 'Ton',
  unitSymbol: 't',
  imageUrl: null,
  description: 'Raw minerals for pharma.',
}

export function makeDefaultResources(): MockResourceType[] {
  // Return fresh objects for every call so test-local mutations never leak
  // into other specs running in the same Playwright worker process.
  return [{ ...woodResource }, { ...grainResource }, { ...chemResource }]
}

export function makeBratislava(): MockCity {
  return {
    id: 'city-ba',
    name: 'Bratislava',
    countryCode: 'SK',
    currencyCode: 'EUR',
    latitude: 48.1486,
    longitude: 17.1077,
    population: 475000,
    averageRentPerSqm: 14,
    baseSalaryPerManhour: 18,
    resources: [
      { resourceType: { id: 'res-wood', name: 'Wood', slug: 'wood', category: 'ORGANIC' }, abundance: 0.7 },
      { resourceType: { id: 'res-grain', name: 'Grain', slug: 'grain', category: 'ORGANIC' }, abundance: 0.6 },
    ],
  }
}

export function makeDefaultCities(): MockCity[] {
  return [
    makeBratislava(),
    {
      id: 'city-pr',
      name: 'Prague',
      countryCode: 'CZ',
      currencyCode: 'CZK',
      latitude: 50.0755,
      longitude: 14.4378,
      population: 1350000,
      averageRentPerSqm: 18,
      baseSalaryPerManhour: 22,
      resources: [
        { resourceType: { id: 'res-wood', name: 'Wood', slug: 'wood', category: 'ORGANIC' }, abundance: 0.7 },
        { resourceType: { id: 'res-grain', name: 'Grain', slug: 'grain', category: 'ORGANIC' }, abundance: 0.6 },
      ],
    },
    {
      id: 'city-vi',
      name: 'Vienna',
      countryCode: 'AT',
      currencyCode: 'EUR',
      latitude: 48.2082,
      longitude: 16.3738,
      population: 1900000,
      averageRentPerSqm: 22,
      baseSalaryPerManhour: 28,
      resources: [
        { resourceType: { id: 'res-wood', name: 'Wood', slug: 'wood', category: 'ORGANIC' }, abundance: 0.7 },
        { resourceType: { id: 'res-grain', name: 'Grain', slug: 'grain', category: 'ORGANIC' }, abundance: 0.6 },
      ],
    },
    {
      id: 'city-ny',
      name: 'New York',
      countryCode: 'US',
      currencyCode: 'USD',
      latitude: 40.7128,
      longitude: -74.006,
      population: 8336000,
      averageRentPerSqm: 55,
      baseSalaryPerManhour: 35,
      resources: [
        { resourceType: { id: 'res-wood', name: 'Wood', slug: 'wood', category: 'ORGANIC' }, abundance: 0.7 },
        { resourceType: { id: 'res-grain', name: 'Grain', slug: 'grain', category: 'ORGANIC' }, abundance: 0.6 },
        { resourceType: { id: 'res-silicon', name: 'Silicon', slug: 'silicon', category: 'MINERAL' }, abundance: 0.5 },
      ],
    },
    {
      id: 'city-ld',
      name: 'London',
      countryCode: 'GB',
      currencyCode: 'GBP',
      latitude: 51.5074,
      longitude: -0.1278,
      population: 8982000,
      averageRentPerSqm: 62,
      baseSalaryPerManhour: 32,
      resources: [
        { resourceType: { id: 'res-wood', name: 'Wood', slug: 'wood', category: 'ORGANIC' }, abundance: 0.7 },
        { resourceType: { id: 'res-grain', name: 'Grain', slug: 'grain', category: 'ORGANIC' }, abundance: 0.6 },
        { resourceType: { id: 'res-cotton', name: 'Cotton', slug: 'cotton', category: 'ORGANIC' }, abundance: 0.4 },
      ],
    },
    {
      id: 'city-bj',
      name: 'Beijing',
      countryCode: 'CN',
      currencyCode: 'CNY',
      latitude: 39.9042,
      longitude: 116.4074,
      population: 21540000,
      averageRentPerSqm: 30,
      baseSalaryPerManhour: 20,
      resources: [
        { resourceType: { id: 'res-wood', name: 'Wood', slug: 'wood', category: 'ORGANIC' }, abundance: 0.7 },
        { resourceType: { id: 'res-grain', name: 'Grain', slug: 'grain', category: 'ORGANIC' }, abundance: 0.6 },
        { resourceType: { id: 'res-coal', name: 'Coal', slug: 'coal', category: 'MINERAL' }, abundance: 0.8 },
      ],
    },
    {
      id: 'city-dl',
      name: 'Delhi',
      countryCode: 'IN',
      currencyCode: 'INR',
      latitude: 28.6139,
      longitude: 77.209,
      population: 32000000,
      averageRentPerSqm: 8,
      baseSalaryPerManhour: 6,
      resources: [
        { resourceType: { id: 'res-wood', name: 'Wood', slug: 'wood', category: 'ORGANIC' }, abundance: 0.7 },
        { resourceType: { id: 'res-grain', name: 'Grain', slug: 'grain', category: 'ORGANIC' }, abundance: 0.6 },
        { resourceType: { id: 'res-cotton', name: 'Cotton', slug: 'cotton', category: 'ORGANIC' }, abundance: 0.7 },
      ],
    },
    {
      id: 'city-be',
      name: 'Berlin',
      countryCode: 'DE',
      currencyCode: 'EUR',
      latitude: 52.52,
      longitude: 13.405,
      population: 3677472,
      averageRentPerSqm: 20,
      baseSalaryPerManhour: 22,
      resources: [
        { resourceType: { id: 'res-coal', name: 'Coal', slug: 'coal', category: 'MINERAL' }, abundance: 0.8 },
        { resourceType: { id: 'res-iron-ore', name: 'Iron Ore', slug: 'iron-ore', category: 'MINERAL' }, abundance: 0.7 },
        { resourceType: { id: 'res-silicon', name: 'Silicon', slug: 'silicon', category: 'MINERAL' }, abundance: 0.6 },
        { resourceType: { id: 'res-grain', name: 'Grain', slug: 'grain', category: 'ORGANIC' }, abundance: 0.6 },
      ],
    },
    {
      id: 'city-wa',
      name: 'Warsaw',
      countryCode: 'PL',
      currencyCode: 'PLN',
      latitude: 52.2297,
      longitude: 21.0122,
      population: 1860281,
      averageRentPerSqm: 30,
      baseSalaryPerManhour: 35,
      resources: [
        { resourceType: { id: 'res-grain', name: 'Grain', slug: 'grain', category: 'ORGANIC' }, abundance: 0.8 },
        { resourceType: { id: 'res-wood', name: 'Wood', slug: 'wood', category: 'ORGANIC' }, abundance: 0.7 },
        { resourceType: { id: 'res-coal', name: 'Coal', slug: 'coal', category: 'MINERAL' }, abundance: 0.6 },
        { resourceType: { id: 'res-iron-ore', name: 'Iron Ore', slug: 'iron-ore', category: 'MINERAL' }, abundance: 0.5 },
      ],
    },
  ]
}

export function makeDefaultBuildingLots(): MockBuildingLot[] {
  return [
    {
      id: 'lot-industrial-1',
      cityId: 'city-ba',
      name: 'Industrial Plot A1',
      description: 'Large industrial plot near the eastern logistics corridor. Sits above an Iron Ore deposit (18,000t at 72% quality).',
      district: 'Industrial Zone',
      latitude: 48.152,
      longitude: 17.125,
      populationIndex: 0.65,
      basePrice: 75000, // Base land value (no resource premium)
      price: 32464500, // = appraised land (75000 * 0.86 = 64500) + resource premium (18000t * $25/t * 0.72 * 100 = 32,400,000)
      suitableTypes: 'FACTORY,MINE',
      ownerCompanyId: null,
      buildingId: null,
      ownerCompany: null,
      building: null,
      resourceType: { id: 'res-iron-ore', name: 'Iron Ore', slug: 'iron-ore' },
      materialQuality: 0.72,
      materialQuantity: 18000,
      originalMaterialQuantity: 18000,
    },
    {
      id: 'lot-industrial-2',
      cityId: 'city-ba',
      name: 'Factory Site B1',
      description: 'Modern industrial park with good power grid access. Suitable for energy-intensive production.',
      district: 'Industrial Zone',
      latitude: 48.15,
      longitude: 17.13,
      populationIndex: 0.72,
      basePrice: 90000,
      price: 90000,
      suitableTypes: 'FACTORY,POWER_PLANT',
      ownerCompanyId: null,
      buildingId: null,
      ownerCompany: null,
      building: null,
      resourceType: null,
      materialQuality: null,
      materialQuantity: null,
    },
    {
      id: 'lot-commercial-1',
      cityId: 'city-ba',
      name: 'High Street Retail Space',
      description: 'Prime storefront on the main pedestrian avenue.',
      district: 'Commercial District',
      latitude: 48.145,
      longitude: 17.107,
      populationIndex: 1.42,
      basePrice: 108000,
      price: 120000,
      suitableTypes: 'SALES_SHOP,COMMERCIAL',
      ownerCompanyId: null,
      buildingId: null,
      ownerCompany: null,
      building: null,
      resourceType: null,
      materialQuality: null,
      materialQuantity: null,
    },
    {
      id: 'lot-residential-1',
      cityId: 'city-ba',
      name: 'Riverside Apartment Block',
      description: 'Scenic residential plot overlooking the Danube.',
      district: 'Residential Quarter',
      latitude: 48.14,
      longitude: 17.1,
      populationIndex: 1.18,
      basePrice: 102000,
      price: 110000,
      suitableTypes: 'APARTMENT',
      ownerCompanyId: null,
      buildingId: null,
      ownerCompany: null,
      building: null,
      resourceType: null,
      materialQuality: null,
      materialQuantity: null,
    },
    {
      id: 'lot-business-1',
      cityId: 'city-ba',
      name: 'Innovation Campus Office',
      description: 'Modern office complex in the technology business park.',
      district: 'Business Park',
      latitude: 48.156,
      longitude: 17.11,
      populationIndex: 1.08,
      basePrice: 124000,
      price: 130000,
      suitableTypes: 'RESEARCH_DEVELOPMENT,BANK',
      ownerCompanyId: null,
      buildingId: null,
      ownerCompany: null,
      building: null,
      resourceType: null,
      materialQuality: null,
      materialQuantity: null,
    },
  ]
}

export function makeChairProduct(): MockProductType {
  return {
    id: 'prod-chair',
    name: 'Wooden Chair',
    slug: 'wooden-chair',
    industry: 'FURNITURE',
    basePrice: 45,
    baseCraftTicks: 2,
    outputQuantity: 20,
    energyConsumptionMwh: 1,
    basicLaborHours: 1.6,
    unitName: 'Chair',
    unitSymbol: 'chairs',
    isProOnly: false,
    description: 'A basic wooden chair.',
    recipes: [{ resourceType: { id: 'res-wood', name: 'Wood', slug: 'wood', unitName: 'Ton', unitSymbol: 't' }, inputProductType: null, quantity: 1 }],
  }
}

export function makeDefaultProducts(): MockProductType[] {
  return [
    makeChairProduct(),
    {
      id: 'prod-table',
      name: 'Wooden Table',
      slug: 'wooden-table',
      industry: 'FURNITURE',
      basePrice: 120,
      baseCraftTicks: 3,
      outputQuantity: 10,
      energyConsumptionMwh: 0.8,
      basicLaborHours: 1.4,
      unitName: 'Table',
      unitSymbol: 'tables',
      isProOnly: false,
      description: 'Classic wooden table.',
      recipes: [{ resourceType: { id: 'res-wood', name: 'Wood', slug: 'wood', unitName: 'Log', unitSymbol: 'logs' }, inputProductType: null, quantity: 2 }],
    },
    {
      id: 'prod-bed',
      name: 'Wooden Bed',
      slug: 'wooden-bed',
      industry: 'FURNITURE',
      basePrice: 200,
      baseCraftTicks: 4,
      outputQuantity: 6,
      energyConsumptionMwh: 1.1,
      basicLaborHours: 1.8,
      unitName: 'Bed',
      unitSymbol: 'beds',
      isProOnly: false,
      description: 'Comfortable wooden bed frame.',
      recipes: [{ resourceType: { id: 'res-wood', name: 'Wood', slug: 'wood', unitName: 'Log', unitSymbol: 'logs' }, inputProductType: null, quantity: 3 }],
    },
    {
      id: 'prod-bread',
      name: 'Bread',
      slug: 'bread',
      industry: 'FOOD_PROCESSING',
      basePrice: 3,
      baseCraftTicks: 1,
      outputQuantity: 12,
      energyConsumptionMwh: 0.5,
      basicLaborHours: 0.9,
      unitName: 'Loaf',
      unitSymbol: 'loaves',
      isProOnly: false,
      description: 'Basic wheat bread.',
      recipes: [{ resourceType: { id: 'res-grain', name: 'Grain', slug: 'grain', unitName: 'Ton', unitSymbol: 't' }, inputProductType: null, quantity: 1 }],
    },
    {
      id: 'prod-pasta',
      name: 'Pasta',
      slug: 'pasta',
      industry: 'FOOD_PROCESSING',
      basePrice: 9,
      baseCraftTicks: 2,
      outputQuantity: 16,
      energyConsumptionMwh: 0.7,
      basicLaborHours: 1.1,
      unitName: 'Pack',
      unitSymbol: 'packs',
      isProOnly: false,
      description: 'Dry pasta made from grain flour.',
      recipes: [{ resourceType: { id: 'res-grain', name: 'Grain', slug: 'grain', unitName: 'Ton', unitSymbol: 't' }, inputProductType: null, quantity: 2 }],
    },
    {
      id: 'prod-crackers',
      name: 'Crackers',
      slug: 'crackers',
      industry: 'FOOD_PROCESSING',
      basePrice: 6,
      baseCraftTicks: 2,
      outputQuantity: 20,
      energyConsumptionMwh: 0.6,
      basicLaborHours: 1.0,
      unitName: 'Pack',
      unitSymbol: 'packs',
      isProOnly: false,
      description: 'Baked snack crackers.',
      recipes: [{ resourceType: { id: 'res-grain', name: 'Grain', slug: 'grain', unitName: 'Ton', unitSymbol: 't' }, inputProductType: null, quantity: 1 }],
    },
    {
      id: 'prod-medicine',
      name: 'Basic Medicine',
      slug: 'basic-medicine',
      industry: 'HEALTHCARE',
      basePrice: 50,
      baseCraftTicks: 3,
      outputQuantity: 8,
      energyConsumptionMwh: 1,
      basicLaborHours: 2.1,
      unitName: 'Bottle',
      unitSymbol: 'bottles',
      isProOnly: false,
      description: 'Essential pharma product.',
      recipes: [{ resourceType: { id: 'res-chem', name: 'Chemical Minerals', slug: 'chemical-minerals', unitName: 'Ton', unitSymbol: 't' }, inputProductType: null, quantity: 1 }],
    },
    {
      id: 'prod-bandages',
      name: 'Bandages',
      slug: 'bandages',
      industry: 'HEALTHCARE',
      basePrice: 15,
      baseCraftTicks: 1,
      outputQuantity: 18,
      energyConsumptionMwh: 0.4,
      basicLaborHours: 1.1,
      unitName: 'Pack',
      unitSymbol: 'packs',
      isProOnly: false,
      description: 'Basic wound care bandages.',
      recipes: [{ resourceType: { id: 'res-chem', name: 'Chemical Minerals', slug: 'chemical-minerals', unitName: 'Ton', unitSymbol: 't' }, inputProductType: null, quantity: 1 }],
    },
    {
      id: 'prod-first-aid-kit',
      name: 'First Aid Kit',
      slug: 'first-aid-kit',
      industry: 'HEALTHCARE',
      basePrice: 42,
      baseCraftTicks: 2,
      outputQuantity: 10,
      energyConsumptionMwh: 0.9,
      basicLaborHours: 1.7,
      unitName: 'Kit',
      unitSymbol: 'kits',
      isProOnly: false,
      description: 'Retail first aid kit.',
      recipes: [{ resourceType: { id: 'res-chem', name: 'Chemical Minerals', slug: 'chemical-minerals', unitName: 'Ton', unitSymbol: 't' }, inputProductType: null, quantity: 2 }],
    },
    {
      id: 'prod-basic-electronics',
      name: 'Basic Electronics',
      slug: 'basic-electronics',
      industry: 'ELECTRONICS',
      basePrice: 45,
      baseCraftTicks: 3,
      outputQuantity: 12,
      energyConsumptionMwh: 1.0,
      basicLaborHours: 1.8,
      unitName: 'Pack',
      unitSymbol: 'packs',
      isProOnly: true,
      description: 'A starter pack of electronic components assembled from raw silicon.',
      recipes: [{ resourceType: { id: 'res-silicon', name: 'Silicon', slug: 'silicon', unitName: 'Ton', unitSymbol: 't' }, inputProductType: null, quantity: 1 }],
    },
    {
      id: 'prod-led-screen',
      name: 'LED Screen',
      slug: 'led-screen',
      industry: 'ELECTRONICS',
      basePrice: 85,
      baseCraftTicks: 4,
      outputQuantity: 6,
      energyConsumptionMwh: 1.3,
      basicLaborHours: 2.2,
      unitName: 'Display',
      unitSymbol: 'displays',
      isProOnly: true,
      description: 'A flat-panel LED display made from silicon.',
      recipes: [{ resourceType: { id: 'res-silicon', name: 'Silicon', slug: 'silicon', unitName: 'Ton', unitSymbol: 't' }, inputProductType: null, quantity: 1 }],
    },
    {
      id: 'prod-circuit-board',
      name: 'Circuit Board',
      slug: 'circuit-board',
      industry: 'ELECTRONICS',
      basePrice: 55,
      baseCraftTicks: 3,
      outputQuantity: 10,
      energyConsumptionMwh: 1.1,
      basicLaborHours: 1.9,
      unitName: 'Board',
      unitSymbol: 'boards',
      isProOnly: true,
      description: 'A populated circuit board assembled from silicon.',
      recipes: [{ resourceType: { id: 'res-silicon', name: 'Silicon', slug: 'silicon', unitName: 'Ton', unitSymbol: 't' }, inputProductType: null, quantity: 2 }],
    },
    {
      id: 'prod-residential-block',
      name: 'Residential Block',
      slug: 'residential-block',
      industry: 'CONSTRUCTION',
      basePrice: 80,
      baseCraftTicks: 3,
      outputQuantity: 8,
      energyConsumptionMwh: 1.2,
      basicLaborHours: 2.1,
      unitName: 'Block',
      unitSymbol: 'blocks',
      isProOnly: true,
      description: 'A prefabricated residential building block made from processed iron.',
      recipes: [{ resourceType: { id: 'res-iron-ore', name: 'Iron Ore', slug: 'iron-ore', unitName: 'Ton', unitSymbol: 't' }, inputProductType: null, quantity: 2 }],
    },
    {
      id: 'prod-commercial-block',
      name: 'Commercial Block',
      slug: 'commercial-block',
      industry: 'CONSTRUCTION',
      basePrice: 120,
      baseCraftTicks: 4,
      outputQuantity: 5,
      energyConsumptionMwh: 1.5,
      basicLaborHours: 2.6,
      unitName: 'Block',
      unitSymbol: 'blocks',
      isProOnly: true,
      description: 'A structural block for commercial buildings.',
      recipes: [{ resourceType: { id: 'res-iron-ore', name: 'Iron Ore', slug: 'iron-ore', unitName: 'Ton', unitSymbol: 't' }, inputProductType: null, quantity: 3 }],
    },
    {
      id: 'prod-industrial-block',
      name: 'Industrial Block',
      slug: 'industrial-block',
      industry: 'CONSTRUCTION',
      basePrice: 180,
      baseCraftTicks: 5,
      outputQuantity: 3,
      energyConsumptionMwh: 1.8,
      basicLaborHours: 3.3,
      unitName: 'Block',
      unitSymbol: 'blocks',
      isProOnly: true,
      description: 'A heavy-duty industrial building block engineered for factories and warehouses.',
      recipes: [{ resourceType: { id: 'res-iron-ore', name: 'Iron Ore', slug: 'iron-ore', unitName: 'Ton', unitSymbol: 't' }, inputProductType: null, quantity: 4 }],
    },
    // Pharmaceuticals starter products (Pro-only, Gold resource)
    {
      id: 'prod-aspirin',
      name: 'Aspirin',
      slug: 'aspirin',
      industry: 'PHARMACEUTICALS',
      basePrice: 55,
      baseCraftTicks: 3,
      outputQuantity: 10,
      energyConsumptionMwh: 1.0,
      basicLaborHours: 2.0,
      unitName: 'Bottle',
      unitSymbol: 'bottles',
      isProOnly: true,
      description: 'A starter pharmaceutical tablet synthesised from refined gold compounds.',
      recipes: [{ resourceType: { id: 'res-gold', name: 'Gold', slug: 'gold', unitName: 'Kilogram', unitSymbol: 'kg' }, inputProductType: null, quantity: 1 }],
    },
    {
      id: 'prod-vitamin-capsule',
      name: 'Vitamin Capsule',
      slug: 'vitamin-capsule',
      industry: 'PHARMACEUTICALS',
      basePrice: 80,
      baseCraftTicks: 4,
      outputQuantity: 6,
      energyConsumptionMwh: 1.2,
      basicLaborHours: 2.6,
      unitName: 'Pack',
      unitSymbol: 'packs',
      isProOnly: true,
      description: 'Premium vitamin supplement produced from pure gold compounds.',
      recipes: [{ resourceType: { id: 'res-gold', name: 'Gold', slug: 'gold', unitName: 'Kilogram', unitSymbol: 'kg' }, inputProductType: null, quantity: 1 }],
    },
    {
      id: 'prod-antibiotic',
      name: 'Antibiotic',
      slug: 'antibiotic',
      industry: 'PHARMACEUTICALS',
      basePrice: 120,
      baseCraftTicks: 5,
      outputQuantity: 4,
      energyConsumptionMwh: 1.5,
      basicLaborHours: 3.3,
      unitName: 'Box',
      unitSymbol: 'boxes',
      isProOnly: true,
      description: 'A broad-spectrum antibiotic formulated from concentrated gold catalyst compounds.',
      recipes: [{ resourceType: { id: 'res-gold', name: 'Gold', slug: 'gold', unitName: 'Kilogram', unitSymbol: 'kg' }, inputProductType: null, quantity: 2 }],
    },
    // Energy starter products (Pro-only, Coal resource)
    {
      id: 'prod-coal-briquette',
      name: 'Coal Briquette',
      slug: 'coal-briquette',
      industry: 'ENERGY',
      basePrice: 28,
      baseCraftTicks: 2,
      outputQuantity: 15,
      energyConsumptionMwh: 0.8,
      basicLaborHours: 1.4,
      unitName: 'Bag',
      unitSymbol: 'bags',
      isProOnly: true,
      description: 'A compressed coal briquette providing consistent heat output.',
      recipes: [{ resourceType: { id: 'res-coal', name: 'Coal', slug: 'coal', unitName: 'Ton', unitSymbol: 't' }, inputProductType: null, quantity: 2 }],
    },
    {
      id: 'prod-heating-oil',
      name: 'Heating Oil',
      slug: 'heating-oil',
      industry: 'ENERGY',
      basePrice: 50,
      baseCraftTicks: 3,
      outputQuantity: 8,
      energyConsumptionMwh: 1.1,
      basicLaborHours: 2.0,
      unitName: 'Barrel',
      unitSymbol: 'barrels',
      isProOnly: true,
      description: 'Refined heating oil distilled from coal.',
      recipes: [{ resourceType: { id: 'res-coal', name: 'Coal', slug: 'coal', unitName: 'Ton', unitSymbol: 't' }, inputProductType: null, quantity: 3 }],
    },
    {
      id: 'prod-industrial-fuel',
      name: 'Industrial Fuel',
      slug: 'industrial-fuel',
      industry: 'ENERGY',
      basePrice: 75,
      baseCraftTicks: 4,
      outputQuantity: 5,
      energyConsumptionMwh: 1.4,
      basicLaborHours: 2.6,
      unitName: 'Drum',
      unitSymbol: 'drums',
      isProOnly: true,
      description: 'High-density industrial fuel refined from premium coal stocks.',
      recipes: [{ resourceType: { id: 'res-coal', name: 'Coal', slug: 'coal', unitName: 'Ton', unitSymbol: 't' }, inputProductType: null, quantity: 4 }],
    },
    // Logistics starter products (Pro-only, Cotton resource)
    {
      id: 'prod-shipping-bag',
      name: 'Shipping Bag',
      slug: 'shipping-bag',
      industry: 'LOGISTICS',
      basePrice: 20,
      baseCraftTicks: 2,
      outputQuantity: 18,
      energyConsumptionMwh: 0.6,
      basicLaborHours: 1.2,
      unitName: 'Bag',
      unitSymbol: 'bags',
      isProOnly: true,
      description: 'A durable cotton shipping bag for consumer goods distribution.',
      recipes: [{ resourceType: { id: 'res-cotton', name: 'Cotton', slug: 'cotton', unitName: 'Ton', unitSymbol: 't' }, inputProductType: null, quantity: 1 }],
    },
    {
      id: 'prod-storage-sack',
      name: 'Storage Sack',
      slug: 'storage-sack',
      industry: 'LOGISTICS',
      basePrice: 35,
      baseCraftTicks: 3,
      outputQuantity: 10,
      energyConsumptionMwh: 0.9,
      basicLaborHours: 1.8,
      unitName: 'Sack',
      unitSymbol: 'sacks',
      isProOnly: true,
      description: 'Reinforced cotton storage sack for bulk commodity warehousing.',
      recipes: [{ resourceType: { id: 'res-cotton', name: 'Cotton', slug: 'cotton', unitName: 'Ton', unitSymbol: 't' }, inputProductType: null, quantity: 2 }],
    },
    {
      id: 'prod-cargo-pack',
      name: 'Cargo Pack',
      slug: 'cargo-pack',
      industry: 'LOGISTICS',
      basePrice: 55,
      baseCraftTicks: 4,
      outputQuantity: 6,
      energyConsumptionMwh: 1.2,
      basicLaborHours: 2.5,
      unitName: 'Pack',
      unitSymbol: 'packs',
      isProOnly: true,
      description: 'Heavy-duty cotton cargo pack built for international shipping.',
      recipes: [{ resourceType: { id: 'res-cotton', name: 'Cotton', slug: 'cotton', unitName: 'Ton', unitSymbol: 't' }, inputProductType: null, quantity: 3 }],
    },
  ]
}

export function makeDefaultFxRates(): MockFxRate[] {
  const today = new Date().toISOString().slice(0, 10)
  return [
    { baseCurrencyCode: 'EUR', quoteCurrencyCode: 'USD', rate: 1.08, rateDate: today, source: 'FALLBACK', quoteCurrencySymbol: '$' },
    { baseCurrencyCode: 'EUR', quoteCurrencyCode: 'GBP', rate: 0.86, rateDate: today, source: 'FALLBACK', quoteCurrencySymbol: '£' },
    { baseCurrencyCode: 'EUR', quoteCurrencyCode: 'CNY', rate: 7.83, rateDate: today, source: 'FALLBACK', quoteCurrencySymbol: '¥' },
    { baseCurrencyCode: 'EUR', quoteCurrencyCode: 'INR', rate: 89.5, rateDate: today, source: 'FALLBACK', quoteCurrencySymbol: '₹' },
    { baseCurrencyCode: 'EUR', quoteCurrencyCode: 'CZK', rate: 25.19, rateDate: today, source: 'FALLBACK', quoteCurrencySymbol: 'Kč' },
  ]
}

/**
 * Builds a list of FX rate history snapshots for E2E tests.
 * Returns `count` ticks of buy/mid/sell snapshots for the given currency pair,
 * with a slight random walk applied to mid so lines move naturally in the chart.
 */
export function makeFxRateHistory(
  quoteCurrencyCode: string,
  baseMidRate: number,
  count = 20,
  startTick = 1,
): Array<{
  baseCurrencyCode: string
  quoteCurrencyCode: string
  midRate: number
  buyRate: number
  sellRate: number
  gameTick: number
  capturedAtUtc: string
}> {
  const SPREAD = 0.005
  const snapshots = []
  let mid = baseMidRate
  const base = new Date('2026-01-01T00:00:00Z').getTime()
  for (let i = 0; i < count; i++) {
    // small drift: ±0.2% per step
    mid = mid * (1 + (Math.random() - 0.5) * 0.004)
    snapshots.push({
      baseCurrencyCode: 'EUR',
      quoteCurrencyCode,
      midRate: Math.round(mid * 10000) / 10000,
      buyRate: Math.round(mid * (1 + SPREAD) * 10000) / 10000,
      sellRate: Math.round(mid * (1 - SPREAD) * 10000) / 10000,
      gameTick: startTick + i,
      capturedAtUtc: new Date(base + i * 3600 * 1000).toISOString(),
    })
  }
  return snapshots
}

/**
 * Returns 3 default government-owned media houses (NEWSPAPER, RADIO, TV) for a city.
 * Used as the fallback when `state.cityMediaHouses[cityId]` is not set.
 */
export function makeDefaultGovernmentMediaHouses(cityId: string, state: Pick<MockState, 'cities'>): MockCityMediaHouseInfo[] {
  const city = state.cities.find((c) => c.id === cityId)
  const cityName = city?.name ?? 'Unknown City'
  const govCompanyId = 'gov-company-id'
  return [
    {
      id: `gov-newspaper-${cityId}`,
      name: `${cityName} Gazette`,
      cityId,
      cityName,
      mediaType: 'NEWSPAPER',
      ownerCompanyId: govCompanyId,
      ownerCompanyName: 'Government',
      effectivenessMultiplier: 1.0,
      powerStatus: 'POWERED',
      isUnderConstruction: false,
      contentRanking: 100,
      contentValue: 1000,
      contentBudgetPerTick: null,
      isGovernmentOwned: true,
    },
    {
      id: `gov-radio-${cityId}`,
      name: `${cityName} Radio`,
      cityId,
      cityName,
      mediaType: 'RADIO',
      ownerCompanyId: govCompanyId,
      ownerCompanyName: 'Government',
      effectivenessMultiplier: 1.5,
      powerStatus: 'POWERED',
      isUnderConstruction: false,
      contentRanking: 100,
      contentValue: 1000,
      contentBudgetPerTick: null,
      isGovernmentOwned: true,
    },
    {
      id: `gov-tv-${cityId}`,
      name: `${cityName} TV`,
      cityId,
      cityName,
      mediaType: 'TV',
      ownerCompanyId: govCompanyId,
      ownerCompanyName: 'Government',
      effectivenessMultiplier: 2.0,
      powerStatus: 'POWERED',
      isUnderConstruction: false,
      contentRanking: 100,
      contentValue: 1000,
      contentBudgetPerTick: null,
      isGovernmentOwned: true,
    },
  ]
}

// ── Mock API setup ───────────────────────────────────────────────────────────

export function setupMockApi(page: Page, initial?: Partial<MockState>): MockState {
  const state: MockState = {
    serverKey: 'test-server',
    players: [],
    shareholdings: [],
    cities: makeDefaultCities(),
    buildingLots: makeDefaultBuildingLots(),
    resourceTypes: makeDefaultResources(),
    productTypes: makeDefaultProducts(),
    currentUserId: null,
    currentToken: null,
    gameState: { currentTick: 42, lastTickAtUtc: new Date(Date.now() - 30000).toISOString(), tickIntervalSeconds: 60, taxCycleTicks: 8760, taxRate: 15 },
    economicCycle: {
      id: 'eco-cycle-1',
      phase: 'EXPANSION',
      phaseStartedTick: 0,
      expectedDurationTicks: 2160,
      intensityFactor: 1.2,
      phaseEndTick: 2160,
      ticksRemaining: 2118,
    },
    activeMarketEvents: [],
    economicHistory: [],
    endgameStatus: {
      gameEnded: false,
      winnerPlayerId: null,
      winnerDisplayName: null,
      winnerCompanyName: null,
      gameEndedAtUtc: null,
      winningThresholdUsd: 430000000000,
      topRealWorldRichest: [
        { id: 'rw-1', rank: 1, name: 'Elon Musk', wealthUsd: 430000000000 },
        { id: 'rw-2', rank: 2, name: 'Jeff Bezos', wealthUsd: 245000000000 },
        { id: 'rw-3', rank: 3, name: 'Mark Zuckerberg', wealthUsd: 216000000000 },
        { id: 'rw-4', rank: 4, name: 'Larry Ellison', wealthUsd: 192000000000 },
        { id: 'rw-5', rank: 5, name: 'Bernard Arnault', wealthUsd: 178000000000 },
        { id: 'rw-6', rank: 6, name: 'Larry Page', wealthUsd: 144000000000 },
        { id: 'rw-7', rank: 7, name: 'Sergey Brin', wealthUsd: 138000000000 },
        { id: 'rw-8', rank: 8, name: 'Warren Buffett', wealthUsd: 133000000000 },
        { id: 'rw-9', rank: 9, name: 'Steve Ballmer', wealthUsd: 130000000000 },
        { id: 'rw-10', rank: 10, name: 'Jensen Huang', wealthUsd: 116000000000 },
      ],
    },
    cityWeatherForecasts: {},
    stockPriceHistory: {},
    stockLimitOrders: [],
    stockLimitOrderExecutions: [],
    dividendProposals: [],
    dividendVotes: [],
    ledgerData: {},
    drillDownData: {},
    researchBrands: {},
    publicSalesRecords: [],
    publicSalesAnalytics: {},
    unitProductAnalytics: {},
    campaignAnalytics: {},
    marketIntelligenceByCity: {},
    buildingFinancialTimelines: {},
    loanOffers: [],
    myLoans: [],
    collateralBuildings: [],
    myDeposits: [],
    allBanks: [],
    procurementPreviews: {},
    sourcingCandidates: {},
    unitUpgradeInfoOverrides: {},
    upgradeInsufficientFundsUnitId: null,
    upgradeMaxConcurrentUnitId: null,
    upgradeAlreadyUpgradingUnitId: null,
    productExchangeListings: [],
    chatMessages: [],
    rootAdminEmails: ['root@example.com'],
    globalGameAdminGrants: [],
    gameNewsEntries: [],
    adminMoneyInflowSummaries: [],
    adminShippingCostSummaries: [],
    adminMultiAccountAlerts: [],
    adminAuditLogs: [],
    impersonationSession: null,
    buildingLayouts: [],
    forceBuildingConfigError: null,
    unitLastTickMovement: {},
    fxRates: makeDefaultFxRates(),
    fxRateHistorySnapshots: [],
    playerCurrencyBalances: [],
    forexTradeHistory: [],
    bankStatementRows: {},
    personalBankStatementRows: [],
    cityMediaHouses: {},
    buildingBankAccounts: {},
    playerNotifications: [],
    myBankAccounts: [],
    goldAmmPools: [],
    goldBalance: { balance: 0, blockedInPools: 0, availableBalance: 0 },
    goldAmmSwapHistory: [],
    marketReports: [],
    supplyChainData: {},
    marketOverviewByCityId: {},
    marketPriceHistoryByProductId: {},
    buildingMarketListings: [],
    myBuildingListings: [],
    tradeRoutes: [],
    additionalCompanyPrerequisites: null,
    tutorialProgress: [
      { milestone: 'FIRST_RESOURCE_SOLD', isCompleted: false, completedAtUtc: null, bountyAwarded: false, bountyAwardedAtUtc: null, bountyPoints: 50 },
      { milestone: 'FIRST_B2B_TRADE', isCompleted: false, completedAtUtc: null, bountyAwarded: false, bountyAwardedAtUtc: null, bountyPoints: 75 },
      { milestone: 'FIRST_LOAN_TAKEN', isCompleted: false, completedAtUtc: null, bountyAwarded: false, bountyAwardedAtUtc: null, bountyPoints: 60 },
      { milestone: 'FIRST_COMPETITOR_OBSERVED', isCompleted: false, completedAtUtc: null, bountyAwarded: false, bountyAwardedAtUtc: null, bountyPoints: 40 },
      { milestone: 'FIRST_BRAND_ESTABLISHED', isCompleted: false, completedAtUtc: null, bountyAwarded: false, bountyAwardedAtUtc: null, bountyPoints: 80 },
      { milestone: 'FIRST_BUILDING_DETAIL_VISIT', isCompleted: false, completedAtUtc: null, bountyAwarded: false, bountyAwardedAtUtc: null, bountyPoints: 30 },
      { milestone: 'FIRST_GRID_EDITOR_OPEN', isCompleted: false, completedAtUtc: null, bountyAwarded: false, bountyAwardedAtUtc: null, bountyPoints: 30 },
      // Tooltip milestones default to completed so existing tests are not interrupted by overlays
      { milestone: 'TOOLTIP_DASHBOARD_SHOWN', isCompleted: true, completedAtUtc: '2026-01-01T00:00:00Z', bountyAwarded: false, bountyAwardedAtUtc: null, bountyPoints: null },
    ],
    playerBadges: {},
    playerRankSnapshots: {},
    passwordResetTokens: {},
    ...initial,
  }

  state.players.forEach(applyImplicitCompanyAccountContext)

  const resolveCurrentPlayer = () => {
    const tokenPlayerId = state.currentToken?.startsWith('token-') ? state.currentToken.slice('token-'.length) : null

    return (
      state.players.find((player) => player.id === state.currentUserId) ??
      (tokenPlayerId ? state.players.find((player) => player.id === tokenPlayerId) : undefined) ??
      (state.players.length === 1 ? state.players[0] : undefined)
    )
  }

  const resolveAdminActor = () => {
    if (state.impersonationSession) {
      return state.players.find((player) => player.id === state.impersonationSession?.adminActorUserId)
    }

    return resolveCurrentPlayer()
  }

  const resolveEffectivePlayer = () => {
    if (state.impersonationSession) {
      return state.players.find((player) => player.id === state.impersonationSession?.effectiveUserId)
    }

    return resolveCurrentPlayer()
  }

  const resolveEffectiveAccountContext = () => {
    if (state.impersonationSession) {
      return {
        activeAccountType: state.impersonationSession.effectiveAccountType,
        activeCompanyId: state.impersonationSession.effectiveCompanyId,
      }
    }

    const currentPlayer = resolveCurrentPlayer()
    return {
      activeAccountType: currentPlayer?.activeAccountType ?? 'PERSON',
      activeCompanyId: currentPlayer?.activeCompanyId ?? null,
    }
  }

  const buildPlayerPayload = (player: MockPlayer | undefined) => {
    if (!player) {
      return null
    }

    const accountContext = resolveEffectiveAccountContext()
    return {
      ...player,
      password: undefined,
      activeAccountType: player.id === resolveEffectivePlayer()?.id ? accountContext.activeAccountType : player.activeAccountType,
      activeCompanyId: player.id === resolveEffectivePlayer()?.id ? accountContext.activeCompanyId : player.activeCompanyId,
      isInvisibleInChat: player.isInvisibleInChat ?? false,
    }
  }

  const buildGameAdminPlayer = (player: MockPlayer) => ({
    id: player.id,
    email: player.email,
    displayName: player.displayName,
    role: player.role,
    isInvisibleInChat: player.isInvisibleInChat ?? false,
    createdAtUtc: player.createdAtUtc,
    lastLoginAtUtc: player.lastLoginAtUtc,
    personalCash: player.personalCash,
    totalCompanyCash: Number(player.companies.reduce((total, company) => total + company.cash, 0).toFixed(2)),
    totalCompanyEquity: Number(
      (player.companies.reduce((total, company) => total + company.cash, 0) + player.companies.reduce((total, company) => total + company.buildings.length * MOCK_BUILDING_BASE_VALUE, 0)).toFixed(2),
    ),
    companyCount: player.companies.length,
    cityNames: [...new Set(player.companies.flatMap((company) => company.buildings.map((building) => state.cities.find((city) => city.id === building.cityId)?.name ?? '').filter((name) => !!name)))],
    companies: player.companies.map((company) => ({
      id: company.id,
      name: company.name,
      cash: company.cash,
    })),
  })

  const buildGameNewsEntry = (entry: MockGameNewsEntry) => ({
    id: entry.id,
    entryType: entry.entryType,
    status: entry.status,
    targetServerKey: entry.targetServerKey,
    createdByEmail: entry.createdByEmail,
    updatedByEmail: entry.updatedByEmail,
    createdAtUtc: entry.createdAtUtc,
    updatedAtUtc: entry.updatedAtUtc,
    publishedAtUtc: entry.publishedAtUtc,
    isRead: !!state.currentUserId && entry.readByPlayerIds.includes(state.currentUserId),
    localizations: entry.localizations.map((localization) => ({ ...localization })),
  })

  const buildGameAdminSession = () => {
    const adminActor = resolveAdminActor()
    const effectivePlayer = resolveEffectivePlayer()

    if (!adminActor) {
      return {
        isLocalAdmin: false,
        hasGlobalAdminRole: false,
        isRootAdministrator: false,
        canAccessAdminDashboard: false,
        isImpersonating: false,
        effectiveAccountType: 'PERSON',
        effectiveCompanyId: null,
        effectiveCompanyName: null,
        adminActor: null,
        effectivePlayer: null,
      }
    }

    const isRootAdministrator = state.rootAdminEmails.some((email) => email.toLowerCase() === adminActor.email.toLowerCase())
    const hasGlobalAdminRole = state.globalGameAdminGrants.some((grant) => grant.email.toLowerCase() === adminActor.email.toLowerCase())
    const isLocalAdmin = adminActor.role === 'ADMIN'
    const accountContext = resolveEffectiveAccountContext()
    const effectiveCompany = effectivePlayer?.companies.find((company) => company.id === accountContext.activeCompanyId) ?? null

    return {
      isLocalAdmin,
      hasGlobalAdminRole,
      isRootAdministrator,
      canAccessAdminDashboard: isRootAdministrator || hasGlobalAdminRole || isLocalAdmin,
      isImpersonating: !!state.impersonationSession,
      effectiveAccountType: accountContext.activeAccountType,
      effectiveCompanyId: accountContext.activeCompanyId,
      effectiveCompanyName: effectiveCompany?.name ?? null,
      adminActor: buildGameAdminPlayer(adminActor),
      effectivePlayer: effectivePlayer ? buildGameAdminPlayer(effectivePlayer) : null,
    }
  }

  const getAdminAccessFailure = (requireRoot = false) => {
    const session = buildGameAdminSession()

    if ((requireRoot && !session.isRootAdministrator) || (!requireRoot && !session.canAccessAdminDashboard)) {
      return { message: 'Not authorized for game administration.', code: 'AUTH_NOT_AUTHORIZED' }
    }

    return null
  }

  mockStateByPage.set(page, state)

  page.route('**/auth/forgot-password', async (route) => {
    if (route.request().method() !== 'POST') {
      return route.fallback()
    }

    const payload = route.request().postDataJSON() as { email?: string } | null
    const normalizedEmail = payload?.email?.trim().toLowerCase() ?? ''
    const matchingPlayer = state.players.find((player) => player.email.toLowerCase() === normalizedEmail)
    if (matchingPlayer) {
      const token = `reset-${matchingPlayer.id}`
      state.passwordResetTokens[token] = matchingPlayer.email
    }

    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ message: 'If an account exists, a reset link has been sent.' }),
    })
  })

  page.route('**/auth/reset-password', async (route) => {
    if (route.request().method() !== 'POST') {
      return route.fallback()
    }

    const payload = route.request().postDataJSON() as { token?: string; newPassword?: string } | null
    const token = payload?.token?.trim() ?? ''
    const newPassword = payload?.newPassword?.trim() ?? ''
    if (!token || !newPassword || newPassword.length < 8) {
      return route.fulfill({
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Password must be at least 8 characters.', code: 'PASSWORD_TOO_SHORT' }),
      })
    }

    const targetEmail = state.passwordResetTokens[token]
    if (!targetEmail) {
      return route.fulfill({
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({
          message: 'This reset link is invalid or expired. Please request a new one.',
          code: 'RESET_TOKEN_INVALID_OR_EXPIRED',
        }),
      })
    }

    const targetPlayer = state.players.find((player) => player.email.toLowerCase() === targetEmail.toLowerCase())
    if (!targetPlayer) {
      return route.fulfill({
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({
          message: 'This reset link is invalid or expired. Please request a new one.',
          code: 'RESET_TOKEN_INVALID_OR_EXPIRED',
        }),
      })
    }

    targetPlayer.password = newPassword
    delete state.passwordResetTokens[token]

    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ message: 'Password has been reset successfully.' }),
    })
  })

  page.route('**/auth/sessions', async (route) => {
    if (route.request().method() !== 'GET') {
      return route.fallback()
    }

    const currentToken = state.currentToken ?? ''
    const currentJti = currentToken.startsWith('token-') ? currentToken.slice('token-'.length) : 'session-current'
    const sessions = [
      {
        jti: currentJti,
        device: 'Current Browser',
        ipAddress: '127.0.0.1',
        lastSeenAtUtc: new Date().toISOString(),
        issuedAtUtc: new Date(Date.now() - 60 * 60 * 1000).toISOString(),
        expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
        isCurrent: true,
        isRevoked: false,
      },
      {
        jti: 'session-other-device',
        device: 'Other Device',
        ipAddress: '10.0.0.2',
        lastSeenAtUtc: new Date(Date.now() - 5 * 60 * 1000).toISOString(),
        issuedAtUtc: new Date(Date.now() - 3 * 60 * 60 * 1000).toISOString(),
        expiresAtUtc: new Date(Date.now() + 30 * 60 * 1000).toISOString(),
        isCurrent: false,
        isRevoked: false,
      },
    ]

    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ sessions }),
    })
  })

  page.route('**/auth/logout-all', async (route) => {
    if (route.request().method() !== 'POST') {
      return route.fallback()
    }

    return route.fulfill({
      status: 204,
      body: '',
    })
  })

  page.route('**/auth/logout', async (route) => {
    if (route.request().method() !== 'POST') {
      return route.fallback()
    }

    return route.fulfill({
      status: 204,
      body: '',
    })
  })

  page.route('**/graphql', async (route) => {
    const routeJson = (data: unknown) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data }),
      })

    const routeJsonError = (message: string, code?: string) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ errors: [{ message, ...(code ? { extensions: { code } } : {}) }] }),
      })

    const body = route.request().postDataJSON()
    const query: string = body?.query ?? ''
    const isRegisterMutation = query.includes('mutation Register') || query.includes('register(input:')
    const isLoginMutation = query.includes('mutation Login') || query.includes('login(input:')
    // Auth token check
    const authHeader = route.request().headers()['authorization'] ?? ''
    if (authHeader.startsWith('Bearer impersonation-')) {
      const rawToken = authHeader.replace('Bearer ', '')
      const [, sessionPayload] = rawToken.split('impersonation-')
      const [adminActorUserId, effectiveUserId, effectiveAccountType, effectiveCompanyId] = sessionPayload.split(':')

      state.currentToken = rawToken
      state.currentUserId = effectiveUserId ?? null
      state.impersonationSession = {
        adminActorUserId,
        effectiveUserId,
        effectiveAccountType: effectiveAccountType === 'COMPANY' ? 'COMPANY' : 'PERSON',
        effectiveCompanyId: effectiveCompanyId && effectiveCompanyId !== 'null' ? effectiveCompanyId : null,
      }
    } else if (authHeader.startsWith('Bearer token-')) {
      state.currentToken = authHeader.replace('Bearer ', '')
      state.currentUserId = authHeader.replace('Bearer token-', '')
      state.impersonationSession = null
    }

    // Mutations
    if (isRegisterMutation) {
      const input = body.variables?.input
      if (state.players.some((p) => p.email === input?.email)) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'A player with this email already exists.', extensions: { code: 'DUPLICATE_EMAIL' } }] }),
        })
      }
      const newPlayer: MockPlayer = {
        id: `player-${Date.now()}`,
        email: input.email,
        password: input.password,
        displayName: input.displayName,
        role: 'PLAYER',
        isInvisibleInChat: false,
        createdAtUtc: new Date().toISOString(),
        lastLoginAtUtc: null,
        personalCash: PERSONAL_STARTING_CASH,
        personalTaxReserve: 0,
        activeAccountType: 'PERSON',
        activeCompanyId: null,
        onboardingCompletedAtUtc: null,
        onboardingCurrentStep: null,
        onboardingIndustry: null,
        onboardingCityId: null,
        onboardingCompanyId: null,
        onboardingFactoryLotId: null,
        onboardingShopBuildingId: null,
        onboardingFirstSaleCompletedAtUtc: null,
        appliedReferralCode: typeof input?.referralCode === 'string' && /^[A-Za-z0-9]{4,20}$/.test(input.referralCode.trim()) ? input.referralCode.trim().toUpperCase() : null,
        proSubscriptionEndsAtUtc: null,
        interestPayments: [],
        dividendPayments: [],
        stockTrades: [],
        companies: [],
      }
      state.players.push(newPlayer)
      state.currentUserId = newPlayer.id
      state.currentToken = `token-${newPlayer.id}`
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            register: {
              token: `token-${newPlayer.id}`,
              expiresAtUtc: new Date(Date.now() + 7200000).toISOString(),
              player: buildPlayerPayload(newPlayer),
            },
          },
        }),
      })
    }

    if (isLoginMutation) {
      const input = body.variables?.input
      const player = state.players.find((p) => p.email === input?.email && p.password === input?.password)
      if (!player) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Invalid email or password.' }] }) })
      }
      player.lastLoginAtUtc = new Date().toISOString()
      state.currentUserId = player.id
      state.currentToken = `token-${player.id}`
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            login: {
              token: `token-${player.id}`,
              expiresAtUtc: new Date(Date.now() + 7200000).toISOString(),
              player: buildPlayerPayload(player),
            },
          },
        }),
      })
    }

    if (query.includes('myBuildingLayouts')) {
      if (!state.currentUserId) {
        return routeJsonError('Not authenticated.')
      }

      return routeJson({
        myBuildingLayouts: state.buildingLayouts
          .filter((layout) => layout.ownerPlayerId === state.currentUserId)
          .map((layout) => ({
            id: layout.id,
            name: layout.name,
            description: layout.description,
            buildingType: layout.buildingType,
            unitsJson: layout.unitsJson,
            updatedAtUtc: layout.updatedAtUtc,
          })),
      })
    }

    if (query.includes('saveBuildingLayout')) {
      if (!state.currentUserId) {
        return routeJsonError('Not authenticated.')
      }

      const input = body.variables?.input
      const existingId = input?.existingId as string | null | undefined
      const existingIndex = existingId ? state.buildingLayouts.findIndex((layout) => layout.id === existingId && layout.ownerPlayerId === state.currentUserId) : -1
      const updatedAtUtc = new Date().toISOString()
      const layout: MockBuildingLayoutTemplate = {
        id: existingIndex >= 0 ? state.buildingLayouts[existingIndex]!.id : `layout-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
        ownerPlayerId: state.currentUserId,
        name: input?.name ?? 'Untitled Layout',
        description: input?.description ?? null,
        buildingType: input?.buildingType ?? 'FACTORY',
        unitsJson: input?.unitsJson ?? '[]',
        updatedAtUtc,
      }

      if (existingIndex >= 0) {
        state.buildingLayouts.splice(existingIndex, 1, layout)
      } else {
        state.buildingLayouts.unshift(layout)
      }

      return routeJson({
        saveBuildingLayout: {
          id: layout.id,
          name: layout.name,
          description: layout.description,
          buildingType: layout.buildingType,
          unitsJson: layout.unitsJson,
          updatedAtUtc: layout.updatedAtUtc,
        },
      })
    }

    if (query.includes('deleteBuildingLayout')) {
      if (!state.currentUserId) {
        return routeJsonError('Not authenticated.')
      }

      const input = body.variables?.input
      state.buildingLayouts = state.buildingLayouts.filter((layout) => !(layout.id === input?.id && layout.ownerPlayerId === state.currentUserId))

      return routeJson({ deleteBuildingLayout: true })
    }

    if (query.includes('StartOnboardingCompany')) {
      const input = body.variables?.input
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Not authenticated' }] }) })
      }

      const lot = state.buildingLots.find((candidate) => candidate.id === input?.factoryLotId)
      if (!lot) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Building lot not found.' }] }) })
      }
      if (lot.ownerCompanyId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'This lot has already been purchased.', extensions: { code: 'LOT_ALREADY_OWNED' } }] }),
        })
      }
      if (
        !lot.suitableTypes
          .split(',')
          .map((type) => type.trim())
          .includes('FACTORY')
      ) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Building type FACTORY is not suitable for this lot.' }] }) })
      }
      const ipoSelection = resolveIpoSelection(Number(body.variables?.input?.ipoRaiseTarget))
      const startingCompanyCash = STARTER_FOUNDER_CONTRIBUTION + ipoSelection.raiseTarget

      if (startingCompanyCash < lot.price) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: `Insufficient funds. This lot costs $${lot.price.toLocaleString()}.` }] }) })
      }

      const companyId = `company-${Date.now()}`
      const factoryId = `building-factory-${Date.now()}`
      const company: MockCompany = {
        id: companyId,
        playerId: player.id,
        name: input.companyName,
        cash: startingCompanyCash - lot.price,
        totalSharesIssued: DEFAULT_COMPANY_SHARE_COUNT,
        dividendPayoutRatio: DEFAULT_DIVIDEND_PAYOUT_RATIO,
        foundedAtUtc: new Date().toISOString(),
        foundedAtTick: state.gameState.currentTick,
        buildings: [
          {
            id: factoryId,
            companyId,
            cityId: input.cityId,
            type: 'FACTORY',
            name: `${input.companyName} Factory`,
            latitude: lot.latitude,
            longitude: lot.longitude,
            level: 1,
            powerConsumption: 2,
            powerStatus: 'POWERED',
            isForSale: false,
            builtAtUtc: new Date().toISOString(),
            units: [],
            pendingConfiguration: null,
          },
        ],
      }

      lot.ownerCompanyId = company.id
      lot.buildingId = factoryId
      lot.ownerCompany = { id: company.id, name: company.name }
      lot.building = { id: factoryId, name: `${input.companyName} Factory`, type: 'FACTORY' }

      player.personalCash -= STARTER_FOUNDER_CONTRIBUTION
      player.activeAccountType = 'COMPANY'
      player.activeCompanyId = company.id
      player.companies.push(company)
      state.shareholdings.push({
        companyId: company.id,
        ownerPlayerId: player.id,
        ownerCompanyId: null,
        shareCount: Number((DEFAULT_COMPANY_SHARE_COUNT * ipoSelection.founderOwnershipRatio).toFixed(4)),
      })
      player.onboardingCurrentStep = 'SHOP_SELECTION'
      player.onboardingIndustry = input.industry
      player.onboardingCityId = input.cityId
      player.onboardingCompanyId = company.id
      player.onboardingFactoryLotId = lot.id

      // Seed personal USD bank account (balance now 0 after founder contribution was transferred)
      const personalAccountId = `personal-usd-${player.id}`
      const existingPersonalAccount = state.myBankAccounts.find((a) => a.id === personalAccountId)
      if (!existingPersonalAccount) {
        state.myBankAccounts.push({
          id: personalAccountId,
          accountNumber: '1234567890123456',
          currencyCode: 'USD',
          currencySymbol: '$',
          balance: 0,
          companyId: null,
          companyName: null,
          ownerType: 'PERSON',
          ownerDisplayName: player.displayName,
        })
      } else {
        existingPersonalAccount.balance = 0
      }

      // Seed personal bank statement rows: government deposit then founder contribution
      const nowIso = new Date().toISOString()
      state.personalBankStatementRows = [
        {
          id: `row-gov-deposit-${player.id}`,
          recordedAtTick: 0,
          recordedAtUtc: nowIso,
          description: 'Government starter funding deposit',
          category: 'BANK_ACCOUNT_TRANSFER_IN',
          amount: STARTER_FOUNDER_CONTRIBUTION,
          runningBalance: STARTER_FOUNDER_CONTRIBUTION,
          buildingId: null,
          buildingName: null,
        },
        {
          id: `row-founder-contribution-${player.id}`,
          recordedAtTick: 1,
          recordedAtUtc: nowIso,
          description: `Founder contribution: ${STARTER_FOUNDER_CONTRIBUTION.toLocaleString()} USD government starter deposit`,
          category: 'FOUNDER_CONTRIBUTION',
          amount: -STARTER_FOUNDER_CONTRIBUTION,
          runningBalance: 0,
          buildingId: null,
          buildingName: null,
        },
      ]

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            startOnboardingCompany: {
              nextStep: 'SHOP_SELECTION',
              company: { id: company.id, name: company.name, cash: company.cash },
              factory: company.buildings[0],
              factoryLot: lot,
            },
          },
        }),
      })
    }

    if (query.includes('FinishOnboarding')) {
      const input = body.variables?.input
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player || player.onboardingCurrentStep !== 'SHOP_SELECTION' || !player.onboardingCompanyId) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'No onboarding progress was found to resume.' }] }) })
      }

      const company = player.companies.find((candidate) => candidate.id === player.onboardingCompanyId)
      const product = state.productTypes.find((candidate) => candidate.id === input?.productTypeId)
      const shopLot = state.buildingLots.find((candidate) => candidate.id === input?.shopLotId)

      if (!company || !product) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Product not found.' }] }) })
      }
      if (!shopLot) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Building lot not found.' }] }) })
      }
      if (shopLot.ownerCompanyId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'This lot has already been purchased.', extensions: { code: 'LOT_ALREADY_OWNED' } }] }),
        })
      }
      if (
        !shopLot.suitableTypes
          .split(',')
          .map((type) => type.trim())
          .includes('SALES_SHOP')
      ) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Building type SALES_SHOP is not suitable for this lot.' }] }) })
      }
      if (company.cash < shopLot.price) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: `Insufficient funds. This lot costs $${shopLot.price.toLocaleString()}.` }] }),
        })
      }

      company.cash -= shopLot.price
      const shopId = `building-shop-${Date.now()}`
      const productId = product?.id ?? ''
      const shopBuilding: MockBuilding = {
        id: shopId,
        companyId: company.id,
        cityId: shopLot.cityId,
        type: 'SALES_SHOP',
        name: `${company.name} Shop`,
        latitude: shopLot.latitude,
        longitude: shopLot.longitude,
        level: 1,
        powerConsumption: 1,
        powerStatus: 'POWERED',
        isForSale: false,
        builtAtUtc: new Date().toISOString(),
        units: [
          {
            id: `unit-shop-purchase-${Date.now()}`,
            buildingId: shopId,
            unitType: 'PURCHASE',
            gridX: 0,
            gridY: 0,
            level: 1,
            linkRight: true,
            linkLeft: false,
            linkUp: false,
            linkDown: false,
            linkUpLeft: false,
            linkUpRight: false,
            linkDownLeft: false,
            linkDownRight: false,
            productTypeId: productId,
            resourceTypeId: null,
            minPrice: null,
            maxPrice: null,
            purchaseSource: 'LOCAL',
            saleVisibility: null,
            budget: null,
            mediaHouseBuildingId: null,
            minQuality: null,
            brandScope: null,
            vendorLockCompanyId: company.id,
          },
          {
            id: `unit-shop-publicsales-${Date.now() + 1}`,
            buildingId: shopId,
            unitType: 'PUBLIC_SALES',
            gridX: 1,
            gridY: 0,
            level: 1,
            linkRight: false,
            linkLeft: false,
            linkUp: false,
            linkDown: false,
            linkUpLeft: false,
            linkUpRight: false,
            linkDownLeft: false,
            linkDownRight: false,
            productTypeId: productId,
            resourceTypeId: null,
            minPrice: product?.basePrice != null ? product.basePrice * 1.5 : null,
            maxPrice: null,
            purchaseSource: null,
            saleVisibility: null,
            budget: null,
            mediaHouseBuildingId: null,
            minQuality: null,
            brandScope: null,
            vendorLockCompanyId: null,
          },
        ],
        pendingConfiguration: null,
      }

      // Configure the factory with starter units (mirrors ConfigureStarterFactory on backend)
      const factoryBuilding = company.buildings.find((candidate) => candidate.type === 'FACTORY')
      if (factoryBuilding && factoryBuilding.units.length === 0) {
        const resourceTypeId = product?.recipes[0]?.resourceType?.id ?? null
        factoryBuilding.units = [
          {
            id: `unit-factory-purchase-${Date.now()}`,
            buildingId: factoryBuilding.id,
            unitType: 'PURCHASE',
            gridX: 0,
            gridY: 0,
            level: 1,
            linkRight: true,
            linkLeft: false,
            linkUp: false,
            linkDown: false,
            linkUpLeft: false,
            linkUpRight: false,
            linkDownLeft: false,
            linkDownRight: false,
            productTypeId: null,
            resourceTypeId,
            minPrice: null,
            maxPrice: null,
            purchaseSource: 'OPTIMAL',
            saleVisibility: null,
            budget: null,
            mediaHouseBuildingId: null,
            minQuality: null,
            brandScope: null,
            vendorLockCompanyId: null,
          },
          {
            id: `unit-factory-manufacturing-${Date.now() + 1}`,
            buildingId: factoryBuilding.id,
            unitType: 'MANUFACTURING',
            gridX: 1,
            gridY: 0,
            level: 1,
            linkRight: true,
            linkLeft: false,
            linkUp: false,
            linkDown: false,
            linkUpLeft: false,
            linkUpRight: false,
            linkDownLeft: false,
            linkDownRight: false,
            productTypeId: productId,
            resourceTypeId: null,
            minPrice: null,
            maxPrice: null,
            purchaseSource: null,
            saleVisibility: null,
            budget: null,
            mediaHouseBuildingId: null,
            minQuality: null,
            brandScope: null,
            vendorLockCompanyId: null,
          },
          {
            id: `unit-factory-storage-${Date.now() + 2}`,
            buildingId: factoryBuilding.id,
            unitType: 'STORAGE',
            gridX: 2,
            gridY: 0,
            level: 1,
            linkRight: true,
            linkLeft: false,
            linkUp: false,
            linkDown: false,
            linkUpLeft: false,
            linkUpRight: false,
            linkDownLeft: false,
            linkDownRight: false,
            productTypeId: null,
            resourceTypeId: null,
            minPrice: null,
            maxPrice: null,
            purchaseSource: null,
            saleVisibility: null,
            budget: null,
            mediaHouseBuildingId: null,
            minQuality: null,
            brandScope: null,
            vendorLockCompanyId: null,
          },
          {
            id: `unit-factory-b2bsales-${Date.now() + 3}`,
            buildingId: factoryBuilding.id,
            unitType: 'B2B_SALES',
            gridX: 3,
            gridY: 0,
            level: 1,
            linkRight: false,
            linkLeft: false,
            linkUp: false,
            linkDown: false,
            linkUpLeft: false,
            linkUpRight: false,
            linkDownLeft: false,
            linkDownRight: false,
            productTypeId: productId,
            resourceTypeId: null,
            minPrice: product?.basePrice ?? null,
            maxPrice: null,
            purchaseSource: null,
            saleVisibility: 'COMPANY',
            budget: null,
            mediaHouseBuildingId: null,
            minQuality: null,
            brandScope: null,
            vendorLockCompanyId: null,
          },
        ]
      }
      company.buildings.push(shopBuilding)

      shopLot.ownerCompanyId = company.id
      shopLot.buildingId = shopBuilding.id
      shopLot.ownerCompany = { id: company.id, name: company.name }
      shopLot.building = { id: shopBuilding.id, name: shopBuilding.name, type: shopBuilding.type }

      player.onboardingCompletedAtUtc = new Date().toISOString()
      player.onboardingShopBuildingId = shopBuilding.id
      player.onboardingCurrentStep = null
      player.onboardingIndustry = null
      player.onboardingCityId = null
      player.onboardingCompanyId = null
      player.onboardingFactoryLotId = null

      // Derive the city currency code from the shop lot's cityId so the frontend
      // can display the correct local currency in the completion step (e.g. CZK for Prague).
      const shopCity = state.cities.find((c) => c.id === shopLot.cityId)
      const cityCurrencyCode = shopCity?.currencyCode ?? 'EUR'

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            finishOnboarding: {
              company: { id: company.id, name: company.name, cash: company.cash },
              factory: company.buildings.find((candidate) => candidate.type === 'FACTORY'),
              salesShop: shopBuilding,
              selectedProduct: product,
              cityCurrencyCode,
            },
          },
        }),
      })
    }

    if (query.includes('CompleteOnboarding')) {
      const input = body.variables?.input
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Not authenticated' }] }) })
      }
      const product = state.productTypes.find((p) => p.id === input.productTypeId)
      const productId = product?.id ?? ''
      const resourceTypeId = product?.recipes[0]?.resourceType?.id ?? null
      const factoryBuildingId = `building-factory-${Date.now()}`
      const shopBuildingId = `building-shop-${Date.now() + 1}`
      const company: MockCompany = {
        id: `company-${Date.now()}`,
        playerId: player.id,
        name: input.companyName,
        cash: 500000,
        totalSharesIssued: DEFAULT_COMPANY_SHARE_COUNT,
        dividendPayoutRatio: DEFAULT_DIVIDEND_PAYOUT_RATIO,
        foundedAtUtc: new Date().toISOString(),
        foundedAtTick: state.gameState.currentTick,
        buildings: [
          {
            id: factoryBuildingId,
            companyId: '',
            cityId: input.cityId,
            type: 'FACTORY',
            name: `${input.companyName} Factory`,
            latitude: 48.15,
            longitude: 17.11,
            level: 1,
            powerConsumption: 2,
            powerStatus: 'POWERED',
            isForSale: false,
            builtAtUtc: new Date().toISOString(),
            units: [
              {
                id: `unit-factory-purchase-${Date.now()}`,
                buildingId: factoryBuildingId,
                unitType: 'PURCHASE',
                gridX: 0,
                gridY: 0,
                level: 1,
                linkRight: true,
                linkLeft: false,
                linkUp: false,
                linkDown: false,
                linkUpLeft: false,
                linkUpRight: false,
                linkDownLeft: false,
                linkDownRight: false,
                productTypeId: null,
                resourceTypeId,
                minPrice: null,
                maxPrice: null,
                purchaseSource: 'OPTIMAL',
                saleVisibility: null,
                budget: null,
                mediaHouseBuildingId: null,
                minQuality: null,
                brandScope: null,
                vendorLockCompanyId: null,
              },
              {
                id: `unit-factory-manufacturing-${Date.now() + 1}`,
                buildingId: factoryBuildingId,
                unitType: 'MANUFACTURING',
                gridX: 1,
                gridY: 0,
                level: 1,
                linkRight: true,
                linkLeft: false,
                linkUp: false,
                linkDown: false,
                linkUpLeft: false,
                linkUpRight: false,
                linkDownLeft: false,
                linkDownRight: false,
                productTypeId: productId,
                resourceTypeId: null,
                minPrice: null,
                maxPrice: null,
                purchaseSource: null,
                saleVisibility: null,
                budget: null,
                mediaHouseBuildingId: null,
                minQuality: null,
                brandScope: null,
                vendorLockCompanyId: null,
              },
              {
                id: `unit-factory-storage-${Date.now() + 2}`,
                buildingId: factoryBuildingId,
                unitType: 'STORAGE',
                gridX: 2,
                gridY: 0,
                level: 1,
                linkRight: true,
                linkLeft: false,
                linkUp: false,
                linkDown: false,
                linkUpLeft: false,
                linkUpRight: false,
                linkDownLeft: false,
                linkDownRight: false,
                productTypeId: null,
                resourceTypeId: null,
                minPrice: null,
                maxPrice: null,
                purchaseSource: null,
                saleVisibility: null,
                budget: null,
                mediaHouseBuildingId: null,
                minQuality: null,
                brandScope: null,
                vendorLockCompanyId: null,
              },
              {
                id: `unit-factory-b2bsales-${Date.now() + 3}`,
                buildingId: factoryBuildingId,
                unitType: 'B2B_SALES',
                gridX: 3,
                gridY: 0,
                level: 1,
                linkRight: false,
                linkLeft: false,
                linkUp: false,
                linkDown: false,
                linkUpLeft: false,
                linkUpRight: false,
                linkDownLeft: false,
                linkDownRight: false,
                productTypeId: productId,
                resourceTypeId: null,
                minPrice: product?.basePrice ?? null,
                maxPrice: null,
                purchaseSource: null,
                saleVisibility: 'COMPANY',
                budget: null,
                mediaHouseBuildingId: null,
                minQuality: null,
                brandScope: null,
                vendorLockCompanyId: null,
              },
            ],
            pendingConfiguration: null,
          },
          {
            id: shopBuildingId,
            companyId: '',
            cityId: input.cityId,
            type: 'SALES_SHOP',
            name: `${input.companyName} Shop`,
            latitude: 48.15,
            longitude: 17.11,
            level: 1,
            powerConsumption: 1,
            powerStatus: 'POWERED',
            isForSale: false,
            builtAtUtc: new Date().toISOString(),
            units: [
              {
                id: `unit-shop-purchase-${Date.now() + 4}`,
                buildingId: shopBuildingId,
                unitType: 'PURCHASE',
                gridX: 0,
                gridY: 0,
                level: 1,
                linkRight: true,
                linkLeft: false,
                linkUp: false,
                linkDown: false,
                linkUpLeft: false,
                linkUpRight: false,
                linkDownLeft: false,
                linkDownRight: false,
                productTypeId: productId,
                resourceTypeId: null,
                minPrice: null,
                maxPrice: null,
                purchaseSource: 'LOCAL',
                saleVisibility: null,
                budget: null,
                mediaHouseBuildingId: null,
                minQuality: null,
                brandScope: null,
                vendorLockCompanyId: null,
              },
              {
                id: `unit-shop-publicsales-${Date.now() + 5}`,
                buildingId: shopBuildingId,
                unitType: 'PUBLIC_SALES',
                gridX: 1,
                gridY: 0,
                level: 1,
                linkRight: false,
                linkLeft: false,
                linkUp: false,
                linkDown: false,
                linkUpLeft: false,
                linkUpRight: false,
                linkDownLeft: false,
                linkDownRight: false,
                productTypeId: productId,
                resourceTypeId: null,
                minPrice: product?.basePrice != null ? product.basePrice * 1.5 : null,
                maxPrice: null,
                purchaseSource: null,
                saleVisibility: null,
                budget: null,
                mediaHouseBuildingId: null,
                minQuality: null,
                brandScope: null,
                vendorLockCompanyId: null,
              },
            ],
            pendingConfiguration: null,
          },
        ],
      }
      player.companies.push(company)
      player.activeAccountType = 'COMPANY'
      player.activeCompanyId = company.id
      state.shareholdings.push({
        companyId: company.id,
        ownerPlayerId: player.id,
        ownerCompanyId: null,
        shareCount: DEFAULT_COMPANY_SHARE_COUNT,
      })
      player.onboardingCompletedAtUtc = new Date().toISOString()
      player.onboardingShopBuildingId = company.buildings[1].id
      player.onboardingCurrentStep = null
      player.onboardingIndustry = null
      player.onboardingCityId = null
      player.onboardingCompanyId = null
      player.onboardingFactoryLotId = null
      const completeCity = state.cities.find((c) => c.id === input.cityId)
      const completeCityCurrencyCode = completeCity?.currencyCode ?? 'EUR'
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            completeOnboarding: {
              company: { id: company.id, name: company.name, cash: company.cash },
              factory: company.buildings[0],
              salesShop: company.buildings[1],
              selectedProduct: product ?? state.productTypes[0],
              cityCurrencyCode: completeCityCurrencyCode,
            },
          },
        }),
      })
    }

    if (query.includes('CreateCompany') && !query.includes('CreateCompanyBankAccount') && !query.includes('createCompanyBankAccount')) {
      const input = body.variables?.input
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Not authenticated' }] }) })
      }
      const company: MockCompany = {
        id: `company-${Date.now()}`,
        playerId: player.id,
        name: input.name,
        cash: 1000000,
        totalSharesIssued: DEFAULT_COMPANY_SHARE_COUNT,
        dividendPayoutRatio: DEFAULT_DIVIDEND_PAYOUT_RATIO,
        foundedAtUtc: new Date().toISOString(),
        foundedAtTick: state.gameState.currentTick,
        buildings: [],
      }
      player.companies.push(company)
      player.activeAccountType = 'COMPANY'
      player.activeCompanyId = company.id
      state.shareholdings.push({
        companyId: company.id,
        ownerPlayerId: player.id,
        ownerCompanyId: null,
        shareCount: DEFAULT_COMPANY_SHARE_COUNT,
      })
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { createCompany: company } }),
      })
    }

    if (query.includes('UpdateCompanySettings')) {
      const input = body.variables?.input
      const player = state.players.find((candidate) => candidate.id === state.currentUserId)
      const company = player?.companies.find((candidate) => candidate.id === input?.companyId)

      if (!company) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Company not found or you do not own it.' }] }) })
      }

      company.name = input.name
      company.dividendPayoutRatio = Number(input.dividendPayoutRatio ?? company.dividendPayoutRatio ?? DEFAULT_DIVIDEND_PAYOUT_RATIO)
      company.citySalaryMultipliers = Object.fromEntries((input.citySalarySettings ?? []).map((entry: { cityId: string; salaryMultiplier: number }) => [entry.cityId, entry.salaryMultiplier]))

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { updateCompanySettings: { id: company.id, name: company.name, dividendPayoutRatio: getCompanyDividendPayoutRatio(company) } } }),
      })
    }

    if (query.includes('SwitchAccountContext')) {
      const input = body.variables?.input
      const player = state.players.find((candidate) => candidate.id === state.currentUserId)

      if (!player) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Not authenticated' }] }) })
      }

      if (input?.accountType === 'PERSON') {
        player.activeAccountType = 'PERSON'
        player.activeCompanyId = null
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ data: { switchAccountContext: { activeAccountType: 'PERSON', activeCompanyId: null, activeAccountName: player.displayName } } }),
        })
      }

      const targetCompany = state.players.flatMap((candidate) => candidate.companies).find((candidate) => candidate.id === input?.companyId)
      if (!targetCompany) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Company not found.' }] }) })
      }

      const controlledRatio = getCombinedControlledOwnershipRatio(state, player.id, targetCompany)
      if (targetCompany.playerId !== player.id && controlledRatio < 0.5) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'You need at least 50% combined ownership to switch into this company.', extensions: { code: 'COMPANY_CONTROL_REQUIRED' } }] }),
        })
      }

      targetCompany.playerId = player.id
      player.activeAccountType = 'COMPANY'
      player.activeCompanyId = targetCompany.id

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { switchAccountContext: { activeAccountType: 'COMPANY', activeCompanyId: targetCompany.id, activeAccountName: targetCompany.name } } }),
      })
    }

    if (query.includes('ReplaceCEO')) {
      const input = body.variables?.input
      const player = state.players.find((candidate) => candidate.id === state.currentUserId)

      if (!player) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Not authenticated' }] }) })
      }

      const targetCompany = state.players.flatMap((candidate) => candidate.companies).find((candidate) => candidate.id === input?.companyId)
      if (!targetCompany) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Company not found.', extensions: { code: 'COMPANY_NOT_FOUND' } }] }) })
      }

      if (input?.newCeoPlayerId !== player.id) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'You can only appoint yourself as the new CEO when taking control.', extensions: { code: 'INVALID_NEW_CEO_PLAYER' } }] }),
        })
      }

      const controlledRatio = getCombinedControlledOwnershipRatio(state, player.id, targetCompany)
      if (targetCompany.playerId !== player.id && controlledRatio < 0.5) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'You need at least 50% combined ownership through your person account and controlled companies to take control of this company.', extensions: { code: 'COMPANY_CONTROL_REQUIRED' } }] }),
        })
      }

      targetCompany.playerId = player.id
      player.activeAccountType = 'COMPANY'
      player.activeCompanyId = targetCompany.id

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            replaceCEO: {
              companyId: targetCompany.id,
              companyName: targetCompany.name,
              newCeoPlayerId: player.id,
              newCeoDisplayName: player.displayName,
            },
          },
        }),
      })
    }

    if (query.includes('BuyShares')) {
      const input = body.variables?.input
      const player = state.players.find((candidate) => candidate.id === state.currentUserId)
      const company = state.players.flatMap((candidate) => candidate.companies).find((candidate) => candidate.id === input?.companyId)

      if (!player || !company) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Company not found or not authenticated.' }] }) })
      }
      if (isGovernmentCompany(state, company)) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Government company shares cannot be traded on the stock exchange.', extensions: { code: 'GOVERNMENT_SHARES_NOT_TRADEABLE' } }] }),
        })
      }

      const shareCount = Number(input?.shareCount ?? 0)
      const publicFloatShares = getPublicFloatShares(state, company)
      const pricePerShare = Number((computeMockSharePrice(company) * 1.01).toFixed(2))
      const totalValue = Number((shareCount * pricePerShare).toFixed(2))

      if (shareCount <= 0) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Share count must be greater than zero.' }] }) })
      }

      if (publicFloatShares < shareCount) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Not enough public float shares are available.', extensions: { code: 'INSUFFICIENT_PUBLIC_FLOAT' } }] }),
        })
      }

      // Resolve trading account: prefer explicit tradeAccountType/tradeAccountCompanyId over active account
      const tradeAccountType: string = input?.tradeAccountType ?? player.activeAccountType
      const tradeAccountCompanyId: string | null = input?.tradeAccountCompanyId ?? player.activeCompanyId ?? null

      let accountName = player.displayName
      let accountCompanyId: string | null = null
      let ownedShareCount = 0
      let companyCash: number | null = null

      if (tradeAccountType === 'COMPANY' && tradeAccountCompanyId) {
        const activeCompany = player.companies.find((candidate) => candidate.id === tradeAccountCompanyId)
        if (!activeCompany || activeCompany.cash < totalValue) {
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ errors: [{ message: 'Not enough company cash.', extensions: { code: 'INSUFFICIENT_FUNDS' } }] }),
          })
        }

        activeCompany.cash = Number((activeCompany.cash - totalValue).toFixed(2))
        accountName = activeCompany.name
        accountCompanyId = activeCompany.id
        companyCash = activeCompany.cash
        recordMockCompanyStockLedgerEntry(state, activeCompany, 'STOCK_PURCHASE', `Bought ${shareCount} shares in ${company.name} @ ${pricePerShare.toFixed(2)}`, -totalValue)

        if (activeCompany.id === company.id) {
          company.totalSharesIssued = Number(Math.max(getCompanyTotalShares(company) - shareCount, 0).toFixed(4))
          ownedShareCount = getCompanyTotalShares(company)
        } else {
          const holding = getOrCreateShareholding(state, company.id, null, activeCompany.id)
          holding.shareCount = Number((holding.shareCount + shareCount).toFixed(4))
          ownedShareCount = holding.shareCount
        }
      } else {
        const availableCash = computeAvailableCash(player)
        if (availableCash < totalValue) {
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ errors: [{ message: 'Not enough personal cash.', extensions: { code: 'INSUFFICIENT_FUNDS' } }] }),
          })
        }

        player.personalCash = Number((player.personalCash - totalValue).toFixed(2))
        const holding = getOrCreateShareholding(state, company.id, player.id, null)
        holding.shareCount = Number((holding.shareCount + shareCount).toFixed(4))
        ownedShareCount = holding.shareCount
        player.stockTrades.unshift({
          id: `trade-buy-${Math.random().toString(36).slice(2)}`,
          companyId: company.id,
          companyName: company.name,
          direction: 'BUY',
          shareCount,
          pricePerShare,
          totalValue,
          recordedAtTick: state.gameState.currentTick,
          recordedAtUtc: '2026-01-10T12:00:00Z',
        })
      }

      appendMockStockPriceHistory(state, company.id, pricePerShare)

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            buyShares: {
              companyId: company.id,
              companyName: company.name,
              accountType: tradeAccountType,
              accountCompanyId,
              accountName,
              shareCount,
              pricePerShare,
              totalValue,
              taxReserved: 0,
              ownedShareCount,
              publicFloatShares: getPublicFloatShares(state, company),
              personalCash: player.personalCash,
              personalTaxReserve: player.personalTaxReserve ?? 0,
              companyCash,
            },
          },
        }),
      })
    }

    if (query.includes('SellShares')) {
      const input = body.variables?.input
      const player = state.players.find((candidate) => candidate.id === state.currentUserId)
      const company = state.players.flatMap((candidate) => candidate.companies).find((candidate) => candidate.id === input?.companyId)

      if (!player || !company) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Company not found or not authenticated.' }] }) })
      }
      if (isGovernmentCompany(state, company)) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Government company shares cannot be traded on the stock exchange.', extensions: { code: 'GOVERNMENT_SHARES_NOT_TRADEABLE' } }] }),
        })
      }

      const shareCount = Number(input?.shareCount ?? 0)
      const pricePerShare = Number((computeMockSharePrice(company) * 0.99).toFixed(2))
      const totalValue = Number((shareCount * pricePerShare).toFixed(2))

      if (shareCount <= 0) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Share count must be greater than zero.' }] }) })
      }

      // Resolve trading account: prefer explicit tradeAccountType/tradeAccountCompanyId over active account
      const tradeAccountType: string = input?.tradeAccountType ?? player.activeAccountType
      const tradeAccountCompanyId: string | null = input?.tradeAccountCompanyId ?? player.activeCompanyId ?? null

      let accountName = player.displayName
      let accountCompanyId: string | null = null
      let ownedShareCount = 0
      let companyCash: number | null = null

      if (tradeAccountType === 'COMPANY' && tradeAccountCompanyId) {
        const activeCompany = player.companies.find((candidate) => candidate.id === tradeAccountCompanyId)
        const holding = activeCompany ? getOrCreateShareholding(state, company.id, null, activeCompany.id) : null

        if (!activeCompany || !holding || holding.shareCount < shareCount) {
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ errors: [{ message: 'Not enough shares to sell.', extensions: { code: 'INSUFFICIENT_SHARES' } }] }),
          })
        }

        holding.shareCount = Number((holding.shareCount - shareCount).toFixed(4))
        activeCompany.cash = Number((activeCompany.cash + totalValue).toFixed(2))
        accountName = activeCompany.name
        accountCompanyId = activeCompany.id
        companyCash = activeCompany.cash
        ownedShareCount = holding.shareCount
        recordMockCompanyStockLedgerEntry(state, activeCompany, 'STOCK_SALE', `Sold ${shareCount} shares in ${company.name} @ ${pricePerShare.toFixed(2)}`, totalValue)
      } else {
        const holding = getOrCreateShareholding(state, company.id, player.id, null)
        if (holding.shareCount < shareCount) {
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ errors: [{ message: 'Not enough shares to sell.', extensions: { code: 'INSUFFICIENT_SHARES' } }] }),
          })
        }

        holding.shareCount = Number((holding.shareCount - shareCount).toFixed(4))
        player.personalCash = Number((player.personalCash + totalValue).toFixed(2))
        const taxAmount = Number((totalValue * 0.15).toFixed(4))
        player.personalTaxReserve = Number(((player.personalTaxReserve ?? 0) + taxAmount).toFixed(4))
        ownedShareCount = holding.shareCount
        player.stockTrades.unshift({
          id: `trade-sell-${Math.random().toString(36).slice(2)}`,
          companyId: company.id,
          companyName: company.name,
          direction: 'SELL',
          shareCount,
          pricePerShare,
          totalValue,
          recordedAtTick: state.gameState.currentTick,
          recordedAtUtc: '2026-01-10T12:00:00Z',
        })
      }

      appendMockStockPriceHistory(state, company.id, pricePerShare)

      const isSellFromPerson = tradeAccountType === 'PERSON'
      const taxReserved = isSellFromPerson ? Number((totalValue * 0.15).toFixed(4)) : 0

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            sellShares: {
              companyId: company.id,
              companyName: company.name,
              accountType: tradeAccountType,
              accountCompanyId,
              accountName,
              shareCount,
              pricePerShare,
              totalValue,
              taxReserved,
              ownedShareCount,
              publicFloatShares: getPublicFloatShares(state, company),
              personalCash: player.personalCash,
              personalTaxReserve: player.personalTaxReserve ?? 0,
              companyCash,
            },
          },
        }),
      })
    }

    if (query.includes('proposeDividend')) {
      const input = body.variables?.input
      const player = state.players.find((candidate) => candidate.id === state.currentUserId)
      const companyId = String(input?.companyId ?? '')
      const stockSymbol = companyId ? stockSymbolForCompany(companyId) : String(input?.stockSymbol ?? '')
      const company =
        state.players.flatMap((candidate) => candidate.companies).find((candidate) => candidate.id === companyId) ??
        state.players.flatMap((candidate) => candidate.companies).find((candidate) => stockSymbolForCompany(candidate.id) === stockSymbol)

      if (!player || !company) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Company not found or not authenticated.' }] }) })
      }

      if (input?.dividendPercent != null) {
        const policyDividendPercent = Number(input.dividendPercent)
        if (company.playerId !== player.id) {
          return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ extensions: { code: 'NOT_CEO' }, message: 'Only the acting CEO can propose dividend changes.' }] }) })
        }

        const currentTick = state.gameState.currentTick
        const hasOpenPolicy = state.dividendProposals.some(
          (candidate) =>
            candidate.companyId === company.id &&
            candidate.status === 'VOTING' &&
            candidate.totalPayout <= 0 &&
            candidate.votingCloseTick >= currentTick,
        )
        if (hasOpenPolicy) {
          return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ extensions: { code: 'PROPOSAL_ALREADY_PENDING' }, message: 'There is already a pending dividend proposal.' }] }) })
        }

        const proposalId = `proposal-${Math.random().toString(36).slice(2)}`
        state.dividendProposals.unshift({
          id: proposalId,
          companyId: company.id,
          stockSymbol: stockSymbolForCompany(company.id),
          proposedByAccountId: player.id,
          proposedByAccountType: 'PERSON',
          dividendPerShare: Number((Math.max(0, Math.min(policyDividendPercent, 100)) / 100).toFixed(4)),
          totalPayout: 0,
          status: 'VOTING',
          outcome: 'PENDING',
          proposedAtTick: currentTick,
          votingOpenTick: currentTick,
          votingCloseTick: currentTick + 120,
          settledAtTick: null,
        })

        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            data: {
              proposeDividend: {
                id: proposalId,
                companyId: company.id,
                stockSymbol: stockSymbolForCompany(company.id),
                status: 'VOTING',
                ticksRemaining: 120,
              },
            },
          }),
        })
      }

      const dividendPerShare = Number(input?.dividendPerShare ?? 0)
      if (dividendPerShare <= 0) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Dividend per share must be greater than zero.' }] }) })
      }

      const currentTick = state.gameState.currentTick
      const totalPayout = Number((dividendPerShare * getCompanyTotalShares(company)).toFixed(4))
      const combinedOwnership = getCombinedControlledOwnershipRatio(state, player.id, company)
      const canPropose = company.playerId === player.id || combinedOwnership > 0.5
      if (!canPropose) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Only majority shareholders can propose dividends.' }] }) })
      }

      if ((company.cash ?? 0) < totalPayout) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Company does not have enough cash.' }] }) })
      }

      company.cash = Number((company.cash - totalPayout).toFixed(4))
      const proposalId = `proposal-${Math.random().toString(36).slice(2)}`
      state.dividendProposals.unshift({
        id: proposalId,
        companyId: company.id,
        stockSymbol,
        proposedByAccountId: player.activeAccountType === 'COMPANY' && player.activeCompanyId ? player.activeCompanyId : player.id,
        proposedByAccountType: player.activeAccountType,
        dividendPerShare,
        totalPayout,
        status: 'VOTING',
        outcome: 'PENDING',
        proposedAtTick: currentTick,
        votingOpenTick: currentTick,
        votingCloseTick: currentTick + 10,
        settledAtTick: null,
      })

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            proposeDividend: {
              id: proposalId,
              companyId: company.id,
              stockSymbol,
              status: 'VOTING',
              votingCloseTick: currentTick + 10,
            },
          },
        }),
      })
    }

    if (query.includes('voteDividend(')) {
      const input = body.variables?.input
      const player = state.players.find((candidate) => candidate.id === state.currentUserId)
      const companyId = String(input?.companyId ?? '')
      const proposal = state.dividendProposals.find(
        (candidate) => candidate.companyId === companyId && candidate.status === 'VOTING' && candidate.totalPayout <= 0,
      )
      if (!player || !proposal) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'No pending dividend proposal found.' }] }) })
      }

      const existingVote = state.dividendVotes.find(
        (candidate) => candidate.proposalId === proposal.id && candidate.voterAccountId === player.id && candidate.voterAccountType === 'PERSON',
      )
      if (existingVote) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ extensions: { code: 'ALREADY_VOTED' }, message: 'You have already voted on this proposal.' }] }) })
      }

      const sharesVoted = state.shareholdings
        .filter((holding) => holding.companyId === companyId && holding.ownerPlayerId === player.id && holding.shareCount > 0)
        .reduce((sum, holding) => sum + holding.shareCount, 0)
      if (sharesVoted <= 0) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Only shareholders can vote.' }] }) })
      }

      const voteChoice = String(input?.vote ?? '').toUpperCase() === 'REJECT' ? 'AGAINST' : 'FOR'
      state.dividendVotes.push({
        id: `vote-${Math.random().toString(36).slice(2)}`,
        proposalId: proposal.id,
        voterAccountId: player.id,
        voterAccountType: 'PERSON',
        sharesVoted,
        voteChoice,
        castAtTick: state.gameState.currentTick,
      })

      const company = state.players.flatMap((candidate) => candidate.companies).find((candidate) => candidate.id === companyId)
      const allVotes = state.dividendVotes.filter((candidate) => candidate.proposalId === proposal.id)
      const forVotes = allVotes.filter((candidate) => candidate.voteChoice === 'FOR').reduce((sum, candidate) => sum + candidate.sharesVoted, 0)
      const againstVotes = allVotes.filter((candidate) => candidate.voteChoice === 'AGAINST').reduce((sum, candidate) => sum + candidate.sharesVoted, 0)
      const totalShares = company ? getCompanyTotalShares(company) : 0
      if (forVotes > totalShares / 2) {
        proposal.status = 'SETTLED'
        proposal.settledAtTick = state.gameState.currentTick
        if (company) {
          company.dividendPayoutRatio = proposal.dividendPerShare
        }
      } else if (againstVotes > totalShares / 2) {
        proposal.status = 'REJECTED'
        proposal.settledAtTick = state.gameState.currentTick
      }

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            voteDividend: {
              id: proposal.id,
              status: proposal.status,
              forVotes,
              againstVotes,
              myVoteChoice: voteChoice,
            },
          },
        }),
      })
    }

    if (query.includes('voteDividendProposal')) {
      const input = body.variables?.input
      const player = state.players.find((candidate) => candidate.id === state.currentUserId)
      const proposal = state.dividendProposals.find((candidate) => candidate.id === input?.proposalId)
      if (!player || !proposal) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Proposal not found or not authenticated.' }] }) })
      }

      const currentTick = state.gameState.currentTick
      if (proposal.status !== 'VOTING' || currentTick > proposal.votingCloseTick) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Voting is closed.' }] }) })
      }

      const voterAccountId = player.activeAccountType === 'COMPANY' && player.activeCompanyId ? player.activeCompanyId : player.id
      const voterAccountType = player.activeAccountType
      const existingVote = state.dividendVotes.find((candidate) => candidate.proposalId === proposal.id && candidate.voterAccountId === voterAccountId)
      if (existingVote) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Already voted.' }] }) })
      }

      const sharesVoted = state.shareholdings
        .filter(
          (holding) =>
            holding.companyId === proposal.companyId &&
            holding.shareCount > 0 &&
            (voterAccountType === 'COMPANY' ? holding.ownerCompanyId === voterAccountId : holding.ownerPlayerId === voterAccountId),
        )
        .reduce((sum, holding) => sum + holding.shareCount, 0)
      if (sharesVoted <= 0) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Only shareholders can vote.' }] }) })
      }

      const voteId = `vote-${Math.random().toString(36).slice(2)}`
      const voteChoice = String(input?.choice ?? '').toUpperCase() === 'AGAINST' ? 'AGAINST' : 'FOR'
      state.dividendVotes.push({
        id: voteId,
        proposalId: proposal.id,
        voterAccountId,
        voterAccountType,
        sharesVoted,
        voteChoice,
        castAtTick: currentTick,
      })

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            voteDividendProposal: {
              id: voteId,
              proposalId: proposal.id,
              voteChoice,
              sharesVoted,
              castAtTick: currentTick,
            },
          },
        }),
      })
    }

    if (query.includes('placeLimitOrder')) {
      const input = body.variables?.input
      const player = state.players.find((candidate) => candidate.id === state.currentUserId)
      if (!player) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Not authenticated.' }] }) })
      }

      const symbol = String(input?.stockSymbol ?? '')
      const company = state.players.flatMap((candidate) => candidate.companies).find((candidate) => stockSymbolForCompany(candidate.id) === symbol)
      if (!company) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Company not found.' }] }) })
      }

      const rawSide = String(input?.side ?? '').toUpperCase()
      if (rawSide !== 'BUY' && rawSide !== 'SELL') {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Invalid order side.' }] }) })
      }
      const side: 'BUY' | 'SELL' = rawSide
      const limitPrice = Number(input?.limitPrice ?? 0)
      const quantity = Math.max(0, Math.floor(Number(input?.quantity ?? 0)))
      if (quantity <= 0 || limitPrice <= 0) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Invalid order input.' }] }) })
      }

      const ownerCompanyId = player.activeAccountType === 'COMPANY' ? player.activeCompanyId : null
      const ownerPlayerId = ownerCompanyId ? null : player.id
      const ownerCompany = ownerCompanyId ? player.companies.find((candidate) => candidate.id === ownerCompanyId) : null
      const reserve = Number((limitPrice * quantity).toFixed(4))

      if (side === 'BUY') {
        if (ownerCompany) {
          if ((ownerCompany.cash ?? 0) < reserve) {
            return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Insufficient funds.' }] }) })
          }
          ownerCompany.cash = Number(((ownerCompany.cash ?? 0) - reserve).toFixed(4))
        } else if (computeAvailableCash(player) < reserve) {
          return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Insufficient funds.' }] }) })
        } else {
          player.personalCash = Number((player.personalCash - reserve).toFixed(4))
        }
      } else {
        const reservedShares = state.stockLimitOrders
          .filter(
            (order) =>
              order.companyId === company.id &&
              order.side === 'SELL' &&
              order.status !== 'CANCELLED' &&
              order.status !== 'FILLED' &&
              order.ownerPlayerId === ownerPlayerId &&
              order.ownerCompanyId === ownerCompanyId,
          )
          .reduce((sum, order) => sum + (order.quantity - order.filledQuantity), 0)
        const holding = getOrCreateShareholding(state, company.id, ownerPlayerId, ownerCompanyId)
        if ((holding.shareCount ?? 0) < quantity + reservedShares) {
          return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Insufficient shares.' }] }) })
        }
      }

      const order: MockLimitOrder = {
        id: `limit-${Math.random().toString(36).slice(2)}`,
        companyId: company.id,
        stockSymbol: stockSymbolForCompany(company.id),
        side,
        limitPrice: Number(limitPrice.toFixed(4)),
        quantity,
        filledQuantity: 0,
        status: 'OPEN',
        ownerPlayerId,
        ownerCompanyId,
        createdAtTick: state.gameState.currentTick,
        updatedAtTick: state.gameState.currentTick,
      }
      state.stockLimitOrders.push(order)

      const buyOrders = state.stockLimitOrders
        .filter((candidate) => candidate.companyId === company.id && candidate.side === 'BUY' && (candidate.status === 'OPEN' || candidate.status === 'PARTIALLY_FILLED'))
        .sort((left, right) => right.limitPrice - left.limitPrice || left.createdAtTick - right.createdAtTick)
      const sellOrders = state.stockLimitOrders
        .filter((candidate) => candidate.companyId === company.id && candidate.side === 'SELL' && (candidate.status === 'OPEN' || candidate.status === 'PARTIALLY_FILLED'))
        .sort((left, right) => left.limitPrice - right.limitPrice || left.createdAtTick - right.createdAtTick)

      let buyIdx = 0
      let sellIdx = 0
      while (buyIdx < buyOrders.length && sellIdx < sellOrders.length) {
        const buy = buyOrders[buyIdx]
        const sell = sellOrders[sellIdx]
        if (buy.limitPrice < sell.limitPrice) break
        const qty = Math.min(buy.quantity - buy.filledQuantity, sell.quantity - sell.filledQuantity)
        if (qty <= 0) break
        const tradePrice = sell.limitPrice
        const tradeValue = Number((tradePrice * qty).toFixed(4))

        const sellHolding = getOrCreateShareholding(state, company.id, sell.ownerPlayerId, sell.ownerCompanyId)
        sellHolding.shareCount = Number(Math.max(0, (sellHolding.shareCount ?? 0) - qty).toFixed(4))
        const buyHolding = getOrCreateShareholding(state, company.id, buy.ownerPlayerId, buy.ownerCompanyId)
        buyHolding.shareCount = Number(((buyHolding.shareCount ?? 0) + qty).toFixed(4))

        if (sell.ownerCompanyId) {
          const sellerCompany = state.players.flatMap((candidate) => candidate.companies).find((candidate) => candidate.id === sell.ownerCompanyId)
          if (sellerCompany) sellerCompany.cash = Number(((sellerCompany.cash ?? 0) + tradeValue).toFixed(4))
        } else if (sell.ownerPlayerId) {
          const seller = state.players.find((candidate) => candidate.id === sell.ownerPlayerId)
          if (seller) seller.personalCash = Number((seller.personalCash + tradeValue).toFixed(4))
        }

        if (buy.ownerCompanyId) {
          const buyerCompany = state.players.flatMap((candidate) => candidate.companies).find((candidate) => candidate.id === buy.ownerCompanyId)
          if (buyerCompany && buy.limitPrice > tradePrice) {
            buyerCompany.cash = Number(((buyerCompany.cash ?? 0) + (buy.limitPrice - tradePrice) * qty).toFixed(4))
          }
        } else if (buy.ownerPlayerId && buy.limitPrice > tradePrice) {
          const buyer = state.players.find((candidate) => candidate.id === buy.ownerPlayerId)
          if (buyer) buyer.personalCash = Number((buyer.personalCash + (buy.limitPrice - tradePrice) * qty).toFixed(4))
        }

        buy.filledQuantity += qty
        sell.filledQuantity += qty
        buy.status = buy.filledQuantity >= buy.quantity ? 'FILLED' : 'PARTIALLY_FILLED'
        sell.status = sell.filledQuantity >= sell.quantity ? 'FILLED' : 'PARTIALLY_FILLED'
        buy.updatedAtTick = state.gameState.currentTick
        sell.updatedAtTick = state.gameState.currentTick

        state.stockLimitOrderExecutions.unshift({
          id: `limit-trade-${Math.random().toString(36).slice(2)}`,
          companyId: company.id,
          stockSymbol: stockSymbolForCompany(company.id),
          price: tradePrice,
          quantity: qty,
          executedAtTick: state.gameState.currentTick,
          executedAtUtc: new Date().toISOString(),
        })
        appendMockStockPriceHistory(state, company.id, tradePrice)

        if (buy.status === 'FILLED') buyIdx += 1
        if (sell.status === 'FILLED') sellIdx += 1
      }

      const result = {
        id: order.id,
        companyId: order.companyId,
        companyName: company.name,
        stockSymbol: order.stockSymbol,
        side: order.side,
        limitPrice: order.limitPrice,
        quantity: order.quantity,
        filledQuantity: order.filledQuantity,
        remainingQuantity: Math.max(0, order.quantity - order.filledQuantity),
        status: order.status,
        createdAtTick: order.createdAtTick,
        updatedAtTick: order.updatedAtTick,
      }
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: { placeLimitOrder: result } }) })
    }

    if (query.includes('cancelLimitOrder')) {
      const orderId = body.variables?.orderId
      const player = state.players.find((candidate) => candidate.id === state.currentUserId)
      const order = state.stockLimitOrders.find((candidate) => candidate.id === orderId)
      if (!player || !order) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Order not found.' }] }) })
      }
      const playerCompanyIds = new Set(player.companies.map((company) => company.id))
      if (!(order.ownerPlayerId === player.id || (order.ownerCompanyId && playerCompanyIds.has(order.ownerCompanyId)))) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Order not owned.' }] }) })
      }
      if (order.status === 'FILLED' || order.status === 'CANCELLED') {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Order cannot be cancelled.' }] }) })
      }

      const remaining = Math.max(0, order.quantity - order.filledQuantity)
      if (order.side === 'BUY' && remaining > 0) {
        const refund = Number((remaining * order.limitPrice).toFixed(4))
        if (order.ownerCompanyId) {
          const company = state.players.flatMap((candidate) => candidate.companies).find((candidate) => candidate.id === order.ownerCompanyId)
          if (company) company.cash = Number(((company.cash ?? 0) + refund).toFixed(4))
        } else {
          player.personalCash = Number((player.personalCash + refund).toFixed(4))
        }
      }
      order.status = 'CANCELLED'
      order.updatedAtTick = state.gameState.currentTick
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: { cancelLimitOrder: { id: order.id, status: order.status, remainingQuantity: remaining } } }) })
    }

    if (query.includes('mergeCompany')) {
      const input = body.variables?.input
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Not authenticated', extensions: { code: 'UNAUTHORIZED' } }] }),
        })
      }
      // Find target company across all players
      const targetCompany = state.players.flatMap((p) => p.companies).find((c) => c.id === input.targetCompanyId)
      const destCompany = player.companies.find((c) => c.id === input.destinationCompanyId)
      if (!targetCompany || !destCompany) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Company not found', extensions: { code: 'COMPANY_NOT_FOUND' } }] }),
        })
      }
      const cashTransferred = targetCompany.cash ?? 0
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            mergeCompany: {
              destinationCompanyId: destCompany.id,
              destinationCompanyName: destCompany.name,
              absorbedCompanyName: targetCompany.name,
              cashTransferred,
              buildingsTransferred: targetCompany.buildings?.length ?? 0,
            },
          },
        }),
      })
    }

    if (query.includes('PlaceBuilding')) {
      const input = body.variables?.input
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Not authenticated' }] }) })
      }
      const company = player.companies.find((c) => c.id === input.companyId)
      if (!company) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Company not found' }] }) })
      }
      const city = state.cities.find((c) => c.id === input.cityId)
      const newBuilding: MockBuilding = {
        id: `building-${Date.now()}`,
        companyId: company.id,
        cityId: input.cityId,
        type: input.type,
        name: input.name,
        latitude: city?.latitude ?? 0,
        longitude: city?.longitude ?? 0,
        level: 1,
        powerConsumption: 1,
        powerStatus: 'POWERED',
        isForSale: false,
        builtAtUtc: new Date().toISOString(),
        units: [],
        pendingConfiguration: null,
      }
      company.buildings.push(newBuilding)
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { placeBuilding: newBuilding } }),
      })
    }

    if (query.includes('StoreBuildingConfiguration')) {
      applyDueBuildingUpgrades(state)

      const input = body.variables?.input
      const player = state.players.find((p) => p.id === state.currentUserId)
      const building = player?.companies.flatMap((company) => company.buildings).find((candidate) => candidate.id === input?.buildingId)

      if (!building) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              {
                message: 'Building not found',
                extensions: { code: 'BUILDING_NOT_FOUND' },
              },
            ],
          }),
        })
      }

      // Allow tests to force a CONTRADICTORY_LINK error (or any other config error) to verify
      // the frontend correctly displays backend validation errors in the save-error-banner.
      if (state.forceBuildingConfigError) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [{ message: state.forceBuildingConfigError, extensions: { code: 'CONTRADICTORY_LINK' } }],
          }),
        })
      }

      const activePlayer = state.players.find((candidate) => candidate.id === state.currentUserId)
      const hasActiveProSubscription = !!activePlayer?.proSubscriptionEndsAtUtc && new Date(activePlayer.proSubscriptionEndsAtUtc).getTime() > Date.now()

      for (const unit of input.units ?? []) {
        if (!unit.productTypeId) {
          continue
        }

        const product = state.productTypes.find((candidate) => candidate.id === unit.productTypeId)
        const isRetainingExistingProduct = [...(building.units ?? []), ...(building.pendingConfiguration?.units ?? [])].some(
          (candidate) => candidate.unitType === unit.unitType && candidate.gridX === unit.gridX && candidate.gridY === unit.gridY && (candidate.productTypeId ?? null) === unit.productTypeId,
        )

        if (product?.isProOnly && !hasActiveProSubscription && !isRetainingExistingProduct) {
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              errors: [
                {
                  message: `Pro subscription unlocks additional products to manufacture and sell. Activate Pro to use ${product.name}.`,
                  extensions: { code: 'PRO_SUBSCRIPTION_REQUIRED' },
                },
              ],
            }),
          })
        }
      }

      // Recipe compatibility validation: if a MANUFACTURING unit specifies a product,
      // and at least one PURCHASE unit has a resource explicitly configured, verify
      // that the resource satisfies the product's recipe.
      if (building.type === 'FACTORY') {
        const purchaseResourceIds = (input.units ?? []).filter((u: MockBuildingUnit) => u.unitType === 'PURCHASE' && u.resourceTypeId).map((u: MockBuildingUnit) => u.resourceTypeId!)

        const purchaseProductIds = (input.units ?? []).filter((u: MockBuildingUnit) => u.unitType === 'PURCHASE' && u.productTypeId).map((u: MockBuildingUnit) => u.productTypeId!)

        if (purchaseResourceIds.length > 0 || purchaseProductIds.length > 0) {
          for (const unit of input.units ?? []) {
            if (unit.unitType !== 'MANUFACTURING' || !unit.productTypeId) continue

            const product = state.productTypes.find((p) => p.id === unit.productTypeId)
            if (!product || product.recipes.length === 0) continue

            const anyRecipeSatisfied = product.recipes.some(
              (recipe: { resourceType: { id: string } | null; inputProductType: { id: string } | null }) =>
                (recipe.resourceType?.id && purchaseResourceIds.includes(recipe.resourceType.id)) || (recipe.inputProductType?.id && purchaseProductIds.includes(recipe.inputProductType.id)),
            )

            if (!anyRecipeSatisfied) {
              return route.fulfill({
                status: 200,
                contentType: 'application/json',
                body: JSON.stringify({
                  errors: [
                    {
                      message: `The Manufacturing unit's product '${product.name}' requires an input that no configured Purchase unit in this plan supplies. Update the Purchase unit to supply a resource or product required by this product's recipe.`,
                      extensions: { code: 'RECIPE_INPUT_MISMATCH' },
                    },
                  ],
                }),
              })
            }
          }
        }
      }

      // Zero/negative price validation: PUBLIC_SALES and B2B_SALES units must have a positive minPrice.
      // The runtime engine (PublicSalesPhase) silently replaces price <= 0 with base price, so
      // accepting 0 would misrepresent the actual selling price to the player.
      for (const unit of input.units ?? []) {
        if ((unit.unitType === 'PUBLIC_SALES' || unit.unitType === 'B2B_SALES') && unit.minPrice !== null && unit.minPrice !== undefined && unit.minPrice <= 0) {
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              errors: [
                {
                  message: 'Minimum price must be greater than zero.',
                  extensions: { code: 'INVALID_MIN_PRICE' },
                },
              ],
            }),
          })
        }
      }

      const currentUnits = new Map(building.units.map((unit) => [`${unit.gridX},${unit.gridY}`, unit]))
      const desiredUnits = new Map(
        (input.units ?? []).map((unit: MockBuildingUnit, index: number) => {
          const current = building.units.find((candidate) => candidate.gridX === unit.gridX && candidate.gridY === unit.gridY)

          return [
            `${unit.gridX},${unit.gridY}`,
            {
              id: `pending-unit-${index}-${Date.now()}`,
              buildingId: building.id,
              unitType: unit.unitType,
              gridX: unit.gridX,
              gridY: unit.gridY,
              level: current?.level ?? 1,
              linkUp: unit.linkUp,
              linkDown: unit.linkDown,
              linkLeft: unit.linkLeft,
              linkRight: unit.linkRight,
              linkUpLeft: unit.linkUpLeft,
              linkUpRight: unit.linkUpRight,
              linkDownLeft: unit.linkDownLeft,
              linkDownRight: unit.linkDownRight,
              resourceTypeId: unit.resourceTypeId ?? null,
              productTypeId: unit.productTypeId ?? null,
              minPrice: unit.minPrice ?? null,
              maxPrice: unit.maxPrice ?? null,
              purchaseSource: unit.purchaseSource ?? null,
              saleVisibility: unit.saleVisibility ?? null,
              budget: unit.budget ?? null,
              mediaHouseBuildingId: unit.mediaHouseBuildingId ?? null,
              minQuality: unit.minQuality ?? null,
              brandScope: unit.brandScope ?? null,
              vendorLockCompanyId: unit.vendorLockCompanyId ?? null,
              lockedCityId: unit.lockedCityId ?? null,
              industryCategory: unit.industryCategory ?? null,
            } satisfies MockBuildingUnit,
          ]
        }),
      )
      const existingUnits = new Map((building.pendingConfiguration?.units ?? []).map((unit) => [`${unit.gridX},${unit.gridY}`, unit]))
      const existingRemovals = new Map((building.pendingConfiguration?.removals ?? []).map((removal) => [`${removal.gridX},${removal.gridY}`, removal]))
      const allPositions = new Set<string>([...currentUnits.keys(), ...desiredUnits.keys(), ...existingUnits.keys(), ...existingRemovals.keys()])

      const nextPendingUnits: MockBuildingConfigurationPlanUnit[] = []
      const nextPendingRemovals: MockBuildingConfigurationPlanRemoval[] = []

      for (const position of allPositions) {
        const current = currentUnits.get(position)
        const desired = desiredUnits.get(position)
        const existingUnit = existingUnits.get(position)
        const existingRemoval = existingRemovals.get(position)
        const [gridX = 0, gridY = 0] = position.split(',').map(Number)

        if (desired) {
          if (existingUnit && arePendingUnitsEquivalent(existingUnit, desired)) {
            nextPendingUnits.push(
              existingUnit.appliesAtTick > state.gameState.currentTick
                ? { ...existingUnit }
                : {
                    ...existingUnit,
                    startedAtTick: state.gameState.currentTick,
                    appliesAtTick: state.gameState.currentTick,
                    ticksRequired: 0,
                    isChanged: false,
                    isReverting: false,
                  },
            )
            continue
          }

          if (existingRemoval && current && areUnitsEquivalent(current, desired)) {
            const ticksRequired = calculateCancelTicks(existingRemoval.ticksRequired)
            nextPendingUnits.push({
              ...cloneUnit(desired),
              startedAtTick: state.gameState.currentTick,
              appliesAtTick: state.gameState.currentTick + ticksRequired,
              ticksRequired,
              isChanged: true,
              isReverting: true,
            })
            continue
          }

          if (existingUnit && current && areUnitsEquivalent(current, desired)) {
            const ticksRequired = calculateCancelTicks(existingUnit.ticksRequired)
            nextPendingUnits.push({
              ...cloneUnit(desired),
              startedAtTick: state.gameState.currentTick,
              appliesAtTick: state.gameState.currentTick + ticksRequired,
              ticksRequired,
              isChanged: true,
              isReverting: true,
            })
            continue
          }

          const ticksRequired = calculateUnitTicks(current, desired)
          nextPendingUnits.push({
            ...cloneUnit(desired),
            startedAtTick: state.gameState.currentTick,
            appliesAtTick: state.gameState.currentTick + ticksRequired,
            ticksRequired,
            isChanged: !areUnitsEquivalent(current, desired),
            isReverting: false,
          })
          continue
        }

        if (existingRemoval && current) {
          nextPendingRemovals.push({ ...existingRemoval })
          continue
        }

        if (existingUnit) {
          nextPendingRemovals.push({
            id: `pending-removal-${gridX}-${gridY}-${Date.now()}`,
            gridX,
            gridY,
            startedAtTick: state.gameState.currentTick,
            appliesAtTick: state.gameState.currentTick + calculateCancelTicks(existingUnit.ticksRequired),
            ticksRequired: calculateCancelTicks(existingUnit.ticksRequired),
            isReverting: true,
          })
          continue
        }

        if (current) {
          nextPendingRemovals.push({
            id: `pending-removal-${gridX}-${gridY}-${Date.now()}`,
            gridX,
            gridY,
            startedAtTick: state.gameState.currentTick,
            appliesAtTick: state.gameState.currentTick + 3,
            ticksRequired: 3,
            isReverting: false,
          })
        }
      }

      if (!nextPendingUnits.some((unit) => unit.isChanged) && nextPendingRemovals.length === 0) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'No building configuration changes were detected.' }] }) })
      }

      const planId = building.pendingConfiguration?.id ?? `plan-${Date.now()}`

      building.pendingConfiguration = buildPlanSummary(
        {
          id: planId,
          buildingId: building.id,
          submittedAtUtc: new Date().toISOString(),
          submittedAtTick: state.gameState.currentTick,
          appliesAtTick: state.gameState.currentTick,
          totalTicksRequired: 0,
          units: nextPendingUnits.sort((left, right) => left.gridY - right.gridY || left.gridX - right.gridX),
          removals: nextPendingRemovals,
        },
        state.gameState.currentTick,
      )

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { storeBuildingConfiguration: building.pendingConfiguration } }),
      })
    }

    if (query.includes('CancelBuildingConfiguration')) {
      applyDueBuildingUpgrades(state)

      const input = body.variables?.input
      const player = state.players.find((p) => p.id === state.currentUserId)
      const building = player?.companies.flatMap((company) => company.buildings).find((candidate) => candidate.id === input?.buildingId)

      if (!building) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Building not found', extensions: { code: 'BUILDING_NOT_FOUND' } }] }) })
      }

      if (!building.pendingConfiguration) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'This building does not have a pending configuration plan to cancel.', extensions: { code: 'NO_PENDING_CONFIGURATION' } }] }),
        })
      }

      // Cancel by submitting the current active layout - creates reverting removals for pending units
      const nextRemovals: MockBuildingConfigurationPlanRemoval[] = []
      for (const pendingUnit of building.pendingConfiguration.units) {
        if (pendingUnit.isChanged) {
          const ticksRequired = Math.max(Math.ceil(pendingUnit.ticksRequired * 0.1), 1)
          nextRemovals.push({
            id: `cancel-removal-${pendingUnit.gridX}-${pendingUnit.gridY}-${Date.now()}`,
            gridX: pendingUnit.gridX,
            gridY: pendingUnit.gridY,
            startedAtTick: state.gameState.currentTick,
            appliesAtTick: state.gameState.currentTick + ticksRequired,
            ticksRequired,
            isReverting: true,
          })
        }
      }
      for (const removal of building.pendingConfiguration.removals) {
        if (!removal.isReverting) {
          nextRemovals.push({ ...removal })
        }
      }

      const planId = building.pendingConfiguration.id
      building.pendingConfiguration = buildPlanSummary(
        {
          id: planId,
          buildingId: building.id,
          submittedAtUtc: new Date().toISOString(),
          submittedAtTick: state.gameState.currentTick,
          appliesAtTick: state.gameState.currentTick,
          totalTicksRequired: 0,
          units: [],
          removals: nextRemovals,
        },
        state.gameState.currentTick,
      )

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { cancelBuildingConfiguration: building.pendingConfiguration } }),
      })
    }

    if (query.includes('SetBuildingForSale') || query.includes('setBuildingForSale')) {
      const input = body.variables?.input
      const player = state.players.find((p) => p.id === state.currentUserId)
      const building = player?.companies.flatMap((company) => company.buildings).find((candidate) => candidate.id === input?.buildingId)

      if (!building) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              {
                message: 'Building not found',
                extensions: { code: 'BUILDING_NOT_FOUND' },
              },
            ],
          }),
        })
      }

      if (input?.isForSale === false) {
        const isLockedByUnpaidCollateral = state.myLoans.some(
          (loan) => loan.collateralBuildingId === building.id && (loan.status === 'OVERDUE' || loan.status === 'DEFAULTED') && (loan.missedPayments ?? 0) > 0 && (loan.remainingPrincipal ?? 0) > 0,
        )

        if (isLockedByUnpaidCollateral) {
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              errors: [
                {
                  message: 'Sale cannot be cancelled because this building is collateral for an unpaid loan.',
                  extensions: { code: 'BUILDING_LOCKED_AS_COLLATERAL' },
                },
              ],
            }),
          })
        }
      }

      if (input?.isForSale === true) {
        const structureBaseByType: Record<string, number> = {
          MINE: 250000,
          FACTORY: 200000,
          SALES_SHOP: 150000,
          RESEARCH_DEVELOPMENT: 300000,
          APARTMENT: 400000,
          COMMERCIAL: 350000,
          MEDIA_HOUSE: 500000,
          BANK: 600000,
          EXCHANGE: 450000,
          POWER_PLANT: 350000,
        }
        const structureValue = (structureBaseByType[building.type] ?? 0) * Math.max(1, building.level ?? 1)
        const unitsValue = (building.units ?? []).reduce((sum, unit) => sum + Math.max(1, unit.level ?? 1) * 20000, 0)
        const fallbackMarketValue = structureValue + unitsValue
        const marketValue = building.marketValuation?.totalValue ?? fallbackMarketValue
        const minimumSalePrice = building.marketValuation?.minimumSalePrice ?? Math.round(marketValue * 0.7 * 100) / 100
        if ((input.askingPrice ?? 0) < minimumSalePrice) {
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              errors: [
                {
                  message: `Asking price must be at least ${minimumSalePrice.toFixed(2)} EUR (70% of market value ${marketValue.toFixed(2)} EUR).`,
                  extensions: { code: 'ASKING_PRICE_BELOW_MINIMUM' },
                },
              ],
            }),
          })
        }
      }

      building.isForSale = input.isForSale
      building.askingPrice = input.isForSale ? input.askingPrice : null
      building.listedAtUtc = input.isForSale ? new Date().toISOString() : null

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { setBuildingForSale: { id: building.id, isForSale: building.isForSale, askingPrice: building.askingPrice, listedAtUtc: building.listedAtUtc } } }),
      })
    }

    if (query.includes('DestroyBuilding') || query.includes('destroyBuilding')) {
      const input = body.variables?.input
      const player = state.players.find((p) => p.id === state.currentUserId)
      const company = player?.companies.find((candidate) => candidate.buildings.some((building) => building.id === input?.buildingId))
      const building = company?.buildings.find((candidate) => candidate.id === input?.buildingId)

      if (!player || !company || !building) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Building not found', extensions: { code: 'BUILDING_NOT_FOUND' } }] }),
        })
      }

      const hasUnpaidCollateralLoan = state.myLoans.some(
        (loan) => loan.collateralBuildingId === building.id && (loan.status === 'ACTIVE' || loan.status === 'OVERDUE' || loan.status === 'DEFAULTED') && (loan.remainingPrincipal ?? 0) > 0,
      )

      if (hasUnpaidCollateralLoan) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              {
                message: 'Building cannot be destroyed while it is collateral for an unpaid loan.',
                extensions: { code: 'BUILDING_LOCKED_AS_COLLATERAL' },
              },
            ],
          }),
        })
      }

      const city = state.cities.find((entry) => entry.id === building.cityId)
      const currencyCode = city?.currencyCode ?? 'EUR'
      const populationIndex = building.populationIndex ?? 0.5
      const estimatedMarketValue = Math.round(((75_000 * Math.pow(1.5, (building.level ?? 1) - 1) + (building.units?.length ?? 0) * 20_000) * (1 + populationIndex * 0.5)) / 1_000) * 1_000
      const refundAmount = Math.round(estimatedMarketValue * 0.8 * 100) / 100

      company.buildings = company.buildings.filter((candidate) => candidate.id !== building.id)

      for (const lot of state.buildingLots) {
        if (lot.buildingId === building.id) {
          lot.buildingId = null
          lot.ownerCompanyId = null
        }
      }

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            destroyBuilding: {
              buildingId: building.id,
              buildingName: building.name,
              refundAmount,
              currencyCode,
            },
          },
        }),
      })
    }

    if (query.includes('SetRentPerSqm') || query.includes('setRentPerSqm')) {
      const input = body.variables?.input
      const player = state.players.find((p) => p.id === state.currentUserId)
      const building = player?.companies.flatMap((company) => company.buildings).find((candidate) => candidate.id === input?.buildingId)

      if (!building) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: "Building not found or you don't own it.", extensions: { code: 'BUILDING_NOT_FOUND' } }] }),
        })
      }

      if (building.type !== 'APARTMENT' && building.type !== 'COMMERCIAL') {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Only apartment and commercial buildings support rent pricing.', extensions: { code: 'INVALID_BUILDING_TYPE' } }] }),
        })
      }

      if (input.rentPerSqm < 0) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Rent per m² must be a non-negative value.', extensions: { code: 'INVALID_RENT' } }] }),
        })
      }

      const currentTick = state.gameState?.currentTick ?? 0
      building.pendingPricePerSqm = input.rentPerSqm
      building.pendingPriceActivationTick = currentTick + 24

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            setRentPerSqm: {
              id: building.id,
              pricePerSqm: building.pricePerSqm ?? null,
              pendingPricePerSqm: building.pendingPricePerSqm,
              pendingPriceActivationTick: building.pendingPriceActivationTick,
            },
          },
        }),
      })
    }

    if (query.includes('SetMediaHouseContentBudget') || query.includes('setMediaHouseContentBudget')) {
      if (!state.currentUserId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Not authenticated.', extensions: { code: 'AUTH_NOT_AUTHORIZED' } }] }),
        })
      }
      const input = body.variables?.input
      const player = state.players.find((p) => p.id === state.currentUserId)
      const building = player?.companies.flatMap((company) => company.buildings).find((candidate) => candidate.id === input?.buildingId)

      if (!building) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: "Building not found or you don't own it.", extensions: { code: 'BUILDING_NOT_FOUND' } }] }),
        })
      }

      if (building.type !== 'MEDIA_HOUSE') {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Only media house buildings support content budget.', extensions: { code: 'INVALID_BUILDING_TYPE' } }] }),
        })
      }

      const budget = input?.contentBudgetPerTick ?? 0
      if (budget < 0) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Content budget per tick must be a non-negative value.', extensions: { code: 'INVALID_BUDGET' } }] }),
        })
      }

      building.contentBudgetPerTick = budget === 0 ? null : budget

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            setMediaHouseContentBudget: {
              id: building.id,
              contentBudgetPerTick: building.contentBudgetPerTick ?? null,
              contentValue: building.contentValue ?? 0,
            },
          },
        }),
      })
    }

    if (query.includes('configureMediaHouseUnit')) {
      if (!state.currentUserId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Not authenticated.', extensions: { code: 'AUTH_NOT_AUTHORIZED' } }] }),
        })
      }

      const input = body.variables?.input
      const player = state.players.find((p) => p.id === state.currentUserId)
      const building = player?.companies.flatMap((company) => company.buildings).find((candidate) => candidate.id === input?.buildingId)
      if (!building || building.type !== 'MEDIA_HOUSE') {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Building not found or not media house.', extensions: { code: 'BUILDING_NOT_FOUND' } }] }),
        })
      }

      building.isAdvertisingActive = Boolean(input?.isActive) && Number(input?.campaignBudgetPerTick ?? 0) > 0

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            configureMediaHouseUnit: {
              id: input?.unitId ?? `mh-unit-${Date.now()}`,
            },
          },
        }),
      })
    }

    if (query.includes('SetPlantDispatch') || query.includes('setPlantDispatch')) {
      const input = body.variables?.input
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Not authenticated' }] }) })
      }
      const building = player.companies.flatMap((c) => c.buildings).find((b) => b.id === input?.buildingId)
      if (!building) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Building not found', extensions: { code: 'BUILDING_NOT_FOUND' } }] }),
        })
      }
      const pct = Number(input?.dispatchTargetPercent ?? 100)
      if (isNaN(pct) || pct < 0 || pct > 100) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Dispatch target must be 0–100.', extensions: { code: 'INVALID_DISPATCH_PERCENT' } }] }),
        })
      }
      building.dispatchTargetPercent = pct
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { setPlantDispatch: { id: building.id, dispatchTargetPercent: building.dispatchTargetPercent } } }),
      })
    }

    if (query.includes('PurchaseLot') || query.includes('purchaseLot')) {
      const input = body.variables?.input
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Not authenticated' }] }) })
      }
      const company = player.companies.find((c) => c.id === input?.companyId)
      if (!company) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Company not found' }] }) })
      }
      const lot = state.buildingLots.find((l) => l.id === input?.lotId)
      if (!lot) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Building lot not found.' }] }) })
      }
      if (lot.ownerCompanyId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'This lot has already been purchased.', extensions: { code: 'LOT_ALREADY_OWNED' } }] }),
        })
      }
      const suitableTypes = lot.suitableTypes.split(',').map((s) => s.trim())
      if (!suitableTypes.includes(input.buildingType)) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: `Building type ${input.buildingType} is not suitable for this lot.`, extensions: { code: 'UNSUITABLE_BUILDING_TYPE' } }] }),
        })
      }
      if (input.buildingType === 'MEDIA_HOUSE') {
        const validMediaTypes = ['NEWSPAPER', 'RADIO', 'TV']
        if (!input.mediaType || !validMediaTypes.includes(input.mediaType)) {
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              errors: [
                {
                  message: `A valid mediaType (NEWSPAPER, RADIO, TV) is required for media house buildings. Received: '${input.mediaType ?? ''}'.`,
                  extensions: { code: 'INVALID_MEDIA_TYPE' },
                },
              ],
            }),
          })
        }
      }
      const constructionCostsByType: Record<string, number> = {
        MINE: 5000,
        FACTORY: 15000,
        SALES_SHOP: 8000,
        RESEARCH_DEVELOPMENT: 25000,
        APARTMENT: 40000,
        COMMERCIAL: 20000,
        MEDIA_HOUSE: 30000,
        BANK: 50000,
        EXCHANGE: 60000,
        POWER_PLANT: 80000,
      }
      const constructionTicksByType: Record<string, number> = {
        MINE: 24,
        FACTORY: 48,
        SALES_SHOP: 24,
        RESEARCH_DEVELOPMENT: 72,
        APARTMENT: 96,
        COMMERCIAL: 48,
        MEDIA_HOUSE: 48,
        BANK: 72,
        EXCHANGE: 96,
        POWER_PLANT: 120,
      }
      const constructionCost = constructionCostsByType[input.buildingType] ?? 10000
      const constructionTicks = constructionTicksByType[input.buildingType] ?? 24
      const currentTick = state.gameState?.currentTick ?? 1
      if (company.cash < lot.price + constructionCost) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              {
                message: `Insufficient funds. Total cost (lot + construction) is $${(lot.price + constructionCost).toLocaleString()} but you only have $${company.cash.toLocaleString()}.`,
                extensions: { code: 'INSUFFICIENT_FUNDS' },
              },
            ],
          }),
        })
      }
      company.cash -= lot.price + constructionCost
      const isPowerPlant = input.buildingType === 'POWER_PLANT'
      const plantType = input.powerPlantType ?? (isPowerPlant ? 'COAL' : null)
      const defaultOutputByType: Record<string, number> = {
        COAL: 50,
        GAS: 40,
        SOLAR: 20,
        WIND: 25,
        NUCLEAR: 200,
      }
      const newBuilding: MockBuilding = {
        id: `building-lot-${Date.now()}`,
        companyId: company.id,
        cityId: lot.cityId,
        type: input.buildingType,
        name: input.buildingName,
        latitude: lot.latitude,
        longitude: lot.longitude,
        level: 1,
        powerConsumption: isPowerPlant ? 0 : 1,
        powerPlantType: plantType,
        powerOutput: isPowerPlant ? (defaultOutputByType[plantType ?? ''] ?? 30) : null,
        powerStatus: 'POWERED',
        isForSale: false,
        mediaType: input.buildingType === 'MEDIA_HOUSE' ? input.mediaType : null,
        builtAtUtc: new Date().toISOString(),
        isUnderConstruction: true,
        constructionCompletesAtTick: currentTick + constructionTicks,
        constructionCost,
        units: [],
        pendingConfiguration: null,
      }
      company.buildings.push(newBuilding)
      lot.ownerCompanyId = company.id
      lot.buildingId = newBuilding.id
      lot.ownerCompany = { id: company.id, name: company.name }
      lot.building = {
        id: newBuilding.id,
        name: newBuilding.name,
        type: newBuilding.type,
        isUnderConstruction: true,
        constructionCompletesAtTick: currentTick + constructionTicks,
        constructionCost,
      }

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            purchaseLot: {
              lot,
              building: newBuilding,
              company: { id: company.id, name: company.name, cash: company.cash },
            },
          },
        }),
      })
    }

    if (query.includes('CompleteFirstSaleMilestone') || query.includes('completeFirstSaleMilestone')) {
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Not authenticated' }] }) })
      }

      // Already completed — idempotent
      if (player.onboardingFirstSaleCompletedAtUtc) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ data: { completeFirstSaleMilestone: { ...player, password: undefined } } }),
        })
      }

      // Validate backend-authoritative condition: shop building must be tracked
      if (!player.onboardingShopBuildingId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'No sales shop was found for this onboarding milestone.', extensions: { code: 'SHOP_NOT_FOUND' } }] }),
        })
      }

      // Find the shop building and check it has a public-sales unit with a price
      const shopBuilding = player.companies.flatMap((c) => c.buildings).find((b) => b.id === player.onboardingShopBuildingId)

      const hasSalesUnit = shopBuilding?.units.some((u) => u.unitType === 'PUBLIC_SALES' && (u.minPrice ?? 0) > 0)

      if (!hasSalesUnit) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              {
                message: 'Your sales shop is not yet configured. Please set up a public sales unit with a selling price and return here to complete the milestone.',
                extensions: { code: 'SHOP_NOT_CONFIGURED' },
              },
            ],
          }),
        })
      }

      // Validate backend-authoritative condition: a real public sale must have occurred
      const hasRealSale = state.publicSalesRecords.some((r) => r.buildingId === player.onboardingShopBuildingId && r.quantitySold > 0)

      if (!hasRealSale) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              {
                message: 'Your shop has not made its first real sale yet. Wait for the simulation to process the next tick and try again after your shop has sold at least one item.',
                extensions: { code: 'FIRST_SALE_NOT_RECORDED' },
              },
            ],
          }),
        })
      }

      player.onboardingFirstSaleCompletedAtUtc = new Date().toISOString()
      player.onboardingShopBuildingId = null

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { completeFirstSaleMilestone: { ...player, password: undefined } } }),
      })
    }

    // Queries - order specific handlers before generic ones
    if (query.includes('firstSaleMission') && !query.includes('CompleteFirstSaleMilestone') && !query.includes('completeFirstSaleMilestone')) {
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Not authenticated' }] }) })
      }

      // Already completed
      if (player.onboardingFirstSaleCompletedAtUtc) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            data: {
              firstSaleMission: {
                phase: 'ALREADY_COMPLETED',
                shopBuildingId: null,
                shopName: null,
                blockers: [],
                firstSaleRevenue: null,
                firstSaleProductName: null,
                firstSaleTick: null,
                firstSaleQuantity: null,
                firstSalePricePerUnit: null,
              },
            },
          }),
        })
      }

      if (!player.onboardingShopBuildingId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            data: {
              firstSaleMission: {
                phase: 'NO_SHOP',
                shopBuildingId: null,
                shopName: null,
                blockers: [],
                firstSaleRevenue: null,
                firstSaleProductName: null,
                firstSaleTick: null,
                firstSaleQuantity: null,
                firstSalePricePerUnit: null,
              },
            },
          }),
        })
      }

      const shopBuilding = player.companies.flatMap((c) => c.buildings).find((b) => b.id === player.onboardingShopBuildingId)

      // Check for real sale
      const firstSaleRecord = state.publicSalesRecords.filter((r) => r.buildingId === player.onboardingShopBuildingId && r.quantitySold > 0).sort((a, b) => a.tick - b.tick)[0]

      if (firstSaleRecord) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            data: {
              firstSaleMission: {
                phase: 'FIRST_SALE_RECORDED',
                shopBuildingId: shopBuilding?.id ?? player.onboardingShopBuildingId,
                shopName: shopBuilding?.name ?? null,
                blockers: [],
                firstSaleRevenue: firstSaleRecord.revenue,
                firstSaleProductName: firstSaleRecord.productTypeName,
                firstSaleTick: firstSaleRecord.tick,
                firstSaleQuantity: firstSaleRecord.quantitySold,
                firstSalePricePerUnit: firstSaleRecord.pricePerUnit,
              },
            },
          }),
        })
      }

      // Compute blockers
      const blockers: string[] = []
      const publicSalesUnit = shopBuilding?.units.find((u) => u.unitType === 'PUBLIC_SALES')
      if (!publicSalesUnit) {
        blockers.push('PUBLIC_SALES_UNIT_MISSING')
      } else {
        if ((publicSalesUnit.minPrice ?? 0) <= 0) blockers.push('PRICE_NOT_SET')
        blockers.push('NO_INVENTORY') // simplified: no inventory simulation in mock
      }

      const phase = blockers.length === 0 ? 'AWAITING_FIRST_SALE' : 'CONFIGURE_SHOP'

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            firstSaleMission: {
              phase,
              shopBuildingId: shopBuilding?.id ?? player.onboardingShopBuildingId,
              shopName: shopBuilding?.name ?? null,
              blockers,
              firstSaleRevenue: null,
              firstSaleProductName: null,
              firstSaleTick: null,
              firstSaleQuantity: null,
              firstSalePricePerUnit: null,
            },
          },
        }),
      })
    }
    if (query.includes('starterIndustries')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            starterIndustries: {
              industries: ['FURNITURE', 'FOOD_PROCESSING', 'HEALTHCARE', 'ELECTRONICS', 'CONSTRUCTION', 'PHARMACEUTICALS', 'ENERGY', 'LOGISTICS'],
              proOnlyIndustries: ['ELECTRONICS', 'CONSTRUCTION', 'PHARMACEUTICALS', 'ENERGY', 'LOGISTICS'],
            },
          },
        }),
      })
    }

    if (query.includes('rankedProductTypes')) {
      const activePlayer = state.players.find((player) => player.id === state.currentUserId)
      const hasActiveProSubscription = !!activePlayer?.proSubscriptionEndsAtUtc && new Date(activePlayer.proSubscriptionEndsAtUtc).getTime() > Date.now()
      const buildingId = body.variables?.buildingId
      const unitType = (body.variables?.unitType as string | undefined)?.toUpperCase() ?? ''

      // Find the building to compute context-aware rankings (simulates backend logic)
      const building = state.players
        .flatMap((p) => p.companies)
        .flatMap((c) => c.buildings)
        .find((b) => b.id === buildingId)

      // Collect unit product IDs from active + pending configuration
      const allBuildingUnits = [...(building?.units ?? []), ...(building?.pendingConfiguration?.units ?? [])]

      const connectedProductIds = new Set<string>()
      // For PRODUCT_QUALITY/BRAND_QUALITY, collect used products across ALL company buildings
      const usedByCompanyIds = new Set<string>()
      if (unitType === 'PUBLIC_SALES') {
        // Connected = products from MANUFACTURING or B2B_SALES units
        allBuildingUnits.filter((u) => (u.unitType === 'MANUFACTURING' || u.unitType === 'B2B_SALES') && u.productTypeId).forEach((u) => connectedProductIds.add(u.productTypeId!))
      } else if (unitType === 'STORAGE') {
        // Connected = products from MANUFACTURING units
        allBuildingUnits.filter((u) => u.unitType === 'MANUFACTURING' && u.productTypeId).forEach((u) => connectedProductIds.add(u.productTypeId!))
        // Also products currently in inventory
        allBuildingUnits
          .filter((u) => u.inventoryItems && u.inventoryItems.length > 0)
          .flatMap((u) => u.inventoryItems ?? [])
          .filter((item) => item.productTypeId)
          .forEach((item) => connectedProductIds.add(item.productTypeId!))
      } else if (unitType === 'B2B_SALES') {
        // Connected = products from MANUFACTURING or PURCHASE units
        allBuildingUnits.filter((u) => (u.unitType === 'MANUFACTURING' || u.unitType === 'PURCHASE') && u.productTypeId).forEach((u) => connectedProductIds.add(u.productTypeId!))
        // Also products currently stocked in B2B_SALES units
        allBuildingUnits
          .filter((u) => u.unitType === 'B2B_SALES' && u.inventoryItems && u.inventoryItems.length > 0)
          .flatMap((u) => u.inventoryItems ?? [])
          .filter((item) => item.productTypeId)
          .forEach((item) => connectedProductIds.add(item.productTypeId!))
      } else if (unitType === 'PRODUCT_QUALITY' || unitType === 'BRAND_QUALITY') {
        // Separate manufacturing products (score 80) from sales/inventory products (score 50).
        // Products actively manufactured are the highest R&D priority.
        const activePlayer = state.players.find((p) => p.id === state.currentUserId)
        const allCompanyBuildings = (activePlayer?.companies ?? []).flatMap((c) => c.buildings)
        const manufacturingIds = new Set<string>()
        allCompanyBuildings.forEach((b) => {
          const allUnits = [...(b.units ?? []), ...(b.pendingConfiguration?.units ?? [])]
          allUnits.filter((u) => u.unitType === 'MANUFACTURING' && u.productTypeId).forEach((u) => manufacturingIds.add(u.productTypeId!))
        })
        allCompanyBuildings.forEach((b) => {
          const allUnits = [...(b.units ?? []), ...(b.pendingConfiguration?.units ?? [])]
          // Sales/inventory: only add if NOT already in manufacturing
          allUnits
            .filter((u) => (u.unitType === 'PUBLIC_SALES' || u.unitType === 'B2B_SALES') && u.productTypeId && !manufacturingIds.has(u.productTypeId!))
            .forEach((u) => usedByCompanyIds.add(u.productTypeId!))
          allUnits
            .filter((u) => u.inventoryItems && u.inventoryItems.length > 0)
            .flatMap((u) => u.inventoryItems ?? [])
            .filter((item) => item.productTypeId && !manufacturingIds.has(item.productTypeId!))
            .forEach((item) => usedByCompanyIds.add(item.productTypeId!))
        })

        // Sort: connected first (score 100), manufacturing second (score 80), used_by_company third (score 50), catalog (score 10)
        const enriched = state.productTypes.map((product) => {
          const isConnected = connectedProductIds.has(product.id)
          const isManufacturing = !isConnected && manufacturingIds.has(product.id)
          const isUsedByCompany = !isConnected && !isManufacturing && usedByCompanyIds.has(product.id)
          return {
            rankingReason: isConnected ? 'connected' : isManufacturing ? 'manufacturing' : isUsedByCompany ? 'used_by_company' : 'catalog',
            rankingScore: isConnected ? 100 : isManufacturing ? 80 : isUsedByCompany ? 50 : 10,
            productType: {
              ...product,
              isUnlockedForCurrentPlayer: product.isProOnly ? hasActiveProSubscription : true,
            },
          }
        })
        enriched.sort((a, b) => {
          if (b.rankingScore !== a.rankingScore) return b.rankingScore - a.rankingScore
          return a.productType.name.localeCompare(b.productType.name)
        })

        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ data: { rankedProductTypes: enriched } }),
        })
      }

      // General case: connected + catalog ranking for PUBLIC_SALES, B2B_SALES, STORAGE, etc.
      const enrichedGeneral = state.productTypes.map((product) => {
        const isConnected = connectedProductIds.has(product.id)
        const isUsedByCompany = !isConnected && usedByCompanyIds.has(product.id)
        return {
          rankingReason: isConnected ? 'connected' : isUsedByCompany ? 'used_by_company' : 'catalog',
          rankingScore: isConnected ? 100 : isUsedByCompany ? 50 : 10,
          productType: {
            ...product,
            isUnlockedForCurrentPlayer: product.isProOnly ? hasActiveProSubscription : true,
          },
        }
      })
      enrichedGeneral.sort((a, b) => {
        if (b.rankingScore !== a.rankingScore) return b.rankingScore - a.rankingScore
        return a.productType.name.localeCompare(b.productType.name)
      })

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { rankedProductTypes: enrichedGeneral } }),
      })
    }

    if (query.includes('productTypes')) {
      const industry = body.variables?.industry
      const activePlayer = state.players.find((player) => player.id === state.currentUserId)
      const hasActiveProSubscription = !!activePlayer?.proSubscriptionEndsAtUtc && new Date(activePlayer.proSubscriptionEndsAtUtc).getTime() > Date.now()
      const filtered = industry ? state.productTypes.filter((p) => p.industry === industry) : state.productTypes
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            productTypes: filtered.map((product) => ({
              ...product,
              isUnlockedForCurrentPlayer: product.isProOnly ? hasActiveProSubscription : true,
            })),
          },
        }),
      })
    }

    if (query.includes('buildingUnitInventorySummaries')) {
      const buildingId = body.variables?.buildingId
      const building = state.players
        .flatMap((player) => player.companies)
        .flatMap((company) => company.buildings)
        .find((candidate) => candidate.id === buildingId)

      const buildingUnitInventorySummaries = (building?.units ?? [])
        .map((unit) => {
          const inventoryItems = getMockUnitInventoryItems(unit)
          const quantity = inventoryItems.reduce((total, item) => total + item.quantity, 0)
          const capacity = getMockUnitCapacity(unit)
          const movement = state.unitLastTickMovement[unit.id] ?? null
          return {
            buildingUnitId: unit.id,
            quantity,
            capacity,
            fillPercent: capacity > 0 ? Math.min(quantity / capacity, 1) : 0,
            averageQuality: quantity > 0 ? Number((inventoryItems.reduce((total, item) => total + item.quantity * item.quality, 0) / quantity).toFixed(4)) : null,
            totalSourcingCost: Number(inventoryItems.reduce((total, item) => total + item.sourcingCostTotal, 0).toFixed(2)),
            sourcingCostPerUnit: quantity > 0 ? Number((inventoryItems.reduce((total, item) => total + item.sourcingCostTotal, 0) / quantity).toFixed(2)) : 0,
            lastTickInflow: movement?.lastTickInflow ?? null,
            lastTickOutflow: movement?.lastTickOutflow ?? null,
          }
        })
        .filter((summary) => summary.capacity > 0 || summary.quantity > 0)

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { buildingUnitInventorySummaries } }),
      })
    }

    if (query.includes('buildingUnitInventories')) {
      const buildingId = body.variables?.buildingId
      const building = state.players
        .flatMap((player) => player.companies)
        .flatMap((company) => company.buildings)
        .find((candidate) => candidate.id === buildingId)

      const buildingUnitInventories = (building?.units ?? []).flatMap((unit) => getMockUnitInventoryItems(unit))

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { buildingUnitInventories } }),
      })
    }

    if (query.includes('buildingUnitResourceHistories')) {
      const buildingId = body.variables?.buildingId
      const building = state.players
        .flatMap((player) => player.companies)
        .flatMap((company) => company.buildings)
        .find((candidate) => candidate.id === buildingId)

      const buildingUnitResourceHistories = (building?.units ?? []).flatMap((unit) => getMockUnitResourceHistory(unit)).sort((left, right) => left.tick - right.tick)

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { buildingUnitResourceHistories } }),
      })
    }

    if (query.includes('buildingUnitOperationalStatuses')) {
      const buildingId = body.variables?.buildingId
      const building = state.players
        .flatMap((player) => player.companies)
        .flatMap((company) => company.buildings)
        .find((candidate) => candidate.id === buildingId)

      const buildingUnitOperationalStatuses = (building?.units ?? []).map((unit) => {
        // Base labor hours and energy MWh per unit type, mirroring backend:
        // projects/Api/Utilities/CompanyEconomyCalculator.cs :: GetBaseUnitLaborHours / GetBaseUnitEnergyMwh
        // Update here if the backend constants change.
        const laborHoursMap: Record<string, number> = {
          MINING: 1.4,
          STORAGE: 0.15,
          B2B_SALES: 0.45,
          PURCHASE: 0.35,
          MANUFACTURING: 0.85,
          BRANDING: 0.3,
          MARKETING: 0.6,
          PUBLIC_SALES: 0.7,
          PRODUCT_QUALITY: 0.55,
          BRAND_QUALITY: 0.55,
        }
        const energyMwhMap: Record<string, number> = {
          MINING: 0.45,
          STORAGE: 0.04,
          B2B_SALES: 0.08,
          PURCHASE: 0.06,
          MANUFACTURING: 0.18,
          BRANDING: 0.05,
          MARKETING: 0.07,
          PUBLIC_SALES: 0.12,
          PRODUCT_QUALITY: 0.09,
          BRAND_QUALITY: 0.09,
        }
        const level = unit.level || 1
        const laborHours = (laborHoursMap[unit.unitType] ?? 0) * level
        const energyMwh = (energyMwhMap[unit.unitType] ?? 0) * level
        // Bratislava base wage $18/hr × default salary multiplier 1.0
        // (projects/Api/Data/AppDbInitializer.cs BaseSalaryPerManhour)
        const hourlyWage = 18
        // projects/Api/Engine/GameConstants.cs EnergyPricePerMwh
        const energyPricePerMwh = 55
        return {
          buildingUnitId: unit.id,
          status: unit.inventoryQuantity && unit.inventoryQuantity > 0 ? 'ACTIVE' : 'IDLE',
          blockedCode: null,
          blockedReason: null,
          idleTicks: 0,
          nextTickLaborCost: laborHours > 0 ? Math.round(laborHours * hourlyWage * 100) / 100 : null,
          nextTickEnergyCost: energyMwh > 0 ? Math.round(energyMwh * energyPricePerMwh * 100) / 100 : null,
        }
      })

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { buildingUnitOperationalStatuses } }),
      })
    }

    if (query.includes('buildingRecentActivity')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { buildingRecentActivity: [] } }),
      })
    }

    if (query.includes('buildingFinancialTimeline') && !query.includes('powerPlantAnalytics')) {
      const buildingId = body.variables?.buildingId
      const limit = Number(body.variables?.limit ?? 100)
      const buildingFinancialTimeline = buildMockBuildingFinancialTimeline(state, buildingId, limit)

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { buildingFinancialTimeline } }),
      })
    }

    if (query.includes('buildingSupplyChain')) {
      if (!state.currentUserId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Not authenticated.' }] }),
        })
      }
      const buildingId = body.variables?.buildingId

      // Return override data if present
      if (buildingId && state.supplyChainData[buildingId]) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ data: { buildingSupplyChain: state.supplyChainData[buildingId] } }),
        })
      }

      // Auto-generate from building units
      const building = state.players
        .flatMap((player) => player.companies)
        .flatMap((company) => company.buildings)
        .find((candidate) => candidate.id === buildingId)

      if (!building) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Building not found.', extensions: { code: 'BUILDING_NOT_FOUND' } }] }),
        })
      }

      const units = (building.units ?? []).map((unit) => ({
        buildingUnitId: unit.id,
        unitType: unit.unitType,
        gridX: unit.gridX,
        gridY: unit.gridY,
        level: unit.level ?? 1,
        status: unit.unitType ? 'ACTIVE' : 'UNCONFIGURED',
        idleTicks: 0,
        fillPercent: 45,
        resourceTypeId: unit.resourceTypeId ?? null,
        productTypeId: unit.productTypeId ?? null,
        resourceOrProductName: null,
        estimatedTransitCost: null,
      }))

      const buildingSupplyChain = {
        buildingId: building.id,
        buildingName: building.name,
        buildingType: building.buildingType ?? 'FACTORY',
        units,
        links: [],
        healthScore: 'GREEN',
        healthReason: 'All units operating normally',
        criticalUnitIds: [],
        warningUnitIds: [],
      }

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { buildingSupplyChain } }),
      })
    }

    if (query.includes('powerPlantAnalytics')) {
      const buildingId = body.variables?.buildingId
      const limit = Number(body.variables?.limit ?? 100)
      const building = state.players
        .flatMap((p) => p.companies)
        .flatMap((c) => c.buildings)
        .find((b) => b.id === buildingId)

      if (!building) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Building not found', extensions: { code: 'BUILDING_NOT_FOUND' } }] }),
        })
      }

      const dataToTick = state.gameState.currentTick
      const dataFromTick = Math.max(0, dataToTick - (limit - 1))

      const isThermal = ['COAL', 'GAS'].includes(building.powerPlantType ?? '')
      const fuelUnits = (building.units ?? []).filter((u) => u.unitType === 'FUEL_PURCHASE')
      const epUnits = (building.units ?? []).filter((u) => u.unitType === 'ENERGY_PRODUCING')
      const maxFuelReserveMwh = isThermal ? fuelUnits.reduce((sum, u) => sum + u.level * 50, 0) : 0
      const fuelPurchaseCapacityMwhPerTick = isThermal ? fuelUnits.reduce((sum, u) => sum + u.level * 10, 0) : 0
      const energyProducingCapacityMw = isThermal ? epUnits.reduce((sum, u) => sum + u.level * 20, 0) : 0
      const currentReserve = building.fuelReserveMwh ?? 0
      const fuelConstrainedOutputMw = isThermal && energyProducingCapacityMw > 0 ? Math.max(0, energyProducingCapacityMw - currentReserve) : 0
      const fuelReservePercent = maxFuelReserveMwh > 0 ? Math.min(100, Math.round((currentReserve / maxFuelReserveMwh) * 100)) : 0
      const fuelTypeLabel = building.powerPlantType === 'GAS' ? 'Natural Gas' : building.powerPlantType === 'COAL' ? 'Coal' : ''
      const fuelCostPerMwhEur = building.powerPlantType === 'GAS' ? 3.6 : building.powerPlantType === 'COAL' ? 3 : 0

      const powerPlantAnalytics = {
        buildingId: building.id,
        buildingName: building.name,
        plantType: building.powerPlantType ?? 'COAL',
        currentOutputMw: building.powerOutput ?? 50,
        dispatchTargetPercent: building.dispatchTargetPercent ?? 100,
        fuelReserveMwh: currentReserve,
        maxFuelReserveMwh,
        fuelReservePercent,
        fuelPurchaseCapacityMwhPerTick,
        energyProducingCapacityMw,
        fuelConstrainedOutputMw,
        fuelTypeLabel,
        fuelCostPerMwhEur,
        dataFromTick,
        dataToTick,
        totalSurplusIncome: 225,
        totalGridFines: 0,
        totalOperatingCosts: 12,
        totalFuelCosts: isThermal ? 85 : 0,
        totalNetProfit: 128,
        timeline: Array.from({ length: Math.min(limit, 5) }, (_, i) => ({
          tick: dataToTick - 4 + i,
          surplusIncome: 45,
          gridFine: 0,
          operatingCosts: 2.4,
          fuelCosts: isThermal ? 17 : 0,
          netProfit: 25.6,
        })),
      }

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { powerPlantAnalytics } }),
      })
    }

    if (query.includes('globalExchangeOffers')) {
      const destinationCityId = body.variables?.destinationCityId
      const resourceTypeId = body.variables?.resourceTypeId
      const destinationCity = state.cities.find((city) => city.id === destinationCityId)
      const resources = state.resourceTypes.filter((resource) => !resourceTypeId || resource.id === resourceTypeId)

      // Determine FX rate for the destination city (EUR→local currency).
      const destCurrencyCode = destinationCity?.currencyCode ?? 'EUR'
      const destFxRate = destCurrencyCode === 'EUR' ? 1 : (state.fxRates.find((r) => r.quoteCurrencyCode === destCurrencyCode)?.rate ?? 1)

      const globalExchangeOffers = destinationCity
        ? state.cities
            .flatMap((city) =>
              resources.map((resource) => {
                const abundance = city.resources.find((entry) => entry.resourceType.id === resource.id)?.abundance ?? 0.05
                const distanceKm = computeDistanceKm(city.latitude, city.longitude, destinationCity.latitude, destinationCity.longitude)
                const exchangePricePerUnit = computeMockExchangePrice(resource.basePrice, abundance, city.averageRentPerSqm, destFxRate)
                const transitCostPerUnit = city.id === destinationCity.id ? 0 : computeMockTransitCost(resource.weightPerUnit, distanceKm, destFxRate)
                const qualityBand = computeMockExchangeQualityBand(abundance)
                return {
                  cityId: city.id,
                  cityName: city.name,
                  resourceTypeId: resource.id,
                  resourceName: resource.name,
                  resourceSlug: resource.slug,
                  unitSymbol: resource.unitSymbol,
                  localAbundance: abundance,
                  exchangePricePerUnit,
                  estimatedQuality: computeMockExchangeQuality(abundance),
                  qualityMin: qualityBand.min,
                  qualityMax: qualityBand.max,
                  transitCostPerUnit,
                  deliveredPricePerUnit: Number((exchangePricePerUnit + transitCostPerUnit).toFixed(2)),
                  distanceKm: Number(distanceKm.toFixed(1)),
                  askPriceHistory: buildMockAskPriceHistory(state.gameState.currentTick, exchangePricePerUnit, `${city.id}-${resource.id}`),
                }
              }),
            )
            .sort((left, right) => left.deliveredPricePerUnit - right.deliveredPricePerUnit)
        : []

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { globalExchangeOffers } }),
      })
    }

    if (query.includes('globalExchangeProductListings')) {
      const productTypeIdFilter = body.variables?.productTypeId
      const listings = state.productExchangeListings.filter((l) => !productTypeIdFilter || l.productTypeId === productTypeIdFilter)
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { globalExchangeProductListings: listings } }),
      })
    }

    if (query.includes('buyFromExchange')) {
      if (!state.currentUserId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ data: { buyFromExchange: { success: false, errorCode: 'UNAUTHORIZED', errorMessage: 'Not authenticated.' } } }),
        })
      }
      const input = body.variables?.input ?? {}
      const resource = state.resourceTypes.find((r) => r.id === input.resourceTypeId)
      const sourceCity = state.cities.find((c) => c.id === input.sourceCityId)
      const account = state.myBankAccounts.find((a) => a.id === input.bankAccountId)
      const qty = parseFloat(String(input.quantity ?? 0))
      if (!resource || !sourceCity) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ data: { buyFromExchange: { success: false, errorCode: 'RESOURCE_NOT_FOUND', errorMessage: 'Resource or city not found.' } } }),
        })
      }
      const abundance = sourceCity.resources.find((e) => e.resourceType.id === resource.id)?.abundance ?? 0.05
      const destCity = state.cities.find((c) => c.id === state.myBankAccounts.find((a) => a.id === input.bankAccountId)?.cityId) ?? sourceCity
      const distanceKm = computeDistanceKm(sourceCity.latitude, sourceCity.longitude, destCity.latitude ?? sourceCity.latitude, destCity.longitude ?? sourceCity.longitude)
      const currencyCode = account?.currencyCode ?? 'EUR'
      const fxRate = currencyCode === 'EUR' ? 1 : (state.fxRates.find((r) => r.quoteCurrencyCode === currencyCode)?.rate ?? 1)
      const exchangePrice = computeMockExchangePrice(resource.basePrice, abundance, sourceCity.averageRentPerSqm, fxRate)
      const transitCost = computeMockTransitCost(resource.weightPerUnit, distanceKm, fxRate)
      const totalCost = (exchangePrice + transitCost) * qty
      if (account && account.balance < totalCost) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ data: { buyFromExchange: { success: false, errorCode: 'INSUFFICIENT_FUNDS', errorMessage: 'Insufficient funds.' } } }),
        })
      }
      if (account) {
        account.balance = Number((account.balance - totalCost).toFixed(2))
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            buyFromExchange: {
              success: true,
              errorCode: null,
              errorMessage: null,
              resourceName: resource.name,
              quantityPurchased: qty,
              exchangePricePerUnit: exchangePrice,
              transitCostPerUnit: transitCost,
              deliveredPricePerUnit: Number((exchangePrice + transitCost).toFixed(2)),
              totalCost: Number(totalCost.toFixed(2)),
              qualityDelivered: computeMockExchangeQuality(abundance),
              currencyCode,
              newBankBalance: account?.balance ?? 0,
            },
          },
        }),
      })
    }

    if (query.includes('sellToExchange')) {
      if (!state.currentUserId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ data: { sellToExchange: { success: false, errorCode: 'UNAUTHORIZED', errorMessage: 'Not authenticated.' } } }),
        })
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            sellToExchange: {
              success: true,
              errorCode: null,
              errorMessage: null,
              resourceName: 'Wood',
              quantitySold: parseFloat(String(body.variables?.input?.quantity ?? 0)),
              exchangePricePerUnit: 10,
              totalProceeds: parseFloat(String(body.variables?.input?.quantity ?? 0)) * 10,
              currencyCode: 'EUR',
              newBankBalance: 50000,
            },
          },
        }),
      })
    }

    if (query.includes('chatMessages')) {
      if (!state.currentUserId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Not authenticated.' }] }),
        })
      }

      const currentPlayer = state.players.find((player) => player.id === state.currentUserId)
      const limit = Math.min(Math.max(Number(body.variables?.limit ?? 50), 1), 100)
      const canSeeInvisible = currentPlayer?.role === 'ADMIN'

      const messages = state.chatMessages
        .filter((message) => {
          const author = state.players.find((player) => player.id === message.playerId)
          if (!author) return false
          return !author.isInvisibleInChat || author.id === state.currentUserId || canSeeInvisible
        })
        .slice(-limit)
        .map((message) => {
          const author = state.players.find((player) => player.id === message.playerId)!
          return {
            id: message.id,
            playerId: message.playerId,
            playerDisplayName: author.displayName,
            message: message.message,
            sentAtUtc: message.sentAtUtc,
            isOwnMessage: message.playerId === state.currentUserId,
          }
        })

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { chatMessages: messages } }),
      })
    }

    if (query.includes('sendChatMessage')) {
      if (!state.currentUserId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Not authenticated.' }] }),
        })
      }

      const currentPlayer = state.players.find((player) => player.id === state.currentUserId)
      const message = String(body.variables?.input?.message ?? '').trim()
      if (!currentPlayer || !message) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Chat message cannot be empty.' }] }),
        })
      }

      const chatMessage = {
        id: `chat-${state.chatMessages.length + 1}`,
        playerId: currentPlayer.id,
        message,
        sentAtUtc: new Date().toISOString(),
      }
      state.chatMessages.push(chatMessage)

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            sendChatMessage: {
              id: chatMessage.id,
              playerId: currentPlayer.id,
              playerDisplayName: currentPlayer.displayName,
              message: chatMessage.message,
              sentAtUtc: chatMessage.sentAtUtc,
              isOwnMessage: true,
            },
          },
        }),
      })
    }

    if (query.includes('myCompanies')) {
      applyDueBuildingUpgrades(state)
      const player = state.players.find((p) => p.id === state.currentUserId)
      const responseData: Record<string, unknown> = { myCompanies: player?.companies ?? [] }

      // Combined dashboard query — also return other requested top-level fields so the
      // dashboard's combined query does not nullify the game-state store.
      if (query.includes('gameState')) {
        responseData.gameState = buildMockGameStatePayload(state.gameState)
      }
      if (query.includes('myPendingActions')) {
        const currentTick = state.gameState.currentTick
        responseData.myPendingActions = (player?.companies ?? [])
          .flatMap((company) =>
            company.buildings
              .filter((building) => building.pendingConfiguration != null && building.pendingConfiguration.appliesAtTick > currentTick)
              .map((building) => ({
                id: building.pendingConfiguration!.id,
                actionType: 'BUILDING_UPGRADE',
                buildingId: building.id,
                buildingName: building.name,
                buildingType: building.type,
                submittedAtUtc: building.pendingConfiguration!.submittedAtUtc,
                submittedAtTick: building.pendingConfiguration!.submittedAtTick,
                appliesAtTick: building.pendingConfiguration!.appliesAtTick,
                ticksRemaining: building.pendingConfiguration!.appliesAtTick - currentTick,
                totalTicksRequired: building.pendingConfiguration!.totalTicksRequired,
              })),
          )
          .sort((a, b) => a.appliesAtTick - b.appliesAtTick)
      }
      if (query.includes('cities')) {
        responseData.cities = state.cities ?? []
      }
      if (query.includes('getCurrentEconomicCycle')) {
        responseData.getCurrentEconomicCycle = state.economicCycle
      }
      if (query.includes('getActiveMarketEvents')) {
        responseData.getActiveMarketEvents = state.activeMarketEvents ?? []
      }
      if (query.includes('getEconomicHistory')) {
        responseData.getEconomicHistory = state.economicHistory ?? []
      }
      if (query.includes('myLoans')) {
        responseData.myLoans = state.myLoans ?? []
      }
      if (query.includes('tutorialProgress')) {
        responseData.tutorialProgress = state.tutorialProgress
      }

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: responseData }),
      })
    }

    if (query.includes('companySettings')) {
      const companyId = body.variables?.companyId
      const player = state.players.find((candidate) => candidate.id === state.currentUserId)
      const company = player?.companies.find((candidate) => candidate.id === companyId)

      if (!company) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ data: { companySettings: null } }),
        })
      }

      const computeAssetValue = (candidate: MockCompany) => candidate.cash + getCompanyAssetBaseValue(candidate)
      const companyAssetValue = computeAssetValue(company)
      const maxAssetValue = Math.max(...state.players.flatMap((candidate) => candidate.companies).map(computeAssetValue), 0)
      const ageTicks = Math.max(state.gameState.currentTick - (company.foundedAtTick ?? 0), 0)
      const ageFactor = Number(Math.min(ageTicks / (TICKS_PER_YEAR * 2), 1).toFixed(4))
      const assetFactor = Number((maxAssetValue > 0 ? Math.min(companyAssetValue / maxAssetValue, 1) : 0).toFixed(4))
      const overheadRate = Number((0.5 * ageFactor * assetFactor).toFixed(4))
      const primaryCurrencyCode = company.buildings.map((building) => state.cities.find((city) => city.id === building.cityId)?.currencyCode).find((currencyCode) => Boolean(currencyCode)) ?? 'EUR'

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            companySettings: {
              companyId: company.id,
              companyName: company.name,
              cash: company.cash,
              totalSharesIssued: getCompanyTotalShares(company),
              dividendPayoutRatio: getCompanyDividendPayoutRatio(company),
              foundedAtTick: company.foundedAtTick ?? 0,
              administrationOverheadRate: overheadRate,
              ageFactor,
              assetFactor,
              assetValue: companyAssetValue,
              currencyCode: primaryCurrencyCode,
                citySalarySettings: state.cities.map((city) => {
                  const salaryMultiplier = company.citySalaryMultipliers?.[city.id] ?? 1
                  return {
                  cityId: city.id,
                  cityName: city.name,
                  currencyCode: city.currencyCode ?? 'EUR',
                  baseSalaryPerManhour: city.baseSalaryPerManhour,
                  salaryMultiplier,
                    effectiveSalaryPerManhour: Number((city.baseSalaryPerManhour * salaryMultiplier).toFixed(2)),
                  }
                }),
                pendingDividendProposal: (() => {
                  const proposal = state.dividendProposals.find(
                    (candidate) =>
                      candidate.companyId === company.id &&
                      candidate.status === 'VOTING' &&
                      candidate.totalPayout <= 0 &&
                      candidate.votingCloseTick >= state.gameState.currentTick,
                  )
                  if (!proposal) {
                    return null
                  }

                  const votes = state.dividendVotes.filter((candidate) => candidate.proposalId === proposal.id)
                  const forVotes = votes
                    .filter((candidate) => candidate.voteChoice === 'FOR')
                    .reduce((sum, candidate) => sum + candidate.sharesVoted, 0)
                  const againstVotes = votes
                    .filter((candidate) => candidate.voteChoice === 'AGAINST')
                    .reduce((sum, candidate) => sum + candidate.sharesVoted, 0)
                  const myVote = votes.find(
                    (candidate) =>
                      candidate.voterAccountId === player.id &&
                      candidate.voterAccountType === 'PERSON',
                  )
                  return {
                    id: proposal.id,
                    dividendPercent: Number((proposal.dividendPerShare * 100).toFixed(2)),
                    votingCloseTick: proposal.votingCloseTick,
                    ticksRemaining: Math.max(0, proposal.votingCloseTick - state.gameState.currentTick),
                    forVotes,
                    againstVotes,
                    myVoteChoice: myVote?.voteChoice ?? null,
                  }
                })(),
              },
            },
          }),
      })
    }

    if (query.includes('personAccount')) {
      const player = state.players.find((candidate) => candidate.id === state.currentUserId)
      if (!player) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Not authenticated' }] }) })
      }

      const shareholdings = state.shareholdings
        .filter((holding) => holding.ownerPlayerId === player.id && holding.shareCount > 0)
        .map((holding) => {
          const company = state.players.flatMap((candidate) => candidate.companies).find((candidate) => candidate.id === holding.companyId)
          if (!company || isGovernmentCompany(state, company)) {
            return null
          }

          const sharePrice = computeMockSharePrice(company)
          return {
            companyId: company.id,
            stockSymbol: stockSymbolForCompany(company.id),
            companyName: company.name,
            shareCount: holding.shareCount,
            ownershipRatio: Number((holding.shareCount / getCompanyTotalShares(company)).toFixed(4)),
            sharePrice,
            marketValue: Number((holding.shareCount * sharePrice).toFixed(2)),
          }
        })
        .filter((holding): holding is NonNullable<typeof holding> => holding !== null)

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            personAccount: {
              playerId: player.id,
              displayName: player.displayName,
              personalCash: player.personalCash,
              taxReserve: player.personalTaxReserve ?? 0,
              availableCash: computeAvailableCash(player),
              totalNetWealth: computeAvailableCash(player) + shareholdings.reduce((sum: number, h: { marketValue: number }) => sum + h.marketValue, 0),
              activeAccountType: player.activeAccountType,
              activeCompanyId: player.activeCompanyId,
              shareholdings,
              interestPayments: player.interestPayments ?? [],
              dividendPayments: player.dividendPayments,
              stockTrades: player.stockTrades,
            },
          },
        }),
      })
    }

    if (query.includes('myOpenDividendProposalCount')) {
      const player = state.players.find((candidate) => candidate.id === state.currentUserId)
      if (!player) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: { myOpenDividendProposalCount: 0 } }) })
      }

      const companyIds = new Set(
        state.shareholdings
          .filter((holding) => holding.shareCount > 0 && (holding.ownerPlayerId === player.id || (holding.ownerCompanyId && player.companies.some((company) => company.id === holding.ownerCompanyId))))
          .map((holding) => holding.companyId),
      )
      const openCount = state.dividendProposals.filter(
        (proposal) => companyIds.has(proposal.companyId) && proposal.status === 'VOTING' && proposal.votingCloseTick >= state.gameState.currentTick,
      ).length

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { myOpenDividendProposalCount: openCount } }),
      })
    }

    if (query.includes('getDividendProposals')) {
      const player = state.players.find((candidate) => candidate.id === state.currentUserId)
      const stockSymbol = String(body.variables?.stockSymbol ?? '')
      const proposals = state.dividendProposals
        .filter((proposal) => proposal.stockSymbol === stockSymbol)
        .map((proposal) => {
          const proposalVotes = state.dividendVotes.filter((vote) => vote.proposalId === proposal.id)
          const forVotes = proposalVotes.filter((vote) => vote.voteChoice === 'FOR').reduce((sum, vote) => sum + vote.sharesVoted, 0)
          const againstVotes = proposalVotes.filter((vote) => vote.voteChoice === 'AGAINST').reduce((sum, vote) => sum + vote.sharesVoted, 0)
          const myVote = player ? proposalVotes.find((vote) => vote.voterAccountId === player.id || player.companies.some((company) => company.id === vote.voterAccountId)) : null

          return {
            ...proposal,
            forVotes,
            againstVotes,
            outcome: proposal.outcome === 'PENDING' ? (forVotes > againstVotes ? 'APPROVED' : 'REJECTED') : proposal.outcome,
            ticksRemaining: proposal.status === 'VOTING' ? Math.max(0, proposal.votingCloseTick - state.gameState.currentTick) : 0,
            myVoteChoice: myVote?.voteChoice ?? null,
            mySharesVoted: myVote?.sharesVoted ?? null,
          }
        })

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { dividendProposals: proposals } }),
      })
    }

    if (query.includes('stockExchangeListings')) {
      const player = state.players.find((candidate) => candidate.id === state.currentUserId)
      const listings = state.players
        .flatMap((candidate) => candidate.companies)
        .filter((company) => !isGovernmentCompany(state, company))
        .map((company) => {
          const playerOwnedShares = player
            ? state.shareholdings.filter((holding) => holding.companyId === company.id && holding.ownerPlayerId === player.id).reduce((total, holding) => total + holding.shareCount, 0)
            : 0

          const controlledCompanyIds = player ? getPlayerControlledCompanyIds(state, player.id) : new Set<string>()
          const controlledCompanyOwnedShares = player
            ? state.shareholdings
                .filter((holding) => holding.companyId === company.id && holding.ownerCompanyId && controlledCompanyIds.has(holding.ownerCompanyId))
                .reduce((total, holding) => total + holding.shareCount, 0)
            : 0

          const combinedControlledOwnershipRatio = player ? getCombinedControlledOwnershipRatio(state, player.id, company) : 0

          return {
            companyId: company.id,
            companyName: company.name,
            primaryCityName: state.cities.find((city) => city.id === company.buildings[0]?.cityId)?.name ?? 'UNKNOWN',
            primaryIndustry: deriveMockPrimaryIndustry(state, company),
            totalSharesIssued: getCompanyTotalShares(company),
            publicFloatShares: getPublicFloatShares(state, company),
            sharePrice: computeMockSharePrice(company),
            dailyChangePercent: 0,
            marketValue: Number((getCompanyTotalShares(company) * computeMockSharePrice(company)).toFixed(2)),
            bidPrice: Number((computeMockSharePrice(company) * 0.99).toFixed(2)),
            askPrice: Number((computeMockSharePrice(company) * 1.01).toFixed(2)),
            dividendPayoutRatio: getCompanyDividendPayoutRatio(company),
            playerOwnedShares,
            controlledCompanyOwnedShares,
            combinedControlledOwnershipRatio,
            canProposeDividend: player ? company.playerId === player.id || combinedControlledOwnershipRatio > 0.5 : false,
            canClaimControl: combinedControlledOwnershipRatio >= 0.5,
            canMerge: combinedControlledOwnershipRatio >= 0.9,
          }
        })
        .sort((left, right) => left.companyName.localeCompare(right.companyName))

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { stockExchangeListings: listings } }),
      })
    }

    if (query.includes('myOpenOrders')) {
      const player = state.players.find((candidate) => candidate.id === state.currentUserId)
      const playerCompanyIds = new Set((player?.companies ?? []).map((company) => company.id))
      const rows = state.stockLimitOrders
        .filter(
          (order) => (order.status === 'OPEN' || order.status === 'PARTIALLY_FILLED') && (order.ownerPlayerId === player?.id || (order.ownerCompanyId && playerCompanyIds.has(order.ownerCompanyId))),
        )
        .map((order) => {
          const company = state.players.flatMap((candidate) => candidate.companies).find((candidate) => candidate.id === order.companyId)
          return {
            id: order.id,
            companyId: order.companyId,
            companyName: company?.name ?? 'Unknown',
            stockSymbol: order.stockSymbol,
            side: order.side,
            limitPrice: order.limitPrice,
            quantity: order.quantity,
            filledQuantity: order.filledQuantity,
            remainingQuantity: Math.max(0, order.quantity - order.filledQuantity),
            status: order.status,
            createdAtTick: order.createdAtTick,
            updatedAtTick: order.updatedAtTick,
          }
        })
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: { myOpenOrders: rows } }) })
    }

    if (query.includes('orderBook')) {
      const symbol = String(body.variables?.stockSymbol ?? '')
      const bids = state.stockLimitOrders
        .filter((order) => order.stockSymbol === symbol && order.side === 'BUY' && (order.status === 'OPEN' || order.status === 'PARTIALLY_FILLED'))
        .reduce<Record<string, number>>((acc, order) => {
          const price = order.limitPrice.toFixed(4)
          acc[price] = (acc[price] ?? 0) + Math.max(0, order.quantity - order.filledQuantity)
          return acc
        }, {})
      const asks = state.stockLimitOrders
        .filter((order) => order.stockSymbol === symbol && order.side === 'SELL' && (order.status === 'OPEN' || order.status === 'PARTIALLY_FILLED'))
        .reduce<Record<string, number>>((acc, order) => {
          const price = order.limitPrice.toFixed(4)
          acc[price] = (acc[price] ?? 0) + Math.max(0, order.quantity - order.filledQuantity)
          return acc
        }, {})
      const mapToLevels = (levels: Record<string, number>, direction: 'ASC' | 'DESC') =>
        Object.entries(levels)
          .map(([price, totalQuantity]) => ({ price: Number(price), totalQuantity }))
          .filter((row) => row.totalQuantity > 0)
          .sort((left, right) => (direction === 'ASC' ? left.price - right.price : right.price - left.price))
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            orderBook: {
              stockSymbol: symbol,
              bids: mapToLevels(bids, 'DESC'),
              asks: mapToLevels(asks, 'ASC'),
            },
          },
        }),
      })
    }

    if (query.includes('stockTradeHistory')) {
      const symbol = String(body.variables?.stockSymbol ?? '')
      const limit = Number(body.variables?.limit ?? 20)
      const rows = state.stockLimitOrderExecutions.filter((execution) => execution.stockSymbol === symbol).slice(0, Math.max(1, limit))
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: { stockTradeHistory: rows } }) })
    }

    if (query.includes('companyShareholders')) {
      const companyId = body.variables?.companyId
      const company = state.players.flatMap((candidate) => candidate.companies).find((candidate) => candidate.id === companyId)
      if (!company) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ data: { companyShareholders: null } }),
        })
      }

      const totalShares = getCompanyTotalShares(company)
      const companyShareholdings = state.shareholdings.filter((h) => h.companyId === companyId && h.shareCount > 0)
      const namedSharesTotal = companyShareholdings.reduce((sum, h) => sum + h.shareCount, 0)
      const publicFloatShares = Math.max(0, totalShares - namedSharesTotal)

      const shareholders = companyShareholdings
        .map((h) => {
          const ownerPlayer = h.ownerPlayerId ? state.players.find((p) => p.id === h.ownerPlayerId) : null
          const ownerCompany = h.ownerCompanyId ? state.players.flatMap((p) => p.companies).find((c) => c.id === h.ownerCompanyId) : null
          const holderName = ownerPlayer?.displayName ?? ownerCompany?.name ?? 'Unknown'
          const holderType = h.ownerPlayerId ? 'PERSON' : 'COMPANY'
          const ownershipRatio = totalShares > 0 ? Number((h.shareCount / totalShares).toFixed(4)) : 0

          return {
            holderName,
            holderType,
            holderPlayerId: h.ownerPlayerId ?? null,
            holderCompanyId: h.ownerCompanyId ?? null,
            shareCount: h.shareCount,
            ownershipRatio,
          }
        })
        .sort((a, b) => b.ownershipRatio - a.ownershipRatio)

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            companyShareholders: {
              companyId: company.id,
              companyName: company.name,
              totalSharesIssued: totalShares,
              publicFloatShares,
              shareholderCount: shareholders.length,
              shareholders,
            },
          },
        }),
      })
    }

    if (query.includes('stockExchangePriceHistory')) {
      const companyId = body.variables?.companyId
      const company = state.players.flatMap((candidate) => candidate.companies).find((candidate) => candidate.id === companyId)
      const priceHistory =
        (companyId ? state.stockPriceHistory[companyId] : null) ??
        (company
          ? [
              {
                companyId: company.id,
                tick: state.gameState.currentTick,
                price: computeMockSharePrice(company),
                recordedAtUtc: new Date().toISOString(),
              },
            ]
          : [])

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { stockExchangePriceHistory: priceHistory } }),
      })
    }

    if (query.includes('rankings') && !query.includes('companyRankings')) {
      const USD_RATE = 1.08
      const rankings = state.players
        .filter((p) => p.role !== 'ADMIN' && p.email !== 'government@capitalism.game')
        .map((p) => {
          const personalCash = p.personalCash ?? 0
          const sharesValue = Number(
            state.shareholdings
              .filter((holding) => holding.ownerPlayerId === p.id && holding.shareCount > 0)
              .reduce((total, holding) => {
                const company = state.players.flatMap((candidate) => candidate.companies).find((candidate) => candidate.id === holding.companyId)

                if (!company) {
                  return total
                }

                return total + holding.shareCount * computeMockSharePrice(company)
              }, 0)
              .toFixed(2),
          )

          const totalWealth = Number((personalCash + sharesValue).toFixed(2))
          const totalWealthUsd = Number((totalWealth * USD_RATE).toFixed(2))

          return {
            playerId: p.id,
            displayName: p.displayName,
            personalAccountName: p.personalAccountName ?? p.displayName,
            personalCash,
            sharesValue,
            totalWealth,
            totalWealthUsd,
            companyCount: p.companies.length,
            badgeTypes: (state.playerBadges[p.id] ?? []).slice(0, 3).map((badge) => badge.badgeType),
          }
        })
        .sort((a, b) => b.totalWealthUsd - a.totalWealthUsd)
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { rankings } }),
      })
    }

    if (query.includes('companyRankings')) {
      const USD_RATE = 1.08
      const companyRankings = state.players
        .filter((player) => player.role !== 'ADMIN' && player.email !== 'government@capitalism.game')
        .flatMap((player) =>
          player.companies.map((company) => {
            const buildingValue = company.buildings.reduce((sum, building) => {
              const baseValues: Record<string, number> = {
                MINE: 250000,
                FACTORY: 200000,
                SALES_SHOP: 150000,
                RESEARCH_DEVELOPMENT: 300000,
                APARTMENT: 400000,
                COMMERCIAL: 350000,
                MEDIA_HOUSE: 500000,
                BANK: 600000,
                EXCHANGE: 450000,
                POWER_PLANT: 350000,
              }
              return sum + (baseValues[building.type] ?? 0) * building.level
            }, 0)
            const inventoryValue = 0
            const totalWealth = company.cash + buildingValue + inventoryValue
            const totalWealthUsd = Number((totalWealth * USD_RATE).toFixed(2))

            return {
              companyId: company.id,
              companyName: company.name,
              playerId: player.id,
              ownerDisplayName: player.personalAccountName ?? player.displayName,
              ownerPersonalAccountName: player.personalAccountName ?? player.displayName,
              currencyCode: 'EUR',
              cash: company.cash,
              buildingValue,
              inventoryValue,
              totalWealth,
              totalWealthUsd,
              buildingCount: company.buildings.length,
            }
          }),
        )
        .sort((left, right) => right.totalWealthUsd - left.totalWealthUsd)

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { companyRankings } }),
      })
    }

    if (query.includes('playerBadges') && !query.includes('playerRankHistory')) {
      const targetPlayerId = body.variables?.playerId as string | undefined
      const badges = (targetPlayerId ? state.playerBadges[targetPlayerId] : null) ?? []
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { playerBadges: badges } }),
      })
    }

    if (query.includes('playerProfile')) {
      const targetPlayerId = body.variables?.playerId as string | undefined
      const targetPlayer = state.players.find((p) => p.id === targetPlayerId)
      if (!targetPlayer) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ data: { playerProfile: null } }),
        })
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            playerProfile: {
              playerId: targetPlayer.id,
              displayName: targetPlayer.displayName,
              bio: null,
              createdAtUtc: '2024-01-01T00:00:00Z',
              joinGameYear: 2024,
              hasProSubscription: targetPlayer.hasProSubscription ?? false,
              totalWealthUsd: 500000,
              totalCompanyEquityUsd: 300000,
              companyCount: targetPlayer.companies.length,
              leaderboardRank: 1,
              activeBuildingTypes: ['FACTORY', 'SALES_SHOP'],
              citiesWithBuildings: 1,
              totalProductsSold: 1000,
              hallOfFame: {
                highestSingleTickRevenue: 25000,
                highestSingleTickRevenueTick: 42,
                largestBuildingAcquisitionPrice: 150000,
                largestBuildingAcquisitionName: 'Acme Factory',
                highestBrandQuality: 0.78,
                highestBrandQualityName: 'Acme Brand',
                accountAgeTicks: 100,
              },
            },
          },
        }),
      })
    }

    // updatePlayerBio mutation
    if (query.includes('updatePlayerBio') && !query.includes('playerProfile')) {
      if (!state.currentUserId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Not authenticated.' }] }),
        })
      }
      const bio = (body.variables?.bio as string | null) ?? null
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            updatePlayerBio: {
              playerId: state.currentUserId,
              bio,
            },
          },
        }),
      })
    }

    // updateDisplayName mutation
    if (query.includes('updatePersonalAccountName')) {
      if (!state.currentUserId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Not authenticated.' }] }),
        })
      }

      const personalAccountName = (body.variables?.input?.personalAccountName as string | undefined)?.trim() ?? ''
      if (!personalAccountName) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [{ message: 'Personal account name is required.', extensions: { code: 'PERSONAL_ACCOUNT_NAME_REQUIRED' } }],
          }),
        })
      }

      const duplicate = state.players.some((player) => player.id !== state.currentUserId && (player.personalAccountName ?? player.displayName).toLowerCase() === personalAccountName.toLowerCase())
      if (duplicate) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [{ message: 'This personal account name is already taken.', extensions: { code: 'PERSONAL_ACCOUNT_NAME_NOT_UNIQUE' } }],
          }),
        })
      }

      const player = state.players.find((p) => p.id === state.currentUserId)
      if (player) {
        player.displayName = personalAccountName
        player.personalAccountName = personalAccountName
      }

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            updatePersonalAccountName: {
              playerId: state.currentUserId,
              personalAccountName,
            },
          },
        }),
      })
    }

    // updateDisplayName mutation
    if (query.includes('updateDisplayName')) {
      if (!state.currentUserId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Not authenticated.' }] }),
        })
      }
      const displayName = (body.variables?.displayName as string | undefined)?.trim() ?? ''
      if (!displayName) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [{ message: 'Display name is required.', extensions: { code: 'DISPLAY_NAME_REQUIRED' } }],
          }),
        })
      }
      // Persist the updated name in the mock state
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (player) {
        player.displayName = displayName
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            updateDisplayName: {
              playerId: state.currentUserId,
              displayName,
            },
          },
        }),
      })
    }

    if (query.includes('playerRankHistory') || query.includes('rankHistory')) {
      const targetPlayerId = body.variables?.playerId as string | undefined
      const snapshots = (targetPlayerId ? state.playerRankSnapshots[targetPlayerId] : null) ?? []
      const responseField = query.includes('rankHistory') ? 'rankHistory' : 'playerRankHistory'
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { [responseField]: snapshots } }),
      })
    }

    if (query.includes('generateStatsExport')) {
      if (!state.currentUserId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Not authenticated.' }] }),
        })
      }
      const player = state.players.find((p) => p.id === state.currentUserId)
      const format = (body.variables?.i?.format as string) ?? 'CSV'
      const dateStr = new Date().toISOString().slice(0, 10)
      const name = (player?.displayName ?? 'Player').replace(/\s+/g, '_')
      const fileName = `${name}_Stats_${dateStr}.${format === 'HTML' ? 'html' : 'csv'}`
      // Return a minimal base64-encoded stub (a single CSV line).
      const content = format === 'HTML' ? `<html><body><h1>${player?.displayName ?? 'Player'} Stats</h1></body></html>` : `Player,${player?.displayName ?? 'Player'}\nExported,${dateStr}`
      const contentBase64 = btoa(content)
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { generateStatsExport: { format, fileName, contentBase64 } } }),
      })
    }

    if (query.includes('gameState') && !query.includes('companyLedger')) {
      applyDueBuildingUpgrades(state)
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { gameState: buildMockGameStatePayload(state.gameState) } }),
      })
    }

    if (query.includes('getCurrentEconomicCycle')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { getCurrentEconomicCycle: state.economicCycle } }),
      })
    }

    if (query.includes('getActiveMarketEvents')) {
      const cityId = body.variables?.cityId ?? null
      const events = cityId ? (state.activeMarketEvents ?? []).filter((event) => event.affectedCityId == null || event.affectedCityId === cityId) : (state.activeMarketEvents ?? [])
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { getActiveMarketEvents: events } }),
      })
    }

    if (query.includes('getEconomicHistory')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { getEconomicHistory: state.economicHistory ?? [] } }),
      })
    }

    if (query.includes('endgameStatus')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { endgameStatus: state.endgameStatus } }),
      })
    }

    if (query.includes('myPendingActions')) {
      if (!state.currentUserId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Not authenticated' }] }),
        })
      }
      applyDueBuildingUpgrades(state)
      const player = state.players.find((p) => p.id === state.currentUserId)
      const currentTick = state.gameState.currentTick
      const pendingActions = (player?.companies ?? [])
        .flatMap((company) =>
          company.buildings
            .filter((building) => building.pendingConfiguration != null && building.pendingConfiguration.appliesAtTick > currentTick)
            .map((building) => ({
              id: building.pendingConfiguration!.id,
              actionType: 'BUILDING_UPGRADE',
              buildingId: building.id,
              buildingName: building.name,
              buildingType: building.type,
              submittedAtUtc: building.pendingConfiguration!.submittedAtUtc,
              submittedAtTick: building.pendingConfiguration!.submittedAtTick,
              appliesAtTick: building.pendingConfiguration!.appliesAtTick,
              ticksRemaining: building.pendingConfiguration!.appliesAtTick - currentTick,
              totalTicksRequired: building.pendingConfiguration!.totalTicksRequired,
            })),
        )
        .sort((a, b) => a.appliesAtTick - b.appliesAtTick)
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { myPendingActions: pendingActions } }),
      })
    }

    if (query.includes('procurementPreview')) {
      const unitId: string = body.variables?.unitId ?? body.variables?.buildingUnitId ?? ''
      const customPreview = state.procurementPreviews[unitId]
      const defaultPreview = {
        sourceType: 'GLOBAL_EXCHANGE',
        sourceCityId: 'city-ba',
        sourceCityName: 'Bratislava',
        sourceVendorCompanyId: null,
        sourceVendorName: null,
        exchangePricePerUnit: 8.5,
        transitCostPerUnit: 1.2,
        deliveredPricePerUnit: 9.7,
        estimatedQuality: 0.7,
        canExecute: true,
        blockReason: null,
        blockMessage: null,
      }
      const procurementPreview = customPreview !== undefined ? customPreview : defaultPreview
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { procurementPreview } }),
      })
    }

    if (query.includes('sourcingCandidates')) {
      const unitId: string = body.variables?.unitId ?? body.variables?.buildingUnitId ?? ''
      const custom = state.sourcingCandidates[unitId]
      const defaultCandidates = [
        {
          sourceType: 'GLOBAL_EXCHANGE',
          sourceCityId: 'city-ba',
          sourceCityName: 'Bratislava',
          sourceVendorCompanyId: null,
          sourceVendorName: null,
          exchangePricePerUnit: 8.5,
          transitCostPerUnit: 0.01,
          deliveredPricePerUnit: 8.51,
          estimatedQuality: 0.7,
          distanceKm: 0,
          isEligible: true,
          blockReason: null,
          blockMessage: null,
          isRecommended: true,
          rank: 1,
        },
        {
          sourceType: 'GLOBAL_EXCHANGE',
          sourceCityId: 'city-pr',
          sourceCityName: 'Prague',
          sourceVendorCompanyId: null,
          sourceVendorName: null,
          exchangePricePerUnit: 7.2,
          transitCostPerUnit: 2.8,
          deliveredPricePerUnit: 10.0,
          estimatedQuality: 0.82,
          distanceKm: 310,
          isEligible: true,
          blockReason: null,
          blockMessage: null,
          isRecommended: false,
          rank: 2,
        },
        {
          sourceType: 'GLOBAL_EXCHANGE',
          sourceCityId: 'city-vi',
          sourceCityName: 'Vienna',
          sourceVendorCompanyId: null,
          sourceVendorName: null,
          exchangePricePerUnit: 9.8,
          transitCostPerUnit: 0.15,
          deliveredPricePerUnit: 9.95,
          estimatedQuality: 0.65,
          distanceKm: 55,
          isEligible: true,
          blockReason: null,
          blockMessage: null,
          isRecommended: false,
          rank: 3,
        },
      ]
      const sourcingCandidates = custom !== undefined ? custom : defaultCandidates
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { sourcingCandidates } }),
      })
    }

    if (query.includes('cityLots')) {
      const cityId = body.variables?.cityId
      const cityLots = state.buildingLots.filter((lot) => lot.cityId === cityId)
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { cityLots } }),
      })
    }

    if (query.includes('cityWeatherForecast')) {
      const cityId = body.variables?.cityId
      const cityWeatherForecast = cityId ? (state.cityWeatherForecasts[cityId] ?? buildDefaultCityWeatherForecast(cityId, state.gameState.currentTick)) : null
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { cityWeatherForecast } }),
      })
    }

    if (query.includes('cityPowerBalance')) {
      const cityId = body.variables?.cityId
      const allBuildings = state.players.flatMap((p) => p.companies.flatMap((c) => c.buildings)).filter((b) => b.cityId === cityId)
      const powerPlants = allBuildings.filter((b) => b.type === 'POWER_PLANT')
      const consumers = allBuildings.filter((b) => b.type !== 'POWER_PLANT')
      const defaultOutputByType: Record<string, number> = { COAL: 50, GAS: 40, SOLAR: 20, WIND: 25, NUCLEAR: 200 }
      const weather = cityId ? (state.cityWeatherForecasts[cityId] ?? buildDefaultCityWeatherForecast(cityId, state.gameState.currentTick)) : null
      const renewableFactorByType: Record<string, number> = {
        SOLAR: (weather?.currentSolarPercent ?? 100) / 100,
        WIND: (weather?.currentWindPercent ?? 100) / 100,
      }
      const totalSupplyMw = powerPlants.reduce((sum, b) => {
        const plantType = b.powerPlantType ?? 'COAL'
        const baseOutput = b.powerOutput ?? defaultOutputByType[plantType] ?? 30
        const factor = renewableFactorByType[plantType] ?? 1
        return sum + baseOutput * factor
      }, 0)
      const totalDemandMw = consumers.reduce((sum, b) => sum + b.powerConsumption, 0)
      const reserveMw = totalSupplyMw - totalDemandMw
      const reservePercent = totalDemandMw > 0 ? Math.round((reserveMw / totalDemandMw) * 1000) / 10 : 100
      let balanceStatus = 'BALANCED'
      if (totalDemandMw > 0 && totalSupplyMw < totalDemandMw) {
        balanceStatus = totalSupplyMw >= totalDemandMw * 0.5 ? 'CONSTRAINED' : 'CRITICAL'
      }
      const cityPowerBalance = {
        cityId,
        totalSupplyMw,
        totalDemandMw,
        reserveMw,
        reservePercent,
        status: balanceStatus,
        powerPlants: powerPlants.map((b) => ({
          buildingId: b.id,
          buildingName: b.name,
          plantType: b.powerPlantType ?? 'COAL',
          outputMw: (() => {
            const plantType = b.powerPlantType ?? 'COAL'
            const baseOutput = b.powerOutput ?? defaultOutputByType[plantType] ?? 30
            const factor = renewableFactorByType[plantType] ?? 1
            return Math.round(baseOutput * factor * 10) / 10
          })(),
          powerStatus: b.powerStatus ?? 'POWERED',
        })),
        powerPlantCount: powerPlants.length,
        consumerBuildingCount: consumers.length,
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { cityPowerBalance } }),
      })
    }

    if (query.includes('getCityEconomicReport') && !query.includes('cityPowerBalance')) {
      const cityId = body.variables?.cityId as string | undefined
      const cityReports = cityId ? (state.cityEconomicReports?.[cityId] ?? []) : []
      const latest = cityReports.length > 0 ? cityReports[cityReports.length - 1] : null
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            getCityEconomicReport: { latest, history: cityReports },
          },
        }),
      })
    }

    if (query.includes('cityMediaHouses')) {
      const cityId = body.variables?.cityId as string | undefined
      const ownerCompanyId = body.variables?.ownerCompanyId as string | undefined
      const allHouses: MockCityMediaHouseInfo[] = cityId ? (state.cityMediaHouses[cityId] ?? makeDefaultGovernmentMediaHouses(cityId, state)) : []
      // Sort: player-owned first, then by contentRanking desc
      const sorted = [...allHouses].sort((a, b) => {
        const aOwn = ownerCompanyId && a.ownerCompanyId === ownerCompanyId ? 0 : 1
        const bOwn = ownerCompanyId && b.ownerCompanyId === ownerCompanyId ? 0 : 1
        if (aOwn !== bOwn) return aOwn - bOwn
        return b.contentRanking - a.contentRanking
      })
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { cityMediaHouses: sorted } }),
      })
    }

    if (query.includes('mediaHouseStats')) {
      const buildingId = body.variables?.buildingId as string | undefined
      const player = state.players.find((p) => p.id === state.currentUserId)
      const building = player?.companies.flatMap((company) => company.buildings).find((candidate) => candidate.id === buildingId)

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            mediaHouseStats: building
              ? {
                  buildingId: building.id,
                  currentBoostDelivered: building.isAdvertisingActive ? 0.25 : 0,
                  campaignCostThisTaxCycle: building.isAdvertisingActive ? 450 : 0,
                  estimatedSalesImpact: building.isAdvertisingActive ? 1250 : 0,
                  boostHistory: [
                    { tick: Math.max(0, state.gameState.currentTick - 2), boost: building.isAdvertisingActive ? 0.2 : 0 },
                    { tick: Math.max(0, state.gameState.currentTick - 1), boost: building.isAdvertisingActive ? 0.22 : 0 },
                    { tick: state.gameState.currentTick, boost: building.isAdvertisingActive ? 0.25 : 0 },
                  ],
                  units: [
                    {
                      id: 'mh-unit-1',
                      targetCompanyId: building.companyId,
                      targetCompanyName: player?.companies.find((c) => c.id === building.companyId)?.name ?? 'Target',
                      mediaType: building.mediaType ?? 'NEWSPAPER',
                      campaignBudgetPerTick: building.contentBudgetPerTick ?? 0,
                      brandQualityBoostPerTick: building.isAdvertisingActive ? 0.25 : 0,
                      isActive: Boolean(building.isAdvertisingActive),
                      laborCostPerTick: 25,
                      energyCostPerTick: 10,
                    },
                  ],
                }
              : null,
          },
        }),
      })
    }

    if (query.includes('GetLot') || (query.includes('lot(') && !query.includes('cityLots'))) {
      const id = body.variables?.id
      const lot = state.buildingLots.find((l) => l.id === id)
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { lot: lot ?? null } }),
      })
    }

    if (query.includes('GetCity') || (query.includes('city(') && !query.includes('cities'))) {
      const id = body.variables?.id
      const city = state.cities.find((c) => c.id === id)
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { city: city ?? null } }),
      })
    }

    if (query.includes('cities') && !query.includes('additionalCompanyPrerequisites')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { cities: state.cities } }),
      })
    }

    if (query.includes('fxRates') && !query.includes('fxRateHistory')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { fxRates: state.fxRates } }),
      })
    }

    if (query.includes('fxRateHistory')) {
      const vars = body.variables as { quoteCurrencyCode?: string; ticksBack?: number } | undefined
      const quoteCurrency = vars?.quoteCurrencyCode ?? ''
      const ticksBack = vars?.ticksBack ?? 100
      const matching = state.fxRateHistorySnapshots.filter((s) => s.quoteCurrencyCode === quoteCurrency).slice(-ticksBack)
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { fxRateHistory: matching } }),
      })
    }

    // Forex exchange handlers
    if (query.includes('forexQuote') && !query.includes('executeForexSwap')) {
      if (!state.currentUserId) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const vars = body.variables?.input as { fromCurrencyCode: string; toCurrencyCode: string; amount: number; fromBankAccountId?: string; toBankAccountId?: string } | undefined
      const player = state.players.find((p) => p.id === state.currentUserId)
      let availableBalance: number
      if (vars?.fromBankAccountId) {
        const acc = state.myBankAccounts.find((a) => a.id === vars.fromBankAccountId)
        availableBalance = acc?.balance ?? 0
      } else {
        availableBalance = player ? player.personalCash : 0
      }
      const feeAmount = Math.round((vars?.amount ?? 0) * 0.01 * 10000) / 10000
      const netAmount = (vars?.amount ?? 0) - feeAmount
      const fromRate = state.fxRates.find((r) => r.quoteCurrencyCode === vars?.fromCurrencyCode)
      const toRate = state.fxRates.find((r) => r.quoteCurrencyCode === vars?.toCurrencyCode)
      const eurFromRate = fromRate?.rate ?? 1
      const eurToRate = toRate?.rate ?? 1.1
      const rate = vars?.fromCurrencyCode === 'EUR' ? eurToRate : vars?.toCurrencyCode === 'EUR' ? 1 / eurFromRate : eurToRate / eurFromRate
      const toAmount = Math.round(netAmount * rate * 10000) / 10000
      const fromSymbol = vars?.fromCurrencyCode === 'EUR' ? '€' : (fromRate?.quoteCurrencySymbol ?? vars?.fromCurrencyCode ?? '')
      const toSymbol = vars?.toCurrencyCode === 'EUR' ? '€' : (toRate?.quoteCurrencySymbol ?? vars?.toCurrencyCode ?? '')
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            forexQuote: {
              fromCurrencyCode: vars?.fromCurrencyCode ?? 'EUR',
              toCurrencyCode: vars?.toCurrencyCode ?? 'CZK',
              fromAmount: vars?.amount ?? 0,
              toAmount,
              feeAmount,
              feePercent: 1,
              rate: Math.round(rate * 1000000) / 1000000,
              availableFromBalance: availableBalance,
              fromCurrencySymbol: fromSymbol,
              toCurrencySymbol: toSymbol,
              quoteNonce: 'mock-nonce-' + Math.random().toString(36).slice(2),
              quotedAtUtc: new Date().toISOString(),
              quoteExpiresInSeconds: 30,
            },
          },
        }),
      })
    }

    if (query.includes('playerCurrencyBalances')) {
      if (!state.currentUserId) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const player = state.players.find((p) => p.id === state.currentUserId)
      const eurBalance = { currencyCode: 'EUR', currencySymbol: '€', balance: player?.personalCash ?? 0 }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: { playerCurrencyBalances: [eurBalance, ...state.playerCurrencyBalances] },
        }),
      })
    }

    if (query.includes('myBankAccounts') && !query.includes('executeForexSwap')) {
      if (!state.currentUserId) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const currentPlayer = state.players.find((p) => p.id === state.currentUserId)
      const normalizedAccounts = state.myBankAccounts.map((account) => normalizeMockBankAccount(account, currentPlayer?.displayName ?? 'Personal Account'))
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { myBankAccounts: normalizedAccounts } }),
      })
    }

    if (query.includes('forexTradeHistory') && !query.includes('executeForexSwap')) {
      if (!state.currentUserId) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { forexTradeHistory: state.forexTradeHistory } }),
      })
    }

    if (query.includes('eurFxRates') && !query.includes('executeForexSwap') && !query.includes('forexTradeHistory')) {
      // Public endpoint — no auth required
      const SPREAD = 0.005
      const eurRates = [
        { currencyCode: 'EUR', rate: 1, midRate: 1, buyRate: 1, sellRate: 1 },
        ...(state.fxRates ?? []).map((r) => ({
          currencyCode: r.quoteCurrencyCode,
          rate: r.rate,
          midRate: r.rate,
          buyRate: r.rate * (1 + SPREAD),
          sellRate: r.rate * (1 - SPREAD),
        })),
      ]
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { eurFxRates: eurRates } }),
      })
    }

    if (query.includes('executeForexSwap')) {
      if (!state.currentUserId) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const vars = body.variables?.input as { fromCurrencyCode: string; toCurrencyCode: string; amount: number; fromBankAccountId?: string; toBankAccountId?: string } | undefined
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) return routeJsonError('Player not found', 'PLAYER_NOT_FOUND')
      const fromCode = vars?.fromCurrencyCode ?? 'EUR'
      const toCode = vars?.toCurrencyCode ?? 'CZK'
      const amount = vars?.amount ?? 0

      // Check balance from bank account or personal wallet
      let availableBalance: number
      if (vars?.fromBankAccountId) {
        const acc = state.myBankAccounts.find((a) => a.id === vars.fromBankAccountId)
        if (!acc) return routeJsonError('Source bank account not found or you do not own it.', 'ACCOUNT_NOT_FOUND')
        if (acc.currencyCode !== fromCode) return routeJsonError(`Source bank account currency (${acc.currencyCode}) does not match the requested from-currency (${fromCode}).`, 'CURRENCY_MISMATCH')
        availableBalance = acc.balance
      } else {
        availableBalance = fromCode === 'EUR' ? player.personalCash : (state.playerCurrencyBalances.find((b) => b.currencyCode === fromCode)?.balance ?? 0)
      }
      if (amount > availableBalance) return routeJsonError('Insufficient balance.', 'INSUFFICIENT_FUNDS')

      const feeAmount = Math.round(amount * 0.01 * 10000) / 10000
      const netAmount = amount - feeAmount
      const fromRate = state.fxRates.find((r) => r.quoteCurrencyCode === fromCode)
      const toRate = state.fxRates.find((r) => r.quoteCurrencyCode === toCode)
      const eurFromRate = fromRate?.rate ?? 1
      const eurToRate = toRate?.rate ?? 1.1
      const rate = fromCode === 'EUR' ? eurToRate : toCode === 'EUR' ? 1 / eurFromRate : eurToRate / eurFromRate
      const toAmount = Math.round(netAmount * rate * 10000) / 10000

      // Update balances
      if (vars?.fromBankAccountId) {
        const acc = state.myBankAccounts.find((a) => a.id === vars.fromBankAccountId)
        if (acc) acc.balance -= amount
      } else if (fromCode === 'EUR') {
        player.personalCash -= amount
      } else {
        const bal = state.playerCurrencyBalances.find((b) => b.currencyCode === fromCode)
        if (bal) bal.balance -= amount
      }

      if (vars?.toBankAccountId) {
        const toAcc = state.myBankAccounts.find((a) => a.id === vars.toBankAccountId)
        if (toAcc) toAcc.balance += toAmount
      } else if (toCode === 'EUR') {
        player.personalCash += toAmount
      } else {
        let toBal = state.playerCurrencyBalances.find((b) => b.currencyCode === toCode)
        if (!toBal) {
          const toSym = toRate?.quoteCurrencySymbol ?? toCode
          toBal = { currencyCode: toCode, currencySymbol: toSym, balance: 0 }
          state.playerCurrencyBalances.push(toBal)
        }
        toBal.balance += toAmount
      }

      const fromSymbol = fromCode === 'EUR' ? '€' : (fromRate?.quoteCurrencySymbol ?? fromCode)
      const toSymbol = toCode === 'EUR' ? '€' : (toRate?.quoteCurrencySymbol ?? toCode)

      const tradeEntry = {
        id: crypto.randomUUID(),
        fromCurrencyCode: fromCode,
        toCurrencyCode: toCode,
        fromAmount: amount,
        toAmount,
        feeAmount,
        rate: Math.round(rate * 1000000) / 1000000,
        executedAtTick: 100,
        executedAtUtc: new Date().toISOString(),
        fromCurrencySymbol: fromSymbol,
        toCurrencySymbol: toSymbol,
      }
      state.forexTradeHistory.unshift(tradeEntry)

      let newFromBalance: number
      if (vars?.fromBankAccountId) {
        newFromBalance = state.myBankAccounts.find((a) => a.id === vars.fromBankAccountId)?.balance ?? 0
      } else {
        newFromBalance = fromCode === 'EUR' ? player.personalCash : (state.playerCurrencyBalances.find((b) => b.currencyCode === fromCode)?.balance ?? 0)
      }
      let newToBalance: number
      if (vars?.toBankAccountId) {
        newToBalance = state.myBankAccounts.find((a) => a.id === vars.toBankAccountId)?.balance ?? 0
      } else {
        newToBalance = toCode === 'EUR' ? player.personalCash : (state.playerCurrencyBalances.find((b) => b.currencyCode === toCode)?.balance ?? 0)
      }

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            executeForexSwap: {
              tradeId: tradeEntry.id,
              fromCurrencyCode: fromCode,
              toCurrencyCode: toCode,
              fromAmount: amount,
              toAmount,
              feeAmount,
              rate: tradeEntry.rate,
              newFromBalance,
              newToBalance,
              fromCurrencySymbol: fromSymbol,
              toCurrencySymbol: toSymbol,
            },
          },
        }),
      })
    }

    if (query.includes('bankStatement') && !query.includes('executeForexSwap')) {
      if (!state.currentUserId) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const vars = body.variables as { companyId?: string; accountId?: string; limit?: number; offset?: number } | undefined
      const accountId = vars?.accountId ?? null
      const companyId = vars?.companyId ?? ''
      const limit = vars?.limit ?? 50
      const offset = vars?.offset ?? 0
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) return routeJsonError('Player not found', 'PLAYER_NOT_FOUND')

      // Handle personal bank account statement (PERSON-type account keyed by accountId)
      const personalAccount = accountId ? state.myBankAccounts.find((acc) => acc.id === accountId && (acc.ownerType === 'PERSON' || acc.companyId === null)) : null
      if (personalAccount) {
        const allRows = state.personalBankStatementRows
        const rows = allRows.slice(offset, offset + limit)
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            data: {
              bankStatement: {
                companyId: null,
                companyName: player.displayName,
                currencyCode: personalAccount.currencyCode,
                currencySymbol: personalAccount.currencySymbol,
                currentBalance: personalAccount.balance,
                totalEntries: allRows.length,
                rows,
              },
            },
          }),
        })
      }

      const company = player.companies.find((c) => c.id === companyId)
      if (!company) return routeJsonError('Company not found or you do not own it.', 'COMPANY_NOT_FOUND')
      const allRows = state.bankStatementRows[companyId] ?? []
      const rows = allRows.slice(offset, offset + limit)
      const currentBalance = allRows.reduce((sum, r) => sum + r.amount, 0)
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            bankStatement: {
              companyId: company.id,
              companyName: company.name,
              currencyCode: 'EUR',
              currencySymbol: '€',
              currentBalance,
              totalEntries: allRows.length,
              rows,
            },
          },
        }),
      })
    }

    // Gold AMM handlers
    if (query.includes('goldAmmPools') && !query.includes('addGoldAmmLiquidity') && !query.includes('createGoldAmmPool') && !query.includes('removeGoldAmmLiquidity')) {
      if (!state.currentUserId) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { goldAmmPools: state.goldAmmPools } }),
      })
    }

    if (query.includes('myGoldBalance')) {
      if (!state.currentUserId) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { myGoldBalance: state.goldBalance } }),
      })
    }

    if (query.includes('goldAmmSwapQuote')) {
      if (!state.currentUserId) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const vars = body.variables?.input as { direction: string; currencyCode: string; amount: number } | undefined
      const direction = vars?.direction ?? 'FIAT_TO_GOLD'
      const currencyCode = vars?.currencyCode ?? 'EUR'
      const amount = vars?.amount ?? 0
      const pool = state.goldAmmPools.find((p) => p.currencyCode === currencyCode)
      if (!pool) return routeJsonError('No liquidity pool found for this currency pair.', 'POOL_NOT_FOUND')
      if (pool.fiatReserve <= 0 || pool.goldReserve <= 0) return routeJsonError('Pool has no liquidity.', 'NO_LIQUIDITY')
      const feeAmount = Math.round(amount * 0.01 * 1e8) / 1e8
      const netAmount = amount - feeAmount
      let outputAmount: number
      if (direction === 'FIAT_TO_GOLD') {
        outputAmount = (pool.goldReserve * netAmount) / (pool.fiatReserve + netAmount)
      } else {
        outputAmount = (pool.fiatReserve * netAmount) / (pool.goldReserve + netAmount)
      }
      const impliedPrice = direction === 'FIAT_TO_GOLD' ? amount / outputAmount : outputAmount / amount
      const slippagePercent = Math.abs((impliedPrice - pool.impliedGoldPrice) / pool.impliedGoldPrice) * 100
      const availableInputBalance = direction === 'FIAT_TO_GOLD' ? (state.playerCurrencyBalances.find((b) => b.currencyCode === currencyCode)?.balance ?? 0) : state.goldBalance.availableBalance
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            goldAmmSwapQuote: {
              direction,
              currencyCode,
              currencySymbol: pool.currencySymbol,
              inputAmount: amount,
              outputAmount: Math.round(outputAmount * 1e8) / 1e8,
              feeAmount,
              feePercent: 1,
              impliedPrice: Math.round(impliedPrice * 1e4) / 1e4,
              slippagePercent: Math.round(slippagePercent * 1e4) / 1e4,
              poolFiatReserve: pool.fiatReserve,
              poolGoldReserve: pool.goldReserve,
              availableInputBalance,
            },
          },
        }),
      })
    }

    if (query.includes('executeGoldAmmSwap')) {
      if (!state.currentUserId) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const vars = body.variables?.input as { direction: string; currencyCode: string; amount: number; minOutputAmount?: number } | undefined
      const direction = vars?.direction ?? 'FIAT_TO_GOLD'
      const currencyCode = vars?.currencyCode ?? 'EUR'
      const amount = vars?.amount ?? 0
      const pool = state.goldAmmPools.find((p) => p.currencyCode === currencyCode)
      if (!pool) return routeJsonError('No liquidity pool found for this currency pair.', 'POOL_NOT_FOUND')
      const feeAmount = Math.round(amount * 0.01 * 1e8) / 1e8
      const netAmount = amount - feeAmount
      let outputAmount: number
      if (direction === 'FIAT_TO_GOLD') {
        outputAmount = (pool.goldReserve * netAmount) / (pool.fiatReserve + netAmount)
        pool.fiatReserve += amount
        pool.goldReserve -= outputAmount
        state.goldBalance.balance += outputAmount
        state.goldBalance.availableBalance += outputAmount
        const bal = state.playerCurrencyBalances.find((b) => b.currencyCode === currencyCode)
        if (bal) bal.balance -= amount
      } else {
        outputAmount = (pool.fiatReserve * netAmount) / (pool.goldReserve + netAmount)
        pool.goldReserve += amount
        pool.fiatReserve -= outputAmount
        state.goldBalance.balance -= amount
        state.goldBalance.availableBalance -= amount
        let bal = state.playerCurrencyBalances.find((b) => b.currencyCode === currencyCode)
        if (!bal) {
          bal = { currencyCode, currencySymbol: pool.currencySymbol, balance: 0 }
          state.playerCurrencyBalances.push(bal)
        }
        bal.balance += outputAmount
      }
      pool.impliedGoldPrice = pool.fiatReserve / pool.goldReserve
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            executeGoldAmmSwap: {
              tradeId: `gold-trade-${Date.now()}`,
              direction,
              currencyCode,
              inputAmount: amount,
              outputAmount: Math.round(outputAmount * 1e8) / 1e8,
              feeAmount,
              impliedPrice: pool.impliedGoldPrice,
              newFiatBalance:
                direction === 'FIAT_TO_GOLD'
                  ? (state.playerCurrencyBalances.find((b) => b.currencyCode === currencyCode)?.balance ?? 0)
                  : (state.playerCurrencyBalances.find((b) => b.currencyCode === currencyCode)?.balance ?? 0),
              newGoldBalance: state.goldBalance.balance,
            },
          },
        }),
      })
    }

    if (query.includes('addGoldAmmLiquidity')) {
      if (!state.currentUserId) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const vars = body.variables?.input as { poolId: string; fiatAmount: number; maxGoldAmount: number } | undefined
      const pool = state.goldAmmPools.find((p) => p.id === vars?.poolId)
      if (!pool) return routeJsonError('Pool not found.', 'POOL_NOT_FOUND')
      const fiatAmount = vars?.fiatAmount ?? 0
      const goldAmount = pool.goldReserve > 0 && pool.fiatReserve > 0 ? (fiatAmount * pool.goldReserve) / pool.fiatReserve : (vars?.maxGoldAmount ?? fiatAmount)
      if (state.goldBalance.availableBalance < goldAmount) return routeJsonError('Insufficient available gold (some may be locked in pools).', 'INSUFFICIENT_GOLD')
      pool.fiatReserve += fiatAmount
      pool.goldReserve += goldAmount
      state.goldBalance.blockedInPools += goldAmount
      state.goldBalance.availableBalance -= goldAmount
      const newShares = pool.totalLiquidityShares > 0 ? (fiatAmount / pool.fiatReserve) * pool.totalLiquidityShares : 1000
      pool.totalLiquidityShares += newShares
      const posId = `pos-${pool.id}-${state.currentUserId}`
      const existingPos = pool.myPosition
      if (existingPos) {
        existingPos.liquidityShares += newShares
        existingPos.sharePercent = (existingPos.liquidityShares / pool.totalLiquidityShares) * 100
        existingPos.fiatProvided += fiatAmount
        existingPos.goldProvided += goldAmount
      } else {
        pool.myPosition = {
          id: posId,
          poolId: pool.id,
          currencyCode: pool.currencyCode,
          liquidityShares: newShares,
          sharePercent: (newShares / pool.totalLiquidityShares) * 100,
          claimableFiat: 0,
          claimableGold: 0,
          fiatProvided: fiatAmount,
          goldProvided: goldAmount,
        }
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            addGoldAmmLiquidity: {
              poolId: pool.id,
              positionId: posId,
              fiatProvided: fiatAmount,
              goldProvided: goldAmount,
              poolFiatReserve: pool.fiatReserve,
              poolGoldReserve: pool.goldReserve,
            },
          },
        }),
      })
    }

    if (query.includes('removeGoldAmmLiquidity')) {
      if (!state.currentUserId) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const vars = body.variables?.input as { positionId: string; shareFraction: number } | undefined
      const pool = state.goldAmmPools.find((p) => p.myPosition?.id === vars?.positionId)
      if (!pool || !pool.myPosition) return routeJsonError('Position not found.', 'POSITION_NOT_FOUND')
      const fraction = Math.min(1, Math.max(0, vars?.shareFraction ?? 1))
      const sharesToRemove = pool.myPosition.liquidityShares * fraction
      const fiatReturned = (sharesToRemove / pool.totalLiquidityShares) * pool.fiatReserve
      const goldReturned = (sharesToRemove / pool.totalLiquidityShares) * pool.goldReserve
      pool.fiatReserve -= fiatReturned
      pool.goldReserve -= goldReturned
      pool.totalLiquidityShares -= sharesToRemove
      pool.myPosition.liquidityShares -= sharesToRemove
      pool.myPosition.sharePercent = pool.totalLiquidityShares > 0 ? (pool.myPosition.liquidityShares / pool.totalLiquidityShares) * 100 : 0
      state.goldBalance.blockedInPools -= goldReturned
      state.goldBalance.availableBalance += goldReturned
      state.goldBalance.balance = state.goldBalance.availableBalance + state.goldBalance.blockedInPools
      if (pool.myPosition.liquidityShares <= 0) pool.myPosition = null
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            removeGoldAmmLiquidity: {
              positionId: vars?.positionId,
              fiatReturned,
              goldReturned,
              remainingShares: pool.myPosition?.liquidityShares ?? 0,
            },
          },
        }),
      })
    }

    if (query.includes('createGoldAmmPool')) {
      if (!state.currentUserId) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const vars = body.variables?.input as { currencyCode: string; fiatAmount: number; goldAmount: number } | undefined
      const currencyCode = vars?.currencyCode ?? 'EUR'
      if (state.goldAmmPools.some((p) => p.currencyCode === currencyCode)) {
        return routeJsonError('A pool for this currency already exists. Add liquidity instead.', 'POOL_ALREADY_EXISTS')
      }
      if (state.goldBalance.availableBalance < (vars?.goldAmount ?? 0)) return routeJsonError('Insufficient available gold.', 'INSUFFICIENT_GOLD')
      const fiatAmount = vars?.fiatAmount ?? 0
      const goldAmount = vars?.goldAmount ?? 0
      const poolId = `pool-${currencyCode}-${Date.now()}`
      const posId = `pos-${poolId}-${state.currentUserId}`
      const fxRate = state.fxRates.find((r) => r.quoteCurrencyCode === currencyCode)
      const currencySymbol = fxRate ? fxRate.quoteCurrencySymbol : currencyCode
      const newPool: MockGoldAmmPool = {
        id: poolId,
        currencyCode,
        currencySymbol,
        fiatReserve: fiatAmount,
        goldReserve: goldAmount,
        totalLiquidityShares: 1000,
        impliedGoldPrice: fiatAmount / goldAmount,
        myPosition: {
          id: posId,
          poolId,
          currencyCode,
          liquidityShares: 1000,
          sharePercent: 100,
          claimableFiat: 0,
          claimableGold: 0,
          fiatProvided: fiatAmount,
          goldProvided: goldAmount,
        },
      }
      state.goldAmmPools.push(newPool)
      state.goldBalance.blockedInPools += goldAmount
      state.goldBalance.availableBalance -= goldAmount
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            createGoldAmmPool: {
              poolId,
              positionId: posId,
              currencyCode,
              fiatProvided: fiatAmount,
              goldProvided: goldAmount,
              liquidityShares: 1000,
            },
          },
        }),
      })
    }

    if (query.includes('goldAmmSwapHistory')) {
      const variables = body.variables as { currencyCode?: string; myTradesOnly?: boolean; limit?: number } | undefined
      const currency = variables?.currencyCode
      const mockHistory = (state.goldAmmSwapHistory ?? []).filter((h) => !currency || h.currencyCode === currency)
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { goldAmmSwapHistory: mockHistory } }),
      })
    }

    if (query.includes('encyclopediaResource')) {
      const variables = body.variables as { slug?: string } | undefined
      const slug = variables?.slug ?? ''
      const resource = state.resourceTypes.find((r) => r.slug === slug) ?? null
      const productsUsingResource = resource ? state.productTypes.filter((p) => p.recipes.some((recipe) => recipe.resourceType?.slug === slug)) : []
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            encyclopediaResource: resource ? { resource, productsUsingResource } : null,
          },
        }),
      })
    }

    if (query.includes('resourceTypes')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { resourceTypes: state.resourceTypes } }),
      })
    }

    if (query.includes('playerNotificationInbox')) {
      if (!state.currentUserId) {
        return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      }

      const limit = Math.max(1, Math.min(200, Number(body.variables?.limit ?? 20)))
      const sorted = [...state.playerNotifications].sort((left, right) => right.createdAtUtc.localeCompare(left.createdAtUtc))
      const items = sorted.slice(0, limit)
      const unreadCount = sorted.filter((item) => !item.isRead).length
      return routeJson({
        playerNotificationInbox: {
          unreadCount,
          items,
        },
      })
    }

    if (query.includes('playerNotificationUnreadCount')) {
      if (!state.currentUserId) {
        return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      }

      return routeJson({ playerNotificationUnreadCount: state.playerNotifications.filter((item) => !item.isRead).length })
    }

    if (query.includes('markPlayerNotificationsRead')) {
      if (!state.currentUserId) {
        return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      }

      const notificationIds: string[] = body.variables?.input?.notificationIds ?? []
      if (notificationIds.length > 0) {
        state.playerNotifications = state.playerNotifications.map((item) => (notificationIds.includes(item.id) ? { ...item, isRead: true } : item))
      }
      return routeJson({ markPlayerNotificationsRead: true })
    }

    if (query.includes('markAllPlayerNotificationsRead')) {
      if (!state.currentUserId) {
        return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      }

      const changed = state.playerNotifications.filter((item) => !item.isRead).length
      if (changed > 0) {
        state.playerNotifications = state.playerNotifications.map((item) => ({ ...item, isRead: true }))
      }
      return routeJson({ markAllPlayerNotificationsRead: changed })
    }

    if (query.includes('markGameNewsRead')) {
      if (!state.currentUserId) {
        return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      }

      const entryIds: string[] = body.variables?.input?.entryIds ?? []
      state.gameNewsEntries = state.gameNewsEntries.map((entry) =>
        entryIds.includes(entry.id) && !entry.readByPlayerIds.includes(state.currentUserId ?? '')
          ? {
              ...entry,
              readByPlayerIds: [...entry.readByPlayerIds, state.currentUserId!],
            }
          : entry,
      )

      return routeJson({ markGameNewsRead: true })
    }

    if (query.includes('markAllGameNewsRead')) {
      if (!state.currentUserId) {
        return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      }

      const unreadEntries = state.gameNewsEntries.filter(
        (entry) => entry.status === 'PUBLISHED' && (entry.targetServerKey === null || entry.targetServerKey === state.serverKey) && !entry.readByPlayerIds.includes(state.currentUserId ?? ''),
      )

      if (unreadEntries.length > 0) {
        state.gameNewsEntries = state.gameNewsEntries.map((entry) =>
          unreadEntries.some((candidate) => candidate.id === entry.id)
            ? {
                ...entry,
                readByPlayerIds: [...entry.readByPlayerIds, state.currentUserId!],
              }
            : entry,
        )
      }

      return routeJson({ markAllGameNewsRead: unreadEntries.length })
    }

    if (query.includes('gameNewsFeed')) {
      const includeDrafts = Boolean(body.variables?.includeDrafts ?? query.includes('includeDrafts: true'))
      const visibleEntries = state.gameNewsEntries
        .filter((entry) => entry.targetServerKey === null || entry.targetServerKey === state.serverKey)
        .filter((entry) => includeDrafts || entry.status === 'PUBLISHED')
        .sort((left, right) => {
          const leftTimestamp = left.publishedAtUtc ?? left.updatedAtUtc
          const rightTimestamp = right.publishedAtUtc ?? right.updatedAtUtc
          return rightTimestamp.localeCompare(leftTimestamp)
        })

      const items = visibleEntries.map(buildGameNewsEntry)
      const unreadCount = items.filter((entry) => entry.status === 'PUBLISHED' && !entry.isRead).length

      return routeJson({
        gameNewsFeed: {
          unreadCount,
          items,
        },
      })
    }

    if (query.includes('cityMarketReports')) {
      const vars = body.variables ?? {}
      let reports = [...state.marketReports]
      if (vars.cityId) {
        reports = reports.filter((r) => r.cityId === vars.cityId)
      }
      if (vars.reportType) {
        reports = reports.filter((r) => r.reportType === vars.reportType)
      }
      const limit = Math.min(vars.limit ?? 10, 100)
      reports = reports.slice(0, limit)
      return routeJson({ cityMarketReports: reports })
    }

    if (query.includes('buildingMarket') && !query.includes('myBuildingListings')) {
      return routeJson({ buildingMarket: state.buildingMarketListings })
    }

    if (query.includes('myBuildingListings')) {
      return routeJson({ myBuildingListings: state.myBuildingListings })
    }

    if (query.includes('myTradeRoutes')) {
      return routeJson({ myTradeRoutes: state.tradeRoutes })
    }

    if (query.includes('getCrossCityShipments') && !query.includes('companyLedger')) {
      return routeJson({ getCrossCityShipments: state.tradeRoutes })
    }

    // ── Tutorial Progress ──────────────────────────────────────────────────────
    if (query.includes('tutorialProgress') && !query.includes('markTutorialMilestoneComplete')) {
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) {
        return routeJson({ errors: [{ message: 'Not authenticated', extensions: { code: 'UNAUTHORIZED' } }] })
      }
      return routeJson({ tutorialProgress: state.tutorialProgress })
    }

    if (query.includes('markTutorialMilestoneComplete')) {
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) {
        return routeJson({ errors: [{ message: 'Not authenticated', extensions: { code: 'UNAUTHORIZED' } }] })
      }
      const milestone: string = body.variables?.input?.milestone ?? ''
      const existing = state.tutorialProgress.find((m) => m.milestone === milestone)
      const now = new Date().toISOString()
      const pointsByMilestone: Record<string, number> = {
        FIRST_RESOURCE_SOLD: 50,
        FIRST_B2B_TRADE: 75,
        FIRST_LOAN_TAKEN: 60,
        FIRST_COMPETITOR_OBSERVED: 40,
        FIRST_BRAND_ESTABLISHED: 80,
        FIRST_BUILDING_DETAIL_VISIT: 30,
        FIRST_GRID_EDITOR_OPEN: 30,
      }
      if (existing) {
        existing.isCompleted = true
        existing.completedAtUtc = existing.completedAtUtc ?? now
        existing.bountyAwarded = pointsByMilestone[milestone] != null
        existing.bountyAwardedAtUtc = existing.bountyAwardedAtUtc ?? (existing.bountyAwarded ? now : null)
        existing.bountyPoints = existing.bountyPoints ?? pointsByMilestone[milestone] ?? null
        return routeJson({ markTutorialMilestoneComplete: { ...existing } })
      }
      const bountyPoints = pointsByMilestone[milestone] ?? null
      const newEntry = {
        milestone,
        isCompleted: true,
        completedAtUtc: now,
        bountyAwarded: bountyPoints != null,
        bountyAwardedAtUtc: bountyPoints != null ? now : null,
        bountyPoints,
      }
      state.tutorialProgress.push(newEntry)
      return routeJson({ markTutorialMilestoneComplete: { ...newEntry } })
    }

    // ── Additional Company IPO ─────────────────────────────────────────────────
    if (query.includes('additionalCompanyPrerequisites')) {
      const prereqs = state.additionalCompanyPrerequisites ?? {
        allRequirementsMet: false,
        companyCount: 0,
        underMaxCap: true,
        hasExistingCompany: false,
        companyAgeTicks: 0,
        companyAgeRequirementMet: false,
        ticksUntilAgeRequirementMet: 8760,
        netIncomeInWindow: 0,
        profitabilityRequirementMet: false,
        personalBalanceUsd: 0,
        balanceRequirementMet: false,
      }
      const responseData: Record<string, unknown> = { additionalCompanyPrerequisites: prereqs }
      if (query.includes('cities')) {
        responseData.cities = state.cities ?? []
      }
      return routeJson(responseData)
    }

    if (query.includes('startAdditionalCompany')) {
      const input = body.variables?.input ?? {}
      const companyName: string = input.companyName ?? 'New Company'
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) {
        return routeJson({ errors: [{ message: 'Not authenticated', extensions: { code: 'UNAUTHORIZED' } }] })
      }
      const prereqs = state.additionalCompanyPrerequisites
      if (prereqs && !prereqs.allRequirementsMet) {
        if (!prereqs.hasExistingCompany) {
          return routeJson({ errors: [{ message: 'No existing company', extensions: { code: 'NO_EXISTING_COMPANY' } }] })
        }
        if (!prereqs.companyAgeRequirementMet) {
          return routeJson({ errors: [{ message: 'Company too young', extensions: { code: 'COMPANY_TOO_YOUNG' } }] })
        }
        if (!prereqs.profitabilityRequirementMet) {
          return routeJson({ errors: [{ message: 'Company not profitable', extensions: { code: 'COMPANY_NOT_PROFITABLE' } }] })
        }
        if (!prereqs.balanceRequirementMet) {
          return routeJson({ errors: [{ message: 'Insufficient personal funds', extensions: { code: 'INSUFFICIENT_PERSONAL_FUNDS' } }] })
        }
        if (!prereqs.underMaxCap) {
          return routeJson({ errors: [{ message: 'Max companies reached', extensions: { code: 'MAX_COMPANIES_REACHED' } }] })
        }
      }
      // Create new company
      const newCompanyId = `company-ipo-${Date.now()}`
      const newCompany: (typeof player.companies)[0] = {
        id: newCompanyId,
        playerId: player.id,
        name: companyName,
        cash: 400000,
        foundedAtUtc: new Date().toISOString(),
        buildings: [],
      }
      player.companies.push(newCompany)
      return routeJson({ startAdditionalCompany: { id: newCompanyId, name: companyName } })
    }

    if (query.includes('makeOfferOnBuilding')) {
      const input = body.variables?.input ?? {}
      const newOffer: MockBuildingMarketOffer = {
        id: `offer-${Date.now()}`,
        offerVersion: crypto.randomUUID(),
        offeredPrice: input.offeredPrice ?? 0,
        status: 'PENDING',
        negotiationNote: input.negotiationNote ?? null,
        createdAtUtc: new Date().toISOString(),
        resolvedAtUtc: null,
        buyerPlayer: { displayName: 'Buyer' },
        buyerCompany: { id: input.buyerCompanyId ?? 'co-1', name: 'Buyer Corp' },
      }
      return routeJson({ makeOfferOnBuilding: { id: newOffer.id, offeredPrice: newOffer.offeredPrice, status: 'PENDING' } })
    }

    if (query.includes('acceptBuildingOffer')) {
      const offerId = body.variables?.input?.offerId
      const offerVersion = body.variables?.input?.offerVersion
      // Update myBuildingListings mock state
      for (const listing of state.myBuildingListings) {
        const offer = listing.offers.find((o) => o.id === offerId)
        if (offer) {
          if (offerVersion && offer.offerVersion !== offerVersion) {
            return routeJsonError('Offer version conflict', 'OFFER_VERSION_CONFLICT')
          }
          offer.status = 'ACCEPTED'
          offer.resolvedAtUtc = new Date().toISOString()
          offer.offerVersion = crypto.randomUUID()
          listing.building.isForSale = false
          break
        }
      }
      return routeJson({
        acceptBuildingOffer: {
          building: { id: 'b-1', name: 'Test Building', companyId: 'co-2', isForSale: false },
          offer: { id: offerId, status: 'ACCEPTED' },
        },
      })
    }

    if (query.includes('cancelBuildingOffer') || query.includes('rejectBuildingOffer')) {
      const offerId = body.variables?.input?.offerId
      const offerVersion = body.variables?.input?.offerVersion
      for (const listing of state.myBuildingListings) {
        const offer = listing.offers.find((o) => o.id === offerId)
        if (offer) {
          if (offerVersion && offer.offerVersion !== offerVersion) {
            return routeJsonError('Offer version conflict', 'OFFER_VERSION_CONFLICT')
          }
          offer.status = 'REJECTED'
          offer.resolvedAtUtc = new Date().toISOString()
          offer.offerVersion = crypto.randomUUID()
          break
        }
      }
      return routeJson({ rejectBuildingOffer: { id: offerId, status: 'REJECTED' } })
    }

    if (query.includes('gameAdminSession') && !query.includes('buildingBankAccount') && !query.includes('assignBuildingBankAccount') && !query.includes('createCompanyBankAccount')) {
      return routeJson({ gameAdminSession: buildGameAdminSession() })
    }

    if (query.includes('gameAdminDashboard')) {
      const accessFailure = getAdminAccessFailure(false)
      if (accessFailure) {
        return routeJsonError(accessFailure.message, accessFailure.code)
      }

      const totalPersonalCash = Number(state.players.reduce((total, player) => total + player.personalCash, 0).toFixed(2))
      const totalCompanyCash = Number(
        state.players
          .flatMap((player) => player.companies)
          .reduce((total, company) => total + company.cash, 0)
          .toFixed(2),
      )
      const inflowSummaries =
        state.adminMoneyInflowSummaries.length > 0
          ? state.adminMoneyInflowSummaries.map((summary) => ({ ...summary }))
          : [
              {
                category: 'NO_ANOMALIES',
                amount: 0,
                description: 'No exceptional money inflow is currently flagged.',
              },
            ]
      const shippingCostSummaries = state.adminShippingCostSummaries
        .map((summary) => ({ ...summary }))
        .sort((left, right) => right.amount - left.amount || left.companyName.localeCompare(right.companyName))

      const multiAccountAlerts = state.adminMultiAccountAlerts
        .map((alert) => {
          const primaryPlayer = state.players.find((player) => player.id === alert.primaryPlayerId)
          const relatedPlayer = state.players.find((player) => player.id === alert.relatedPlayerId)

          if (!primaryPlayer || !relatedPlayer) {
            return null
          }

          return {
            reason: alert.reason,
            exposureAmount: alert.exposureAmount,
            confidenceScore: alert.confidenceScore,
            supportingEntityType: alert.supportingEntityType,
            supportingEntityName: alert.supportingEntityName,
            primaryPlayer: buildGameAdminPlayer(primaryPlayer),
            relatedPlayer: buildGameAdminPlayer(relatedPlayer),
          }
        })
        .filter((alert): alert is NonNullable<typeof alert> => alert !== null)

      return routeJson({
        gameAdminDashboard: {
          serverKey: state.serverKey,
          totalPersonalCash,
          totalCompanyCash,
          moneySupply: Number((totalPersonalCash + totalCompanyCash).toFixed(2)),
          externalMoneyInflowLast100Ticks: Number(inflowSummaries.reduce((total, summary) => total + summary.amount, 0).toFixed(2)),
          totalShippingCostsLast100Ticks: Number(shippingCostSummaries.reduce((total, summary) => total + summary.amount, 0).toFixed(2)),
          inflowSummaries,
          shippingCostSummaries,
          multiAccountAlerts,
          players: state.players
            .filter((player) => player.email !== 'government@capitalism.game')
            .map(buildGameAdminPlayer)
            .sort((left, right) => left.displayName.localeCompare(right.displayName)),
          invisiblePlayers: state.players.filter((player) => player.email !== 'government@capitalism.game' && player.isInvisibleInChat).map(buildGameAdminPlayer),
          governmentPlayer: (() => {
            const govPlayer = state.players.find((player) => player.email === 'government@capitalism.game')
            return govPlayer ? buildGameAdminPlayer(govPlayer) : null
          })(),
          globalGameAdminGrants: state.globalGameAdminGrants.map((grant) => ({ ...grant })).sort((left, right) => left.email.localeCompare(right.email)),
          recentAuditLogs: [...state.adminAuditLogs].sort((left, right) => right.recordedAtUtc.localeCompare(left.recordedAtUtc)).slice(0, 12),
          realWorldBillionaires: [...state.endgameStatus.topRealWorldRichest]
            .map((item) => ({
              id: item.id,
              rank: item.rank,
              name: item.name,
              wealthUsd: item.wealthUsd,
              updatedAtUtc: new Date().toISOString(),
            }))
            .sort((left, right) => left.rank - right.rank),
        },
      })
    }

    if (query.includes('operationsStatistics')) {
      const accessFailure = getAdminAccessFailure(false)
      if (accessFailure) {
        return routeJsonError(accessFailure.message, accessFailure.code)
      }
      const selectedRange = body?.variables?.input?.range ?? 'LAST_7_DAYS'
      const playerCount = state.players.filter((p) => p.email !== 'government@capitalism.game').length
      const companyCount = state.players.flatMap((p) => p.companies).length
      const buildingCount = state.players.flatMap((p) => p.companies).reduce((sum, c) => sum + (c.buildings?.length ?? 0), 0)
      return routeJson({
        operationsStatistics: {
          currentTick: state.gameState.currentTick,
          range: selectedRange,
          windowTicks: 100,
          totalInflow: 450000,
          totalOutflow: 280000,
          netFlow: 170000,
          totalPlayerCount: playerCount,
          totalCompanyCount: companyCount,
          totalBuildingCount: buildingCount,
          inflowItems: [
            { category: 'PUBLIC_SALES', label: 'Public Sales Revenue', amount: 320000, percentage: 71.1, entryCount: 48 },
            { category: 'RENT', label: 'Rent Income', amount: 80000, percentage: 17.8, entryCount: 12 },
            { category: 'IPO', label: 'IPO Raises', amount: 50000, percentage: 11.1, entryCount: 3 },
          ],
          outflowItems: [
            { category: 'LABOR', label: 'Labor Costs', amount: 120000, percentage: 42.9, entryCount: 60 },
            { category: 'ENERGY', label: 'Energy Costs', amount: 85000, percentage: 30.4, entryCount: 40 },
            { category: 'TAX', label: 'Taxes Paid', amount: 55000, percentage: 19.6, entryCount: 20 },
            { category: 'MARKETING', label: 'Marketing Spend', amount: 20000, percentage: 7.1, entryCount: 15 },
          ],
        },
      })
    }

    if (query.includes('adminProductAnalytics')) {
      const accessFailure = getAdminAccessFailure(false)
      if (accessFailure) {
        return routeJsonError(accessFailure.message, accessFailure.code)
      }
      return routeJson({
        adminProductAnalytics: {
          currentTick: state.gameState.currentTick,
          windowTicks: 100,
          rows: [
            {
              productTypeId: 'prod-wooden-chair',
              productName: 'Wooden Chair',
              industry: 'FURNITURE',
              basePrice: 45,
              totalProduced: 1200,
              activeManufacturerCount: 3,
              totalSold: 980,
              totalRevenue: 49000,
              avgSellingPrice: 50,
              marketSize: 1400,
              activeSellerCount: 4,
              activeCityCount: 2,
              totalMaterialCost: 8000,
              totalLaborCost: 6000,
              totalEnergyCost: 3000,
              totalCost: 17000,
              marketSaturation: 45.5,
              totalMarketingSpend: 2000,
              totalResearchSpend: 1500,
              marketingScore: 62.5,
              researchScore: 48.0,
            },
            {
              productTypeId: 'prod-bread',
              productName: 'Bread',
              industry: 'FOOD_PROCESSING',
              basePrice: 3,
              totalProduced: 5000,
              activeManufacturerCount: 2,
              totalSold: 4800,
              totalRevenue: 14400,
              avgSellingPrice: 3,
              marketSize: 5300,
              activeSellerCount: 3,
              activeCityCount: 1,
              totalMaterialCost: 3000,
              totalLaborCost: 2000,
              totalEnergyCost: 1000,
              totalCost: 6000,
              marketSaturation: 62.0,
              totalMarketingSpend: 500,
              totalResearchSpend: 250,
              marketingScore: 34.0,
              researchScore: 22.0,
            },
            {
              productTypeId: 'prod-medicine',
              productName: 'Basic Medicine',
              industry: 'HEALTHCARE',
              basePrice: 50,
              totalProduced: 800,
              activeManufacturerCount: 1,
              totalSold: 750,
              totalRevenue: 37500,
              avgSellingPrice: 50,
              marketSize: 950,
              activeSellerCount: 2,
              activeCityCount: 1,
              totalMaterialCost: 10000,
              totalLaborCost: 5000,
              totalEnergyCost: 2500,
              totalCost: 17500,
              marketSaturation: 28.3,
              totalMarketingSpend: 1200,
              totalResearchSpend: 2200,
              marketingScore: 58.0,
              researchScore: 71.0,
            },
          ],
        },
      })
    }

    if (query.includes('startAdminImpersonation')) {
      const accessFailure = getAdminAccessFailure(false)
      if (accessFailure) {
        return routeJsonError(accessFailure.message, accessFailure.code)
      }

      const input = body.variables?.input
      const adminActor = resolveAdminActor()
      const targetPlayer = state.players.find((player) => player.id === input?.targetPlayerId)

      if (!adminActor || !targetPlayer) {
        return routeJsonError('Player not found.')
      }

      const targetCompany = input?.accountType === 'COMPANY' ? (targetPlayer.companies.find((company) => company.id === input?.companyId) ?? null) : null
      if (input?.accountType === 'COMPANY' && !targetCompany) {
        return routeJsonError('Company not found.')
      }

      state.impersonationSession = {
        adminActorUserId: adminActor.id,
        effectiveUserId: targetPlayer.id,
        effectiveAccountType: input?.accountType === 'COMPANY' ? 'COMPANY' : 'PERSON',
        effectiveCompanyId: targetCompany?.id ?? null,
      }

      const token = `impersonation-${adminActor.id}:${targetPlayer.id}:${state.impersonationSession.effectiveAccountType}:${state.impersonationSession.effectiveCompanyId ?? 'null'}`
      state.currentToken = token
      state.currentUserId = targetPlayer.id

      return routeJson({
        startAdminImpersonation: {
          token,
          expiresAtUtc: new Date(Date.now() + 7200000).toISOString(),
          player: buildPlayerPayload(targetPlayer),
        },
      })
    }

    if (query.includes('stopAdminImpersonation')) {
      const adminActor = resolveAdminActor()
      if (!adminActor || !state.impersonationSession) {
        return routeJsonError('No impersonation session is active.')
      }

      state.impersonationSession = null
      state.currentUserId = adminActor.id
      state.currentToken = `token-${adminActor.id}`

      return routeJson({
        stopAdminImpersonation: {
          token: state.currentToken,
          expiresAtUtc: new Date(Date.now() + 7200000).toISOString(),
          player: buildPlayerPayload(adminActor),
        },
      })
    }

    if (query.includes('setPlayerInvisibleInChat')) {
      const accessFailure = getAdminAccessFailure(false)
      if (accessFailure) {
        return routeJsonError(accessFailure.message, accessFailure.code)
      }

      const input = body.variables?.input
      const targetPlayer = state.players.find((player) => player.id === input?.playerId)
      if (!targetPlayer) {
        return routeJsonError('Player not found.')
      }

      targetPlayer.isInvisibleInChat = Boolean(input?.isInvisibleInChat)
      return routeJson({ setPlayerInvisibleInChat: buildGameAdminPlayer(targetPlayer) })
    }

    if (query.includes('setLocalGameAdminRole')) {
      const accessFailure = getAdminAccessFailure(true)
      if (accessFailure) {
        return routeJsonError(accessFailure.message, accessFailure.code)
      }

      const input = body.variables?.input
      const targetPlayer = state.players.find((player) => player.id === input?.playerId)
      if (!targetPlayer) {
        return routeJsonError('Player not found.')
      }

      targetPlayer.role = input?.isAdmin ? 'ADMIN' : 'PLAYER'
      return routeJson({ setLocalGameAdminRole: buildGameAdminPlayer(targetPlayer) })
    }

    if (query.includes('updateRealWorldBillionaire')) {
      const accessFailure = getAdminAccessFailure(false)
      if (accessFailure) {
        return routeJsonError(accessFailure.message, accessFailure.code)
      }

      const input = body.variables?.input
      const billionaire = state.endgameStatus.topRealWorldRichest.find((item) => item.id === input?.id)
      if (!billionaire) {
        return routeJsonError('Real-world billionaire benchmark not found.', 'REAL_WORLD_BENCHMARK_NOT_FOUND')
      }

      billionaire.rank = Number(input.rank)
      billionaire.name = String(input.name ?? billionaire.name)
      billionaire.wealthUsd = Number(input.wealthUsd ?? billionaire.wealthUsd)
      state.endgameStatus.topRealWorldRichest = [...state.endgameStatus.topRealWorldRichest].sort((a, b) => a.rank - b.rank)
      state.endgameStatus.winningThresholdUsd = state.endgameStatus.topRealWorldRichest[0]?.wealthUsd ?? 0

      return routeJson({
        updateRealWorldBillionaire: {
          id: billionaire.id,
          rank: billionaire.rank,
          name: billionaire.name,
          wealthUsd: billionaire.wealthUsd,
          updatedAtUtc: new Date().toISOString(),
        },
      })
    }

    if (query.includes('assignGlobalGameAdminRole')) {
      const accessFailure = getAdminAccessFailure(true)
      if (accessFailure) {
        return routeJsonError(accessFailure.message, accessFailure.code)
      }

      const input = body.variables?.input
      const adminActor = resolveAdminActor()
      const normalizedEmail = String(input?.email ?? '')
        .trim()
        .toLowerCase()

      if (!normalizedEmail || !adminActor) {
        return routeJsonError('Email is required.')
      }

      const existingGrant = state.globalGameAdminGrants.find((grant) => grant.email.toLowerCase() === normalizedEmail)
      const now = new Date().toISOString()
      const grant = existingGrant
        ? {
            ...existingGrant,
            grantedByEmail: adminActor.email,
            updatedAtUtc: now,
          }
        : {
            id: `global-admin-${Date.now()}`,
            email: normalizedEmail,
            grantedByEmail: adminActor.email,
            grantedAtUtc: now,
            updatedAtUtc: now,
          }

      state.globalGameAdminGrants = [...state.globalGameAdminGrants.filter((candidate) => candidate.email.toLowerCase() !== normalizedEmail), grant]
      return routeJson({ assignGlobalGameAdminRole: grant })
    }

    if (query.includes('removeGlobalGameAdminRole')) {
      const accessFailure = getAdminAccessFailure(true)
      if (accessFailure) {
        return routeJsonError(accessFailure.message, accessFailure.code)
      }

      const input = body.variables?.input
      const normalizedEmail = String(input?.email ?? '')
        .trim()
        .toLowerCase()
      state.globalGameAdminGrants = state.globalGameAdminGrants.filter((grant) => grant.email.toLowerCase() !== normalizedEmail)
      return routeJson({ removeGlobalGameAdminRole: true })
    }

    if (query.includes('upsertGameNewsEntry')) {
      const accessFailure = getAdminAccessFailure(false)
      if (accessFailure) {
        return routeJsonError(accessFailure.message, accessFailure.code)
      }

      const input = body.variables?.input
      const adminActor = resolveAdminActor()
      const session = buildGameAdminSession()
      if (!adminActor) {
        return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      }

      const existingEntry = state.gameNewsEntries.find((entry) => entry.id === input?.entryId)
      if (existingEntry?.targetServerKey === null && !session.isRootAdministrator && !session.hasGlobalAdminRole) {
        return routeJsonError('Only global or root administrators can edit global feed entries.', 'AUTH_NOT_AUTHORIZED')
      }

      const now = new Date().toISOString()
      const targetServerKey = existingEntry ? existingEntry.targetServerKey : session.isRootAdministrator || session.hasGlobalAdminRole ? null : state.serverKey

      const nextEntry: MockGameNewsEntry = existingEntry
        ? {
            ...existingEntry,
            entryType: input?.entryType ?? existingEntry.entryType,
            status: input?.status ?? existingEntry.status,
            updatedByEmail: adminActor.email,
            updatedAtUtc: now,
            publishedAtUtc: (input?.status ?? existingEntry.status) === 'PUBLISHED' ? (existingEntry.publishedAtUtc ?? now) : null,
            localizations: (input?.localizations ?? []).map((localization: MockGameNewsLocalization) => ({ ...localization })),
          }
        : {
            id: `news-entry-${Date.now()}`,
            entryType: input?.entryType ?? 'NEWS',
            status: input?.status ?? 'DRAFT',
            targetServerKey,
            createdByEmail: adminActor.email,
            updatedByEmail: adminActor.email,
            createdAtUtc: now,
            updatedAtUtc: now,
            publishedAtUtc: (input?.status ?? 'DRAFT') === 'PUBLISHED' ? now : null,
            localizations: (input?.localizations ?? []).map((localization: MockGameNewsLocalization) => ({ ...localization })),
            readByPlayerIds: [],
          }

      state.gameNewsEntries = [...state.gameNewsEntries.filter((entry) => entry.id !== nextEntry.id), nextEntry]
      return routeJson({ upsertGameNewsEntry: buildGameNewsEntry(nextEntry) })
    }

    if (query.includes('endShardManually')) {
      const accessFailure = getAdminAccessFailure(false)
      if (accessFailure) {
        return routeJsonError(accessFailure.message, accessFailure.code)
      }

      const input = body.variables?.input
      if (input?.reason && String(input.reason).length > 500) {
        return routeJsonError('Reason must not exceed 500 characters.', 'REASON_TOO_LONG')
      }

      // Mark the game as ended and pick a mock winner
      state.endgameStatus = {
        ...state.endgameStatus,
        gameEnded: true,
        winnerPlayerId: state.currentUserId,
        winnerDisplayName: 'Mock Winner',
        winnerCompanyName: 'Winner Corp',
        gameEndedAtUtc: new Date().toISOString(),
      }
      return routeJson({ endShardManually: { ...state.endgameStatus } })
    }

    // Helper: true if the query is a standalone `me` query
    // (not a more-specific query whose field names happen to include "me" as a substring).
    // NOTE: Many field names end in "Name" (e.g. bankBuildingName, lenderCompanyName, cityName) which contain "me" as a substring.
    // Also "payment" contains "me" (pay-me-nt). Always add exclusions here for any new query/mutation with such fields.
    const isStandaloneMeQuery = (q: string) =>
      q.includes('me') &&
      !q.includes('gameNewsFeed') &&
      !q.includes('gameAdminSession') &&
      !q.includes('gameAdminDashboard') &&
      !q.includes('companyLedger') &&
      !q.includes('gameState') &&
      !q.includes('ledgerDrillDown') &&
      !q.includes('companyBrands') &&
      !q.includes('publicSalesAnalytics') &&
      !q.includes('unitProductAnalytics') &&
      // Loan queries: field names like bankBuildingName, lenderCompanyName, cityName, paymentAmount contain 'me'
      !q.includes('loanOffers') &&
      !q.includes('myLoans') &&
      !q.includes('myLoanOffers') &&
      !q.includes('bankLoans') &&
      !q.includes('allBanks') &&
      !q.includes('myDeposits') &&
      !q.includes('bankDeposits') &&
      !q.includes('bankInfo') &&
      !q.includes('createDeposit') &&
      !q.includes('withdrawDeposit') &&
      !q.includes('openBankAccount') &&
      !q.includes('closeBankAccount') &&
      !q.includes('setBankRates') &&
      !q.includes('initiateBaseDeposit') &&
      // acceptLoan mutation response includes paymentAmount which contains 'me'
      !q.includes('acceptLoan') &&
      !q.includes('repayLoanDebt') &&
      // procurementPreview contains 'me' as substring
      !q.includes('procurementPreview') &&
      // personAccount query has companyName field which contains 'me' as substring
      !q.includes('personAccount') &&
      // myCollateralBuildings has buildingName field (ends in 'me')
      !q.includes('myCollateralBuildings') &&
      // campaignAnalytics has companyName/cityName fields that contain 'me' as substring
      !q.includes('campaignAnalytics') &&
      // buildingBankAccount query contains 'me' as substring (via cityName, buildingName response fields).
      // NOTE: JavaScript String.includes() is case-sensitive. 'buildingBankAccount' (lowercase 'b') does NOT
      // appear in 'fundBuildingBankAccount' because the camelCase prefix 'fund' makes 'Building' start with
      // uppercase 'B'. Therefore mutations with camelCase prefixes need their own explicit exclusions.
      !q.includes('buildingBankAccount') &&
      // fundBuildingBankAccount/assignBuildingBankAccount mutations contain 'me' via cityName/buildingName
      // fields. They do NOT match '!q.includes("buildingBankAccount")' above because includes() is
      // case-sensitive and 'fund'+'Building' has uppercase B — so exclude them explicitly here.
      !q.includes('fundBuildingBankAccount') &&
      !q.includes('assignBuildingBankAccount') &&
      // transferFunds mutation response includes companyName/currencySymbol/accountNumber which contain 'me' as substring
      !q.includes('transferFunds') &&
      // getMineDepletionForecast has estimatedGameDaysRemaining field which contains 'me' via 'Game'
      !q.includes('getMineDepletionForecast') &&
      !q.includes('getMineExtractionIntelligence') &&
      // endgameStatus contains 'me' as substring (endga**me**Status)
      !q.includes('endgameStatus') &&
      // market queries include cityName/productName fields which contain 'me' as substring
      !q.includes('marketOverview') &&
      !q.includes('marketPrice') &&
      !q.includes('cityDemandSummary') &&
      // cities query contains 'name' field which has 'me' as substring; not a me query
      !q.includes('cities')

    if (isStandaloneMeQuery(query)) {
      const player = resolveCurrentPlayer()
      if (!player) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Not authenticated' }] }) })
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            me: buildPlayerPayload(player),
          },
        }),
      })
    }

    if (query.includes('companyLedger')) {
      const companyId = body.variables?.companyId
      const gameYear = body.variables?.gameYear
      const summary = state.ledgerData[`${companyId}:${gameYear}`] ?? state.ledgerData[companyId]
      if (!summary) {
        const player = state.players.find((p) => p.id === state.currentUserId)
        const company = player?.companies.find((c) => c.id === companyId)
        if (!company) {
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ data: { companyLedger: null } }),
          })
        }
        const baseValues: Record<string, number> = {
          MINE: 250000,
          FACTORY: 200000,
          SALES_SHOP: 150000,
          RESEARCH_DEVELOPMENT: 300000,
          APARTMENT: 400000,
          COMMERCIAL: 350000,
          MEDIA_HOUSE: 500000,
          BANK: 600000,
          EXCHANGE: 450000,
          POWER_PLANT: 350000,
        }
        const buildingValue = company.buildings.reduce((sum, b) => sum + (baseValues[b.type] ?? 0) * b.level, 0)
        const auto: MockLedgerSummary = {
          companyId: company.id,
          companyName: company.name,
          gameYear: computeMockGameYear(state.gameState.currentTick),
          isCurrentGameYear: true,
          currentCash: company.cash,
          totalRevenue: 0,
          totalPurchasingCosts: 0,
          totalShippingCosts: 0,
          totalLaborCosts: 0,
          totalEnergyCosts: 0,
          totalMarketingCosts: 0,
          totalTaxPaid: 0,
          totalOtherCosts: 0,
          taxableIncome: 0,
          estimatedIncomeTax: 0,
          netIncome: 0,
          propertyValue: 0,
          propertyAppreciation: 0,
          buildingValue,
          inventoryValue: 0,
          totalAssets: company.cash + buildingValue,
          totalPropertyPurchases: 0,
          cashFromOperations: 0,
          cashFromInvestments: 0,
          firstRecordedTick: 0,
          lastRecordedTick: 0,
          history: [],
          buildingSummaries: [],
        }
        const autoLedger = buildMockLedgerSummaryPayload(auto, state.gameState)
        const responseData: Record<string, unknown> = { companyLedger: autoLedger }
        if (query.includes('companyCityFinancialBreakdown')) {
          responseData.companyCityFinancialBreakdown = []
        }
        if (query.includes('getCrossCityShipments')) {
          responseData.logisticsShipments = state.tradeRoutes
        }
        if (query.includes('gameState')) {
          responseData.gameState = buildMockGameStatePayload(state.gameState)
        }
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ data: responseData }),
        })
      }
      const ledger = buildMockLedgerSummaryPayload(summary, state.gameState)
      const responseData: Record<string, unknown> = { companyLedger: ledger }
      if (query.includes('companyCityFinancialBreakdown')) {
        const cityByBuildingId = new Map(
          state.players
            .flatMap((player) => player.companies)
            .flatMap((company) => company.buildings)
            .map((building) => [building.id, state.cities.find((city) => city.id === building.cityId)]),
        )
        const grouped = new Map<
          string,
          { cityId: string; cityName: string; currencyCode: string; currencySymbol: string; revenue: number; costs: number; revenueTrend: Array<{ tick: number; revenue: number }> }
        >()
        for (const buildingSummary of ledger.buildingSummaries ?? []) {
          const city = cityByBuildingId.get(buildingSummary.buildingId)
          if (!city) continue
          const existing = grouped.get(city.id) ?? {
            cityId: city.id,
            cityName: city.name,
            currencyCode: city.currencyCode ?? 'EUR',
            currencySymbol: city.currencyCode === 'CZK' ? 'Kč' : city.currencyCode === 'USD' ? '$' : city.currencyCode === 'GBP' ? '£' : city.currencyCode === 'INR' ? '₹' : '€',
            revenue: 0,
            costs: 0,
            revenueTrend: [],
          }
          existing.revenue += Number(buildingSummary.revenue ?? 0)
          existing.costs += Number(buildingSummary.costs ?? 0)
          existing.revenueTrend.push({
            tick: state.gameState.currentTick,
            revenue: Number(buildingSummary.revenue ?? 0),
          })
          grouped.set(city.id, existing)
        }
        responseData.companyCityFinancialBreakdown = Array.from(grouped.values()).map((entry) => ({
          ...entry,
          profit: entry.revenue - entry.costs,
        }))
      }
      if (query.includes('getCrossCityShipments')) {
        responseData.logisticsShipments = state.tradeRoutes
      }
      if (query.includes('gameState')) {
        responseData.gameState = buildMockGameStatePayload(state.gameState)
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: responseData }),
      })
    }

    if (query.includes('ledgerDrillDown')) {
      const companyId = body.variables?.companyId
      const category = body.variables?.category
      const gameYear = body.variables?.gameYear
      const entries = state.drillDownData[`${companyId}:${category}:${gameYear}`] ?? state.drillDownData[`${companyId}:${category}`] ?? []
      const enrichedEntries = entries.map((e) => ({
        ...e,
        currencyCode: e.currencyCode ?? 'EUR',
        currencySymbol: e.currencySymbol ?? '€',
      }))
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { ledgerDrillDown: enrichedEntries } }),
      })
    }

    if (query.includes('companyBrands') || query.includes('CompanyBrands')) {
      const companyId = body.variables?.companyId
      const brands = companyId ? (state.researchBrands[companyId] ?? []) : []
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { companyBrands: brands } }),
      })
    }

    if (query.includes('publicSalesAnalytics') || query.includes('PublicSalesAnalytics')) {
      const unitId: string = body.variables?.unitId ?? ''
      const analytics = state.publicSalesAnalytics[unitId] ?? null
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { publicSalesAnalytics: analytics } }),
      })
    }

    if (query.includes('unitProductAnalytics') || query.includes('UnitProductAnalytics')) {
      const unitId: string = body.variables?.unitId ?? ''
      const analytics = state.unitProductAnalytics[unitId] ?? null
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { unitProductAnalytics: analytics } }),
      })
    }

    if (query.includes('campaignAnalytics')) {
      const companyId: string = body.variables?.companyId ?? body.variables?.cid ?? ''
      const result = state.campaignAnalytics[companyId] ?? {
        companyId,
        windowTicks: 10,
        totalRevenue: 0,
        totalMarketingSpend: 0,
        bestPerformingCity: null,
        bestPerformingProduct: null,
        globalRecommendation: 'No sales data yet. Open a sales shop and start selling to see campaign analytics.',
        rows: [],
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { campaignAnalytics: result } }),
      })
    }

    if (query.includes('marketIntelligence') && !query.includes('marketOverview')) {
      const cityId: string = body.variables?.cityId ?? ''
      const city = state.cities.find((entry) => entry.id === cityId)
      const result = state.marketIntelligenceByCity[cityId] ?? {
        cityId,
        cityName: city?.name ?? 'Unknown City',
        dataFromTick: Math.max(0, state.gameState.currentTick - 167),
        dataToTick: state.gameState.currentTick,
        products: [],
      }

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { marketIntelligence: result } }),
      })
    }

    if (query.includes('marketOverview')) {
      const topN: number = body.variables?.topN ?? 10
      const tick = state.gameState.currentTick
      const overviewResults = state.cities.map((city) => {
        const overridden = state.marketOverviewByCityId[city.id]
        if (overridden) return overridden
        return {
          cityId: city.id,
          cityName: city.name,
          currencyCode: city.currencyCode ?? 'EUR',
          fromTick: Math.max(0, tick - 99),
          toTick: tick,
          products: (state.productTypes ?? [])
            .slice(0, topN)
            .map((pt, idx) => ({
              productTypeId: pt.id,
              productName: pt.name,
              industry: pt.industry,
              totalDemand: 500 + idx * 50,
              totalQuantitySold: 400 + idx * 40,
              satisfactionRate: 0.5 + idx * 0.05,
              averageClearingPrice: pt.basePrice ?? (10 + idx * 5),
              totalRevenue: (pt.basePrice ?? 15) * (400 + idx * 40),
              sellerCount: 1 + idx,
            })),
        }
      })
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { marketOverview: overviewResults } }),
      })
    }

    if (query.includes('cityDemandSummary')) {
      const cityId: string = body.variables?.cityId ?? ''
      const topN: number = body.variables?.topN ?? 5
      const tick = state.gameState.currentTick
      const city = state.cities.find((c) => c.id === cityId)
      const overridden = state.marketOverviewByCityId[cityId]
      const products = (overridden?.products ?? (state.productTypes ?? []).slice(0, topN).map((pt, idx) => ({
        productTypeId: pt.id,
        productName: pt.name,
        industry: pt.industry,
        totalDemand: 500 + idx * 50,
        totalQuantitySold: 400 + idx * 40,
        satisfactionRate: 0.5 + idx * 0.05,
        averageClearingPrice: pt.basePrice ?? (10 + idx * 5),
        totalRevenue: (pt.basePrice ?? 15) * (400 + idx * 40),
        sellerCount: 1 + idx,
      }))).slice(0, topN)
      const result = {
        cityId,
        cityName: city?.name ?? 'Unknown City',
        currencyCode: city?.currencyCode ?? 'EUR',
        fromTick: Math.max(0, tick - 99),
        toTick: tick,
        products,
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { cityDemandSummary: result } }),
      })
    }

    if (query.includes('marketPriceHistory') && !query.includes('marketOverview')) {
      const productTypeId: string = body.variables?.productTypeId ?? ''
      const lastNTicks: number = body.variables?.lastNTicks ?? 100
      const tick = state.gameState.currentTick
      const history = state.marketPriceHistoryByProductId[productTypeId] ?? []
      const filtered = history.filter((p) => p.tick >= tick - lastNTicks)
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { marketPriceHistory: filtered } }),
      })
    }

    if (query.includes('UpdatePublicSalesPrice') || query.includes('updatePublicSalesPrice')) {
      const input = body.variables?.input
      const unitId: string = input?.unitId ?? ''
      const newMinPrice: number = input?.newMinPrice ?? 0

      if (!state.currentUserId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Not authenticated', extensions: { code: 'AUTH_NOT_AUTHORIZED' } }] }),
        })
      }

      if (newMinPrice <= 0) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Minimum sale price must be greater than zero.', extensions: { code: 'INVALID_PRICE' } }] }),
        })
      }

      // Find the unit across all buildings owned by the current player
      const player = state.players.find((p) => p.id === state.currentUserId)
      let foundUnit: MockBuildingUnit | undefined
      for (const company of player?.companies ?? []) {
        for (const building of company.buildings ?? []) {
          const u = building.units?.find((unit) => unit.id === unitId)
          if (u) {
            foundUnit = u
            break
          }
        }
        if (foundUnit) break
      }

      if (!foundUnit) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: "Unit not found or you don't own it.", extensions: { code: 'UNIT_NOT_FOUND' } }] }),
        })
      }

      if (foundUnit.unitType !== 'PUBLIC_SALES') {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Only PUBLIC_SALES units support instant price updates.', extensions: { code: 'INVALID_UNIT_TYPE' } }] }),
        })
      }

      // Update the unit's minPrice in state
      foundUnit.minPrice = newMinPrice

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { updatePublicSalesPrice: { id: unitId, minPrice: newMinPrice } } }),
      })
    }

    // Guard: loanOffers query must not be confused with the 'me' handler (contains 'me' via no overlap here)
    if (query.includes('loanOffers') && !query.includes('myLoanOffers')) {
      const activeOffers = state.loanOffers.filter((o) => o.isActive && o.remainingCapacity > 0)
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { loanOffers: activeOffers } }),
      })
    }

    if (query.includes('myLoanOffers')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { myLoanOffers: state.loanOffers } }),
      })
    }

    if (query.includes('myLoans')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { myLoans: state.myLoans } }),
      })
    }

    if (query.includes('myCollateralBuildings')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { myCollateralBuildings: state.collateralBuildings } }),
      })
    }

    // allBanks must be checked before 'bankLoans' to avoid substring collision
    if (query.includes('allBanks')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { allBanks: state.allBanks } }),
      })
    }

    // bankInfo: look up from allBanks, then synthesize from player company buildings
    if (query.includes('bankInfo')) {
      const bankId = body.variables?.id
      const bank = state.allBanks.find((b) => b.bankBuildingId === bankId)
      if (bank) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ data: { bankInfo: bank } }),
        })
      }
      // Synthesize from player companies when the bank isn't in allBanks
      for (const player of state.players) {
        for (const company of player.companies) {
          const building = (company.buildings ?? []).find((b) => b.id === bankId && b.type === 'BANK')
          if (building) {
            return route.fulfill({
              status: 200,
              contentType: 'application/json',
              body: JSON.stringify({
                data: {
                  bankInfo: {
                    bankBuildingId: bankId,
                    bankBuildingName: building.name,
                    cityId: building.cityId,
                    cityName: 'Bratislava',
                    lenderCompanyId: company.id,
                    lenderCompanyName: company.name,
                    depositInterestRatePercent: 5,
                    lendingInterestRatePercent: 12,
                    totalDeposits: 10_000_000,
                    lendableCapacity: 9_000_000,
                    outstandingLoanPrincipal: 0,
                    availableLendingCapacity: 9_000_000,
                    baseCapitalDeposited: true,
                    centralBankDebt: 0,
                    centralBankInterestRatePercent: 2,
                    reserveRequirement: 1_000_000,
                    availableCash: 5_000_000,
                    reserveShortfall: 0,
                    liquidityStatus: 'HEALTHY',
                    cityCurrencyCode: 'EUR',
                    cityCurrencySymbol: '€',
                    baseCapitalRequirement: 10_000_000,
                  },
                },
              }),
            })
          }
        }
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { bankInfo: null } }),
      })
    }

    if (query.includes('myDeposits')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { myDeposits: state.myDeposits } }),
      })
    }

    if (query.includes('bankDeposits')) {
      const bankBuildingId = body.variables?.bankBuildingId
      const deposits = bankBuildingId ? state.myDeposits.filter((d) => d.bankBuildingId === bankBuildingId) : state.myDeposits
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { bankDeposits: deposits } }),
      })
    }

    if (query.includes('openBankAccount') || query.includes('createDeposit')) {
      const input = body.variables?.input ?? {}
      const bank = state.allBanks.find((b) => b.bankBuildingId === input.bankBuildingId)
      const currentPlayer = state.players.find((p) => p.id === state.currentUserId)
      const depositorCompanyId = input.depositorCompanyId ?? null
      const depositorCompany = depositorCompanyId ? currentPlayer?.companies.find((company) => company.id === depositorCompanyId) : null
      const ownerType: 'PERSON' | 'COMPANY' = depositorCompany ? 'COMPANY' : 'PERSON'
      const ownerDisplayName = depositorCompany?.name ?? currentPlayer?.displayName ?? 'Personal Account'
      const newDeposit: MockBankDeposit = {
        id: `deposit-${Date.now()}`,
        bankBuildingId: input.bankBuildingId ?? '',
        bankBuildingName: bank?.bankBuildingName ?? 'Bank',
        depositorCompanyId: depositorCompany?.id ?? '',
        depositorCompanyName: ownerDisplayName,
        amount: input.amount ?? 0,
        depositInterestRatePercent: bank?.depositInterestRatePercent ?? 5,
        isBaseCapital: false,
        isActive: true,
        depositedAtTick: state.gameState.currentTick,
        depositedAtUtc: new Date().toISOString(),
        totalInterestPaid: 0,
      }
      state.myDeposits.push(newDeposit)
      const currencyCode = bank?.cityCurrencyCode ?? 'EUR'
      const existingAccount = state.myBankAccounts.find((account) => {
        const normalized = normalizeMockBankAccount(account, currentPlayer?.displayName ?? 'Personal Account')
        return (
          normalized.currencyCode === currencyCode &&
          normalized.ownerType === ownerType &&
          ((ownerType === 'COMPANY' && normalized.companyId === depositorCompany?.id) || (ownerType === 'PERSON' && normalized.companyId == null))
        )
      })
      if (!existingAccount) {
        state.myBankAccounts.push({
          id: `bank-account-${Date.now()}`,
          accountNumber: String(Math.floor(Math.random() * 1e16)).padStart(16, '0'),
          currencyCode,
          currencySymbol: bank?.cityCurrencySymbol ?? currencyCode,
          balance: 0,
          companyId: depositorCompany?.id ?? null,
          companyName: depositorCompany?.name ?? null,
          ownerType,
          ownerDisplayName,
        })
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { openBankAccount: newDeposit, createDeposit: newDeposit } }),
      })
    }

    if (query.includes('topUpDeposit')) {
      const input = body.variables?.input ?? {}
      const deposit = state.myDeposits.find((d) => d.id === input.depositId && d.isActive)
      if (deposit) {
        deposit.amount += input.amount ?? 0
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { topUpDeposit: deposit ?? null } }),
      })
    }

    if (query.includes('closeBankAccount') || query.includes('withdrawDeposit')) {
      const depositId = body.variables?.input?.depositId
      const deposit = state.myDeposits.find((d) => d.id === depositId)
      if (deposit) deposit.isActive = false
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { closeBankAccount: deposit ?? null, withdrawDeposit: deposit ?? null } }),
      })
    }

    if (query.includes('closeCompanyBankAccount')) {
      const bankAccountId = body.variables?.input?.bankAccountId
      const accountIdx = state.myBankAccounts.findIndex((a) => a.id === bankAccountId)
      if (accountIdx >= 0) {
        const account = state.myBankAccounts[accountIdx]
        if (!account) {
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              errors: [{ message: 'Bank account not found or already closed.', extensions: { code: 'ACCOUNT_NOT_FOUND' } }],
            }),
          })
        }
        if (account.balance !== 0) {
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              errors: [{ message: `Balance must be zero. Current: ${account.balance}`, extensions: { code: 'NON_ZERO_BALANCE' } }],
            }),
          })
        }
        state.myBankAccounts.splice(accountIdx, 1)
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            data: {
              closeCompanyBankAccount: {
                id: bankAccountId,
                accountNumber: account.accountNumber,
                currencyCode: account.currencyCode,
                closedAtUtc: new Date().toISOString(),
              },
            },
          }),
        })
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          errors: [{ message: 'Bank account not found or already closed.', extensions: { code: 'ACCOUNT_NOT_FOUND' } }],
        }),
      })
    }

    if (query.includes('setBankRates')) {
      const input = body.variables?.input ?? {}
      const bank = state.allBanks.find((b) => b.bankBuildingId === input.bankBuildingId)
      if (bank) {
        if (input.depositInterestRatePercent !== undefined) bank.depositInterestRatePercent = input.depositInterestRatePercent
        if (input.lendingInterestRatePercent !== undefined) bank.lendingInterestRatePercent = input.lendingInterestRatePercent
      }
      // Return a full BankInfoSummary so the component can update bankInfo correctly
      const updatedBank = bank ?? {
        bankBuildingId: input.bankBuildingId,
        bankBuildingName: 'Unknown Bank',
        cityId: 'city-ba',
        cityName: 'Bratislava',
        lenderCompanyId: 'lender-company-1',
        lenderCompanyName: 'Lending Corp',
        depositInterestRatePercent: input.depositInterestRatePercent ?? 5,
        lendingInterestRatePercent: input.lendingInterestRatePercent ?? 10,
        totalDeposits: 10_000_000,
        lendableCapacity: 9_000_000,
        outstandingLoanPrincipal: 0,
        availableLendingCapacity: 9_000_000,
        baseCapitalDeposited: true,
        centralBankDebt: 0,
        centralBankInterestRatePercent: 2,
        reserveRequirement: 1_000_000,
        availableCash: 5_000_000,
        reserveShortfall: 0,
        liquidityStatus: 'HEALTHY' as const,
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { setBankRates: updatedBank } }),
      })
    }

    if (query.includes('initiateBaseDeposit')) {
      const bankBuildingId = body.variables?.bankBuildingId
      // Find the bank building in player companies and mark it as activated
      for (const player of state.players) {
        for (const company of player.companies) {
          const building = (company.buildings ?? []).find((b) => b.id === bankBuildingId && b.type === 'BANK')
          if (building) {
            // Deduct the base capital (use city-appropriate amount) from company cash
            company.cash = (company.cash ?? 0) - 10_000_000
            // Synthesise an activated BankInfoSummary
            const activatedBankInfo: MockBankInfo = {
              bankBuildingId,
              bankBuildingName: building.name,
              cityId: building.cityId,
              cityName: 'Bratislava',
              lenderCompanyId: company.id,
              lenderCompanyName: company.name,
              depositInterestRatePercent: 5,
              lendingInterestRatePercent: 12,
              totalDeposits: 10_000_000,
              lendableCapacity: 9_000_000,
              outstandingLoanPrincipal: 0,
              availableLendingCapacity: 9_000_000,
              baseCapitalDeposited: true,
              centralBankDebt: 0,
              centralBankInterestRatePercent: 2,
              reserveRequirement: 1_000_000,
              availableCash: company.cash,
              reserveShortfall: 0,
              liquidityStatus: 'HEALTHY' as const,
              cityCurrencyCode: 'EUR',
              cityCurrencySymbol: '€',
              baseCapitalRequirement: 10_000_000,
            }
            // Update or insert in allBanks so subsequent bankInfo queries return the activated state
            const existingIdx = state.allBanks.findIndex((b) => b.bankBuildingId === bankBuildingId)
            if (existingIdx >= 0) {
              state.allBanks[existingIdx] = activatedBankInfo
            } else {
              state.allBanks.push(activatedBankInfo)
            }
            return route.fulfill({
              status: 200,
              contentType: 'application/json',
              body: JSON.stringify({ data: { initiateBaseDeposit: activatedBankInfo } }),
            })
          }
        }
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          errors: [{ message: 'Bank building not found or you do not own it.', extensions: { code: 'BANK_NOT_FOUND' } }],
        }),
      })
    }

    if (query.includes('bankLoans')) {
      const bankBuildingId = body.variables?.bankBuildingId
      const loans = bankBuildingId ? state.myLoans.filter((l) => l.bankBuildingId === bankBuildingId) : state.myLoans
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { bankLoans: loans } }),
      })
    }

    if (query.includes('publishLoanOffer')) {
      const input = body.variables?.input ?? {}
      const newOffer: MockLoanOffer = {
        id: `offer-${Date.now()}`,
        bankBuildingId: input.bankBuildingId ?? '',
        bankBuildingName: 'Test Bank',
        cityId: 'city-ba',
        cityName: 'Bratislava',
        lenderCompanyId: 'test-company',
        lenderCompanyName: 'Test Co',
        annualInterestRatePercent: input.annualInterestRatePercent ?? 10,
        maxPrincipalPerLoan: input.maxPrincipalPerLoan ?? 50000,
        totalCapacity: input.totalCapacity ?? 200000,
        usedCapacity: 0,
        remainingCapacity: input.totalCapacity ?? 200000,
        durationTicks: input.durationTicks ?? 1440,
        isActive: true,
        createdAtTick: state.gameState.currentTick,
        createdAtUtc: new Date().toISOString(),
      }
      state.loanOffers.push(newOffer)
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { publishLoanOffer: newOffer } }),
      })
    }

    if (query.includes('deactivateLoanOffer')) {
      const loanOfferId = body.variables?.id
      const offer = state.loanOffers.find((o) => o.id === loanOfferId)
      if (offer) offer.isActive = false
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { deactivateLoanOffer: offer ?? null } }),
      })
    }

    if (query.includes('acceptLoan')) {
      const input = body.variables?.input ?? {}
      const offer = state.loanOffers.find((o) => o.id === input.loanOfferId)
      const directBank = state.allBanks.find((bank) => bank.bankBuildingId === input.loanOfferId)

      if (directBank && !input.collateralBuildingId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'A collateral building is required for bank loan requests.', extensions: { code: 'COLLATERAL_REQUIRED' } }] }),
        })
      }

      if (input.collateralBuildingId && state.myLoans.some((loan) => loan.collateralBuildingId === input.collateralBuildingId && (loan.status === 'ACTIVE' || loan.status === 'OVERDUE'))) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'This building is already pledged as collateral for another active loan.', extensions: { code: 'COLLATERAL_ALREADY_PLEDGED' } }] }),
        })
      }

      const principal = input.principalAmount ?? 0
      if (offer) {
        offer.usedCapacity += principal
        offer.remainingCapacity -= principal
      }
      if (directBank) {
        directBank.outstandingLoanPrincipal += principal
        directBank.availableLendingCapacity = Math.max(0, directBank.availableLendingCapacity - principal)
      }
      const borrowerCompany = state.players.flatMap((player) => player.companies).find((company) => company.id === input.borrowerCompanyId)
      const annualInterestRatePercent = offer?.annualInterestRatePercent ?? directBank?.lendingInterestRatePercent ?? 10
      const durationTicks = input.durationTicks ?? offer?.durationTicks ?? 8760
      const periodicRate = annualInterestRatePercent <= 0 ? 0 : annualInterestRatePercent / 100 / 8760
      const paymentAmount = durationTicks <= 0 ? principal : periodicRate <= 0 ? principal / durationTicks : (principal * periodicRate) / (1 - (1 + periodicRate) ** -durationTicks)
      const newLoan: MockLoan = {
        id: `loan-${Date.now()}`,
        loanOfferId: input.loanOfferId ?? '',
        borrowerCompanyId: input.borrowerCompanyId ?? '',
        borrowerCompanyName: borrowerCompany?.name ?? 'Borrower Co',
        lenderCompanyId: offer?.lenderCompanyId ?? directBank?.lenderCompanyId ?? '',
        lenderCompanyName: offer?.lenderCompanyName ?? directBank?.lenderCompanyName ?? '',
        bankBuildingId: offer?.bankBuildingId ?? directBank?.bankBuildingId ?? '',
        bankBuildingName: offer?.bankBuildingName ?? directBank?.bankBuildingName ?? '',
        originalPrincipal: principal,
        remainingPrincipal: principal,
        annualInterestRatePercent,
        durationTicks,
        startTick: state.gameState.currentTick,
        dueTick: state.gameState.currentTick + durationTicks,
        nextPaymentTick: state.gameState.currentTick + 1,
        paymentAmount: Number(paymentAmount.toFixed(2)),
        paymentsMade: 0,
        totalPayments: durationTicks,
        status: 'ACTIVE',
        missedPayments: 0,
        accumulatedPenalty: 0,
        defaultedAtTick: null,
        acceptedAtUtc: new Date().toISOString(),
        closedAtUtc: null,
        collateralBuildingId: input.collateralBuildingId ?? null,
        collateralBuildingName: input.collateralBuildingId ? (state.collateralBuildings.find((b) => b.buildingId === input.collateralBuildingId)?.buildingName ?? null) : null,
        collateralAppraisedValue: input.collateralBuildingId ? (state.collateralBuildings.find((b) => b.buildingId === input.collateralBuildingId)?.appraisedValue ?? null) : null,
      }
      state.myLoans.push(newLoan)
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { acceptLoan: newLoan } }),
      })
    }

    if (query.includes('repayLoanDebt')) {
      const input = body.variables?.input ?? {}
      const loan = state.myLoans.find((candidate) => candidate.id === input.loanId)
      if (!loan) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Loan not found.', extensions: { code: 'LOAN_NOT_FOUND' } }] }),
        })
      }

      loan.status = 'REPAID'
      loan.remainingPrincipal = 0
      loan.missedPayments = 0
      loan.accumulatedPenalty = 0
      loan.defaultedAtTick = null
      loan.closedAtUtc = new Date().toISOString()

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { repayLoanDebt: loan } }),
      })
    }

    if (query.includes('FlushStorage') || query.includes('flushStorage')) {
      const input = body.variables?.input
      const buildingUnitId: string = input?.buildingUnitId ?? ''

      if (!state.currentUserId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Not authenticated', extensions: { code: 'AUTH_NOT_AUTHORIZED' } }] }),
        })
      }

      // Find unit and validate ownership
      const player = state.players.find((p) => p.id === state.currentUserId)
      let foundUnit: MockBuildingUnit | undefined
      for (const company of player?.companies ?? []) {
        for (const building of company.buildings ?? []) {
          const u = building.units?.find((unit) => unit.id === buildingUnitId)
          if (u) {
            foundUnit = u
            break
          }
        }
        if (foundUnit) break
      }

      if (!foundUnit) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: "Unit not found or you don't own it.", extensions: { code: 'UNIT_NOT_FOUND' } }] }),
        })
      }

      const flushableTypes = ['STORAGE', 'MINING', 'MANUFACTURING']
      if (!flushableTypes.includes(foundUnit.unitType)) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Only STORAGE, MINING and MANUFACTURING units can be flushed.', extensions: { code: 'INVALID_UNIT_TYPE' } }] }),
        })
      }

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            flushStorage: {
              discardedItemCount: 1,
              totalDiscardedValue: 100,
              discardedEntries: [{ itemName: 'Wood', quantity: 10, sourcingCostLost: 100 }],
            },
          },
        }),
      })
    }

    // unitUpgradeInfo query — check precisely to avoid false substring matches
    // NOTE: do NOT use query.includes('UUI') here — 'UUI' is a substring of 'UUID', so any
    // query that uses UUID-typed parameters (e.g. buildingBankAccount) would be falsely matched.
    if (query.includes('unitUpgradeInfo') && !query.includes('scheduleUnitUpgrade') && !query.includes('ScheduleUnitUpgrade')) {
      const unitId: string = body.variables?.unitId ?? ''
      const override = state.unitUpgradeInfoOverrides[unitId]
      if (override === null) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ data: { unitUpgradeInfo: null } }),
        })
      }
      // Resolve the unit from state to return type-accurate stat label and values
      const allUnits = state.players
        .flatMap((p) => p.companies)
        .flatMap((c) => c.buildings)
        .flatMap((b) => b.units)
      const matchedUnit = allUnits.find((u) => u.id === unitId)
      const resolvedUnitType = matchedUnit?.unitType ?? 'MANUFACTURING'
      const resolvedLevel = matchedUnit?.level ?? 1
      // Mirror GameConstants.GetUnitStat and GetUnitStatLabel
      const isStorage = resolvedUnitType === 'STORAGE'
      const storageCapacities: Record<number, number> = { 1: 1000, 2: 2500, 3: 5000, 4: 10000 }
      const baseCapacities: Record<number, number> = { 1: 100, 2: 250, 3: 500, 4: 1000 }
      const statByType: Record<string, { label: string; current: number; next: number }> = {
        STORAGE: {
          label: 'capacity',
          current: storageCapacities[resolvedLevel] ?? 1000,
          next: storageCapacities[Math.min(resolvedLevel + 1, 4)] ?? 2500,
        },
        MINING: {
          label: 'units/tick',
          current: baseCapacities[resolvedLevel] ?? 100,
          next: baseCapacities[Math.min(resolvedLevel + 1, 4)] ?? 250,
        },
        MANUFACTURING: { label: 'Batches/tick', current: 1.0, next: 2.0 },
        PURCHASE: {
          label: 'units/tick',
          current: baseCapacities[resolvedLevel] ?? 100,
          next: baseCapacities[Math.min(resolvedLevel + 1, 4)] ?? 250,
        },
        PUBLIC_SALES: {
          label: 'units/tick',
          current: baseCapacities[resolvedLevel] ?? 100,
          next: baseCapacities[Math.min(resolvedLevel + 1, 4)] ?? 250,
        },
        B2B_SALES: {
          label: 'units/tick',
          current: baseCapacities[resolvedLevel] ?? 100,
          next: baseCapacities[Math.min(resolvedLevel + 1, 4)] ?? 250,
        },
      }
      const stat = statByType[resolvedUnitType] ?? { label: 'Batches/tick', current: 1.0, next: 2.0 }
      const defaultInfo = {
        unitId,
        unitType: resolvedUnitType,
        currentLevel: resolvedLevel,
        nextLevel: Math.min(resolvedLevel + 1, 4),
        isMaxLevel: resolvedLevel >= 4,
        isUpgradable: !['BRANDING', 'MARKETING'].includes(resolvedUnitType),
        upgradeCost: isStorage ? 8000 : 8000,
        upgradeTicks: 10,
        currentStat: stat.current,
        nextStat: stat.next,
        statLabel: stat.label,
        // Operating cost deltas at reference wage ($20/manhour) and energy ($55/MWh)
        // Values mirror CompanyEconomyCalculator.GetBaseUnitLaborHours/EnergyMwh × multipliers
        currentLaborHoursPerTick: 0.7 * resolvedLevel,
        nextLaborHoursPerTick: 0.7 * Math.min(resolvedLevel + 1, 4),
        currentEnergyMwhPerTick: 0.12 * resolvedLevel,
        nextEnergyMwhPerTick: 0.12 * Math.min(resolvedLevel + 1, 4),
        currentLaborCostPerTick: Math.round(0.7 * resolvedLevel * 20 * 100) / 100,
        nextLaborCostPerTick: Math.round(0.7 * Math.min(resolvedLevel + 1, 4) * 20 * 100) / 100,
        currentEnergyCostPerTick: Math.round(0.12 * resolvedLevel * 55 * 100) / 100,
        nextEnergyCostPerTick: Math.round(0.12 * Math.min(resolvedLevel + 1, 4) * 55 * 100) / 100,
        // Inventory holding capacity (mirrors GameConstants.GetUnitHoldingCapacity)
        currentStorageCapacity: isStorage ? (storageCapacities[resolvedLevel] ?? 1000) : (baseCapacities[resolvedLevel] ?? 100),
        nextStorageCapacity: isStorage ? (storageCapacities[Math.min(resolvedLevel + 1, 4)] ?? 2500) : (baseCapacities[Math.min(resolvedLevel + 1, 4)] ?? 250),
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { unitUpgradeInfo: override ?? defaultInfo } }),
      })
    }

    // scheduleUnitUpgrade mutation
    if (query.includes('scheduleUnitUpgrade') || query.includes('ScheduleUnitUpgrade') || query.includes('SUU')) {
      if (!state.currentUserId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Not authenticated', extensions: { code: 'AUTH_NOT_AUTHORIZED' } }] }),
        })
      }
      const unitId: string = body.variables?.input?.unitId ?? ''
      if (state.upgradeInsufficientFundsUnitId === unitId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Insufficient funds.', extensions: { code: 'INSUFFICIENT_FUNDS' } }] }),
        })
      }
      if (state.upgradeMaxConcurrentUnitId === unitId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Max concurrent upgrades reached.', extensions: { code: 'MAX_CONCURRENT_UPGRADES' } }] }),
        })
      }
      if (state.upgradeAlreadyUpgradingUnitId === unitId) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Unit already upgrading.', extensions: { code: 'UNIT_ALREADY_UPGRADING' } }] }),
        })
      }
      const gameState = state.gameState
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            scheduleUnitUpgrade: {
              id: crypto.randomUUID(),
              appliesAtTick: gameState.currentTick + 10,
              totalTicksRequired: 10,
            },
          },
        }),
      })
    }

    // ── Company bank accounts query ─────────────────────────────────────────
    if (query.includes('companyBankAccounts')) {
      if (!state.currentUserId) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const vars = body.variables as { companyId?: string } | undefined
      const companyId = vars?.companyId ?? ''
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const company = player.companies.find((candidate) => candidate.id === companyId)
      if (!company) return routeJsonError('Company not found', 'COMPANY_NOT_FOUND')

      const explicitAccounts = company.buildings
        .map((building) => state.buildingBankAccounts[building.id])
        .filter((account): account is NonNullable<typeof account> => Boolean(account?.hasBankAccount && account.bankAccountId && account.accountNumber))
        .map((account) => ({
          id: account.bankAccountId!,
          accountNumber: account.accountNumber!,
          currencyCode: account.currencyCode,
          balance: account.balance ?? 0,
          alertMinBalanceThreshold: account.alertMinBalanceThreshold ?? null,
        }))

      const playerAccounts = state.myBankAccounts
        .filter((account) => account.companyId === companyId)
        .map((account) => ({
          id: account.id,
          accountNumber: account.accountNumber,
          currencyCode: account.currencyCode,
          balance: account.balance,
          alertMinBalanceThreshold: null,
        }))

      const companyBankAccounts = [...playerAccounts, ...explicitAccounts].filter((account, index, accounts) => accounts.findIndex((candidate) => candidate.id === account.id) === index)

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { companyBankAccounts } }),
      })
    }

    // ── Building bank account query ──────────────────────────────────────────
    if (query.includes('buildingBankAccount') && !query.includes('fundBuildingBankAccount') && !query.includes('assignBuildingBankAccount') && !query.includes('createCompanyBankAccount')) {
      if (!state.currentUserId) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const vars = body.variables as { buildingId?: string } | undefined
      const buildingId = vars?.buildingId ?? ''
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const building = player.companies.flatMap((c) => c.buildings).find((b) => b.id === buildingId)
      if (!building) return routeJsonError('Building not found', 'BUILDING_NOT_FOUND')

      const explicit = state.buildingBankAccounts[buildingId]
      const city = state.cities.find((c) => c.id === building.cityId)
      const currencyCode = city?.currencyCode ?? 'EUR'

      const info = explicit ?? {
        hasBankAccount: false,
        bankAccountId: null,
        accountNumber: null,
        balance: null,
        alertMinBalanceThreshold: null,
        isSuspendedForFunds: building.isSuspendedForFunds ?? false,
        suspendedReason: building.suspendedReason ?? null,
        currencyCode,
      }

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            buildingBankAccount: {
              buildingId,
              buildingName: building.name,
              cityName: city?.name ?? '',
              currencyCode: info.currencyCode,
              hasBankAccount: info.hasBankAccount,
              bankAccountId: info.bankAccountId,
              accountNumber: info.accountNumber,
              balance: info.balance,
              alertMinBalanceThreshold: info.alertMinBalanceThreshold ?? null,
              isSuspendedForFunds: info.isSuspendedForFunds,
              suspendedReason: info.suspendedReason,
            },
          },
        }),
      })
    }

    // ── Transfer funds between two of the player's bank accounts ─────────────
    if (query.includes('transferFunds')) {
      if (!state.currentUserId) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const vars = body.variables as
        | {
            input?: { fromBankAccountId?: string; toBankAccountId?: string; amount?: number }
          }
        | undefined
      const fromId = vars?.input?.fromBankAccountId ?? ''
      const toId = vars?.input?.toBankAccountId ?? ''
      const amount = vars?.input?.amount ?? 0
      if (fromId === toId) return routeJsonError('Source and destination must be different.', 'SAME_ACCOUNT')
      if (amount <= 0) return routeJsonError('Amount must be positive.', 'INVALID_AMOUNT')
      const fromAcc = state.myBankAccounts.find((a) => a.id === fromId)
      if (!fromAcc) return routeJsonError('Source bank account not found.', 'FROM_ACCOUNT_NOT_FOUND')
      const toAcc = state.myBankAccounts.find((a) => a.id === toId)
      if (!toAcc) return routeJsonError('Destination bank account not found.', 'TO_ACCOUNT_NOT_FOUND')
      if (fromAcc.currencyCode !== toAcc.currencyCode) {
        return routeJsonError('Both accounts must use the same currency.', 'CURRENCY_MISMATCH')
      }
      if (fromAcc.balance < amount) {
        return routeJsonError('Insufficient funds in source account.', 'INSUFFICIENT_FUNDS')
      }
      fromAcc.balance -= amount
      toAcc.balance += amount
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            transferFunds: {
              amount,
              currencyCode: fromAcc.currencyCode,
              fromAccount: normalizeMockBankAccount(fromAcc, 'Personal Account'),
              toAccount: normalizeMockBankAccount(toAcc, 'Personal Account'),
            },
          },
        }),
      })
    }

    // ── Fund building bank account mutation ──────────────────────────────────
    if (query.includes('fundBuildingBankAccount')) {
      if (!state.currentUserId) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const vars = body.variables as { input?: { buildingId?: string; amount?: number } } | undefined
      const buildingId = vars?.input?.buildingId ?? ''
      const amount = vars?.input?.amount ?? 0
      if (amount <= 0) return routeJsonError('Amount must be positive.', 'INVALID_AMOUNT')

      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const building = player.companies.flatMap((c) => c.buildings).find((b) => b.id === buildingId)
      if (!building) return routeJsonError('Building not found', 'BUILDING_NOT_FOUND')
      const company = player.companies.find((c) => c.buildings.some((b) => b.id === buildingId))
      if (!company) return routeJsonError('Building not found', 'BUILDING_NOT_FOUND')
      if (company.cash < amount) return routeJsonError(`Insufficient company cash. Available: ${company.cash}`, 'INSUFFICIENT_COMPANY_CASH')

      company.cash -= amount

      const city = state.cities.find((c) => c.id === building.cityId)
      const currencyCode = city?.currencyCode ?? 'EUR'
      const existing = state.buildingBankAccounts[buildingId]
      const prev = existing ?? {
        hasBankAccount: false,
        bankAccountId: null,
        accountNumber: null,
        balance: 0,
        alertMinBalanceThreshold: null,
        isSuspendedForFunds: false,
        suspendedReason: null,
        currencyCode,
      }
      const newBalance = (prev.balance ?? 0) + amount
      const accountNumber = prev.accountNumber ?? String(Math.floor(Math.random() * 1e16)).padStart(16, '0')
      const accountId = prev.bankAccountId ?? crypto.randomUUID()

      state.buildingBankAccounts[buildingId] = {
        hasBankAccount: true,
        bankAccountId: accountId,
        accountNumber,
        balance: newBalance,
        alertMinBalanceThreshold: prev.alertMinBalanceThreshold ?? null,
        isSuspendedForFunds: false,
        suspendedReason: null,
        currencyCode,
      }

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            fundBuildingBankAccount: {
              bankAccount: {
                buildingId,
                buildingName: building.name,
                cityName: city?.name ?? '',
                currencyCode,
                hasBankAccount: true,
                bankAccountId: accountId,
                accountNumber,
                balance: newBalance,
                alertMinBalanceThreshold: prev.alertMinBalanceThreshold ?? null,
                isSuspendedForFunds: false,
                suspendedReason: null,
              },
              remainingCompanyCash: company.cash,
            },
          },
        }),
      })
    }

    // ── Assign building bank account mutation ────────────────────────────────
    if (query.includes('assignBuildingBankAccount')) {
      if (!state.currentUserId) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const vars = body.variables as { input?: { buildingId?: string; bankAccountId?: string } } | undefined
      const buildingId = vars?.input?.buildingId ?? ''
      const bankAccountId = vars?.input?.bankAccountId ?? ''
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const building = player.companies.flatMap((c) => c.buildings).find((b) => b.id === buildingId)
      if (!building) return routeJsonError('Building not found', 'BUILDING_NOT_FOUND')
      const company = player.companies.find((candidate) => candidate.buildings.some((candidateBuilding) => candidateBuilding.id === buildingId))
      if (!company) return routeJsonError('Company not found', 'COMPANY_NOT_FOUND')

      const playerAccount = state.myBankAccounts.find((account) => account.id === bankAccountId && account.companyId === company.id)
      const acctInfo = playerAccount
        ? {
            hasBankAccount: true,
            bankAccountId: playerAccount.id,
            accountNumber: playerAccount.accountNumber,
            balance: playerAccount.balance,
            alertMinBalanceThreshold: null,
            isSuspendedForFunds: false,
            suspendedReason: null,
            currencyCode: playerAccount.currencyCode,
          }
        : Object.entries(state.buildingBankAccounts).find(([, info]) => info.bankAccountId === bankAccountId)?.[1]

      if (!acctInfo) return routeJsonError('Bank account not found', 'BANK_ACCOUNT_NOT_FOUND')

      const city = state.cities.find((c) => c.id === building.cityId)
      const cityCurrency = city?.currencyCode ?? 'EUR'
      if (acctInfo.currencyCode !== cityCurrency) return routeJsonError(`Account currency ${acctInfo.currencyCode} does not match city currency ${cityCurrency}.`, 'CURRENCY_MISMATCH')

      // Reassign the account to this building.
      state.buildingBankAccounts[buildingId] = { ...acctInfo }

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            assignBuildingBankAccount: {
              bankAccount: {
                buildingId,
                buildingName: building.name,
                cityName: city?.name ?? '',
                currencyCode: acctInfo.currencyCode,
                hasBankAccount: true,
                bankAccountId,
                accountNumber: acctInfo.accountNumber,
                balance: acctInfo.balance,
                alertMinBalanceThreshold: acctInfo.alertMinBalanceThreshold ?? null,
                isSuspendedForFunds: acctInfo.isSuspendedForFunds,
                suspendedReason: acctInfo.suspendedReason,
              },
            },
          },
        }),
      })
    }

    // ── Create company bank account mutation ─────────────────────────────────
    if (query.includes('createCompanyBankAccount')) {
      if (!state.currentUserId) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const vars = body.variables as { input?: { companyId?: string; currencyCode?: string } } | undefined
      const companyId = vars?.input?.companyId ?? ''
      const currencyCode = (vars?.input?.currencyCode ?? 'EUR').toUpperCase()
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const company = player.companies.find((c) => c.id === companyId)
      if (!company) return routeJsonError('Company not found', 'COMPANY_NOT_FOUND')
      const accountId = crypto.randomUUID()
      const accountNumber = String(Math.floor(Math.random() * 1e16)).padStart(16, '0')
      const currencySymbol = currencyCode === 'EUR' ? '€' : (state.fxRates.find((rate) => rate.quoteCurrencyCode === currencyCode)?.quoteCurrencySymbol ?? currencyCode)
      state.myBankAccounts.push({
        id: accountId,
        accountNumber,
        currencyCode,
        currencySymbol,
        balance: 0,
        companyId: company.id,
        companyName: company.name,
      })
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            createCompanyBankAccount: {
              account: {
                id: accountId,
                accountNumber,
                currencyCode,
                balance: 0,
                alertMinBalanceThreshold: null,
              },
            },
          },
        }),
      })
    }

    if (query.includes('setBankAccountAlertThreshold')) {
      if (!state.currentUserId) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const vars = body.variables as { input?: { bankAccountId?: string; minBalanceThreshold?: number | null } } | undefined
      const bankAccountId = vars?.input?.bankAccountId ?? ''
      const minBalanceThreshold = vars?.input?.minBalanceThreshold ?? null
      if (minBalanceThreshold !== null && minBalanceThreshold < 0) {
        return routeJsonError('Minimum balance threshold must be zero or positive.', 'INVALID_THRESHOLD')
      }

      const explicitEntry = Object.entries(state.buildingBankAccounts).find(([, info]) => info.bankAccountId === bankAccountId)
      if (!explicitEntry) {
        return routeJsonError('Bank account not found.', 'BANK_ACCOUNT_NOT_FOUND')
      }

      const [buildingId, info] = explicitEntry
      state.buildingBankAccounts[buildingId] = {
        ...info,
        alertMinBalanceThreshold: minBalanceThreshold,
      }

      return routeJson({
        setBankAccountAlertThreshold: {
          bankAccountId,
          alertMinBalanceThreshold: minBalanceThreshold,
        },
      })
    }

    if (query.includes('setPublicSalesInventoryAlertThreshold')) {
      if (!state.currentUserId) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const vars = body.variables as { input?: { buildingUnitId?: string; minInventoryThreshold?: number | null } } | undefined
      const buildingUnitId = vars?.input?.buildingUnitId ?? ''
      const minInventoryThreshold = vars?.input?.minInventoryThreshold ?? null
      if (minInventoryThreshold !== null && minInventoryThreshold < 0) {
        return routeJsonError('Minimum inventory threshold must be zero or positive.', 'INVALID_THRESHOLD')
      }

      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) return routeJsonError('Not authenticated', 'AUTH_NOT_AUTHORIZED')
      const ownedUnits = player.companies.flatMap((company) => company.buildings).flatMap((building) => building.units)
      const unit = ownedUnits.find((candidate) => candidate.id === buildingUnitId && candidate.unitType === 'PUBLIC_SALES')
      if (!unit) {
        return routeJsonError('Public sales unit not found.', 'PUBLIC_SALES_UNIT_NOT_FOUND')
      }

      unit.lowInventoryAlertThreshold = minInventoryThreshold
      return routeJson({
        setPublicSalesInventoryAlertThreshold: {
          buildingUnitId,
          lowInventoryAlertThreshold: minInventoryThreshold,
        },
      })
    }

    if (query.includes('getMineExtractionHistory')) {
      const mockRecords = state.mineExtractionRecords ?? []
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { getMineExtractionHistory: mockRecords } }),
      })
    }

    if (query.includes('getMineDepletionForecast')) {
      const mockForecast = state.mineDepletionForecast ?? null
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { getMineDepletionForecast: mockForecast } }),
      })
    }

    if (query.includes('getMineExtractionIntelligence')) {
      const mockRecords = state.mineExtractionRecords ?? []
      const dailyMap = new Map<number, { extractedAmount: number; efficiencyTotal: number; points: number; reserveRemaining: number }>()
      for (const record of mockRecords) {
        const dayIndex = Math.floor(record.tick / 24)
        const existing = dailyMap.get(dayIndex)
        if (existing) {
          existing.extractedAmount += record.extractedAmount
          existing.efficiencyTotal += record.efficiencyPercent
          existing.points += 1
          existing.reserveRemaining = record.reserveRemaining
        } else {
          dailyMap.set(dayIndex, {
            extractedAmount: record.extractedAmount,
            efficiencyTotal: record.efficiencyPercent,
            points: 1,
            reserveRemaining: record.reserveRemaining,
          })
        }
      }

      const dailyExtraction = [...dailyMap.entries()]
        .sort((a, b) => a[0] - b[0])
        .map(([dayIndex, value]) => ({
          dayIndex,
          extractedAmount: value.extractedAmount,
          efficiencyPercent: value.points > 0 ? value.efficiencyTotal / value.points : 0,
          reserveRemaining: value.reserveRemaining,
        }))

      const forecast = state.mineDepletionForecast
      const mockIntelligence =
        state.mineExtractionIntelligence !== undefined
          ? state.mineExtractionIntelligence
          : {
              currentTick: (mockRecords[0]?.tick ?? 0) + 1,
              burnRatePerTick: forecast?.averageExtractionRatePerTick ?? null,
              burnRatePerDay: forecast?.averageExtractionRatePerTick !== null && forecast?.averageExtractionRatePerTick !== undefined ? forecast.averageExtractionRatePerTick * 24 : null,
              expectedDepletionTick: forecast?.depletionTick ?? null,
              qualityDecayInflectionTick: forecast?.critical20PctTick ?? null,
              estimatedGameDaysRemaining: forecast?.estimatedGameDaysRemaining ?? null,
              currentReserve: forecast?.currentReserve ?? null,
              originalReserve: forecast?.originalReserve ?? null,
              dailyExtraction,
            }

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { getMineExtractionIntelligence: mockIntelligence } }),
      })
    }

    // Fallback
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ data: null }),
    })
  })

  return state
}

// ── Login helper ─────────────────────────────────────────────────────────────

export async function restoreMockSession(page: Page, token: string): Promise<void> {
  const state = mockStateByPage.get(page)
  if (!state) {
    throw new Error('Mock API state was not initialized for this page.')
  }

  const playerId = token.replace(/^token-/, '')
  const player = state.players.find((candidate) => candidate.id === playerId)
  if (!player) {
    throw new Error(`No mock player found for token ${token}.`)
  }

  await loginAs(page, state, player)
}

export async function loginAs(page: Page, state: MockState, player: MockPlayer): Promise<void> {
  await page.goto('/login')
  await page.getByLabel('Email').fill(player.email)
  await page.getByLabel('Password').fill(player.password)
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  const token = `token-${player.id}`
  const expiresAtUtc = new Date(Date.now() + 7200000).toISOString()
  const cookieUrl = process.env.CI ? 'http://localhost:4173' : 'http://localhost:5173'
  await page.context().addCookies([
    {
      name: 'auth_token',
      value: token,
      url: cookieUrl,
    },
    {
      name: 'auth_expires',
      value: expiresAtUtc,
      url: cookieUrl,
    },
  ])
  await page.evaluate((token) => {
    const expires = new Date(Date.now() + 7200000).toISOString()
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', expires)
    document.cookie = `auth_token=${encodeURIComponent(token)}; path=/`
    document.cookie = `auth_expires=${encodeURIComponent(expires)}; path=/`
  }, token)
  await page.getByRole('button', { name: 'Sign In', exact: true }).click()
  await page.waitForURL('/')
}

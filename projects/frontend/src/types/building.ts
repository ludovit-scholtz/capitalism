import type { Company } from './company'

/** Queued building configuration that becomes active on a future tick. */
export interface BuildingConfigurationPlan {
  id: string
  buildingId: string
  submittedAtUtc: string
  submittedAtTick: number
  appliesAtTick: number
  totalTicksRequired: number
  blockReason?: string | null
  units: BuildingConfigurationPlanUnit[]
  removals: BuildingConfigurationPlanRemoval[]
}

/** Pending unit snapshot inside a queued building configuration. */
export interface BuildingConfigurationPlanUnit {
  id: string
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
  startedAtTick: number
  appliesAtTick: number
  ticksRequired: number
  isChanged: boolean
  isReverting: boolean
  resourceTypeId: string | null
  productTypeId: string | null
  minPrice: number | null
  maxPrice: number | null
  purchaseSource: string | null
  saleVisibility: string | null
  budget: number | null
  mediaHouseBuildingId: string | null
  minQuality: number | null
  brandScope: string | null
  vendorLockCompanyId: string | null
  lockedCityId: string | null
  industryCategory: string | null
}

export interface BuildingConfigurationPlanRemoval {
  id: string
  gridX: number
  gridY: number
  startedAtTick: number
  appliesAtTick: number
  ticksRequired: number
  isReverting: boolean
}

/** Matches backend BuildingUnit entity */
export interface BuildingUnit {
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
  resourceTypeId: string | null
  productTypeId: string | null
  minPrice: number | null
  maxPrice: number | null
  purchaseSource: string | null
  saleVisibility: string | null
  budget: number | null
  mediaHouseBuildingId: string | null
  minQuality: number | null
  brandScope: string | null
  vendorLockCompanyId: string | null
  lockedCityId: string | null
  industryCategory: string | null
  lowInventoryAlertThreshold?: number | null
}

/** Matches backend Building entity */
export interface Building {
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
  askingPrice: number | null
  /** UTC timestamp when the building was listed for sale. Null when not listed. */
  listedAtUtc: string | null
  pricePerSqm: number | null
  /** Pending rent per m² scheduled by the player; activates at pendingPriceActivationTick. */
  pendingPricePerSqm: number | null
  /** Tick number when pendingPricePerSqm becomes active. Null if no pending change. */
  pendingPriceActivationTick: number | null
  occupancyPercent: number | null
  totalAreaSqm: number | null
  powerPlantType: string | null
  powerOutput: number | null
  /** Power supply status set by the tick engine: POWERED | CONSTRAINED | OFFLINE */
  powerStatus: string
  mediaType: string | null
  interestRate: number | null
  builtAtUtc: string
  /** True while the building is still under construction (before constructionCompletesAtTick). */
  isUnderConstruction: boolean
  /** Tick number when construction finishes and the building becomes operational. Null for legacy buildings. */
  constructionCompletesAtTick: number | null
  /** Cash cost charged for construction (separate from the land price). */
  constructionCost: number
  /** Accumulated content value for MEDIA_HOUSE buildings. Grows from content investment; decays 0.5% per tick. */
  contentValue: number
  /** Per-tick content spending configured by the media house owner. Null if not set. */
  contentBudgetPerTick: number | null
  /** True when the MEDIA_HOUSE building is a government-seeded baseline outlet (cannot be upgraded). */
  isGovernmentOwned: boolean
  /** True when at least one media-house campaign unit is actively running. */
  isAdvertisingActive?: boolean
  /** True when the building was suspended on the last tick due to insufficient bank account funds. */
  isSuspendedForFunds: boolean
  /** Dispatch target percentage for POWER_PLANT buildings (0–100). 100 = full output (default). */
  dispatchTargetPercent: number
  /** Current fuel reserve for thermal power plants (COAL/GAS) in MWh. */
  fuelReserveMwh: number
  /** Machine-readable suspension reason: null | 'MISSING_BANK_ACCOUNT' | 'INSUFFICIENT_FUNDS:<amount>' */
  suspendedReason: string | null
  /**
   * City base average rent per m² in local currency (APARTMENT/COMMERCIAL only).
   * Useful as a baseline reference for rent-setting decisions.
   */
  cityReferenceRentPerSqm: number | null
  /**
   * Location-adjusted market rent per m²: city base rate × lot PopulationIndex
   * (APARTMENT/COMMERCIAL only). This is the rate players should compare their rent against.
   */
  adjustedMarketRentPerSqm: number | null
  /** Lot PopulationIndex (location quality score, 1.0 = baseline). APARTMENT/COMMERCIAL only. */
  populationIndex: number | null
  /** Mine lot resource under this building's land (null for non-mine lots). */
  lotResourceTypeId?: string | null
  /** Mine lot deposit quality in 0..1 (null for non-mine lots). */
  lotMaterialQuality?: number | null
  /** Remaining extractable quantity on the mine lot (null for non-mine lots). */
  lotMaterialQuantity?: number | null
  /** Original (full) extractable quantity at the time the lot was first tracked (null for non-mine lots). */
  lotOriginalMaterialQuantity?: number | null
  /** UTC timestamp when the building was destroyed by loan default foreclosure. Null when still standing. */
  destroyedAtUtc: string | null
  /** Reason the building was destroyed: 'PlayerDemolished', 'GracePeriodExpired', 'DefaultedLoan', or null. */
  destroyedReason: string | null
  /** True when at least one DEFAULTED loan uses this building as collateral. */
  hasDefaultedCollateralLoan?: boolean
  /** True when this building is currently locked as unpaid loan collateral. */
  isCollateralized?: boolean
  /** Foreclosure countdown in ticks when collateral is in DEFAULTED status. */
  foreclosureTicksRemaining?: number | null
  /**
   * Power priority for load-shedding. Higher priority buildings stay online longer during
   * a grid shortage. Range 1 (lowest) to 10 (highest). Default 5.
   */
  powerPriority: number
  /**
   * Maximum price per kWh (local currency) this building will auto-bid on the energy spot market.
   * Null means no auto-purchasing (power plant buildings always have null here).
   */
  maxEnergyBidPrice: number | null
  marketValuation?: {
    landValue: number
    structureValue: number
    unitsValue: number
    totalValue: number
    minimumSalePrice: number
    currencyCode: string
  } | null
  units: BuildingUnit[]
  pendingConfiguration: BuildingConfigurationPlan | null
}

export interface BuildingUnitInventorySummary {
  buildingUnitId: string
  quantity: number
  capacity: number
  fillPercent: number
  averageQuality: number | null
  totalSourcingCost: number
  sourcingCostPerUnit: number
  /** Total inflow (received + produced) during the most recent completed tick. Null if no tick history exists. */
  lastTickInflow: number | null
  /** Total outflow (sent + consumed) during the most recent completed tick. Null if no tick history exists. */
  lastTickOutflow: number | null
}

export interface BuildingUnitInventory {
  id: string
  buildingUnitId: string
  resourceTypeId: string | null
  productTypeId: string | null
  quantity: number
  sourcingCostTotal: number
  sourcingCostPerUnit: number
  quality: number
}

export interface BuildingUnitResourceHistoryPoint {
  buildingUnitId: string
  resourceTypeId: string | null
  productTypeId: string | null
  tick: number
  inflowQuantity: number
  outflowQuantity: number
  consumedQuantity: number
  producedQuantity: number
}

/** Matches backend BuildingLot entity */
export interface BuildingLot {
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
    isUnderConstruction: boolean
    constructionCompletesAtTick: number | null
    constructionCost: number
    isForSale: boolean
    askingPrice: number | null
    destroyedAtUtc: string | null
    destroyedReason: string | null
    hasDefaultedCollateralLoan?: boolean
  } | null
  /** Raw material available for extraction — null when no resource on this lot */
  resourceType: { id: string; name: string; slug: string } | null
  /** Quality of the raw material (0.0–1.0); null when no resource */
  materialQuality: number | null
  /** Estimated extractable quantity in tonnes; null when no resource */
  materialQuantity: number | null
  /** Original tracked extractable quantity used to compute depletion ratio */
  originalMaterialQuantity?: number | null
}

/** Result of purchasing a building lot */
export interface PurchaseLotResult {
  lot: BuildingLot
  building: Building
  company: Company
}

export interface BuildingLedgerSummary {
  buildingId: string
  buildingName: string
  buildingType: string
  revenue: number
  costs: number
  currencyCode: string
  currencySymbol: string
}

export interface BuildingFinancialTickSnapshot {
  tick: number
  sales: number
  costs: number
  profit: number
}

export interface BuildingFinancialTimeline {
  buildingId: string
  buildingName: string
  dataFromTick: number
  dataToTick: number
  totalSales: number
  totalCosts: number
  totalProfit: number
  timeline: BuildingFinancialTickSnapshot[]
}

/**
 * Operational status for a single building unit.
 * Tells the player whether the unit is actively processing, idle, blocked, or unconfigured.
 */
export interface BuildingUnitOperationalStatus {
  buildingUnitId: string
  /**
   * Machine-readable status.
   * Values: ACTIVE | IDLE | BLOCKED | FULL | UNCONFIGURED
   */
  status: 'ACTIVE' | 'IDLE' | 'BLOCKED' | 'FULL' | 'UNCONFIGURED'
  /**
   * Machine-readable blocked reason code (null when status is not BLOCKED or FULL).
   * Values: NO_INPUTS | OUTPUT_FULL | NO_INVENTORY | PRICE_TOO_HIGH | UNCONFIGURED | AWAITING_STOCK | NO_DEMAND
   */
  blockedCode: string | null
  /** Human-readable explanation shown to the player. */
  blockedReason: string | null
  /** Number of consecutive ticks the unit has had no activity. */
  idleTicks: number
  /**
   * Estimated base labor cost per tick for this unit, in game currency.
   * Derived from unit type, level, and the company's effective hourly wage for the building's city.
   * Null if the unit carries no labor cost.
   */
  nextTickLaborCost: number | null
  /**
   * Estimated energy cost per tick for this unit, in game currency.
   * Derived from unit type, level, and the fixed energy price per MWh.
   * Null if the unit carries no energy cost.
   */
  nextTickEnergyCost: number | null
}

/**
 * A single human-readable event from a building's recent tick history.
 * Covers purchasing, manufacturing, resource movement, and public sales.
 */
export interface BuildingRecentActivityEvent {
  tick: number
  buildingUnitId: string
  /**
   * Machine-readable event type.
   * Values: PURCHASED | MANUFACTURED | MOVED | SOLD | IDLE | BLOCKED
   */
  eventType: 'PURCHASED' | 'MANUFACTURED' | 'MOVED' | 'SOLD' | 'IDLE' | 'BLOCKED'
  /** Human-readable one-line description. */
  description: string
  quantity: number | null
  amount: number | null
  resourceTypeId: string | null
  productTypeId: string | null
}

export * from './buildingSourcing'
export * from './buildingSaleOffer'

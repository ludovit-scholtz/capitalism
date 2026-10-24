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

export * from './analytics/sales'
export * from './analytics/unitProduct'
export * from './analytics/marketing'
export * from './analytics/market'
export * from './analytics/rental'

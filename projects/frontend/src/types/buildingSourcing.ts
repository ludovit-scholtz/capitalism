/** Procurement preview result from the backend. */
export interface ProcurementPreview {
  sourceType: 'GLOBAL_EXCHANGE' | 'LOCAL_B2B' | 'LOCKED_VENDOR' | 'NO_SOURCE'
  sourceCityId: string | null
  sourceCityName: string | null
  sourceVendorCompanyId: string | null
  sourceVendorName: string | null
  exchangePricePerUnit: number | null
  transitCostPerUnit: number | null
  deliveredPricePerUnit: number | null
  estimatedQuality: number | null
  canExecute: boolean
  blockReason: string | null
  blockMessage: string | null
}

/**
 * A single sourcing candidate for a purchase unit.
 * Each candidate represents one possible supply route with a full landed-cost
 * breakdown (exchange price + transit cost = delivered price).
 * Returned by the `sourcingCandidates` GraphQL query.
 */
export interface SourcingCandidate {
  sourceType: 'GLOBAL_EXCHANGE' | 'PLAYER_EXCHANGE_ORDER' | 'LOCAL_B2B' | 'LOCKED_VENDOR' | 'NO_SOURCE'
  sourceCityId: string | null
  sourceCityName: string | null
  sourceVendorCompanyId: string | null
  sourceVendorName: string | null
  exchangePricePerUnit: number | null
  transitCostPerUnit: number | null
  deliveredPricePerUnit: number | null
  estimatedQuality: number | null
  distanceKm: number | null
  isEligible: boolean
  blockReason: string | null
  blockMessage: string | null
  isRecommended: boolean
  rank: number
}

/** Upgrade info for a single building unit: cost, timing, and stat projections. */
export interface UnitUpgradeInfo {
  unitId: string
  unitType: string
  currentLevel: number
  nextLevel: number
  isMaxLevel: boolean
  isUpgradable: boolean
  upgradeCost: number
  upgradeTicks: number
  currentStat: number
  nextStat: number
  statLabel: string
  // Operating cost deltas (per tick at reference wage/energy price)
  currentLaborHoursPerTick: number
  nextLaborHoursPerTick: number
  currentEnergyMwhPerTick: number
  nextEnergyMwhPerTick: number
  currentLaborCostPerTick: number
  nextLaborCostPerTick: number
  currentEnergyCostPerTick: number
  nextEnergyCostPerTick: number
  // Inventory holding capacity (units stored in the unit's buffer) before and after upgrade
  currentStorageCapacity: number
  nextStorageCapacity: number
}

/** Health status for supply chain visualization */
export type SupplyChainHealth = 'GREEN' | 'YELLOW' | 'RED'

/** Unit summary in supply chain diagram */
export interface SupplyChainUnitSummary {
  buildingUnitId: string
  unitType: string
  gridX: number
  gridY: number
  level: number
  /** ACTIVE | IDLE | BLOCKED | FULL | UNCONFIGURED */
  status: string
  idleTicks: number
  fillPercent: number
  resourceTypeId: string | null
  productTypeId: string | null
  resourceOrProductName: string | null
  estimatedTransitCost: number | null
}

/** Link between two units in supply chain */
export interface SupplyChainLink {
  fromUnitId: string
  toUnitId: string
  /** RIGHT | DOWN | DIAGONAL_DR | etc. */
  direction: string
  estimatedTransitCost: number
}

/** Complete supply chain diagram for a factory */
export interface BuildingSupplyChainDiagram {
  buildingId: string
  buildingName: string
  buildingType: string
  units: SupplyChainUnitSummary[]
  links: SupplyChainLink[]
  healthScore: SupplyChainHealth
  healthReason: string
  criticalUnitIds: string[]
  warningUnitIds: string[]
}

// ── Building Secondary Market ────────────────────────────────────────────────

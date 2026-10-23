/**
 * Pure rules for the dashboard advisor card (#602): turn per-unit operational statuses into at most a couple of
 * actionable suggestions, each with a deep link to where the player fixes it. No clock, no network, no i18n.
 */

export type AdvisorRule = 'OUT_OF_STOCK_SHOP' | 'FACTORY_NO_INPUTS' | 'SHOP_NO_DEMAND' | 'UNCONFIGURED_UNIT'

export interface AdvisorBuilding {
  id: string
  name: string
  type: string
  units: { id: string; unitType: string }[]
}

export interface AdvisorUnitStatus {
  buildingUnitId: string
  status: string
  blockedCode: string | null
  /** Consecutive ticks (in-game hours) without activity. */
  idleTicks: number
}

export interface AdvisorSuggestion {
  /** Stable per rule and building, so a dismissal survives the problem getting older. */
  id: string
  rule: AdvisorRule
  buildingId: string
  buildingName: string
  /** How long the problem has lasted, in ticks; format with `formatTickDuration`, never show it raw. */
  ticks: number
  /** Whether the building already has a purchase unit (changes the advice for an empty shop). */
  hasPurchaseUnit: boolean
  linkPath: string
}

/** How long a problem must last before it is worth the player's attention. 1 tick = 1 in-game hour. */
export const ADVISOR_THRESHOLDS = {
  outOfStockTicks: 12,
  noInputsTicks: 12,
  noDemandTicks: 24,
  unconfiguredTicks: 6,
} as const

export const ADVISOR_MAX_SUGGESTIONS = 2

const RULE_ORDER: AdvisorRule[] = ['OUT_OF_STOCK_SHOP', 'FACTORY_NO_INPUTS', 'SHOP_NO_DEMAND', 'UNCONFIGURED_UNIT']

function longestMatching(
  building: AdvisorBuilding,
  statuses: AdvisorUnitStatus[],
  unitType: string | null,
  matches: (status: AdvisorUnitStatus) => boolean,
  minTicks: number,
): number | null {
  const unitTypeById = new Map(building.units.map((unit) => [unit.id, unit.unitType]))
  const ticks = statuses
    .filter((status) => unitTypeById.has(status.buildingUnitId))
    .filter((status) => unitType === null || unitTypeById.get(status.buildingUnitId) === unitType)
    .filter(matches)
    .map((status) => status.idleTicks)
    .filter((idleTicks) => idleTicks >= minTicks)
  return ticks.length > 0 ? Math.max(...ticks) : null
}

export function deriveAdvisorSuggestions(
  buildings: AdvisorBuilding[],
  statusesByBuilding: Record<string, AdvisorUnitStatus[] | undefined>,
  dismissedIds: readonly string[] = [],
): AdvisorSuggestion[] {
  const found: AdvisorSuggestion[] = []

  for (const building of buildings) {
    const statuses = statusesByBuilding[building.id] ?? []
    if (statuses.length === 0) continue
    const hasPurchaseUnit = building.units.some((unit) => unit.unitType === 'PURCHASE')

    const add = (rule: AdvisorRule, ticks: number | null) => {
      if (ticks === null) return
      found.push({
        id: `${rule}:${building.id}`,
        rule,
        buildingId: building.id,
        buildingName: building.name,
        ticks,
        hasPurchaseUnit,
        linkPath: `/building/${building.id}`,
      })
    }

    if (building.type === 'SALES_SHOP') {
      add('OUT_OF_STOCK_SHOP', longestMatching(building, statuses, 'PUBLIC_SALES', (s) => s.blockedCode === 'NO_INVENTORY', ADVISOR_THRESHOLDS.outOfStockTicks))
      add('SHOP_NO_DEMAND', longestMatching(building, statuses, 'PUBLIC_SALES', (s) => s.blockedCode === 'NO_DEMAND', ADVISOR_THRESHOLDS.noDemandTicks))
    }
    if (building.type === 'FACTORY') {
      add('FACTORY_NO_INPUTS', longestMatching(building, statuses, 'MANUFACTURING', (s) => s.blockedCode === 'NO_INPUTS', ADVISOR_THRESHOLDS.noInputsTicks))
    }
    add('UNCONFIGURED_UNIT', longestMatching(building, statuses, null, (s) => s.status === 'UNCONFIGURED', ADVISOR_THRESHOLDS.unconfiguredTicks))
  }

  // Dismissed suggestions are removed before the limit so the next problem takes the free slot.
  return found
    .filter((suggestion) => !dismissedIds.includes(suggestion.id))
    .sort((a, b) => RULE_ORDER.indexOf(a.rule) - RULE_ORDER.indexOf(b.rule) || b.ticks - a.ticks)
    .slice(0, ADVISOR_MAX_SUGGESTIONS)
}

/** Reads the dismissed suggestion ids from session storage text; anything unexpected means nothing was dismissed. */
export function parseDismissedIds(raw: string | null): string[] {
  if (!raw) return []
  try {
    const parsed: unknown = JSON.parse(raw)
    return Array.isArray(parsed) ? parsed.filter((value): value is string => typeof value === 'string') : []
  } catch {
    return []
  }
}

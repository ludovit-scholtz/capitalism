import { describe, expect, it } from 'vitest'
import {
  getInventorySourcingCostPerUnit,
  getPlannedUnitConstructionCost,
  getTotalInventorySourcingCost,
  getUnitConstructionCost,
  sumPlannedConfigurationCost,
} from '../buildingUnitEconomics'

describe('getUnitConstructionCost', () => {
  it('returns a configured cost for known unit types', () => {
    expect(getUnitConstructionCost('MANUFACTURING')).toBe(12000)
  })

  it('returns 0 for unknown unit types', () => {
    expect(getUnitConstructionCost('UNKNOWN')).toBe(0)
  })
})

describe('getPlannedUnitConstructionCost', () => {
  it('charges full cost when adding a new unit', () => {
    expect(
      getPlannedUnitConstructionCost(null, { unitType: 'PURCHASE', gridX: 0, gridY: 0 }),
    ).toBe(4500)
  })

  it('charges full cost when replacing a unit with a different type', () => {
    expect(
      getPlannedUnitConstructionCost(
        { unitType: 'STORAGE', gridX: 0, gridY: 0 },
        { unitType: 'PUBLIC_SALES', gridX: 0, gridY: 0 },
      ),
    ).toBe(6000)
  })

  it('does not charge for same-type reconfiguration', () => {
    expect(
      getPlannedUnitConstructionCost(
        { unitType: 'PURCHASE', gridX: 0, gridY: 0 },
        { unitType: 'PURCHASE', gridX: 0, gridY: 0 },
      ),
    ).toBe(0)
  })
})

describe('sumPlannedConfigurationCost', () => {
  it('sums only added and replaced units', () => {
    expect(
      sumPlannedConfigurationCost(
        [
          { unitType: 'PURCHASE', gridX: 0, gridY: 0 },
          { unitType: 'STORAGE', gridX: 1, gridY: 0 },
        ],
        [
          { unitType: 'PURCHASE', gridX: 0, gridY: 0 },
          { unitType: 'MANUFACTURING', gridX: 1, gridY: 0 },
          { unitType: 'BRANDING', gridX: 2, gridY: 0 },
        ],
      ),
    ).toBe(19000)
  })
})

describe('getInventorySourcingCostPerUnit', () => {
  it('calculates a per-unit sourcing cost from quantity and total cost', () => {
    expect(getInventorySourcingCostPerUnit(10, 125)).toBe(12.5)
  })

  it('returns null when quantity is empty', () => {
    expect(getInventorySourcingCostPerUnit(0, 125)).toBeNull()
    expect(getInventorySourcingCostPerUnit(null, 125)).toBeNull()
  })
})

describe('getTotalInventorySourcingCost', () => {
  it('sums sourcing costs for positive-quantity rows', () => {
    expect(
      getTotalInventorySourcingCost([
        { quantity: 10, sourcingCostTotal: 125 },
        { quantity: 5, sourcingCostTotal: 42.5 },
      ]),
    ).toBe(167.5)
  })

  it('ignores empty rows', () => {
    expect(
      getTotalInventorySourcingCost([
        { quantity: 0, sourcingCostTotal: 99 },
        { quantity: 2, sourcingCostTotal: 10 },
      ]),
    ).toBe(10)
  })
})

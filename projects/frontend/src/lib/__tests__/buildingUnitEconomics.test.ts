import { describe, expect, it } from 'vitest'
import {
  getConfiguredItemBasePrice,
  getInventoryEstimatedValue,
  getPlannedUnitConstructionCost,
  getUnitConstructionCost,
  sumPlannedConfigurationCost,
} from '../buildingUnitEconomics'

const resourceTypes = [
  {
    id: 'res-wood',
    name: 'Wood',
    slug: 'wood',
    category: 'RAW_MATERIAL',
    basePrice: 12,
    weightPerUnit: 1,
    unitName: 'log',
    unitSymbol: 'log',
    imageUrl: null,
    description: 'Timber resource',
  },
] as const

const productTypes = [
  {
    id: 'prod-chair',
    name: 'Wooden Chair',
    slug: 'wooden-chair',
    industry: 'FURNITURE',
    basePrice: 85,
    baseCraftTicks: 1,
    outputQuantity: 1,
    energyConsumptionMwh: 1,
    unitName: 'chair',
    unitSymbol: 'pc',
    isProOnly: false,
    isUnlockedForCurrentPlayer: true,
    description: 'Starter furniture',
    recipes: [],
  },
] as const

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

describe('getConfiguredItemBasePrice', () => {
  it('prefers configured product base price', () => {
    expect(
      getConfiguredItemBasePrice('res-wood', 'prod-chair', [...resourceTypes], [...productTypes]),
    ).toBe(85)
  })

  it('returns resource base price when only resource is set', () => {
    expect(getConfiguredItemBasePrice('res-wood', null, [...resourceTypes], [...productTypes])).toBe(12)
  })
})

describe('getInventoryEstimatedValue', () => {
  it('calculates resource inventory value', () => {
    expect(
      getInventoryEstimatedValue(10, 'res-wood', null, [...resourceTypes], [...productTypes]),
    ).toBe(120)
  })

  it('calculates product inventory value', () => {
    expect(
      getInventoryEstimatedValue(3, null, 'prod-chair', [...resourceTypes], [...productTypes]),
    ).toBe(255)
  })

  it('returns null when quantity is empty or no configured item exists', () => {
    expect(getInventoryEstimatedValue(0, 'res-wood', null, [...resourceTypes], [...productTypes])).toBeNull()
    expect(getInventoryEstimatedValue(5, null, null, [...resourceTypes], [...productTypes])).toBeNull()
  })
})
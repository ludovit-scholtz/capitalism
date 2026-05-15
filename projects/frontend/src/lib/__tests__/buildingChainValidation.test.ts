import { describe, expect, it } from 'vitest'
import { getProductionChainStatus, hasReachableOutputPath, type ChainValidationUnit } from '../buildingChainValidation'

function makeUnit(overrides: Partial<ChainValidationUnit> = {}): ChainValidationUnit {
  return {
    id: overrides.id ?? 'unit-1',
    unitType: overrides.unitType ?? 'MANUFACTURING',
    gridX: overrides.gridX ?? 0,
    gridY: overrides.gridY ?? 0,
    linkUp: overrides.linkUp ?? false,
    linkDown: overrides.linkDown ?? false,
    linkLeft: overrides.linkLeft ?? false,
    linkRight: overrides.linkRight ?? false,
    linkUpLeft: overrides.linkUpLeft ?? false,
    linkUpRight: overrides.linkUpRight ?? false,
    linkDownLeft: overrides.linkDownLeft ?? false,
    linkDownRight: overrides.linkDownRight ?? false,
    resourceTypeId: overrides.resourceTypeId ?? null,
    productTypeId: overrides.productTypeId ?? null,
  }
}

describe('getProductionChainStatus', () => {
  it('treats storage as optional for a configured factory chain', () => {
    const units = [makeUnit({ id: 'purchase', unitType: 'PURCHASE', resourceTypeId: 'res-wood' }), makeUnit({ id: 'manufacturing', unitType: 'MANUFACTURING', gridX: 1, productTypeId: 'prod-chair' })]

    expect(getProductionChainStatus(units)).toEqual({
      isPurchaseConfigured: true,
      isManufacturingConfigured: true,
      isStoragePresent: false,
      isChainComplete: true,
    })
  })
})

describe('hasReachableOutputPath', () => {
  it('accepts a downstream manufacturing relay before the final output', () => {
    const units = [
      makeUnit({ id: 'manufacturing-a', gridX: 0, linkRight: true, productTypeId: 'prod-chair' }),
      makeUnit({ id: 'manufacturing-b', gridX: 1, linkRight: true, productTypeId: 'prod-chair' }),
      makeUnit({ id: 'sales', unitType: 'B2B_SALES', gridX: 2 }),
    ]

    expect(
      hasReachableOutputPath(units[0]!, units, {
        passthroughUnitTypes: ['MANUFACTURING'],
        terminalUnitTypes: ['STORAGE', 'B2B_SALES', 'PUBLIC_SALES'],
      }),
    ).toBe(true)
  })

  it('returns false when the downstream manufacturing chain has no real output', () => {
    const units = [makeUnit({ id: 'manufacturing-a', gridX: 0, linkRight: true, productTypeId: 'prod-chair' }), makeUnit({ id: 'manufacturing-b', gridX: 1, productTypeId: 'prod-chair' })]

    expect(
      hasReachableOutputPath(units[0]!, units, {
        passthroughUnitTypes: ['MANUFACTURING'],
        terminalUnitTypes: ['STORAGE', 'B2B_SALES', 'PUBLIC_SALES'],
      }),
    ).toBe(false)
  })
})

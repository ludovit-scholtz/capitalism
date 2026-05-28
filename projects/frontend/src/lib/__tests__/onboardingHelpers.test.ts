import { describe, it, expect } from 'vitest'
import {
  stepToKey,
  keyToStep,
  getMaxReachableStep,
  clampStep,
  getAvailableLots,
  getRecommendedFactoryLotIds,
  getRecommendedShopLotIds,
  getDefaultRecommendedLotId,
  canProceedStep3,
  canProceedStep4,
  type OnboardingStepState,
} from '../onboardingHelpers'
import type { BuildingLot } from '@/types'

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeLot(overrides: Partial<BuildingLot> = {}): BuildingLot {
  return {
    id: 'lot-1',
    cityId: 'city-1',
    name: 'Test Lot',
    description: '',
    district: 'General',
    latitude: 48.15,
    longitude: 17.1,
    price: 100_000,
    suitableTypes: 'FACTORY',
    ownerCompanyId: null,
    buildingId: null,
    ownerCompany: null,
    building: null,
    populationIndex: 1,
    basePrice: 100_000,
    resourceType: null,
    materialQuality: null,
    materialQuantity: null,
    ...overrides,
  }
}

// ---------------------------------------------------------------------------
// stepToKey
// ---------------------------------------------------------------------------

describe('stepToKey', () => {
  it('maps step 1 to "city"', () => {
    expect(stepToKey(1)).toBe('city')
  })

  it('maps step 2 to "industry"', () => {
    expect(stepToKey(2)).toBe('industry')
  })

  it('maps step 3 to "product"', () => {
    expect(stepToKey(3)).toBe('product')
  })

  it('maps step 4 to "ipo"', () => {
    expect(stepToKey(4)).toBe('ipo')
  })

  it('maps step 5 to "factory"', () => {
    expect(stepToKey(5)).toBe('factory')
  })

  it('maps step 6 to "shop"', () => {
    expect(stepToKey(6)).toBe('shop')
  })

  it('maps step 7 to "complete"', () => {
    expect(stepToKey(7)).toBe('complete')
  })

  it('falls back to "city" for unknown step values', () => {
    expect(stepToKey(0)).toBe('city')
    expect(stepToKey(8)).toBe('city')
    expect(stepToKey(-1)).toBe('city')
  })
})

// ---------------------------------------------------------------------------
// keyToStep
// ---------------------------------------------------------------------------

describe('keyToStep', () => {
  it('maps "city" to 1', () => {
    expect(keyToStep('city')).toBe(1)
  })

  it('maps "industry" to 2', () => {
    expect(keyToStep('industry')).toBe(2)
  })

  it('maps "product" to 3', () => {
    expect(keyToStep('product')).toBe(3)
  })

  it('maps "ipo" to 4', () => {
    expect(keyToStep('ipo')).toBe(4)
  })

  it('maps "factory" to 5', () => {
    expect(keyToStep('factory')).toBe(5)
  })

  it('maps "shop" to 6', () => {
    expect(keyToStep('shop')).toBe(6)
  })

  it('maps "complete" to 7', () => {
    expect(keyToStep('complete')).toBe(7)
  })

  it('falls back to 1 for unknown keys', () => {
    expect(keyToStep('unknown')).toBe(1)
    expect(keyToStep(null)).toBe(1)
    expect(keyToStep(undefined)).toBe(1)
    expect(keyToStep(42)).toBe(1)
    expect(keyToStep('')).toBe(1)
  })
})

// ---------------------------------------------------------------------------
// stepToKey / keyToStep round-trip
// ---------------------------------------------------------------------------

describe('stepToKey / keyToStep round-trip', () => {
  it('round-trips for all valid steps 1-7', () => {
    for (let step = 1; step <= 7; step++) {
      expect(keyToStep(stepToKey(step))).toBe(step)
    }
  })
})

// ---------------------------------------------------------------------------
// getMaxReachableStep
// ---------------------------------------------------------------------------

describe('getMaxReachableStep', () => {
  const baseState: OnboardingStepState = {
    hasCompletionResult: false,
    isResumingConfigureStep: false,
    onboardingCurrentStep: null,
    hasLocalFactoryProgress: false,
    selectedIndustry: '',
    selectedProductId: '',
    selectedCityId: '',
    hasSelectedIpoPlan: false,
  }

  it('returns 1 when nothing is selected', () => {
    expect(getMaxReachableStep(baseState)).toBe(1)
  })

  it('returns 2 when a city is selected', () => {
    expect(getMaxReachableStep({ ...baseState, selectedCityId: 'city-1' })).toBe(2)
  })

  it('returns 3 when an industry is selected', () => {
    expect(getMaxReachableStep({ ...baseState, selectedCityId: 'city-1', selectedIndustry: 'FURNITURE' })).toBe(3)
  })

  it('returns 4 when a product is selected', () => {
    expect(
      getMaxReachableStep({
        ...baseState,
        selectedCityId: 'city-1',
        selectedIndustry: 'FURNITURE',
        selectedProductId: 'prod-1',
      }),
    ).toBe(4)
  })

  it('returns 5 when an IPO plan is selected', () => {
    expect(
      getMaxReachableStep({
        ...baseState,
        selectedCityId: 'city-1',
        selectedIndustry: 'FURNITURE',
        selectedProductId: 'prod-1',
        hasSelectedIpoPlan: true,
      }),
    ).toBe(5)
  })

  it('returns 6 when onboarding is in SHOP_SELECTION step', () => {
    expect(
      getMaxReachableStep({
        ...baseState,
        selectedCityId: 'city-1',
        selectedIndustry: 'FURNITURE',
        selectedProductId: 'prod-1',
        onboardingCurrentStep: 'SHOP_SELECTION',
      }),
    ).toBe(6)
  })

  it('returns 6 when local factory progress exists after a reload', () => {
    expect(
      getMaxReachableStep({
        ...baseState,
        selectedCityId: 'city-1',
        selectedIndustry: 'FURNITURE',
        selectedProductId: 'prod-1',
        hasSelectedIpoPlan: true,
        hasLocalFactoryProgress: true,
      }),
    ).toBe(6)
  })

  it('returns 7 when there is a completion result', () => {
    expect(getMaxReachableStep({ ...baseState, hasCompletionResult: true })).toBe(7)
  })

  it('returns 7 when resuming configure step', () => {
    expect(getMaxReachableStep({ ...baseState, isResumingConfigureStep: true })).toBe(7)
  })

  it('completion result takes precedence over everything else', () => {
    expect(
      getMaxReachableStep({
        ...baseState,
        hasCompletionResult: true,
        isResumingConfigureStep: true,
        onboardingCurrentStep: 'SHOP_SELECTION',
        selectedIndustry: 'HEALTHCARE',
        selectedProductId: 'prod-2',
        selectedCityId: 'city-x',
        hasSelectedIpoPlan: true,
      }),
    ).toBe(7)
  })
})

// ---------------------------------------------------------------------------
// clampStep
// ---------------------------------------------------------------------------

describe('clampStep', () => {
  it('clamps to 1 when max is 1', () => {
    expect(clampStep(3, 1)).toBe(1)
  })

  it('clamps to max when requested exceeds max', () => {
    expect(clampStep(5, 3)).toBe(3)
  })

  it('returns the requested step when within bounds', () => {
    expect(clampStep(2, 5)).toBe(2)
  })

  it('clamps below minimum to 1', () => {
    expect(clampStep(0, 5)).toBe(1)
    expect(clampStep(-1, 5)).toBe(1)
  })

  it('allows reaching exactly the max step', () => {
    expect(clampStep(5, 5)).toBe(5)
  })
})

// ---------------------------------------------------------------------------
// getAvailableLots
// ---------------------------------------------------------------------------

describe('getAvailableLots', () => {
  const lots = [
    makeLot({ id: 'factory-lot', suitableTypes: 'FACTORY,MINE' }),
    makeLot({ id: 'shop-lot', suitableTypes: 'SALES_SHOP,COMMERCIAL' }),
    makeLot({ id: 'mixed-lot', suitableTypes: 'FACTORY,SALES_SHOP' }),
    makeLot({ id: 'apartment-lot', suitableTypes: 'APARTMENT' }),
  ]

  it('returns only FACTORY-suitable lots', () => {
    const result = getAvailableLots(lots, 'FACTORY')
    expect(result.map((l) => l.id)).toEqual(['factory-lot', 'mixed-lot'])
  })

  it('returns only SALES_SHOP-suitable lots', () => {
    const result = getAvailableLots(lots, 'SALES_SHOP')
    expect(result.map((l) => l.id)).toEqual(['shop-lot', 'mixed-lot'])
  })

  it('returns empty array when no lots match', () => {
    const result = getAvailableLots(lots, 'POWER_PLANT')
    expect(result).toHaveLength(0)
  })

  it('handles extra whitespace in suitableTypes', () => {
    const spacedLot = makeLot({ id: 'spaced', suitableTypes: ' FACTORY , MINE ' })
    const result = getAvailableLots([spacedLot], 'FACTORY')
    expect(result).toHaveLength(1)
  })

  it('returns empty array for empty input', () => {
    expect(getAvailableLots([], 'FACTORY')).toHaveLength(0)
  })
})

// ---------------------------------------------------------------------------
// getRecommendedFactoryLotIds
// ---------------------------------------------------------------------------

describe('getRecommendedFactoryLotIds', () => {
  it('returns top-2 industrial lots sorted by price', () => {
    const lots = [
      makeLot({ id: 'ind-1', district: 'Industrial Zone', price: 120_000 }),
      makeLot({ id: 'ind-2', district: 'Heavy Industrial', price: 80_000 }),
      makeLot({ id: 'ind-3', district: 'Industrial North', price: 100_000 }),
      makeLot({ id: 'res-1', district: 'Residential', price: 60_000 }),
    ]
    expect(getRecommendedFactoryLotIds(lots)).toEqual(['ind-2', 'ind-3'])
  })

  it('falls back to cheapest available lots when no industrial lots exist', () => {
    const lots = [makeLot({ id: 'a', district: 'Downtown', price: 150_000 }), makeLot({ id: 'b', district: 'Suburbs', price: 90_000 }), makeLot({ id: 'c', district: 'Waterfront', price: 120_000 })]
    expect(getRecommendedFactoryLotIds(lots)).toEqual(['b', 'c'])
  })

  it('excludes already-owned lots', () => {
    const lots = [
      makeLot({ id: 'owned', district: 'Industrial Zone', price: 80_000, ownerCompanyId: 'co-1' }),
      makeLot({ id: 'free-ind', district: 'Industrial Park', price: 100_000 }),
      makeLot({ id: 'free-other', district: 'Commercial', price: 70_000 }),
    ]
    expect(getRecommendedFactoryLotIds(lots)).toEqual(['free-ind'])
  })

  it('returns fewer than count when not enough lots available', () => {
    const lots = [makeLot({ id: 'only-one', district: 'Industrial Zone', price: 100_000 })]
    expect(getRecommendedFactoryLotIds(lots)).toHaveLength(1)
  })

  it('returns empty array when all lots are owned', () => {
    const lots = [makeLot({ id: 'owned', ownerCompanyId: 'co-1' })]
    expect(getRecommendedFactoryLotIds(lots)).toHaveLength(0)
  })

  it('respects custom count parameter', () => {
    const lots = [
      makeLot({ id: 'a', district: 'Industrial Zone', price: 80_000 }),
      makeLot({ id: 'b', district: 'Industrial Zone', price: 90_000 }),
      makeLot({ id: 'c', district: 'Industrial Zone', price: 100_000 }),
    ]
    expect(getRecommendedFactoryLotIds(lots, 3)).toHaveLength(3)
    expect(getRecommendedFactoryLotIds(lots, 1)).toHaveLength(1)
  })

  it('returns empty array for empty input', () => {
    expect(getRecommendedFactoryLotIds([])).toHaveLength(0)
  })
})

// ---------------------------------------------------------------------------
// getRecommendedShopLotIds
// ---------------------------------------------------------------------------

describe('getRecommendedShopLotIds', () => {
  it('returns top-2 commercial lots sorted by price', () => {
    const lots = [
      makeLot({ id: 'com-1', district: 'Commercial District', price: 130_000 }),
      makeLot({ id: 'com-2', district: 'Business Center', price: 90_000 }),
      makeLot({ id: 'res-1', district: 'Residential', price: 60_000 }),
    ]
    expect(getRecommendedShopLotIds(lots)).toEqual(['com-2', 'com-1'])
  })

  it('falls back to cheapest available lots when no commercial lots exist', () => {
    const lots = [makeLot({ id: 'a', district: 'Industrial', price: 80_000 }), makeLot({ id: 'b', district: 'Suburban', price: 60_000 })]
    expect(getRecommendedShopLotIds(lots)).toEqual(['b', 'a'])
  })

  it('excludes already-owned lots', () => {
    const lots = [makeLot({ id: 'owned', district: 'Business Park', price: 80_000, ownerCompanyId: 'co-1' }), makeLot({ id: 'free', district: 'Commercial Avenue', price: 90_000 })]
    expect(getRecommendedShopLotIds(lots)).toEqual(['free'])
  })

  it('matches "business" district pattern', () => {
    const lots = [makeLot({ id: 'biz', district: 'Business Quarter', price: 90_000 }), makeLot({ id: 'res', district: 'Residential', price: 70_000 })]
    expect(getRecommendedShopLotIds(lots)).toEqual(['biz'])
  })

  it('returns empty array for empty input', () => {
    expect(getRecommendedShopLotIds([])).toHaveLength(0)
  })

  it('prioritizes higher population index and then shorter factory distance', () => {
    const factoryLot = makeLot({ id: 'factory', latitude: 48.15, longitude: 17.13 })
    const lots = [
      makeLot({ id: 'far-premium', district: 'Commercial District', latitude: 48.3, longitude: 17.3, populationIndex: 1.6, price: 90_000 }),
      makeLot({ id: 'near-high', district: 'Commercial District', latitude: 48.151, longitude: 17.131, populationIndex: 1.42, price: 120_000 }),
      makeLot({ id: 'far-high', district: 'Commercial District', latitude: 48.2, longitude: 17.2, populationIndex: 1.4, price: 80_000 }),
    ]

    expect(getRecommendedShopLotIds(lots, 3, factoryLot)).toEqual(['far-premium', 'near-high', 'far-high'])
  })

  it('uses shorter factory distance before price when population index is close', () => {
    const factoryLot = makeLot({ id: 'factory', latitude: 48.15, longitude: 17.13 })
    const lots = [
      makeLot({ id: 'far-cheap', district: 'Commercial District', latitude: 48.22, longitude: 17.2, populationIndex: 1.4, price: 50_000 }),
      makeLot({ id: 'near-expensive', district: 'Commercial District', latitude: 48.151, longitude: 17.131, populationIndex: 1.45, price: 150_000 }),
    ]

    expect(getRecommendedShopLotIds(lots, 2, factoryLot)).toEqual(['near-expensive', 'far-cheap'])
  })
})

// ---------------------------------------------------------------------------
// getDefaultRecommendedLotId
// ---------------------------------------------------------------------------

describe('getDefaultRecommendedLotId', () => {
  it('selects the first affordable recommended lot', () => {
    const lots = [makeLot({ id: 'a', price: 80_000 }), makeLot({ id: 'b', price: 90_000 })]

    expect(getDefaultRecommendedLotId(lots, ['b', 'a'], 100_000)).toBe('b')
  })

  it('falls back to the first affordable lot when recommendations are not affordable', () => {
    const lots = [makeLot({ id: 'expensive', price: 300_000 }), makeLot({ id: 'fallback', price: 90_000 })]

    expect(getDefaultRecommendedLotId(lots, ['expensive'], 100_000)).toBe('fallback')
  })

  it('skips an unaffordable first recommendation and selects the next affordable recommendation', () => {
    const lots = [makeLot({ id: 'expensive', price: 300_000 }), makeLot({ id: 'affordable-recommended', price: 90_000 }), makeLot({ id: 'fallback', price: 80_000 })]

    expect(getDefaultRecommendedLotId(lots, ['expensive', 'affordable-recommended'], 100_000)).toBe('affordable-recommended')
  })

  it('returns an empty string when no lot can be purchased', () => {
    const lots = [makeLot({ id: 'owned', price: 50_000, ownerCompanyId: 'co-1' }), makeLot({ id: 'expensive', price: 300_000 })]

    expect(getDefaultRecommendedLotId(lots, ['owned', 'expensive'], 100_000)).toBe('')
  })
})

// ---------------------------------------------------------------------------
// canProceedStep3
// ---------------------------------------------------------------------------

describe('canProceedStep3', () => {
  const freeLot = makeLot({ ownerCompanyId: null, price: 75_000 })
  const ownedLot = makeLot({ ownerCompanyId: 'someone-else', price: 75_000 })
  const expensiveLot = makeLot({ ownerCompanyId: null, price: 600_000 })

  it('returns true when all conditions are met', () => {
    expect(canProceedStep3(freeLot, 500_000)).toBe(true)
  })

  it('returns false when no lot is selected', () => {
    expect(canProceedStep3(null, 500_000)).toBe(false)
  })

  it('returns false when the lot is already owned', () => {
    expect(canProceedStep3(ownedLot, 500_000)).toBe(false)
  })

  it('returns false when starting cash is insufficient', () => {
    expect(canProceedStep3(expensiveLot, 500_000)).toBe(false)
  })

  it('returns true at exact budget boundary', () => {
    const exactLot = makeLot({ ownerCompanyId: null, price: 500_000 })
    expect(canProceedStep3(exactLot, 500_000)).toBe(true)
  })
})

// ---------------------------------------------------------------------------
// canProceedStep4
// ---------------------------------------------------------------------------

describe('canProceedStep4', () => {
  const freeShopLot = makeLot({ suitableTypes: 'SALES_SHOP', ownerCompanyId: null, price: 90_000 })
  const ownedShopLot = makeLot({
    suitableTypes: 'SALES_SHOP',
    ownerCompanyId: 'someone',
    price: 90_000,
  })
  const expensiveShopLot = makeLot({
    suitableTypes: 'SALES_SHOP',
    ownerCompanyId: null,
    price: 999_000,
  })

  it('returns true when all conditions are met', () => {
    expect(canProceedStep4('product-1', freeShopLot, 200_000)).toBe(true)
  })

  it('returns false when no product is selected', () => {
    expect(canProceedStep4('', freeShopLot, 200_000)).toBe(false)
  })

  it('returns false when no shop lot is selected', () => {
    expect(canProceedStep4('product-1', null, 200_000)).toBe(false)
  })

  it('returns false when the shop lot is already owned', () => {
    expect(canProceedStep4('product-1', ownedShopLot, 200_000)).toBe(false)
  })

  it('returns false when available cash is insufficient', () => {
    expect(canProceedStep4('product-1', expensiveShopLot, 200_000)).toBe(false)
  })

  it('returns true at exact cash boundary', () => {
    const exactLot = makeLot({ suitableTypes: 'SALES_SHOP', ownerCompanyId: null, price: 200_000 })
    expect(canProceedStep4('product-1', exactLot, 200_000)).toBe(true)
  })

  it('returns false when available cash is zero', () => {
    expect(canProceedStep4('product-1', freeShopLot, 0)).toBe(false)
  })
})

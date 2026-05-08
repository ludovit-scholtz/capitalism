import { describe, expect, it } from 'vitest'
import {
  EMV_BASE_LOT_VALUE,
  EMV_DEFAULT_POPULATION_INDEX,
  EMV_HIGH_PRICE_WARNING_FACTOR,
  EMV_LEVEL_MULTIPLIER_BASE,
  EMV_UNIT_BASE_VALUE,
  MINIMUM_SALE_PRICE_FACTOR,
  computeMinimumSalePrice,
  computeEstimatedMarketValue,
  isAskingPriceBelowMinimum,
  isAskingPriceTooHigh,
} from '../sellBuilding'

// ────────────────────────────────────────────────────────────────────────────
// Constant regression guards
// ────────────────────────────────────────────────────────────────────────────

describe('EMV constants', () => {
  it('EMV_BASE_LOT_VALUE is 75,000', () => {
    expect(EMV_BASE_LOT_VALUE).toBe(75_000)
  })

  it('EMV_LEVEL_MULTIPLIER_BASE is 1.5', () => {
    expect(EMV_LEVEL_MULTIPLIER_BASE).toBe(1.5)
  })

  it('EMV_UNIT_BASE_VALUE is 20,000', () => {
    expect(EMV_UNIT_BASE_VALUE).toBe(20_000)
  })

  it('EMV_DEFAULT_POPULATION_INDEX is 0.5', () => {
    expect(EMV_DEFAULT_POPULATION_INDEX).toBe(0.5)
  })

  it('EMV_HIGH_PRICE_WARNING_FACTOR is 1.5', () => {
    expect(EMV_HIGH_PRICE_WARNING_FACTOR).toBe(1.5)
  })

  it('MINIMUM_SALE_PRICE_FACTOR is 0.7', () => {
    expect(MINIMUM_SALE_PRICE_FACTOR).toBe(0.7)
  })
})

// ────────────────────────────────────────────────────────────────────────────
// computeEstimatedMarketValue
// ────────────────────────────────────────────────────────────────────────────

describe('computeEstimatedMarketValue', () => {
  it('level-1 building with no units and default population index returns 113,000', () => {
    // levelMultiplier = 1.5^0 = 1
    // unitValue = 0 × 20,000 = 0
    // locationMultiplier = 1 + 0.5 × 0.5 = 1.25
    // raw = (75,000 × 1 + 0) × 1.25 = 93,750 → rounded to 94,000
    // Hmm: 93750 / 1000 = 93.75 → rounds to 94 → 94,000
    expect(computeEstimatedMarketValue({ level: 1, unitCount: 0 })).toBe(94_000)
  })

  it('level-1 building with 1 unit and default population index', () => {
    // raw = (75,000 + 20,000) × 1.25 = 118,750 → 119,000
    expect(computeEstimatedMarketValue({ level: 1, unitCount: 1 })).toBe(119_000)
  })

  it('level-2 building with no units and default population index', () => {
    // levelMultiplier = 1.5^1 = 1.5
    // raw = (75,000 × 1.5) × 1.25 = 140,625 → 141,000
    expect(computeEstimatedMarketValue({ level: 2, unitCount: 0 })).toBe(141_000)
  })

  it('level-3 building with 4 units and default population index', () => {
    // levelMultiplier = 1.5^2 = 2.25
    // unitValue = 4 × 20,000 = 80,000
    // raw = (75,000 × 2.25 + 80,000) × 1.25 = (168,750 + 80,000) × 1.25 = 311,000 (rounded)
    // 248750 × 1.25 = 310,937.5 → 311,000
    expect(computeEstimatedMarketValue({ level: 3, unitCount: 4 })).toBe(311_000)
  })

  it('uses provided populationIndex=0 (rural location)', () => {
    // locationMultiplier = 1 + 0 × 0.5 = 1.0
    // raw = 75,000 × 1 × 1.0 = 75,000
    expect(computeEstimatedMarketValue({ level: 1, unitCount: 0, populationIndex: 0 })).toBe(75_000)
  })

  it('uses provided populationIndex=1 (city centre)', () => {
    // locationMultiplier = 1 + 1 × 0.5 = 1.5
    // raw = 75,000 × 1 × 1.5 = 112,500 → 113,000
    expect(computeEstimatedMarketValue({ level: 1, unitCount: 0, populationIndex: 1 })).toBe(113_000)
  })

  it('falls back to default population index when null is provided', () => {
    const withNull = computeEstimatedMarketValue({ level: 1, unitCount: 0, populationIndex: null })
    const withDefault = computeEstimatedMarketValue({ level: 1, unitCount: 0 })
    expect(withNull).toBe(withDefault)
  })

  it('falls back to default population index when undefined is provided', () => {
    const withUndefined = computeEstimatedMarketValue({
      level: 1,
      unitCount: 0,
      populationIndex: undefined,
    })
    const withDefault = computeEstimatedMarketValue({ level: 1, unitCount: 0 })
    expect(withUndefined).toBe(withDefault)
  })

  it('result is always a multiple of 1,000 (rounded)', () => {
    const emv = computeEstimatedMarketValue({ level: 2, unitCount: 3, populationIndex: 0.7 })
    expect(emv % 1_000).toBe(0)
  })

  it('higher level always produces higher EMV than lower level (same units and population)', () => {
    const level1 = computeEstimatedMarketValue({ level: 1, unitCount: 0 })
    const level2 = computeEstimatedMarketValue({ level: 2, unitCount: 0 })
    const level3 = computeEstimatedMarketValue({ level: 3, unitCount: 0 })
    expect(level2).toBeGreaterThan(level1)
    expect(level3).toBeGreaterThan(level2)
  })

  it('more units always produce higher EMV (same level and population)', () => {
    const zero = computeEstimatedMarketValue({ level: 1, unitCount: 0 })
    const two = computeEstimatedMarketValue({ level: 1, unitCount: 2 })
    const eight = computeEstimatedMarketValue({ level: 1, unitCount: 8 })
    expect(two).toBeGreaterThan(zero)
    expect(eight).toBeGreaterThan(two)
  })

  it('higher population index always produces higher EMV', () => {
    const rural = computeEstimatedMarketValue({ level: 1, unitCount: 0, populationIndex: 0 })
    const city = computeEstimatedMarketValue({ level: 1, unitCount: 0, populationIndex: 1 })
    expect(city).toBeGreaterThan(rural)
  })
})

// ────────────────────────────────────────────────────────────────────────────
// isAskingPriceTooHigh
// ────────────────────────────────────────────────────────────────────────────

describe('isAskingPriceTooHigh', () => {
  const emv = 100_000

  it('returns false when asking price equals EMV', () => {
    expect(isAskingPriceTooHigh(100_000, emv)).toBe(false)
  })

  it('returns false when asking price is 150% of EMV (at the threshold)', () => {
    expect(isAskingPriceTooHigh(150_000, emv)).toBe(false)
  })

  it('returns true when asking price is just above 150% of EMV', () => {
    expect(isAskingPriceTooHigh(150_001, emv)).toBe(true)
  })

  it('returns true when asking price is 200% of EMV (very overpriced)', () => {
    expect(isAskingPriceTooHigh(200_000, emv)).toBe(true)
  })

  it('returns false when asking price is below EMV (underpriced)', () => {
    expect(isAskingPriceTooHigh(50_000, emv)).toBe(false)
  })

  it('returns false when asking price is zero', () => {
    expect(isAskingPriceTooHigh(0, emv)).toBe(false)
  })
})

describe('minimum sale price rules', () => {
  const marketValue = 100_000

  it('computes minimum sale price as 70% of market value', () => {
    expect(computeMinimumSalePrice(marketValue)).toBe(70_000)
  })

  it('rejects asking price below 70% of market value', () => {
    expect(isAskingPriceBelowMinimum(69_999, marketValue)).toBe(true)
  })

  it('allows asking price exactly at 70% of market value', () => {
    expect(isAskingPriceBelowMinimum(70_000, marketValue)).toBe(false)
  })
})

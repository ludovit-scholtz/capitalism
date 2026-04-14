/**
 * Unit tests for the grid tile display helpers in gridTileHelpers.ts.
 * All helpers are pure functions with no Vue or i18n dependencies.
 */

import { describe, expect, it } from 'vitest'
import {
  formatPercent,
  formatUnitQuantity,
  getFillBucket,
  getUnitConfiguredItemId,
  getUnitPriceMetric,
} from '../gridTileHelpers'

// ---------------------------------------------------------------------------
// formatUnitQuantity
// ---------------------------------------------------------------------------

describe('formatUnitQuantity', () => {
  it('returns integer as plain string', () => {
    expect(formatUnitQuantity(60)).toBe('60')
  })

  it('returns 0 as plain string', () => {
    expect(formatUnitQuantity(0)).toBe('0')
  })

  it('returns fractional value with trailing zeros stripped', () => {
    expect(formatUnitQuantity(60.5)).toBe('60.5')
  })

  it('trims trailing zeros after decimal point', () => {
    expect(formatUnitQuantity(60.5)).toBe('60.5')
    expect(formatUnitQuantity(60.1)).toBe('60.1')
  })

  it('rounds to at most 2 decimal places', () => {
    expect(formatUnitQuantity(12.345)).toBe('12.35')
  })

  it('handles negative integers', () => {
    expect(formatUnitQuantity(-5)).toBe('-5')
  })

  it('handles large integers', () => {
    expect(formatUnitQuantity(1000)).toBe('1000')
  })
})

// ---------------------------------------------------------------------------
// formatPercent
// ---------------------------------------------------------------------------

describe('formatPercent', () => {
  it('formats 0 as 0%', () => {
    expect(formatPercent(0)).toBe('0%')
  })

  it('formats 1.0 as 100%', () => {
    expect(formatPercent(1)).toBe('100%')
  })

  it('formats mid value rounded', () => {
    expect(formatPercent(0.75)).toBe('75%')
  })

  it('rounds fractional percentages', () => {
    expect(formatPercent(0.333)).toBe('33%')
    expect(formatPercent(0.666)).toBe('67%')
  })

  it('returns em dash for null', () => {
    expect(formatPercent(null)).toBe('—')
  })

  it('returns em dash for undefined', () => {
    expect(formatPercent(undefined)).toBe('—')
  })
})

// ---------------------------------------------------------------------------
// getFillBucket
// ---------------------------------------------------------------------------

describe('getFillBucket', () => {
  it('returns empty for null', () => {
    expect(getFillBucket(null)).toBe('empty')
  })

  it('returns empty for undefined', () => {
    expect(getFillBucket(undefined)).toBe('empty')
  })

  it('returns empty for 0', () => {
    expect(getFillBucket(0)).toBe('empty')
  })

  it('returns low for values below 0.30', () => {
    expect(getFillBucket(0.01)).toBe('low')
    expect(getFillBucket(0.1)).toBe('low')
    expect(getFillBucket(0.29)).toBe('low')
  })

  it('returns medium at exactly 0.30', () => {
    expect(getFillBucket(0.3)).toBe('medium')
  })

  it('returns medium up to (not including) 0.75', () => {
    expect(getFillBucket(0.5)).toBe('medium')
    expect(getFillBucket(0.74)).toBe('medium')
  })

  it('returns high at exactly 0.75', () => {
    expect(getFillBucket(0.75)).toBe('high')
  })

  it('returns high for values above 0.75', () => {
    expect(getFillBucket(0.9)).toBe('high')
    expect(getFillBucket(1.0)).toBe('high')
  })
})

// ---------------------------------------------------------------------------
// getUnitConfiguredItemId
// ---------------------------------------------------------------------------

describe('getUnitConfiguredItemId', () => {
  it('returns null when neither field is set', () => {
    expect(getUnitConfiguredItemId({ productTypeId: null, resourceTypeId: null })).toBeNull()
  })

  it('returns null for empty unit object', () => {
    expect(getUnitConfiguredItemId({})).toBeNull()
  })

  it('returns product result when productTypeId is set', () => {
    expect(getUnitConfiguredItemId({ productTypeId: 'prod-1', resourceTypeId: null })).toEqual({
      kind: 'product',
      id: 'prod-1',
    })
  })

  it('returns resource result when resourceTypeId is set and no product', () => {
    expect(getUnitConfiguredItemId({ productTypeId: null, resourceTypeId: 'res-wood' })).toEqual({
      kind: 'resource',
      id: 'res-wood',
    })
  })

  it('prefers product over resource when both are set', () => {
    expect(
      getUnitConfiguredItemId({ productTypeId: 'prod-chair', resourceTypeId: 'res-wood' }),
    ).toEqual({ kind: 'product', id: 'prod-chair' })
  })

  it('handles missing keys as undefined (no product, no resource)', () => {
    expect(getUnitConfiguredItemId({})).toBeNull()
  })

  it('handles empty-string ids as falsy (returns null)', () => {
    expect(getUnitConfiguredItemId({ productTypeId: '', resourceTypeId: '' })).toBeNull()
  })
})

// ---------------------------------------------------------------------------
// getUnitPriceMetric
// ---------------------------------------------------------------------------

describe('getUnitPriceMetric', () => {
  it('returns null for empty unit', () => {
    expect(getUnitPriceMetric({})).toBeNull()
  })

  it('returns null when all fields are null', () => {
    expect(getUnitPriceMetric({ minPrice: null, maxPrice: null, budget: null, brandScope: null })).toBeNull()
  })

  it('returns minPrice when set', () => {
    expect(getUnitPriceMetric({ minPrice: 120 })).toEqual({ kind: 'minPrice', value: 120 })
  })

  it('returns maxPrice when only maxPrice is set', () => {
    expect(getUnitPriceMetric({ maxPrice: 500 })).toEqual({ kind: 'maxPrice', value: 500 })
  })

  it('returns budget when only budget is set', () => {
    expect(getUnitPriceMetric({ budget: 2500 })).toEqual({ kind: 'budget', value: 2500 })
  })

  it('returns scope when only brandScope is set', () => {
    expect(getUnitPriceMetric({ brandScope: 'PRODUCT' })).toEqual({
      kind: 'scope',
      value: 'PRODUCT',
    })
  })

  it('prefers minPrice over maxPrice', () => {
    expect(getUnitPriceMetric({ minPrice: 100, maxPrice: 500 })).toEqual({
      kind: 'minPrice',
      value: 100,
    })
  })

  it('prefers maxPrice over budget', () => {
    expect(getUnitPriceMetric({ maxPrice: 500, budget: 2500 })).toEqual({
      kind: 'maxPrice',
      value: 500,
    })
  })

  it('prefers budget over scope', () => {
    expect(getUnitPriceMetric({ budget: 2500, brandScope: 'COMPANY' })).toEqual({
      kind: 'budget',
      value: 2500,
    })
  })

  it('handles zero minPrice as a valid price (not skipped)', () => {
    expect(getUnitPriceMetric({ minPrice: 0 })).toEqual({ kind: 'minPrice', value: 0 })
  })

  it('handles zero maxPrice as a valid price (not skipped)', () => {
    expect(getUnitPriceMetric({ maxPrice: 0 })).toEqual({ kind: 'maxPrice', value: 0 })
  })

  it('zero minPrice still takes priority over maxPrice when set', () => {
    expect(getUnitPriceMetric({ minPrice: 0, maxPrice: 500 })).toEqual({
      kind: 'minPrice',
      value: 0,
    })
  })

  // representative mine unit (B2B_SALES with minPrice)
  it('returns minPrice for a mine B2B_SALES unit', () => {
    const unit = { resourceTypeId: 'res-iron', minPrice: 80 }
    expect(getUnitPriceMetric(unit)).toEqual({ kind: 'minPrice', value: 80 })
  })

  // representative factory unit (MANUFACTURING – no price configured)
  it('returns null for a manufacturing unit with no pricing', () => {
    const unit = { productTypeId: 'prod-chair' }
    expect(getUnitPriceMetric(unit)).toBeNull()
  })

  // representative R&D unit (brandScope set)
  it('returns scope for an R&D BRANDING unit', () => {
    const unit = { brandScope: 'CATEGORY' }
    expect(getUnitPriceMetric(unit)).toEqual({ kind: 'scope', value: 'CATEGORY' })
  })
})

// ---------------------------------------------------------------------------
// getFlowSegments
// ---------------------------------------------------------------------------

import { getFlowSegments } from '../gridTileHelpers'

describe('getFlowSegments', () => {
  it('returns no-movement result when lastTickInflow and lastTickOutflow are both null', () => {
    const result = getFlowSegments(0.6, 100, null, null)
    expect(result.hasMovement).toBe(false)
    expect(result.fillWidth).toBe(60)
    expect(result.inflowWidth).toBe(0)
    expect(result.outflowWidth).toBe(0)
  })

  it('returns no-movement result when capacity is zero', () => {
    const result = getFlowSegments(0, 0, 10, 5)
    expect(result.hasMovement).toBe(false)
  })

  it('shows inflow overlay at the right edge of the fill bar', () => {
    // fill 70%, inflow 10 out of 100 capacity (10%)
    const result = getFlowSegments(0.7, 100, 10, 0)
    expect(result.hasMovement).toBe(true)
    expect(result.fillWidth).toBeCloseTo(60) // 70% - 10% inflow
    expect(result.inflowWidth).toBeCloseTo(10) // 10% inflow
    expect(result.inflowLeft).toBeCloseTo(60) // starts at base fill
    expect(result.outflowWidth).toBe(0)
    expect(result.outflowLeft).toBeCloseTo(70)
  })

  it('shows outflow in the empty area after the fill bar', () => {
    // fill 65%, outflow 5 out of 100 capacity (5%)
    const result = getFlowSegments(0.65, 100, 0, 5)
    expect(result.hasMovement).toBe(true)
    expect(result.fillWidth).toBeCloseTo(65) // unchanged (no inflow)
    expect(result.inflowWidth).toBe(0)
    expect(result.outflowWidth).toBeCloseTo(5)
    expect(result.outflowLeft).toBeCloseTo(65)
  })

  it('clamps inflow to current fill so it does not exceed the fill bar', () => {
    // fill 20%, inflow 50 out of 100 capacity (50% > fill 20%)
    const result = getFlowSegments(0.2, 100, 50, 0)
    expect(result.hasMovement).toBe(true)
    expect(result.inflowWidth).toBeCloseTo(20) // clamped to fill
    expect(result.fillWidth).toBeCloseTo(0)
  })

  it('clamps outflow to empty space so it does not exceed the track', () => {
    // fill 90%, outflow 50 out of 100 capacity (50% > empty space 10%)
    const result = getFlowSegments(0.9, 100, 0, 50)
    expect(result.hasMovement).toBe(true)
    expect(result.outflowWidth).toBeCloseTo(10) // clamped to empty space
    expect(result.outflowLeft).toBeCloseTo(90)
  })

  it('handles both inflow and outflow simultaneously', () => {
    // fill 60%, inflow 10/100 (10%), outflow 15/100 (15%)
    const result = getFlowSegments(0.6, 100, 10, 15)
    expect(result.hasMovement).toBe(true)
    expect(result.fillWidth).toBeCloseTo(50) // 60% - 10%
    expect(result.inflowWidth).toBeCloseTo(10)
    expect(result.outflowWidth).toBeCloseTo(15) // empty space = 40%, fits
    expect(result.outflowLeft).toBeCloseTo(60)
  })

  it('handles null fillPercent (treats as zero fill)', () => {
    const result = getFlowSegments(null, 100, 5, 3)
    expect(result.hasMovement).toBe(true)
    expect(result.fillWidth).toBe(0) // 0% fill
    expect(result.inflowWidth).toBe(0) // inflow clamped to 0% fill
    expect(result.outflowWidth).toBeCloseTo(3) // outflow in the full empty area
  })

  it('returns zero-movement result when capacity is null', () => {
    const result = getFlowSegments(0.5, null, 10, 5)
    expect(result.hasMovement).toBe(false)
  })
})


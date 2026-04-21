import { describe, it, expect } from 'vitest'
import {
  fmtBuildingAmount,
  fmtBuildingProfit,
  profitClass,
  hasFinancialData,
} from '@/lib/buildingHeaderFormatters'

describe('fmtBuildingAmount', () => {
  it('formats positive integers with EUR currency', () => {
    expect(fmtBuildingAmount(12000, 'en', 'EUR')).toBe('€12,000')
  })

  it('formats positive integers with USD currency', () => {
    expect(fmtBuildingAmount(12000, 'en', 'USD')).toBe('$12,000')
  })

  it('formats positive integers with CZK currency', () => {
    expect(fmtBuildingAmount(12000, 'en', 'CZK')).toBe('CZK\u00a012,000')
  })

  it('formats zero as €0 with default EUR', () => {
    expect(fmtBuildingAmount(0, 'en', 'EUR')).toBe('€0')
  })

  it('formats negative values as absolute (no minus sign)', () => {
    expect(fmtBuildingAmount(-4500, 'en', 'EUR')).toBe('€4,500')
  })

  it('truncates decimals to whole units', () => {
    expect(fmtBuildingAmount(999.99, 'en', 'EUR')).toBe('€1,000')
  })

  it('returns — for null', () => {
    expect(fmtBuildingAmount(null)).toBe('—')
  })

  it('returns — for NaN', () => {
    expect(fmtBuildingAmount(NaN)).toBe('—')
  })

  it('returns — for Infinity', () => {
    expect(fmtBuildingAmount(Infinity)).toBe('—')
  })

  it('returns — for -Infinity', () => {
    expect(fmtBuildingAmount(-Infinity)).toBe('—')
  })

  it('defaults to EUR when no currency code provided', () => {
    const result = fmtBuildingAmount(1000)
    expect(result).toContain('1,000')
    expect(result).toContain('€')
  })
})

describe('fmtBuildingProfit', () => {
  it('prefixes positive profit with + (EUR)', () => {
    expect(fmtBuildingProfit(7500, 'en', 'EUR')).toBe('+€7,500')
  })

  it('prefixes negative profit with - (EUR)', () => {
    expect(fmtBuildingProfit(-2000, 'en', 'EUR')).toBe('-€2,000')
  })

  it('formats zero profit with no sign prefix', () => {
    expect(fmtBuildingProfit(0, 'en', 'EUR')).toBe('€0')
  })

  it('returns — for null', () => {
    expect(fmtBuildingProfit(null)).toBe('—')
  })

  it('returns — for NaN', () => {
    expect(fmtBuildingProfit(NaN)).toBe('—')
  })

  it('returns — for Infinity', () => {
    expect(fmtBuildingProfit(Infinity)).toBe('—')
  })

  it('formats large profit values correctly (EUR)', () => {
    expect(fmtBuildingProfit(1000000, 'en', 'EUR')).toBe('+€1,000,000')
  })

  it('formats CZK profit with correct prefix', () => {
    const result = fmtBuildingProfit(7500, 'en', 'CZK')
    expect(result).toMatch(/^\+CZK/)
    expect(result).toContain('7,500')
  })
})

describe('profitClass', () => {
  it('returns bh-positive for positive profit', () => {
    expect(profitClass(500)).toBe('bh-positive')
  })

  it('returns bh-negative for negative profit', () => {
    expect(profitClass(-1)).toBe('bh-negative')
  })

  it('returns bh-neutral for zero profit', () => {
    expect(profitClass(0)).toBe('bh-neutral')
  })

  it('returns empty string for null', () => {
    expect(profitClass(null)).toBe('')
  })
})

describe('hasFinancialData', () => {
  it('returns true when revenue > 0', () => {
    expect(hasFinancialData(100, 0, 100)).toBe(true)
  })

  it('returns true when costs > 0', () => {
    expect(hasFinancialData(0, 50, -50)).toBe(true)
  })

  it('returns true when profit !== 0', () => {
    expect(hasFinancialData(0, 0, -5)).toBe(true)
  })

  it('returns false when all values are zero', () => {
    expect(hasFinancialData(0, 0, 0)).toBe(false)
  })

  it('returns false when revenue is null', () => {
    expect(hasFinancialData(null, 100, 50)).toBe(false)
  })

  it('returns false when costs is null', () => {
    expect(hasFinancialData(100, null, 50)).toBe(false)
  })

  it('returns false when profit is null', () => {
    expect(hasFinancialData(100, 50, null)).toBe(false)
  })

  it('returns false when all are null', () => {
    expect(hasFinancialData(null, null, null)).toBe(false)
  })
})

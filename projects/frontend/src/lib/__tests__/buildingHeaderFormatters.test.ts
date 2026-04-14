import { describe, it, expect } from 'vitest'
import {
  fmtBuildingAmount,
  fmtBuildingProfit,
  profitClass,
  hasFinancialData,
} from '@/lib/buildingHeaderFormatters'

describe('fmtBuildingAmount', () => {
  it('formats positive integers correctly', () => {
    expect(fmtBuildingAmount(12000)).toBe('$12,000')
  })

  it('formats zero as $0', () => {
    expect(fmtBuildingAmount(0)).toBe('$0')
  })

  it('formats negative values as absolute (no minus sign)', () => {
    expect(fmtBuildingAmount(-4500)).toBe('$4,500')
  })

  it('truncates decimals to whole dollars', () => {
    expect(fmtBuildingAmount(999.99)).toBe('$1,000')
  })

  it('returns $— for null', () => {
    expect(fmtBuildingAmount(null)).toBe('$—')
  })

  it('returns $— for NaN', () => {
    expect(fmtBuildingAmount(NaN)).toBe('$—')
  })

  it('returns $— for Infinity', () => {
    expect(fmtBuildingAmount(Infinity)).toBe('$—')
  })

  it('returns $— for -Infinity', () => {
    expect(fmtBuildingAmount(-Infinity)).toBe('$—')
  })
})

describe('fmtBuildingProfit', () => {
  it('prefixes positive profit with +', () => {
    expect(fmtBuildingProfit(7500)).toBe('+$7,500')
  })

  it('prefixes negative profit with -', () => {
    expect(fmtBuildingProfit(-2000)).toBe('-$2,000')
  })

  it('formats zero profit with no sign prefix', () => {
    expect(fmtBuildingProfit(0)).toBe('$0')
  })

  it('returns $— for null', () => {
    expect(fmtBuildingProfit(null)).toBe('$—')
  })

  it('returns $— for NaN', () => {
    expect(fmtBuildingProfit(NaN)).toBe('$—')
  })

  it('returns $— for Infinity', () => {
    expect(fmtBuildingProfit(Infinity)).toBe('$—')
  })

  it('formats large profit values correctly', () => {
    expect(fmtBuildingProfit(1000000)).toBe('+$1,000,000')
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

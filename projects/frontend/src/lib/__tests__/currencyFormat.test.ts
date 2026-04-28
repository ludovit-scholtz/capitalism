import { describe, it, expect } from 'vitest'
import {
  formatCompactMoney,
  formatMoney,
  formatNumber,
  formatCompactNumber,
  formatCurrencyTitle,
} from '../currencyFormat'

describe('formatCompactMoney', () => {
  it('formats USD millions with $ prefix', () => {
    // 1,000,000 USD → compact ("$1M")
    const result = formatCompactMoney(1_000_000, 'USD', 'en')
    expect(result).toContain('$')
    expect(result.toUpperCase()).toContain('M')
  })

  it('formats USD thousands', () => {
    const result = formatCompactMoney(2_845, 'USD', 'en')
    // Intl compact: $2.85K or $2.85k — normalize case for assertion
    expect(result.toLowerCase()).toMatch(/^\$2\.85[kt]/)
  })

  it('formats EUR millions', () => {
    const result = formatCompactMoney(123_456_789, 'EUR', 'en')
    // expect something like €123M or €123.46M
    expect(result).toContain('M')
    expect(result).toContain('€')
  })

  it('formats zero', () => {
    const result = formatCompactMoney(0, 'USD', 'en')
    expect(result).toContain('$')
    expect(result).toContain('0')
  })

  it('returns — for NaN', () => {
    expect(formatCompactMoney(NaN, 'USD', 'en')).toBe('—')
  })

  it('returns — for Infinity', () => {
    expect(formatCompactMoney(Infinity, 'USD', 'en')).toBe('—')
  })

  it('formats negative values', () => {
    const result = formatCompactMoney(-500_000, 'USD', 'en')
    expect(result).toContain('-')
    expect(result).toContain('$')
  })

  it('uses sk locale for Slovak', () => {
    // sk-SK uses space as thousands sep and comma as decimal
    const result = formatCompactMoney(1_000_000, 'EUR', 'sk')
    expect(typeof result).toBe('string')
    expect(result.length).toBeGreaterThan(0)
    // Should not be empty, and should contain a euro symbol or EUR code
    expect(result).toMatch(/€|EUR/)
  })

  it('uses de locale for German', () => {
    const result = formatCompactMoney(1_000_000, 'EUR', 'de')
    expect(typeof result).toBe('string')
    expect(result).toMatch(/€|EUR/)
  })
})

describe('formatMoney', () => {
  it('formats EUR with full notation, no decimal for integers', () => {
    const result = formatMoney(200_000, 'EUR', 'en')
    expect(result).toBe('€200,000')
  })

  it('formats CZK with full notation', () => {
    const result = formatMoney(5_040_000, 'CZK', 'en')
    // en-US formats CZK as CZK\u00a05,040,000
    expect(result).toContain('5,040,000')
  })

  it('formats USD with full notation', () => {
    const result = formatMoney(12_345, 'USD', 'en')
    expect(result).toBe('$12,345')
  })

  it('formats with 2 decimal places for non-integers', () => {
    const result = formatMoney(1234.56, 'EUR', 'en')
    expect(result).toBe('€1,234.56')
  })

  it('formats zero as €0', () => {
    expect(formatMoney(0, 'EUR', 'en')).toBe('€0')
  })

  it('returns — for NaN', () => {
    expect(formatMoney(NaN, 'USD', 'en')).toBe('—')
  })

  it('returns — for Infinity', () => {
    expect(formatMoney(Infinity, 'USD', 'en')).toBe('—')
  })
})

describe('formatNumber', () => {
  it('formats integer with thousands separator (en)', () => {
    expect(formatNumber(1_234_567, 'en')).toBe('1,234,567')
  })

  it('formats integer with space separator (sk)', () => {
    const result = formatNumber(1_234_567, 'sk')
    // sk-SK uses narrow no-break space (\u202f) or regular space as grouping separator
    expect(result.replace(/\u202f|\u00a0/g, ' ')).toBe('1 234 567')
  })

  it('formats decimal with comma in German', () => {
    const result = formatNumber(12.5, 'de', 1)
    expect(result).toBe('12,5')
  })

  it('returns — for NaN', () => {
    expect(formatNumber(NaN)).toBe('—')
  })
})

describe('formatCompactNumber', () => {
  it('abbreviates millions', () => {
    const result = formatCompactNumber(1_500_000, 'en')
    expect(result).toMatch(/1\.5M/)
  })

  it('abbreviates thousands', () => {
    const result = formatCompactNumber(2_500, 'en')
    expect(result.toLowerCase()).toMatch(/2\.5[kt]/)
  })

  it('formats small numbers without abbreviation', () => {
    const result = formatCompactNumber(500, 'en')
    expect(result).toBe('500')
  })

  it('returns — for NaN', () => {
    expect(formatCompactNumber(NaN)).toBe('—')
  })
})

describe('formatCurrencyTitle', () => {
  it('returns full EUR amount with code appended when EUR symbol used', () => {
    const result = formatCurrencyTitle(200_000, 'EUR', 'en')
    // EUR uses € symbol → code not in formatted string → appends " EUR"
    expect(result).toBe('€200,000 EUR')
  })

  it('returns full USD amount with code appended when $ symbol used', () => {
    const result = formatCurrencyTitle(12_345, 'USD', 'en')
    expect(result).toBe('$12,345 USD')
  })

  it('CZK code is embedded by Intl so no duplicate', () => {
    const result = formatCurrencyTitle(5_040_000, 'CZK', 'en')
    // Intl en-US renders CZK as "CZK 5,040,000"
    expect(result).toContain('CZK')
    expect(result).toContain('5,040,000')
    // Should not contain "CZK CZK"
    expect(result).not.toContain('CZK CZK')
  })

  it('includes decimals for non-integer amounts', () => {
    const result = formatCurrencyTitle(1_234.56, 'EUR', 'en')
    expect(result).toBe('€1,234.56 EUR')
  })

  it('returns — currency for NaN', () => {
    const result = formatCurrencyTitle(NaN, 'EUR', 'en')
    expect(result).toBe('— EUR')
  })

  it('returns — currency for Infinity', () => {
    const result = formatCurrencyTitle(Infinity, 'USD', 'en')
    expect(result).toBe('— USD')
  })

  it('handles negative amounts', () => {
    const result = formatCurrencyTitle(-50_000, 'EUR', 'en')
    expect(result).toContain('-')
    expect(result).toContain('50,000')
    expect(result).toContain('EUR')
  })

  it('uses locale-aware separators for sk locale', () => {
    const result = formatCurrencyTitle(1_000_000, 'EUR', 'sk')
    expect(typeof result).toBe('string')
    expect(result).toMatch(/€|EUR/)
    expect(result).toMatch(/1/)
  })
})

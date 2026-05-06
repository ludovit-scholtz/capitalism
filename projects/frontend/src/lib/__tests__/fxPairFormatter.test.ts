import { describe, it, expect } from 'vitest'
import {
  currencyStrength,
  isStrongerThan,
  formatPairLabel,
  pairBase,
  pairQuote,
  buildEurPairList,
  rateForPair,
} from '../fxPairFormatter'

describe('fxPairFormatter', () => {
  describe('currencyStrength', () => {
    it('returns 0 for EUR (strongest)', () => {
      expect(currencyStrength('EUR')).toBe(0)
    })

    it('returns 1 for USD (second strongest)', () => {
      expect(currencyStrength('USD')).toBe(1)
    })

    it('CZK is weaker than EUR', () => {
      expect(currencyStrength('CZK')).toBeGreaterThan(currencyStrength('EUR'))
    })

    it('returns 999 for unknown currency', () => {
      expect(currencyStrength('XYZ')).toBe(999)
    })

    it('is case-insensitive', () => {
      expect(currencyStrength('eur')).toBe(currencyStrength('EUR'))
      expect(currencyStrength('czk')).toBe(currencyStrength('CZK'))
    })
  })

  describe('isStrongerThan', () => {
    it('EUR is stronger than CZK', () => {
      expect(isStrongerThan('EUR', 'CZK')).toBe(true)
    })

    it('CZK is not stronger than EUR', () => {
      expect(isStrongerThan('CZK', 'EUR')).toBe(false)
    })

    it('EUR is stronger than USD', () => {
      expect(isStrongerThan('EUR', 'USD')).toBe(true)
    })

    it('USD is stronger than PLN', () => {
      expect(isStrongerThan('USD', 'PLN')).toBe(true)
    })

    it('EUR is not stronger than itself', () => {
      expect(isStrongerThan('EUR', 'EUR')).toBe(false)
    })
  })

  describe('formatPairLabel', () => {
    it('places stronger currency first: EUR/CZK not CZK/EUR', () => {
      expect(formatPairLabel('CZK', 'EUR')).toBe('EUR/CZK')
    })

    it('places EUR first in EUR/USD pair', () => {
      expect(formatPairLabel('USD', 'EUR')).toBe('EUR/USD')
    })

    it('preserves already-correct ordering EUR/CZK', () => {
      expect(formatPairLabel('EUR', 'CZK')).toBe('EUR/CZK')
    })

    it('uppercases currency codes', () => {
      expect(formatPairLabel('eur', 'czk')).toBe('EUR/CZK')
    })

    it('handles unknown currencies by putting them on the right', () => {
      const label = formatPairLabel('EUR', 'XYZ')
      expect(label).toBe('EUR/XYZ')
    })

    it('places two unknown currencies in alphabetical order by default strength=999', () => {
      // Both have strength 999 — neither is stronger than the other, so the result
      // depends on the comparison: isStrongerThan('AAA', 'ZZZ') = false so base=ZZZ, quote=AAA
      const label = formatPairLabel('AAA', 'ZZZ')
      expect(label).toMatch(/^[A-Z]{3}\/[A-Z]{3}$/)
    })
  })

  describe('pairBase', () => {
    it('returns EUR when EUR is paired with CZK', () => {
      expect(pairBase('CZK', 'EUR')).toBe('EUR')
    })

    it('returns EUR when paired with USD', () => {
      expect(pairBase('USD', 'EUR')).toBe('EUR')
    })
  })

  describe('pairQuote', () => {
    it('returns CZK for EUR/CZK pair', () => {
      expect(pairQuote('EUR', 'CZK')).toBe('CZK')
    })

    it('returns USD for EUR/USD pair', () => {
      expect(pairQuote('EUR', 'USD')).toBe('USD')
    })
  })

  describe('buildEurPairList', () => {
    it('excludes EUR itself', () => {
      const pairs = buildEurPairList(['EUR', 'CZK', 'USD'])
      expect(pairs).not.toContain('EUR/EUR')
    })

    it('returns sorted pair labels', () => {
      const pairs = buildEurPairList(['USD', 'CZK', 'PLN'])
      const sorted = [...pairs].sort()
      expect(pairs).toEqual(sorted)
    })

    it('returns empty array for empty input', () => {
      expect(buildEurPairList([])).toEqual([])
    })

    it('returns empty array when only EUR is provided', () => {
      expect(buildEurPairList(['EUR'])).toEqual([])
    })

    it('creates correct EUR/X labels for all provided currencies', () => {
      const pairs = buildEurPairList(['CZK', 'USD'])
      expect(pairs).toContain('EUR/CZK')
      expect(pairs).toContain('EUR/USD')
    })
  })

  describe('rateForPair', () => {
    it('returns raw rate unchanged for EUR/CZK pair (EUR is base)', () => {
      expect(rateForPair('EUR/CZK', 25.2)).toBe(25.2)
    })

    it('returns zero when rate is zero to avoid division', () => {
      expect(rateForPair('EUR/CZK', 0)).toBe(0)
    })

    it('inverts rate when base is not EUR', () => {
      // USD/EUR at eurToQuoteRate=1.08 means EUR/USD=1.08 → USD/EUR=1/1.08
      const result = rateForPair('USD/EUR', 1.08)
      expect(result).toBeCloseTo(1 / 1.08, 5)
    })
  })
})

import { describe, it, expect } from 'vitest'
import {
  currencyStrength,
  isStrongerThan,
  formatPairLabel,
  pairWeaker,
  pairStronger,
  buildEurPairList,
  rateForPair,
  extractQuoteCurrencyFromEurPair,
} from '../fxPairFormatter'

describe('fxPairFormatter', () => {
  describe('currencyStrength', () => {
    it('returns 0 for EUR (strongest)', () => {
      expect(currencyStrength('EUR')).toBe(0)
    })

    it('returns 1 for USD (second strongest)', () => {
      expect(currencyStrength('USD')).toBe(1)
    })

    it('CZK is weaker than INR', () => {
      expect(currencyStrength('CZK')).toBeGreaterThan(currencyStrength('INR'))
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
    it('EUR is stronger than USD', () => {
      expect(isStrongerThan('EUR', 'USD')).toBe(true)
    })

    it('CZK is not stronger than EUR', () => {
      expect(isStrongerThan('CZK', 'EUR')).toBe(false)
    })

    it('EUR is stronger than CNY', () => {
      expect(isStrongerThan('EUR', 'CNY')).toBe(true)
    })

    it('EUR is not stronger than itself', () => {
      expect(isStrongerThan('EUR', 'EUR')).toBe(false)
    })
  })

  describe('formatPairLabel', () => {
    it('places stronger currency first as roadmap format: EURCZK', () => {
      expect(formatPairLabel('CZK', 'EUR')).toBe('EURCZK')
    })

    it('uses EURUSD in EUR/USD pair because EUR is stronger than USD', () => {
      expect(formatPairLabel('USD', 'EUR')).toBe('EURUSD')
    })

    it('preserves already-correct ordering EURCZK', () => {
      expect(formatPairLabel('CZK', 'EUR')).toBe('EURCZK')
    })

    it('uppercases currency codes', () => {
      expect(formatPairLabel('eur', 'czk')).toBe('EURCZK')
    })

    it('handles unknown currencies by treating them as weaker than known majors (known first)', () => {
      const label = formatPairLabel('EUR', 'XYZ')
      expect(label).toBe('EURXYZ')
    })

    it('returns a 6-character pair code for two unknown currencies', () => {
      const label = formatPairLabel('AAA', 'ZZZ')
      expect(label).toMatch(/^[A-Z]{6}$/)
    })
  })

  describe('pairWeaker', () => {
    it('returns weaker currency for EUR/CZK pair', () => {
      expect(pairWeaker('CZK', 'EUR')).toBe('CZK')
    })

    it('returns USD when paired with EUR (USD is weaker)', () => {
      expect(pairWeaker('USD', 'EUR')).toBe('USD')
    })
  })

  describe('pairStronger', () => {
    it('returns stronger currency for CZK/EUR pair', () => {
      expect(pairStronger('EUR', 'CZK')).toBe('EUR')
    })

    it('returns EUR for EUR/USD pair (EUR stronger)', () => {
      expect(pairStronger('EUR', 'USD')).toBe('EUR')
    })
  })

  describe('buildEurPairList', () => {
    it('excludes EUR itself', () => {
      const pairs = buildEurPairList(['EUR', 'CZK', 'USD'])
      expect(pairs).not.toContain('EUREUR')
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
      expect(pairs).toContain('EURCZK')
      expect(pairs).toContain('EURUSD')
    })
  })

  describe('rateForPair', () => {
    it('returns raw rate unchanged for EURUSD pair (EUR base)', () => {
      expect(rateForPair('EURUSD', 1.08)).toBe(1.08)
    })

    it('returns zero when rate is zero to avoid division', () => {
      expect(rateForPair('EURUSD', 0)).toBe(0)
    })

    it('inverts rate when pair ends with EUR', () => {
      const result = rateForPair('USDEUR', 1.08)
      expect(result).toBeCloseTo(1 / 1.08, 5)
    })

    it('returns raw rate unchanged for EURCZK pair (EUR base)', () => {
      const result = rateForPair('EURCZK', 25.2)
      expect(result).toBe(25.2)
    })

    it('inverts rate when pair starts with non-EUR base and ends with EUR', () => {
      const result = rateForPair('CZKEUR', 25.2)
      expect(result).toBeCloseTo(1 / 25.2, 5)
    })
  })

  describe('extractQuoteCurrencyFromEurPair', () => {
    it('returns USD for EURUSD', () => {
      expect(extractQuoteCurrencyFromEurPair('EURUSD')).toBe('USD')
    })

    it('returns CZK for EURCZK', () => {
      expect(extractQuoteCurrencyFromEurPair('EURCZK')).toBe('CZK')
    })

    it('returns null for invalid pair code', () => {
      expect(extractQuoteCurrencyFromEurPair('USDJPY')).toBeNull()
      expect(extractQuoteCurrencyFromEurPair('EURUS')).toBeNull()
    })
  })
})

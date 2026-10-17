import { describe, expect, it } from 'vitest'
import {
  CITY_LABOR_MARKETS_QUERY,
  STRIKE_PAY_RATIO_THRESHOLD,
  computePayRatio,
  formatWagePressure,
  payPosition,
  strikeRisk,
  unemploymentPercent,
} from '../laborMarket'

describe('computePayRatio', () => {
  it('compares with the city average but never with an average below the baseline wage', () => {
    expect(computePayRatio(0.8, 1.0)).toBe(0.8)
    expect(computePayRatio(1.2, 1.1)).toBeCloseTo(1.0909, 4)
    expect(computePayRatio(0.5, 0.5)).toBe(0.5)
    expect(computePayRatio(1, 0)).toBe(1)
  })
})

describe('strikeRisk', () => {
  it('mirrors the backend threshold of 0.85', () => {
    expect(STRIKE_PAY_RATIO_THRESHOLD).toBe(0.85)
    expect(strikeRisk(1)).toBe('NONE')
    expect(strikeRisk(0.85)).toBe('NONE')
    expect(strikeRisk(0.84)).toBe('ELEVATED')
    expect(strikeRisk(0.7)).toBe('ELEVATED')
    expect(strikeRisk(0.69)).toBe('HIGH')
    expect(strikeRisk(0.5)).toBe('HIGH')
  })
})

describe('payPosition', () => {
  it('classifies pay relative to the city', () => {
    expect(payPosition(0.9)).toBe('BELOW')
    expect(payPosition(1)).toBe('FAIR')
    expect(payPosition(1.04)).toBe('FAIR')
    expect(payPosition(1.2)).toBe('ABOVE')
  })
})

describe('formatting helpers', () => {
  it('turns the unemployment rate into a rounded percent', () => {
    expect(unemploymentPercent(0.9234)).toBe(92)
    expect(unemploymentPercent(0)).toBe(0)
    expect(unemploymentPercent(1)).toBe(100)
  })

  it('formats wage pressure as a signed percent change', () => {
    expect(formatWagePressure(1)).toBe('+0%')
    expect(formatWagePressure(1.5)).toBe('+50%')
    expect(formatWagePressure(0.9)).toBe('-10%')
  })
})

describe('CITY_LABOR_MARKETS_QUERY', () => {
  it('requests every field the section renders with balanced braces', () => {
    for (const field of ['cityLaborMarkets', 'unemploymentRate', 'wageIndex', 'averageSalaryMultiplier', 'payRatio', 'strikeProne', 'yourSalaryMultiplier', 'affectedBuildings', 'onStrike', 'strikeUntilTick']) {
      expect(CITY_LABOR_MARKETS_QUERY).toContain(field)
    }
    expect(CITY_LABOR_MARKETS_QUERY.split('{').length).toBe(CITY_LABOR_MARKETS_QUERY.split('}').length)
  })
})

import { describe, expect, it } from 'vitest'
import { computeMiningEfficiencyFactor } from '@/lib/miningScarcity'

describe('computeMiningEfficiencyFactor', () => {
  it('returns expected factors at scarcity thresholds', () => {
    expect(computeMiningEfficiencyFactor(100, 100)).toBe(1)
    expect(computeMiningEfficiencyFactor(70, 100)).toBe(1)
    expect(computeMiningEfficiencyFactor(20, 100)).toBeCloseTo(0.6)
    expect(computeMiningEfficiencyFactor(5, 100)).toBeCloseTo(0.375)
    expect(computeMiningEfficiencyFactor(0, 100)).toBeCloseTo(0.3)
  })

  it('returns full efficiency for missing or invalid values', () => {
    expect(computeMiningEfficiencyFactor(null, 100)).toBe(1)
    expect(computeMiningEfficiencyFactor(100, null)).toBe(1)
    expect(computeMiningEfficiencyFactor(100, 0)).toBe(1)
  })
})

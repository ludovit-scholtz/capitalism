import { describe, expect, it } from 'vitest'

import { computeCityUnlockProgress, formatEstimatedTicksLabel } from '@/lib/cityExpansion'

describe('computeCityUnlockProgress', () => {
  it('returns 100 for unlocked cities', () => {
    expect(
      computeCityUnlockProgress({
        isUnlocked: true,
        requiredNetWorth: 500000,
        currentNetWorth: 125000,
        progressPercent: 25,
      }),
    ).toBe(100)
  })

  it('uses provided progress percent for locked cities', () => {
    expect(
      computeCityUnlockProgress({
        isUnlocked: false,
        requiredNetWorth: 500000,
        currentNetWorth: 100000,
        progressPercent: 20,
      }),
    ).toBe(20)
  })

  it('falls back to computed ratio when progress percent is missing', () => {
    expect(
      computeCityUnlockProgress({
        isUnlocked: false,
        requiredNetWorth: 400000,
        currentNetWorth: 100000,
        progressPercent: 0,
      }),
    ).toBe(25)
  })
})

describe('formatEstimatedTicksLabel', () => {
  it('returns an em dash when the estimate is unavailable', () => {
    expect(formatEstimatedTicksLabel(null)).toBe('—')
    expect(formatEstimatedTicksLabel(0)).toBe('—')
  })

  it('formats whole-number tick estimates', () => {
    expect(formatEstimatedTicksLabel(1234)).toBe('1,234')
  })

  it('supports explicit app locales instead of host defaults', () => {
    expect(formatEstimatedTicksLabel(1234, 'sk')).toBe(new Intl.NumberFormat('sk').format(1234))
  })
})

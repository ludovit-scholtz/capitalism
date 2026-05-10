import { describe, expect, it } from 'vitest'
import {
  buildDepletionTrendlinePath,
  buildSparklinePath,
  summarizeExtractionTrend,
  type MineExtractionDailyPoint,
} from '../mineExtractionIntelligence'

function makeData(values: number[]): MineExtractionDailyPoint[] {
  return values.map((extractedAmount, index) => ({
    dayIndex: index,
    extractedAmount,
    efficiencyPercent: 1,
    reserveRemaining: 1000 - index * 10,
  }))
}

describe('mineExtractionIntelligence', () => {
  it('builds sparkline path for stable data', () => {
    const path = buildSparklinePath(makeData([10, 10, 10]), 280, 80, 0)
    expect(path).toContain('M ')
    expect(path).toContain('L ')
  })

  it('builds sparkline path for declining data and trendline projection', () => {
    const data = makeData([20, 18, 14, 10, 6])
    const sparkline = buildSparklinePath(data, 280, 80, 12)
    const trendline = buildDepletionTrendlinePath(data, 280, 80, 12)

    expect(sparkline).toContain('M ')
    expect(trendline).toContain('L ')
  })

  it('returns no trendline when projected days are missing', () => {
    const trendline = buildDepletionTrendlinePath(makeData([5, 4, 3]), 280, 80, 0)
    expect(trendline).toBe('')
  })

  it('summarizes near-zero depletion scenarios as declining', () => {
    const trend = summarizeExtractionTrend(makeData([12, 10, 8]), 5)
    expect(trend).toBe('declining')
  })

  it('returns empty trend summary when no history exists', () => {
    expect(summarizeExtractionTrend([], null)).toBe('empty')
  })
})

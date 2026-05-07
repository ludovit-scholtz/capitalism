import { describe, it, expect } from 'vitest'
import type { FxRateSnapshot } from '@/types'
import {
  snapshotsToPoints,
  computeBounds,
  buildChartPaths,
  buildYAxisLabels,
  nearestPointToX,
  pointToX,
  DEFAULT_DIMENSIONS,
} from '../fxRateChart'

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeSnapshot(
  tick: number,
  midRate: number,
  spread = 0.005,
): FxRateSnapshot {
  return {
    baseCurrencyCode: 'EUR',
    quoteCurrencyCode: 'CZK',
    midRate,
    buyRate: midRate * (1 + spread),
    sellRate: midRate * (1 - spread),
    gameTick: tick,
    capturedAtUtc: new Date().toISOString(),
  }
}

const SAMPLE_SNAPSHOTS: FxRateSnapshot[] = [
  makeSnapshot(10, 25.0),
  makeSnapshot(20, 25.5),
  makeSnapshot(30, 26.0),
]

// ── snapshotsToPoints ─────────────────────────────────────────────────────────

describe('snapshotsToPoints', () => {
  it('returns empty array for empty input', () => {
    expect(snapshotsToPoints([])).toEqual([])
  })

  it('maps gameTick to tick field', () => {
    const points = snapshotsToPoints(SAMPLE_SNAPSHOTS)
    expect(points[0]!.tick).toBe(10)
    expect(points[1]!.tick).toBe(20)
    expect(points[2]!.tick).toBe(30)
  })

  it('maps midRate, buyRate, sellRate correctly', () => {
    const points = snapshotsToPoints(SAMPLE_SNAPSHOTS)
    expect(points[0]!.mid).toBe(25.0)
    expect(points[0]!.buy).toBeCloseTo(25.0 * 1.005, 5)
    expect(points[0]!.sell).toBeCloseTo(25.0 * 0.995, 5)
  })

  it('preserves order of input snapshots', () => {
    const reversed = [...SAMPLE_SNAPSHOTS].reverse()
    const points = snapshotsToPoints(reversed)
    expect(points[0]!.tick).toBe(30)
    expect(points[2]!.tick).toBe(10)
  })
})

// ── computeBounds ─────────────────────────────────────────────────────────────

describe('computeBounds', () => {
  it('returns null for empty points', () => {
    expect(computeBounds([])).toBeNull()
  })

  it('computes minTick and maxTick correctly', () => {
    const points = snapshotsToPoints(SAMPLE_SNAPSHOTS)
    const bounds = computeBounds(points)!
    expect(bounds.minTick).toBe(10)
    expect(bounds.maxTick).toBe(30)
    expect(bounds.tickRange).toBe(20)
  })

  it('includes buy and sell in rate bounds', () => {
    const points = snapshotsToPoints(SAMPLE_SNAPSHOTS)
    const bounds = computeBounds(points)!
    // buy of tick 30 (26.0 * 1.005 = 26.13) should be maxRate
    expect(bounds.maxRate).toBeGreaterThanOrEqual(26.0)
    // sell of tick 10 (25.0 * 0.995 = 24.875) should be minRate
    expect(bounds.minRate).toBeLessThanOrEqual(25.0)
  })

  it('ensures rateRange is at least 0.0001 for flat data', () => {
    const flatSnapshots = [makeSnapshot(1, 25.0, 0), makeSnapshot(2, 25.0, 0)]
    const points = snapshotsToPoints(flatSnapshots)
    const bounds = computeBounds(points)!
    expect(bounds.rateRange).toBeGreaterThanOrEqual(0.0001)
  })

  it('ensures tickRange is at least 1 for single-tick data', () => {
    const single = [makeSnapshot(5, 25.0)]
    const points = snapshotsToPoints(single)
    const bounds = computeBounds(points)!
    expect(bounds.tickRange).toBeGreaterThanOrEqual(1)
  })

  it('minTick equals maxTick for single point — tickRange forced to 1', () => {
    const points = snapshotsToPoints([makeSnapshot(42, 1.08)])
    const bounds = computeBounds(points)!
    expect(bounds.minTick).toBe(42)
    expect(bounds.maxTick).toBe(42)
    expect(bounds.tickRange).toBe(1)
  })
})

// ── buildChartPaths ───────────────────────────────────────────────────────────

describe('buildChartPaths', () => {
  it('returns non-empty strings for all three series', () => {
    const points = snapshotsToPoints(SAMPLE_SNAPSHOTS)
    const bounds = computeBounds(points)!
    const paths = buildChartPaths(points, bounds, DEFAULT_DIMENSIONS)
    expect(paths.midPoints.length).toBeGreaterThan(0)
    expect(paths.buyPoints.length).toBeGreaterThan(0)
    expect(paths.sellPoints.length).toBeGreaterThan(0)
  })

  it('outputs correct number of coordinate pairs (one per point)', () => {
    const points = snapshotsToPoints(SAMPLE_SNAPSHOTS)
    const bounds = computeBounds(points)!
    const paths = buildChartPaths(points, bounds, DEFAULT_DIMENSIONS)
    // Each point produces "x,y" — count spaces+1 = number of points
    const midPairs = paths.midPoints.trim().split(' ')
    expect(midPairs).toHaveLength(SAMPLE_SNAPSHOTS.length)
  })

  it('uses DEFAULT_DIMENSIONS when not provided', () => {
    const points = snapshotsToPoints(SAMPLE_SNAPSHOTS)
    const bounds = computeBounds(points)!
    const withDefault = buildChartPaths(points, bounds)
    const explicit = buildChartPaths(points, bounds, DEFAULT_DIMENSIONS)
    expect(withDefault.midPoints).toBe(explicit.midPoints)
  })

  it('first point x is at paddingLeft (tick = minTick)', () => {
    const points = snapshotsToPoints(SAMPLE_SNAPSHOTS)
    const bounds = computeBounds(points)!
    const paths = buildChartPaths(points, bounds, DEFAULT_DIMENSIONS)
    const firstPair = paths.midPoints.split(' ')[0]!
    const x = parseFloat(firstPair.split(',')[0]!)
    expect(x).toBeCloseTo(DEFAULT_DIMENSIONS.paddingLeft, 0)
  })

  it('last point x is at width - paddingRight (tick = maxTick)', () => {
    const points = snapshotsToPoints(SAMPLE_SNAPSHOTS)
    const bounds = computeBounds(points)!
    const paths = buildChartPaths(points, bounds, DEFAULT_DIMENSIONS)
    const pairs = paths.midPoints.trim().split(' ')
    const lastPair = pairs[pairs.length - 1]!
    const x = parseFloat(lastPair.split(',')[0]!)
    expect(x).toBeCloseTo(DEFAULT_DIMENSIONS.width - DEFAULT_DIMENSIONS.paddingRight, 0)
  })
})

// ── buildYAxisLabels ──────────────────────────────────────────────────────────

describe('buildYAxisLabels', () => {
  it('returns 5 labels (0..4 steps) for non-flat data', () => {
    const points = snapshotsToPoints(SAMPLE_SNAPSHOTS)
    const bounds = computeBounds(points)!
    const labels = buildYAxisLabels(bounds, DEFAULT_DIMENSIONS)
    expect(labels).toHaveLength(5)
  })

  it('each label has a y coordinate and a formatted string', () => {
    const points = snapshotsToPoints(SAMPLE_SNAPSHOTS)
    const bounds = computeBounds(points)!
    const labels = buildYAxisLabels(bounds, DEFAULT_DIMENSIONS)
    for (const label of labels) {
      expect(typeof label.y).toBe('number')
      expect(typeof label.label).toBe('string')
      expect(label.label.length).toBeGreaterThan(0)
    }
  })

  it('respects custom precision parameter', () => {
    const points = snapshotsToPoints(SAMPLE_SNAPSHOTS)
    const bounds = computeBounds(points)!
    const labels2 = buildYAxisLabels(bounds, DEFAULT_DIMENSIONS, 2)
    const labels6 = buildYAxisLabels(bounds, DEFAULT_DIMENSIONS, 6)
    // 2 decimals: "25.00", 6 decimals: "25.000000"
    expect(labels2[0]!.label.split('.')[1]!.length).toBe(2)
    expect(labels6[0]!.label.split('.')[1]!.length).toBe(6)
  })

  it('y values span the chart canvas (top to bottom)', () => {
    const points = snapshotsToPoints(SAMPLE_SNAPSHOTS)
    const bounds = computeBounds(points)!
    const labels = buildYAxisLabels(bounds, DEFAULT_DIMENSIONS)
    const ys = labels.map((l) => l.y)
    const minY = Math.min(...ys)
    const maxY = Math.max(...ys)
    // Must be inside the canvas area
    expect(minY).toBeGreaterThanOrEqual(DEFAULT_DIMENSIONS.paddingTop)
    expect(maxY).toBeLessThanOrEqual(DEFAULT_DIMENSIONS.height - DEFAULT_DIMENSIONS.paddingBottom)
  })
})

// ── nearestPointToX ───────────────────────────────────────────────────────────

describe('nearestPointToX', () => {
  it('returns null for empty points', () => {
    const bounds = computeBounds([])
    expect(nearestPointToX(100, [], bounds ?? { minTick: 0, maxTick: 1, minRate: 0, maxRate: 1, tickRange: 1, rateRange: 1 }, DEFAULT_DIMENSIONS)).toBeNull()
  })

  it('returns the single point for single-element array', () => {
    const points = snapshotsToPoints([makeSnapshot(10, 25.0)])
    const bounds = computeBounds(points)!
    const nearest = nearestPointToX(0, points, bounds, DEFAULT_DIMENSIONS)
    expect(nearest?.tick).toBe(10)
  })

  it('returns first point when x is at paddingLeft', () => {
    const points = snapshotsToPoints(SAMPLE_SNAPSHOTS)
    const bounds = computeBounds(points)!
    const nearest = nearestPointToX(DEFAULT_DIMENSIONS.paddingLeft, points, bounds, DEFAULT_DIMENSIONS)
    expect(nearest?.tick).toBe(10)
  })

  it('returns last point when x is at right edge', () => {
    const points = snapshotsToPoints(SAMPLE_SNAPSHOTS)
    const bounds = computeBounds(points)!
    const rightEdge = DEFAULT_DIMENSIONS.width - DEFAULT_DIMENSIONS.paddingRight
    const nearest = nearestPointToX(rightEdge, points, bounds, DEFAULT_DIMENSIONS)
    expect(nearest?.tick).toBe(30)
  })

  it('returns middle point when x is at center', () => {
    const points = snapshotsToPoints(SAMPLE_SNAPSHOTS)
    const bounds = computeBounds(points)!
    // Center x corresponds to tick 20 (middle of 10..30)
    const centerX = DEFAULT_DIMENSIONS.paddingLeft + (DEFAULT_DIMENSIONS.width - DEFAULT_DIMENSIONS.paddingLeft - DEFAULT_DIMENSIONS.paddingRight) / 2
    const nearest = nearestPointToX(centerX, points, bounds, DEFAULT_DIMENSIONS)
    expect(nearest?.tick).toBe(20)
  })
})

// ── pointToX ─────────────────────────────────────────────────────────────────

describe('pointToX', () => {
  it('maps first point to paddingLeft', () => {
    const points = snapshotsToPoints(SAMPLE_SNAPSHOTS)
    const bounds = computeBounds(points)!
    const x = pointToX(points[0]!, bounds, DEFAULT_DIMENSIONS)
    expect(x).toBeCloseTo(DEFAULT_DIMENSIONS.paddingLeft, 0)
  })

  it('maps last point to width - paddingRight', () => {
    const points = snapshotsToPoints(SAMPLE_SNAPSHOTS)
    const bounds = computeBounds(points)!
    const x = pointToX(points[points.length - 1]!, bounds, DEFAULT_DIMENSIONS)
    expect(x).toBeCloseTo(DEFAULT_DIMENSIONS.width - DEFAULT_DIMENSIONS.paddingRight, 0)
  })

  it('uses DEFAULT_DIMENSIONS when not provided', () => {
    const points = snapshotsToPoints(SAMPLE_SNAPSHOTS)
    const bounds = computeBounds(points)!
    const xDefault = pointToX(points[0]!, bounds)
    const xExplicit = pointToX(points[0]!, bounds, DEFAULT_DIMENSIONS)
    expect(xDefault).toBe(xExplicit)
  })
})

// ── DEFAULT_DIMENSIONS ────────────────────────────────────────────────────────

describe('DEFAULT_DIMENSIONS', () => {
  it('has expected shape and positive values', () => {
    expect(DEFAULT_DIMENSIONS.width).toBeGreaterThan(0)
    expect(DEFAULT_DIMENSIONS.height).toBeGreaterThan(0)
    expect(DEFAULT_DIMENSIONS.paddingTop).toBeGreaterThan(0)
    expect(DEFAULT_DIMENSIONS.paddingBottom).toBeGreaterThan(0)
    expect(DEFAULT_DIMENSIONS.paddingLeft).toBeGreaterThan(0)
    expect(DEFAULT_DIMENSIONS.paddingRight).toBeGreaterThan(0)
  })

  it('canvas area (width - paddingLeft - paddingRight) is positive', () => {
    const canvasW = DEFAULT_DIMENSIONS.width - DEFAULT_DIMENSIONS.paddingLeft - DEFAULT_DIMENSIONS.paddingRight
    expect(canvasW).toBeGreaterThan(0)
  })

  it('canvas height (height - paddingTop - paddingBottom) is positive', () => {
    const canvasH = DEFAULT_DIMENSIONS.height - DEFAULT_DIMENSIONS.paddingTop - DEFAULT_DIMENSIONS.paddingBottom
    expect(canvasH).toBeGreaterThan(0)
  })
})

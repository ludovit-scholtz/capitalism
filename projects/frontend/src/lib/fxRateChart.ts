/**
 * SVG chart model builder for FX rate history (buy/mid/sell series).
 *
 * Pure data-to-SVG-path utilities — no Vue or DOM dependency.
 * Consumed by FxRateChart.vue to render a 3-line responsive chart.
 */
import type { FxRateSnapshot } from '@/types'

export interface ChartPoint {
  tick: number
  mid: number
  buy: number
  sell: number
}

export interface ChartBounds {
  minTick: number
  maxTick: number
  minRate: number
  maxRate: number
  tickRange: number
  rateRange: number
}

export interface ChartDimensions {
  width: number
  height: number
  paddingTop: number
  paddingBottom: number
  paddingLeft: number
  paddingRight: number
}

export const DEFAULT_DIMENSIONS: ChartDimensions = {
  width: 600,
  height: 200,
  paddingTop: 12,
  paddingBottom: 24,
  paddingLeft: 50,
  paddingRight: 16,
}

/** Convert FxRateSnapshot[] to normalized ChartPoint[]. */
export function snapshotsToPoints(snapshots: FxRateSnapshot[]): ChartPoint[] {
  return snapshots.map((s) => ({
    tick: s.gameTick,
    mid: s.midRate,
    buy: s.buyRate,
    sell: s.sellRate,
  }))
}

/** Compute the value bounds for a set of chart points. */
export function computeBounds(points: ChartPoint[]): ChartBounds | null {
  if (points.length === 0) return null

  const ticks = points.map((p) => p.tick)
  const rates = points.flatMap((p) => [p.mid, p.buy, p.sell])
  const minTick = Math.min(...ticks)
  const maxTick = Math.max(...ticks)
  const minRate = Math.min(...rates)
  const maxRate = Math.max(...rates)
  const tickRange = Math.max(maxTick - minTick, 1)
  const rateRange = Math.max(maxRate - minRate, 0.0001)

  return { minTick, maxTick, minRate, maxRate, tickRange, rateRange }
}

/** Map a tick value to an x-coordinate in the chart canvas area. */
function toX(tick: number, bounds: ChartBounds, dims: ChartDimensions): number {
  const canvasW = dims.width - dims.paddingLeft - dims.paddingRight
  return dims.paddingLeft + ((tick - bounds.minTick) / bounds.tickRange) * canvasW
}

/** Map a rate value to a y-coordinate in the chart canvas area (inverted: top = max). */
function toY(rate: number, bounds: ChartBounds, dims: ChartDimensions): number {
  const canvasH = dims.height - dims.paddingTop - dims.paddingBottom
  // Add 5% padding to the rate range so lines don't touch the top/bottom edge
  const paddedMin = bounds.minRate - bounds.rateRange * 0.05
  const paddedRange = bounds.rateRange * 1.1
  return dims.paddingTop + canvasH - ((rate - paddedMin) / paddedRange) * canvasH
}

/** Build an SVG polyline points string for the given series. */
function buildPolylinePoints(
  points: ChartPoint[],
  series: 'mid' | 'buy' | 'sell',
  bounds: ChartBounds,
  dims: ChartDimensions
): string {
  return points
    .map((p) => `${toX(p.tick, bounds, dims).toFixed(1)},${toY(p[series], bounds, dims).toFixed(1)}`)
    .join(' ')
}

export interface ChartPaths {
  midPoints: string
  buyPoints: string
  sellPoints: string
}

/** Build all three polyline points strings for the chart. */
export function buildChartPaths(
  points: ChartPoint[],
  bounds: ChartBounds,
  dims: ChartDimensions = DEFAULT_DIMENSIONS
): ChartPaths {
  return {
    midPoints: buildPolylinePoints(points, 'mid', bounds, dims),
    buyPoints: buildPolylinePoints(points, 'buy', bounds, dims),
    sellPoints: buildPolylinePoints(points, 'sell', bounds, dims),
  }
}

/** Build Y-axis tick labels. Returns up to 5 evenly spaced rate values with display strings. */
export function buildYAxisLabels(
  bounds: ChartBounds,
  dims: ChartDimensions = DEFAULT_DIMENSIONS,
  precision = 4
): Array<{ y: number; label: string }> {
  const steps = 4
  const labels: Array<{ y: number; label: string }> = []
  for (let i = 0; i <= steps; i++) {
    const rate = bounds.minRate + (bounds.rateRange * i) / steps
    labels.push({
      y: toY(rate, bounds, dims),
      label: rate.toFixed(precision),
    })
  }
  return labels
}

/** Find the nearest chart point to a given x-coordinate. */
export function nearestPointToX(
  x: number,
  points: ChartPoint[],
  bounds: ChartBounds,
  dims: ChartDimensions = DEFAULT_DIMENSIONS
): ChartPoint | null {
  if (points.length === 0) return null
  const first = points[0]
  if (!first) return null
  let best = first
  let bestDist = Math.abs(toX(best.tick, bounds, dims) - x)
  for (const p of points) {
    const dist = Math.abs(toX(p.tick, bounds, dims) - x)
    if (dist < bestDist) {
      bestDist = dist
      best = p
    }
  }
  return best
}

/** Return the x coordinate for a given chart point. */
export function pointToX(
  point: ChartPoint,
  bounds: ChartBounds,
  dims: ChartDimensions = DEFAULT_DIMENSIONS
): number {
  return toX(point.tick, bounds, dims)
}

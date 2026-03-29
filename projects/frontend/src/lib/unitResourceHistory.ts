import type { BuildingUnitResourceHistoryPoint } from '@/types'

export type UnitResourceHistoryMetricKey =
  | 'inflowQuantity'
  | 'outflowQuantity'
  | 'consumedQuantity'
  | 'producedQuantity'

export type UnitResourceHistoryItemOption = {
  key: string
  label: string
}

export type UnitResourceHistorySeries = {
  key: UnitResourceHistoryMetricKey
  total: number
  lastValue: number
  polylinePoints: string
  points: Array<{ x: number; y: number; tick: number; value: number }>
}

export type UnitResourceHistoryChartModel = {
  maxValue: number
  hasData: boolean
  tickRange: { start: number | null; end: number | null }
  series: UnitResourceHistorySeries[]
}

const metricKeys: UnitResourceHistoryMetricKey[] = [
  'inflowQuantity',
  'outflowQuantity',
  'consumedQuantity',
  'producedQuantity',
]

function normalizeValue(value: number | null | undefined): number {
  return Number.isFinite(value) ? Math.max(value ?? 0, 0) : 0
}

function roundMetric(value: number): number {
  return Number(value.toFixed(4))
}

function getScaleCeiling(value: number): number {
  if (value <= 0) {
    return 1
  }

  const magnitude = 10 ** Math.floor(Math.log10(value))
  const normalized = value / magnitude

  if (normalized <= 1) return magnitude
  if (normalized <= 2) return 2 * magnitude
  if (normalized <= 5) return 5 * magnitude
  return 10 * magnitude
}

export function getUnitResourceHistoryItemKey(
  resourceTypeId: string | null | undefined,
  productTypeId: string | null | undefined,
): string | null {
  if (resourceTypeId) {
    return `resource:${resourceTypeId}`
  }

  if (productTypeId) {
    return `product:${productTypeId}`
  }

  return null
}

export function buildUnitResourceHistoryChartModel(
  entries: BuildingUnitResourceHistoryPoint[],
  width: number,
  height: number,
): UnitResourceHistoryChartModel {
  const sortedEntries = [...entries].sort((left, right) => left.tick - right.tick)
  const maxObservedValue = sortedEntries.reduce((currentMax, entry) => {
    return Math.max(
      currentMax,
      normalizeValue(entry.inflowQuantity),
      normalizeValue(entry.outflowQuantity),
      normalizeValue(entry.consumedQuantity),
      normalizeValue(entry.producedQuantity),
    )
  }, 0)
  const maxValue = getScaleCeiling(maxObservedValue)
  const stepX = sortedEntries.length > 1 ? width / (sortedEntries.length - 1) : 0

  const series = metricKeys.map((metricKey) => {
    const points = sortedEntries.map((entry, index) => {
      const value = normalizeValue(entry[metricKey])
      const x = sortedEntries.length === 1 ? width / 2 : stepX * index
      const y = maxValue > 0 ? height - (value / maxValue) * height : height

      return {
        x: roundMetric(x),
        y: roundMetric(y),
        tick: entry.tick,
        value: roundMetric(value),
      }
    })

    return {
      key: metricKey,
      total: roundMetric(points.reduce((total, point) => total + point.value, 0)),
      lastValue: points.length > 0 ? points[points.length - 1]!.value : 0,
      polylinePoints: points.map((point) => `${point.x},${point.y}`).join(' '),
      points,
    }
  })

  return {
    maxValue,
    hasData: maxObservedValue > 0,
    tickRange: {
      start: sortedEntries[0]?.tick ?? null,
      end: sortedEntries.length > 0 ? sortedEntries[sortedEntries.length - 1]!.tick : null,
    },
    series,
  }
}

export function getRecentUnitResourceHistoryRows(
  entries: BuildingUnitResourceHistoryPoint[],
  limit = 6,
): BuildingUnitResourceHistoryPoint[] {
  return [...entries]
    .sort((left, right) => right.tick - left.tick)
    .slice(0, limit)
}

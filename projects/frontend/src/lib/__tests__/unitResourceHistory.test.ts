import { describe, expect, it } from 'vitest'
import {
  buildUnitResourceHistoryChartModel,
  getRecentUnitResourceHistoryRows,
  getUnitResourceHistoryItemKey,
} from '../unitResourceHistory'

describe('getUnitResourceHistoryItemKey', () => {
  it('builds a resource key when resourceTypeId is present', () => {
    expect(getUnitResourceHistoryItemKey('wood', null)).toBe('resource:wood')
  })

  it('builds a product key when productTypeId is present', () => {
    expect(getUnitResourceHistoryItemKey(null, 'chair')).toBe('product:chair')
  })

  it('returns null when neither item id is present', () => {
    expect(getUnitResourceHistoryItemKey(null, null)).toBeNull()
  })
})

describe('buildUnitResourceHistoryChartModel', () => {
  it('returns a non-empty chart model with totals and point coordinates', () => {
    const model = buildUnitResourceHistoryChartModel(
      [
        {
          buildingUnitId: 'unit-1',
          resourceTypeId: 'wood',
          productTypeId: null,
          tick: 10,
          inflowQuantity: 4,
          outflowQuantity: 0,
          consumedQuantity: 4,
          producedQuantity: 0,
        },
        {
          buildingUnitId: 'unit-1',
          resourceTypeId: 'wood',
          productTypeId: null,
          tick: 11,
          inflowQuantity: 2,
          outflowQuantity: 0,
          consumedQuantity: 2,
          producedQuantity: 0,
        },
      ],
      520,
      170,
    )

    expect(model.hasData).toBe(true)
    expect(model.tickRange).toEqual({ start: 10, end: 11 })
    expect(model.series.find((series) => series.key === 'inflowQuantity')?.total).toBe(6)
    expect(model.series.find((series) => series.key === 'consumedQuantity')?.total).toBe(6)
    expect(model.series.find((series) => series.key === 'producedQuantity')?.polylinePoints).toContain('0,170')
    expect(model.maxValue).toBeGreaterThan(0)
  })

  it('returns an empty chart state when all metrics are zero', () => {
    const model = buildUnitResourceHistoryChartModel(
      [
        {
          buildingUnitId: 'unit-1',
          resourceTypeId: null,
          productTypeId: 'chair',
          tick: 15,
          inflowQuantity: 0,
          outflowQuantity: 0,
          consumedQuantity: 0,
          producedQuantity: 0,
        },
      ],
      520,
      170,
    )

    expect(model.hasData).toBe(false)
    expect(model.maxValue).toBe(1)
  })
})

describe('getRecentUnitResourceHistoryRows', () => {
  it('returns rows sorted from newest to oldest and respects the limit', () => {
    const rows = getRecentUnitResourceHistoryRows(
      [
        {
          buildingUnitId: 'unit-1',
          resourceTypeId: null,
          productTypeId: 'chair',
          tick: 9,
          inflowQuantity: 0,
          outflowQuantity: 1,
          consumedQuantity: 0,
          producedQuantity: 0,
        },
        {
          buildingUnitId: 'unit-1',
          resourceTypeId: null,
          productTypeId: 'chair',
          tick: 12,
          inflowQuantity: 0,
          outflowQuantity: 0,
          consumedQuantity: 0,
          producedQuantity: 3,
        },
        {
          buildingUnitId: 'unit-1',
          resourceTypeId: null,
          productTypeId: 'chair',
          tick: 11,
          inflowQuantity: 0,
          outflowQuantity: 0,
          consumedQuantity: 0,
          producedQuantity: 2,
        },
      ],
      2,
    )

    expect(rows.map((row) => row.tick)).toEqual([12, 11])
  })
})

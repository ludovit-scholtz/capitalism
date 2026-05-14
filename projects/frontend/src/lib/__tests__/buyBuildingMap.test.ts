import { describe, expect, it } from 'vitest'
import { nearestBuildingsForLot, syncSelectedLotId } from '@/lib/buyBuildingMap'

describe('syncSelectedLotId', () => {
  it('keeps selected lot id when lot is still available', () => {
    expect(
      syncSelectedLotId('lot-2', [
        { id: 'lot-1', latitude: 48.15, longitude: 17.11 },
        { id: 'lot-2', latitude: 48.14, longitude: 17.09 },
      ]),
    ).toBe('lot-2')
  })

  it('clears selected lot id when lot is no longer available', () => {
    expect(
      syncSelectedLotId('lot-2', [{ id: 'lot-1', latitude: 48.15, longitude: 17.11 }]),
    ).toBe('')
  })
})

describe('nearestBuildingsForLot', () => {
  it('returns nearest buildings ordered by distance and limited', () => {
    const nearest = nearestBuildingsForLot(
      { id: 'lot-a', latitude: 48.1486, longitude: 17.1077 },
      [
        { id: 'b1', name: 'Far', type: 'FACTORY', latitude: 48.2082, longitude: 16.3738 },
        { id: 'b2', name: 'Near', type: 'MINE', latitude: 48.1491, longitude: 17.1082 },
        { id: 'b3', name: 'Mid', type: 'SALES_SHOP', latitude: 48.175, longitude: 17.15 },
      ],
      2,
    )

    expect(nearest).toHaveLength(2)
    expect(nearest[0]?.name).toBe('Near')
    expect(nearest[1]?.name).toBe('Mid')
    expect(nearest[0]?.distanceKm ?? 0).toBeLessThan(nearest[1]?.distanceKm ?? 0)
  })

  it('returns empty list when no lot or buildings are provided', () => {
    expect(nearestBuildingsForLot(null, [])).toEqual([])
    expect(
      nearestBuildingsForLot(
        { id: 'lot-a', latitude: 48.1486, longitude: 17.1077 },
        [],
      ),
    ).toEqual([])
  })
})

import { describe, expect, it } from 'vitest'

import {
  getBuildingCountByCity,
  selectMainCity,
  shouldAutoSwitchCity,
} from '@/lib/cityContext'

describe('cityContext', () => {
  const cities = [
    { id: 'city-ba', name: 'Bratislava' },
    { id: 'city-pr', name: 'Prague' },
    { id: 'city-vi', name: 'Vienna' },
  ]

  it('counts all player buildings by city', () => {
    expect(
      getBuildingCountByCity([
        { cityId: 'city-ba', type: 'FACTORY' },
        { cityId: 'city-ba', type: 'SALES_SHOP' },
        { cityId: 'city-pr', type: 'FACTORY' },
        { cityId: 'city-pr', type: 'FACTORY' },
      ]),
    ).toEqual({
      'city-ba': 2,
      'city-pr': 2,
    })
  })

  it('selectMainCity returns the city with the most player buildings', () => {
    const result = selectMainCity(cities, [
      { cityId: 'city-ba', type: 'FACTORY' },
      { cityId: 'city-pr', type: 'FACTORY' },
      { cityId: 'city-pr', type: 'FACTORY' },
      { cityId: 'city-vi', type: 'SALES_SHOP' },
    ])

    expect(result).toEqual({ id: 'city-pr', name: 'Prague' })
  })

  it('selectMainCity breaks ties alphabetically by city name', () => {
    const result = selectMainCity(cities, [
      { cityId: 'city-pr', type: 'FACTORY' },
      { cityId: 'city-vi', type: 'FACTORY' },
      { cityId: 'city-ba', type: 'FACTORY' },
    ])

    expect(result).toEqual({ id: 'city-ba', name: 'Bratislava' })
  })

  it('selectMainCity can choose a city that only has non-factory buildings', () => {
    expect(
      selectMainCity(cities, [
        { cityId: 'city-ba', type: 'SALES_SHOP' },
        { cityId: 'city-ba', type: 'BANK' },
        { cityId: 'city-pr', type: 'BANK' },
      ]),
    ).toEqual({ id: 'city-ba', name: 'Bratislava' })
  })

  it('shouldAutoSwitchCity returns true when current city has zero buildings and main city differs', () => {
    expect(
      shouldAutoSwitchCity('city-pr', 'city-ba', [
        { cityId: 'city-ba', type: 'FACTORY' },
      ]),
    ).toBe(true)
  })

  it('shouldAutoSwitchCity returns false when the current city already has any building', () => {
    expect(
      shouldAutoSwitchCity('city-pr', 'city-ba', [
        { cityId: 'city-ba', type: 'FACTORY' },
        { cityId: 'city-pr', type: 'SALES_SHOP' },
      ]),
    ).toBe(false)
  })

  it('shouldAutoSwitchCity returns false when current city and main city match', () => {
    expect(
      shouldAutoSwitchCity('city-ba', 'city-ba', [{ cityId: 'city-ba', type: 'FACTORY' }]),
    ).toBe(false)
  })

  it('shouldAutoSwitchCity returns false when there is no main city', () => {
    expect(
      shouldAutoSwitchCity('city-pr', null, [{ cityId: 'city-pr', type: 'SALES_SHOP' }]),
    ).toBe(false)
  })
})

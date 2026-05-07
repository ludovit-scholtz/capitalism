import type { Building, City } from '@/types'

type CityContextBuilding = Pick<Building, 'cityId' | 'type'>
type CityContextCity = Pick<City, 'id' | 'name'>

export function getFactoryBuildingCountByCity(buildings: readonly CityContextBuilding[]): Record<string, number> {
  const counts: Record<string, number> = {}

  for (const building of buildings) {
    if (building.type !== 'FACTORY') {
      continue
    }

    counts[building.cityId] = (counts[building.cityId] ?? 0) + 1
  }

  return counts
}

export function selectMainCity(
  cities: readonly CityContextCity[],
  buildings: readonly CityContextBuilding[],
): CityContextCity | null {
  const counts = getFactoryBuildingCountByCity(buildings)
  const rankedCities = cities
    .map((city) => ({
      city,
      factoryCount: counts[city.id] ?? 0,
    }))
    .filter((entry) => entry.factoryCount > 0)
    .sort((left, right) => {
      if (right.factoryCount !== left.factoryCount) {
        return right.factoryCount - left.factoryCount
      }

      return left.city.name.localeCompare(right.city.name)
    })

  return rankedCities[0]?.city ?? null
}

export function shouldAutoSwitchCity(
  currentCityId: string | null | undefined,
  mainCityId: string | null | undefined,
  buildings: readonly CityContextBuilding[],
): boolean {
  if (!currentCityId || !mainCityId || currentCityId === mainCityId) {
    return false
  }

  const counts = getFactoryBuildingCountByCity(buildings)
  return (counts[currentCityId] ?? 0) === 0
}

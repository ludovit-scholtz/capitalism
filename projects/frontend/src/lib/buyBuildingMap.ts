import { computeDistanceKm } from '@/lib/globalExchange'

export interface BuyBuildingMapLot {
  id: string
  latitude: number
  longitude: number
}

export interface BuyBuildingMapBuilding {
  id: string
  name: string
  type: string
  latitude: number
  longitude: number
}

export interface NearestBuildingDistance extends BuyBuildingMapBuilding {
  distanceKm: number
}

export function syncSelectedLotId(selectedLotId: string, lots: BuyBuildingMapLot[]): string {
  if (!selectedLotId) return ''
  return lots.some((lot) => lot.id === selectedLotId) ? selectedLotId : ''
}

export function nearestBuildingsForLot(
  lot: BuyBuildingMapLot | null,
  buildings: BuyBuildingMapBuilding[],
  limit = 3,
): NearestBuildingDistance[] {
  if (!lot || buildings.length === 0 || limit <= 0) return []

  return buildings
    .map((building) => ({
      ...building,
      distanceKm: computeDistanceKm(
        lot.latitude,
        lot.longitude,
        building.latitude,
        building.longitude,
      ),
    }))
    .sort((a, b) => a.distanceKm - b.distanceKm)
    .slice(0, limit)
}

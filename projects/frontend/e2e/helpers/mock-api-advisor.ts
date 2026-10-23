import type { Page } from '@playwright/test'
import type { MockBuilding, MockBuildingUnit } from './mock-api'

export interface MockUnitStatus {
  buildingUnitId: string
  status: string
  blockedCode: string | null
  blockedReason: string | null
  idleTicks: number
}

/** A grid unit with every link off, enough for the dashboard (it only reads id and unit type). */
export function makeUnit(buildingId: string, id: string, unitType: string, gridX = 0): MockBuildingUnit {
  return {
    id,
    buildingId,
    unitType,
    gridX,
    gridY: 0,
    level: 1,
    linkUp: false,
    linkDown: false,
    linkLeft: false,
    linkRight: false,
    linkUpLeft: false,
    linkUpRight: false,
    linkDownLeft: false,
    linkDownRight: false,
  }
}

export function makeShop(id: string, name: string, units: MockBuildingUnit[]): MockBuilding {
  return {
    id,
    companyId: 'comp-advisor',
    cityId: 'city-ba',
    type: 'SALES_SHOP',
    name,
    latitude: 48.15,
    longitude: 17.11,
    level: 1,
    powerConsumption: 1,
    isForSale: false,
    builtAtUtc: '2026-01-01T00:00:00Z',
    units,
  }
}

/**
 * Serves `buildingUnitOperationalStatuses` from a canned per-building map, on top of `setupMockApi` (the most recently
 * registered Playwright route runs first). Call after `setupMockApi`, before navigating. Buildings without an entry fall
 * through to the shared mock.
 */
export async function mockUnitStatuses(page: Page, statusesByBuilding: Record<string, MockUnitStatus[]>): Promise<void> {
  await page.route('**/graphql', async (route) => {
    const body = route.request().postDataJSON() as { query?: string; variables?: { buildingId?: string } } | null
    const buildingId = body?.variables?.buildingId ?? ''
    if (/\bbuildingUnitOperationalStatuses\s*\(/.test(body?.query ?? '') && statusesByBuilding[buildingId]) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { buildingUnitOperationalStatuses: statusesByBuilding[buildingId] } }),
      })
      return
    }
    await route.fallback()
  })
}

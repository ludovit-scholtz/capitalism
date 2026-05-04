import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer, makeDefaultCities } from '../../helpers/mock-api'
import type { MockBuilding, MockBuildingUnit } from '../../helpers/mock-api'

// ── Helpers ────────────────────────────────────────────────────────────────

function authenticateViaLocalStorage(page: Parameters<typeof test>[0]['page'], token: string) {
  return page.addInitScript((t) => {
    localStorage.setItem('auth_token', t)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, token)
}

function clearSessionStorage(page: Parameters<typeof test>[0]['page']) {
  return page.addInitScript(() => {
    sessionStorage.clear()
  })
}

function makeFactoryBuilding(companyId: string): MockBuilding {
  const unit: MockBuildingUnit = {
    id: 'unit-grid-1',
    buildingId: 'building-grid-1',
    unitType: 'MANUFACTURING',
    gridX: 0,
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
    resourceTypeId: null,
    startedAtTick: null,
    appliesAtTick: null,
    ticksRequired: null,
    isChanged: false,
    isReverting: false,
  }
  return {
    id: 'building-grid-1',
    companyId,
    cityId: 'city-ba',
    type: 'FACTORY',
    name: 'Test Factory',
    latitude: 48.15,
    longitude: 17.11,
    level: 1,
    powerConsumption: 2,
    isForSale: false,
    builtAtUtc: '2026-01-01T00:00:00Z',
    pendingConfiguration: null,
    powerStatus: 'POWERED',
    lotMaterialQuantity: null,
    lotOriginalMaterialQuantity: null,
    units: [unit],
  }
}

// ── Grid editor tooltip tests ──────────────────────────────────────────────

test.describe('Building detail and grid editor contextual tooltip overlay', () => {
  test('new player sees building detail tooltip overlay on first visit', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
    const factory = makeFactoryBuilding('company-grid-1')
    player.companies.push({
      id: 'company-grid-1',
      playerId: player.id,
      name: 'Grid Co',
      cash: 500000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [factory],
    })

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    // No tooltip dismissed yet
    state.tutorialProgress = []
    state.cities = makeDefaultCities()

    await clearSessionStorage(page)
    await authenticateViaLocalStorage(page, `token-${player.id}`)
    await page.goto('/building/building-grid-1')

    // Building detail tooltip appears first (before grid editor tooltip)
    await expect(page.locator('.tutorial-tooltip')).toBeVisible({ timeout: 5000 })
    await expect(page.locator('.tutorial-tooltip__title')).toContainText('Building Detail View')
  })

  test('player can dismiss building detail tooltip by clicking "Got it"', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
    const factory = makeFactoryBuilding('company-grid-2')
    player.companies.push({
      id: 'company-grid-2',
      playerId: player.id,
      name: 'Grid Co 2',
      cash: 500000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [factory],
    })

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.tutorialProgress = []
    state.cities = makeDefaultCities()

    await clearSessionStorage(page)
    await authenticateViaLocalStorage(page, `token-${player.id}`)
    await page.goto('/building/building-grid-1')

    // Building detail tooltip should appear first
    await expect(page.locator('.tutorial-tooltip')).toBeVisible({ timeout: 5000 })
    await expect(page.locator('.tutorial-tooltip__title')).toContainText('Building Detail View')

    // Dismiss it
    await page.locator('.tutorial-tooltip__dismiss-btn').click()

    // The building detail tooltip specifically is gone; grid editor may appear
    await expect(
      page.locator('.tutorial-tooltip[aria-label="Building Detail View"]'),
    ).toBeHidden()
  })

  test('grid editor tooltip appears after building detail tooltip is dismissed', async ({
    page,
  }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
    const factory = makeFactoryBuilding('company-grid-3')
    player.companies.push({
      id: 'company-grid-3',
      playerId: player.id,
      name: 'Grid Co 3',
      cash: 500000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [factory],
    })

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    // Building detail tooltip is already dismissed, grid editor is not
    state.tutorialProgress = [
      {
        milestone: 'TOOLTIP_BUILDING_DETAIL_SHOWN',
        isCompleted: true,
        completedAtUtc: '2026-01-01T00:00:00Z',
      },
    ]
    state.cities = makeDefaultCities()

    await clearSessionStorage(page)
    // Set sessionStorage flag so building detail tooltip is suppressed
    await page.addInitScript(() => {
      sessionStorage.setItem('tt_building_detail_dismissed', '1')
    })
    await authenticateViaLocalStorage(page, `token-${player.id}`)
    await page.goto('/building/building-grid-1')

    // Grid editor tooltip should appear (in the grid section)
    await expect(page.locator('.tutorial-tooltip')).toBeVisible({ timeout: 5000 })
    await expect(page.locator('.tutorial-tooltip__title')).toContainText('Unit Grid Editor')
  })

  test('returning player with both tooltips dismissed sees no tooltip', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
    const factory = makeFactoryBuilding('company-grid-4')
    player.companies.push({
      id: 'company-grid-4',
      playerId: player.id,
      name: 'Grid Co 4',
      cash: 500000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [factory],
    })

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    // Both tooltips already dismissed
    state.tutorialProgress = [
      {
        milestone: 'TOOLTIP_BUILDING_DETAIL_SHOWN',
        isCompleted: true,
        completedAtUtc: '2026-01-01T00:00:00Z',
      },
      {
        milestone: 'TOOLTIP_GRID_EDITOR_SHOWN',
        isCompleted: true,
        completedAtUtc: '2026-01-01T00:00:00Z',
      },
    ]
    state.cities = makeDefaultCities()

    await clearSessionStorage(page)
    await page.addInitScript(() => {
      sessionStorage.setItem('tt_building_detail_dismissed', '1')
      sessionStorage.setItem('tt_grid_editor_dismissed', '1')
    })
    await authenticateViaLocalStorage(page, `token-${player.id}`)
    await page.goto('/building/building-grid-1')

    // Wait for tooltip delay
    await page.waitForTimeout(2000)

    // No tooltip should appear
    await expect(page.locator('.tutorial-tooltip')).toBeHidden()
  })

  test('grid editor tooltip can be dismissed via Escape key', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
    const factory = makeFactoryBuilding('company-grid-5')
    player.companies.push({
      id: 'company-grid-5',
      playerId: player.id,
      name: 'Grid Co 5',
      cash: 500000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [factory],
    })

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.tutorialProgress = [
      {
        milestone: 'TOOLTIP_BUILDING_DETAIL_SHOWN',
        isCompleted: true,
        completedAtUtc: '2026-01-01T00:00:00Z',
      },
    ]
    state.cities = makeDefaultCities()

    await clearSessionStorage(page)
    await page.addInitScript(() => {
      sessionStorage.setItem('tt_building_detail_dismissed', '1')
    })
    await authenticateViaLocalStorage(page, `token-${player.id}`)
    await page.goto('/building/building-grid-1')

    await expect(page.locator('.tutorial-tooltip')).toBeVisible({ timeout: 5000 })
    await page.keyboard.press('Escape')
    await expect(page.locator('.tutorial-tooltip')).toBeHidden()
  })

  test('grid editor tooltip persists dismissal via mutation', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
    const factory = makeFactoryBuilding('company-grid-6')
    player.companies.push({
      id: 'company-grid-6',
      playerId: player.id,
      name: 'Grid Co 6',
      cash: 500000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [factory],
    })

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.tutorialProgress = [
      {
        milestone: 'TOOLTIP_BUILDING_DETAIL_SHOWN',
        isCompleted: true,
        completedAtUtc: '2026-01-01T00:00:00Z',
      },
    ]
    state.cities = makeDefaultCities()

    await clearSessionStorage(page)
    await page.addInitScript(() => {
      sessionStorage.setItem('tt_building_detail_dismissed', '1')
    })
    await authenticateViaLocalStorage(page, `token-${player.id}`)
    await page.goto('/building/building-grid-1')

    await expect(page.locator('.tutorial-tooltip')).toBeVisible({ timeout: 5000 })
    await page.locator('.tutorial-tooltip__dismiss-btn').click()
    await expect(page.locator('.tutorial-tooltip')).toBeHidden()

    // Verify milestone was persisted
    await page.waitForTimeout(500)
    const saved = state.tutorialProgress.find((m) => m.milestone === 'TOOLTIP_GRID_EDITOR_SHOWN')
    expect(saved?.isCompleted).toBe(true)
  })
})

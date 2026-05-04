import { test, expect } from '@playwright/test'
import { makePlayer, setupMockApi, type MockBuilding } from '../../helpers/mock-api'

/**
 * Creates a MINE building with optional depletion state.
 */
function makeMineBuilding(
  opts: {
    id?: string
    companyId?: string
    cityId?: string
    name?: string
    lotMaterialQuantity?: number | null
    lotOriginalMaterialQuantity?: number | null
  } = {},
): MockBuilding {
  return {
    id: opts.id ?? 'building-mine-1',
    companyId: opts.companyId ?? 'company-depletion',
    cityId: opts.cityId ?? 'city-ba',
    type: 'MINE',
    name: opts.name ?? 'Coal Mine',
    latitude: 48.15,
    longitude: 17.11,
    level: 1,
    powerConsumption: 2,
    isForSale: false,
    builtAtUtc: '2026-01-01T00:00:00Z',
    pendingConfiguration: null,
    lotMaterialQuantity: opts.lotMaterialQuantity ?? 1000,
    lotOriginalMaterialQuantity: opts.lotOriginalMaterialQuantity ?? 1600,
    lotResourceTypeId: 'res-coal',
    lotMaterialQuality: 0.7,
    units: [
      {
        id: 'unit-mining-1',
        buildingId: opts.id ?? 'building-mine-1',
        unitType: 'MINING',
        gridX: 1,
        gridY: 1,
        level: 1,
        linkUp: false,
        linkDown: false,
        linkLeft: false,
        linkRight: false,
        linkUpLeft: false,
        linkUpRight: false,
        linkDownLeft: false,
        linkDownRight: false,
        resourceTypeId: 'res-coal',
      },
    ],
  }
}

test.describe('Resource Depletion - Mining Unit Detail Panel', () => {
  test('shows deposit progress bar with remaining percentage for healthy mine', async ({ page }) => {
    const player = makePlayer()
    player.companies.push({
      id: 'company-depletion',
      playerId: player.id,
      name: 'Depletion Corp',
      cash: 500000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [
        makeMineBuilding({ lotMaterialQuantity: 1392, lotOriginalMaterialQuantity: 1600 }), // 87%
      ],
    })

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/building/building-mine-1')
    await expect(page.getByRole('heading', { name: 'Coal Mine' })).toBeVisible()

    // Click on the MINING unit cell to select it
    const activeGrid = page.locator('.grid-container').first()
    await activeGrid.locator('.grid-cell').first().click()

    // Resource Status panel should be visible
    await expect(page.locator('.mining-resource-status-panel')).toBeVisible()
    // Progress bar should be visible
    await expect(page.locator('.deposit-progress-bar')).toBeVisible()
    // Should show ~87% remaining text
    await expect(page.locator('.mining-resource-status-panel')).toContainText('87% Remaining')
  })

  test('shows depletion risk badge and warning when below 20%', async ({ page }) => {
    const player = makePlayer()
    player.companies.push({
      id: 'company-depletion',
      playerId: player.id,
      name: 'Depletion Corp',
      cash: 500000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [
        makeMineBuilding({ lotMaterialQuantity: 192, lotOriginalMaterialQuantity: 1600 }), // 12%
      ],
    })

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/building/building-mine-1')
    await expect(page.getByRole('heading', { name: 'Coal Mine' })).toBeVisible()

    // Click on the MINING unit cell
    const activeGrid = page.locator('.grid-container').first()
    await activeGrid.locator('.grid-cell').first().click()

    // Depletion risk badge should appear in the panel
    await expect(page.locator('.depletion-risk-badge')).toBeVisible()
    // Should show risk text
    await expect(page.locator('.mining-resource-status-panel')).toContainText('Depletion Risk')
  })

  test('shows estimated ticks until depletion when mining rate is known', async ({ page }) => {
    const player = makePlayer()
    player.companies.push({
      id: 'company-depletion',
      playerId: player.id,
      name: 'Depletion Corp',
      cash: 500000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [
        makeMineBuilding({ lotMaterialQuantity: 500, lotOriginalMaterialQuantity: 1600 }),
      ],
    })

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/building/building-mine-1')
    await expect(page.getByRole('heading', { name: 'Coal Mine' })).toBeVisible()

    const activeGrid = page.locator('.grid-container').first()
    await activeGrid.locator('.grid-cell').first().click()

    // Panel shows estimated depletion (level 1 = 10 units/tick → 500/10 = 50 ticks)
    await expect(page.locator('.mining-resource-status-panel')).toContainText('ticks')
  })

  test('shows depleted state when materialQuantity is 0', async ({ page }) => {
    const player = makePlayer()
    player.companies.push({
      id: 'company-depletion',
      playerId: player.id,
      name: 'Depletion Corp',
      cash: 500000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [
        makeMineBuilding({ lotMaterialQuantity: 0, lotOriginalMaterialQuantity: 1600 }),
      ],
    })

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/building/building-mine-1')
    await expect(page.getByRole('heading', { name: 'Coal Mine' })).toBeVisible()

    const activeGrid = page.locator('.grid-container').first()
    await activeGrid.locator('.grid-cell').first().click()

    // Should show "Depleted" text prominently
    await expect(page.locator('.depleted-state')).toBeVisible()
    await expect(page.locator('.mining-resource-status-panel')).toContainText('Depleted')
  })

  test("view available lots button is present in depleted state", async ({ page }) => {
    const player = makePlayer()
    player.companies.push({
      id: 'company-depletion',
      playerId: player.id,
      name: 'Depletion Corp',
      cash: 500000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [
        makeMineBuilding({ lotMaterialQuantity: 0, lotOriginalMaterialQuantity: 1600 }),
      ],
    })

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/building/building-mine-1')
    await expect(page.getByRole('heading', { name: 'Coal Mine' })).toBeVisible()

    const activeGrid = page.locator('.grid-container').first()
    await activeGrid.locator('.grid-cell').first().click()

    // "View Available Lots" button should be present
    await expect(page.locator('.depleted-state').getByRole('button', { name: 'View Available Lots' })).toBeVisible()
  })
})

test.describe('Resource Depletion - Dashboard Building Card Badge', () => {
  test('shows depletion risk badge on dashboard for mine building below 20%', async ({ page }) => {
    const player = makePlayer()
    player.companies.push({
      id: 'company-depletion',
      playerId: player.id,
      name: 'Depletion Corp',
      cash: 500000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [
        makeMineBuilding({ lotMaterialQuantity: 192, lotOriginalMaterialQuantity: 1600 }), // 12% → badge
      ],
    })

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/dashboard')
    await expect(page.getByRole('heading', { name: 'Depletion Corp' })).toBeVisible()

    // Depletion risk badge should appear on the building card
    await expect(page.locator('.depletion-risk-badge')).toBeVisible()
  })

  test('does NOT show depletion risk badge for mine above 20%', async ({ page }) => {
    const player = makePlayer()
    player.companies.push({
      id: 'company-healthy',
      playerId: player.id,
      name: 'Healthy Mining Co',
      cash: 500000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [
        makeMineBuilding({
          id: 'building-mine-healthy',
          companyId: 'company-healthy',
          lotMaterialQuantity: 1000,
          lotOriginalMaterialQuantity: 1600,
        }), // 62.5% → no badge
      ],
    })

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/dashboard')
    await expect(page.getByRole('heading', { name: 'Healthy Mining Co' })).toBeVisible()

    // Depletion risk badge should NOT appear
    await expect(page.locator('.depletion-risk-badge')).toHaveCount(0)
  })
})

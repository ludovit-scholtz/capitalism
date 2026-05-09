import { test, expect } from '@playwright/test'
import { makePlayer, setupMockApi, type MockBuilding } from '../../helpers/mock-api'

/**
 * Creates a MINE building with optional depletion state.
 * The MINING unit is placed at (0,0) so `.grid-cell.first()` selects it.
 * The building grid renders cells in row-major order starting at (0,0),
 * so a unit at gridX:0, gridY:0 is always the first rendered cell.
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
        resourceTypeId: 'res-coal',
      },
    ],
  }
}

/** Helper: create an authenticated player setup for building-detail tests. */
async function setupMineDetailPage(
  page: Parameters<typeof setupMockApi>[0],
  opts: Parameters<typeof makeMineBuilding>[0] = {},
) {
  const player = makePlayer()
  player.companies.push({
    id: opts.companyId ?? 'company-depletion',
    playerId: player.id,
    name: 'Depletion Corp',
    cash: 500000,
    foundedAtUtc: '2026-01-01T00:00:00Z',
    buildings: [makeMineBuilding(opts)],
  })

  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  return state
}

test.describe('Resource Depletion - Mining Unit Detail Panel', () => {
  test('shows deposit progress bar with remaining percentage for healthy mine', async ({ page }) => {
    // 1392 / 1600 = 87%
    await setupMineDetailPage(page, { lotMaterialQuantity: 1392, lotOriginalMaterialQuantity: 1600 })

    await page.goto('/building/building-mine-1')
    await expect(page.getByRole('heading', { name: 'Coal Mine' })).toBeVisible()

    // Click on the MINING unit cell (placed at gridX:0, gridY:0 → first .grid-cell)
    await page.locator('.grid-container').first().locator('.grid-cell').first().click()

    // Resource Status panel should be visible with active-deposit-state
    await expect(page.locator('.mining-resource-status-panel')).toBeVisible()
    await expect(page.locator('.active-deposit-state')).toBeVisible()
    // Progress bar should be visible
    await expect(page.locator('.deposit-progress-bar')).toBeVisible()
    // Should show 87% remaining text
    await expect(page.locator('.mining-resource-status-panel')).toContainText('87% Remaining')
  })

  test('shows depletion risk badge and warning when below 20%', async ({ page }) => {
    // 192 / 1600 = 12% → depletion risk
    await setupMineDetailPage(page, { lotMaterialQuantity: 192, lotOriginalMaterialQuantity: 1600 })

    await page.goto('/building/building-mine-1')
    await expect(page.getByRole('heading', { name: 'Coal Mine' })).toBeVisible()

    await page.locator('.grid-container').first().locator('.grid-cell').first().click()

    // Depletion risk badge should appear inside the panel
    await expect(page.locator('.mining-resource-status-panel .depletion-risk-badge')).toBeVisible()
    await expect(page.locator('.mining-resource-status-panel')).toContainText('Depletion Risk')
  })

  test('does NOT show depletion risk badge when deposit is exactly 20%', async ({ page }) => {
    // 320 / 1600 = exactly 20% — condition is strictly < 0.20 so badge must NOT appear
    await setupMineDetailPage(page, { lotMaterialQuantity: 320, lotOriginalMaterialQuantity: 1600 })

    await page.goto('/building/building-mine-1')
    await expect(page.getByRole('heading', { name: 'Coal Mine' })).toBeVisible()

    await page.locator('.grid-container').first().locator('.grid-cell').first().click()

    await expect(page.locator('.mining-resource-status-panel')).toBeVisible()
    // At exactly 20%, no badge should be shown (boundary check)
    await expect(page.locator('.active-deposit-state')).toBeVisible()
    await expect(page.locator('.mining-resource-status-panel .depletion-risk-badge')).toHaveCount(0)
    await expect(page.locator('.mining-resource-status-panel')).toContainText('20% Remaining')
  })

  test('shows estimated ticks until depletion when mining rate is known', async ({ page }) => {
    // At 31% remaining, scarcity curve applies (~69% efficiency): 500 / (10 * 0.69) ≈ 73 ticks.
    await setupMineDetailPage(page, { lotMaterialQuantity: 500, lotOriginalMaterialQuantity: 1600 })

    await page.goto('/building/building-mine-1')
    await expect(page.getByRole('heading', { name: 'Coal Mine' })).toBeVisible()

    await page.locator('.grid-container').first().locator('.grid-cell').first().click()

    // Panel shows estimated depletion count in ticks
    await expect(page.locator('.mining-resource-status-panel')).toContainText('ticks')
    await expect(page.locator('.mining-resource-status-panel')).toContainText('~73 ticks')
  })

  test('shows depleted state when materialQuantity is 0', async ({ page }) => {
    await setupMineDetailPage(page, { lotMaterialQuantity: 0, lotOriginalMaterialQuantity: 1600 })

    await page.goto('/building/building-mine-1')
    await expect(page.getByRole('heading', { name: 'Coal Mine' })).toBeVisible()

    await page.locator('.grid-container').first().locator('.grid-cell').first().click()

    // Should show "Depleted" state (not the active-deposit-state)
    await expect(page.locator('.depleted-state')).toBeVisible()
    await expect(page.locator('.active-deposit-state')).toHaveCount(0)
    await expect(page.locator('.mining-resource-status-panel')).toContainText('Depleted')
  })

  test('view available lots button is present in depleted state', async ({ page }) => {
    await setupMineDetailPage(page, { lotMaterialQuantity: 0, lotOriginalMaterialQuantity: 1600 })

    await page.goto('/building/building-mine-1')
    await expect(page.getByRole('heading', { name: 'Coal Mine' })).toBeVisible()

    await page.locator('.grid-container').first().locator('.grid-cell').first().click()

    // "View Available Lots" button should be present in the depleted state
    await expect(
      page.locator('.depleted-state').getByRole('button', { name: 'View Available Lots' }),
    ).toBeVisible()
  })

  test('view available lots button also present in active deposit state', async ({ page }) => {
    // Even for a healthy mine, the "View Available Lots" link should be accessible
    await setupMineDetailPage(page, { lotMaterialQuantity: 800, lotOriginalMaterialQuantity: 1600 })

    await page.goto('/building/building-mine-1')
    await expect(page.getByRole('heading', { name: 'Coal Mine' })).toBeVisible()

    await page.locator('.grid-container').first().locator('.grid-cell').first().click()

    await expect(page.locator('.active-deposit-state')).toBeVisible()
    await expect(
      page.locator('.active-deposit-state').getByRole('button', { name: 'View Available Lots' }),
    ).toBeVisible()
  })
})

test.describe('Resource Depletion - Dashboard Building Card Badge', () => {
  test('shows depletion risk badge on dashboard for mine building below 20%', async ({ page }) => {
    const player = makePlayer()
    // Must set onboardingCompletedAtUtc so DashboardView does not redirect to /onboarding
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
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

    // Switch to the Buildings tab — the badge is inside the buildings tab panel
    await page.getByRole('tab', { name: 'Buildings' }).click()

    // Depletion risk badge should appear on the building card
    await expect(page.locator('.depletion-risk-badge')).toBeVisible()
  })

  test('shows depleted badge on dashboard for fully depleted mine', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
    player.companies.push({
      id: 'company-depleted',
      playerId: player.id,
      name: 'Depleted Mine Corp',
      cash: 500000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [
        makeMineBuilding({ lotMaterialQuantity: 0, lotOriginalMaterialQuantity: 1600 }), // 0% → depleted
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
    await expect(page.getByRole('heading', { name: 'Depleted Mine Corp' })).toBeVisible()

    // Switch to the Buildings tab — the badge is inside the buildings tab panel
    await page.getByRole('tab', { name: 'Buildings' }).click()

    // The badge text should say "Depleted" when materialQuantity is 0
    await expect(page.locator('.depletion-risk-badge')).toBeVisible()
    await expect(page.locator('.depletion-risk-badge')).toContainText('Depleted')
  })

  test('does NOT show depletion risk badge for mine above 20%', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
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

    // Switch to the Buildings tab before checking badge absence
    await page.getByRole('tab', { name: 'Buildings' }).click()

    // Depletion risk badge should NOT appear
    await expect(page.locator('.depletion-risk-badge')).toHaveCount(0)
  })

  test('does NOT show depletion risk badge for non-MINE buildings', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
    player.companies.push({
      id: 'company-factory',
      playerId: player.id,
      name: 'Factory Corp',
      cash: 500000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [
        {
          id: 'building-factory-1',
          companyId: 'company-factory',
          cityId: 'city-ba',
          type: 'FACTORY',
          name: 'Main Factory',
          latitude: 48.15,
          longitude: 17.11,
          level: 1,
          powerConsumption: 5,
          isForSale: false,
          builtAtUtc: '2026-01-01T00:00:00Z',
          pendingConfiguration: null,
          // Provide depletion data — badge should NOT show for non-MINE type
          lotMaterialQuantity: 50,
          lotOriginalMaterialQuantity: 1600,
          units: [],
        },
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
    await expect(page.getByRole('heading', { name: 'Factory Corp' })).toBeVisible()

    // Switch to the Buildings tab
    await page.getByRole('tab', { name: 'Buildings' }).click()

    // Building is type FACTORY — depletion badge must not appear regardless of lot values
    await expect(page.locator('.depletion-risk-badge')).toHaveCount(0)
  })
})

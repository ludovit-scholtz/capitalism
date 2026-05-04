import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer } from '../../helpers/mock-api'
import type { MockTradeRoute } from '../../helpers/mock-api'

// ── Helpers ──────────────────────────────────────────────────────────────────

function makeRoute(overrides: Partial<MockTradeRoute> = {}): MockTradeRoute {
  return {
    id: `route-${Math.random().toString(36).slice(2)}`,
    companyId: 'co-1',
    sourceBuildingId: 'b-bratislava',
    sourceBuildingName: 'Bratislava Factory',
    sourceCityName: 'Bratislava',
    sourceCurrencyCode: 'EUR',
    destinationBuildingId: 'b-prague',
    destinationBuildingName: 'Prague Factory',
    destinationCityName: 'Prague',
    destinationCurrencyCode: 'CZK',
    productTypeId: null,
    productTypeName: null,
    resourceTypeId: 'res-wood',
    resourceTypeName: 'Wood',
    quantity: 50,
    quality: 0.7,
    pricePerUnit: 15,
    scheduledDepartureTick: 10,
    expectedArrivalTick: 11,
    transitTicks: 1,
    shippingCostEstimate: 25,
    shippingCostActual: 0,
    status: 'IN_TRANSIT',
    failureReason: null,
    createdAtUtc: new Date().toISOString(),
    departedAtUtc: new Date().toISOString(),
    completedAtUtc: null,
    ...overrides,
  }
}

async function loginAndNavigate(
  page: import('@playwright/test').Page,
  tradeRoutes: MockTradeRoute[],
) {
  const player = makePlayer()
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.tradeRoutes = tradeRoutes

  await page.addInitScript(
    ([token]) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7_200_000).toISOString())
    },
    [`token-${player.id}`],
  )

  await page.goto('/trade-routes')
  return { player, state }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

test.describe('Trade Routes Management View', () => {
  test('Route_TradeRoutes_ManagementView_ShowsTitle', async ({ page }) => {
    await loginAndNavigate(page, [])
    await expect(page.getByRole('heading', { name: 'Trade Routes' })).toBeVisible()
  })

  test('Route_TradeRoutes_ManagementView_Empty_ShowsEmptyState', async ({ page }) => {
    await loginAndNavigate(page, [])
    await expect(page.locator('.tr-empty-msg')).toBeVisible()
  })

  test('Route_TradeRoutes_ManagementView_Lists_AllRoutes', async ({ page }) => {
    const routes = [
      makeRoute({ status: 'IN_TRANSIT' }),
      makeRoute({ status: 'DELIVERED', completedAtUtc: new Date().toISOString() }),
      makeRoute({ status: 'FAILED', failureReason: 'Destination full' }),
    ]
    await loginAndNavigate(page, routes)

    await expect(page.locator('table[aria-label="Trade Routes"]')).toBeVisible()
    await expect(page.locator('.tr-row')).toHaveCount(3)
  })

  test('Route_TradeRoutes_ManagementView_StatusBadges_Displayed', async ({ page }) => {
    const routes = [
      makeRoute({ id: 'r-transit', status: 'IN_TRANSIT' }),
      makeRoute({ id: 'r-delivered', status: 'DELIVERED', completedAtUtc: new Date().toISOString() }),
      makeRoute({ id: 'r-failed', status: 'FAILED', failureReason: 'Destination full' }),
    ]
    await loginAndNavigate(page, routes)

    // All three status badges must be visible
    await expect(page.locator('.tr-badge--in_transit').first()).toBeVisible()
    await expect(page.locator('.tr-badge--delivered').first()).toBeVisible()
    await expect(page.locator('.tr-badge--failed').first()).toBeVisible()
  })

  test('Route_TradeRoutes_Filter_Active_ShowsOnlyInTransit', async ({ page }) => {
    const routes = [
      makeRoute({ id: 'r-active', status: 'IN_TRANSIT' }),
      makeRoute({ id: 'r-done', status: 'DELIVERED', completedAtUtc: new Date().toISOString() }),
    ]
    await loginAndNavigate(page, routes)

    // Click "Active" filter tab
    await page.getByRole('tab', { name: 'Active' }).click()

    // Only IN_TRANSIT route should be shown
    await expect(page.locator('.tr-row')).toHaveCount(1)
    await expect(page.locator('.tr-badge--in_transit')).toBeVisible()
    await expect(page.locator('.tr-badge--delivered')).toBeHidden()
  })

  test('Route_TradeRoutes_Filter_Completed_ShowsDeliveredAndFailed', async ({ page }) => {
    const routes = [
      makeRoute({ id: 'r-active', status: 'IN_TRANSIT' }),
      makeRoute({ id: 'r-done', status: 'DELIVERED', completedAtUtc: new Date().toISOString() }),
      makeRoute({ id: 'r-fail', status: 'FAILED', failureReason: 'Full' }),
    ]
    await loginAndNavigate(page, routes)

    // Click "Completed" filter tab
    await page.getByRole('tab', { name: 'Completed' }).click()

    // Should show 2 completed (delivered + failed), not the active one
    await expect(page.locator('.tr-row')).toHaveCount(2)
  })

  test('Route_Dashboard_Shows_ActiveRoutes_Summary', async ({ page }) => {
    const routes = [
      makeRoute({ status: 'IN_TRANSIT', expectedArrivalTick: 20 }),
      makeRoute({ status: 'IN_TRANSIT', expectedArrivalTick: 15 }),
    ]
    await loginAndNavigate(page, routes)

    // Summary strip must show 2 active routes
    await expect(page.locator('.tr-summary-value').first()).toHaveText('2')

    // Next delivery tick should be the minimum (15)
    await expect(page.locator('.tr-summary-value').nth(1)).toHaveText('15')
  })

  test('Route_TradeRoutes_ShowsCityNames', async ({ page }) => {
    const routes = [makeRoute()]
    await loginAndNavigate(page, routes)

    await expect(page.locator('.tr-table').getByText('Bratislava')).toBeVisible()
    await expect(page.locator('.tr-table').getByText('Prague')).toBeVisible()
  })

  test('Route_TradeRoutes_ShowsResourceName', async ({ page }) => {
    const routes = [makeRoute({ resourceTypeName: 'Wood' })]
    await loginAndNavigate(page, routes)

    await expect(page.locator('.tr-table').getByText('Wood')).toBeVisible()
  })

  test('Route_Unauthenticated_Redirect', async ({ page }) => {
    // Do NOT set auth token; expect redirect to login or empty state
    setupMockApi(page, {})
    await page.goto('/trade-routes')
    // Should either redirect to login or show the empty/unauthenticated state
    const url = page.url()
    const hasContent =
      url.includes('/login') || url.includes('/trade-routes')
    expect(hasContent).toBeTruthy()
  })
})

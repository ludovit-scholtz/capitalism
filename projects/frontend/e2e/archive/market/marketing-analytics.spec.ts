import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer } from '../../helpers/mock-api'

function makeAnalyticsResult(companyId: string, withRows = false) {
  if (!withRows) {
    return {
      companyId,
      windowTicks: 10,
      totalRevenue: 0,
      totalMarketingSpend: 0,
      bestPerformingCity: null,
      bestPerformingProduct: null,
      globalRecommendation: 'No sales data yet. Open a sales shop and start selling.',
      rows: [],
    }
  }
  return {
    companyId,
    windowTicks: 10,
    totalRevenue: 4500.0,
    totalMarketingSpend: 200.0,
    bestPerformingCity: 'Bratislava',
    bestPerformingProduct: 'Wooden Chair',
    globalRecommendation: 'Your brand is working. Consider expanding to new cities.',
    rows: [
      {
        buildingUnitId: 'unit-001',
        buildingId: 'bld-001',
        buildingName: 'Main Shop',
        productName: 'Wooden Chair',
        productTypeId: 'prod-001',
        cityName: 'Bratislava',
        brandAwareness: 0.72,
        brandQuality: 0.61,
        marketingQuality: 0.55,
        currentPrice: 48.0,
        basePrice: 45.0,
        priceIndex: 1.03,
        revenueLastTicks: 4500.0,
        quantityLastTicks: 50.0,
        utilizationRate: 0.85,
        trendDirection: 'UP',
        demandSignal: 'STRONG',
        topPositiveFactor: 'BRAND_QUALITY',
        topNegativeFactor: null,
        brandRevenueBoost: 350.0,
        campaignImpact: 'STRONG',
        brandVsPriceBalance: 'PREMIUM_JUSTIFIED',
        recommendation: 'Your brand quality justifies the premium price. Maintain marketing investment.',
        cityCurrencyCode: 'EUR',
      },
    ],
  }
}

test.describe('Marketing Analytics Dashboard', () => {
  test('shows login-required message when not authenticated', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/marketing-analytics')
    await expect(page.getByText(/please log in/i).first()).toBeVisible()
  })

  test('shows page title when authenticated', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript(
      (token) => {
        localStorage.setItem('auth_token', token)
        localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
      },
      `token-${player.id}`,
    )
    await page.goto('/marketing-analytics')
    await expect(page.getByRole('heading', { name: /Campaign Analytics/i })).toBeVisible()
  })

  test('shows empty-state recommendation when no sales data', async ({ page }) => {
    const player = makePlayer()
    player.companies.push({
      id: 'comp-001',
      playerId: player.id,
      name: 'Empty Co',
      cash: 10000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.campaignAnalytics['comp-001'] = makeAnalyticsResult('comp-001', false)
    await page.addInitScript(
      (token) => {
        localStorage.setItem('auth_token', token)
        localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
      },
      `token-${player.id}`,
    )
    await page.goto('/marketing-analytics')
    // Empty-state global recommendation text is displayed
    await expect(page.getByText(/No sales data yet/i)).toBeVisible()
  })

  test('shows KPI cards and analytics row when data is available', async ({ page }) => {
    const player = makePlayer()
    player.companies.push({
      id: 'comp-101',
      playerId: player.id,
      name: 'Brand Empire',
      cash: 50000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.campaignAnalytics['comp-101'] = makeAnalyticsResult('comp-101', true)
    await page.addInitScript(
      (token) => {
        localStorage.setItem('auth_token', token)
        localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
      },
      `token-${player.id}`,
    )
    await page.goto('/marketing-analytics')

    // KPI summary row
    await expect(page.locator('[aria-label="Campaign summary"]')).toBeVisible()
    await expect(page.getByText('Total Revenue')).toBeVisible()
    await expect(page.getByText('Marketing Spend')).toBeVisible()

    // Best city and product appear in KPI cards
    await expect(page.locator('.ca-kpi-card').filter({ hasText: 'Best City' }).getByRole('strong')).toContainText('Bratislava')
    await expect(page.locator('.ca-kpi-card').filter({ hasText: 'Best Product' }).getByRole('strong')).toContainText('Wooden Chair')

    // Analytics row with brand data
    await expect(page.locator('.ca-row-card').first()).toBeVisible()
    await expect(page.getByText('Main Shop')).toBeVisible()
  })

  test('shows brand vs price balance badge for PREMIUM_JUSTIFIED', async ({ page }) => {
    const player = makePlayer()
    player.companies.push({
      id: 'comp-102',
      playerId: player.id,
      name: 'Premium Brand',
      cash: 50000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.campaignAnalytics['comp-102'] = makeAnalyticsResult('comp-102', true)
    await page.addInitScript(
      (token) => {
        localStorage.setItem('auth_token', token)
        localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
      },
      `token-${player.id}`,
    )
    await page.goto('/marketing-analytics')

    // The balance badge for PREMIUM_JUSTIFIED should be visible
    await expect(page.locator('.ca-balance-badge').first()).toBeVisible()
  })

  test('shows global recommendation text', async ({ page }) => {
    const player = makePlayer()
    player.companies.push({
      id: 'comp-103',
      playerId: player.id,
      name: 'Growing Corp',
      cash: 50000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.campaignAnalytics['comp-103'] = makeAnalyticsResult('comp-103', true)
    await page.addInitScript(
      (token) => {
        localStorage.setItem('auth_token', token)
        localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
      },
      `token-${player.id}`,
    )
    await page.goto('/marketing-analytics')

    // Global recommendation text visible in the recommendation panel
    await expect(page.locator('.ca-global-rec')).toBeVisible()
    await expect(page.locator('.ca-global-rec')).toContainText('brand')
  })
})

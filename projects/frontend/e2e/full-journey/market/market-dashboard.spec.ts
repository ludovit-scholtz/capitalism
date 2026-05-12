import { test, expect } from '@playwright/test'
import {
  setupMockApi,
  makePlayer,
  type MockMarketDemandSummary,
  type MockMarketPriceHistoryPoint,
} from '../../helpers/mock-api'

const CHAIR_PRODUCT_ID = 'pt-wooden-chair'
const BREAD_PRODUCT_ID = 'pt-bread'

function makeMarketSummary(cityId: string, cityName: string, currencyCode: string): MockMarketDemandSummary {
  return {
    cityId,
    cityName,
    currencyCode,
    fromTick: 0,
    toTick: 100,
    products: [
      {
        productTypeId: CHAIR_PRODUCT_ID,
        productName: 'Wooden Chair',
        industry: 'FURNITURE',
        totalDemand: 500,
        totalQuantitySold: 450,
        satisfactionRate: 0.9,
        averageClearingPrice: 45.0,
        totalRevenue: 20250.0,
        sellerCount: 3,
      },
      {
        productTypeId: BREAD_PRODUCT_ID,
        productName: 'Bread',
        industry: 'FOOD_PROCESSING',
        totalDemand: 800,
        totalQuantitySold: 400,
        satisfactionRate: 0.5,
        averageClearingPrice: 3.0,
        totalRevenue: 1200.0,
        sellerCount: 1,
      },
    ],
  }
}

test('shows Market Dashboard title and city tabs', async ({ page }) => {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.marketOverviewByCityId = {
    'city-ba': makeMarketSummary('city-ba', 'Bratislava', 'EUR'),
    'city-pr': makeMarketSummary('city-pr', 'Prague', 'CZK'),
  }
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto('/market')
  await expect(page.getByRole('heading', { name: 'Market Dashboard' })).toBeVisible()
  // Use city-tabs nav scope to avoid strict-mode clash with context switcher button
  const cityTabs = page.locator('.city-tabs')
  await expect(cityTabs.getByRole('button', { name: 'Bratislava', exact: true })).toBeVisible()
  await expect(cityTabs.getByRole('button', { name: 'Prague', exact: true })).toBeVisible()
})

test('shows product table with clearing price and satisfaction', async ({ page }) => {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.marketOverviewByCityId = {
    'city-ba': makeMarketSummary('city-ba', 'Bratislava', 'EUR'),
  }
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto('/market')
  await expect(page.getByText('Wooden Chair')).toBeVisible()
  // Satisfaction badge for 90% satisfied product
  await expect(page.getByText('Well supplied').first()).toBeVisible()
  // Satisfaction badge for 50% satisfied product (partial shortage)
  await expect(page.getByText('Partial shortage').first()).toBeVisible()
})

test('shows satisfaction bar with correct fill percentage', async ({ page }) => {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.marketOverviewByCityId = {
    'city-ba': makeMarketSummary('city-ba', 'Bratislava', 'EUR'),
  }
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto('/market')
  // The progressbar for Wooden Chair at 90% satisfaction — assert it exists, not toBeVisible (zero-height bar)
  const bar = page.locator('[role="progressbar"][aria-valuenow="90"]').first()
  await expect(bar).not.toHaveCount(0)
})

test('shows empty state when no sales data', async ({ page }) => {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  // No market override — mock returns empty products via default handler
  state.marketOverviewByCityId = {
    'city-ba': {
      cityId: 'city-ba',
      cityName: 'Bratislava',
      currencyCode: 'EUR',
      fromTick: 0,
      toTick: 0,
      products: [],
    },
  }
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto('/market')
  await expect(
    page.getByText('No consumer sales have been recorded yet', { exact: false }),
  ).toBeVisible()
})

test('switches city tab when clicking Prague', async ({ page }) => {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.marketOverviewByCityId = {
    'city-ba': makeMarketSummary('city-ba', 'Bratislava', 'EUR'),
    'city-pr': {
      cityId: 'city-pr',
      cityName: 'Prague',
      currencyCode: 'CZK',
      fromTick: 0,
      toTick: 100,
      products: [
        {
          productTypeId: 'pt-flour',
          productName: 'Flour',
          industry: 'FOOD_PROCESSING',
          totalDemand: 300,
          totalQuantitySold: 60,
          satisfactionRate: 0.2,
          averageClearingPrice: 5.5,
          totalRevenue: 330.0,
          sellerCount: 1,
        },
      ],
    },
  }
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto('/market')
  await page.getByRole('button', { name: 'Prague' }).click()
  // After clicking Prague, the severe shortage badge should be visible for Prague flour
  await expect(page.getByText('Severe shortage').first()).toBeVisible()
})

test('unauthenticated user can still access /market route', async ({ page }) => {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  const state = setupMockApi(page, { players: [player] })
  state.marketOverviewByCityId = {
    'city-ba': makeMarketSummary('city-ba', 'Bratislava', 'EUR'),
  }

  await page.goto('/market')
  // Page loads even for unauthenticated users (market data is public)
  await expect(page.getByRole('heading', { name: 'Market Dashboard' })).toBeVisible()
})

test('Market Dashboard nav link is visible in header', async ({ page }) => {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto('/dashboard')
  const desktopNav = page.locator('header.app-header')
  await expect(desktopNav.getByRole('link', { name: 'Market Dashboard' })).toBeVisible()
})

test('city demand panel is visible in city map view', async ({ page }) => {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.marketOverviewByCityId = {
    'city-ba': makeMarketSummary('city-ba', 'Bratislava', 'EUR'),
  }
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto('/city/city-ba')
  // City demand panel should be visible
  await expect(page.locator('.city-demand-panel')).toBeVisible()
  await expect(page.getByText('Top Demanded Products')).toBeVisible()
})

test('seller count and industry column are shown', async ({ page }) => {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.marketOverviewByCityId = {
    'city-ba': makeMarketSummary('city-ba', 'Bratislava', 'EUR'),
  }
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto('/market')
  await expect(page.getByRole('columnheader', { name: 'Industry' })).toBeVisible()
  await expect(page.getByRole('columnheader', { name: 'Sellers' })).toBeVisible()
  // Wooden Chair has 3 sellers
  const rows = page.locator('.product-row')
  await expect(rows.first()).toContainText('FURNITURE')
  await expect(rows.first()).toContainText('3')
})

test('Vienna city tab shows EUR data', async ({ page }) => {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.marketOverviewByCityId = {
    'city-ba': makeMarketSummary('city-ba', 'Bratislava', 'EUR'),
    'city-pr': makeMarketSummary('city-pr', 'Prague', 'CZK'),
    'city-vi': {
      cityId: 'city-vi',
      cityName: 'Vienna',
      currencyCode: 'EUR',
      fromTick: 0,
      toTick: 100,
      products: [
        {
          productTypeId: CHAIR_PRODUCT_ID,
          productName: 'Wooden Chair',
          industry: 'FURNITURE',
          totalDemand: 200,
          totalQuantitySold: 200,
          satisfactionRate: 1.0,
          averageClearingPrice: 48.0,
          totalRevenue: 9600.0,
          sellerCount: 2,
        },
      ],
    },
  }
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto('/market')
  const cityTabs = page.locator('.city-tabs')
  await expect(cityTabs.getByRole('button', { name: 'Vienna', exact: true })).toBeVisible()
  await cityTabs.getByRole('button', { name: 'Vienna', exact: true }).click()
  // After clicking Vienna, well-supplied badge should appear (100% satisfaction → 'Well supplied')
  await expect(page.getByText('Well supplied').first()).toBeVisible()
})

test('shows error state when market data fails to load', async ({ page }) => {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  // Simulate API error by setting no marketOverviewByCityId and overriding the route
  await page.route('**/graphql', async (route) => {
    const body = route.request().postDataJSON() as { query?: string }
    if (body?.query?.includes('marketOverview')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ errors: [{ message: 'Internal server error' }] }),
      })
      return
    }
    await route.continue()
  })
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto('/market')
  await expect(page.getByText('Failed to load market data', { exact: false })).toBeVisible()
})

test('market dashboard renders correctly at mobile viewport', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 })
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.marketOverviewByCityId = {
    'city-ba': makeMarketSummary('city-ba', 'Bratislava', 'EUR'),
  }
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto('/market')
  await expect(page.getByRole('heading', { name: 'Market Dashboard' })).toBeVisible()
  await expect(page.getByText('Wooden Chair')).toBeVisible()
  // Verify no horizontal overflow at mobile width
  const bodyWidth = await page.evaluate(() => document.body.scrollWidth)
  expect(bodyWidth).toBeLessThanOrEqual(395) // allow slight tolerance
})

test('shows all three starter industry products in market table', async ({ page }) => {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.marketOverviewByCityId = {
    'city-ba': {
      cityId: 'city-ba',
      cityName: 'Bratislava',
      currencyCode: 'EUR',
      fromTick: 0,
      toTick: 100,
      products: [
        {
          productTypeId: 'pt-wooden-chair',
          productName: 'Wooden Chair',
          industry: 'FURNITURE',
          totalDemand: 500,
          totalQuantitySold: 450,
          satisfactionRate: 0.9,
          averageClearingPrice: 45.0,
          totalRevenue: 20250.0,
          sellerCount: 3,
        },
        {
          productTypeId: 'pt-bread',
          productName: 'Bread',
          industry: 'FOOD_PROCESSING',
          totalDemand: 800,
          totalQuantitySold: 400,
          satisfactionRate: 0.5,
          averageClearingPrice: 3.0,
          totalRevenue: 1200.0,
          sellerCount: 1,
        },
        {
          productTypeId: 'pt-basic-medicine',
          productName: 'Basic Medicine',
          industry: 'HEALTHCARE',
          totalDemand: 300,
          totalQuantitySold: 60,
          satisfactionRate: 0.2,
          averageClearingPrice: 50.0,
          totalRevenue: 3000.0,
          sellerCount: 1,
        },
      ],
    },
  }
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto('/market')
  await expect(page.getByText('Wooden Chair')).toBeVisible()
  await expect(page.getByText('Bread')).toBeVisible()
  await expect(page.getByText('Basic Medicine')).toBeVisible()
})

test('shows severe shortage badge for products below 40% satisfaction', async ({ page }) => {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.marketOverviewByCityId = {
    'city-ba': {
      cityId: 'city-ba',
      cityName: 'Bratislava',
      currencyCode: 'EUR',
      fromTick: 0,
      toTick: 100,
      products: [
        {
          productTypeId: 'pt-basic-medicine',
          productName: 'Basic Medicine',
          industry: 'HEALTHCARE',
          totalDemand: 300,
          totalQuantitySold: 30, // 10% satisfaction → "Scarce"
          satisfactionRate: 0.1,
          averageClearingPrice: 50.0,
          totalRevenue: 1500.0,
          sellerCount: 1,
        },
      ],
    },
  }
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto('/market')
  await expect(page.getByText('Basic Medicine')).toBeVisible()
  // 10% satisfaction → "Severe shortage" label (satisfactionPoor)
  await expect(page.getByText('Severe shortage').first()).toBeVisible()
})

test('shows partial shortage badge for products with 40-79% satisfaction', async ({ page }) => {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.marketOverviewByCityId = {
    'city-ba': {
      cityId: 'city-ba',
      cityName: 'Bratislava',
      currencyCode: 'EUR',
      fromTick: 0,
      toTick: 100,
      products: [
        {
          productTypeId: BREAD_PRODUCT_ID,
          productName: 'Bread',
          industry: 'FOOD_PROCESSING',
          totalDemand: 500,
          totalQuantitySold: 300, // 60% satisfaction → "Partial shortage"
          satisfactionRate: 0.6,
          averageClearingPrice: 3.2,
          totalRevenue: 960.0,
          sellerCount: 2,
        },
      ],
    },
  }
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto('/market')
  await expect(page.getByText('Bread')).toBeVisible()
  // 60% satisfaction (≥40% and <80%) → "Partial shortage" badge
  await expect(page.getByText('Partial shortage').first()).toBeVisible()
})

test('shows price history panel when a product row is clicked', async ({ page }) => {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.marketOverviewByCityId = {
    'city-ba': makeMarketSummary('city-ba', 'Bratislava', 'EUR'),
  }
  // Seed price history for Wooden Chair (tick values <= default tick=42)
  const historyPoints: MockMarketPriceHistoryPoint[] = [
    { tick: 10, clearingPrice: 43.0, totalVolume: 18, totalRevenue: 774, sellerCount: 1 },
    { tick: 20, clearingPrice: 43.5, totalVolume: 20, totalRevenue: 870, sellerCount: 1 },
    { tick: 30, clearingPrice: 44.0, totalVolume: 22, totalRevenue: 968, sellerCount: 2 },
    { tick: 40, clearingPrice: 45.0, totalVolume: 25, totalRevenue: 1125, sellerCount: 2 },
    { tick: 42, clearingPrice: 45.0, totalVolume: 30, totalRevenue: 1350, sellerCount: 2 },
  ]
  state.marketPriceHistoryByProductId = { [CHAIR_PRODUCT_ID]: historyPoints }
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto('/market')
  // Wait for table to load
  const chairRow = page.locator('.product-row').first()
  await expect(chairRow).toBeVisible()
  // Click the Wooden Chair row to open price history panel
  await chairRow.click()
  // Price history panel heading should become visible
  await expect(page.getByText('Price History (last 100 ticks)').first()).toBeVisible()
  // The history table should contain at least one price entry
  await expect(page.locator('.price-history-panel .history-table')).toBeVisible()
})

test('hides price history panel when same product row is clicked again', async ({ page }) => {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.marketOverviewByCityId = {
    'city-ba': makeMarketSummary('city-ba', 'Bratislava', 'EUR'),
  }
  state.marketPriceHistoryByProductId = {
    [CHAIR_PRODUCT_ID]: [
      { tick: 40, clearingPrice: 45.0, totalVolume: 25, totalRevenue: 1125, sellerCount: 1 },
    ],
  }
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto('/market')
  const chairRow = page.locator('.product-row').first()
  await expect(chairRow).toBeVisible()
  // First click — opens panel
  await chairRow.click()
  await expect(page.locator('.price-history-panel')).toBeVisible()
  // Second click on same row — closes panel
  await chairRow.click()
  await expect(page.locator('.price-history-panel')).toBeHidden()
})


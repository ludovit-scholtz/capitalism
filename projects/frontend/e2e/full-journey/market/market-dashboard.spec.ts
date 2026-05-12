import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer, type MockMarketDemandSummary } from '../../helpers/mock-api'

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

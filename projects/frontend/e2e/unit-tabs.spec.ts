import { expect, test } from '@playwright/test'
import {
  makeChairProduct,
  makePlayer,
  setupMockApi,
  type MockPublicSalesAnalytics,
} from './helpers/mock-api'

function makeShopWithChair() {
  const chair = makeChairProduct()
  const player = makePlayer({
    onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
    companies: [
      {
        id: 'company-tabs',
        playerId: 'player-1',
        name: 'Tab Test Corp',
        cash: 500000,
        foundedAtUtc: '2026-01-01T00:00:00Z',
        buildings: [
          {
            id: 'building-tabs-shop',
            companyId: 'company-tabs',
            cityId: 'city-ba',
            type: 'SALES_SHOP',
            name: 'Tab Test Shop',
            latitude: 48.15,
            longitude: 17.11,
            level: 1,
            powerConsumption: 1,
            isForSale: false,
            builtAtUtc: '2026-01-01T00:00:00Z',
            pendingConfiguration: null,
            units: [
              {
                id: 'u-tabs-purchase',
                buildingId: 'building-tabs-shop',
                unitType: 'PURCHASE',
                gridX: 0,
                gridY: 0,
                level: 1,
                linkRight: true,
                linkLeft: false,
                linkUp: false,
                linkDown: false,
                linkUpLeft: false,
                linkUpRight: false,
                linkDownLeft: false,
                linkDownRight: false,
                productTypeId: chair.id,
              },
              {
                id: 'u-tabs-ps',
                buildingId: 'building-tabs-shop',
                unitType: 'PUBLIC_SALES',
                gridX: 1,
                gridY: 0,
                level: 1,
                linkRight: false,
                linkLeft: false,
                linkUp: false,
                linkDown: false,
                linkUpLeft: false,
                linkUpRight: false,
                linkDownLeft: false,
                linkDownRight: false,
                productTypeId: chair.id,
                minPrice: chair.basePrice * 1.5,
                saleVisibility: 'PUBLIC',
              },
            ],
          },
        ],
      },
    ],
  })
  return { player, chair }
}

test.describe('Unit detail tab navigation', () => {
  test('PUBLIC_SALES unit shows 6 tabs: Basic Info, Quick Actions, Inventory, History, Market, Activity', async ({
    page,
  }) => {
    const { player, chair } = makeShopWithChair()
    const state = setupMockApi(page, { players: [player], products: [chair] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/building/building-tabs-shop')
    await expect(page.getByRole('heading', { name: 'Tab Test Shop' })).toBeVisible()

    // Click the PUBLIC_SALES cell
    const activeSection = page
      .locator('.grid-section')
      .filter({ has: page.getByRole('heading', { name: 'Current Configuration' }) })
      .first()
    await activeSection.locator('.unit-row').nth(0).locator('.grid-cell').nth(1).click()

    // Tab nav should appear
    const tabs = page.locator('.unit-detail-tabs')
    await expect(tabs).toBeVisible()

    // All 6 tabs should be present
    await expect(tabs.getByRole('button', { name: 'Basic Info' })).toBeVisible()
    await expect(tabs.getByRole('button', { name: 'Quick Actions' })).toBeVisible()
    await expect(tabs.getByRole('button', { name: 'Inventory' })).toBeVisible()
    await expect(tabs.getByRole('button', { name: 'History' })).toBeVisible()
    await expect(tabs.getByRole('button', { name: 'Market' })).toBeVisible()
    await expect(tabs.getByRole('button', { name: 'Activity' })).toBeVisible()

    // Basic Info tab is active by default
    await expect(tabs.getByRole('button', { name: 'Basic Info' })).toHaveClass(/unit-tab-btn--active/)
  })

  test('PURCHASE unit shows 4 tabs without Quick Actions', async ({ page }) => {
    const { player, chair } = makeShopWithChair()
    const state = setupMockApi(page, { players: [player], products: [chair] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/building/building-tabs-shop')

    const activeSection = page
      .locator('.grid-section')
      .filter({ has: page.getByRole('heading', { name: 'Current Configuration' }) })
      .first()
    // Click the PURCHASE cell (gridX=0)
    await activeSection.locator('.unit-row').nth(0).locator('.grid-cell').nth(0).click()

    const tabs = page.locator('.unit-detail-tabs')
    await expect(tabs).toBeVisible()

    await expect(tabs.getByRole('button', { name: 'Basic Info' })).toBeVisible()
    // Quick Actions should NOT appear for PURCHASE units
    await expect(tabs.getByRole('button', { name: 'Quick Actions' })).toHaveCount(0)
    await expect(tabs.getByRole('button', { name: 'Inventory' })).toBeVisible()
    await expect(tabs.getByRole('button', { name: 'History' })).toBeVisible()
    await expect(tabs.getByRole('button', { name: 'Market' })).toBeVisible()
    await expect(tabs.getByRole('button', { name: 'Activity' })).toBeVisible()
  })

  test('switching tabs shows correct content — Quick Actions contains price input', async ({ page }) => {
    const { player, chair } = makeShopWithChair()
    const analytics: MockPublicSalesAnalytics = {
      buildingUnitId: 'u-tabs-ps',
      buildingId: 'building-tabs-shop',
      buildingName: 'Tab Test Shop',
      cityName: 'Bratislava',
      totalRevenue: 450,
      totalQuantitySold: 30,
      averagePricePerUnit: chair.basePrice * 1.5,
      currentSalesCapacity: 80,
      dataFromTick: 1,
      dataToTick: 5,
      demandSignal: 'STRONG',
      actionHint: 'Demand is strong.',
      recentUtilization: 0.8,
      revenueHistory: Array.from({ length: 5 }, (_, i) => ({ tick: i + 1, revenue: 90, quantitySold: 6 })),
      priceHistory: Array.from({ length: 5 }, (_, i) => ({ tick: i + 1, pricePerUnit: chair.basePrice * 1.5 })),
      marketShare: [{ label: 'Tab Test Corp', companyId: 'company-tabs', share: 1.0, isUnmet: false }],
      elasticityIndex: -1.0,
      unmetDemandShare: 0,
      populationIndex: 1.0,
      inventoryQuality: 0.7,
      brandAwareness: null,
      totalProfit: null,
      profitHistory: null,
      demandDrivers: [],
    }
    const state = setupMockApi(page, { players: [player], products: [chair] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.publicSalesAnalytics['u-tabs-ps'] = analytics
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/building/building-tabs-shop')

    const activeSection = page
      .locator('.grid-section')
      .filter({ has: page.getByRole('heading', { name: 'Current Configuration' }) })
      .first()
    await activeSection.locator('.unit-row').nth(0).locator('.grid-cell').nth(1).click()

    const tabs = page.locator('.unit-detail-tabs')
    await expect(tabs).toBeVisible()

    // Switch to Quick Actions
    await tabs.getByRole('button', { name: 'Quick Actions' }).click()
    await expect(tabs.getByRole('button', { name: 'Quick Actions' })).toHaveClass(/unit-tab-btn--active/)

    // Quick price input is visible
    const priceInput = page.locator('#quick-price-input')
    await expect(priceInput).toBeVisible()

    // Update the price
    await priceInput.fill('60.00')
    await page.getByRole('button', { name: 'Apply Price' }).click()
    await expect(page.locator('.mi-price-success')).toBeVisible()
    await expect(page.locator('.mi-price-success')).toContainText('Price updated')

    // Switch to Market tab — analytics should render
    await tabs.getByRole('button', { name: 'Market' }).click()
    await expect(tabs.getByRole('button', { name: 'Market' })).toHaveClass(/unit-tab-btn--active/)
    const miPanel = page.locator('[aria-label="Market Intelligence"]')
    await expect(miPanel).toBeVisible()
    await expect(miPanel.locator('.mi-demand-badge')).toContainText('Strong')

    // Switch to Inventory tab
    await tabs.getByRole('button', { name: 'Inventory' }).click()
    await expect(tabs.getByRole('button', { name: 'Inventory' })).toHaveClass(/unit-tab-btn--active/)

    // Switch to Activity tab
    await tabs.getByRole('button', { name: 'Activity' }).click()
    await expect(tabs.getByRole('button', { name: 'Activity' })).toHaveClass(/unit-tab-btn--active/)
    await expect(page.locator('[aria-label="Recent Activity"]')).toBeVisible()
  })

  test('tab state resets to Basic Info when selecting a different unit', async ({ page }) => {
    const { player, chair } = makeShopWithChair()
    const state = setupMockApi(page, { players: [player], products: [chair] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/building/building-tabs-shop')

    const activeSection = page
      .locator('.grid-section')
      .filter({ has: page.getByRole('heading', { name: 'Current Configuration' }) })
      .first()

    // Click PUBLIC_SALES and switch to Quick Actions tab
    await activeSection.locator('.unit-row').nth(0).locator('.grid-cell').nth(1).click()
    const tabs = page.locator('.unit-detail-tabs')
    await tabs.getByRole('button', { name: 'Quick Actions' }).click()
    await expect(tabs.getByRole('button', { name: 'Quick Actions' })).toHaveClass(/unit-tab-btn--active/)

    // Now click a different unit (PURCHASE)
    await activeSection.locator('.unit-row').nth(0).locator('.grid-cell').nth(0).click()

    // Tab nav re-renders for PURCHASE; Basic Info should be active
    await expect(tabs.getByRole('button', { name: 'Basic Info' })).toHaveClass(/unit-tab-btn--active/)
    // Quick Actions should not be present for PURCHASE
    await expect(tabs.getByRole('button', { name: 'Quick Actions' })).toHaveCount(0)
  })

  test('screenshot: PUBLIC_SALES Quick Actions tab', async ({ page }) => {
    const { player, chair } = makeShopWithChair()
    const analytics: MockPublicSalesAnalytics = {
      buildingUnitId: 'u-tabs-ps',
      buildingId: 'building-tabs-shop',
      buildingName: 'Tab Test Shop',
      cityName: 'Bratislava',
      totalRevenue: 900,
      totalQuantitySold: 60,
      averagePricePerUnit: chair.basePrice * 1.5,
      currentSalesCapacity: 100,
      dataFromTick: 1,
      dataToTick: 10,
      demandSignal: 'STRONG',
      actionHint: 'Demand is strong. Consider a slightly higher price.',
      recentUtilization: 0.85,
      revenueHistory: Array.from({ length: 10 }, (_, i) => ({ tick: i + 1, revenue: 90, quantitySold: 6 })),
      priceHistory: Array.from({ length: 10 }, (_, i) => ({
        tick: i + 1,
        pricePerUnit: chair.basePrice * 1.5,
      })),
      marketShare: [{ label: 'Tab Test Corp', companyId: 'company-tabs', share: 1.0, isUnmet: false }],
      elasticityIndex: -1.2,
      unmetDemandShare: 0.1,
      populationIndex: 1.1,
      inventoryQuality: 0.75,
      brandAwareness: null,
      totalProfit: null,
      profitHistory: null,
      demandDrivers: [],
    }
    const state = setupMockApi(page, { players: [player], products: [chair] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.publicSalesAnalytics['u-tabs-ps'] = analytics
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/building/building-tabs-shop')

    const activeSection = page
      .locator('.grid-section')
      .filter({ has: page.getByRole('heading', { name: 'Current Configuration' }) })
      .first()
    await activeSection.locator('.unit-row').nth(0).locator('.grid-cell').nth(1).click()

    const tabs = page.locator('.unit-detail-tabs')
    await expect(tabs).toBeVisible()

    await tabs.getByRole('button', { name: 'Quick Actions' }).click()
    await expect(page.locator('#quick-price-input')).toBeVisible()

    await page.screenshot({ path: '/tmp/unit-tabs-quick-actions.png' })
    await tabs.getByRole('button', { name: 'Market' }).click()
    await expect(page.locator('[aria-label="Market Intelligence"]')).toBeVisible()
    await page.screenshot({ path: '/tmp/unit-tabs-market.png' })
  })
})

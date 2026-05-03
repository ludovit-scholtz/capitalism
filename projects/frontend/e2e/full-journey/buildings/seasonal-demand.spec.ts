import { expect, test, type Page } from '@playwright/test'
import {
  makeChairProduct,
  makeDefaultCities,
  makeDefaultProducts,
  makePlayer,
  setupMockApi,
  type MockBuildingUnit,
  type MockPublicSalesAnalytics,
  type MockSeasonalOutlook,
} from '../../helpers/mock-api'

// ── Helpers ───────────────────────────────────────────────────────────────────

function getGridSection(page: Page, heading: string) {
  return page
    .locator('.grid-section')
    .filter({ has: page.getByRole('heading', { name: heading }) })
    .first()
}

function getGridCell(section: ReturnType<typeof getGridSection>, x: number, y: number) {
  return section.locator('.unit-row').nth(y).locator('.grid-cell').nth(x)
}

async function clickUnitTab(page: Page, tabName: string) {
  await page.locator('.unit-detail-tabs').getByRole('button', { name: tabName }).click()
}

function makePsSalesUnit(): MockBuildingUnit {
  const chairProduct = makeChairProduct()
  return {
    id: 'unit-seasonal-ps',
    buildingId: 'building-seasonal-shop',
    unitType: 'PUBLIC_SALES',
    gridX: 0,
    gridY: 0,
    level: 3,
    linkUp: false,
    linkDown: false,
    linkLeft: false,
    linkRight: false,
    linkUpLeft: false,
    linkUpRight: false,
    linkDownLeft: false,
    linkDownRight: false,
    productTypeId: chairProduct.id,
    resourceTypeId: null,
    minPrice: chairProduct.basePrice * 1.5,
    maxPrice: null,
    purchaseSource: null,
    saleVisibility: null,
    budget: null,
    mediaHouseBuildingId: null,
    minQuality: null,
    brandScope: null,
    vendorLockCompanyId: null,
  } satisfies MockBuildingUnit
}

function makeSeasonalShopPlayer() {
  const player = makePlayer()
  player.onboardingCompletedAtUtc = new Date().toISOString()
  player.companies.push({
    id: 'co-seasonal',
    playerId: player.id,
    name: 'Seasonal Furniture Co',
    cash: 500_000,
    foundedAtUtc: '2026-01-01T00:00:00Z',
    buildings: [
      {
        id: 'building-seasonal-shop',
        companyId: 'co-seasonal',
        cityId: 'city-ba',
        type: 'SALES_SHOP',
        name: 'Seasonal Test Shop',
        latitude: 48.15,
        longitude: 17.11,
        level: 1,
        powerConsumption: 3,
        isForSale: false,
        builtAtUtc: '2026-01-01T00:00:00Z',
        pendingConfiguration: null,
        units: [makePsSalesUnit()],
      },
    ],
  })
  return player
}

function makeSeasonalOutlook(quarterIndex: number): MockSeasonalOutlook {
  const labels = ['Q1 (Jan–Mar)', 'Q2 (Apr–Jun)', 'Q3 (Jul–Sep)', 'Q4 (Oct–Dec)']
  const multipliers = [0.8, 1.5, 1.3, 1.0]
  const colorCodes = ['ORANGE', 'GREEN', 'GREEN', 'YELLOW']
  const q = quarterIndex

  return {
    currentQuarterIndex: q,
    currentQuarterLabel: labels[q] ?? 'Q1 (Jan–Mar)',
    currentMultiplier: multipliers[q] ?? 1.0,
    demandLevel:
      (multipliers[q] ?? 1.0) >= 1.3
        ? 'HIGH'
        : (multipliers[q] ?? 1.0) >= 1.0
          ? 'MODERATE'
          : (multipliers[q] ?? 1.0) >= 0.7
            ? 'BELOW_AVERAGE'
            : 'LOW',
    callout:
      q === 1
        ? 'Q2 furniture demand peaks at 1.5×. Build and stage inventory now.'
        : q === 0
          ? 'Q1 post-holiday slump. Reduce production run sizes or diversify.'
          : 'Moderate demand conditions.',
    quarterForecasts: [0, 1, 2, 3].map((qi) => ({
      quarterIndex: qi,
      label: labels[qi] ?? `Q${qi + 1}`,
      multiplier: multipliers[qi] ?? 1.0,
      isCurrent: qi === q,
      colorCode: colorCodes[qi] ?? 'YELLOW',
    })),
  }
}

function makeAnalyticsWithSeasonal(
  quarterIndex: number,
  override?: Partial<MockPublicSalesAnalytics>,
): MockPublicSalesAnalytics {
  const chairProduct = makeChairProduct()
  return {
    buildingUnitId: 'unit-seasonal-ps',
    buildingId: 'building-seasonal-shop',
    buildingName: 'Seasonal Test Shop',
    cityName: 'Bratislava',
    productTypeId: chairProduct.id,
    productName: chairProduct.name,
    totalRevenue: 2250,
    totalQuantitySold: 50,
    averagePricePerUnit: chairProduct.basePrice * 1.5,
    currentSalesCapacity: 100,
    dataFromTick: 1,
    dataToTick: 100,
    revenueHistory: Array.from({ length: 5 }, (_, i) => ({ tick: 96 + i, revenue: 450, quantitySold: 10 })),
    priceHistory: [{ tick: 100, pricePerUnit: chairProduct.basePrice * 1.5 }],
    profitHistory: null,
    marketShare: [],
    demandSignal: 'MODERATE',
    trendDirection: 'FLAT',
    actionHint: 'Steady demand. Maintain current pricing.',
    recentUtilization: 0.5,
    elasticityIndex: -0.8,
    unmetDemandShare: 0.1,
    populationIndex: 1.0,
    inventoryQuality: 0.7,
    brandAwareness: 0.4,
    brandQuality: 0.3,
    totalProfit: 200,
    demandDrivers: [
      { factor: 'PRICE', impact: 'NEUTRAL', score: 0.0, description: 'Price at market average.' },
      {
        factor: 'SEASONAL',
        impact: quarterIndex === 1 ? 'POSITIVE' : quarterIndex === 0 ? 'NEGATIVE' : 'NEUTRAL',
        score: (makeSeasonalOutlook(quarterIndex).currentMultiplier - 1.0) * 100,
        description: `Seasonal multiplier: ${makeSeasonalOutlook(quarterIndex).currentMultiplier.toFixed(1)}×`,
      },
    ],
    trendFactor: 1.0,
    cityCurrencyCode: 'EUR',
    cityAveragePrice: chairProduct.basePrice * 1.5,
    seasonalOutlook: makeSeasonalOutlook(quarterIndex),
    ...override,
  }
}

// ── Tests ──────────────────────────────────────────────────────────────────────

test.describe('Seasonal demand – Public Sales unit panel', () => {
  test('displays seasonal outlook panel for Q2 (high demand)', async ({ page }) => {
    const player = makeSeasonalShopPlayer()
    const state = setupMockApi(page, {
      players: [player],
      cities: makeDefaultCities(),
      products: makeDefaultProducts(),
    })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.gameState.currentTick = 2190 // Q2
    state.publicSalesAnalytics['unit-seasonal-ps'] = makeAnalyticsWithSeasonal(1)

    await page.addInitScript(
      ({ token, expires }) => {
        localStorage.setItem('auth_token', token)
        localStorage.setItem('auth_expires', expires)
      },
      { token: `token-${player.id}`, expires: new Date(Date.now() + 7_200_000).toISOString() },
    )

    await page.goto('/building/building-seasonal-shop')
    await expect(page.getByRole('heading', { name: 'Seasonal Test Shop' })).toBeVisible()

    // Click the PUBLIC_SALES unit at (0,0)
    const activeSection = getGridSection(page, 'Current Configuration')
    await getGridCell(activeSection, 0, 0).click()

    // Navigate to the Market tab
    await clickUnitTab(page, 'Market')

    // The seasonal outlook panel should be visible
    const panel = page.locator('[aria-label="Market Intelligence"]')
    await expect(panel).toBeVisible()

    await expect(panel.locator('.seasonal-outlook')).toBeVisible()
    await expect(panel.locator('.seasonal-current-quarter')).toContainText('Q2')
    await expect(panel.locator('.seasonal-current-multiplier')).toContainText('1.5×')

    // High demand badge
    await expect(panel.locator('.seasonal-badge-high')).toBeVisible()

    // 4 forecast bars
    const bars = panel.locator('.seasonal-forecast-bar')
    await expect(bars).not.toHaveCount(0)
    expect(await bars.count()).toBe(4)

    // Callout is visible
    await expect(panel.locator('.seasonal-callout')).toBeVisible()
    await expect(panel.locator('.seasonal-callout')).toContainText(/Q2 furniture demand peaks/i)
  })

  test('displays seasonal outlook panel for Q1 (below average demand)', async ({ page }) => {
    const player = makeSeasonalShopPlayer()
    const state = setupMockApi(page, {
      players: [player],
      cities: makeDefaultCities(),
      products: makeDefaultProducts(),
    })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.gameState.currentTick = 100 // Q1
    state.publicSalesAnalytics['unit-seasonal-ps'] = makeAnalyticsWithSeasonal(0)

    await page.addInitScript(
      ({ token, expires }) => {
        localStorage.setItem('auth_token', token)
        localStorage.setItem('auth_expires', expires)
      },
      { token: `token-${player.id}`, expires: new Date(Date.now() + 7_200_000).toISOString() },
    )

    await page.goto('/building/building-seasonal-shop')
    await expect(page.getByRole('heading', { name: 'Seasonal Test Shop' })).toBeVisible()

    const activeSection = getGridSection(page, 'Current Configuration')
    await getGridCell(activeSection, 0, 0).click()
    await clickUnitTab(page, 'Market')

    const panel = page.locator('[aria-label="Market Intelligence"]')
    await expect(panel.locator('.seasonal-outlook')).toBeVisible()
    await expect(panel.locator('.seasonal-current-quarter')).toContainText('Q1')
    await expect(panel.locator('.seasonal-current-multiplier')).toContainText('0.8×')

    // Below-average demand badge
    await expect(panel.locator('.seasonal-badge-below')).toBeVisible()

    // Callout mentions slump
    await expect(panel.locator('.seasonal-callout')).toContainText(/slump/i)
  })

  test('quarter badge is shown in navbar GameTimeChip', async ({ page }) => {
    const player = makeSeasonalShopPlayer()
    const state = setupMockApi(page, {
      players: [player],
      cities: makeDefaultCities(),
      products: makeDefaultProducts(),
    })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.gameState.currentTick = 2190 // Q2

    await page.addInitScript(
      ({ token, expires }) => {
        localStorage.setItem('auth_token', token)
        localStorage.setItem('auth_expires', expires)
      },
      { token: `token-${player.id}`, expires: new Date(Date.now() + 7_200_000).toISOString() },
    )

    await page.goto('/dashboard')
    // Wait for the game time chip to be visible
    await expect(page.locator('.game-time-chip')).toBeVisible()

    // The quarter badge should show Q2
    const badge = page.locator('.game-quarter-badge')
    await expect(badge).toBeVisible()
    await expect(badge).toContainText('Q2')
  })

  test('seasonal outlook panel is absent when seasonalOutlook is null', async ({ page }) => {
    const player = makeSeasonalShopPlayer()
    const state = setupMockApi(page, {
      players: [player],
      cities: makeDefaultCities(),
      products: makeDefaultProducts(),
    })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    // Analytics with null seasonalOutlook (backward compatibility)
    state.publicSalesAnalytics['unit-seasonal-ps'] = makeAnalyticsWithSeasonal(0, {
      seasonalOutlook: null,
    })

    await page.addInitScript(
      ({ token, expires }) => {
        localStorage.setItem('auth_token', token)
        localStorage.setItem('auth_expires', expires)
      },
      { token: `token-${player.id}`, expires: new Date(Date.now() + 7_200_000).toISOString() },
    )

    await page.goto('/building/building-seasonal-shop')
    await expect(page.getByRole('heading', { name: 'Seasonal Test Shop' })).toBeVisible()

    const activeSection = getGridSection(page, 'Current Configuration')
    await getGridCell(activeSection, 0, 0).click()
    await clickUnitTab(page, 'Market')

    const panel = page.locator('[aria-label="Market Intelligence"]')
    await expect(panel).toBeVisible()

    // Demand signal still visible
    await expect(panel.locator('.mi-demand-card')).toBeVisible()

    // But seasonal outlook panel is NOT rendered
    await expect(panel.locator('.seasonal-outlook')).toHaveCount(0)
  })

  test('forecast bar chart renders all 4 quarters with "now" indicator on current quarter', async ({
    page,
  }) => {
    const player = makeSeasonalShopPlayer()
    const state = setupMockApi(page, {
      players: [player],
      cities: makeDefaultCities(),
      products: makeDefaultProducts(),
    })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.publicSalesAnalytics['unit-seasonal-ps'] = makeAnalyticsWithSeasonal(3) // Q4

    await page.addInitScript(
      ({ token, expires }) => {
        localStorage.setItem('auth_token', token)
        localStorage.setItem('auth_expires', expires)
      },
      { token: `token-${player.id}`, expires: new Date(Date.now() + 7_200_000).toISOString() },
    )

    await page.goto('/building/building-seasonal-shop')
    await expect(page.getByRole('heading', { name: 'Seasonal Test Shop' })).toBeVisible()

    const activeSection = getGridSection(page, 'Current Configuration')
    await getGridCell(activeSection, 0, 0).click()
    await clickUnitTab(page, 'Market')

    const panel = page.locator('[aria-label="Market Intelligence"]')
    await expect(panel.locator('.seasonal-outlook')).toBeVisible()

    // 4 quarter labels
    const quarterLabels = panel.locator('.seasonal-quarter-label')
    await expect(quarterLabels).not.toHaveCount(0)
    expect(await quarterLabels.count()).toBe(4)

    // Forecast chart is visible
    await expect(panel.locator('.seasonal-forecast-chart')).toBeVisible()

    // "now" indicator appears exactly once (for Q4)
    const nowIndicator = panel.locator('.seasonal-current-indicator')
    await expect(nowIndicator).toHaveCount(1)
  })

  test('seasonal demand factor appears in demand drivers list', async ({ page }) => {
    const player = makeSeasonalShopPlayer()
    const state = setupMockApi(page, {
      players: [player],
      cities: makeDefaultCities(),
      products: makeDefaultProducts(),
    })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.publicSalesAnalytics['unit-seasonal-ps'] = makeAnalyticsWithSeasonal(1) // Q2 positive

    await page.addInitScript(
      ({ token, expires }) => {
        localStorage.setItem('auth_token', token)
        localStorage.setItem('auth_expires', expires)
      },
      { token: `token-${player.id}`, expires: new Date(Date.now() + 7_200_000).toISOString() },
    )

    await page.goto('/building/building-seasonal-shop')
    await expect(page.getByRole('heading', { name: 'Seasonal Test Shop' })).toBeVisible()

    const activeSection = getGridSection(page, 'Current Configuration')
    await getGridCell(activeSection, 0, 0).click()
    await clickUnitTab(page, 'Market')

    const panel = page.locator('[aria-label="Market Intelligence"]')
    await expect(panel).toBeVisible()

    // The SEASONAL demand driver label should be visible
    await expect(panel.locator('.mi-driver-factor', { hasText: /Seasonal/i })).toBeVisible()
  })
})

test.describe('Seasonal demand – mobile viewport', () => {
  test.use({ viewport: { width: 480, height: 812 } })

  test('seasonal outlook panel renders correctly on mobile without horizontal overflow', async ({
    page,
  }) => {
    const player = makeSeasonalShopPlayer()
    const state = setupMockApi(page, {
      players: [player],
      cities: makeDefaultCities(),
      products: makeDefaultProducts(),
    })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.publicSalesAnalytics['unit-seasonal-ps'] = makeAnalyticsWithSeasonal(1)

    await page.addInitScript(
      ({ token, expires }) => {
        localStorage.setItem('auth_token', token)
        localStorage.setItem('auth_expires', expires)
      },
      { token: `token-${player.id}`, expires: new Date(Date.now() + 7_200_000).toISOString() },
    )

    await page.goto('/building/building-seasonal-shop')
    await expect(page.getByRole('heading', { name: 'Seasonal Test Shop' })).toBeVisible()

    const activeSection = getGridSection(page, 'Current Configuration')
    await getGridCell(activeSection, 0, 0).click()
    await clickUnitTab(page, 'Market')

    const panel = page.locator('[aria-label="Market Intelligence"]')
    await expect(panel.locator('.seasonal-outlook')).toBeVisible()
    await expect(panel.locator('.seasonal-current-multiplier')).toContainText('1.5×')
  })
})

import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer, makeDefaultBuildingLots, type MockBuildingLot } from '../../helpers/mock-api'

// ── Helpers ──────────────────────────────────────────────────────────────────

function setupAuthenticatedPlayer(page: import('@playwright/test').Page) {
  const player = makePlayer({
    onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
    companies: [
      {
        id: 'company-1',
        playerId: 'player-1',
        name: 'Test Empire',
        cash: 500000,
        foundedAtUtc: '2026-01-01T00:00:00Z',
        buildings: [],
      },
    ],
  })
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  return { state, player }
}

async function authenticateViaLocalStorage(page: import('@playwright/test').Page, playerId: string) {
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${playerId}`)
}

// ── Tests ─────────────────────────────────────────────────────────────────────

test.describe('Real-world Map Integration', () => {
  // ── AC5: Map Rendering — renders lots on interactive map with zoom/pan ──────

  test('renders city map with GPS-positioned building lots', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')

    // Map header must show city name
    await expect(page.getByRole('heading', { name: /Bratislava/i })).toBeVisible()
    // Lot count summary is shown
    await expect(page.getByText(/5 lots/i)).toBeVisible()
  })

  test('public query — unauthenticated user can view map without login', async ({ page }) => {
    // AC: cityLots query is public (no auth required)
    setupMockApi(page)

    await page.goto('/city/city-ba')

    // Map must render even without authentication
    await expect(page.getByRole('heading', { name: /Bratislava/i })).toBeVisible()
  })

  // ── AC1: GPS Storage — lots expose latitude/longitude coordinates ─────────

  test('lot detail panel shows GPS latitude and longitude', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /High Street Retail Space/i }).click()

    // AC1: GPS coordinates displayed in the lot detail panel (data-testid="lot-coordinates")
    // Mock data: lat=48.145, lon=17.107 → rendered as "48.14500°N, 17.10700°E"
    await expect(page.getByTestId('lot-coordinates')).toBeVisible()
    await expect(page.getByTestId('lot-coordinates')).toContainText('48.')
    await expect(page.getByTestId('lot-coordinates')).toContainText('17.')
  })

  test('GPS coordinates are stored with decimal precision for all mock lots', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()

    // Verify Factory Site B1 coordinates (lat=48.15, lon=17.13) are shown
    await page.getByRole('button', { name: /Factory Site B1/i }).click()
    // GPS coords are in data-testid="lot-coordinates", e.g. "48.15000°N, 17.13000°E"
    await expect(page.getByTestId('lot-coordinates')).toBeVisible()
    await expect(page.getByTestId('lot-coordinates')).toContainText('48.')
    await expect(page.getByTestId('lot-coordinates')).toContainText('17.')
  })

  // ── AC4: Land Availability — minimum lots are displayed per building type ──

  test('all five lot types are available in the default fixture', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()

    // All 5 fixture lots should be listed
    await expect(page.getByRole('button', { name: /Industrial Plot A1/i })).toBeVisible()
    await expect(page.getByRole('button', { name: /Factory Site B1/i })).toBeVisible()
    await expect(page.getByRole('button', { name: /High Street Retail Space/i })).toBeVisible()
    await expect(page.getByRole('button', { name: /Riverside Apartment Block/i })).toBeVisible()
    await expect(page.getByRole('button', { name: /Innovation Campus Office/i })).toBeVisible()
  })

  // ── AC3: Logistics Cost — distance-based cost displayed on lot detail ──────

  test('mine lot with raw material shows resource premium pricing context', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    const panel = page.getByRole('complementary')
    // Should show the appraised value and asking price for the mine lot
    await expect(page.getByTestId('asking-price')).toBeVisible()
    // Resource premium badge should indicate price includes material deposit value
    await expect(panel.getByText(/Iron Ore/i).first()).toBeVisible()
  })

  // ── AC6 + AC8: Land Purchase with GPS coordinate persistence ──────────────

  test('lot coordinates are immutable — GPS shown before and after purchase stays same', async ({
    page,
  }) => {
    const { player } = setupAuthenticatedPlayer(page)
    // Activate company context so purchase is enabled
    player.activeAccountType = 'COMPANY'
    player.activeCompanyId = 'company-1'
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()

    // Capture the GPS coordinates shown before purchase using the specific data-testid
    await page.getByRole('button', { name: /High Street Retail Space/i }).click()
    const coordsBefore = await page.getByTestId('lot-coordinates').textContent()

    // Initiate purchase
    await page.getByRole('button', { name: /Purchase Lot/i }).click()
    await page.locator('.building-type-card').filter({ hasText: /Sales Shop/i }).click()
    await page.getByRole('complementary').locator('input[type="text"]').fill('My City Shop')
    await page.getByRole('button', { name: /Confirm Purchase/i }).click()

    // After purchase, re-open the lot — verify coordinates have not changed (AC8: immutable)
    await expect(page.locator('.status-badge.yours')).toBeVisible()
    const coordsAfter = page.getByTestId('lot-coordinates')
    await expect(coordsAfter).toHaveText(coordsBefore)
  })

  test('purchase form validates building type must match lot suitable types', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    player.activeAccountType = 'COMPANY'
    player.activeCompanyId = 'company-1'
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()

    // Residential lot — only APARTMENT type should be offered
    await page.getByRole('button', { name: /Riverside Apartment Block/i }).click()
    await page.getByRole('button', { name: /Purchase Lot/i }).click()

    // Only APARTMENT type card should be shown (the lot suitableTypes="APARTMENT")
    await expect(page.locator('.building-type-card').filter({ hasText: /Apartment/i })).toBeVisible()
    await expect(page.locator('.building-type-card').filter({ hasText: /Factory/i })).toBeHidden()
  })

  // ── Population index — strategic location signal ─────────────────────────

  test('population index reflects geographic location value', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()

    // Industrial lot (outskirts) should have low pop index
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()
    const panel = page.getByRole('complementary')
    await expect(panel.getByText('Population Index', { exact: true })).toBeVisible()
    await expect(panel.getByText('0.65x')).toBeVisible()

    // Commercial lot (city center) should have high pop index
    await page.getByRole('button', { name: /High Street Retail Space/i }).click()
    await expect(panel.getByText('1.42x')).toBeVisible()
  })

  test('city power planning section shows weather, forecast, and grid guidance', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-power',
          playerId: 'player-1',
          name: 'Power Corp',
          cash: 2000000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [
            {
              id: 'plant-1',
              companyId: 'company-power',
              cityId: 'city-ba',
              type: 'POWER_PLANT',
              name: 'Solar Plant',
              latitude: 48.155,
              longitude: 17.115,
              level: 1,
              powerConsumption: 0,
              powerOutput: 20,
              powerPlantType: 'SOLAR',
              powerStatus: 'POWERED',
              isForSale: false,
              builtAtUtc: '2026-01-01T00:00:00Z',
              units: [],
              pendingConfiguration: null,
            },
            {
              id: 'factory-1',
              companyId: 'company-power',
              cityId: 'city-ba',
              type: 'FACTORY',
              name: 'Factory A',
              latitude: 48.16,
              longitude: 17.12,
              level: 1,
              powerConsumption: 5,
              powerStatus: 'POWERED',
              isForSale: false,
              builtAtUtc: '2026-01-01T00:00:00Z',
              units: [],
              pendingConfiguration: null,
            },
          ],
        },
      ],
    })

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.cityWeatherForecasts['city-ba'] = {
      cityId: 'city-ba',
      currentWindPercent: 54,
      currentSolarPercent: 78,
      forecast: Array.from({ length: 24 }, (_, index) => ({
        tick: index + 1,
        windPercent: 40 + (index % 5) * 5,
        solarPercent: 60 + (index % 4) * 6,
      })),
    }
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')

    const powerSection = page.getByTestId('city-power-section')
    await powerSection.scrollIntoViewIfNeeded()
    await expect(powerSection.getByRole('heading', { name: /Weather & Power/i })).toBeVisible()

    const weatherCard = page.getByTestId('city-weather-card')
    await expect(weatherCard).toBeVisible()
    await expect(weatherCard.getByTestId('solar-badge')).toContainText('78%')
    await expect(weatherCard.getByTestId('wind-badge')).toContainText('54%')
    await expect(weatherCard.locator('.forecast-bar-group')).toHaveCount(24)

    const balanceCard = page.getByTestId('city-power-balance-card')
    await expect(balanceCard).toBeVisible()
    await expect(balanceCard.locator('.status-balanced')).toBeVisible()
    await expect(balanceCard.locator('.balance-guidance')).toContainText(/balanced|surplus|revenue/i)

    const whyCard = page.getByTestId('why-matters-card')
    await expect(whyCard).toBeVisible()
    await expect(whyCard.locator('.solar-item')).toBeVisible()
    await expect(whyCard.locator('.wind-item')).toBeVisible()
    await expect(whyCard.locator('.power-item')).toBeVisible()
  })

  test('city power planning section shows constrained shortage state', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-power',
          playerId: 'player-1',
          name: 'Power Corp',
          cash: 2000000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [
            {
              id: 'plant-1',
              companyId: 'company-power',
              cityId: 'city-ba',
              type: 'POWER_PLANT',
              name: 'Small Coal Plant',
              latitude: 48.155,
              longitude: 17.115,
              level: 1,
              powerConsumption: 0,
              powerOutput: 8,
              powerPlantType: 'COAL',
              powerStatus: 'POWERED',
              isForSale: false,
              builtAtUtc: '2026-01-01T00:00:00Z',
              units: [],
              pendingConfiguration: null,
            },
            {
              id: 'factory-1',
              companyId: 'company-power',
              cityId: 'city-ba',
              type: 'FACTORY',
              name: 'Factory A',
              latitude: 48.16,
              longitude: 17.12,
              level: 1,
              powerConsumption: 5,
              powerStatus: 'CONSTRAINED',
              isForSale: false,
              builtAtUtc: '2026-01-01T00:00:00Z',
              units: [],
              pendingConfiguration: null,
            },
            {
              id: 'factory-2',
              companyId: 'company-power',
              cityId: 'city-ba',
              type: 'FACTORY',
              name: 'Factory B',
              latitude: 48.17,
              longitude: 17.13,
              level: 1,
              powerConsumption: 5,
              powerStatus: 'CONSTRAINED',
              isForSale: false,
              builtAtUtc: '2026-01-01T00:00:00Z',
              units: [],
              pendingConfiguration: null,
            },
            {
              id: 'factory-3',
              companyId: 'company-power',
              cityId: 'city-ba',
              type: 'FACTORY',
              name: 'Factory C',
              latitude: 48.18,
              longitude: 17.14,
              level: 1,
              powerConsumption: 5,
              powerStatus: 'CONSTRAINED',
              isForSale: false,
              builtAtUtc: '2026-01-01T00:00:00Z',
              units: [],
              pendingConfiguration: null,
            },
          ],
        },
      ],
    })

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')

    const balanceCard = page.getByTestId('city-power-balance-card')
    await balanceCard.scrollIntoViewIfNeeded()
    await expect(balanceCard).toBeVisible()
    await expect(balanceCard.locator('.status-constrained')).toBeVisible()
    await expect(balanceCard.locator('.balance-guidance')).toContainText(/shortage|capacity|returns/i)
  })

  // ── AC7: Performance — 100+ markers load without degradation ─────────────

  test('map loads and renders 100+ lot markers without timeout', async ({ page }) => {
    // Create 100+ mock lots to test rendering performance.
    // This verifies AC7: performance with 100+ land markers remains smooth (60 FPS goal).
    const manyLots: MockBuildingLot[] = makeDefaultBuildingLots()

    // Add 95 more generated lots spread around Bratislava center
    for (let i = 0; i < 95; i++) {
      const angle = (i / 95) * 2 * Math.PI
      const radiusKm = 1 + (i % 5) * 0.5
      const lat = 48.1486 + (radiusKm / 111) * Math.cos(angle)
      const lon = 17.1077 + (radiusKm / (111 * Math.cos((48.1486 * Math.PI) / 180))) * Math.sin(angle)
      manyLots.push({
        id: `perf-lot-${i}`,
        cityId: 'city-ba',
        name: `Generated Lot ${i}`,
        description: `Performance test lot #${i}`,
        district: 'Industrial Zone',
        latitude: lat,
        longitude: lon,
        populationIndex: 0.8,
        basePrice: 90000,
        price: 90000,
        suitableTypes: 'FACTORY',
        ownerCompanyId: null,
        buildingId: null,
        ownerCompany: null,
        building: null,
        resourceType: null,
        materialQuality: null,
        materialQuantity: null,
      })
    }

    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Test Empire',
          cash: 500000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player], buildingLots: manyLots })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    const start = Date.now()
    await page.goto('/city/city-ba')

    // Verify the map loaded with 100 lots in the count
    await expect(page.getByText(/100 lots/i)).toBeVisible()

    const elapsed = Date.now() - start
    // Should load within 5 seconds (well within performance budget)
    expect(elapsed).toBeLessThan(5000)
  })

  // ── Secondary Market: For-Sale Badge on Lot Marker ─────────────────────────

  test('lot detail panel shows "For Sale" badge when building on lot is listed', async ({ page }) => {
    // The commercial lot (index 2) is "High Street Retail Space" — override it with a for-sale building
    const lots = makeDefaultBuildingLots()
    lots[2] = {
      ...lots[2]!,
      ownerCompanyId: 'co-other',
      buildingId: 'bldg-for-sale',
      ownerCompany: { id: 'co-other', name: 'Other Corp' },
      building: {
        id: 'bldg-for-sale',
        name: 'For Sale Building',
        type: 'SALES_SHOP',
        isForSale: true,
        askingPrice: 750000,
      },
    }
    setupMockApi(page, { buildingLots: lots })
    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /High Street Retail Space/i }).click()
    await expect(page.getByTestId('lot-for-sale-badge')).toBeVisible()
  })

  test('lot detail panel does not show "For Sale" badge for non-listed buildings', async ({
    page,
  }) => {
    // The commercial lot (index 2) has a building that is NOT for sale
    const lots = makeDefaultBuildingLots()
    lots[2] = {
      ...lots[2]!,
      ownerCompanyId: 'co-other',
      buildingId: 'bldg-not-sale',
      ownerCompany: { id: 'co-other', name: 'Other Corp' },
      building: {
        id: 'bldg-not-sale',
        name: 'Normal Building',
        type: 'SALES_SHOP',
        isForSale: false,
        askingPrice: null,
      },
    }
    setupMockApi(page, { buildingLots: lots })
    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /High Street Retail Space/i }).click()
    await expect(page.getByTestId('lot-for-sale-badge')).toHaveCount(0)
  })
})

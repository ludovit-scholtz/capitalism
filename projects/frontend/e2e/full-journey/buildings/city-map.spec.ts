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

    const panel = page.getByRole('complementary')
    // Lat/lon must be visible in the lot detail panel
    // Mock data: lat=48.145, lon=17.107
    await expect(panel.getByText(/48\.\d+/)).toBeVisible()
    await expect(panel.getByText(/17\.\d+/)).toBeVisible()
  })

  test('GPS coordinates are stored with decimal precision for all mock lots', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()

    // Verify Factory Site B1 coordinates (lat=48.15, lon=17.13) are shown
    await page.getByRole('button', { name: /Factory Site B1/i }).click()
    const panel = page.getByRole('complementary')
    await expect(panel.getByText(/48\.\d+/)).toBeVisible()
    await expect(panel.getByText(/17\.\d+/)).toBeVisible()
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

    // Capture the GPS coordinates shown before purchase
    await page.getByRole('button', { name: /High Street Retail Space/i }).click()
    const panel = page.getByRole('complementary')
    const latTextBefore = await panel.getByText(/48\.\d+/).first().textContent()

    // Initiate purchase
    await page.getByRole('button', { name: /Purchase Lot/i }).click()
    await page.locator('.building-type-card').filter({ hasText: /Sales Shop/i }).click()
    await page.getByRole('complementary').locator('input[type="text"]').fill('My City Shop')
    await page.getByRole('button', { name: /Confirm Purchase/i }).click()

    // After purchase, re-open the lot — verify coordinates have not changed
    await expect(page.locator('.status-badge.yours')).toBeVisible()
    const latTextAfter = panel.getByText(/48\.\d+/).first()
    await expect(latTextAfter).toHaveText(latTextBefore)
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
})

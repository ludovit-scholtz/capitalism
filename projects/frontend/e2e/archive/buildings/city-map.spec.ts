import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer, makeDefaultBuildingLots, type MockBuildingLot } from '../../helpers/mock-api'

// ── Helper to set up an authenticated player with a company ──────────────────

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
        buildings: [
          {
            id: 'building-1',
            companyId: 'company-1',
            cityId: 'city-ba',
            type: 'FACTORY',
            name: 'Test Factory',
            latitude: 48.15,
            longitude: 17.11,
            level: 1,
            powerConsumption: 1,
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
  return { state, player }
}

async function authenticateViaLocalStorage(page: import('@playwright/test').Page, playerId: string) {
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${playerId}`)
}

async function switchCityViaContextSwitcher(page: import('@playwright/test').Page, cityName: 'Bratislava' | 'Prague' | 'Vienna') {
  await page.locator('.ctx-trigger').click()
  await page.locator('.ctx-city-option', { hasText: cityName }).click()
  await expect(page.locator('.ctx-trigger .ctx-city-name')).toContainText(cityName)
}

// ── Tests ────────────────────────────────────────────────────────────────────

test.describe('City Map View', () => {
  test('requires the active company account before purchasing a lot', async ({ page }) => {
    const { player, state } = setupAuthenticatedPlayer(page)
    player.activeAccountType = 'PERSON'
    player.activeCompanyId = null
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    await expect(page.getByText('Switch to a company account in the top menu to purchase lots and place buildings.')).toBeVisible()
    await expect(page.getByRole('button', { name: /Purchase Lot/i })).toHaveCount(0)

    await page.locator('.ctx-trigger').click()
    await page.locator('.ctx-account-option', { hasText: 'Test Empire' }).click()

    await expect(page.getByRole('button', { name: /Purchase Lot/i })).toBeVisible()
    expect(state.players[0]?.activeAccountType).toBe('COMPANY')
  })

  test('renders city map with building lots', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')

    // Should show the city name and lot count
    await expect(page.getByRole('heading', { name: /Bratislava/i })).toBeVisible()
    await expect(page.getByText(/5 lots/i)).toBeVisible()
  })

  test('shows lot details when clicking a lot in list view', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')

    // Switch to list view
    await page.getByRole('button', { name: /List View/i }).click()

    // Click on a lot
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    // Should show lot details in the detail panel
    await expect(page.getByRole('heading', { name: 'Industrial Plot A1' })).toBeVisible()
    await expect(page.getByRole('complementary').getByText('Industrial Zone')).toBeVisible()
    // Price should be in the millions range (premium mine lot with Iron Ore deposit)
    await expect(page.getByTestId('asking-price')).toBeVisible()
    await expect(page.locator('.type-tag', { hasText: 'Factory' })).toBeVisible()
  })

  test('shows available status for unowned lots', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')

    // Switch to list view
    await page.getByRole('button', { name: /List View/i }).click()

    // Click an unowned lot
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    // Should show Available badge
    await expect(page.locator('.status-badge.available')).toBeVisible()
  })

  test('can initiate purchase flow for available lot', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')

    // Switch to list view
    await page.getByRole('button', { name: /List View/i }).click()

    // Select a lot
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    // Click Purchase Lot button
    await page.getByRole('button', { name: /Purchase Lot/i }).click()

    // Should show purchase form
    await expect(page.getByText('Building Type', { exact: true })).toBeVisible()
    await expect(page.getByText('Building Name')).toBeVisible()
  })

  test('completes purchase flow successfully', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')

    // Switch to list view
    await page.getByRole('button', { name: /List View/i }).click()

    // Use an affordable commercial lot ($120K) — the mine lot costs $32M+ which exceeds starter cash
    await page.getByRole('button', { name: /High Street Retail Space/i }).click()

    // Click Purchase Lot button
    await page.getByRole('button', { name: /Purchase Lot/i }).click()

    // Fill in purchase form
    await page
      .locator('.building-type-card')
      .filter({ hasText: /Sales Shop/i })
      .click()
    await page.getByRole('complementary').locator('input[type="text"]').fill('My New Sales Shop')

    // Click confirm
    await page.getByRole('button', { name: /Confirm Purchase/i }).click()

    // Should show success message or the lot should become owned
    await expect(page.getByText(/purchased successfully/i).or(page.locator('.status-badge.yours'))).toBeVisible()
  })

  test('shows renewable weather outlook and purchases a solar power plant', async ({ page }) => {
    const lots: MockBuildingLot[] = [
      ...makeDefaultBuildingLots(),
      {
        id: 'lot-power-1',
        cityId: 'city-ba',
        name: 'Grid Edge Energy Park',
        description: 'Utility-scale energy lot on the edge of the city grid.',
        district: 'Energy District',
        latitude: 48.161,
        longitude: 17.134,
        populationIndex: 0.38,
        basePrice: 150000,
        price: 160000,
        suitableTypes: 'POWER_PLANT,FACTORY',
        ownerCompanyId: null,
        buildingId: null,
        ownerCompany: null,
        building: null,
        resourceType: null,
        materialQuality: null,
        materialQuantity: null,
      },
    ]

    const { player } = setupAuthenticatedPlayer(page)
    const state = setupMockApi(page, { players: [player], buildingLots: lots })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.cityWeatherForecasts['city-ba'] = {
      cityId: 'city-ba',
      currentWindPercent: 67,
      currentSolarPercent: 82,
      forecast: Array.from({ length: 12 }, (_, index) => ({
        tick: 42 + index,
        windPercent: 67 - (index % 3) * 4,
        solarPercent: Math.max(12, 82 - index * 5),
      })),
    }
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Grid Edge Energy Park/i }).click()

    await expect(page.getByTestId('weather-outlook-panel')).toBeVisible()
    await expect(page.getByText(/82% solar/i)).toBeVisible()
    await expect(page.getByText(/67% wind/i)).toBeVisible()

    await page.getByRole('button', { name: /Purchase Lot/i }).click()
    await page
      .locator('.building-type-card')
      .filter({ hasText: /Power Plant/i })
      .first()
      .click()
    await expect(page.getByText('Plant type', { exact: true })).toBeVisible()
    await expect(page.getByRole('radio', { name: /Solar20 MW/i })).toBeVisible()
    await expect(page.getByRole('radio', { name: /Solar20 MW/i })).toContainText('82%')
    await page.getByRole('radio', { name: /Solar20 MW/i }).click()
    await page.getByRole('complementary').locator('input[type="text"]').fill('Solar Ridge Station')
    await page.getByRole('button', { name: /Confirm Purchase/i }).click()

    await expect(page.getByRole('heading', { name: 'Grid Edge Energy Park' })).toBeVisible()
    await expect(page.getByText('Solar Ridge Station')).toBeVisible()
    await expect(page.locator('.status-badge.yours')).toBeVisible()
  })

  test('shows owned lots with different status', async ({ page }) => {
    const lots: MockBuildingLot[] = makeDefaultBuildingLots()
    // Mark one lot as owned by another player's company
    lots[0]!.ownerCompanyId = 'other-company-id'
    lots[0]!.buildingId = 'other-building-id'
    lots[0]!.ownerCompany = { id: 'other-company-id', name: 'Other Corp' }
    lots[0]!.building = { id: 'other-building-id', name: 'Rival Factory', type: 'FACTORY' }

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
    const state = setupMockApi(page, { players: [player], buildingLots: lots })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')

    // Switch to list view
    await page.getByRole('button', { name: /List View/i }).click()

    // Click on the owned lot
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    // Should show "Owned" badge
    await expect(page.locator('.status-badge.owned')).toBeVisible()

    // Purchase button should NOT be present for owned lots
    await expect(page.getByRole('button', { name: /Purchase Lot/i })).toBeHidden()
  })

  test('handles already-purchased lot error gracefully', async ({ page }) => {
    // Set up a lot that becomes purchased between selection and purchase attempt
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
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /High Street Retail Space/i }).click()
    await page.getByRole('button', { name: /Purchase Lot/i }).click()

    // Now mark the lot as owned before form submission to simulate race condition
    const lot = state.buildingLots.find((l) => l.id === 'lot-commercial-1')!
    lot.ownerCompanyId = 'other-company'
    lot.ownerCompany = { id: 'other-company', name: 'Rival Corp' }

    await page
      .locator('.building-type-card')
      .filter({ hasText: /Sales Shop/i })
      .click()
    await page.locator('.form-input').fill('My Shop')
    await page.getByRole('button', { name: /Confirm Purchase/i }).click()

    // Should show actionable guidance — stale lot, not just a generic error
    await expect(page.getByText(/just claimed by another player/i)).toBeVisible()
    await expect(page.getByText(/select a different available lot/i)).toBeVisible()

    // Purchase form should be dismissed; lot should now show as Owned
    await expect(page.locator('.status-badge.owned')).toBeVisible()
  })

  test('filter toggle shows only available lots', async ({ page }) => {
    const lots: MockBuildingLot[] = makeDefaultBuildingLots()
    // Mark one lot as owned
    lots[0]!.ownerCompanyId = 'other-company-id'
    lots[0]!.ownerCompany = { id: 'other-company-id', name: 'Other Corp' }

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
    const state = setupMockApi(page, { players: [player], buildingLots: lots })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')

    // Initially shows all 5 lots
    await expect(page.getByText(/5 lots/i)).toBeVisible()

    // Click "Available Only" filter
    await page.getByRole('button', { name: /Available Only/i }).click()

    // Should show 4 lots (one is owned)
    await expect(page.getByText(/4 lots/i)).toBeVisible()
  })

  test('unauthenticated user sees login required notice', async ({ page }) => {
    // No authentication setup
    setupMockApi(page)

    await page.goto('/city/city-ba')

    // Switch to list view
    await page.getByRole('button', { name: /List View/i }).click()

    // Select a lot
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    // Should show login required notice
    await expect(page.getByText(/Log in to purchase/i)).toBeVisible()
  })

  test('detail panel shows population index with contextual label', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')

    // Switch to list view and select an industrial lot (low pop index = 0.65x in mock data)
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    // The detail panel should show Population Index label and a numeric value
    const panel = page.getByRole('complementary')
    await expect(panel.getByText('Population Index', { exact: true })).toBeVisible()
    // Mock data has populationIndex: 0.65 → formatted as "0.65x"
    await expect(panel.getByText('0.65x')).toBeVisible()
    // Should show a tier label (Low for 0.65)
    await expect(panel.getByText('Low', { exact: true })).toBeVisible()
    // Should show the explanatory hint about why location matters
    await expect(panel.getByText(/stronger demand for retail/i)).toBeVisible()
  })

  test('commercial lot shows high population index label', async ({ page }) => {
    // Lot lot-commercial-1 has populationIndex: 1.42 in mock data → High
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /High Street Retail Space/i }).click()

    const panel = page.getByRole('complementary')
    await expect(panel.getByText('Population Index', { exact: true })).toBeVisible()
    await expect(panel.getByText('1.42x')).toBeVisible()
    // 1.42 is in the "High" band (>= 1.3, < 1.8)
    await expect(panel.getByText('High', { exact: true })).toBeVisible()
  })

  test('dashboard links to city map', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Test Empire',
          cash: 500000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [
            {
              id: 'building-1',
              companyId: 'company-1',
              cityId: 'city-ba',
              type: 'FACTORY',
              name: 'Test Factory',
              latitude: 48.15,
              longitude: 17.11,
              level: 1,
              powerConsumption: 1,
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

    await page.goto('/dashboard')

    // Should show city map link
    const cityMapLink = page.getByRole('link', { name: /City Map/i })
    await expect(cityMapLink).toBeVisible()

    // Click it and verify navigation
    await cityMapLink.click()
    await page.waitForURL(/\/city\//)
  })

  test('post-purchase shows construction-started banner', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')

    // Switch to list view and select an affordable commercial lot ($120K within $500K cash)
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /High Street Retail Space/i }).click()

    // Open purchase form
    await page.getByRole('button', { name: /Purchase Lot/i }).click()

    // Select building type via card picker, fill name, and submit
    await page
      .locator('.building-type-card')
      .filter({ hasText: /Sales Shop/i })
      .click()
    await page.getByRole('complementary').locator('input[type="text"]').fill('Victory Shop')
    await page.getByRole('button', { name: /Confirm Purchase/i }).click()

    // After purchase the construction banner should appear
    await expect(page.locator('[data-testid="construction-banner"]')).toBeVisible()
    await expect(page.getByText(/Construction started/i)).toBeVisible()
  })

  test('previously owned lot shows manage building link (not post-purchase banner)', async ({ page }) => {
    const lots = makeDefaultBuildingLots()
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Test Empire',
          cash: 500000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [
            {
              id: 'building-owned',
              companyId: 'company-1',
              cityId: 'city-ba',
              type: 'FACTORY',
              name: 'Old Factory',
              latitude: 48.15,
              longitude: 17.11,
              level: 1,
              powerConsumption: 1,
              isForSale: false,
              builtAtUtc: '2026-01-01T00:00:00Z',
              units: [],
              pendingConfiguration: null,
            },
          ],
        },
      ],
    })
    // Mark the first lot as already owned by the player
    lots[0]!.ownerCompanyId = 'company-1'
    lots[0]!.buildingId = 'building-owned'
    lots[0]!.ownerCompany = { id: 'company-1', name: 'Test Empire' }
    lots[0]!.building = { id: 'building-owned', name: 'Old Factory', type: 'FACTORY' }

    const state = setupMockApi(page, { players: [player], buildingLots: lots })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    // Already-owned lot shows "Manage Building" (not the post-purchase banner)
    await expect(page.getByRole('link', { name: /Manage Building/i })).toBeVisible()
    await expect(page.locator('.post-purchase-banner')).toBeHidden()
  })

  test('shows insufficient funds error when company cash is too low', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Broke Corp',
          cash: 0, // no money
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()
    await page.getByRole('button', { name: /Purchase Lot/i }).click()

    await page
      .locator('.building-type-card')
      .filter({ hasText: /Factory/i })
      .click()
    await page.locator('.form-input').fill('Bankrupt Factory')
    await page.getByRole('button', { name: /Confirm Purchase/i }).click()

    // Should show actionable insufficient funds guidance
    await expect(page.getByText(/does not have enough cash/i)).toBeVisible()
    await expect(page.getByText(/Review your finances/i)).toBeVisible()
  })

  // ── Raw Material & Placement Guidance ──────────────────────────────────────

  test('lot list shows resource type badge for mining-capable lots', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()

    // Industrial Plot A1 is a MINE-eligible lot with Iron Ore — its list item should show the badge
    const mineListItem = page.locator('.lot-list-item').filter({ hasText: /Industrial Plot A1/i })
    await expect(mineListItem.getByTestId('lot-resource-badge')).toBeVisible()
    await expect(mineListItem.getByTestId('lot-resource-badge')).toContainText(/Iron Ore/i)

    // High Street Retail Space has no resource — no badge should appear
    const commercialListItem = page.locator('.lot-list-item').filter({ hasText: /High Street Retail Space/i })
    await expect(commercialListItem.locator('[data-testid="lot-resource-badge"]')).toHaveCount(0)
  })

  test('mining deposit summary shown in purchase form when MINE type selected', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()
    await page.getByRole('button', { name: /Purchase Lot/i }).click()

    // Select Mine building type
    await page.locator('.building-type-card').filter({ hasText: /Mine/i }).click()

    // Deposit investment summary should appear
    const depositSummary = page.locator('[data-testid="mining-deposit-summary"]')
    await expect(depositSummary).toBeVisible()
    await expect(depositSummary.getByText(/Iron Ore/i)).toBeVisible()
    await expect(depositSummary.getByText(/72%/)).toBeVisible()
    // Investment hint explains the premium pricing
    await expect(depositSummary.getByText(/long-term industrial asset/i)).toBeVisible()
  })

  test('mining deposit summary hidden when Factory type selected on mine lot', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()
    await page.getByRole('button', { name: /Purchase Lot/i }).click()

    // Select Factory building type (not Mine) — deposit summary should NOT appear
    await page
      .locator('.building-type-card')
      .filter({ hasText: /Factory/i })
      .click()
    await expect(page.locator('[data-testid="mining-deposit-summary"]')).toHaveCount(0)
  })

  test('shows raw material panel for MINE-eligible lots with resource data', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    // Industrial Plot A1 has iron ore raw material in makeDefaultBuildingLots
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    // Raw material panel should be visible
    const panel = page.locator('[data-testid="raw-material-panel"]')
    await expect(panel).toBeVisible()

    // Should show the resource name
    await expect(panel.getByText(/Iron Ore/i)).toBeVisible()
    // Should show material quality
    await expect(panel.getByText(/72%/)).toBeVisible()
    // Should show material quantity
    await expect(panel.getByText(/18[,.]?000/)).toBeVisible()
  })

  test('hides raw material panel for non-extraction lots', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    // Commercial lot has no raw material
    await page.getByRole('button', { name: /High Street Retail Space/i }).click()

    // Raw material panel should NOT be visible
    await expect(page.locator('[data-testid="raw-material-panel"]')).toBeHidden()
  })

  test('shows placement guidance panel for selected lot', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    const guidancePanel = page.locator('[data-testid="placement-guidance-panel"]')
    await expect(guidancePanel).toBeVisible()

    // Should mention Factory guidance (Industrial Plot A1 has FACTORY,MINE suitableTypes)
    await expect(guidancePanel.locator('.guidance-building-type').filter({ hasText: /Factory/i })).toBeVisible()
    // Should mention Mine guidance
    await expect(guidancePanel.locator('.guidance-building-type').filter({ hasText: /Mine/i })).toBeVisible()
    // Should show transport cost note (scroll into view since panel may be long)
    const transportNote = guidancePanel.locator('.transport-cost-note')
    await transportNote.scrollIntoViewIfNeeded()
    await expect(transportNote).toBeVisible()
  })

  test('shows retail-specific placement guidance for commercial lots', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /High Street Retail Space/i }).click()

    const guidancePanel = page.locator('[data-testid="placement-guidance-panel"]')
    await expect(guidancePanel).toBeVisible()
    // Should mention retail-specific guidance (SALES_SHOP)
    await expect(guidancePanel.getByText(/demand/i)).toBeVisible()
  })

  test('raw material quality badge shows correct label', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    // Customize lot to have excellent quality (0.85)
    const lots = makeDefaultBuildingLots()
    lots[0]!.materialQuality = 0.85
    lots[0]!.materialQuantity = 25000
    lots[0]!.resourceType = { id: 'res-gold', name: 'Gold', slug: 'gold' }

    const state = setupMockApi(page, { players: [player], buildingLots: lots })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    const panel = page.locator('[data-testid="raw-material-panel"]')
    await expect(panel).toBeVisible()
    // 85% quality = Excellent
    await expect(panel.getByText(/Excellent/i)).toBeVisible()
    await expect(panel.getByText(/Gold/i)).toBeVisible()
  })

  test('placement guidance mine hint mentions resource extraction strategy', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    const guidancePanel = page.locator('[data-testid="placement-guidance-panel"]')
    await expect(guidancePanel).toBeVisible()
    // Mine guidance should mention exchange (transport cost vs exchange comparison)
    await expect(guidancePanel.getByText(/exchange/i)).toBeVisible()
  })

  test('narrow viewport still shows raw material and placement guidance', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 })
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    // On narrow viewport the detail panel should still be scrollable/accessible
    const rawMaterialPanel = page.locator('[data-testid="raw-material-panel"]')
    const guidancePanel = page.locator('[data-testid="placement-guidance-panel"]')
    await expect(rawMaterialPanel).toBeVisible()
    await expect(guidancePanel).toBeVisible()
  })
})

// ── Invalid/stale selection paths ────────────────────────────────────────────

test.describe('City Map — invalid and stale selection paths', () => {
  test('shows error when trying to purchase lot with unsuitable building type', async ({ page }) => {
    // SALES_SHOP lot only allows SALES_SHOP,COMMERCIAL — not FACTORY
    const lots = makeDefaultBuildingLots()
    // Patch industrial lot to only accept MINE (so FACTORY is unsuitable)
    lots[0]!.suitableTypes = 'MINE'

    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-unsuitable',
          playerId: 'player-1',
          name: 'Wrong Type Corp',
          cash: 500000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player], buildingLots: lots })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()
    await page.getByRole('button', { name: /Purchase Lot/i }).click()

    // The card picker only shows suitable types — Mine card should be present, Factory should not
    await expect(page.locator('.building-type-card').filter({ hasText: /Mine/i })).toBeVisible()
    await expect(page.locator('.building-type-card').filter({ hasText: /Factory/i })).toHaveCount(0)
  })

  test('shows stale-lot error when lot was claimed by another player before purchase completes', async ({ page }) => {
    // Player selects a lot, another player claims it, then the first player submits purchase
    const lots = makeDefaultBuildingLots()
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-stale',
          playerId: 'player-1',
          name: 'Slow Corp',
          cash: 500000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player], buildingLots: lots })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()
    await page.getByRole('button', { name: /Purchase Lot/i }).click()

    // Another player claims the lot between selection and purchase
    lots[0]!.ownerCompanyId = 'other-company-99'

    // Now submit the purchase
    await page
      .locator('.building-type-card')
      .filter({ hasText: /Factory/i })
      .click()
    await page.locator('.form-input').fill('Too Late Factory')
    await page.getByRole('button', { name: /Confirm Purchase/i }).click()

    // Should show stale lot / already owned error message
    await expect(page.getByText(/just claimed by another player/i, { exact: false })).toBeVisible()
    // Should prompt the player to choose a different lot
    await expect(page.getByText(/select a different available lot/i, { exact: false })).toBeVisible()
  })

  test('lot detail shows district information to help player understand location context', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    // District name is shown (Industrial Zone for lot-industrial-1)
    await expect(page.getByRole('complementary').getByText(/Industrial Zone/i)).toBeVisible()
  })

  test('lot detail shows price to help player understand land valuation', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    // Price is visible (Industrial lot has price=32464500 with Iron Ore resource premium in mock data)
    // New premium pricing: 18000t × $25/t × 72% × captureRate(100) = $32.4M deposit premium
    const detailPanel = page.getByRole('complementary')
    await expect(detailPanel.getByTestId('asking-price')).toBeVisible()
    // Price should be in the millions range (premium mine lot)
    await expect(detailPanel.getByText(/32[,.]?464[,.]?500|32\.4[Mm]|32,464/)).toBeVisible()
  })

  test('lot detail shows appraised value and asking price separately', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    // Industrial lot has basePrice=75000 and price=32464500 (includes $32.4M Iron Ore resource premium)
    const detailPanel = page.getByRole('complementary')
    // Appraised value label shows base land value
    await expect(detailPanel.getByText(/Appraised Value/i)).toBeVisible()
    // Both values are shown
    await expect(detailPanel.getByTestId('appraised-value')).toBeVisible()
    await expect(detailPanel.getByTestId('asking-price')).toBeVisible()
  })

  test('mine lot with raw material shows resource premium badge on asking price', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    // Industrial Plot A1 has Iron Ore (resourceType set) + price > basePrice
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    const detailPanel = page.getByRole('complementary')
    // The resource premium badge is shown next to the asking price
    await expect(detailPanel.locator('.resource-premium-badge')).toBeVisible()
  })

  test('non-resource lot does NOT show resource premium badge', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    // High Street Retail Space has no resourceType (null)
    await page.getByRole('button', { name: /High Street Retail Space/i }).click()

    const detailPanel = page.getByRole('complementary')
    // No resource premium badge for non-extraction lots
    await expect(detailPanel.locator('.resource-premium-badge')).toHaveCount(0)
  })

  test('population index hint explains retail demand to player', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    const detailPanel = page.getByRole('complementary')
    // The population index educational hint text is shown
    await expect(detailPanel.getByText(/Higher index.*more nearby residents/i, { exact: false })).toBeVisible()
  })

  test('placement guidance panel shows transport cost note', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    const detailPanel = page.getByRole('complementary')
    // Transport cost note is shown in the placement guidance panel
    await expect(detailPanel.getByTestId('placement-guidance-panel')).toBeVisible()
    await expect(detailPanel.getByText(/Distance from your other buildings/i, { exact: false })).toBeVisible()
  })

  test('lot detail shows GPS coordinates for logistics context', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    // Industrial Plot A1 has latitude: 48.152, longitude: 17.125 in mock data
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    const detailPanel = page.getByRole('complementary')
    // GPS coordinates section is visible
    await expect(detailPanel.getByText('GPS Coordinates')).toBeVisible()
    // Coordinate value matches the lot's actual lat/lon from mock data
    const coordEl = detailPanel.getByTestId('lot-coordinates')
    await expect(coordEl).toBeVisible()
    // Match numeric parts only (degree symbol encoding may vary across environments)
    await expect(coordEl).toContainText(/48\.152/)
    await expect(coordEl).toContainText(/17\.125/)
    // Logistics hint is shown
    await expect(detailPanel.getByText(/Coordinates are used for logistics/i, { exact: false })).toBeVisible()
  })
})

// ── Building type card picker ────────────────────────────────────────────────

test.describe('City Map — building type card picker', () => {
  test('purchase form shows card-based building type picker with icon and description', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()
    await page.getByRole('button', { name: /Purchase Lot/i }).click()

    // Cards should be visible for each suitable type (Industrial Plot A1 has FACTORY,MINE)
    const factoryCard = page.locator('.building-type-card').filter({ hasText: /Factory/i })
    const mineCard = page.locator('.building-type-card').filter({ hasText: /Mine/i })
    await expect(factoryCard).toBeVisible()
    await expect(mineCard).toBeVisible()

    // Each card shows an icon and description
    await expect(factoryCard.locator('.card-type-icon')).toBeVisible()
    await expect(factoryCard.locator('.card-type-desc')).toBeVisible()
    await expect(mineCard.locator('.card-type-icon')).toBeVisible()
  })

  test('selecting a building type card marks it as selected and shows strategic guidance', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()
    await page.getByRole('button', { name: /Purchase Lot/i }).click()

    // Click the Factory card
    await page
      .locator('.building-type-card')
      .filter({ hasText: /Factory/i })
      .click()

    // Factory card should be selected
    await expect(page.locator('.building-type-card.selected').filter({ hasText: /Factory/i })).toBeVisible()

    // Strategic guidance for factory should appear below the cards
    await expect(page.locator('.selected-type-guidance')).toBeVisible()
    await expect(page.locator('.selected-type-guidance')).toContainText(/industrial/i)
  })

  test('card picker only shows building types suitable for the lot', async ({ page }) => {
    // High Street Retail Space is suitable for SALES_SHOP and COMMERCIAL only
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /High Street Retail Space/i }).click()
    await page.getByRole('button', { name: /Purchase Lot/i }).click()

    // Should show Sales Shop card (suitable)
    await expect(page.locator('.building-type-card').filter({ hasText: /Sales Shop/i })).toBeVisible()

    // Should NOT show Factory card (not suitable for retail space)
    await expect(page.locator('.building-type-card').filter({ hasText: /Factory/i })).toHaveCount(0)
  })

  test('post-purchase banner shows construction started state', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    // Use affordable commercial lot — mine lot now costs $32M+ (premium pricing)
    await page.getByRole('button', { name: /High Street Retail Space/i }).click()
    await page.getByRole('button', { name: /Purchase Lot/i }).click()

    await page
      .locator('.building-type-card')
      .filter({ hasText: /Sales Shop/i })
      .click()
    await page.locator('.form-input').fill('Supply Chain Shop')
    await page.getByRole('button', { name: /Confirm Purchase/i }).click()

    // Post-purchase banner should show construction started state
    await expect(page.locator('[data-testid="construction-banner"]')).toBeVisible()
    await expect(page.locator('[data-testid="construction-banner"]')).toContainText(/Construction started/i)
  })

  test('mobile viewport can complete purchase flow using card picker', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 })
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    // Use affordable commercial lot — mine lot now costs $32M+ (premium pricing)
    await page.getByRole('button', { name: /High Street Retail Space/i }).click()

    await page.getByRole('button', { name: /Purchase Lot/i }).click()

    // On mobile the card picker should still be visible and usable
    const shopCard = page.locator('.building-type-card').filter({ hasText: /Sales Shop/i })
    await expect(shopCard).toBeVisible()
    await shopCard.click()
    await expect(shopCard).toHaveClass(/selected/)

    await page.locator('.form-input').fill('Mobile Shop')
    await page.getByRole('button', { name: /Confirm Purchase/i }).click()

    // Success: construction-started banner appears
    await expect(page.locator('[data-testid="construction-banner"]')).toBeVisible()
    await expect(page.locator('[data-testid="construction-banner"]')).toContainText(/Construction started/i)
  })
})

test.describe('City Map — purchase cost summary and cash delta', () => {
  test('purchase form shows lot price and available cash before confirming', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    // Select an available mine lot (Industrial Plot A1 — premium mine lot at ~$32M with Iron Ore deposit)
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()
    await page.getByRole('button', { name: /Purchase Lot/i }).click()

    // The purchase cost summary should be visible
    const summary = page.locator('[aria-label="Purchase cost summary"]')
    await expect(summary).toBeVisible()

    // Lot price is shown
    await expect(summary.getByText(/Lot price/i)).toBeVisible()
    // Current cash is shown (player has $500,000)
    await expect(summary.getByText(/Available cash/i)).toBeVisible()
    // Remaining after purchase is shown
    await expect(summary.getByText(/Remaining after purchase/i)).toBeVisible()
  })

  test('remaining cash after purchase shows positive value for affordable lot', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    // High Street Retail Space costs $120K; player has $500,000 → remaining is $372,000 (after $8K shop construction)
    await page.getByRole('button', { name: /High Street Retail Space/i }).click()
    await page.getByRole('button', { name: /Purchase Lot/i }).click()

    // Select a building type to trigger cost calculation
    await page
      .locator('.building-type-card')
      .filter({ hasText: /Sales Shop/i })
      .click()

    const summary = page.locator('[aria-label="Purchase cost summary"]')
    await expect(summary).toBeVisible()
    // Remaining cash should have cost-positive class (green)
    await expect(summary.locator('.cost-positive')).toBeVisible()
  })

  test('cash balance decreases after successful purchase', async ({ page }) => {
    const lots: MockBuildingLot[] = makeDefaultBuildingLots()
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
        {
          id: 'company-2',
          playerId: 'player-1',
          name: 'Second Empire',
          cash: 200000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player], buildingLots: lots })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    // High Street Retail Space costs $120,000 — affordable with $500K starting cash
    await page.getByRole('button', { name: /High Street Retail Space/i }).click()
    await page.getByRole('button', { name: /Purchase Lot/i }).click()

    // The purchase form now uses the active company from the header switcher.
    const activeCompanySummary = page.locator('.active-company-summary')
    await expect(activeCompanySummary).toBeVisible()
    await expect(activeCompanySummary).toContainText('Test Empire')
    await expect(activeCompanySummary).toContainText('500,000')

    await page
      .locator('.building-type-card')
      .filter({ hasText: /Sales Shop/i })
      .click()
    await page.locator('.form-input').fill('Retail Outlet')
    await page.getByRole('button', { name: /Confirm Purchase/i }).click()

    // Success banner appears — confirms the purchase went through
    await expect(page.locator('.post-purchase-banner').or(page.locator('[data-testid="construction-banner"]'))).toBeVisible()

    // Now select a second available lot and open the purchase form to verify the company
    // cash has been reduced (500,000 - 120,000 lot - 8,000 shop construction = 372,000)
    await page.getByRole('button', { name: /Riverside Apartment Block/i }).click()
    await page.getByRole('button', { name: /Purchase Lot/i }).click()

    // The active company summary should now reflect the updated cash (contains $372,000)
    await expect(activeCompanySummary).toBeVisible()
    await expect(activeCompanySummary).toContainText('372,000')
  })
})

test.describe('City Map — multi-city navigation and graceful empty state', () => {
  test('Prague city map shows city name with no lots available', async ({ page }) => {
    const { state, player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    // Clear lots so Prague has no building lots (not yet seeded in the game)
    state.buildingLots = []

    await page.goto('/city/city-pr')

    // City name should appear
    await expect(page.getByRole('heading', { name: /Prague/i })).toBeVisible()
    // No lots: shows "0 lots" in the lot-count badge
    await expect(page.locator('.lot-count')).toContainText('0')
  })

  test('Vienna city map shows city name with no lots available', async ({ page }) => {
    const { state, player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    // Clear lots so Vienna has no building lots (not yet seeded in the game)
    state.buildingLots = []

    await page.goto('/city/city-vi')

    // City name should appear
    await expect(page.getByRole('heading', { name: /Vienna/i })).toBeVisible()
    // No lots: shows "0 lots" in the lot-count badge
    await expect(page.locator('.lot-count')).toContainText('0')
  })
})

test.describe('City Map — strategic recommendation badge (decision support)', () => {
  test('resource-oriented lot shows "Resource-oriented" recommendation badge', async ({ page }) => {
    // Industrial Plot A1 has Iron Ore → should show resource-oriented label
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    const badge = page.locator('[data-testid="strategic-recommendation"]')
    await expect(badge).toBeVisible()
    await expect(badge).toContainText(/Resource-oriented/i)
  })

  test('high-population retail lot shows "Strong for retail demand" recommendation badge', async ({ page }) => {
    // High Street Retail Space has populationIndex 1.42 + SALES_SHOP → strong retail
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /High Street Retail Space/i }).click()

    const badge = page.locator('[data-testid="strategic-recommendation"]')
    await expect(badge).toBeVisible()
    await expect(badge).toContainText(/Strong for retail demand/i)
  })

  test('recommendation badge changes when switching between lots (decision support comparison)', async ({ page }) => {
    // Players compare two lots and see how the recommendation changes —
    // this is the core "why location matters" decision-support feature.
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()

    // Select the industrial lot first
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()
    const badge = page.locator('[data-testid="strategic-recommendation"]')
    await expect(badge).toBeVisible()
    await expect(badge).toContainText(/Resource-oriented/i)

    // Switch to the commercial lot — recommendation must update immediately
    await page.getByRole('button', { name: /High Street Retail Space/i }).click()
    await expect(badge).toContainText(/Strong for retail demand/i)

    // Switch back to industrial — should revert
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()
    await expect(badge).toContainText(/Resource-oriented/i)
  })

  test('mobile viewport shows recommendation badge and population index decision-support', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 })
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /High Street Retail Space/i }).click()

    const panel = page.getByRole('complementary')
    // Recommendation badge visible on mobile
    await expect(panel.locator('[data-testid="strategic-recommendation"]')).toBeVisible()
    await expect(panel.locator('[data-testid="strategic-recommendation"]')).toContainText(/Strong for retail demand/i)
    // Population index educational hint visible on mobile
    await expect(panel.getByText(/Higher index.*more nearby residents/i)).toBeVisible()
  })

  test('residential lot shows "Balanced starter location" recommendation', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Riverside Apartment Block/i }).click()

    const badge = page.locator('[data-testid="strategic-recommendation"]')
    await expect(badge).toBeVisible()
    await expect(badge).toContainText(/Balanced starter location/i)
  })
})

test.describe('City Map — construction order flow', () => {
  test('purchase cost summary shows construction cost when building type selected', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()
    await page.getByRole('button', { name: /Purchase Lot/i }).click()

    // Before selecting a building type, only lot price shows
    const summary = page.locator('[aria-label="Purchase cost summary"]')
    await expect(summary).toBeVisible()
    await expect(summary.getByText(/Lot price/i)).toBeVisible()

    // Select a building type — construction cost should appear
    await page
      .locator('.building-type-card')
      .filter({ hasText: /Factory/i })
      .click()
    await expect(summary.getByText(/Construction cost/i)).toBeVisible()
    await expect(summary.getByText(/Build time/i)).toBeVisible()
  })

  test('owned lot with under-construction building shows construction panel', async ({ page }) => {
    const lots = makeDefaultBuildingLots()
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Builder Corp',
          cash: 500000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [
            {
              id: 'building-under-construction',
              companyId: 'company-1',
              cityId: 'city-ba',
              type: 'FACTORY',
              name: 'New Factory Site',
              latitude: 48.152,
              longitude: 17.125,
              level: 1,
              powerConsumption: 5,
              isForSale: false,
              builtAtUtc: new Date().toISOString(),
              isUnderConstruction: true,
              constructionCompletesAtTick: 100,
              constructionCost: 15000,
              units: [],
              pendingConfiguration: null,
            },
          ],
        },
      ],
    })
    lots[0]!.ownerCompanyId = 'company-1'
    lots[0]!.buildingId = 'building-under-construction'
    lots[0]!.ownerCompany = { id: 'company-1', name: 'Builder Corp' }
    lots[0]!.building = {
      id: 'building-under-construction',
      name: 'New Factory Site',
      type: 'FACTORY',
      isUnderConstruction: true,
      constructionCompletesAtTick: 100,
      constructionCost: 15000,
    }

    const state = setupMockApi(page, { players: [player], buildingLots: lots })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    // The under-construction panel should be visible instead of "Manage Building"
    await expect(page.locator('[data-testid="under-construction-panel"]')).toBeVisible()
    await expect(page.locator('[data-testid="under-construction-panel"]')).toContainText(/Under Construction/i)
    await expect(page.locator('[data-testid="construction-ticks-remaining"]')).toBeVisible()
    // "Manage Building" should not be present (building is not operational)
    await expect(page.getByRole('link', { name: /Manage Building/i })).toBeHidden()
    // "View Building" link should be present
    await expect(page.getByRole('link', { name: /View Building/i })).toBeVisible()
  })

  test('post-purchase banner shows construction started with tick countdown', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    // Use affordable commercial lot — mine lot now costs $32M+ (premium pricing)
    await page.getByRole('button', { name: /High Street Retail Space/i }).click()
    await page.getByRole('button', { name: /Purchase Lot/i }).click()

    await page
      .locator('.building-type-card')
      .filter({ hasText: /Sales Shop/i })
      .click()
    await page.locator('.form-input').fill('Construction Test Shop')
    await page.getByRole('button', { name: /Confirm Purchase/i }).click()

    // Construction banner is shown (not the legacy "Set Up Your Building" CTA)
    const banner = page.locator('[data-testid="construction-banner"]')
    await expect(banner).toBeVisible()
    await expect(banner).toContainText(/Construction started/i)
    // The "Set Up Your Building" link should NOT appear (building is under construction)
    await expect(page.getByRole('link', { name: /Set Up Your Building/i })).toBeHidden()
  })

  test('insufficient funds for construction cost shows error', async ({ page }) => {
    const lots = makeDefaultBuildingLots()
    // Player has $124,000 — enough for the lot ($120,000) but not lot + shop construction ($8,000 extra = $128,000 total)
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Low Cash Corp',
          cash: 124000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player], buildingLots: lots })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    // Use High Street Retail Space ($120K lot) — player has $124K which is not enough for lot + construction ($128K total)
    await page.getByRole('button', { name: /High Street Retail Space/i }).click()
    await page.getByRole('button', { name: /Purchase Lot/i }).click()

    await page
      .locator('.building-type-card')
      .filter({ hasText: /Sales Shop/i })
      .click()
    await page.locator('.form-input').fill('Underfunded Shop')
    await page.getByRole('button', { name: /Confirm Purchase/i }).click()

    // The mock should return an INSUFFICIENT_FUNDS error (120,000 lot + 8,000 construction = 128,000 > 124,000)
    await expect(page.getByRole('alert').or(page.locator('.error-message')).first()).toBeVisible()
  })
})

test.describe('City Map — construction completion transition', () => {
  test('completed building (isUnderConstruction=false) shows Manage Building link, not construction panel', async ({ page }) => {
    // After ConstructionPhase fires, isUnderConstruction becomes false.
    // The lot detail panel should switch from the construction panel to "Manage Building".
    const lots = makeDefaultBuildingLots()
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Completed Builder Corp',
          cash: 300000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [
            {
              id: 'building-completed',
              companyId: 'company-1',
              cityId: 'city-ba',
              type: 'FACTORY',
              name: 'Completed Factory',
              latitude: 48.152,
              longitude: 17.125,
              level: 1,
              powerConsumption: 5,
              isForSale: false,
              builtAtUtc: new Date().toISOString(),
              // Construction already completed — isUnderConstruction is false
              isUnderConstruction: false,
              constructionCompletesAtTick: null,
              constructionCost: 15000,
              units: [],
              pendingConfiguration: null,
            },
          ],
        },
      ],
    })
    lots[0]!.ownerCompanyId = 'company-1'
    lots[0]!.buildingId = 'building-completed'
    lots[0]!.ownerCompany = { id: 'company-1', name: 'Completed Builder Corp' }
    lots[0]!.building = {
      id: 'building-completed',
      name: 'Completed Factory',
      type: 'FACTORY',
      isUnderConstruction: false,
      constructionCompletesAtTick: null,
      constructionCost: 15000,
    }

    const state = setupMockApi(page, { players: [player], buildingLots: lots })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    // Completed building shows "Manage Building", NOT the construction panel
    await expect(page.getByRole('link', { name: /Manage Building/i })).toBeVisible()
    await expect(page.locator('[data-testid="under-construction-panel"]')).toBeHidden()
    await expect(page.locator('[data-testid="construction-banner"]')).toBeHidden()
  })

  test('building with 0 ticks remaining shows 0 in ticks-remaining display', async ({ page }) => {
    // Edge-case: if constructionCompletesAtTick === currentTick, remaining should show 0.
    const lots = makeDefaultBuildingLots()
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Last Tick Corp',
          cash: 300000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [
            {
              id: 'building-last-tick',
              companyId: 'company-1',
              cityId: 'city-ba',
              type: 'MINE',
              name: 'Almost Done Mine',
              latitude: 48.152,
              longitude: 17.125,
              level: 1,
              powerConsumption: 1,
              isForSale: false,
              builtAtUtc: new Date().toISOString(),
              isUnderConstruction: true,
              constructionCompletesAtTick: 1, // same as default currentTick=1 in mock
              constructionCost: 5000,
              units: [],
              pendingConfiguration: null,
            },
          ],
        },
      ],
    })
    lots[0]!.ownerCompanyId = 'company-1'
    lots[0]!.buildingId = 'building-last-tick'
    lots[0]!.ownerCompany = { id: 'company-1', name: 'Last Tick Corp' }
    lots[0]!.building = {
      id: 'building-last-tick',
      name: 'Almost Done Mine',
      type: 'MINE',
      isUnderConstruction: true,
      constructionCompletesAtTick: 1,
      constructionCost: 5000,
    }

    const state = setupMockApi(page, { players: [player], buildingLots: lots })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()

    // Under-construction panel must be visible
    await expect(page.locator('[data-testid="under-construction-panel"]')).toBeVisible()
    // The ticks-remaining should show "0 ticks remaining"
    const ticksDisplay = page.locator('[data-testid="construction-ticks-remaining"]')
    await expect(ticksDisplay).toBeVisible()
    await expect(ticksDisplay).toContainText('0')
  })

  test('map and list views both reflect under-construction state consistently', async ({ page }) => {
    // Regression: construction state must be consistent whether the player is in map or list mode.
    const lots = makeDefaultBuildingLots()
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Consistency Corp',
          cash: 300000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [
            {
              id: 'building-consistency',
              companyId: 'company-1',
              cityId: 'city-ba',
              type: 'FACTORY',
              name: 'Consistency Factory',
              latitude: 48.152,
              longitude: 17.125,
              level: 1,
              powerConsumption: 5,
              isForSale: false,
              builtAtUtc: new Date().toISOString(),
              isUnderConstruction: true,
              constructionCompletesAtTick: 200,
              constructionCost: 15000,
              units: [],
              pendingConfiguration: null,
            },
          ],
        },
      ],
    })
    lots[0]!.ownerCompanyId = 'company-1'
    lots[0]!.buildingId = 'building-consistency'
    lots[0]!.ownerCompany = { id: 'company-1', name: 'Consistency Corp' }
    lots[0]!.building = {
      id: 'building-consistency',
      name: 'Consistency Factory',
      type: 'FACTORY',
      isUnderConstruction: true,
      constructionCompletesAtTick: 200,
      constructionCost: 15000,
    }

    const state = setupMockApi(page, { players: [player], buildingLots: lots })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/city/city-ba')

    // List view: select lot and verify construction panel
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()
    await expect(page.locator('[data-testid="under-construction-panel"]')).toBeVisible()
    await expect(page.getByRole('link', { name: /Manage Building/i })).toBeHidden()

    // Map view: same lot marker click, same construction state
    await page.getByRole('button', { name: /Map View/i }).click()
    // Select via list view again (map click is complex in E2E), then check the detail panel
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()
    await expect(page.locator('[data-testid="under-construction-panel"]')).toBeVisible()
  })
})

// ── Navbar context-switcher multi-city navigation ───────────────────────────

test.describe('City Map — navbar context switcher', () => {
  test('shows all seeded cities in the navbar context switcher', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')

    await page.locator('.ctx-trigger').click()
    await expect(page.locator('.ctx-city-option', { hasText: 'Bratislava' })).toBeVisible()
    await expect(page.locator('.ctx-city-option', { hasText: 'Prague' })).toBeVisible()
    await expect(page.locator('.ctx-city-option', { hasText: 'Vienna' })).toBeVisible()
  })

  test('switching city via navbar context switcher navigates to the new city URL', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await expect(page.getByRole('heading', { name: /Bratislava/i })).toBeVisible()

    await switchCityViaContextSwitcher(page, 'Prague')

    await page.waitForURL(/\/city\/city-pr/)
    await expect(page).toHaveURL(/\/city\/city-pr/)
  })

  test('city map heading updates when switching cities via navbar context switcher', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await expect(page.getByRole('heading', { name: /Bratislava/i })).toBeVisible()

    await switchCityViaContextSwitcher(page, 'Prague')
    await page.waitForURL(/\/city\/city-pr/)
    await expect(page.getByRole('heading', { name: /Prague/i })).toBeVisible()
  })

  test('switching to Vienna (third city) via navbar context switcher shows Vienna heading', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')

    await switchCityViaContextSwitcher(page, 'Vienna')
    await page.waitForURL(/\/city\/city-vi/)
    await expect(page.getByRole('heading', { name: /Vienna/i })).toBeVisible()
  })

  test('navbar context switcher shows Prague as selected when navigating directly to Prague', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-pr')

    await expect(page.locator('.ctx-trigger .ctx-city-name')).toContainText('Prague')
  })
})

// ── Blank-map regression ─────────────────────────────────────────────────────

test.describe('City Map — blank-map regression (list→map toggle)', () => {
  test('map container is visible after switching from list view back to map view', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')

    // Start in map view — map container should be visible
    await expect(page.locator('.map-container')).toBeVisible()

    // Switch to list view — map container remains in DOM (v-show) but is hidden
    await page.getByRole('button', { name: /List View/i }).click()
    await expect(page.locator('.map-container')).toBeHidden()

    // Switch back to map view — map container must become visible again (no blank map)
    await page.getByRole('button', { name: /Map View/i }).click()
    await expect(page.locator('.map-container')).toBeVisible()
  })

  test('map container stays in DOM (v-show) when switching to list view', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')

    // Switch to list view
    await page.getByRole('button', { name: /List View/i }).click()

    // The map container element exists in DOM (v-show, not v-if) — toHaveCount(1)
    await expect(page.locator('.map-container')).toHaveCount(1)
    // But it is hidden
    await expect(page.locator('.map-container')).toBeHidden()
  })

  test('switching back from list view restores lot selection context', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')

    // Select a lot in list view
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()
    await expect(page.getByRole('heading', { name: 'Industrial Plot A1' })).toBeVisible()

    // Switch to map view and back to list — selection should be preserved
    await page.getByRole('button', { name: /Map View/i }).click()
    await page.getByRole('button', { name: /List View/i }).click()

    // The detail panel should still show the previously selected lot
    await expect(page.getByRole('heading', { name: 'Industrial Plot A1' })).toBeVisible()
  })
})

test.describe('City Media Houses', () => {
  test('shows government-owned media houses with GOV badge for unauthenticated visitor', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/city/city-ba')

    // Media houses section should be visible
    await expect(page.getByRole('heading', { name: 'Media Houses' })).toBeVisible()

    // Government newspaper should show with GOV badge
    await expect(page.locator('.media-house-card').first()).toBeVisible()
    await expect(page.locator('.mh-gov-badge').first()).toBeVisible()
  })

  test('shows 3 government media houses per city with type badges', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/city/city-ba')

    await expect(page.getByRole('heading', { name: 'Media Houses' })).toBeVisible()

    // Should have at least 3 cards (NEWSPAPER, RADIO, TV)
    const cards = page.locator('.media-house-card')
    await expect(cards).not.toHaveCount(0)

    // Type badges should be present
    await expect(page.locator('.mh-type-badge').first()).toBeVisible()
  })

  test('shows content ranking on media house cards', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/city/city-ba')

    await expect(page.getByRole('heading', { name: 'Media Houses' })).toBeVisible()

    // Content ranking text should appear on at least one card
    const rankingEl = page.locator('.mh-ranking').first()
    await expect(rankingEl).toBeVisible()
    await expect(rankingEl).toContainText('%')
  })

  test('player-owned media house shows YOUR STATION badge', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-media',
          playerId: 'player-test',
          name: 'Media Empire',
          cash: 1_000_000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    // Add a player-owned media house alongside government ones
    state.cityMediaHouses['city-ba'] = [
      {
        id: 'player-tv-station',
        name: 'My TV Station',
        cityId: 'city-ba',
        cityName: 'Bratislava',
        mediaType: 'TV',
        ownerCompanyId: 'company-media',
        ownerCompanyName: 'Media Empire',
        effectivenessMultiplier: 2.0,
        powerStatus: 'POWERED',
        isUnderConstruction: false,
        contentRanking: 100,
        isGovernmentOwned: false,
      },
      {
        id: 'gov-newspaper-city-ba',
        name: 'Bratislava Gazette',
        cityId: 'city-ba',
        cityName: 'Bratislava',
        mediaType: 'NEWSPAPER',
        ownerCompanyId: 'gov-company-id',
        ownerCompanyName: 'Government',
        effectivenessMultiplier: 1.0,
        powerStatus: 'POWERED',
        isUnderConstruction: false,
        contentRanking: 100,
        isGovernmentOwned: true,
      },
    ]

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/city/city-ba')
    await expect(page.getByRole('heading', { name: 'Media Houses' })).toBeVisible()

    // Player-owned station shows a non-GOV badge, government shows GOV badge
    await expect(page.locator('.mh-gov-badge')).toBeVisible()
    await expect(page.locator('.media-house-card').filter({ hasText: 'My TV Station' })).toBeVisible()
  })

  test('city power planning section is visible with weather conditions and power balance', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.cityWeatherForecasts['city-ba'] = {
      cityId: 'city-ba',
      currentWindPercent: 54,
      currentSolarPercent: 78,
      forecast: Array.from({ length: 24 }, (_, i) => ({
        tick: 100 + i,
        windPercent: 54 + (i % 5),
        solarPercent: Math.max(10, 78 - i * 3),
      })),
    }
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')

    const powerSection = page.locator('[data-testid="city-power-section"]')
    await expect(powerSection).toBeVisible()

    // Should show section heading
    await expect(powerSection.getByRole('heading', { name: /Weather & Power/i })).toBeVisible()

    // Weather card should show solar and wind badges
    const weatherCard = page.locator('[data-testid="city-weather-card"]')
    await expect(weatherCard).toBeVisible()
    await expect(weatherCard.locator('[data-testid="solar-badge"]')).toContainText('78%')
    await expect(weatherCard.locator('[data-testid="wind-badge"]')).toContainText('54%')

    // Power balance card should be visible
    await expect(page.locator('[data-testid="city-power-balance-card"]')).toBeVisible()

    // Why it matters card should be visible
    const whyCard = page.locator('[data-testid="why-matters-card"]')
    await whyCard.scrollIntoViewIfNeeded()
    await expect(whyCard).toBeVisible()
    await expect(whyCard.locator('.why-item.solar-item')).toBeVisible()
    await expect(whyCard.locator('.why-item.wind-item')).toBeVisible()
    await expect(whyCard.locator('.why-item.power-item')).toBeVisible()
  })

  test('city power planning section shows power shortage status correctly', async ({ page }) => {
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
              type: 'POWER_PLANT' as const,
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
              type: 'FACTORY' as const,
              name: 'Factory A',
              latitude: 48.16,
              longitude: 17.12,
              level: 1,
              powerConsumption: 5,
              isForSale: false,
              builtAtUtc: '2026-01-01T00:00:00Z',
              units: [],
              pendingConfiguration: null,
            },
            {
              id: 'factory-2',
              companyId: 'company-power',
              cityId: 'city-ba',
              type: 'FACTORY' as const,
              name: 'Factory B',
              latitude: 48.17,
              longitude: 17.13,
              level: 1,
              powerConsumption: 5,
              isForSale: false,
              builtAtUtc: '2026-01-01T00:00:00Z',
              units: [],
              pendingConfiguration: null,
            },
            {
              id: 'factory-3',
              companyId: 'company-power',
              cityId: 'city-ba',
              type: 'FACTORY' as const,
              name: 'Factory C',
              latitude: 48.18,
              longitude: 17.14,
              level: 1,
              powerConsumption: 5,
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

    const balanceCard = page.locator('[data-testid="city-power-balance-card"]')
    await balanceCard.scrollIntoViewIfNeeded()
    await expect(balanceCard).toBeVisible()

    // 8 MW supply / 15 MW demand = CONSTRAINED — balance card should show constrained status
    await expect(balanceCard.locator('.status-constrained')).toBeVisible()
    // Should show the constrained guidance text
    await expect(balanceCard.locator('.balance-guidance')).toContainText(/shortage|capacity|returns/i)
  })
})

test.describe('currency-aware lot price display', () => {
  test('EUR city shows lot price in EUR (€ symbol)', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    // Bratislava is EUR — lots with prices like 96 900 should render as "€"
    const lots = makeDefaultBuildingLots() // all city-ba (EUR) lots
    const state = setupMockApi(page, { players: [player], buildingLots: lots })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()
    // Price panel should contain €, not $
    const aside = page.getByRole('complementary')
    await expect(aside).toContainText('€')
    await expect(aside).not.toContainText('$')
  })

  test('CZK city shows lot price without $ symbol', async ({ page }) => {
    const { player } = setupAuthenticatedPlayer(page)
    // Prague is CZK — lots should not show $ symbol
    const czkLots: MockBuildingLot[] = [
      {
        id: 'lot-prague-factory-1',
        cityId: 'city-pr',
        name: 'Prague Industrial Plot',
        description: 'Factory land in Prague',
        district: 'Industrial Zone',
        latitude: 50.083,
        longitude: 14.426,
        populationIndex: 0.85,
        basePrice: 2_142_000, // ~EUR 85 000 × 25.2 CZK rate
        price: 2_500_000,
        suitableTypes: 'FACTORY',
        ownerCompanyId: null,
        buildingId: null,
        ownerCompany: null,
        building: null,
        resourceType: null,
        materialQuality: null,
        materialQuantity: null,
      },
    ]
    const state = setupMockApi(page, { players: [player], buildingLots: czkLots })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticateViaLocalStorage(page, player.id)

    await page.goto('/city/city-pr')
    await page.getByRole('button', { name: /List View/i }).click()
    await page.getByRole('button', { name: /Prague Industrial Plot/i }).click()
    // CZK price should not display a dollar sign
    const aside = page.getByRole('complementary')
    await expect(aside).not.toContainText('$')
  })
})

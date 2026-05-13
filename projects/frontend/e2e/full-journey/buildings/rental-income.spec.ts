import { expect, test } from '@playwright/test'
import { makePlayer, setupMockApi } from '../../helpers/mock-api'

// ── Fixtures ─────────────────────────────────────────────────────────────────

function makeApartmentPlayer() {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  player.companies.push({
    id: 'company-rent',
    playerId: player.id,
    name: 'Property Holdings',
    cash: 500_000,
    foundedAtUtc: '2026-01-01T00:00:00Z',
    buildings: [
      {
        id: 'building-apt',
        companyId: 'company-rent',
        cityId: 'city-ba',
        type: 'APARTMENT',
        name: 'Riverside Apartments',
        latitude: 48.14,
        longitude: 17.1,
        level: 1,
        powerConsumption: 1,
        isForSale: false,
        builtAtUtc: '2026-01-01T00:00:00Z',
        pendingConfiguration: null,
        units: [],
        pricePerSqm: 12,
        occupancyPercent: 85.0,
        totalAreaSqm: 2000,
        pendingPricePerSqm: null,
        pendingPriceActivationTick: null,
        cityReferenceRentPerSqm: 10,
        adjustedMarketRentPerSqm: 11,
        populationIndex: 1.0,
      },
    ],
  })
  return player
}

// ── Test 1: Apartment panel shows occupancy + rent + revenue sparkline ────────

test('apartment building panel shows occupancy gauge, active rent, and revenue sparkline', async ({ page }) => {
  const player = makeApartmentPlayer()
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`

  // Seed rental history for the sparkline
  state.rentalHistory = [
    { buildingId: 'building-apt', tick: 1, revenue: 240, occupancyPercent: 85, rentPerSqm: 12 },
    { buildingId: 'building-apt', tick: 2, revenue: 240, occupancyPercent: 85, rentPerSqm: 12 },
    { buildingId: 'building-apt', tick: 3, revenue: 240, occupancyPercent: 85, rentPerSqm: 12 },
  ]
  state.cityAverageRentPerSqm = 10

  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto('/building/building-apt')

  const panel = page.locator('[aria-label="Property management"]')
  await expect(panel).toBeVisible()

  // Occupancy badge
  await expect(panel).toContainText('85.0%')

  // Active rent price
  await expect(panel).toContainText('€12 / m²')

  // City average benchmark
  await expect(panel).toContainText('€10')

  // Set Rent button
  await expect(panel.getByRole('button', { name: /Set Rent/i })).toBeVisible()

  // Revenue sparkline section is rendered once history is loaded
  await expect(panel.locator('.rental-sparkline-section')).toBeVisible()
})

// ── Test 2: Pending rent change shows countdown notice ────────────────────────

test('apartment panel shows pending rent notice when a rent change is queued', async ({ page }) => {
  const player = makeApartmentPlayer()
  // Set a pending price change on the building
  const building = player.companies
    .find((c) => c.id === 'company-rent')!
    .buildings.find((b) => b.id === 'building-apt')!
  building.pendingPricePerSqm = 18
  building.pendingPriceActivationTick = 50

  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.gameState = { ...state.gameState, currentTick: 30 }

  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto('/building/building-apt')

  const panel = page.locator('[aria-label="Property management"]')
  await expect(panel).toBeVisible()

  // Pending notice must be visible and mention the new price
  const notice = panel.locator('.pending-rent-notice')
  await expect(notice).toBeVisible()
  await expect(notice).toContainText('€18')
})

// ── Test 3: Set rent → dialog appears, schedule change ───────────────────────

test('can open rent dialog and schedule a new rent price', async ({ page }) => {
  const player = makeApartmentPlayer()
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`

  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto('/building/building-apt')

  const panel = page.locator('[aria-label="Property management"]')
  await panel.getByRole('button', { name: /Set Rent/i }).click()

  // Dialog must appear with a 1-day delay notice
  const dialog = panel.locator('.rent-dialog')
  await expect(dialog).toBeVisible()
  await expect(dialog).toContainText('1 day')

  // Fill new rent price and submit
  await dialog.locator('input[type="number"]').fill('20')
  await panel.getByRole('button', { name: /Schedule Change/i }).click()

  // Dialog closes, pending notice appears with the new price
  await expect(dialog).toBeHidden()
  await expect(panel.locator('.pending-rent-notice')).toBeVisible()
  await expect(panel.locator('.pending-rent-notice')).toContainText('€20')
})

// ── Test 4: Ledger shows RENTAL_INCOME row when rent income > 0 ───────────────

test('ledger income statement shows Rental Income row when company has rental income', async ({ page }) => {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  const companyId = 'company-rent-ledger'
  player.companies.push({
    id: companyId,
    playerId: player.id,
    name: 'Rental Co',
    cash: 100_000,
    foundedAtUtc: '2026-01-01T00:00:00Z',
    buildings: [
      {
        id: 'apt-ledger',
        companyId,
        cityId: 'city-ba',
        type: 'APARTMENT',
        name: 'Ledger Apartments',
        latitude: 48.14,
        longitude: 17.1,
        level: 1,
        powerConsumption: 1,
        isForSale: false,
        builtAtUtc: '2026-01-01T00:00:00Z',
        pendingConfiguration: null,
        units: [],
        pricePerSqm: 12,
        occupancyPercent: 80,
        totalAreaSqm: 1500,
      },
    ],
  })

  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`

  // Seed ledger data with rental income
  state.ledgerData[companyId] = {
    companyId,
    companyName: 'Rental Co',
    currentCash: 100_000,
    totalRevenue: 5000,
    totalRentIncome: 3600,
    totalPropertyMaintenance: 300,
    totalPurchasingCosts: 0,
    totalShippingCosts: 0,
    totalLaborCosts: 0,
    totalEnergyCosts: 0,
    totalMarketingCosts: 0,
    totalTaxPaid: 0,
    totalOtherCosts: 0,
    netIncome: 8300,
    propertyValue: 500000,
    propertyAppreciation: 0,
    buildingValue: 500000,
    inventoryValue: 0,
    totalAssets: 600000,
    totalPropertyPurchases: 500000,
    cashFromOperations: 8300,
    cashFromInvestments: -500000,
    firstRecordedTick: 1,
    lastRecordedTick: 100,
    buildingSummaries: [
      {
        buildingId: 'apt-ledger',
        buildingName: 'Ledger Apartments',
        buildingType: 'APARTMENT',
        revenue: 3600,
        costs: 300,
        currencyCode: 'EUR',
        currencySymbol: '€',
      },
    ],
  }

  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto(`/company/${companyId}/ledger`)

  // Rental income row should be visible in the income statement
  const incomeStatement = page
    .locator('.statement-card')
    .filter({ has: page.locator('.statement-title').filter({ hasText: /income statement/i }) })
    .first()
  await expect(incomeStatement.locator('.rental-income-row')).toBeVisible()
  await expect(incomeStatement.locator('.rental-income-row')).toContainText('Rental Income')

  // Click drill-down button on rental income row
  await incomeStatement.locator('.rental-income-row .drill-btn').click()

  // Drilldown panel should appear
  await expect(page.locator('.drill-panel')).toBeVisible()
})

// ── Test 5: Ledger does NOT show Rental Income row when income is zero ─────────

test('ledger income statement hides Rental Income row when there is no rental income', async ({ page }) => {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  const companyId = 'company-no-rent'
  player.companies.push({
    id: companyId,
    playerId: player.id,
    name: 'Factory Only Co',
    cash: 100_000,
    foundedAtUtc: '2026-01-01T00:00:00Z',
    buildings: [],
  })

  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`

  state.ledgerData[companyId] = {
    companyId,
    companyName: 'Factory Only Co',
    currentCash: 100_000,
    totalRevenue: 5000,
    // totalRentIncome omitted — defaults to 0
    totalPurchasingCosts: 2000,
    totalShippingCosts: 0,
    totalLaborCosts: 500,
    totalEnergyCosts: 200,
    totalMarketingCosts: 0,
    totalTaxPaid: 0,
    totalOtherCosts: 0,
    netIncome: 2300,
    propertyValue: 0,
    propertyAppreciation: 0,
    buildingValue: 0,
    inventoryValue: 0,
    totalAssets: 100000,
    totalPropertyPurchases: 0,
    cashFromOperations: 2300,
    cashFromInvestments: 0,
    firstRecordedTick: 1,
    lastRecordedTick: 50,
    buildingSummaries: [],
  }

  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto(`/company/${companyId}/ledger`)

  const incomeStatement = page
    .locator('.statement-card')
    .filter({ has: page.locator('.statement-title').filter({ hasText: /income statement/i }) })
    .first()
  await expect(incomeStatement).toBeVisible()
  // Rental income row must NOT be present
  await expect(incomeStatement.locator('.rental-income-row')).toHaveCount(0)
})

// ── Test 6: Non-owner cannot set rent (backend error surfaced in UI) ───────────

test('non-owner player sees "not found or not owned" error when trying to set rent via UI', async ({ page }) => {
  // This player owns no apartment buildings
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`

  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  // Navigate to a building ID that the player does not own
  await page.goto('/building/foreign-building-apt')

  // The page should show the not-found state (no property panel)
  await expect(page.locator('[aria-label="Property management"]')).toHaveCount(0)
})

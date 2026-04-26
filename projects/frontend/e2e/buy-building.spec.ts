import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer, makeDefaultBuildingLots } from './helpers/mock-api'

async function authenticate(page: Parameters<typeof test>[0]['page'], playerId: string) {
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${playerId}`)
}

test.describe('Buy Building View', () => {
  test('shows compatible land after selecting city and building type', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Land Corp',
          cash: 500000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-1')

    await page.getByRole('button', { name: /Factory/i }).click()
    await page.locator('.city-select-grid').getByRole('button', { name: /Bratislava/i }).click()

    const starterFactoryLot = page.getByRole('button', { name: /Factory Site B1/i })
    await expect(starterFactoryLot).toBeVisible()
    await expect(starterFactoryLot.getByText(/Population index/i)).toBeVisible()
  })

  test('purchases a selected land parcel and opens the building detail page', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Expansion Group',
          cash: 500000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-1')

    await page.getByRole('button', { name: /Factory/i }).click()
    await page.getByLabel('Building Name').fill('Danube Works')
    await page.locator('.city-select-grid').getByRole('button', { name: /Bratislava/i }).click()
    await page.getByRole('button', { name: /Factory Site B1/i }).click()
    await page.getByRole('button', { name: /^Buy Now$/i }).click()

    await page.waitForURL(/\/building\//)
    await expect(page.getByRole('heading', { name: /Danube Works/i })).toBeVisible()
  })

  test('requires media channel selection before buying a media house', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Broadcast Group',
          cash: 5000000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.buildingLots = [
      ...makeDefaultBuildingLots(),
      {
        id: 'lot-media-house-1',
        cityId: 'city-ba',
        name: 'Media House Lot A1',
        description: 'Purpose-built media complex for broadcast operations.',
        district: 'OldTown',
        latitude: 48.1492,
        longitude: 17.1077,
        populationIndex: 1.85,
        price: 120000,
        basePrice: 90000,
        suitableTypes: 'MEDIA_HOUSE',
        ownerCompanyId: null,
        buildingId: null,
      },
    ]

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-1')

    await page.getByRole('button', { name: /Media House/i }).click()
    await page.getByLabel('Building Name').fill('Pulse TV')
    await page.locator('.city-select-grid').getByRole('button', { name: /Bratislava/i }).click()
    await page.getByRole('button', { name: /Media House Lot A1/i }).click()

    const buyNowButton = page.getByRole('button', { name: /^Buy Now$/i })
    await expect(buyNowButton).toBeDisabled()

    await page.locator('#mediaType').selectOption('TV')
    await expect(buyNowButton).toBeEnabled()

    await buyNowButton.click()
    await page.waitForURL(/\/building\//)
    await expect(page.getByRole('heading', { name: /Pulse TV/i })).toBeVisible()
  })

  test('shows bank setup info panel, capital check, and rate fields when BANK type is selected', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Financial Group',
          cash: 50000000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-1')

    await page.locator('.type-card', { hasText: 'Bank' }).click()

    // Bank setup info panel should appear
    await expect(page.getByText('Setting up your bank')).toBeVisible()

    // Capital check shows sufficient funds (company has 50M, requirement is 10M)
    await expect(page.getByText('Company has sufficient funds')).toBeVisible()

    // Deposit and lending rate fields should be visible with defaults
    await expect(page.getByLabel(/Deposit Interest Rate/i)).toBeVisible()
    await expect(page.getByLabel(/Lending Interest Rate/i)).toBeVisible()
  })

  test('shows capital insufficient warning when company lacks funds for bank', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Startup Corp',
          cash: 500000, // Only 500K, needs 10M for bank
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-1')

    await page.locator('.type-card', { hasText: 'Bank' }).click()
    await page.locator('.city-select-grid').getByRole('button', { name: /Bratislava/i }).click()

    // Should show insufficient funds warning
    await expect(page.locator('.capital-warn')).toBeVisible()
    await expect(page.locator('.capital-status-warn')).toBeVisible()

    // Buy Now button should be disabled
    await page.locator('.lot-card').first().click()
    await expect(page.getByRole('button', { name: /^Buy Now$/i })).toBeDisabled()
  })

  test('pre-selects BANK type when navigating with ?type=BANK query param', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Banking Corp',
          cash: 50000000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await authenticate(page, player.id)
    // Navigate with ?type=BANK query param (as "Acquire a Bank" button does)
    await page.goto('/buy-building/company-1?type=BANK')

    // Bank type should be pre-selected and bank setup UI should be visible immediately
    await expect(page.getByText('Setting up your bank')).toBeVisible()
    await expect(page.locator('.type-card.selected', { hasText: 'Bank' })).toBeVisible()
  })

  test('purchasing a BANK lot redirects to /bank/:id, not /building/:id', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Capital Bank Group',
          cash: 50000000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-1')

    await page.locator('.type-card', { hasText: 'Bank' }).click()
    await page.locator('.city-select-grid').getByRole('button', { name: /Bratislava/i }).click()
    // Select any lot
    await page.locator('.lot-card').first().click()
    await page.getByRole('button', { name: /^Buy Now$/i }).click()

    // Should redirect to bank management page, not generic building page
    await page.waitForURL(/\/bank\//)
    await expect(page).toHaveURL(/\/bank\//)
  })

  test('shows funding gap warning when selecting Prague (CZK) with no CZK balance', async ({
    page,
  }) => {
    // AC: Expansion flow detects missing destination-currency bank accounts.
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Europe Expansion Corp',
          cash: 5000000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    // No CZK balance – player only has EUR

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-1')

    await page.getByRole('button', { name: /Factory/i }).click()
    // Scope to .city-select-grid to avoid matching account-switcher button
    await page.locator('.city-select-grid').getByText('Prague').click()

    // Funding gap warning must be visible with "missing account" message
    const guidance = page.locator('.funding-guidance')
    await expect(guidance).toBeVisible()
    await expect(guidance).toContainText('CZK')
    await expect(guidance).toContainText(/No CZK account found/i)

    // Forex and bank-statement CTAs must be present (scope to guidance panel)
    await expect(guidance.getByRole('link', { name: /Forex Exchange/i })).toBeVisible()
    await expect(guidance.getByRole('link', { name: /Bank Statement/i })).toBeVisible()
  })

  test('Buy Now is disabled when funding gap exists for non-EUR city', async ({ page }) => {
    // AC: UI blocks the purchase action when the player has no CZK balance.
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Prague Expansion Corp',
          cash: 5000000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    // No CZK balance

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-1')

    await page.getByRole('button', { name: /Factory/i }).click()
    await page.locator('.city-select-grid').getByText('Prague').click()

    // Funding gap warning must be shown before any lot selection
    await expect(page.locator('.funding-guidance')).toBeVisible()

    // Buy Now must be disabled while funding gap persists (hasFundingGap disables regardless of lot)
    await expect(page.getByRole('button', { name: /^Buy Now$/i })).toBeDisabled()
  })

  test('shows insufficient-funds warning when CZK balance exists but is below lot total cost', async ({
    page,
  }) => {
    // AC: Expansion flow detects insufficient eligible balance in destination currency.
    // Factory lot price = 90,000, construction cost = 15,000 → total = 105,000 CZK
    // Player has only 50,000 CZK → insufficient
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Prague Low Funds Corp',
          cash: 5000000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    // Player has some CZK but not enough (50,000 < lot 90,000 + construction 15,000 = 105,000)
    state.playerCurrencyBalances = [{ currencyCode: 'CZK', currencySymbol: 'Kč', balance: 50_000 }]
    state.buildingLots.push({
      id: 'lot-prague-factory-cheap',
      cityId: 'city-pr',
      name: 'Prague Starter Site',
      description: 'Affordable industrial plot in Prague.',
      district: 'Industrial Zone',
      latitude: 50.085,
      longitude: 14.445,
      populationIndex: 0.65,
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

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-1')

    await page.getByRole('button', { name: /Factory/i }).click()
    await page.locator('.city-select-grid').getByText('Prague').click()

    // Select the lot to trigger lot-total comparison
    await page.locator('.lot-card', { hasText: 'Prague Starter Site' }).click()

    // Must show "insufficient funds" variant (has CZK but not enough)
    const guidance = page.locator('.funding-guidance')
    await expect(guidance).toBeVisible()
    await expect(guidance).toContainText(/Insufficient CZK balance/i)

    // Required vs Available amounts must be shown
    await expect(guidance.locator('.amount-required')).toBeVisible()
    await expect(guidance.locator('.amount-available')).toBeVisible()
    await expect(guidance.locator('.amount-shortfall')).toBeVisible()

    // Buy Now remains disabled
    await expect(page.getByRole('button', { name: /^Buy Now$/i })).toBeDisabled()
  })

    test('Buy Now is enabled after player has sufficient CZK balance', async ({ page }) => {
    // AC: Expansion succeeds once the player is funded in the destination currency.
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Prague Funded Corp',
          cash: 5000000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    // Player has sufficient CZK balance
    state.playerCurrencyBalances = [{ currencyCode: 'CZK', currencySymbol: 'Kč', balance: 5_000_000 }]
    // Add a Prague lot so the player can select it
    state.buildingLots.push({
      id: 'lot-prague-factory',
      cityId: 'city-pr',
      name: 'Prague Factory Site',
      description: 'Industrial site in Prague.',
      district: 'Industrial Zone',
      latitude: 50.08,
      longitude: 14.44,
      populationIndex: 0.7,
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

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-1')

    await page.getByRole('button', { name: /Factory/i }).click()
    await page.locator('.city-select-grid').getByText('Prague').click()

    // Funding gap warning must NOT be shown
    await expect(page.locator('.funding-guidance')).toBeHidden()

    // Select the Prague lot and verify Buy Now is enabled
    await page.locator('.lot-card', { hasText: 'Prague Factory Site' }).click()
    await expect(page.getByRole('button', { name: /^Buy Now$/i })).toBeEnabled()
  })

  test('does not show funding gap warning for EUR city (Bratislava)', async ({ page }) => {
    // AC: Expansion flow does not show a false positive for EUR cities.
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Central Europe Corp',
          cash: 500000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-1')

    await page.getByRole('button', { name: /Factory/i }).click()
    // Scope to city-select-grid to avoid matching account-switcher button
    await page.locator('.city-select-grid').getByText('Bratislava').click()

    // No funding gap warning for EUR city
    await expect(page.locator('.funding-guidance')).toBeHidden()
  })
})

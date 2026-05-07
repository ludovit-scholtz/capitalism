import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer, makeDefaultBuildingLots } from '../../helpers/mock-api'

async function authenticate(page: Parameters<typeof test>[0]['page'], playerId: string) {
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${playerId}`)
}

async function switchCityViaContextSwitcher(page: Parameters<typeof test>[0]['page'], cityName: 'Bratislava' | 'Prague' | 'Vienna') {
  await page.locator('.ctx-trigger').click()
  await page.locator('.ctx-city-option', { hasText: cityName }).click()
  await expect(page.locator('.ctx-trigger .ctx-city-name')).toContainText(cityName)
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
    await switchCityViaContextSwitcher(page, 'Bratislava')

    const starterFactoryLot = page.getByRole('button', { name: /Factory Site B1/i })
    await expect(starterFactoryLot).toBeVisible()
    await expect(starterFactoryLot.getByText(/Population index/i)).toBeVisible()
  })

  test('shows apartment property size in land cards and selected summary', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Housing Group',
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

    await page.getByRole('button', { name: /Apartment/i }).click()
    await switchCityViaContextSwitcher(page, 'Bratislava')

    const apartmentLot = page.getByRole('button', { name: /Riverside Apartment Block/i })
    await expect(apartmentLot).toBeVisible()
    await expect(apartmentLot.getByText(/Property size:/i)).toBeVisible()
    await expect(apartmentLot.getByText(/1,800 m²/i)).toBeVisible()

    await apartmentLot.click()
    await expect(page.getByText(/Property size:/i).nth(1)).toBeVisible()
    await expect(page.getByText(/1,800 m²/i).nth(1)).toBeVisible()
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
    await switchCityViaContextSwitcher(page, 'Bratislava')
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
    await switchCityViaContextSwitcher(page, 'Bratislava')
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
    state.myBankAccounts = [
      {
        id: 'financial-group-bank-account',
        accountNumber: '4000000000000001',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 50_000_000,
        companyId: 'company-1',
        companyName: 'Financial Group',
        ownerType: 'COMPANY',
        ownerDisplayName: 'Financial Group',
        cityId: 'city-ba',
      },
    ]

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
    await switchCityViaContextSwitcher(page, 'Bratislava')

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
    state.myBankAccounts = [
      {
        id: 'banking-corp-bank-account',
        accountNumber: '4000000000000002',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 50_000_000,
        companyId: 'company-1',
        companyName: 'Banking Corp',
        ownerType: 'COMPANY',
        ownerDisplayName: 'Banking Corp',
        cityId: 'city-ba',
      },
    ]

    await authenticate(page, player.id)
    // Navigate with ?type=BANK query param (as "Acquire a Bank" button does)
    await page.goto('/buy-building/company-1?type=BANK')

    // Bank type should be pre-selected and bank setup UI should be visible immediately
    await expect(page.getByText('Setting up your bank')).toBeVisible()
    await expect(page.getByLabel(/Deposit Interest Rate/i)).toBeVisible()
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
    state.myBankAccounts = [
      {
        id: 'company-1-eur-bank',
        accountNumber: '4000000000000003',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 50_000_000,
        companyId: 'company-1',
        companyName: 'Capital Bank Group',
        ownerType: 'COMPANY',
        ownerDisplayName: 'Capital Bank Group',
        cityId: 'city-ba',
      },
    ]

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-1')

    await page.locator('.type-card', { hasText: 'Bank' }).click()
    await switchCityViaContextSwitcher(page, 'Bratislava')
    // Select any lot
    await page.locator('.lot-card').first().click()
    await page.getByRole('button', { name: /^Buy Now$/i }).click()

    // Should redirect to bank management page, not generic building page
    await page.waitForURL(/\/bank\//)
    await expect(page).toHaveURL(/\/bank\//)
  })

  test('shows funding gap warning when selecting Prague (CZK) with no CZK balance', async ({ page }) => {
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
    await switchCityViaContextSwitcher(page, 'Prague')

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
    await switchCityViaContextSwitcher(page, 'Prague')

    // Funding gap warning must be shown before any lot selection
    await expect(page.locator('.funding-guidance')).toBeVisible()

    // Buy Now must be disabled while funding gap persists (hasFundingGap disables regardless of lot)
    await expect(page.getByRole('button', { name: /^Buy Now$/i })).toBeDisabled()
  })

  test('shows insufficient-funds warning when CZK balance exists but is below lot total cost', async ({ page }) => {
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
    state.myBankAccounts = [
      {
        id: 'company-1-czk-bank-low',
        accountNumber: '4000000000000004',
        currencyCode: 'CZK',
        currencySymbol: 'Kč',
        balance: 50_000,
        companyId: 'company-1',
        companyName: 'Prague Low Funds Corp',
        ownerType: 'COMPANY',
        ownerDisplayName: 'Prague Low Funds Corp',
        cityId: 'city-pr',
      },
    ]
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
    await switchCityViaContextSwitcher(page, 'Prague')

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
    state.myBankAccounts = [
      {
        id: 'company-1-czk-bank-funded',
        accountNumber: '4000000000000005',
        currencyCode: 'CZK',
        currencySymbol: 'Kč',
        balance: 5_000_000,
        companyId: 'company-1',
        companyName: 'Prague Funded Corp',
        ownerType: 'COMPANY',
        ownerDisplayName: 'Prague Funded Corp',
        cityId: 'city-pr',
      },
    ]
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
    await switchCityViaContextSwitcher(page, 'Prague')

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
    await switchCityViaContextSwitcher(page, 'Bratislava')

    // No funding gap warning for EUR city
    await expect(page.locator('.funding-guidance')).toBeHidden()
  })

  test('pre-selects active city from navbar when selected_city_id is stored', async ({ page }) => {
    // AC: Buy-building uses the active city from the city navbar/filter by default
    // instead of asking the user to choose the city again.
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Prague Auto Corp',
          cash: 5000000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [
            {
              id: 'prague-factory-1',
              companyId: 'company-1',
              cityId: 'city-pr',
              type: 'FACTORY',
              name: 'Prague Anchor Factory',
              latitude: 50.08,
              longitude: 14.44,
              level: 1,
              powerConsumption: 10,
              isForSale: false,
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
    // Seed a Prague factory lot so lots load for Prague
    state.buildingLots.push({
      id: 'lot-prague-auto',
      cityId: 'city-pr',
      name: 'Prague Auto Factory',
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

    // Set Prague as the active city in localStorage before the page loads
    await page.addInitScript(
      (params) => {
        localStorage.setItem('auth_token', params.token)
        localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
        localStorage.setItem('selected_city_id', params.cityId)
      },
      { token: `token-${player.id}`, cityId: 'city-pr' },
    )

    await page.goto('/buy-building/company-1')

    // Select Factory type — city should already be pre-selected as Prague
    await page.getByRole('button', { name: /Factory/i }).click()

    // Active city in context switcher should resolve to Prague from persisted selection
    await expect(page.locator('.ctx-trigger .ctx-city-name')).toContainText('Prague')

    // Lots for Prague should load automatically without manual city click
    await expect(page.getByRole('button', { name: /Prague Auto Factory/i })).toBeVisible()
  })

  test('active city currency is displayed correctly in context switcher', async ({ page }) => {
    // AC: Context selection shows the correct currency for the active city (Prague = CZK, not USD).
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Prague Corp',
          cash: 5000000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [
            {
              id: 'prague-factory-2',
              companyId: 'company-1',
              cityId: 'city-pr',
              type: 'FACTORY',
              name: 'Prague Currency Factory',
              latitude: 50.08,
              longitude: 14.44,
              level: 1,
              powerConsumption: 10,
              isForSale: false,
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

    // Set Prague as the active city
    await page.addInitScript(
      (params) => {
        localStorage.setItem('auth_token', params.token)
        localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
        localStorage.setItem('selected_city_id', params.cityId)
      },
      { token: `token-${player.id}`, cityId: 'city-pr' },
    )

    await page.goto('/dashboard')

    // Open the context switcher panel
    await page.locator('.ctx-trigger').click()

    // The company cash must be formatted in CZK, not USD
    const companyOption = page.locator('.ctx-account-option', { hasText: 'Prague Corp' })
    await expect(companyOption).toBeVisible()
    const cashLabel = companyOption.locator('.ctx-acc-cash')
    await expect(cashLabel).toBeVisible()
    // CZK amounts are displayed with Kč or CZK symbol, not $
    const cashText = await cashLabel.textContent()
    expect(cashText).not.toMatch(/\$/)
    expect(cashText).toMatch(/CZK|Kč/)
  })
})

test.describe('Buy Building — Mining lot resource display', () => {
  function setupMineTestPlayer() {
    return makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-mine',
          playerId: 'player-1',
          name: 'Iron Empire Corp',
          cash: 50_000_000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
  }

  test('mine lot card shows resource badge with material name', async ({ page }) => {
    const player = setupMineTestPlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-mine')

    await page.getByRole('button', { name: /Mine/i }).click()
    await switchCityViaContextSwitcher(page, 'Bratislava')

    // Industrial Plot A1 has Iron Ore resource — badge should be visible on the lot card
    const lotCard = page.locator('.lot-card', { hasText: 'Industrial Plot A1' })
    await expect(lotCard).toBeVisible()
    const badge = lotCard.locator('[data-testid="buy-building-resource-badge"]')
    await expect(badge).toBeVisible()
    await expect(badge).toContainText(/Iron Ore/i)
  })

  test('non-mine lot card does not show resource badge', async ({ page }) => {
    const player = setupMineTestPlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-mine')

    await page.getByRole('button', { name: /Factory/i }).click()
    await switchCityViaContextSwitcher(page, 'Bratislava')

    // Factory Site B1 has no resource — no badge should be shown
    const lotCard = page.locator('.lot-card', { hasText: 'Factory Site B1' })
    await expect(lotCard).toBeVisible()
    await expect(lotCard.locator('[data-testid="buy-building-resource-badge"]')).toHaveCount(0)
  })

  test('selecting mine lot shows mining deposit summary with resource quality and quantity', async ({ page }) => {
    const player = setupMineTestPlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-mine')

    await page.getByRole('button', { name: /Mine/i }).click()
    await switchCityViaContextSwitcher(page, 'Bratislava')

    // Select the mine lot with Iron Ore
    await page.locator('.lot-card', { hasText: 'Industrial Plot A1' }).click()

    // Mining deposit summary must appear
    const summary = page.locator('[data-testid="buy-building-mining-summary"]')
    await expect(summary).toBeVisible()

    // Must show resource name
    await expect(summary).toContainText(/Iron Ore/i)

    // Must show quality percentage
    await expect(summary).toContainText(/72%/)

    // Must show quantity
    await expect(summary).toContainText(/18/)
  })

  test('mine lot cards show material quality and quantity instead of population index', async ({ page }) => {
    const player = setupMineTestPlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-mine')

    await page.getByRole('button', { name: /Mine/i }).click()
    await switchCityViaContextSwitcher(page, 'Bratislava')

    const mineLotCard = page.locator('.lot-card', { hasText: 'Industrial Plot A1' })
    await expect(mineLotCard).toBeVisible()
    await expect(mineLotCard).toContainText(/72%/)
    await expect(mineLotCard).toContainText(/18,?000/)
    await expect(mineLotCard).not.toContainText(/Population/i)

    await mineLotCard.click()
    const selectedSummary = page.locator('[data-testid="buy-building-mining-summary"]')
    await expect(selectedSummary).toContainText(/72%/)
    await expect(selectedSummary).toContainText(/18,?000/)
  })

  test('selecting mine lot shows resource premium in asking price area', async ({ page }) => {
    const player = setupMineTestPlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-mine')

    await page.getByRole('button', { name: /Mine/i }).click()
    await switchCityViaContextSwitcher(page, 'Bratislava')
    await page.locator('.lot-card', { hasText: 'Industrial Plot A1' }).click()

    // Resource premium badge should be visible in the selected lot summary
    const premiumBadge = page.locator('.buy-building-resource-premium-badge')
    await expect(premiumBadge).toBeVisible()
    await expect(premiumBadge).toContainText(/resource/i)
  })

  test('mining deposit summary is hidden when Factory type selected on mine lot', async ({ page }) => {
    const player = setupMineTestPlayer()
    const lots = makeDefaultBuildingLots()
    // Make Industrial Plot A1 also support FACTORY
    const state = setupMockApi(page, { players: [player], buildingLots: lots })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-mine')

    // Select Factory type — the mine lot also supports FACTORY
    await page.getByRole('button', { name: /Factory/i }).click()
    await switchCityViaContextSwitcher(page, 'Bratislava')
    await page.locator('.lot-card', { hasText: 'Industrial Plot A1' }).click()

    // Deposit summary should NOT appear when building type is not MINE
    await expect(page.locator('[data-testid="buy-building-mining-summary"]')).toHaveCount(0)
  })

  test('mine lot list can be filtered by resource type', async ({ page }) => {
    const player = setupMineTestPlayer()
    const lots = makeDefaultBuildingLots()
    lots.push({
      id: 'lot-industrial-chem-1',
      cityId: 'city-ba',
      name: 'Industrial Plot C1',
      description: 'Heavy-industry lot above a Chemical Minerals deposit.',
      district: 'Industrial Zone',
      latitude: 48.153,
      longitude: 17.128,
      populationIndex: 0.66,
      basePrice: 80000,
      price: 25480000,
      suitableTypes: 'FACTORY,MINE',
      ownerCompanyId: null,
      buildingId: null,
      ownerCompany: null,
      building: null,
      resourceType: { id: 'res-chem', name: 'Chemical Minerals', slug: 'chemical-minerals' },
      materialQuality: 0.64,
      materialQuantity: 16000,
    })

    const state = setupMockApi(page, { players: [player], buildingLots: lots })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-mine')

    await page.getByRole('button', { name: /Mine/i }).click()
    await switchCityViaContextSwitcher(page, 'Bratislava')

    // Two mine resources are visible before filtering.
    await expect(page.locator('.lot-card', { hasText: 'Industrial Plot A1' })).toBeVisible()
    await expect(page.locator('.lot-card', { hasText: 'Industrial Plot C1' })).toBeVisible()

    // Filter to Iron Ore and verify only iron lot remains.
    await page.locator('.resource-filter-btn', { hasText: /Iron Ore/i }).click()
    await expect(page.locator('.lot-card', { hasText: 'Industrial Plot A1' })).toBeVisible()
    await expect(page.locator('.lot-card', { hasText: 'Industrial Plot C1' })).toHaveCount(0)
  })

  test('asking price shows premium range (>€15M) for premium mine lot', async ({ page }) => {
    const player = setupMineTestPlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-mine')

    await page.getByRole('button', { name: /Mine/i }).click()
    await switchCityViaContextSwitcher(page, 'Bratislava')

    // The Industrial Plot A1 mock has a price of ~32 million — must show large currency amount
    const lotCard = page.locator('.lot-card', { hasText: 'Industrial Plot A1' })
    await expect(lotCard).toBeVisible()
    // Price text should contain millions indicator (M or large number)
    await expect(lotCard).toContainText(/32/)
  })
})

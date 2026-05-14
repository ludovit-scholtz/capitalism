import { test, expect } from '@playwright/test'
import {
  setupMockApi,
  makePlayer,
  type MockPlayer,
  type MockBuildingMarketListing,
  type MockBuildingMarketMyListing,
  type MockBuildingMarketOffer,
} from '../../helpers/mock-api'

const BRATISLAVA = { id: 'city-ba', name: 'Bratislava', currencyCode: 'EUR', countryCode: 'SK' }
const PRAGUE = { id: 'city-pr', name: 'Prague', currencyCode: 'CZK', countryCode: 'CZ' }

function makePlayerWithCompany(): MockPlayer {
  return makePlayer({
    onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
    activeAccountType: 'COMPANY',
    activeCompanyId: 'co-1',
    companies: [
      {
        id: 'co-1',
        playerId: 'player-1',
        name: 'My Corp',
        cash: 1000000,
        foundedAtUtc: '2026-01-01T00:00:00Z',
        buildings: [],
      },
    ],
  })
}

function makeMarketListing(overrides?: Partial<MockBuildingMarketListing['building']>): MockBuildingMarketListing {
  return {
    pendingOfferCount: 2,
    building: {
      id: 'bldg-1',
      name: 'Iron Mine Alpha',
      type: 'MINE',
      isForSale: true,
      askingPrice: 500000,
      level: 2,
      city: BRATISLAVA,
      company: { id: 'co-seller', name: 'Seller Corp', player: { displayName: 'Alice' } },
      ...overrides,
    },
  }
}

function makeMyListing(overrides?: Partial<MockBuildingMarketMyListing>): MockBuildingMarketMyListing {
  return {
    building: {
      id: 'bldg-2',
      name: 'Bread Factory',
      type: 'FACTORY',
      isForSale: true,
      askingPrice: 300000,
      level: 1,
      city: BRATISLAVA,
      company: { id: 'co-mine', name: 'My Corp' },
    },
    offers: [],
    ...overrides,
  }
}

function makePendingOffer(id = 'offer-1', price = 280000): MockBuildingMarketOffer {
  return {
    id,
    offerVersion: '11111111-1111-1111-1111-111111111111',
    offeredPrice: price,
    status: 'PENDING',
    negotiationNote: 'Please accept my offer',
    createdAtUtc: '2026-01-01T10:00:00Z',
    resolvedAtUtc: null,
    buyerPlayer: { displayName: 'Bob' },
    buyerCompany: { id: 'co-buyer', name: 'Buyer Corp' },
  }
}

test('shows Building Market title on market page', async ({ page }) => {
  setupMockApi(page, { buildingMarketListings: [] })
  await page.goto('/buildings/market')
  await expect(page.getByRole('heading', { name: 'Building Market' })).toBeVisible()
})

test('shows empty state when no listings are available', async ({ page }) => {
  setupMockApi(page, { buildingMarketListings: [] })
  await page.goto('/buildings/market')
  await expect(page.getByText(/no buildings are currently listed for sale/i)).toBeVisible()
})

test('shows market listing card with building info', async ({ page }) => {
  setupMockApi(page, { buildingMarketListings: [makeMarketListing()] })
  await page.goto('/buildings/market')
  await expect(page.locator('.market-listing-card').first()).toBeVisible()
  await expect(page.locator('.building-name').first()).toContainText('Iron Mine Alpha')
  await expect(page.locator('.asking-price').first()).toContainText('€500,000')
  await expect(page.locator('.for-sale-badge').first()).toBeVisible()
})

test('shows collateral lock badge and disables make offer for collateralized listings', async ({ page }) => {
  const player = makePlayerWithCompany()
  const state = setupMockApi(page, {
    players: [player],
    buildingMarketListings: [makeMarketListing({ isCollateralized: true, foreclosureTicksRemaining: 3 })],
  })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto('/buildings/market')
  await expect(page.locator('.collateral-locked-badge')).toBeVisible()
  await expect(page.locator('.market-listing-card').first()).toContainText('Destruction in 3 ticks')
  await expect(page.getByRole('button', { name: 'Make Offer' })).toBeDisabled()
})

test('shows seller and pending offer count', async ({ page }) => {
  setupMockApi(page, {
    buildingMarketListings: [makeMarketListing()],
  })
  await page.goto('/buildings/market')
  await expect(page.locator('.market-listing-card').first()).toContainText('Seller Corp')
  await expect(page.locator('.market-listing-card').first()).toContainText('2')
})

test('shows make offer button for authenticated users', async ({ page }) => {
  const player = makePlayerWithCompany()
  const state = setupMockApi(page, {
    players: [player],
    buildingMarketListings: [makeMarketListing()],
  })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)
  await page.goto('/buildings/market')
  await expect(page.getByRole('button', { name: 'Make Offer' })).toBeVisible()
})

test('player A cannot list player B building for sale via GraphQL mutation', async ({ page }) => {
  const owner = makePlayer({
    id: 'owner-player',
    email: 'owner@example.com',
    displayName: 'Owner',
    onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
    activeAccountType: 'COMPANY',
    activeCompanyId: 'owner-company',
    companies: [
      {
        id: 'owner-company',
        playerId: 'owner-player',
        name: 'Owner Corp',
        cash: 1000000,
        foundedAtUtc: '2026-01-01T00:00:00Z',
        buildings: [
          {
            id: 'owner-building',
            companyId: 'owner-company',
            cityId: BRATISLAVA.id,
            name: 'Owner Factory',
            type: 'FACTORY',
            latitude: 48.1486,
            longitude: 17.1077,
            level: 1,
            powerConsumption: 0,
            isForSale: false,
            units: [],
          },
        ],
      },
    ],
  })
  const viewer = makePlayer({
    id: 'viewer-player',
    email: 'viewer@example.com',
    displayName: 'Viewer',
    onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
    activeAccountType: 'COMPANY',
    activeCompanyId: 'viewer-company',
    companies: [
      {
        id: 'viewer-company',
        playerId: 'viewer-player',
        name: 'Viewer Corp',
        cash: 1000000,
        foundedAtUtc: '2026-01-01T00:00:00Z',
        buildings: [],
      },
    ],
  })

  const state = setupMockApi(page, {
    players: [owner, viewer],
    buildingMarketListings: [makeMarketListing({ id: 'owner-building', company: { id: 'owner-company', name: 'Owner Corp', player: { displayName: 'Owner' } } })],
  })
  state.currentUserId = viewer.id
  state.currentToken = `token-${viewer.id}`
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${viewer.id}`)

  await page.goto('/buildings/market')

  const result = await page.evaluate(async () => {
    const response = await fetch('/graphql', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        query: `
          mutation SetForSale($input: SetBuildingForSaleInput!) {
            setBuildingForSale(input: $input) { id }
          }
        `,
        variables: {
          input: {
            buildingId: 'owner-building',
            isForSale: true,
            askingPrice: 500000,
          },
        },
      }),
    })

    return response.json()
  })

  expect(result.errors?.length ?? 0).toBeGreaterThan(0)
  expect(result.errors[0].message).toContain('Building not found')
  expect(result.errors[0].extensions?.code).toBe('BUILDING_NOT_FOUND')
})

test('hides make offer button for guests', async ({ page }) => {
  setupMockApi(page, { buildingMarketListings: [makeMarketListing()] })
  await page.goto('/buildings/market')
  await expect(page.getByRole('button', { name: 'Make Offer' })).toHaveCount(0)
})

test('opens offer modal when clicking make offer', async ({ page }) => {
  const player = makePlayerWithCompany()
  const state = setupMockApi(page, {
    players: [player],
    buildingMarketListings: [makeMarketListing()],
  })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)
  await page.goto('/buildings/market')
  await page.getByRole('button', { name: 'Make Offer' }).click()
  await expect(page.locator('.modal-panel')).toBeVisible()
  await expect(page.locator('.modal-building-name')).toContainText('Iron Mine Alpha')
})

test('submits offer and shows success message', async ({ page }) => {
  const player = makePlayerWithCompany()
  const state = setupMockApi(page, {
    players: [player],
    buildingMarketListings: [makeMarketListing()],
  })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)
  await page.goto('/buildings/market')
  await expect(page.getByRole('button', { name: 'Make Offer' })).toBeVisible()
  await page.getByRole('button', { name: 'Make Offer' }).click()
  await expect(page.locator('.modal-panel')).toBeVisible()
  // Offer amount defaults to asking price; click submit
  await expect(page.getByRole('button', { name: 'Submit Offer' })).toBeEnabled()
  await page.getByRole('button', { name: 'Submit Offer' }).click()
  await expect(page.locator('.alert-success')).toBeVisible()
  await expect(page.locator('.modal-panel')).toHaveCount(0)
})

test('my listings tab shows empty state for authenticated user with no listings', async ({ page }) => {
  const player = makePlayerWithCompany()
  const state = setupMockApi(page, {
    players: [player],
    myBuildingListings: [],
  })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)
  await page.goto('/buildings/market')
  await page.getByRole('tab', { name: 'My Listings' }).click()
  await expect(page.getByText(/you have no buildings listed for sale/i)).toBeVisible()
})

test('my listings tab shows listing with pending offer', async ({ page }) => {
  const player = makePlayerWithCompany()
  const listing = makeMyListing({ offers: [makePendingOffer()] })
  const state = setupMockApi(page, {
    players: [player],
    myBuildingListings: [listing],
  })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)
  await page.goto('/buildings/market')
  await page.getByRole('tab', { name: 'My Listings' }).click()
  await expect(page.locator('.my-listing-card')).toBeVisible()
  await expect(page.locator('.offer-row').first()).toBeVisible()
  await expect(page.locator('.offer-row').first()).toContainText('Buyer Corp')
})

test('can accept a pending offer from my listings tab', async ({ page }) => {
  const player = makePlayerWithCompany()
  const listing = makeMyListing({ offers: [makePendingOffer()] })
  const state = setupMockApi(page, {
    players: [player],
    myBuildingListings: [listing],
  })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)
  await page.goto('/buildings/market')
  await page.getByRole('tab', { name: 'My Listings' }).click()
  await expect(page.locator('.offer-row')).toBeVisible()
  await page.getByRole('button', { name: 'Accept' }).click()
  await expect(page.locator('.alert-success')).toBeVisible()
  await expect(page.locator('.alert-success')).toContainText('accepted')
})

test('can reject a pending offer from my listings tab', async ({ page }) => {
  const player = makePlayerWithCompany()
  const listing = makeMyListing({ offers: [makePendingOffer()] })
  const state = setupMockApi(page, {
    players: [player],
    myBuildingListings: [listing],
  })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)
  await page.goto('/buildings/market')
  await page.getByRole('tab', { name: 'My Listings' }).click()
  await expect(page.locator('.offer-row')).toBeVisible()
  await page.getByRole('button', { name: 'Reject' }).click()
  await expect(page.locator('.alert-success')).toBeVisible()
  await expect(page.locator('.alert-success')).toContainText('rejected')
})

test('shows conflict message and refreshes listings on stale offer version', async ({ page }) => {
  const player = makePlayerWithCompany()
  const conflictOffer = makePendingOffer('offer-conflict', 275000)
  const listing = makeMyListing({ offers: [conflictOffer] })
  const state = setupMockApi(page, {
    players: [player],
    myBuildingListings: [listing],
  })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto('/buildings/market')
  await page.getByRole('tab', { name: 'My Listings' }).click()

  conflictOffer.offerVersion = '22222222-2222-2222-2222-222222222222'
  await page.getByRole('button', { name: 'Accept' }).click()

  await expect(page.locator('.alert-error')).toContainText('refreshing market')
})

test('shows collateral lock warning when accepting an offer fails with collateral lock code', async ({ page }) => {
  const player = makePlayerWithCompany()
  const listing = makeMyListing({ offers: [makePendingOffer('offer-locked', 275000)] })
  const state = setupMockApi(page, {
    players: [player],
    myBuildingListings: [listing],
  })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.route('**/graphql', async (route) => {
    const body = route.request().postDataJSON() as { query?: string }
    if (body.query?.includes('acceptBuildingOffer')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          errors: [
            {
              message: 'Building is locked as loan collateral.',
              extensions: { code: 'BUILDING_LOCKED_AS_COLLATERAL' },
            },
          ],
          data: null,
        }),
      })
      return
    }
    await route.fallback()
  })

  await page.goto('/buildings/market')
  await page.getByRole('tab', { name: 'My Listings' }).click()
  await page.getByRole('button', { name: 'Accept' }).click()

  await expect(page.locator('.alert-error')).toContainText(
    'currently locked as loan collateral',
  )
})

test('shows below-floor rejection message when accepted offer violates settlement floor', async ({ page }) => {
  const player = makePlayerWithCompany()
  const listing = makeMyListing({ offers: [makePendingOffer('offer-floor', 275000)] })
  const state = setupMockApi(page, {
    players: [player],
    myBuildingListings: [listing],
  })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.route('**/graphql', async (route) => {
    const body = route.request().postDataJSON() as { query?: string }
    if (body.query?.includes('acceptBuildingOffer')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          errors: [
            {
              message: 'Offer is below the minimum sale floor (350000.00 EUR).',
              extensions: { code: 'OFFER_BELOW_FLOOR', minimumSaleFloor: 350000, currencyCode: 'EUR' },
            },
          ],
          data: null,
        }),
      })
      return
    }
    await route.fallback()
  })

  await page.goto('/buildings/market')
  await page.getByRole('tab', { name: 'My Listings' }).click()
  await page.getByRole('button', { name: 'Accept' }).click()

  await expect(page.locator('.alert-error')).toContainText('minimum sale floor')
})

test('accept button is disabled during offer submission', async ({ page }) => {
  const player = makePlayerWithCompany()
  const listing = makeMyListing({ offers: [makePendingOffer('offer-slow', 290000)] })
  const state = setupMockApi(page, {
    players: [player],
    myBuildingListings: [listing],
  })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.route('**/graphql', async (route) => {
    const body = route.request().postDataJSON() as {
      query?: string
      variables?: { input?: { offerId?: string; offerVersion?: string } }
    }
    if (body.query?.includes('acceptBuildingOffer')) {
      await new Promise((resolve) => setTimeout(resolve, 250))
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            acceptBuildingOffer: {
              building: { id: 'b-1', name: 'Test Building', companyId: 'co-2', isForSale: false },
              offer: { id: body.variables?.input?.offerId ?? 'offer-slow', status: 'ACCEPTED' },
            },
          },
        }),
      })
      return
    }
    await route.fallback()
  })

  await page.goto('/buildings/market')
  await page.getByRole('tab', { name: 'My Listings' }).click()
  const acceptButton = page.getByRole('button', { name: 'Accept' })
  await acceptButton.click()
  await expect(page.locator('.offer-row').first().getByRole('button', { name: 'Processing…' }).first()).toBeDisabled()
  await expect(page.locator('.alert-success')).toContainText('accepted')
})

test('shows CZK asking price for Prague listing', async ({ page }) => {
  const pragueListing = makeMarketListing({
    id: 'bldg-prag',
    name: 'Czech Factory',
    city: PRAGUE,
    askingPrice: 12000000,
  })
  setupMockApi(page, { buildingMarketListings: [pragueListing] })
  await page.goto('/buildings/market')
  await expect(page.locator('.asking-price').first()).toContainText('CZK')
})

test('shows multiple listings in a grid', async ({ page }) => {
  const listings = [
    makeMarketListing({ id: 'b1', name: 'Mine A' }),
    makeMarketListing({ id: 'b2', name: 'Mine B' }),
    makeMarketListing({ id: 'b3', name: 'Factory C', type: 'FACTORY' }),
  ]
  setupMockApi(page, { buildingMarketListings: listings })
  await page.goto('/buildings/market')
  await expect(page.locator('.market-listing-card')).toHaveCount(3)
  await expect(page.locator('.building-name').nth(0)).toContainText('Mine A')
  await expect(page.locator('.building-name').nth(1)).toContainText('Mine B')
  await expect(page.locator('.building-name').nth(2)).toContainText('Factory C')
})

test('unlist building removes it from market listings', async ({ page }) => {
  const player = makePlayerWithCompany()
  const state = setupMockApi(page, {
    players: [player],
    myBuildingListings: [
      makeMyListing({
        building: {
          id: 'bldg-listed',
          name: 'Listed Factory',
          type: 'FACTORY',
          isForSale: true,
          askingPrice: 400000,
          level: 1,
          city: BRATISLAVA,
          company: { id: 'co-1', name: 'My Corp' },
        },
      }),
    ],
  })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto('/buildings/market')
  await page.getByRole('tab', { name: 'My Listings' }).click()

  // Listing should be visible initially
  await expect(page.locator('.my-listing-card').first()).toBeVisible()

  // Simulate unlist: update mock state and reload
  state.myBuildingListings = []
  await page.reload()
  await page.getByRole('tab', { name: 'My Listings' }).click()
  await expect(page.getByText(/you have no buildings listed for sale/i)).toBeVisible()
})

test('market shows two listings from different cities', async ({ page }) => {
  const listings = [
    makeMarketListing({ id: 'b1', name: 'BA Mine', city: BRATISLAVA }),
    makeMarketListing({ id: 'b2', name: 'PR Mine', city: PRAGUE }),
  ]
  setupMockApi(page, { buildingMarketListings: listings })
  await page.goto('/buildings/market')

  // Both listings shown initially
  await expect(page.locator('.market-listing-card')).toHaveCount(2)
})

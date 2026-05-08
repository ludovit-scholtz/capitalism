import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer } from '../../helpers/mock-api'

async function authenticate(page: Parameters<typeof test>[0]['page'], token: string) {
  await page.addInitScript((value) => {
    localStorage.setItem('auth_token', value)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, token)
}

async function seedSelectedCity(page: Parameters<typeof test>[0]['page'], cityId: string) {
  await page.addInitScript((storedCityId) => {
    localStorage.setItem('selected_city_id', storedCityId)
  }, cityId)
}

function makeBuilding(id: string, companyId: string, cityId: string, type: string, name: string) {
  return {
    id,
    companyId,
    cityId,
    type,
    name,
    latitude: 48.1486,
    longitude: 17.1077,
    level: 1,
    powerConsumption: 10,
    isForSale: false,
    units: [],
    pendingConfiguration: null,
  }
}

function makeMainCityPlayer() {
  return makePlayer({
    email: 'existing@test.com',
    password: 'TestPass1!',
    onboardingCompletedAtUtc: '2026-01-01T12:00:00Z',
    onboardingCityId: 'city-ba',
    companies: [
      {
        id: 'comp-main',
        playerId: 'player-1',
        name: 'Main City Industries',
        cash: 500000,
        foundedAtUtc: '2026-01-01T00:00:00Z',
        buildings: [
          makeBuilding('factory-ba-1', 'comp-main', 'city-ba', 'FACTORY', 'Bratislava Factory 1'),
          makeBuilding('factory-ba-2', 'comp-main', 'city-ba', 'FACTORY', 'Bratislava Factory 2'),
          makeBuilding('shop-pr-1', 'comp-main', 'city-pr', 'SALES_SHOP', 'Prague Shop'),
        ],
      },
    ],
  })
}

function makeMixedCityPlayer() {
  return makePlayer({
    email: 'mixed@test.com',
    password: 'TestPass1!',
    onboardingCompletedAtUtc: '2026-01-01T12:00:00Z',
    onboardingCityId: 'city-ba',
    companies: [
      {
        id: 'comp-mixed',
        playerId: 'player-1',
        name: 'Mixed City Holdings',
        cash: 500000,
        foundedAtUtc: '2026-01-01T00:00:00Z',
        buildings: [
          makeBuilding('factory-ba-1', 'comp-mixed', 'city-ba', 'FACTORY', 'Bratislava Factory'),
          makeBuilding('shop-pr-1', 'comp-mixed', 'city-pr', 'SALES_SHOP', 'Prague Shop'),
        ],
      },
    ],
  })
}

function makeOidcToken(nonce: string) {
  const header = Buffer.from(JSON.stringify({ alg: 'none', typ: 'JWT' })).toString('base64url')
  const payload = Buffer.from(
    JSON.stringify({
      nonce,
      iss: 'https://google.biatec.io',
      aud: 'capitalism',
      exp: Math.floor(Date.now() / 1000) + 3600,
    }),
  ).toString('base64url')

  return `${header}.${payload}.signature`
}

function addPlayerShareholding(state: ReturnType<typeof setupMockApi>, playerId: string, companyId: string, shareCount = 10000) {
  state.shareholdings.push({
    companyId,
    ownerPlayerId: playerId,
    ownerCompanyId: null,
    shareCount,
  })
}

test.describe('Home page', () => {
  test('shows hero section with Get Started link when not authenticated', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/')
    await expect(page.getByRole('heading', { name: 'Capitalism 5' }).first()).toBeVisible()
    const getStartedLink = page.getByRole('link', { name: 'Get Started' })
    await expect(getStartedLink).toBeVisible()
    await expect(getStartedLink).toHaveAttribute('href', '/onboarding')
  })

  test('shows leaderboard heading', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/')
    await expect(page.getByRole('heading', { name: 'Top Players' })).toBeVisible()
  })

  test('shows leaderboard section when data loads', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/')
    await expect(page.getByRole('heading', { name: 'Top Players' })).toBeVisible()
    await expect(page.getByRole('link', { name: 'View Full Leaderboard' })).toBeVisible()
  })

  test('shows leaderboard row for player with company', async ({ page }) => {
    const player = makePlayer({ displayName: 'Tycoon' })
    player.companies.push({
      id: 'comp-1',
      playerId: player.id,
      name: 'Tycoon Corp',
      cash: 750000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [],
    })
    const state = setupMockApi(page, { players: [player] })
    addPlayerShareholding(state, player.id, 'comp-1')
    await page.goto('/')
    await expect(page.getByText('Tycoon')).toBeVisible()
    // totalWealthUsd = (200,000 personalCash + 750,000 sharesValue) * 1.08 ≈ $1.03M in compact USD
    const wealthCell = page.locator('td.wealth').filter({ hasText: '$' })
    await expect(wealthCell.first()).toBeVisible()
  })

  test('shows empty leaderboard message when no players', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/')
    await expect(page.getByText('No players yet')).toBeVisible()
  })

  test('shows Start Your Empire CTA for authenticated player with unfinished onboarding', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticate(page, `token-${player.id}`)

    await page.goto('/')
    const startLink = page.getByRole('link', { name: 'Start Your Empire' })
    await expect(startLink).toBeVisible()
    await expect(startLink).toHaveAttribute('href', '/onboarding')
  })

  test('shows Go to Dashboard CTA for authenticated player who finished onboarding', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T12:00:00Z',
      companies: [
        {
          id: 'comp-home',
          playerId: 'player-1',
          name: 'Finished Corp',
          cash: 500000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticate(page, `token-${player.id}`)

    await page.goto('/')
    const dashboardLink = page.getByRole('link', { name: 'Go to Dashboard' })
    await expect(dashboardLink).toBeVisible()
    await expect(dashboardLink).toHaveAttribute('href', '/dashboard')
  })
})

test.describe('Header navigation', () => {
  test('shows Login link when logged out', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/')
    await expect(page.getByRole('link', { name: 'Login' })).toBeVisible()
  })

  test('logo navigates to home', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/login')
    await page
      .getByRole('link', { name: /Capitalism 5/i })
      .first()
      .click()
    await expect(page).toHaveURL(/\/$/)
  })

  test('login view redirects to Biatec authorize endpoint', async ({ page }) => {
    setupMockApi(page)
    await page.route('https://google.biatec.io/**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'text/html',
        body: '<html><body>OIDC authorize mock</body></html>',
      })
    })

    await page.goto('/login')
    await page.getByRole('button', { name: 'Sign in with google' }).click()

    await expect(page).toHaveURL(/https:\/\/google.biatec.io\/authorize/)
    await expect(page).toHaveURL(/client_id=capitalism/)
    const authorizeUrl = new URL(page.url())
    const redirectUri = authorizeUrl.searchParams.get('redirect_uri')
    expect(redirectUri).toBeTruthy()
    expect(decodeURIComponent(redirectUri ?? '')).toMatch(/\/auth\/callback$/)
    await expect(page).toHaveURL(/scope=openid/)
    await expect(page).toHaveURL(/state=/)
    await expect(page).toHaveURL(/nonce=/)
  })

  test('shows Dashboard link when authenticated', async ({ page }) => {
    const player = makePlayer()
    setupMockApi(page, {
      players: [player],
      currentUserId: player.id,
      currentToken: `token-${player.id}`,
    })
    await authenticate(page, `token-${player.id}`)
    await page.goto('/')
    await expect(page.getByRole('link', { name: 'Dashboard' })).toBeVisible()
    await expect(page.getByRole('banner').getByText(player.displayName)).toBeVisible()
  })

  test('native login auto-switches city context back to the main factory city', async ({ page }) => {
    const player = makeMainCityPlayer()
    setupMockApi(page, { players: [player] })
    await seedSelectedCity(page, 'city-vi')

    await page.goto('/login')
    await page.getByLabel('Email').fill(player.email)
    await page.getByLabel('Password').fill(player.password)
    await page.getByRole('button', { name: 'Sign In', exact: true }).click()

    await expect(page).toHaveURL('/')
    await expect(page.locator('.ctx-trigger .ctx-city-name')).toContainText('Bratislava')
    await expect(page.locator('.city-auto-switch-toast')).toContainText(
      'Switched to Bratislava — your main city.',
    )
  })

  test('oidc callback auto-switches city context back to the main factory city', async ({ page }) => {
    const player = makeMainCityPlayer()
    setupMockApi(page, { players: [player] })

    const state = 'oidc-state'
    const nonce = 'oidc-nonce'
    const token = makeOidcToken(nonce)
    await page.addInitScript(
      ({ storedState, storedNonce }) => {
        localStorage.setItem('selected_city_id', 'city-vi')
        sessionStorage.setItem(
          'biatec_oidc_state',
          JSON.stringify({
            state: storedState,
            nonce: storedNonce,
            redirectPath: '/',
          }),
        )
      },
      { storedState: state, storedNonce: nonce },
    )

    await page.goto(`/auth/callback?state=${state}&id_token=${encodeURIComponent(token)}`)

    await expect(page).toHaveURL('/')
    await expect(page.locator('.ctx-trigger .ctx-city-name')).toContainText('Bratislava')
    await expect(page.locator('.city-auto-switch-toast')).toContainText(
      'Switched to Bratislava — your main city.',
    )
  })

  test('player without factories falls back to onboarding city after login', async ({ page }) => {
    const player = makePlayer({
      email: 'newbie@test.com',
      password: 'TestPass1!',
      onboardingCityId: 'city-vi',
      companies: [],
    })
    setupMockApi(page, { players: [player] })
    await seedSelectedCity(page, 'city-pr')

    await page.goto('/login')
    await page.getByLabel('Email').fill(player.email)
    await page.getByLabel('Password').fill(player.password)
    await page.getByRole('button', { name: 'Sign In', exact: true }).click()

    await expect(page).toHaveURL('/')
    await expect(page.locator('.ctx-trigger .ctx-city-name')).toContainText('Vienna')
    await expect(page.locator('.city-auto-switch-toast')).toHaveCount(0)
  })

  test('oidc callback keeps the stored city when the player already has a building there', async ({
    page,
  }) => {
    const player = makeMixedCityPlayer()
    setupMockApi(page, { players: [player] })

    const state = 'oidc-state-keep-city'
    const nonce = 'oidc-nonce-keep-city'
    const token = makeOidcToken(nonce)
    await page.addInitScript(
      ({ storedState, storedNonce }) => {
        localStorage.setItem('selected_city_id', 'city-pr')
        sessionStorage.setItem(
          'biatec_oidc_state',
          JSON.stringify({
            state: storedState,
            nonce: storedNonce,
            redirectPath: '/',
          }),
        )
      },
      { storedState: state, storedNonce: nonce },
    )

    await page.goto(`/auth/callback?state=${state}&id_token=${encodeURIComponent(token)}`)

    await expect(page).toHaveURL('/')
    await expect(page.locator('.ctx-trigger .ctx-city-name')).toContainText('Prague')
    await expect(page.locator('.city-auto-switch-toast')).toHaveCount(0)
  })

  test('shows current in-game time in the header', async ({ page }) => {
    const state = setupMockApi(page)
    state.gameState.currentTick = 48

    await page.goto('/')

    const gameTimeChip = page.locator('.game-time-chip')
    await expect(gameTimeChip).toBeVisible()
    await expect(gameTimeChip).toContainText('2000')
  })

  test('opens notifications panel and marks all notifications as read', async ({ page }) => {
    const player = makePlayer()
    const now = new Date().toISOString()
    setupMockApi(page, {
      players: [player],
      currentUserId: player.id,
      currentToken: `token-${player.id}`,
      playerNotifications: [
        {
          id: 'notif-1',
          type: 'BUILDING_CONSTRUCTION_COMPLETED',
          title: 'Construction complete',
          message: 'Your factory is ready.',
          isRead: false,
          createdAtTick: 42,
          createdAtUtc: now,
          buildingId: 'building-1',
        },
        {
          id: 'notif-2',
          type: 'BANK_ACCOUNT_LOW_BALANCE',
          title: 'Low balance',
          message: 'Top up your account.',
          isRead: false,
          createdAtTick: 41,
          createdAtUtc: now,
          bankAccountId: 'acc-1',
        },
      ],
    })
    await authenticate(page, `token-${player.id}`)

    await page.goto('/')

    await expect(page.locator('.notification-badge')).toContainText('2')

    await page.getByRole('button', { name: 'Notifications' }).click()

    const panel = page.locator('.notification-panel')
    await expect(panel).toBeVisible()
    await expect(panel.locator('.notification-item')).toHaveCount(2)
    await expect(panel).toContainText('Construction complete')

    await panel.getByRole('button', { name: 'Mark all read' }).click()

    await expect(page.locator('.notification-badge')).toHaveCount(0)
    await expect(panel.locator('.notification-item-unread')).toHaveCount(0)
  })
})

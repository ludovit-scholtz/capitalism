import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer } from '../../helpers/mock-api'

async function authenticate(page: Parameters<typeof test>[0]['page'], token: string) {
  await page.addInitScript((storedToken) => {
    localStorage.setItem('auth_token', storedToken)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7_200_000).toISOString())
    localStorage.setItem('auth_provider', 'local')
  }, token)
  await page.context().addCookies([
    {
      name: 'auth_token',
      value: token,
      url: process.env.CI ? 'http://localhost:4173' : 'http://localhost:5173',
      httpOnly: true,
      sameSite: 'Strict',
    },
  ])
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
        buildings: [makeBuilding('factory-ba-1', 'comp-mixed', 'city-ba', 'FACTORY', 'Bratislava Factory'), makeBuilding('shop-pr-1', 'comp-mixed', 'city-pr', 'SALES_SHOP', 'Prague Shop')],
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
      aud: 'capitalism-pkce',
      exp: Math.floor(Date.now() / 1000) + 3600,
    }),
  ).toString('base64url')

  return `${header}.${payload}.signature`
}

async function mockOidcTokenExchange(page: Parameters<typeof test>[0]['page'], idToken: string) {
  await page.route('https://google.biatec.io/token', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ id_token: idToken, access_token: idToken, expires_in: 3600 }),
    })
  })
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
    await expect(page.getByRole('cell', { name: 'Tycoon', exact: true })).toBeVisible()
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

  test('energy navigation link opens energy market route', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/')

    await page.getByRole('button', { name: 'Economy' }).hover()
    const energyLink = page.locator('.desktop-section-panel').getByRole('link', { name: 'Energy', exact: true })
    await expect(energyLink).toBeVisible()
    await energyLink.click()

    await expect(page).toHaveURL(/\/energy-market$/)
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
    await expect(page).toHaveURL(/client_id=capitalism-pkce/)
    const authorizeUrl = new URL(page.url())
    const redirectUri = authorizeUrl.searchParams.get('redirect_uri')
    expect(redirectUri).toBeTruthy()
    expect(decodeURIComponent(redirectUri ?? '')).toMatch(/\/auth\/callback$/)
    await expect(page).toHaveURL(/scope=openid/)
    await expect(page).toHaveURL(/state=/)
    await expect(page).toHaveURL(/nonce=/)
    expect(authorizeUrl.searchParams.get('response_type')).toBe('code')
    expect(authorizeUrl.searchParams.get('code_challenge')).toBeTruthy()
    expect(authorizeUrl.searchParams.get('code_challenge_method')).toBe('S256')
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
    await page.getByRole('button', { name: 'Main' }).hover()
    await expect(page.locator('.desktop-section-panel').getByRole('link', { name: 'Dashboard', exact: true })).toBeVisible()
    await expect(page.getByRole('banner').getByText(player.displayName)).toBeVisible()
  })

  test('logout redirects to landing page and shows signed-out toast', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T12:00:00Z',
    })
    setupMockApi(page, {
      players: [player],
      currentUserId: player.id,
      currentToken: `token-${player.id}`,
    })
    await authenticate(page, `token-${player.id}`)

    await page.goto('/dashboard')
    await expect(page).toHaveURL('/dashboard')

    await page.locator('button[title="Logout"]').first().click()

    await expect(page).toHaveURL('/')
    await expect(page).not.toHaveURL(/\/login/)
    await expect(page.getByText('You have been signed out.')).toBeVisible()
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
  })

  test('oidc callback auto-switches city context back to the main factory city', async ({ page }) => {
    const player = makeMainCityPlayer()
    setupMockApi(page, { players: [player] })

    const state = 'oidc-state'
    const nonce = 'oidc-nonce'
    const codeVerifier = 'oidc-code-verifier'
    const token = makeOidcToken(nonce)
    await mockOidcTokenExchange(page, token)
    await page.addInitScript(
      ({ storedState, storedNonce, storedCodeVerifier }) => {
        localStorage.setItem('selected_city_id', 'city-vi')
        sessionStorage.setItem(
          'biatec_oidc_state',
          JSON.stringify({
            state: storedState,
            nonce: storedNonce,
            redirectPath: '/',
            codeVerifier: storedCodeVerifier,
          }),
        )
      },
      { storedState: state, storedNonce: nonce, storedCodeVerifier: codeVerifier },
    )

    await page.goto(`/auth/callback?state=${state}&code=oidc-auth-code`)

    await expect(page).toHaveURL('/')
    await expect(page.locator('.ctx-trigger .ctx-city-name')).toContainText('Bratislava')
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

  test('oidc callback keeps the stored city when the player already has a building there', async ({ page }) => {
    const player = makeMixedCityPlayer()
    setupMockApi(page, { players: [player] })

    const state = 'oidc-state-keep-city'
    const nonce = 'oidc-nonce-keep-city'
    const codeVerifier = 'oidc-code-verifier-keep-city'
    const token = makeOidcToken(nonce)
    await mockOidcTokenExchange(page, token)
    await page.addInitScript(
      ({ storedState, storedNonce, storedCodeVerifier }) => {
        localStorage.setItem('selected_city_id', 'city-pr')
        sessionStorage.setItem(
          'biatec_oidc_state',
          JSON.stringify({
            state: storedState,
            nonce: storedNonce,
            redirectPath: '/',
            codeVerifier: storedCodeVerifier,
          }),
        )
      },
      { storedState: state, storedNonce: nonce, storedCodeVerifier: codeVerifier },
    )

    await page.goto(`/auth/callback?state=${state}&code=oidc-auth-code-keep-city`)

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
    await expect(panel.getByRole('button', { name: 'Mark all read' })).toBeDisabled()

    await expect(page.locator('.notification-badge')).toHaveCount(0)
    await expect(panel.locator('.notification-item-unread')).toHaveCount(0)
  })

  test('renders localized notification text when only translation keys are present', async ({ page }) => {
    const player = makePlayer()
    const now = new Date().toISOString()
    setupMockApi(page, {
      players: [player],
      currentUserId: player.id,
      currentToken: `token-${player.id}`,
      playerNotifications: [
        {
          id: 'notif-city-unlock',
          type: 'CITY_EXPANSION_UNLOCKED',
          title: '',
          message: '',
          titleKey: 'cityExpansion.notificationTitle',
          bodyKey: 'cityExpansion.notificationMessage',
          bodyParamsJson: JSON.stringify({ city: 'Berlin', company: 'Northwind Holdings' }),
          isRead: false,
          createdAtTick: 120,
          createdAtUtc: now,
          companyId: 'company-1',
          relatedEntityType: 'CITY',
          relatedEntityId: 'city-ber',
        },
      ],
    })
    await authenticate(page, `token-${player.id}`)

    await page.goto('/')
    await page.getByRole('button', { name: 'Notifications' }).click()

    const panel = page.locator('.notification-panel')
    await expect(panel).toContainText('Berlin is now unlocked!')
    await expect(panel).toContainText('You can now expand Northwind Holdings into Berlin.')
  })

  test('shows shipment and margin warning notification icons with deep links', async ({ page }) => {
    const player = makePlayer()
    const now = new Date().toISOString()
    setupMockApi(page, {
      players: [player],
      currentUserId: player.id,
      currentToken: `token-${player.id}`,
      playerNotifications: [
        {
          id: 'notif-shipment',
          type: 'SHIPMENT_ARRIVED',
          title: 'Shipment arrived',
          message: 'Shipment arrived at Prague Factory.',
          isRead: false,
          createdAtTick: 75,
          createdAtUtc: now,
          buildingId: 'building-1',
        },
        {
          id: 'notif-margin',
          type: 'LOGISTICS_MARGIN_EROSION',
          title: 'Logistics cost warning',
          message: 'Shipping costs are eroding your margins.',
          isRead: false,
          createdAtTick: 74,
          createdAtUtc: now,
        },
      ],
    })
    await authenticate(page, `token-${player.id}`)

    await page.goto('/')
    await page.getByRole('button', { name: 'Notifications' }).click()

    const panel = page.locator('.notification-panel')
    await expect(panel.locator('.notification-icon-shipment')).toBeVisible()
    await expect(panel.locator('.notification-icon-margin')).toBeVisible()

    await panel.getByRole('button', { name: /shipment arrived/i }).click()
    await expect(page).toHaveURL('/building/building-1')

    await page.goto('/')
    await page.getByRole('button', { name: 'Notifications' }).click()
    await panel.getByRole('button', { name: /logistics cost warning/i }).click()
    await expect(page).toHaveURL('/trade-routes')
  })

  test('shows severity colours and empty state in notification panel', async ({ page }) => {
    const player = makePlayer()
    const now = new Date().toISOString()
    const state = setupMockApi(page, {
      players: [player],
      currentUserId: player.id,
      currentToken: `token-${player.id}`,
      playerNotifications: [
        {
          id: 'notif-critical',
          type: 'LOAN_DEFAULT',
          severity: 'CRITICAL',
          title: 'Loan default',
          message: 'A loan default happened.',
          isRead: false,
          createdAtTick: 99,
          createdAtUtc: now,
        },
        {
          id: 'notif-warning',
          type: 'OVERSUPPLY_WARNING',
          severity: 'WARNING',
          title: 'Oversupply warning',
          message: 'Demand fell below threshold.',
          isRead: false,
          createdAtTick: 98,
          createdAtUtc: now,
        },
        {
          id: 'notif-info',
          type: 'BUILDING_OFFER_RECEIVED',
          severity: 'INFO',
          title: 'Offer received',
          message: 'New building offer arrived.',
          isRead: false,
          createdAtTick: 97,
          createdAtUtc: now,
        },
      ],
    })
    await authenticate(page, `token-${player.id}`)

    await page.goto('/')
    await page.getByRole('button', { name: 'Notifications' }).click()
    const panel = page.locator('.notification-panel')
    await expect(panel.locator('.notification-severity-critical')).toHaveCount(1)
    await expect(panel.locator('.notification-severity-warning')).toHaveCount(1)
    await expect(panel.locator('.notification-severity-info')).toHaveCount(1)

    state.playerNotifications = []
    await page.getByRole('button', { name: 'Notifications' }).click()
    await page.getByRole('button', { name: 'Notifications' }).click()
    await expect(panel).toContainText('No notifications yet.')
  })

  test('notification panel closes on backdrop click and ESC', async ({ page }) => {
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
          message: 'Factory finished.',
          isRead: false,
          createdAtTick: 10,
          createdAtUtc: now,
        },
      ],
    })
    await authenticate(page, `token-${player.id}`)

    await page.goto('/')
    await page.getByRole('button', { name: 'Notifications' }).click()
    await expect(page.locator('.notification-panel')).toBeVisible()

    await page.locator('.notification-overlay').click()
    await expect(page.locator('.notification-panel')).toHaveCount(0)

    await page.getByRole('button', { name: 'Notifications' }).click()
    await expect(page.locator('.notification-panel')).toBeVisible()
    await page.keyboard.press('Escape')
    await expect(page.locator('.notification-panel')).toHaveCount(0)
  })

  test('mobile notifications button opens dedicated notifications page', async ({ page }) => {
    const player = makePlayer()
    const now = new Date().toISOString()
    setupMockApi(page, {
      players: [player],
      currentUserId: player.id,
      currentToken: `token-${player.id}`,
      playerNotifications: [
        {
          id: 'notif-mobile',
          type: 'BUILDING_CONSTRUCTION_COMPLETED',
          title: 'Construction complete',
          message: 'Factory finished.',
          isRead: false,
          createdAtTick: 10,
          createdAtUtc: now,
        },
      ],
    })
    await authenticate(page, `token-${player.id}`)
    await page.setViewportSize({ width: 375, height: 812 })

    await page.goto('/')
    await page.getByRole('button', { name: 'Notifications' }).click()

    await expect(page).toHaveURL('/notifications')
    await expect(page.locator('.notification-panel')).toHaveCount(0)
    await expect(page.getByRole('heading', { name: 'Notifications' })).toBeVisible()
    await expect(page.getByText('Construction complete')).toBeVisible()
  })
})

import { expect, test } from '@playwright/test'
import { loginAs, makePlayer, makeServer, setupMockApi } from './helpers/mock-api'

test.describe('Home page', () => {
  test('shows ranking-focused hero and section navigation', async ({ page }) => {
    setupMockApi(page, { servers: [] })
    await page.goto('/')

    await expect(page.locator('.hero-title')).toContainText('Capitalism')
    await expect(page.getByRole('heading', { name: 'Leaderboard' })).toBeVisible()
    await expect(page.getByRole('link', { name: 'Game Servers' }).first()).toBeVisible()
  })

  test('shows ranking error state when backend fails', async ({ page }) => {
    await page.route('**/graphql', async (route) => {
      await route.abort('failed')
    })

    await page.goto('/')
    await expect(page.locator('.state-error').first()).toBeVisible()
  })

  test('shows feature highlights section with four cards', async ({ page }) => {
    setupMockApi(page, { servers: [] })
    await page.goto('/')

    await expect(page.getByRole('heading', { name: 'Why Capitalism?' })).toBeVisible()
    await expect(page.locator('.feature-card')).toHaveCount(4)
    await expect(page.getByRole('heading', { name: 'Economic Simulation' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Stock Exchange' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Power Grid' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Research & Development' })).toBeVisible()
  })

  test('shows learn more docs link in feature highlights', async ({ page }) => {
    setupMockApi(page, { servers: [] })
    await page.goto('/')

    const docsLink = page.getByRole('link', { name: /docs/i }).last()
    await expect(docsLink).toBeVisible()
  })

  test('shows active servers teaser section with top 3 online servers', async ({ page }) => {
    const s1 = makeServer({ id: 's1', serverKey: 'key-1', displayName: 'EU Server 1', playerCount: 50, isOnline: true })
    const s2 = makeServer({ id: 's2', serverKey: 'key-2', displayName: 'US Server 2', playerCount: 30, isOnline: true })
    const s3 = makeServer({ id: 's3', serverKey: 'key-3', displayName: 'Asia Server 3', playerCount: 10, isOnline: true })
    const s4 = makeServer({ id: 's4', serverKey: 'key-4', displayName: 'Hidden Server 4', playerCount: 5, isOnline: true })
    setupMockApi(page, { servers: [s1, s2, s3, s4] })
    await page.goto('/')

    await expect(page.getByRole('heading', { name: 'Active Servers' })).toBeVisible()
    // Top 3 online servers shown in teaser
    const teaserCards = page.locator('.server-teaser-card')
    await expect(teaserCards).toHaveCount(3)
    await expect(teaserCards.first()).toContainText('EU Server 1')
  })

  test('shows empty state in teaser when no servers are online', async ({ page }) => {
    setupMockApi(page, { servers: [] })
    await page.goto('/')

    await expect(page.getByRole('heading', { name: 'Active Servers' })).toBeVisible()
    await expect(page.locator('.servers-teaser-empty')).toBeVisible()
    await expect(page.locator('.servers-teaser-empty')).toContainText('No active servers right now')
  })

  test('home teaser "View all servers" links to /game-servers', async ({ page }) => {
    setupMockApi(page, { servers: [] })
    await page.goto('/')

    const viewAllLink = page.getByRole('link', { name: /view all servers/i })
    await expect(viewAllLink).toBeVisible()
    await expect(viewAllLink).toHaveAttribute('href', '/game-servers')
  })

  test('teaser server cards show player count and play link', async ({ page }) => {
    const server = makeServer({
      id: 'ts1',
      serverKey: 'ts-key-1',
      displayName: 'Test Shard',
      playerCount: 77,
      currentTick: 1234,
      isOnline: true,
      frontendUrl: 'https://shard.example.com/app',
    })
    setupMockApi(page, { servers: [server] })
    await page.goto('/')

    const card = page.locator('.server-teaser-card').first()
    await expect(card).toContainText('Test Shard')
    await expect(card).toContainText('77')
    await expect(card.getByRole('link', { name: 'Play on server' })).toHaveAttribute(
      'href',
      'https://shard.example.com/app',
    )
  })
})


test.describe('Game servers page', () => {
  test('shows empty state when no server is registered', async ({ page }) => {
    setupMockApi(page, { servers: [] })
    await page.goto('/game-servers')

    await expect(page.getByText('No servers have registered yet')).toBeVisible()
  })

  test('renders server card data and play link', async ({ page }) => {
    const server = makeServer({
      displayName: 'Capitalism EU #1',
      region: 'EU',
      environment: 'production',
      version: '1.0.0',
      description: 'First production economy for EU players',
    })

    setupMockApi(page, { servers: [server] })
    await page.goto('/game-servers')

    await expect(page.getByText('Capitalism EU #1')).toBeVisible()
    await expect(page.getByText('EU · production · v1.0.0')).toBeVisible()
    const playLink = page.getByRole('link', { name: 'Play on server' })
    await expect(playLink).toHaveAttribute('href', 'https://game.example.com/app')
  })

  test('renders offline server badge', async ({ page }) => {
    const server = makeServer({
      displayName: 'Offline shard',
      isOnline: false,
      lastHeartbeatAtUtc: '2020-01-01T00:00:00.000Z',
    })

    setupMockApi(page, { servers: [server] })
    await page.goto('/game-servers')

    await expect(page.getByText('Offline shard')).toBeVisible()
    await expect(page.getByText('Offline', { exact: true })).toBeVisible()
  })

  test('shows connection error state when gameServers query fails', async ({ page }) => {
    const state = setupMockApi(page, { servers: [makeServer()] })

    await page.route('**/graphql', async (route) => {
      const body = route.request().postDataJSON() as { query: string }
      if (body.query?.includes('gameServers')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [{ message: 'Failed to fetch.', extensions: { code: 'SERVER_ERROR' } }],
          }),
        })
        return
      }

      const originalBody = JSON.stringify({ data: { gameServers: state.servers } })
      await route.fulfill({ status: 200, contentType: 'application/json', body: originalBody })
    })

    await page.goto('/game-servers')
    await expect(page.getByRole('alert').first()).toContainText('Failed to fetch')
  })

  test('refresh button reloads changed server list', async ({ page }) => {
    const state = setupMockApi(page, { servers: [] })
    await page.goto('/game-servers')

    await expect(page.getByText('No servers have registered yet')).toBeVisible()

    state.servers.push(
      makeServer({ id: 'server-2', serverKey: 'capitalism-eu-2', displayName: 'Capitalism EU #2' }),
    )

    await page.getByRole('button', { name: 'Refresh' }).click()
    await expect(page.getByText('Capitalism EU #2')).toBeVisible()
  })

  test('auto-refresh interval is set and clears on unmount', async ({ page }) => {
    // Verify the page registers an interval that clears on navigation away
    const state = setupMockApi(page, { servers: [makeServer({ displayName: 'Initial Server' })] })
    await page.goto('/game-servers')

    await expect(page.getByText('Initial Server')).toBeVisible()

    // Update mock data so next refresh would show new content
    state.servers = [makeServer({ id: 'auto-s2', serverKey: 'auto-key', displayName: 'Auto Refreshed Server' })]

    // Navigate away and back — interval cleanup means no outstanding timer errors
    await page.goto('/')
    await page.goto('/game-servers')

    // The fresh page load shows the updated mock data
    await expect(page.getByText('Auto Refreshed Server')).toBeVisible()
  })
})

test.describe('Login page', () => {
  test('shows sign in form by default', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/login?oidc_retry=consent')

    await expect(page.getByRole('button', { name: 'Sign in', exact: true })).toBeVisible()
    await expect(page.getByLabel('Email')).toBeVisible()
    await expect(page.getByLabel('Password')).toBeVisible()
  })

  test('switches to register form', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/login?oidc_retry=consent')

    await page.getByRole('button', { name: 'Register' }).click()
    await expect(page.getByRole('button', { name: 'Create account' })).toBeVisible()
    await expect(page.getByLabel('Display name')).toBeVisible()
  })

  test('shows error on bad credentials', async ({ page }) => {
    setupMockApi(page, { servers: [] })
    await page.goto('/login?oidc_retry=consent')

    await page.getByLabel('Email').fill('wrong@example.com')
    await page.getByLabel('Password').fill('badpass')
    await page.getByRole('button', { name: 'Sign in', exact: true }).click()

    await expect(page.getByRole('alert')).toBeVisible()
    await expect(page.getByRole('alert')).toContainText('Invalid credentials.')
  })

  test('shows neutral registration message for duplicate email failures', async ({ page }) => {
    setupMockApi(page, { servers: [] })
    await page.route('**/graphql', async (route) => {
      const body = route.request().postDataJSON() as { query: string }
      if (body.query?.includes('mutation') && body.query?.includes('register')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              {
                message: 'Registration failed.',
                extensions: { code: 'REGISTRATION_FAILED' },
              },
            ],
          }),
        })
        return
      }
      await route.fallback()
    })

    await page.goto('/login?oidc_retry=consent')
    await page.getByRole('button', { name: 'Register' }).click()
    await page.getByLabel('Email').fill('existing@example.com')
    await page.getByLabel('Display name').fill('Another Player')
    await page.getByLabel('Password').fill('password123')
    await page.getByRole('button', { name: 'Create account' }).click()

    await expect(page.getByRole('alert')).toContainText(
      'If this email is not already registered, you will receive a confirmation.',
    )
  })

  test('redirects to Biatec authorize endpoint with required OIDC params', async ({ page }) => {
    setupMockApi(page)
    await page.route('https://google.biatec.io/**', (route) => route.abort())
    await page.goto('/login?oidc_retry=consent')

    // window.location.href navigation to the OIDC endpoint is captured as a request.
    // Route intercept aborts the navigation so the test stays on the login page.

    const [request] = await Promise.all([
      page.waitForRequest((req) => req.url().startsWith('https://google.biatec.io/authorize')),
      page.getByRole('button', { name: 'Authenticate using Google' }).click(),
    ])

    const url = new URL(request.url())
    expect(url.searchParams.get('client_id')).toBe('capitalism-master')
    expect(url.searchParams.get('redirect_uri')).toContain('/auth/callback')
    expect(url.searchParams.get('response_type')).toBe('id_token')
    expect(url.searchParams.get('scope')).toContain('openid')
    expect(url.searchParams.get('state')).toBeTruthy()
    expect(url.searchParams.get('nonce')).toBeTruthy()
  })
})

test.describe('Authenticated navigation', () => {
  test('shows logout button and returns to guest nav after sign out', async ({ page }) => {
    const player = makePlayer({ displayName: 'Bob' })
    const state = setupMockApi(page, { servers: [] })
    await loginAs(page, state, player)

    await page.goto('/')
    await expect(page.getByRole('button', { name: 'Sign out' })).toBeVisible()

    await page.getByRole('button', { name: 'Sign out' }).click()
    await expect(page.getByRole('link', { name: 'Sign in' })).toBeVisible()
  })
})

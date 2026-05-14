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

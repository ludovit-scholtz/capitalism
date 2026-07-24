/// <reference lib="dom" />

import { expect, test } from '@playwright/test'
// @ts-expect-error Playwright resolves this spec helper import at runtime without a .js suffix.
import { makePlayer, setupMockApi } from '../../helpers/mock-api'

function encodeBase64Url(value: string) {
  return btoa(value).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '')
}

function makeOidcToken(nonce: string) {
  const header = encodeBase64Url(JSON.stringify({ alg: 'none', typ: 'JWT' }))
  const payload = encodeBase64Url(
    JSON.stringify({
      nonce,
      iss: 'https://google.biatec.io',
      aud: 'capitalism-pkce',
      exp: Math.floor(Date.now() / 1000) + 3600,
    }),
  )

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

test('OIDC callback persists only the provider marker and survives a reload on the redirected route', async ({ page }) => {
  const player = makePlayer({
    onboardingCompletedAtUtc: '2026-01-01T12:00:00Z',
    companies: [
      {
        id: 'comp-oidc',
        playerId: 'player-1',
        name: 'OIDC Corp',
        cash: 500000,
        foundedAtUtc: '2026-01-01T00:00:00Z',
        buildings: [],
      },
    ],
  })

  const oidcState = 'oidc-state-1'
  const oidcNonce = 'oidc-nonce-1'
  const oidcCodeVerifier = 'oidc-code-verifier-1'
  const oidcToken = makeOidcToken(oidcNonce)
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = oidcToken
  await mockOidcTokenExchange(page, oidcToken)

  await page.addInitScript(
    ({ pendingState }) => {
      window.sessionStorage.setItem('biatec_oidc_state', JSON.stringify(pendingState))
    },
    {
      pendingState: {
        state: oidcState,
        nonce: oidcNonce,
        redirectPath: '/dashboard',
        codeVerifier: oidcCodeVerifier,
      },
    },
  )

  await page.goto(`/auth/callback?state=${encodeURIComponent(oidcState)}&code=oidc-auth-code-1`)

  await expect(page).toHaveURL(/\/dashboard$/)
  await expect(page.locator('.tick-clock-widget')).toBeVisible()

  const storedSession = await page.evaluate(() => ({
    token: window.localStorage.getItem('auth_token'),
    expiresAtUtc: window.localStorage.getItem('auth_expires'),
    provider: window.localStorage.getItem('auth_provider'),
    sessionToken: window.sessionStorage.getItem('auth_token'),
  }))

  expect(storedSession.token).toBeNull()
  expect(storedSession.expiresAtUtc).toBeNull()
  expect(storedSession.provider).toBe('biatec_oidc')
  expect(storedSession.sessionToken).toBeNull()

  await page.reload()

  await expect(page).toHaveURL(/\/dashboard$/)
  await expect(page.locator('.tick-clock-widget')).toBeVisible()
})

test('OIDC callback does not depend on optional master session bootstrap', async ({ page }) => {
  const player = makePlayer({
    onboardingCompletedAtUtc: '2026-01-01T12:00:00Z',
    companies: [
      {
        id: 'comp-oidc-optional-master',
        playerId: 'player-1',
        name: 'Optional Master Corp',
        cash: 500000,
        foundedAtUtc: '2026-01-01T00:00:00Z',
        buildings: [],
      },
    ],
  })

  const oidcState = 'oidc-state-optional-master'
  const oidcNonce = 'oidc-nonce-optional-master'
  const oidcCodeVerifier = 'oidc-code-verifier-optional-master'
  const oidcToken = makeOidcToken(oidcNonce)
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = oidcToken
  await mockOidcTokenExchange(page, oidcToken)

  await page.route('**/auth/session', async (route) => {
    const url = route.request().url()
    if (url.includes('44364')) {
      await route.fulfill({ status: 503, body: 'master session unavailable' })
      return
    }

    await route.fallback()
  })

  await page.addInitScript(
    ({ pendingState }) => {
      window.sessionStorage.setItem('biatec_oidc_state', JSON.stringify(pendingState))
    },
    {
      pendingState: {
        state: oidcState,
        nonce: oidcNonce,
        redirectPath: '/dashboard',
        codeVerifier: oidcCodeVerifier,
      },
    },
  )

  await page.goto(`/auth/callback?state=${encodeURIComponent(oidcState)}&code=oidc-auth-code-optional-master`)

  // Core assertion: OIDC login succeeds even when master session is unavailable or not configured
  await expect(page).toHaveURL(/\/dashboard$/)
  await expect(page.locator('.tick-clock-widget')).toBeVisible()
})

test('OIDC callback shows an error instead of restarting sign-in when game session bootstrap fails', async ({ page }) => {
  const player = makePlayer({
    onboardingCompletedAtUtc: '2026-01-01T12:00:00Z',
    companies: [
      {
        id: 'comp-oidc-fail',
        playerId: 'player-1',
        name: 'Session Failure Corp',
        cash: 500000,
        foundedAtUtc: '2026-01-01T00:00:00Z',
        buildings: [],
      },
    ],
  })

  const oidcState = 'oidc-state-session-fail'
  const oidcNonce = 'oidc-nonce-session-fail'
  const oidcCodeVerifier = 'oidc-code-verifier-session-fail'
  const oidcToken = makeOidcToken(oidcNonce)
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = oidcToken

  let oidcRestartAttempted = false
  // Only the /authorize endpoint indicates a sign-in restart; the legitimate
  // PKCE code exchange against /token must still be allowed to succeed below.
  await page.route('https://google.biatec.io/authorize', async (route) => {
    oidcRestartAttempted = true
    await route.fulfill({ status: 200, body: 'unexpected oidc retry' })
  })
  await mockOidcTokenExchange(page, oidcToken)
  await page.route('**/auth/session', async (route) => {
    await route.fulfill({ status: 401, body: 'unauthorized' })
  })

  await page.addInitScript(
    ({ pendingState }) => {
      window.sessionStorage.setItem('biatec_oidc_state', JSON.stringify(pendingState))
    },
    {
      pendingState: {
        state: oidcState,
        nonce: oidcNonce,
        redirectPath: '/dashboard',
        codeVerifier: oidcCodeVerifier,
      },
    },
  )

  await page.goto(`/auth/callback?state=${encodeURIComponent(oidcState)}&code=oidc-auth-code-session-fail`)

  await expect(page).toHaveURL(/\/auth\/callback\?state=/)
  await expect(page.getByRole('alert')).toContainText('Failed to establish secure session.')
  expect(oidcRestartAttempted).toBe(false)
})

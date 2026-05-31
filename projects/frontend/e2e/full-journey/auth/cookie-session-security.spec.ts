import { expect, test } from '@playwright/test'
import { makePlayer, setupMockApi } from '../../helpers/mock-api'

test.describe('cookie-based auth session hardening', () => {
  test('login establishes a cookie session without persisting the raw JWT', async ({
    page,
  }) => {
    const player = makePlayer({
      email: 'cookie-session@example.com',
      password: 'Passw0rd!',
    })
    setupMockApi(page, { players: [player] })

    await page.goto('/login')
    await page.getByLabel('Email').fill(player.email)
    await page.getByLabel('Password').fill(player.password)
    await page.getByRole('button', { name: 'Sign In', exact: true }).click()
    await page.waitForURL('/')

    const authStorage = await page.evaluate(() => ({
      authToken: localStorage.getItem('auth_token'),
      authExpires: localStorage.getItem('auth_expires'),
      authProvider: localStorage.getItem('auth_provider'),
      sessionAuthToken: sessionStorage.getItem('auth_token'),
      cookie: document.cookie,
    }))

    // The raw bearer token is never persisted to web storage.
    expect(authStorage.authToken).toBeNull()
    expect(authStorage.authExpires).toBeNull()
    expect(authStorage.sessionAuthToken).toBeNull()
    // Only a non-sensitive provider marker is kept to rehydrate from the cookie.
    expect(authStorage.authProvider).toBe('local')
    // HttpOnly cookies are not visible to document.cookie
    expect(authStorage.cookie.includes('auth_token=')).toBeFalsy()
  })

  test('session survives a page reload via the cookie session', async ({ page }) => {
    const player = makePlayer({
      email: 'cookie-reload@example.com',
      password: 'Passw0rd!',
    })
    setupMockApi(page, { players: [player] })

    await page.goto('/login')
    await page.getByLabel('Email').fill(player.email)
    await page.getByLabel('Password').fill(player.password)
    await page.getByRole('button', { name: 'Sign In', exact: true }).click()
    await page.waitForURL('/')

    await page.reload()

    // The user stays authenticated after reload (not bounced to /login),
    // because the gameplay session is rehydrated from the cookie session.
    await expect(page).toHaveURL('/')
    const stillAuthenticated = await page.evaluate(() => localStorage.getItem('auth_provider'))
    expect(stillAuthenticated).toBe('local')
  })
})

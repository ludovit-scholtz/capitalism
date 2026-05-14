import { expect, test } from '@playwright/test'
import { makePlayer, setupMockApi } from '../../helpers/mock-api'

test.describe('cookie-based auth session hardening', () => {
  test('login establishes cookie session and stores bootstrap token in localStorage', async ({
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
      sessionAuthToken: sessionStorage.getItem('auth_token'),
      cookie: document.cookie,
    }))

    // Bootstrap token is stored in localStorage for initFromStorage() on page reload
    expect(authStorage.authToken).toBeTruthy()
    expect(authStorage.authExpires).toBeTruthy()
    // Session storage is not used for auth tokens
    expect(authStorage.sessionAuthToken).toBeNull()
    // HttpOnly cookies are not visible to document.cookie
    expect(authStorage.cookie.includes('auth_token=')).toBeFalsy()
  })
})

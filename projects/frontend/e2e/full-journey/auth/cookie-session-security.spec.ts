import { expect, test } from '@playwright/test'
import { makePlayer, setupMockApi } from '../../helpers/mock-api'

test.describe('cookie-based auth session hardening', () => {
  test('login does not persist JWT tokens in localStorage or sessionStorage', async ({ page }) => {
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

    expect(authStorage.authToken).toBeNull()
    expect(authStorage.authExpires).toBeNull()
    expect(authStorage.sessionAuthToken).toBeNull()
    expect(authStorage.cookie.includes('auth_token=')).toBeFalsy()
  })
})

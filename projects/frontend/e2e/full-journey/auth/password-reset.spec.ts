import { test, expect } from '@playwright/test'
import { makePlayer, setupMockApi } from '../../helpers/mock-api'

test.describe('Password reset flow', () => {
  test('player can request reset, set a new password, and login', async ({ page }) => {
    const player = makePlayer({
      email: 'reset@example.com',
      password: 'OldPass123!',
    })
    const state = setupMockApi(page, { players: [player] })
    await page.addInitScript(() => localStorage.setItem('app_locale', 'en'))

    await page.goto('/login')
    await page.getByRole('link', { name: 'Forgot password?' }).click()
    await expect(page).toHaveURL(/\/forgot-password$/)
    await page.getByLabel('Email').fill(player.email)
    const forgotPasswordRequest = page.waitForRequest((request) => request.url().includes('/auth/forgot-password') && request.method() === 'POST', { timeout: 10000 })
    await page.getByRole('button', { name: 'Send reset link' }).click()
    await forgotPasswordRequest

    const token = `reset-${player.id}`
    expect(state.passwordResetTokens[token]).toBe(player.email)

    await page.goto(`/reset-password?token=${token}`)
    await page.getByLabel('New password', { exact: true }).fill('NewPass123!')
    await page.getByLabel('Confirm new password', { exact: true }).fill('NewPass123!')
    await page.getByRole('button', { name: 'Reset password' }).click()
    await expect(page.getByText('Password has been reset successfully.')).toBeVisible()

    await page.goto('/login')
    await page.getByLabel('Email').fill(player.email)
    await page.getByLabel('Password').fill('NewPass123!')
    await page.getByRole('button', { name: 'Sign In', exact: true }).click()
    await expect(page).toHaveURL('/')
  })
})

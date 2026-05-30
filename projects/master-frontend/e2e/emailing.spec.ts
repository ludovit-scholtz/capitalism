import { expect, test } from '@playwright/test'
import { loginAs, makePlayer, setupMockApi } from './helpers/mock-api'

// ── Account page — email preferences ───────────────────────────────────────

test.describe('Account page — email preferences', () => {
  test('shows subscribed status and lets the player unsubscribe', async ({ page }) => {
    const player = makePlayer({ weeklyReportEmailSubscribed: true })
    const state = setupMockApi(page, {
      currentPlayer: player,
      playerGoldAccount: {
        goldTokenBalance: 0,
        lastUpdatedAtUtc: null,
        recentTransactions: [],
      },
    })
    state.currentToken = 'token-player'

    await loginAs(page, state, player, 'token-player')
    await page.goto('/account')

    await expect(page.getByRole('heading', { name: 'Email preferences' })).toBeVisible()
    await expect(page.getByText('Weekly report email')).toBeVisible()
    await expect(page.locator('.email-pref-status')).toContainText('Subscribed')

    await page.getByRole('button', { name: 'Unsubscribe' }).click()

    await expect(page.locator('.email-pref-status')).toContainText('Unsubscribed')
    await expect(page.getByRole('button', { name: 'Subscribe' })).toBeVisible()
  })

  test('shows unsubscribed status and lets the player re-subscribe', async ({ page }) => {
    const player = makePlayer({ weeklyReportEmailSubscribed: false })
    const state = setupMockApi(page, {
      currentPlayer: player,
      playerGoldAccount: {
        goldTokenBalance: 0,
        lastUpdatedAtUtc: null,
        recentTransactions: [],
      },
    })
    state.currentToken = 'token-player'

    await loginAs(page, state, player, 'token-player')
    await page.goto('/account')

    await expect(page.locator('.email-pref-status')).toContainText('Unsubscribed')
    await page.getByRole('button', { name: 'Subscribe' }).click()
    await expect(page.locator('.email-pref-status')).toContainText('Subscribed')
  })
})

// ── Unsubscribe page (one-click, unauthenticated) ──────────────────────────

test.describe('Email unsubscribe page', () => {
  test('confirms unsubscribe when a token is present', async ({ page }) => {
    setupMockApi(page, { servers: [] })
    await page.goto('/email/unsubscribe?token=11111111-1111-1111-1111-111111111111')

    await expect(page.getByRole('heading', { name: 'Unsubscribe' })).toBeVisible()
    await expect(
      page.getByText('You have been unsubscribed from the weekly report emails.'),
    ).toBeVisible()
  })

  test('shows an error when the token is missing', async ({ page }) => {
    setupMockApi(page, { servers: [] })
    await page.goto('/email/unsubscribe')

    await expect(page.getByText('This unsubscribe link is missing its token.')).toBeVisible()
  })
})

import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer } from '../helpers/mock-api'

test('capture notifications panel screenshot', async ({ page }) => {
  const player = makePlayer()
  const now = new Date().toISOString()
  setupMockApi(page, {
    players: [player],
    currentUserId: player.id,
    currentToken: `token-${player.id}`,
    playerNotifications: [
      {
        id: 'notif-crit',
        type: 'LOAN_DEFAULT',
        severity: 'CRITICAL',
        title: 'Loan default',
        message: 'Collateral is in foreclosure.',
        isRead: false,
        createdAtTick: 120,
        createdAtUtc: now,
      },
      {
        id: 'notif-warn',
        type: 'OVERSUPPLY_WARNING',
        severity: 'WARNING',
        title: 'Oversupply warning',
        message: 'Demand satisfaction dropped below 30%.',
        isRead: false,
        createdAtTick: 119,
        createdAtUtc: now,
      },
    ],
  })

  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 60 * 60 * 1000).toISOString())
    localStorage.setItem('auth_provider', 'native')
    document.cookie = `auth_token=${encodeURIComponent(token)}; path=/; SameSite=Lax`
  }, `token-${player.id}`)

  await page.goto('/')
  await page.getByRole('button', { name: 'Notifications' }).click()
  await expect(page.locator('.notification-panel')).toBeVisible()
  await page.setViewportSize({ width: 1920, height: 1080 })
  await page.screenshot({ path: 'docs/screenshots/notification-panel-1920x1080.png', fullPage: false })
})

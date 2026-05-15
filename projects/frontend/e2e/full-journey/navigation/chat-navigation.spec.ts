import { expect, test } from '@playwright/test'
import { makePlayer, setupMockApi } from '../../helpers/mock-api'

async function bootstrapAuthenticated(page: Parameters<typeof test>[0]['page'], token: string) {
  await page.addInitScript((sessionToken) => {
    localStorage.setItem('auth_token', sessionToken)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7_200_000).toISOString())
  }, token)
}

test.describe('Chat side panel', () => {
  test('opens/closes and clears unread badge when opened', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const other = makePlayer({ id: 'player-2', email: 'other@test.com', displayName: 'Other Trader' })
    const state = setupMockApi(page, {
      players: [player, other],
      chatMessages: [
        {
          id: 'chat-1',
          authorPlayerId: other.id,
          cityId: null,
          content: 'Unread market ping',
          createdAtUtc: '2026-01-01T00:00:00Z',
          isVisible: true,
        },
      ],
    })
    state.currentUserId = player.id
    await bootstrapAuthenticated(page, `token-${player.id}`)
    await page.addInitScript(() => localStorage.removeItem('chat_last_seen_message_id'))
    await page.goto('/')

    await expect(page.locator('.chat-badge')).toBeVisible()
    await page.getByRole('button', { name: 'Chat' }).click()
    await expect(page.locator('.chat-side-panel')).toBeVisible()
    await expect(page.locator('.chat-badge')).toBeHidden()
    await page.locator('.close-btn').click()
    await expect(page.locator('.chat-side-panel')).toBeHidden()
  })

  test('sends a message and renders it without page refresh', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    await bootstrapAuthenticated(page, `token-${player.id}`)
    await page.goto('/')

    await page.getByRole('button', { name: 'Chat' }).click()
    await page.getByLabel('Chat message').fill('Need wood in Bratislava.')
    await page.getByRole('button', { name: 'Send' }).click()
    await expect(page.locator('.chat-log')).toContainText('Need wood in Bratislava.')
  })

  test('auto-selects city tab on city route and global tab elsewhere', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    await bootstrapAuthenticated(page, `token-${player.id}`)

    await page.goto('/city/city-ba')
    await page.getByRole('button', { name: 'Chat' }).click()
    await expect(page.getByRole('tab', { name: 'Bratislava' })).toHaveAttribute('aria-selected', 'true')
    await page.locator('.close-btn').click()

    await page.goto('/dashboard')
    await page.getByRole('button', { name: 'Chat' }).click()
    await expect(page.getByRole('tab', { name: 'Global' })).toHaveAttribute('aria-selected', 'true')
  })

  test('shows visible error when backend rejects too-long message', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    await page.route('**/graphql', async (route, request) => {
      const body = JSON.parse(request.postData() ?? '{}') as { query?: string }
      if (body.query?.includes('sendChatMessage')) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [{ message: 'Chat message is too long.', extensions: { code: 'MESSAGE_TOO_LONG' } }],
          }),
        })
      }
      return route.fallback()
    })
    await bootstrapAuthenticated(page, `token-${player.id}`)
    await page.goto('/')

    await page.getByRole('button', { name: 'Chat' }).click()
    await page.getByLabel('Chat message').fill('x'.repeat(500))
    await page.getByRole('button', { name: 'Send' }).click()
    await expect(page.locator('.panel-footer .chat-state-error')).toContainText('Message is too long')
  })

  test('keeps newest message visible after sending while log is at bottom', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, {
      players: [player],
      chatMessages: Array.from({ length: 40 }, (_, i) => ({
        id: `chat-${i + 1}`,
        authorPlayerId: player.id,
        cityId: null,
        content: `history ${i + 1}`,
        createdAtUtc: new Date(Date.now() - (40 - i) * 1000).toISOString(),
        isVisible: true,
      })),
    })
    state.currentUserId = player.id
    await bootstrapAuthenticated(page, `token-${player.id}`)
    await page.goto('/')

    await page.getByRole('button', { name: 'Chat' }).click()
    await page.evaluate(() => {
      const log = document.querySelector('.chat-log')
      if (log) {
        log.scrollTop = log.scrollHeight
      }
    })
    await page.getByLabel('Chat message').fill('latest ping')
    await page.getByRole('button', { name: 'Send' }).click()
    await expect(page.locator('.chat-log')).toContainText('latest ping')
  })

  test('shows rate-limit error after rapid sends', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    await bootstrapAuthenticated(page, `token-${player.id}`)
    await page.goto('/')

    await page.getByRole('button', { name: 'Chat' }).click()
    for (let i = 0; i < 5; i += 1) {
      await page.getByLabel('Chat message').fill(`spam ${i}`)
      await page.getByRole('button', { name: 'Send' }).click()
      await expect(page.getByLabel('Chat message')).toHaveValue('')
    }
    await page.getByLabel('Chat message').fill('spam 5')
    await page.getByRole('button', { name: 'Send' }).click()
    await expect(page.locator('.panel-footer .chat-state-error')).toContainText('sending messages too fast')
  })
})

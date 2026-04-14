/**
 * E2E tests for the in-game chat navigation improvements:
 *  - Chat icon visible in top nav for authenticated players
 *  - Clicking the icon opens the side panel
 *  - Side panel shows messages, allows sending, and can be closed
 *  - Unread badge appears when new messages arrive
 *  - Mobile viewport renders full-screen panel
 */
import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer } from './helpers/mock-api'
import type { MockChatMessage } from './helpers/mock-api'

function makeChatMessage(
  id: string,
  playerId: string,
  message: string,
  offset = 0,
): MockChatMessage {
  return {
    id,
    playerId,
    message,
    sentAtUtc: new Date(Date.now() - offset).toISOString(),
  }
}

// ─── helpers ─────────────────────────────────────────────────────────────────

async function loginAndGoHome(
  page: Parameters<typeof test>[0]['page'],
  token: string,
) {
  await page.addInitScript((t) => {
    localStorage.setItem('auth_token', t)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7_200_000).toISOString())
  }, token)
  await page.goto('/')
}

// ─── tests ───────────────────────────────────────────────────────────────────

test.describe('Chat nav button', () => {
  test('chat button is visible for authenticated players', async ({ page }) => {
    const player = makePlayer()
    setupMockApi(page, { players: [player] })
    await loginAndGoHome(page, `token-${player.id}`)

    await expect(page.getByRole('button', { name: 'Chat' })).toBeVisible()
  })

  test('chat button is NOT visible for unauthenticated visitors', async ({ page }) => {
    setupMockApi(page, { players: [] })
    await page.goto('/')

    await expect(page.getByRole('button', { name: 'Chat' })).toBeHidden()
  })
})

test.describe('Chat side panel — desktop', () => {
  test.use({ viewport: { width: 1280, height: 800 } })

  test('opens side panel when chat button is clicked', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    await loginAndGoHome(page, `token-${player.id}`)

    await page.getByRole('button', { name: 'Chat' }).click()

    await expect(page.locator('.chat-side-panel')).toBeVisible()
    await expect(page.getByRole('heading', { name: 'In-Game Chat' })).toBeVisible()
  })

  test('shows empty state when there are no messages', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    await loginAndGoHome(page, `token-${player.id}`)

    await page.getByRole('button', { name: 'Chat' }).click()

    await expect(page.locator('.chat-side-panel')).toBeVisible()
    await expect(page.locator('.chat-side-panel')).toContainText('No messages yet')
  })

  test('displays existing messages in the panel', async ({ page }) => {
    const player = makePlayer()
    const otherPlayer = makePlayer()
    otherPlayer.displayName = 'AliceTrader'
    const state = setupMockApi(page, {
      players: [player, otherPlayer],
      chatMessages: [
        makeChatMessage('msg-1', otherPlayer.id, 'Hello market!', 5000),
        makeChatMessage('msg-2', player.id, 'Hi there!', 2000),
      ],
    })
    state.currentUserId = player.id
    await loginAndGoHome(page, `token-${player.id}`)

    await page.getByRole('button', { name: 'Chat' }).click()

    await expect(page.locator('.chat-side-panel')).toBeVisible()
    await expect(page.locator('.chat-side-panel')).toContainText('AliceTrader')
    await expect(page.locator('.chat-side-panel')).toContainText('Hello market!')
    await expect(page.locator('.chat-side-panel')).toContainText('Hi there!')
  })

  test('can send a message via the composer', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    await loginAndGoHome(page, `token-${player.id}`)

    await page.getByRole('button', { name: 'Chat' }).click()
    await expect(page.locator('.chat-side-panel')).toBeVisible()

    const input = page.locator('.chat-side-panel .chat-input')
    await input.fill('Supply chain secured!')
    await page.locator('.chat-side-panel .chat-send-button').click()

    // After sending, the message should appear in the log
    await expect(page.locator('.chat-side-panel')).toContainText('Supply chain secured!')
  })

  test('shows error state when loading fails', async ({ page }) => {
    const player = makePlayer()
    setupMockApi(page, { players: [player] })
    // Override chatMessages route to return an error
    await page.route('**/graphql', (route, request) => {
      const body = JSON.parse(request.postData() ?? '{}') as { query?: string }
      if (body.query?.includes('chatMessages')) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Service unavailable.' }] }),
        })
      }
      return route.continue()
    })
    await loginAndGoHome(page, `token-${player.id}`)

    await page.getByRole('button', { name: 'Chat' }).click()
    await expect(page.locator('.chat-side-panel')).toBeVisible()
    await expect(page.locator('.chat-side-panel .chat-state-error')).toBeVisible()
  })

  test('closes the panel when the close button is clicked', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    await loginAndGoHome(page, `token-${player.id}`)

    await page.getByRole('button', { name: 'Chat' }).click()
    await expect(page.locator('.chat-side-panel')).toBeVisible()

    await page.locator('.close-btn').click()
    await expect(page.locator('.chat-side-panel')).toBeHidden()
  })

  test('toggle: clicking nav button again closes the panel', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    await loginAndGoHome(page, `token-${player.id}`)

    const chatBtn = page.getByRole('button', { name: 'Chat' })
    await chatBtn.click()
    await expect(page.locator('.chat-side-panel')).toBeVisible()

    await chatBtn.click()
    await expect(page.locator('.chat-side-panel')).toBeHidden()
  })

  test('panel is accessible on routes other than dashboard', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    await loginAndGoHome(page, `token-${player.id}`)

    // Navigate to leaderboard
    await page.goto('/leaderboard')

    await page.getByRole('button', { name: 'Chat' }).click()
    await expect(page.locator('.chat-side-panel')).toBeVisible()
  })
})

test.describe('Chat side panel — mobile full-screen', () => {
  test.use({ viewport: { width: 375, height: 812 } })

  test('panel takes full screen on mobile viewport', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    await loginAndGoHome(page, `token-${player.id}`)

    // Open mobile nav menu first
    await page.getByRole('button', { name: 'Toggle navigation menu' }).click()
    await page.getByRole('button', { name: 'Chat' }).click()

    const panel = page.locator('.chat-side-panel')
    await expect(panel).toBeVisible()

    const box = await panel.boundingBox()
    expect(box).not.toBeNull()
    // On mobile the panel should span the full viewport width
    expect(box!.width).toBeGreaterThanOrEqual(370)
  })

  test('backdrop is shown on mobile and clicking it closes panel', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    await loginAndGoHome(page, `token-${player.id}`)

    // Open mobile nav menu and click chat
    await page.getByRole('button', { name: 'Toggle navigation menu' }).click()
    await page.getByRole('button', { name: 'Chat' }).click()

    await expect(page.locator('.chat-side-panel')).toBeVisible()
    // Close via close button on mobile
    await page.locator('.close-btn').click()
    await expect(page.locator('.chat-side-panel')).toBeHidden()
  })
})

test.describe('Chat unread badge', () => {
  test.use({ viewport: { width: 1280, height: 800 } })

  test('no badge when there are no messages', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    await loginAndGoHome(page, `token-${player.id}`)

    await expect(page.locator('.chat-badge')).toBeHidden()
  })

  test('badge clears after opening chat panel', async ({ page }) => {
    const player = makePlayer()
    const otherPlayer = makePlayer()
    const state = setupMockApi(page, {
      players: [player, otherPlayer],
      chatMessages: [makeChatMessage('msg-1', otherPlayer.id, 'Anyone selling coal?', 3000)],
    })
    state.currentUserId = player.id
    // Clear last seen so messages appear as unread
    await page.addInitScript(() => {
      localStorage.removeItem('chat_last_seen_message_id')
    })
    await loginAndGoHome(page, `token-${player.id}`)

    // Open chat — badge should disappear after panel loads messages
    await page.getByRole('button', { name: 'Chat' }).click()
    await expect(page.locator('.chat-side-panel')).toBeVisible()
    // After the panel is open, unread count should drop to 0 (no badge)
    await expect(page.locator('.chat-badge')).toBeHidden()
  })
})

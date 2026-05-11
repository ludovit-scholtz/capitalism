import { test, expect } from '@playwright/test'
import { makePlayer, setupMockApi } from '../../helpers/mock-api'

async function authenticate(page: Parameters<typeof test>[0]['page'], token: string) {
  await page.addInitScript((value) => {
    localStorage.setItem('auth_token', value)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, token)
}

test.describe('News mark all as read', () => {
  test('marks all unread entries as read after confirmation', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.gameNewsEntries = [
      {
        id: 'news-unread-1',
        entryType: 'NEWS',
        status: 'PUBLISHED',
        targetServerKey: null,
        createdByEmail: 'system@capitalism.local',
        updatedByEmail: 'system@capitalism.local',
        createdAtUtc: '2026-01-01T00:00:00Z',
        updatedAtUtc: '2026-01-01T00:00:00Z',
        publishedAtUtc: '2026-01-01T00:00:00Z',
        localizations: [{ locale: 'en', title: 'Unread 1', summary: 'Summary', htmlContent: '<p>Body</p>' }],
        readByPlayerIds: [],
      },
      {
        id: 'news-unread-2',
        entryType: 'CHANGELOG',
        status: 'PUBLISHED',
        targetServerKey: null,
        createdByEmail: 'system@capitalism.local',
        updatedByEmail: 'system@capitalism.local',
        createdAtUtc: '2026-01-02T00:00:00Z',
        updatedAtUtc: '2026-01-02T00:00:00Z',
        publishedAtUtc: '2026-01-02T00:00:00Z',
        localizations: [{ locale: 'en', title: 'Unread 2', summary: 'Summary', htmlContent: '<p>Body</p>' }],
        readByPlayerIds: [],
      },
    ]

    await authenticate(page, `token-${player.id}`)
    await page.goto('/news')

    await expect(page.locator('.news-unread-badge')).toHaveCount(2)

    page.once('dialog', async (dialog) => {
      expect(dialog.type()).toBe('confirm')
      await dialog.accept()
    })
    await page.getByRole('button', { name: 'Mark all as read' }).click()

    await expect(page.locator('.news-unread-badge')).toHaveCount(0)
    await expect(page.getByText('All news entries were marked as read.')).toBeVisible()
  })

  test('keeps entries unread when confirmation is cancelled', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.gameNewsEntries = [
      {
        id: 'news-unread-cancel',
        entryType: 'NEWS',
        status: 'PUBLISHED',
        targetServerKey: null,
        createdByEmail: 'system@capitalism.local',
        updatedByEmail: 'system@capitalism.local',
        createdAtUtc: '2026-01-01T00:00:00Z',
        updatedAtUtc: '2026-01-01T00:00:00Z',
        publishedAtUtc: '2026-01-01T00:00:00Z',
        localizations: [{ locale: 'en', title: 'Unread', summary: 'Summary', htmlContent: '<p>Body</p>' }],
        readByPlayerIds: [],
      },
    ]

    await authenticate(page, `token-${player.id}`)
    await page.goto('/news')
    await expect(page.locator('.news-unread-badge')).toHaveCount(1)

    page.once('dialog', async (dialog) => {
      await dialog.dismiss()
    })
    await page.getByRole('button', { name: 'Mark all as read' }).click()

    await expect(page.locator('.news-unread-badge')).toHaveCount(1)
  })

  test('news feed neutralizes svg onload payload', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.gameNewsEntries = [
      {
        id: 'news-xss-svg',
        entryType: 'NEWS',
        status: 'PUBLISHED',
        targetServerKey: null,
        createdByEmail: 'system@capitalism.local',
        updatedByEmail: 'system@capitalism.local',
        createdAtUtc: '2026-01-01T00:00:00Z',
        updatedAtUtc: '2026-01-01T00:00:00Z',
        publishedAtUtc: '2026-01-01T00:00:00Z',
        localizations: [
          {
            locale: 'en',
            title: 'Xss payload',
            summary: '',
            htmlContent: '<svg onload=alert(1)><circle></circle></svg><p>Safe body</p>',
          },
        ],
        readByPlayerIds: [],
      },
    ]

    await page.addInitScript(() => {
      ;(window as Window & { __alerts: string[] }).__alerts = []
      window.alert = (message?: string) => {
        ;(window as Window & { __alerts: string[] }).__alerts.push(String(message ?? ''))
      }
    })
    await authenticate(page, `token-${player.id}`)
    await page.goto('/news')

    await expect(page.locator('.news-card-body')).toContainText('Safe body')
    const alertCount = await page.evaluate(
      () => (window as Window & { __alerts?: string[] }).__alerts?.length ?? 0,
    )
    expect(alertCount).toBe(0)
  })
})

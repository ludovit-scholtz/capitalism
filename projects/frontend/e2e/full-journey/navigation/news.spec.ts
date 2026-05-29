import { test, expect, type Page } from '@playwright/test'
import { makePlayer, setupMockApi, type MockGameNewsEntry } from '../../helpers/mock-api'

async function authenticate(page: Page, token: string) {
  await page.addInitScript((value) => {
    localStorage.setItem('auth_token', value)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, token)
}

const makeNewsEntry = (overrides: Partial<MockGameNewsEntry> = {}): MockGameNewsEntry => ({
  id: `news-${Date.now()}-${Math.random()}`,
  entryType: 'NEWS',
  status: 'PUBLISHED',
  targetServerKey: null,
  createdByEmail: 'admin@test.com',
  updatedByEmail: 'admin@test.com',
  createdAtUtc: '2026-01-10T08:00:00Z',
  updatedAtUtc: '2026-01-10T08:00:00Z',
  publishedAtUtc: '2026-01-10T08:00:00Z',
  readByPlayerIds: [],
  localizations: [
    {
      locale: 'en',
      title: 'Default News Title',
      summary: 'Default summary text.',
      htmlContent: '<p>Default HTML content.</p>',
    },
  ],
  ...overrides,
})

const makeChangelogEntry = (overrides: Partial<MockGameNewsEntry> = {}): MockGameNewsEntry => makeNewsEntry({ entryType: 'CHANGELOG', ...overrides })

test.describe('News feed — public access', () => {
  test('unauthenticated visitor can browse the news page and see changelog entries', async ({ page }) => {
    setupMockApi(page, {
      players: [],
      gameNewsEntries: [
        makeChangelogEntry({
          id: 'cl-1',
          localizations: [
            {
              locale: 'en',
              title: 'Version 1.0 released',
              summary: 'Initial public release of the game.',
              htmlContent: '<p>The game is now publicly available.</p>',
            },
          ],
        }),
      ],
    })

    await page.goto('/news')

    await expect(page.getByRole('heading', { name: 'Newsroom & Changelog' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Version 1.0 released' })).toBeVisible()
    await expect(page.getByText('Initial public release of the game.')).toBeVisible()
    // Unauthenticated: no badge rendered
    await expect(page.locator('.news-badge')).toHaveCount(0)
  })

  test('empty state is displayed with informative text when no entries exist', async ({ page }) => {
    setupMockApi(page, {
      players: [],
      gameNewsEntries: [],
    })

    await page.goto('/news')

    await expect(page.locator('.state-card')).toBeVisible()
    await expect(page.getByText('No published entries yet')).toBeVisible()
    await expect(page.getByText('When administrators publish news or changelog notes, they will appear here.')).toBeVisible()
  })

  test('error state is displayed with retry button when the feed fails to load', async ({ page }) => {
    setupMockApi(page, {
      players: [],
      gameNewsEntries: [],
    })

    // Override the gameNewsFeed handler to return a server error response.
    // Must be registered AFTER setupMockApi so it takes priority (LIFO ordering).
    await page.route('**/graphql', (route) => {
      const postData = route.request().postDataJSON() as { query?: string } | null
      const query = postData?.query ?? ''
      if (query.includes('gameNewsFeed')) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'News feed is temporarily unavailable' }] }),
        })
      }
      return route.continue()
    })

    await page.goto('/news')

    await expect(page.locator('.state-card-error')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Try again' })).toBeVisible()
  })

  test('renders multiple entries with publication dates and html content', async ({ page }) => {
    setupMockApi(page, {
      players: [],
      gameNewsEntries: [
        makeChangelogEntry({
          id: 'cl-multi-1',
          publishedAtUtc: '2026-03-15T10:00:00Z',
          localizations: [
            {
              locale: 'en',
              title: 'March Changelog Entry',
              summary: 'New buildings added to the city.',
              htmlContent: '<p>Three new industrial buildings are now available in Prague.</p>',
            },
          ],
        }),
        makeChangelogEntry({
          id: 'cl-multi-2',
          publishedAtUtc: '2026-04-01T09:00:00Z',
          localizations: [
            {
              locale: 'en',
              title: 'April Changelog Entry',
              summary: 'Tax system updated.',
              htmlContent: '<p>The tax rate now applies to net profit rather than gross revenue.</p>',
            },
          ],
        }),
      ],
    })

    await page.goto('/news')

    await expect(page.getByRole('heading', { name: 'March Changelog Entry' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'April Changelog Entry' })).toBeVisible()
    await expect(page.getByText('New buildings added to the city.')).toBeVisible()
    await expect(page.getByText('Tax system updated.')).toBeVisible()
    // HTML content is rendered
    await expect(page.getByText('Three new industrial buildings are now available in Prague.')).toBeVisible()
    await expect(page.getByText('The tax rate now applies to net profit rather than gross revenue.')).toBeVisible()
  })

  test('unauthenticated users see no NEW badge even on unread entries', async ({ page }) => {
    setupMockApi(page, {
      players: [],
      gameNewsEntries: [
        makeChangelogEntry({
          id: 'public-entry',
          readByPlayerIds: [],
          localizations: [
            {
              locale: 'en',
              title: 'Public Changelog Entry',
              summary: 'Open to all.',
              htmlContent: '<p>Info.</p>',
            },
          ],
        }),
      ],
    })

    await page.goto('/news')

    const card = page.locator('.news-card', { hasText: 'Public Changelog Entry' })
    await expect(card).toBeVisible()
    // Unauthenticated — no NEW badge regardless of read state
    await expect(card.locator('.news-unread-badge')).toHaveCount(0)
  })
})

test.describe('News feed — type filters', () => {
  test('changelog filter shows only changelog entries', async ({ page }) => {
    setupMockApi(page, {
      players: [],
      gameNewsEntries: [
        makeNewsEntry({
          id: 'news-only',
          localizations: [
            {
              locale: 'en',
              title: 'Market News Headline',
              summary: 'News summary',
              htmlContent: '<p>News body</p>',
            },
          ],
        }),
        makeChangelogEntry({
          id: 'cl-only',
          localizations: [
            {
              locale: 'en',
              title: 'Changelog Update',
              summary: 'Changelog summary',
              htmlContent: '<p>Changelog body</p>',
            },
          ],
        }),
      ],
    })

    await page.goto('/news')

    // All entries visible initially
    await expect(page.getByRole('heading', { name: 'Market News Headline' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Changelog Update' })).toBeVisible()

    // Click Changelog filter tab
    await page.getByRole('button', { name: 'Changelog' }).click()

    await expect(page.getByRole('heading', { name: 'Changelog Update' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Market News Headline' })).toHaveCount(0)
  })

  test('newspaper filter shows only news entries', async ({ page }) => {
    setupMockApi(page, {
      players: [],
      gameNewsEntries: [
        makeNewsEntry({
          id: 'news-only-2',
          localizations: [
            {
              locale: 'en',
              title: 'Breaking Economic News',
              summary: 'News summary',
              htmlContent: '<p>News body</p>',
            },
          ],
        }),
        makeChangelogEntry({
          id: 'cl-only-2',
          localizations: [
            {
              locale: 'en',
              title: 'Patch Notes Entry',
              summary: 'Changelog summary',
              htmlContent: '<p>Changelog body</p>',
            },
          ],
        }),
      ],
    })

    await page.goto('/news')

    // Click Newspaper filter tab
    await page.getByRole('button', { name: 'Newspaper' }).click()

    await expect(page.getByRole('heading', { name: 'Breaking Economic News' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Patch Notes Entry' })).toHaveCount(0)
  })
})

test.describe('News feed — entry cards and badges', () => {
  test('news entry badge pill is shown with correct label', async ({ page }) => {
    setupMockApi(page, {
      players: [],
      gameNewsEntries: [
        makeChangelogEntry({
          id: 'pill-cl',
          localizations: [{ locale: 'en', title: 'Changelog Pill Test', summary: '', htmlContent: '' }],
        }),
        makeNewsEntry({
          id: 'pill-news',
          localizations: [{ locale: 'en', title: 'News Pill Test', summary: '', htmlContent: '' }],
        }),
      ],
    })

    await page.goto('/news')

    const changelogCard = page.locator('.news-card', { hasText: 'Changelog Pill Test' })
    await expect(changelogCard.locator('.news-pill-changelog')).toContainText('Changelog')

    const newsCard = page.locator('.news-card', { hasText: 'News Pill Test' })
    await expect(newsCard.locator('.news-pill-news')).toContainText('Newspaper')
  })

  test('newsroom paginates entries and shows 10 items by default', async ({ page }) => {
    const pagedEntries = Array.from({ length: 12 }, (_, index) => {
      const number = index + 1
      return makeChangelogEntry({
        id: `paged-cl-${number}`,
        publishedAtUtc: `2026-03-${String(number).padStart(2, '0')}T10:00:00Z`,
        localizations: [
          {
            locale: 'en',
            title: `Paginated Entry ${number}`,
            summary: `Summary ${number}`,
            htmlContent: `<p>Body ${number}</p>`,
          },
        ],
      })
    })

    setupMockApi(page, {
      players: [],
      gameNewsEntries: pagedEntries,
    })

    await page.goto('/news')

    await expect(page.locator('.news-card')).toHaveCount(10)
    await expect(page.getByRole('heading', { name: 'Paginated Entry 12' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Paginated Entry 2' })).toHaveCount(0)

    await page.getByRole('button', { name: 'Next' }).click()

    await expect(page.locator('.news-card')).toHaveCount(2)
    await expect(page.getByRole('heading', { name: 'Paginated Entry 2' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Paginated Entry 12' })).toHaveCount(0)
  })

  test('category filters paginate from each category first page', async ({ page }) => {
    const marketReports = Array.from({ length: 12 }, (_, index) =>
      makeNewsEntry({
        id: `market-report-${index + 1}`,
        entryType: 'MARKET_REPORT',
        publishedAtUtc: `2026-04-${String(index + 1).padStart(2, '0')}T10:00:00Z`,
        localizations: [
          {
            locale: 'en',
            title: `Market Report ${index + 1}`,
            summary: `Market summary ${index + 1}`,
            htmlContent: `<p>Market body ${index + 1}</p>`,
          },
        ],
      }),
    )

    setupMockApi(page, {
      players: [],
      gameNewsEntries: [
        ...marketReports,
        makeChangelogEntry({
          id: 'category-changelog',
          publishedAtUtc: '2026-03-01T10:00:00Z',
          localizations: [
            {
              locale: 'en',
              title: 'Category Changelog',
              summary: 'Changelog still appears.',
              htmlContent: '<p>Changelog body</p>',
            },
          ],
        }),
      ],
    })

    await page.goto('/news')

    await page.getByRole('button', { name: /Market Reports/ }).click()
    await expect(page.locator('.news-card')).toHaveCount(10)
    await page.getByRole('button', { name: 'Next' }).click()
    await expect(page.getByRole('heading', { name: 'Market Report 2' })).toBeVisible()

    await page.getByRole('button', { name: 'Changelog' }).click()
    await expect(page.locator('.news-card')).toHaveCount(1)
    await expect(page.getByRole('heading', { name: 'Category Changelog' })).toBeVisible()
  })

  test('global news entries are visible on all servers (null targetServerKey)', async ({ page }) => {
    setupMockApi(page, {
      players: [],
      gameNewsEntries: [
        makeChangelogEntry({
          id: 'global-cl',
          targetServerKey: null,
          localizations: [
            {
              locale: 'en',
              title: 'Global Announcement',
              summary: 'This appears everywhere.',
              htmlContent: '<p>Visible across all game servers.</p>',
            },
          ],
        }),
        makeNewsEntry({
          id: 'other-server-news',
          targetServerKey: 'other-server',
          localizations: [
            {
              locale: 'en',
              title: 'Hidden Server News',
              summary: 'Only for other-server players.',
              htmlContent: '<p>Server-specific news.</p>',
            },
          ],
        }),
      ],
    })

    await page.goto('/news')

    await expect(page.getByRole('heading', { name: 'Global Announcement' })).toBeVisible()
    // Entry for another server key should not be visible
    await expect(page.getByRole('heading', { name: 'Hidden Server News' })).toHaveCount(0)
  })
})

test.describe('News feed — authenticated read state', () => {
  test('shows unread news badge and clears it after the news page is opened', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-02T00:00:00Z' })

    setupMockApi(page, {
      players: [player],
      currentUserId: player.id,
      currentToken: `token-${player.id}`,
      gameNewsEntries: [
        {
          id: 'news-1',
          entryType: 'NEWS',
          status: 'PUBLISHED',
          targetServerKey: 'test-server',
          createdByEmail: 'admin@test.com',
          updatedByEmail: 'admin@test.com',
          createdAtUtc: '2026-01-10T08:00:00Z',
          updatedAtUtc: '2026-01-10T08:00:00Z',
          publishedAtUtc: '2026-01-10T08:00:00Z',
          readByPlayerIds: [],
          localizations: [
            {
              locale: 'en',
              title: 'Server Gazette',
              summary: 'A fresh issue is waiting for every founder.',
              htmlContent: '<p>New production dashboards are now live.</p>',
            },
          ],
        },
      ],
    })

    await authenticate(page, `token-${player.id}`)
    await page.goto('/dashboard')

    await page.getByRole('button', { name: 'Main' }).hover()
    const mainPanel = page.locator('.desktop-section-panel')
    await expect(mainPanel.getByRole('link', { name: 'News' }).locator('.desktop-sub-badge')).toContainText('1')

    await mainPanel.getByRole('link', { name: 'News' }).click()

    await expect(page).toHaveURL('/news')
    await expect(page.getByRole('heading', { name: 'Server Gazette' })).toBeVisible()
    await page.getByRole('button', { name: 'Main' }).hover()
    await expect(page.locator('.desktop-section-panel').getByRole('link', { name: 'News' }).locator('.desktop-sub-badge')).toHaveCount(0)
  })

  test('unread entries show NEW badge; already-read entries do not', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-02T00:00:00Z' })
    const state = setupMockApi(page, {
      players: [player],
      gameNewsEntries: [
        makeChangelogEntry({
          id: 'unread-entry',
          readByPlayerIds: [],
          localizations: [
            {
              locale: 'en',
              title: 'Brand New Feature',
              summary: 'Just shipped.',
              htmlContent: '<p>Details.</p>',
            },
          ],
        }),
        makeChangelogEntry({
          id: 'read-entry',
          readByPlayerIds: [player.id],
          localizations: [
            {
              locale: 'en',
              title: 'Old Feature',
              summary: 'Already seen.',
              htmlContent: '<p>Old.</p>',
            },
          ],
        }),
      ],
    })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await authenticate(page, `token-${player.id}`)

    await page.goto('/news')

    // Unread entry must show the NEW badge
    const unreadCard = page.locator('.news-card', { hasText: 'Brand New Feature' })
    await expect(unreadCard.locator('.news-unread-badge')).toBeVisible()
    await expect(unreadCard.locator('.news-unread-badge')).toContainText('New')

    // Already-read entry must NOT show the NEW badge
    const readCard = page.locator('.news-card', { hasText: 'Old Feature' })
    await expect(readCard.locator('.news-unread-badge')).toHaveCount(0)

    // Unread card should have the visual unread class
    await expect(unreadCard).toHaveClass(/news-card-unread/)
    await expect(readCard).not.toHaveClass(/news-card-unread/)
  })

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
})

test.describe('News feed — security', () => {
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
    const alertCount = await page.evaluate(() => (window as Window & { __alerts?: string[] }).__alerts?.length ?? 0)
    expect(alertCount).toBe(0)
  })
})

test.describe('News feed — mobile viewport', () => {
  test('news cards stack and badge is visible in collapsed navbar on mobile', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 })

    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, {
      players: [player],
      gameNewsEntries: [
        makeNewsEntry({
          id: 'mobile-news-1',
          readByPlayerIds: [],
          localizations: [
            {
              locale: 'en',
              title: 'Mobile News One',
              summary: 'News summary for mobile.',
              htmlContent: '<p>Body on mobile.</p>',
            },
          ],
        }),
        makeChangelogEntry({
          id: 'mobile-cl-1',
          readByPlayerIds: [],
          localizations: [
            {
              locale: 'en',
              title: 'Mobile Changelog',
              summary: 'Changelog on mobile.',
              htmlContent: '<p>Changelog body.</p>',
            },
          ],
        }),
      ],
    })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await authenticate(page, `token-${player.id}`)
    await page.goto('/news')

    // Cards are visible on mobile
    await expect(page.locator('.news-card')).toHaveCount(2)
    await expect(page.getByRole('heading', { name: 'Mobile News One' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Mobile Changelog' })).toBeVisible()

    // Both cards show unread badges
    await expect(page.locator('.news-unread-badge')).toHaveCount(2)

    // No horizontal overflow: every card should be within the viewport width
    const cardWidths = await page.locator('.news-card').evaluateAll((cards) => cards.map((c) => c.getBoundingClientRect().width))
    for (const width of cardWidths) {
      expect(width).toBeLessThanOrEqual(375)
    }
  })
})

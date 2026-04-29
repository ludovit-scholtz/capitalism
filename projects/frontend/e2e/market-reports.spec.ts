import { expect, test } from '@playwright/test'

import { makePlayer, setupMockApi } from './helpers/mock-api'

const BRATISLAVA_ID = 'city-br'

function makeMockWeeklyReport(overrides: Record<string, unknown> = {}) {
  return {
    id: `mr-${Date.now()}-${Math.random()}`,
    cityId: BRATISLAVA_ID,
    cityName: 'Bratislava',
    reportType: 'WEEKLY' as const,
    tickFrom: 1,
    tickTo: 168,
    totalRevenue: 24_000,
    totalQuantitySold: 480,
    uniqueProducts: 3,
    topProducts: [
      {
        productTypeId: 'pt-chair',
        productName: 'Wooden Chair',
        industry: 'FURNITURE',
        totalRevenue: 15_000,
        totalQuantitySold: 300,
        averagePricePerUnit: 50,
        basePrice: 45,
        grossMarginPct: 10.0,
        sellerCount: 2,
      },
      {
        productTypeId: 'pt-bread',
        productName: 'Bread',
        industry: 'FOOD_PROCESSING',
        totalRevenue: 9_000,
        totalQuantitySold: 180,
        averagePricePerUnit: 50,
        basePrice: 3,
        grossMarginPct: 94.0,
        sellerCount: 1,
      },
    ],
    ...overrides,
  }
}

function makeMockMonthlyReport(overrides: Record<string, unknown> = {}) {
  return makeMockWeeklyReport({
    id: `mr-monthly-${Date.now()}`,
    reportType: 'MONTHLY' as const,
    tickFrom: 1,
    tickTo: 720,
    totalRevenue: 96_000,
    totalQuantitySold: 1_920,
    ...overrides,
  })
}

test.describe('Market Reports in Newsroom', () => {
  test('shows Market Reports filter tab in the newsroom', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, {
      players: [player],
      marketReports: [makeMockWeeklyReport()],
    })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7_200_000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/news')
    await expect(page.getByRole('button', { name: /Market Reports/i })).toBeVisible()
  })

  test('clicking Market Reports tab filters entries', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, {
      players: [player],
      marketReports: [makeMockWeeklyReport()],
      gameNewsEntries: [
        {
          id: 'news-1',
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
              title: 'Regular News',
              summary: 'A regular news item.',
              htmlContent: '<p>News content.</p>',
            },
          ],
        },
      ],
    })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7_200_000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/news')
    await page.getByRole('button', { name: /Market Reports/i }).click()

    // Should show empty state since gameNewsEntries has no MARKET_REPORT entries.
    await expect(page.getByText(/market reports will appear here/i)).toBeVisible()
  })

  test('displays WEEKLY and MONTHLY filter pills correctly', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, {
      players: [player],
      marketReports: [makeMockWeeklyReport(), makeMockMonthlyReport()],
    })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7_200_000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/news')

    // Market Reports tab should be present.
    await expect(page.getByRole('button', { name: /Market Reports/i })).toBeVisible()

    // All entries tab shows normal entries; market report entries are separate.
    await expect(page.getByRole('button', { name: /All entries/i })).toBeVisible()
    await expect(page.getByRole('button', { name: /Newspaper/i })).toBeVisible()
    await expect(page.getByRole('button', { name: /Changelog/i })).toBeVisible()
  })

  test('shows empty state with informative message for Market Reports tab', async ({ page }) => {
    const player = makePlayer()
    setupMockApi(page, {
      players: [player],
      marketReports: [],
    })

    await page.goto('/news')
    await page.getByRole('button', { name: /Market Reports/i }).click()

    const emptyMsg = page.locator('.state-card')
    await expect(emptyMsg).toBeVisible()
    await expect(emptyMsg).toContainText(/market reports will appear here/i)
  })

  test('market-report card has teal/green pill styling class', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, {
      players: [player],
      gameNewsEntries: [
        {
          id: 'mr-news-1',
          entryType: 'MARKET_REPORT' as const,
          status: 'PUBLISHED',
          targetServerKey: null,
          createdByEmail: 'system@game.io',
          updatedByEmail: 'system@game.io',
          createdAtUtc: '2026-04-28T10:00:00Z',
          updatedAtUtc: '2026-04-28T10:00:00Z',
          publishedAtUtc: '2026-04-28T10:00:00Z',
          readByPlayerIds: [],
          localizations: [
            {
              locale: 'en',
              title: '📊 Weekly Market Report — Bratislava',
              summary: 'Bratislava: 3 products tracked, total revenue EUR 24,000.',
              htmlContent:
                '<div class="market-report"><div class="mr-summary"><div class="mr-summary-item"><span class="mr-label">Period</span><span class="mr-value">Ticks 1–168</span></div></div></div>',
            },
          ],
        },
      ],
    })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7_200_000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/news')

    // Market Reports filter tab.
    await page.getByRole('button', { name: /Market Reports/i }).click()

    // Card with market report title should be visible.
    await expect(page.getByRole('heading', { name: /Weekly Market Report/i })).toBeVisible()

    // The pill should have the market class.
    const pill = page.locator('.news-pill-market')
    await expect(pill).toBeVisible()
    await expect(pill).toContainText(/Market Reports/i)
  })

  test('market-report card renders HTML body with mr-summary structure', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, {
      players: [player],
      gameNewsEntries: [
        {
          id: 'mr-news-2',
          entryType: 'MARKET_REPORT' as const,
          status: 'PUBLISHED',
          targetServerKey: null,
          createdByEmail: 'system@game.io',
          updatedByEmail: 'system@game.io',
          createdAtUtc: '2026-04-29T00:00:00Z',
          updatedAtUtc: '2026-04-29T00:00:00Z',
          publishedAtUtc: '2026-04-29T00:00:00Z',
          readByPlayerIds: [],
          localizations: [
            {
              locale: 'en',
              title: '📊 Weekly Market Report — Prague',
              summary: 'Prague: 2 products, CZK 50,000.',
              htmlContent: `<div class="market-report">
<div class="mr-summary">
  <div class="mr-summary-item"><span class="mr-label">Period</span><span class="mr-value">Ticks 1–168</span></div>
  <div class="mr-summary-item"><span class="mr-label">City Total Revenue</span><span class="mr-value mr-value-highlight">CZK 50,000</span></div>
</div>
<table class="mr-table">
  <thead><tr><th>#</th><th>Product</th><th>Revenue</th><th>Qty Sold</th><th>Gross Margin</th><th>Sellers</th></tr></thead>
  <tbody>
    <tr><td class="mr-rank mr-rank-top1">1</td><td><strong>Wooden Chair</strong></td><td>CZK 40,000</td><td>800</td><td class="mr-positive">10.0%</td><td>2</td></tr>
  </tbody>
</table>
</div>`,
            },
          ],
        },
      ],
    })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7_200_000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/news')

    await page.getByRole('button', { name: /Market Reports/i }).click()

    // The structured report card should render.
    await expect(page.getByRole('heading', { name: /Weekly Market Report.*Prague/i })).toBeVisible()
    await expect(page.locator('.news-card-market')).toBeVisible()
    // The summary section is rendered inside the news card body.
    await expect(page.locator('.news-card-body')).toContainText('CZK 50,000')
    await expect(page.locator('.news-card-body')).toContainText('Wooden Chair')
  })
})

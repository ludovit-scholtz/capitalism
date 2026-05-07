import { expect, test } from '@playwright/test'
import { makeAdminPlayer, makePlayer, setupMockApi } from '../../helpers/mock-api'

function buildAdminSession(state: ReturnType<typeof setupMockApi>, admin: ReturnType<typeof makeAdminPlayer>) {
  state.currentUserId = admin.id
  state.currentToken = `token-${admin.id}`
  state.rootAdminEmails = [admin.email]
}

async function goToOperationsAsAdmin(
  page: Parameters<typeof setupMockApi>[0],
  state: ReturnType<typeof setupMockApi>,
  admin: ReturnType<typeof makeAdminPlayer>,
  subPath = '',
) {
  await page.addInitScript(
    ([token, expires]: [string, string]) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', expires)
    },
    [`token-${admin.id}`, new Date(Date.now() + 7200000).toISOString()],
  )
  void state
  await page.goto(`/operations${subPath}`)
}

test('Operations dashboard redirects to the canonical /operations/statistics route', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin] })
  buildAdminSession(state, admin)

  await goToOperationsAsAdmin(page, state, admin)

  await expect(page).toHaveURL(/\/operations\/statistics$/)
})

test('Operations dashboard uses level-2 navigation and removes the old /admin nav link', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin] })
  buildAdminSession(state, admin)

  await goToOperationsAsAdmin(page, state, admin)

  const subnav = page.locator('.ops-subnav')
  await expect(subnav).toBeVisible()
  await expect(subnav.getByRole('link', { name: /overview/i })).toBeVisible()
  await expect(subnav.getByRole('link', { name: /money flow/i })).toBeVisible()
  await expect(subnav.getByRole('link', { name: /product analytics/i })).toBeVisible()
  await expect(subnav.getByRole('link', { name: /news/i })).toBeVisible()
  await expect(subnav.getByRole('link', { name: /players/i })).toBeVisible()
  await expect(page.locator('a[href="/admin"]')).toHaveCount(0)
})

test('Operations dashboard is hidden for non-admin players', async ({ page }) => {
  const player = makePlayer()
  player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  await page.addInitScript(
    ([token, expires]: [string, string]) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', expires)
    },
    [`token-${player.id}`, new Date(Date.now() + 7200000).toISOString()],
  )

  await page.goto('/operations')

  await expect(page.locator('.ops-subnav')).toBeHidden()
})

test('Overview page loads merged admin metrics and audit panels', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin] })
  buildAdminSession(state, admin)

  await goToOperationsAsAdmin(page, state, admin, '/statistics')

  await expect(page.getByRole('heading', { name: /operations overview/i })).toBeVisible()
  await expect(page.locator('.ops-metrics')).toBeVisible()
  await expect(page.locator('.ops-grid').first()).toContainText(/money distribution signals|intervention alerts/i)
})

test('Money flow page shows live range filter and two-column inflow/outflow layout', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin] })
  buildAdminSession(state, admin)

  await goToOperationsAsAdmin(page, state, admin, '/statistics/money-flow')

  await expect(page.getByRole('heading', { name: /money flow/i })).toBeVisible()
  await expect(page.locator('.ops-flow-grid')).toBeVisible()
  await expect(page.locator('.ops-flow-panel').first()).toContainText('Public Sales Revenue')
  await expect(page.locator('.ops-flow-panel').last()).toContainText('Labor Costs')
  await page.locator('.ops-range-filter select').selectOption('LAST_30_DAYS')
  await expect(page.locator('.ops-range-filter select')).toHaveValue('LAST_30_DAYS')
})

test('News and changelog page uses a split list/editor layout with pagination', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin] })
  buildAdminSession(state, admin)
  state.gameNewsEntries = Array.from({ length: 21 }, (_, index) => ({
    id: `news-${index + 1}`,
    entryType: index % 2 === 0 ? 'NEWS' : 'CHANGELOG',
    status: 'PUBLISHED',
    targetServerKey: null,
    createdByEmail: admin.email,
    updatedByEmail: admin.email,
    createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: '2026-01-02T00:00:00Z',
    publishedAtUtc: '2026-01-02T00:00:00Z',
    readByPlayerIds: [],
    localizations: [
      {
        locale: 'en',
        title: `Entry ${index + 1}`,
        summary: `Summary ${index + 1}`,
        htmlContent: `<p>Entry ${index + 1}</p>`,
      },
    ],
  }))

  await goToOperationsAsAdmin(page, state, admin, '/statistics/news')

  await expect(page.locator('.ops-news-layout')).toBeVisible()
  await expect(page.locator('.ops-news-list-panel')).toContainText('Page 1 of 2')
  await expect(page.locator('.ops-editor-panel')).toBeVisible()
  await page.getByRole('button', { name: 'Next' }).click()
  await expect(page.locator('.ops-news-list-panel')).toContainText('Page 2 of 2')
})

test('Players page filters by city/company and opens player detail actions', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const player = makePlayer({ id: 'player-2', email: 'alice@test.com', displayName: 'Alice' })
  player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  player.companies = [
    {
      id: 'company-vienna-foods',
      playerId: player.id,
      name: 'Vienna Foods',
      cash: 250000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [
        {
          id: 'building-vienna-foods',
          companyId: 'company-vienna-foods',
          cityId: 'city-vi',
          type: 'SALES_SHOP',
          name: 'Vienna Shop',
          latitude: 48.2,
          longitude: 16.37,
          level: 1,
          powerConsumption: 0,
          isForSale: false,
          units: [],
        },
      ],
    },
  ]
  const state = setupMockApi(page, { players: [admin, player] })
  buildAdminSession(state, admin)

  await goToOperationsAsAdmin(page, state, admin, '/statistics/players')

  await page.locator('.ops-filter').first().selectOption('Vienna')
  await page.locator('.ops-filter').nth(1).selectOption('Vienna Foods')
  await page.locator('.ops-search').fill('alice@test.com')
  await expect(page.locator('table[aria-label="Player list"]')).toContainText('Alice')
  await page.getByRole('button', { name: /view detail/i }).first().click()
  await expect(page).toHaveURL(/\/operations\/statistics\/players\/player-2/)
  await expect(page.locator('.ops-actions-panel')).toBeVisible()
})

test('Product analytics page filters and exports CSV', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin] })
  buildAdminSession(state, admin)

  await goToOperationsAsAdmin(page, state, admin, '/statistics/product-analytics')

  await expect(page.locator('table[aria-label="Product analytics"]')).toBeVisible()
  await page.locator('.ops-industry-select').selectOption('FURNITURE')
  await page.locator('.ops-search').fill('Wooden Chair')
  await expect(page.locator('table[aria-label="Product analytics"]')).toContainText('Wooden Chair')
  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: /export csv/i }).click()
  const download = await downloadPromise
  expect(download.suggestedFilename()).toContain('operations-product-analytics-')
})

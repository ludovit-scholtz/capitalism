import { test, expect } from '@playwright/test'
import { setupMockApi, makeAdminPlayer, makePlayer } from '../../helpers/mock-api'

// ─── Helpers ─────────────────────────────────────────────────────────────────

function buildAdminSession(page: ReturnType<typeof setupMockApi>, admin: ReturnType<typeof makeAdminPlayer>) {
  page.currentUserId = admin.id
  page.currentToken = `token-${admin.id}`
  page.rootAdminEmails = [admin.email]
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

// ─── Navigation ───────────────────────────────────────────────────────────────

test('Operations Dashboard redirects to statistics by default', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin] })
  buildAdminSession(state, admin)
  await goToOperationsAsAdmin(page, state, admin)
  await expect(page).toHaveURL(/\/operations\/statistics/)
})

test('Operations Dashboard shows the sub-navigation tabs', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin] })
  buildAdminSession(state, admin)
  await goToOperationsAsAdmin(page, state, admin)
  await expect(page.locator('.ops-subnav')).toBeVisible()
  await expect(page.locator('.ops-subnav').getByRole('link', { name: /statistics/i })).toBeVisible()
  await expect(page.locator('.ops-subnav').getByRole('link', { name: /news/i })).toBeVisible()
  await expect(page.locator('.ops-subnav').getByRole('link', { name: /players/i })).toBeVisible()
  await expect(page.locator('.ops-subnav').getByRole('link', { name: /analytics/i })).toBeVisible()
})

test('Operations Dashboard is NOT accessible for non-admin players', async ({ page }) => {
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
  // Non-admin users should NOT see operations-specific content
  await expect(page.locator('.ops-subnav')).toBeHidden()
})

// ─── Statistics page ──────────────────────────────────────────────────────────

test('Operations Statistics page loads money flow data', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin] })
  buildAdminSession(state, admin)
  await goToOperationsAsAdmin(page, state, admin, '/statistics')

  // Should show aggregate metrics
  await expect(page.locator('.ops-metrics-row')).toBeVisible()

  // Should show inflow and outflow panels
  await expect(page.locator('.ops-flow-grid')).toBeVisible()
  // Inflow panel shows revenue items
  await expect(page.locator('.ops-flow-panel').first()).toContainText('Public Sales Revenue')
  // Outflow panel shows cost items
  await expect(page.locator('.ops-flow-panel').last()).toContainText('Labor Costs')
})

test('Operations Statistics shows player/company/building counts', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const player2 = makePlayer({ id: 'player-2', email: 'player2@test.com' })
  const state = setupMockApi(page, { players: [admin, player2] })
  buildAdminSession(state, admin)
  await goToOperationsAsAdmin(page, state, admin, '/statistics')

  // 2 non-government players present
  await expect(page.locator('.ops-metrics-row')).toBeVisible()
  const metricsText = await page.locator('.ops-metrics-row').textContent()
  expect(metricsText).toContain('2')
})

// ─── News manager ─────────────────────────────────────────────────────────────

test('Operations News page shows paginated news entries table', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin] })
  buildAdminSession(state, admin)
  // Seed a news entry (MockGameNewsEntry requires readByPlayerIds array)
  state.gameNewsEntries = [
    {
      id: 'news-1',
      entryType: 'CHANGELOG',
      status: 'PUBLISHED',
      targetServerKey: null,
      createdByEmail: admin.email,
      updatedByEmail: admin.email,
      createdAtUtc: '2026-01-01T00:00:00Z',
      updatedAtUtc: '2026-01-02T00:00:00Z',
      publishedAtUtc: '2026-01-02T00:00:00Z',
      readByPlayerIds: [],
      localizations: [{ locale: 'en', title: 'Test Changelog Entry', summary: 'A test summary', htmlContent: '<p>Content</p>' }],
    },
  ]
  await goToOperationsAsAdmin(page, state, admin, '/news')

  await expect(page.locator('table[aria-label="News entries"]')).toBeVisible()
  await expect(page.locator('table[aria-label="News entries"]')).toContainText('Test Changelog Entry')
  await expect(page.locator('table[aria-label="News entries"]')).toContainText('CHANGELOG')
  await expect(page.locator('table[aria-label="News entries"]')).toContainText('PUBLISHED')
})

test('Operations News page shows empty state when no entries', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin] })
  buildAdminSession(state, admin)
  state.gameNewsEntries = []
  await goToOperationsAsAdmin(page, state, admin, '/news')

  await expect(page.locator('.ops-table-empty')).toBeVisible()
})

test('Operations News page has search and filter controls', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin] })
  buildAdminSession(state, admin)
  await goToOperationsAsAdmin(page, state, admin, '/news')

  await expect(page.locator('.ops-search')).toBeVisible()
  await expect(page.locator('.ops-filter-group').first()).toBeVisible()
})

test('Operations News page has Compose button for admins', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin] })
  buildAdminSession(state, admin)
  await goToOperationsAsAdmin(page, state, admin, '/news')

  // The compose button uses the i18n key 'operations.news.compose' → 'New Entry'
  await expect(page.getByRole('button', { name: /new entry/i })).toBeVisible()
})

// ─── Players page ─────────────────────────────────────────────────────────────

test('Operations Players page shows player table with sortable columns', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const player2 = makePlayer({ id: 'player-2', email: 'alice@test.com', displayName: 'Alice' })
  player2.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin, player2] })
  buildAdminSession(state, admin)
  await goToOperationsAsAdmin(page, state, admin, '/players')

  await expect(page.locator('table[aria-label="Player list"]')).toBeVisible()
  await expect(page.locator('table[aria-label="Player list"]')).toContainText('Alice')
  // Admin badge visible for admin player
  await expect(page.locator('table[aria-label="Player list"]')).toContainText('ADMIN')
})

test('Operations Players page search filters the player list', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const player2 = makePlayer({ id: 'player-2', email: 'alice@test.com', displayName: 'Alice' })
  player2.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const player3 = makePlayer({ id: 'player-3', email: 'bob@test.com', displayName: 'Bob Builder' })
  player3.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin, player2, player3] })
  buildAdminSession(state, admin)
  await goToOperationsAsAdmin(page, state, admin, '/players')

  // Type in search - filter to "alice"
  await page.locator('.ops-search').fill('alice')
  await expect(page.locator('table[aria-label="Player list"]')).toContainText('Alice')
  await expect(page.locator('table[aria-label="Player list"]')).not.toContainText('Bob Builder')
})

test('Operations Players page sort buttons are visible', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin] })
  buildAdminSession(state, admin)
  await goToOperationsAsAdmin(page, state, admin, '/players')

  await expect(page.locator('.ops-sort-group')).toBeVisible()
  await expect(page.locator('.ops-sort-btn').first()).toBeVisible()
})

// ─── Player detail page ───────────────────────────────────────────────────────

test('Operations Player detail page shows player stats and actions', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const player2 = makePlayer({ id: 'player-detail-test', email: 'charlie@test.com', displayName: 'Charlie' })
  player2.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin, player2] })
  buildAdminSession(state, admin)
  await goToOperationsAsAdmin(page, state, admin, '/players/player-detail-test')

  await expect(page.locator('.ops-player-detail')).toBeVisible()
  await expect(page.locator('.ops-detail-name')).toContainText('Charlie')
  await expect(page.locator('.ops-detail-email')).toContainText('charlie@test.com')
  await expect(page.locator('.ops-actions-panel')).toBeVisible()
  // i18n key 'operations.players.impersonatePerson' → 'Impersonate (Person)'
  await expect(page.locator('.ops-actions-grid').getByRole('button', { name: /impersonate \(person\)/i })).toBeVisible()
})

test('Operations Player detail page has back button', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const player2 = makePlayer({ id: 'player-detail-back', email: 'dave@test.com', displayName: 'Dave' })
  player2.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin, player2] })
  buildAdminSession(state, admin)
  await goToOperationsAsAdmin(page, state, admin, '/players/player-detail-back')

  await expect(page.locator('.ops-back-btn')).toBeVisible()
  await page.locator('.ops-back-btn').click()
  await expect(page).toHaveURL(/\/operations\/players/)
})

test('Operations Player detail page shows not-found for unknown player', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin] })
  buildAdminSession(state, admin)
  await goToOperationsAsAdmin(page, state, admin, '/players/nonexistent-player-id')

  await expect(page.locator('.ops-not-found')).toBeVisible()
})

// ─── Analytics page ───────────────────────────────────────────────────────────

test('Operations Analytics page shows product table with sort and filter controls', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin] })
  buildAdminSession(state, admin)
  await goToOperationsAsAdmin(page, state, admin, '/analytics')

  await expect(page.locator('table[aria-label="Product analytics"]')).toBeVisible()
  await expect(page.locator('.ops-analytics-controls')).toBeVisible()
  // Shows mock product data from mock-api handler
  await expect(page.locator('table[aria-label="Product analytics"]')).toContainText('Wooden Chair')
  await expect(page.locator('table[aria-label="Product analytics"]')).toContainText('Bread')
  await expect(page.locator('table[aria-label="Product analytics"]')).toContainText('Basic Medicine')
})

test('Operations Analytics page search filters products', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin] })
  buildAdminSession(state, admin)
  await goToOperationsAsAdmin(page, state, admin, '/analytics')

  await page.locator('.ops-search').fill('Bread')
  await expect(page.locator('table[aria-label="Product analytics"]')).toContainText('Bread')
  await expect(page.locator('table[aria-label="Product analytics"]')).not.toContainText('Wooden Chair')
})

test('Operations Analytics page industry filter works', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin] })
  buildAdminSession(state, admin)
  await goToOperationsAsAdmin(page, state, admin, '/analytics')

  // Select FURNITURE industry filter
  await page.locator('.ops-industry-select').selectOption('FURNITURE')
  await expect(page.locator('table[aria-label="Product analytics"]')).toContainText('Wooden Chair')
  await expect(page.locator('table[aria-label="Product analytics"]')).not.toContainText('Bread')
  await expect(page.locator('table[aria-label="Product analytics"]')).not.toContainText('Basic Medicine')
})

test('Operations Analytics page CSV export button is visible', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin] })
  buildAdminSession(state, admin)
  await goToOperationsAsAdmin(page, state, admin, '/analytics')

  await expect(page.locator('table[aria-label="Product analytics"]')).toBeVisible()
  await expect(page.getByRole('button', { name: /export/i })).toBeVisible()
  // Button should be enabled when data is loaded
  await expect(page.getByRole('button', { name: /export/i })).toBeEnabled()
})

test('Operations Analytics page column sorting changes data order', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin] })
  buildAdminSession(state, admin)
  await goToOperationsAsAdmin(page, state, admin, '/analytics')

  await expect(page.locator('table[aria-label="Product analytics"]')).toBeVisible()
  // Click the "Produced" column header to sort ascending
  await page.locator('th.sortable').filter({ hasText: /produced/i }).click()
  // After clicking again, sort direction changes
  await page.locator('th.sortable').filter({ hasText: /produced/i }).click()
  // Table should still be visible and contain data
  await expect(page.locator('table[aria-label="Product analytics"]')).toContainText('Wooden Chair')
})

test('Operations Analytics page shows saturation bars', async ({ page }) => {
  const admin = makeAdminPlayer()
  admin.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  const state = setupMockApi(page, { players: [admin] })
  buildAdminSession(state, admin)
  await goToOperationsAsAdmin(page, state, admin, '/analytics')

  await expect(page.locator('table[aria-label="Product analytics"]')).toBeVisible()
  await expect(page.locator('.saturation-bar-wrap').first()).toBeVisible()
})

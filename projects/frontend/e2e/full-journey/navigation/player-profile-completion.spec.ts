import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer } from '../../helpers/mock-api'

async function authenticate(page: Parameters<typeof test>[0]['page'], token: string) {
  await page.addInitScript((value) => {
    localStorage.setItem('auth_token', value)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, token)
}

function makeBadges(playerId: string) {
  return {
    [playerId]: [
      { id: 'badge-1', badgeType: 'FIRST_MILLION', rarity: 'COMMON', unlockCondition: 'Reach $1M total wealth', unlockedAtUtc: '2024-03-15T10:00:00Z', unlockedAtTick: 200 },
      { id: 'badge-2', badgeType: 'MASTER_TRADER', rarity: 'RARE', unlockCondition: 'Execute 1,000 exchange transactions', unlockedAtUtc: '2024-04-01T08:00:00Z', unlockedAtTick: 400 },
      { id: 'badge-3', badgeType: 'LEGENDARY_TYCOON', rarity: 'LEGENDARY', unlockCondition: 'Reach rank #1 for 100 consecutive ticks', unlockedAtUtc: '2024-05-01T12:00:00Z', unlockedAtTick: 800 },
    ],
  }
}

function makeSnapshots(playerId: string) {
  return {
    [playerId]: [
      { snapshotTick: 1008, snapshotUtc: '2024-02-01T00:00:00Z', leaderboardRank: 5, wealthUsd: 300000, percentileRank: 60, positionChange: null },
      { snapshotTick: 2016, snapshotUtc: '2024-02-08T00:00:00Z', leaderboardRank: 3, wealthUsd: 600000, percentileRank: 75, positionChange: 2 },
      { snapshotTick: 3024, snapshotUtc: '2024-02-15T00:00:00Z', leaderboardRank: 2, wealthUsd: 900000, percentileRank: 85, positionChange: 1 },
      { snapshotTick: 4032, snapshotUtc: '2024-02-22T00:00:00Z', leaderboardRank: 1, wealthUsd: 1200000, percentileRank: 100, positionChange: 1 },
    ],
  }
}

/** Tabs have role="tab", not role="button". */
async function clickProfileTab(page: Parameters<typeof test>[0]['page'], name: RegExp | string) {
  await page.getByRole('tab', { name }).click()
}

test.describe('Player Profile – Achievement Badges', () => {
  test('displays achievement badges in a grid with rarity color coding', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2024-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.playerBadges = makeBadges(player.id)
    await authenticate(page, `token-${player.id}`)
    await page.goto(`/player/${player.id}`)
    await clickProfileTab(page, /Achievements/i)
    await expect(page.locator('[aria-label="Achievement badges"]')).toBeVisible()
    await expect(page.locator('.badge-card')).toHaveCount(3)
  })

  test('shows badge tooltip with unlock date on hover', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2024-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.playerBadges = makeBadges(player.id)
    await authenticate(page, `token-${player.id}`)
    await page.goto(`/player/${player.id}`)
    await clickProfileTab(page, /Achievements/i)
    await expect(page.locator('.badge-card').first()).toBeVisible()
    await page.locator('.badge-card').first().hover()
    await expect(page.locator('.badge-tooltip').first()).toBeVisible()
  })

  test('shows empty state when player has no badges', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2024-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticate(page, `token-${player.id}`)
    await page.goto(`/player/${player.id}`)
    await clickProfileTab(page, /Achievements/i)
    await expect(page.locator('.badge-empty-state')).toBeVisible()
  })

  test('displays correct rarity classes for different rarity tiers', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2024-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.playerBadges = makeBadges(player.id)
    await authenticate(page, `token-${player.id}`)
    await page.goto(`/player/${player.id}`)
    await clickProfileTab(page, /Achievements/i)
    await expect(page.locator('.badge-legendary')).toBeVisible()
    await expect(page.locator('.badge-rare')).toBeVisible()
    await expect(page.locator('.badge-common')).toBeVisible()
  })
})

test.describe('Player Profile – Rank History', () => {
  test('Rank History tab shows SVG chart with data points', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2024-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.playerRankSnapshots = makeSnapshots(player.id)
    await authenticate(page, `token-${player.id}`)
    await page.goto(`/player/${player.id}`)
    await clickProfileTab(page, /Rank History/i)
    await expect(page.locator('.rank-chart-svg')).toBeVisible()
    await expect(page.locator('.rank-chart-svg circle')).not.toHaveCount(0)
  })

  test('displays summary KPI cards with Best Rank', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2024-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.playerRankSnapshots = makeSnapshots(player.id)
    await authenticate(page, `token-${player.id}`)
    await page.goto(`/player/${player.id}`)
    await clickProfileTab(page, /Rank History/i)
    await expect(page.locator('.rank-summary-grid')).toBeVisible()
    await expect(page.locator('.rank-kpi-card').filter({ hasText: 'Best Rank' })).toBeVisible()
    await expect(
      page.locator('.rank-kpi-card').filter({ hasText: 'Best Rank' }).locator('.rank-kpi-value'),
    ).toContainText('#1')
  })

  test('time filter buttons are visible and change active state', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2024-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.playerRankSnapshots = makeSnapshots(player.id)
    await authenticate(page, `token-${player.id}`)
    await page.goto(`/player/${player.id}`)
    await clickProfileTab(page, /Rank History/i)
    await expect(page.locator('[aria-label="Time range"]')).toBeVisible()
    await expect(page.locator('.rank-filter-btn').filter({ hasText: '365d' })).toHaveClass(/active/)
    await page.locator('.rank-filter-btn').filter({ hasText: '30d' }).click()
    await expect(page.locator('.rank-filter-btn').filter({ hasText: '30d' })).toHaveClass(/active/)
  })

  test('shows empty state when player has no rank history', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2024-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticate(page, `token-${player.id}`)
    await page.goto(`/player/${player.id}`)
    await clickProfileTab(page, /Rank History/i)
    await expect(page.locator('.rank-chart-empty')).toBeVisible()
  })
})

test.describe('Player Profile – Statistics Export', () => {
  test('Export Stats button is visible for authenticated user', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2024-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticate(page, `token-${player.id}`)
    await page.goto(`/player/${player.id}`)
    await expect(page.locator('.export-btn')).toBeVisible()
  })

  test('clicking Export Stats opens dropdown with CSV and HTML options', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2024-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticate(page, `token-${player.id}`)
    await page.goto(`/player/${player.id}`)
    await page.locator('.export-btn').click()
    await expect(page.locator('.export-dropdown')).toBeVisible()
    await expect(page.locator('.export-option').filter({ hasText: /CSV/i })).toBeVisible()
    await expect(page.locator('.export-option').filter({ hasText: /HTML/i })).toBeVisible()
  })

  test('Export Stats button is hidden for unauthenticated users', async ({ page }) => {
    const player = makePlayer()
    setupMockApi(page, { players: [player] })
    await page.goto(`/player/${player.id}`)
    await expect(page.locator('.export-btn')).toBeHidden()
  })
})

test.describe('Player Profile – Tab Navigation', () => {
  test('Overview tab is active by default', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2024-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticate(page, `token-${player.id}`)
    await page.goto(`/player/${player.id}`)
    await expect(page.locator('.profile-tab').filter({ hasText: /Overview/i })).toHaveClass(/active/)
  })

  test('tabs switch content area correctly', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2024-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.playerBadges = makeBadges(player.id)
    state.playerRankSnapshots = makeSnapshots(player.id)
    await authenticate(page, `token-${player.id}`)
    await page.goto(`/player/${player.id}`)
    await clickProfileTab(page, /Achievements/i)
    await expect(page.locator('.profile-tab').filter({ hasText: /Achievements/i })).toHaveClass(/active/)
    await clickProfileTab(page, /Rank History/i)
    await expect(page.locator('.profile-tab').filter({ hasText: /Rank History/i })).toHaveClass(/active/)
  })
})

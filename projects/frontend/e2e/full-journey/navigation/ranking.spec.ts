import { expect, test } from '@playwright/test'
import { makePlayer, setupMockApi } from '../../helpers/mock-api'

async function authenticate(page: Parameters<typeof test>[0]['page'], token: string) {
  await page.addInitScript((value) => {
    localStorage.setItem('auth_token', value)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, token)
}

function makeRankedPlayers(count: number) {
  return Array.from({ length: count }, (_, index) =>
    makePlayer({
      id: `rank-player-${index + 1}`,
      email: `rank-player-${index + 1}@example.com`,
      displayName: `Rank Player ${index + 1}`,
      personalCash: 1_000_000 - index * 10_000,
    }),
  )
}

test.describe('Ranking page', () => {
  test('auto-jumps to the active player page and highlights the row', async ({ page }) => {
    const players = makeRankedPlayers(30)
    const currentPlayer = players[24]
    const state = setupMockApi(page, { players })
    state.currentUserId = currentPlayer.id
    state.currentToken = `token-${currentPlayer.id}`
    await authenticate(page, `token-${currentPlayer.id}`)

    await page.goto('/ranking')

    await expect(page).toHaveURL(/\/(ranking|leaderboard)\?page=3/)
    await expect(page.getByText('Page 3 of 3')).toBeVisible()
    const playerRow = page.locator('.rank-card', { hasText: currentPlayer.displayName })
    await expect(playerRow).toHaveAttribute('aria-current', 'true')
    await expect(playerRow.locator('.you-badge')).toContainText('You')
  })

  test('shows global master ranking link as external new-tab action', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/ranking')

    const masterLink = page.locator('a[target="_blank"][href$="/ranking"]').first()
    await expect(masterLink).toBeVisible()
    await expect(masterLink).toHaveAttribute('target', '_blank')
    await expect(masterLink).toHaveAttribute('href', /\/ranking$/)
  })

  test('shows real-world benchmark section with top five billionaires', async ({ page }) => {
    const state = setupMockApi(page, {
      players: [makePlayer({ id: 'benchmark-player', email: 'benchmark@test.com', displayName: 'Benchmark Player' })],
    })
    state.currentUserId = 'benchmark-player'
    state.currentToken = 'token-benchmark-player'
    await authenticate(page, 'token-benchmark-player')
    await page.goto('/ranking')

    await expect(page.getByRole('heading', { name: 'Race to the Top — Real World Benchmark' })).toBeVisible()
    const benchmarkTable = page.getByRole('table', { name: 'Race to the Top — Real World Benchmark' })
    await expect(benchmarkTable.getByText('Elon Musk')).toBeVisible()
    await expect(benchmarkTable.getByText('Jensen Huang')).toBeVisible()

    const benchmarkY = await page.getByRole('heading', { name: 'Race to the Top — Real World Benchmark' }).evaluate(
      (node) => node.getBoundingClientRect().top,
    )
    const firstCardY = await page
      .locator('.rank-card')
      .first()
      .evaluate((node) => node.getBoundingClientRect().top)
    expect(benchmarkY).toBeLessThan(firstCardY)
  })

  test('unauthenticated user stays on page 1 with no active-row highlight', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/ranking')

    await expect(page).toHaveURL(/\/(ranking|leaderboard)$/)
    await expect(page.locator('.rank-card[aria-current="true"]')).toHaveCount(0)
  })

  test('shows player badge icon in leaderboard row when player has earned a badge', async ({ page }) => {
    const players = makeRankedPlayers(5)
    const featuredPlayer = players[0]!
    const state = setupMockApi(page, { players })
    state.playerBadges[featuredPlayer.id] = [
      {
        id: 'badge-1',
        badgeType: 'FIRST_B2B_TRADE',
        rarity: 'COMMON',
        unlockCondition: 'Complete your first B2B wholesale trade.',
        unlockedAtUtc: '2026-01-01T00:00:00Z',
        unlockedAtTick: 100,
      },
    ]

    await page.goto('/ranking')

    const row = page.locator('.rank-card', { hasText: featuredPlayer.displayName }).first()
    await expect(row.locator('.player-badge-icon')).toBeVisible()
  })

  test('shows % of target and distance-to-win details for top players', async ({ page }) => {
    const players = [
      makePlayer({ id: 'p1', email: 'p1@test.com', displayName: 'Leader One', personalCash: 205_000_000_000 }),
      makePlayer({ id: 'p2', email: 'p2@test.com', displayName: 'Second Two', personalCash: 120_000_000_000 }),
      makePlayer({ id: 'p3', email: 'p3@test.com', displayName: 'Third Three', personalCash: 90_000_000_000 }),
      makePlayer({ id: 'p4', email: 'p4@test.com', displayName: 'Fourth Four', personalCash: 10_000_000_000 }),
    ]
    const state = setupMockApi(page, { players })
    state.endgameStatus.winningThresholdUsd = 430_000_000_000

    await page.goto('/ranking')

    await expect(page.locator('.leader-percent-of-target').first()).toBeVisible()
    await expect(page.locator('.leader-distance-to-win')).toHaveCount(3)
    await expect(page.getByText('Leader One has surpassed #3 — Mark Zuckerberg.')).toBeVisible()
    await expect(page.locator('.rank-card').first()).toContainText('of #1 target')
  })

  test('shows collapsed mobile benchmark target view', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 })
    setupMockApi(page)
    await page.goto('/ranking')

    await expect(page.getByText('Mobile benchmark target')).toBeVisible()
    await expect(page.getByText('#1 — Elon Musk')).toBeVisible()
    await expect(page.getByRole('table', { name: 'Race to the Top — Real World Benchmark' })).toBeHidden()
  })
})

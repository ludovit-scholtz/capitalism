import { test, expect, type Page } from '@playwright/test'
import { setupMockApi, makePlayer } from '../../helpers/mock-api'
import { mockAwaySummary, type MockAwaySummary } from '../../helpers/mock-api-away-summary'

/** The first page load of a run compiles the dev bundle lazily, which can exceed the default 2 s assertion timeout. */
const COLD_START_TIMEOUT = 15_000
const CURRENT_TICK = 140

const SUMMARY: MockAwaySummary = {
  sinceTick: 100,
  currentTick: CURRENT_TICK,
  ticksAway: 40,
  revenueUsd: 18_000,
  costsUsd: -3_000,
  netUsd: 15_000,
  loanPaymentsCount: 2,
}

async function openDashboard(page: Page, options: { lastSeenTick: number | null; summary?: MockAwaySummary }) {
  const player = makePlayer({
    onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
    companies: [{ id: 'comp-away', playerId: 'player-1', name: 'Away Corp', cash: 400000, foundedAtUtc: '2026-01-01T00:00:00Z', buildings: [] }],
  })
  const mock = setupMockApi(page, { players: [player] })
  mock.currentUserId = player.id
  mock.currentToken = `token-${player.id}`
  mock.gameState.currentTick = CURRENT_TICK
  mock.gameState.lastTickAtUtc = new Date(Date.now() - 5000).toISOString()
  mock.gameState.tickIntervalSeconds = 60
  const away = await mockAwaySummary(page, options.summary ?? SUMMARY)
  await page.addInitScript(
    ({ token, lastSeenTick }) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
      if (lastSeenTick !== null && !sessionStorage.getItem('away_summary_shown')) localStorage.setItem('away_last_seen_tick', String(lastSeenTick))
    },
    { token: `token-${player.id}`, lastSeenTick: options.lastSeenTick },
  )
  await page.goto('/dashboard')
  return away
}

test.describe('Dashboard — while you were away', () => {
  test('shows the summary once for a returning player and asks from the remembered tick', async ({ page }) => {
    const away = await openDashboard(page, { lastSeenTick: 100 })

    const dialog = page.getByRole('dialog', { name: 'While you were away' })
    await expect(dialog).toBeVisible({ timeout: COLD_START_TIMEOUT })
    await expect(dialog.getByText('Over the last 1 day 16 hours')).toBeVisible()
    await expect(dialog.getByText('+$18,000')).toBeVisible()
    await expect(dialog.getByText('-$3,000')).toBeVisible()
    await expect(dialog.getByText('+$15,000')).toBeVisible()
    await expect(dialog.getByText('Loan payments made')).toBeVisible()
    expect(away.requests).toEqual([100])

    await dialog.getByRole('button', { name: 'Continue' }).click()
    await expect(page.getByRole('dialog', { name: 'While you were away' })).toHaveCount(0)

    await page.reload()
    await expect(page.getByRole('heading', { name: 'Dashboard' }).first()).toBeVisible({ timeout: COLD_START_TIMEOUT })
    await expect(page.getByRole('dialog', { name: 'While you were away' })).toHaveCount(0)
    expect(away.requests).toEqual([100])
  })

  test('closes with Escape', async ({ page }) => {
    await openDashboard(page, { lastSeenTick: 100 })

    const dialog = page.getByRole('dialog', { name: 'While you were away' })
    await expect(dialog.getByRole('button', { name: 'Continue' })).toBeFocused({ timeout: COLD_START_TIMEOUT })
    await page.keyboard.press('Escape')
    await expect(page.getByRole('dialog', { name: 'While you were away' })).toHaveCount(0)
  })

  test('does not ask a browser that has never seen a tick', async ({ page }) => {
    const away = await openDashboard(page, { lastSeenTick: null })

    await expect(page.getByRole('heading', { name: 'Dashboard' }).first()).toBeVisible({ timeout: COLD_START_TIMEOUT })
    await expect(page.locator('.tick-clock-widget')).toBeVisible()
    await expect(page.getByRole('dialog', { name: 'While you were away' })).toHaveCount(0)
    expect(away.requests).toEqual([])
  })

  test('does not interrupt a short absence', async ({ page }) => {
    const away = await openDashboard(page, { lastSeenTick: CURRENT_TICK - 2 })

    await expect(page.getByRole('heading', { name: 'Dashboard' }).first()).toBeVisible({ timeout: COLD_START_TIMEOUT })
    await expect(page.locator('.tick-clock-widget')).toBeVisible()
    await expect(page.getByRole('dialog', { name: 'While you were away' })).toHaveCount(0)
    expect(away.requests).toEqual([])
  })

  test('stays closed when nothing happened while away', async ({ page }) => {
    const away = await openDashboard(page, {
      lastSeenTick: 100,
      summary: { ...SUMMARY, revenueUsd: 0, costsUsd: 0, netUsd: 0, loanPaymentsCount: 0 },
    })

    await expect.poll(() => away.requests.length, { timeout: COLD_START_TIMEOUT }).toBe(1)
    await expect(page.getByRole('dialog', { name: 'While you were away' })).toHaveCount(0)
  })

  test('remembers the tick when the player leaves', async ({ page }) => {
    await openDashboard(page, { lastSeenTick: null })
    await expect(page.locator('.tick-clock-widget')).toBeVisible({ timeout: COLD_START_TIMEOUT })

    await page.evaluate(() => {
      Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => 'hidden' })
      document.dispatchEvent(new Event('visibilitychange'))
    })

    expect(await page.evaluate(() => localStorage.getItem('away_last_seen_tick'))).toBe(String(CURRENT_TICK))
  })
})

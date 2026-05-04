import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer } from '../../helpers/mock-api'

// ── Helpers ────────────────────────────────────────────────────────────────

function authenticateViaLocalStorage(page: Parameters<typeof test>[0]['page'], token: string) {
  return page.addInitScript((t) => {
    localStorage.setItem('auth_token', t)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, token)
}

function clearSessionStorage(page: Parameters<typeof test>[0]['page']) {
  return page.addInitScript(() => {
    sessionStorage.clear()
  })
}

// ── Dashboard tooltip tests ────────────────────────────────────────────────

test.describe('Dashboard contextual tooltip overlay', () => {
  test('new player sees dashboard tooltip overlay after first visit', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    // Tutorial milestones have no tooltip dismissed yet
    state.tutorialProgress = [
      { milestone: 'FIRST_RESOURCE_SOLD', isCompleted: false, completedAtUtc: null },
    ]

    await clearSessionStorage(page)
    await authenticateViaLocalStorage(page, `token-${player.id}`)
    await page.goto('/dashboard')

    // Wait for 1s delay + render
    await expect(page.locator('.tutorial-tooltip')).toBeVisible({ timeout: 5000 })

    // Verify tooltip content
    await expect(page.locator('.tutorial-tooltip__title')).toContainText('Welcome to Your Dashboard')
    await expect(page.locator('.tutorial-tooltip__body')).toContainText('command centre')

    // Verify backdrop overlay is shown
    await expect(page.locator('.tutorial-tooltip__overlay')).toBeVisible()

    // Dismiss button is visible
    await expect(page.locator('.tutorial-tooltip__dismiss-btn')).toBeVisible()
  })

  test('player can dismiss dashboard tooltip by clicking "Got it"', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.tutorialProgress = []

    await clearSessionStorage(page)
    await authenticateViaLocalStorage(page, `token-${player.id}`)
    await page.goto('/dashboard')

    // Wait for tooltip to appear
    await expect(page.locator('.tutorial-tooltip')).toBeVisible({ timeout: 5000 })

    // Click "Got it" to dismiss
    await page.locator('.tutorial-tooltip__dismiss-btn').click()

    // Tooltip should fade out
    await expect(page.locator('.tutorial-tooltip')).toBeHidden()
  })

  test('player can dismiss dashboard tooltip using Escape key', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.tutorialProgress = []

    await clearSessionStorage(page)
    await authenticateViaLocalStorage(page, `token-${player.id}`)
    await page.goto('/dashboard')

    // Wait for tooltip to appear
    await expect(page.locator('.tutorial-tooltip')).toBeVisible({ timeout: 5000 })

    // Press Escape to dismiss
    await page.keyboard.press('Escape')

    // Tooltip should fade out
    await expect(page.locator('.tutorial-tooltip')).toBeHidden()
  })

  test('returning player with dismissed tooltip sees no tooltip', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    // Dashboard tooltip already dismissed
    state.tutorialProgress = [
      { milestone: 'TOOLTIP_DASHBOARD_SHOWN', isCompleted: true, completedAtUtc: '2026-01-01T00:00:00Z' },
    ]

    await clearSessionStorage(page)
    await authenticateViaLocalStorage(page, `token-${player.id}`)
    await page.goto('/dashboard')

    // Existing player (tooltip already dismissed) logs in, verify no tooltip on dashboard.
    // We wait long enough to be past the 1-second tooltip-ready delay.
    await expect(page.locator('.tutorial-tooltip')).toBeHidden({ timeout: 3000 })
  })

  test('dismissing tooltip persists via markTutorialMilestoneComplete mutation', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.tutorialProgress = []

    await clearSessionStorage(page)
    await authenticateViaLocalStorage(page, `token-${player.id}`)
    await page.goto('/dashboard')

    // Wait for tooltip, then dismiss
    await expect(page.locator('.tutorial-tooltip')).toBeVisible({ timeout: 5000 })
    await page.locator('.tutorial-tooltip__dismiss-btn').click()
    await expect(page.locator('.tutorial-tooltip')).toBeHidden()

    // Verify the milestone was persisted in mock state
    await expect
      .poll(() => state.tutorialProgress.find((m) => m.milestone === 'TOOLTIP_DASHBOARD_SHOWN')?.isCompleted, { timeout: 3000 })
      .toBe(true)
  })

  test('dashboard tooltip renders in Slovak locale', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.tutorialProgress = []

    await clearSessionStorage(page)
    await page.addInitScript(() => {
      localStorage.setItem('app_locale', 'sk')
    })
    await authenticateViaLocalStorage(page, `token-${player.id}`)
    await page.goto('/dashboard')

    await expect(page.locator('.tutorial-tooltip')).toBeVisible({ timeout: 5000 })
    await expect(page.locator('.tutorial-tooltip__title')).toContainText('Vitajte vo vašom dashboarde')
  })

  test('dashboard tooltip renders in German locale', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.tutorialProgress = []

    await clearSessionStorage(page)
    await page.addInitScript(() => {
      localStorage.setItem('app_locale', 'de')
    })
    await authenticateViaLocalStorage(page, `token-${player.id}`)
    await page.goto('/dashboard')

    await expect(page.locator('.tutorial-tooltip')).toBeVisible({ timeout: 5000 })
    await expect(page.locator('.tutorial-tooltip__title')).toContainText('Willkommen in Ihrem Dashboard')
  })
})

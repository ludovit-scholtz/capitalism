import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer } from '../../helpers/mock-api'

// ── Helpers ────────────────────────────────────────────────────────────────

function authenticatePlayer(page: Parameters<typeof test>[0]['page'], playerId: string) {
  const token = `token-${playerId}`
  return page.addInitScript((t) => {
    localStorage.setItem('auth_token', t)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, token)
}

// ── Tests ─────────────────────────────────────────────────────────────────

test.describe('Tutorial view (/tutorial)', () => {
  test('unauthenticated visitor sees all 5 milestone titles and public descriptions', async ({
    page,
  }) => {
    setupMockApi(page)
    await page.goto('/tutorial')

    // Page heading
    await expect(page.getByRole('heading', { name: 'Tutorial' })).toBeVisible()

    // All 5 milestone cards should be listed (they show even without auth)
    await expect(page.locator('.milestone-card')).toHaveCount(5)

    // A known milestone title should be visible
    await expect(page.getByRole('heading', { name: 'First Resource Sold' })).toBeVisible()
  })

  test('unauthenticated visitor does not see Resume buttons', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/tutorial')

    // No resume buttons because the player is not authenticated
    await expect(page.locator('.milestone-card__resume-btn')).toHaveCount(0)
  })

  test('unauthenticated visitor sees sign-in notice', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/tutorial')

    await expect(page.locator('.tutorial-auth-notice')).toBeVisible()
  })

  test('authenticated player with no completions sees 5 incomplete milestones', async ({
    page,
  }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticatePlayer(page, player.id)

    await page.goto('/tutorial')

    // All 5 milestones should show pending status
    const pendingIcons = page.locator('.milestone-card__status-icon--pending')
    await expect(pendingIcons).toHaveCount(5)

    // All 5 resume buttons should be present
    const resumeButtons = page.locator('.milestone-card__resume-btn')
    await expect(resumeButtons).toHaveCount(5)
  })

  test('progress bar shows 0/5 when no milestones completed', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticatePlayer(page, player.id)

    await page.goto('/tutorial')

    await expect(page.locator('.tutorial-progress__label')).toContainText('0/5')
  })

  test('authenticated player with one completion sees checkmark', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    // Pre-complete one milestone
    state.tutorialProgress = state.tutorialProgress.map((m) =>
      m.milestone === 'FIRST_RESOURCE_SOLD'
        ? { ...m, isCompleted: true, completedAtUtc: '2026-01-02T10:00:00Z' }
        : m,
    )

    await authenticatePlayer(page, player.id)
    await page.goto('/tutorial')

    // One done badge, four pending
    await expect(page.locator('.milestone-card__status-icon--done')).toHaveCount(1)
    await expect(page.locator('.milestone-card__status-icon--pending')).toHaveCount(4)

    // Progress should show 1/5
    await expect(page.locator('.tutorial-progress__label')).toContainText('1/5')
  })

  test('Resume button for incomplete milestone navigates to correct route', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticatePlayer(page, player.id)

    await page.goto('/tutorial')

    // The first milestone card is "First Resource Sold" with resumeRoute=/dashboard
    // Click its Resume button
    const firstCard = page.locator('.milestone-card').first()
    await firstCard.getByRole('button', { name: 'Resume' }).click()

    // Should navigate to /dashboard
    await expect(page).toHaveURL(/\/dashboard/)
  })

  test('all 5 milestone bounty points are visible', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/tutorial')

    const bounties = page.locator('.milestone-card__bounty')
    await expect(bounties).toHaveCount(5)

    // Each bounty should show a "pts" suffix
    const bountyTexts = await bounties.allTextContents()
    for (const text of bountyTexts) {
      expect(text).toMatch(/\+\d+ pts/)
    }
  })

  test('all 5 milestone titles are visible', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/tutorial')

    await expect(page.getByRole('heading', { name: 'First Resource Sold' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'First B2B Trade' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'First Loan Taken' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'First Competitor Observed' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'First Brand Established' })).toBeVisible()
  })

  test('page is accessible via nav link Tutorial', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/')

    // Find and click the Tutorial nav link
    const tutorialLink = page.getByRole('link', { name: 'Tutorial' })
    await expect(tutorialLink).toBeVisible()
    await tutorialLink.click()
    await expect(page).toHaveURL(/\/tutorial/)
  })

  test('mobile viewport: milestone cards still display correctly', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 })
    setupMockApi(page)
    await page.goto('/tutorial')

    await expect(page.locator('.milestone-card')).toHaveCount(5)
    await expect(page.getByRole('heading', { name: 'Tutorial' })).toBeVisible()
  })

  test('Slovak locale: milestone titles show in Slovak', async ({ page }) => {
    await page.addInitScript(() => localStorage.setItem('app_locale', 'sk'))
    setupMockApi(page)
    await page.goto('/tutorial')

    await expect(page.getByRole('heading', { name: 'Tutoriál' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Prvý predaj' })).toBeVisible()
  })

  test('all completions: progress shows 5/5 and all done badges', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    // All milestones completed
    state.tutorialProgress = state.tutorialProgress.map((m) => ({
      ...m,
      isCompleted: true,
      completedAtUtc: '2026-01-05T00:00:00Z',
    }))

    await authenticatePlayer(page, player.id)
    await page.goto('/tutorial')

    await expect(page.locator('.milestone-card__status-icon--done')).toHaveCount(5)
    await expect(page.locator('.milestone-card__resume-btn')).toHaveCount(0)
    await expect(page.locator('.milestone-card__done-badge')).toHaveCount(5)
    await expect(page.locator('.tutorial-progress__label')).toContainText('5/5')
  })
})

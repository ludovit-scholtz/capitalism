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
  test('unauthenticated visitor sees milestone list plus endgame goal card', async ({
    page,
  }) => {
    setupMockApi(page)
    await page.goto('/tutorial')

    // Page heading
    await expect(page.getByRole('heading', { name: 'Tutorial' })).toBeVisible()

    // Endgame goal card + 7 milestone cards
    await expect(page.locator('.milestone-card')).toHaveCount(8)
    await expect(page.getByRole('heading', { name: 'Endgame Goal' })).toBeVisible()

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

  test('authenticated player with no completions sees 7 incomplete milestones', async ({
    page,
  }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticatePlayer(page, player.id)

    await page.goto('/tutorial')

    // All 7 milestones should show pending status
    const pendingIcons = page.locator('.milestone-card__status-icon--pending')
    await expect(pendingIcons).toHaveCount(7)

    // All 7 resume buttons should be present
    const resumeButtons = page.locator('.milestone-card__resume-btn')
    await expect(resumeButtons).toHaveCount(7)
  })

  test('progress bar shows 0/7 when no milestones completed', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticatePlayer(page, player.id)

    await page.goto('/tutorial')

    await expect(page.locator('.tutorial-progress__label')).toContainText('0/7')
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

    // One done badge, six pending
    await expect(page.locator('.milestone-card__status-icon--done')).toHaveCount(1)
    await expect(page.locator('.milestone-card__status-icon--pending')).toHaveCount(6)

    // Progress should show 1/7
    await expect(page.locator('.tutorial-progress__label')).toContainText('1/7')
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
    const firstCard = page.locator('.milestone-card').nth(1)
    await firstCard.getByRole('button', { name: 'Resume' }).click()

    // Should navigate to /dashboard
    await expect(page).toHaveURL(/\/dashboard/)
  })

  test('all 7 milestone bounty points are visible', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/tutorial')

    const bounties = page.locator('.milestone-card__bounty')
    await expect(bounties).toHaveCount(7)

    // Each bounty should show a "pts" suffix
    const bountyTexts = await bounties.allTextContents()
    for (const text of bountyTexts) {
      expect(text).toMatch(/\+\d+ pts/)
    }
  })

  test('all 7 milestone titles are visible', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/tutorial')

    await expect(page.getByRole('heading', { name: 'First Resource Sold' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'First B2B Trade' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'First Loan Taken' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'First Competitor Observed' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'First Brand Established' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'First Building Detail Visit' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'First Grid Editor Open' })).toBeVisible()
  })

  test('page is accessible via nav link Tutorial', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/')

    await page.getByRole('button', { name: 'Main' }).hover()
    const tutorialLink = page.locator('.desktop-section-panel').getByRole('link', { name: 'Tutorial' })
    await expect(tutorialLink).toBeVisible()
    await tutorialLink.click()
    await expect(page).toHaveURL(/\/tutorial/)
  })

  test('mobile viewport: milestone cards still display correctly', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 })
    setupMockApi(page)
    await page.goto('/tutorial')

    await expect(page.locator('.milestone-card')).toHaveCount(8)
    await expect(page.getByRole('heading', { name: 'Tutorial' })).toBeVisible()
  })

  test('Slovak locale: milestone titles show in Slovak', async ({ page }) => {
    await page.addInitScript(() => localStorage.setItem('app_locale', 'sk'))
    setupMockApi(page)
    await page.goto('/tutorial')

    await expect(page.getByRole('heading', { name: 'Tutoriál' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Prvý predaj' })).toBeVisible()
  })

  test('all completions: progress shows 7/7 and all done badges', async ({ page }) => {
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
      bountyAwarded: m.bountyPoints != null,
      bountyAwardedAtUtc: m.bountyPoints != null ? '2026-01-05T00:00:00Z' : null,
    }))

    await authenticatePlayer(page, player.id)
    await page.goto('/tutorial')

    await expect(page.locator('.milestone-card__status-icon--done')).toHaveCount(7)
    await expect(page.locator('.milestone-card__resume-btn')).toHaveCount(0)
    await expect(page.locator('.milestone-card__done-badge')).toHaveCount(7)
    await expect(page.locator('.milestone-card__done-badge').first()).toContainText('Bounty Earned')
    await expect(page.locator('.tutorial-progress__label')).toContainText('7/7')
  })
})

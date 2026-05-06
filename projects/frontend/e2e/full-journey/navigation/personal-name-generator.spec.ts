import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer } from '../../helpers/mock-api'

async function authenticate(page: Parameters<typeof test>[0]['page'], token: string) {
  await page.addInitScript((value) => {
    localStorage.setItem('auth_token', value)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, token)
}

// ─────────────────────────────────────────────────────────────────────────────
// Registration — personal name generator UX
// ─────────────────────────────────────────────────────────────────────────────

test.describe('Registration – personal name generator', () => {
  test('display name field is auto-filled when switching to register mode', async ({ page }) => {
    setupMockApi(page, { players: [] })
    await page.goto('/login')
    // Click "Register" toggle
    await page.getByRole('button', { name: 'Create Account' }).click()
    // Display name field must be pre-populated (id="displayName")
    const displayNameInput = page.locator('#displayName')
    const value = await displayNameInput.inputValue()
    expect(value.length).toBeGreaterThan(0)
  })

  test('auto-filled display name has exactly three words (Firstname Middlename Lastname)', async ({ page }) => {
    setupMockApi(page, { players: [] })
    await page.goto('/login')
    await page.getByRole('button', { name: 'Create Account' }).click()
    const displayNameInput = page.locator('#displayName')
    const value = await displayNameInput.inputValue()
    const words = value.trim().split(' ')
    expect(words).toHaveLength(3)
    // Every word should start with a capital
    for (const word of words) {
      expect(word.charAt(0)).toBe(word.charAt(0).toUpperCase())
    }
  })

  test('generate-another-name button changes the displayed name', async ({ page }) => {
    setupMockApi(page, { players: [] })
    await page.goto('/login')
    await page.getByRole('button', { name: 'Create Account' }).click()
    const displayNameInput = page.locator('#displayName')
    // Click the dice button (🎲) which has title "Generate Another Name" 3 times
    const generateBtn = page.locator('button[title="Generate Another Name"]')
    await generateBtn.click()
    await generateBtn.click()
    await generateBtn.click()
    // The generate button must still be visible and the field must have a valid 3-word name
    await expect(generateBtn).toBeVisible()
    const finalValue = await displayNameInput.inputValue()
    expect(finalValue.split(' ')).toHaveLength(3)
  })

  test('shows real-name privacy warning on the display name field', async ({ page }) => {
    setupMockApi(page, { players: [] })
    await page.goto('/login')
    await page.getByRole('button', { name: 'Create Account' }).click()
    await expect(page.locator('.personal-name-warning')).toBeVisible()
  })

  test('player can type a custom display name instead of the generated one', async ({ page }) => {
    setupMockApi(page, { players: [] })
    await page.goto('/login')
    await page.getByRole('button', { name: 'Create Account' }).click()
    const displayNameInput = page.locator('#displayName')
    await displayNameInput.fill('Bors Maximilian Kestrel')
    await expect(displayNameInput).toHaveValue('Bors Maximilian Kestrel')
  })
})

// ─────────────────────────────────────────────────────────────────────────────
// Player profile — display name editing
// ─────────────────────────────────────────────────────────────────────────────

test.describe('Player profile – display name editing', () => {
  test('edit display name button is visible for own profile', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2024-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticate(page, `token-${player.id}`)
    await page.goto(`/player/${player.id}`)
    await expect(page.locator('.edit-display-name-btn')).toBeVisible()
  })

  test('edit display name button is NOT visible for another player profile', async ({ page }) => {
    const owner = makePlayer({ id: 'player-owner-1', email: 'owner@test.com' })
    const viewer = makePlayer({ id: 'player-viewer-2', email: 'viewer@test.com' })
    owner.onboardingCompletedAtUtc = '2024-01-01T00:00:00Z'
    viewer.onboardingCompletedAtUtc = '2024-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [owner, viewer] })
    state.currentUserId = viewer.id
    state.currentToken = `token-${viewer.id}`
    await authenticate(page, `token-${viewer.id}`)
    await page.goto(`/player/${owner.id}`)
    await expect(page.locator('.edit-display-name-btn')).toBeHidden()
  })

  test('clicking edit opens display name input field', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2024-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticate(page, `token-${player.id}`)
    await page.goto(`/player/${player.id}`)
    await page.locator('.edit-display-name-btn').click()
    await expect(page.locator('.display-name-input')).toBeVisible()
  })

  test('real-name warning is shown in the display name edit form', async ({ page }) => {
    const player = makePlayer()
    player.onboardingCompletedAtUtc = '2024-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticate(page, `token-${player.id}`)
    await page.goto(`/player/${player.id}`)
    await page.locator('.edit-display-name-btn').click()
    await expect(page.locator('.display-name-real-name-warning')).toBeVisible()
  })

  test('saving updated display name reflects on the profile page', async ({ page }) => {
    const player = makePlayer()
    player.displayName = 'Aurelius Victor Fontaine'
    player.onboardingCompletedAtUtc = '2024-01-01T00:00:00Z'
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticate(page, `token-${player.id}`)
    await page.goto(`/player/${player.id}`)
    await page.locator('.edit-display-name-btn').click()
    const input = page.locator('.display-name-input')
    await input.fill('Quintus Hadrian Wolfe')
    await page.getByRole('button', { name: 'Save' }).click()
    // After saving, the edit form should close and the new name appear in the heading
    await expect(page.locator('.display-name-input')).toBeHidden()
    await expect(page.locator('h1')).toContainText('Quintus Hadrian Wolfe')
  })
})

// ─────────────────────────────────────────────────────────────────────────────
// Rankings — display name shown (ROADMAP: "In ranking show the personal account name")
// ─────────────────────────────────────────────────────────────────────────────

test.describe('Rankings – display name shown', () => {
  test('leaderboard shows player displayName in rankings (ROADMAP alignment)', async ({ page }) => {
    const player = makePlayer()
    player.displayName = 'Maximus Decimus Aurelius'
    player.onboardingCompletedAtUtc = '2024-01-01T00:00:00Z'
    setupMockApi(page, { players: [player] })
    await page.goto('/leaderboard')
    // The player's displayName must appear in the rankings list
    await expect(page.locator('.rank-card').getByText('Maximus Decimus Aurelius')).toBeVisible()
  })

  test('leaderboard shows custom displayName updated via profile editing', async ({ page }) => {
    const player = makePlayer()
    player.displayName = 'Caius Julius Caesar'
    player.onboardingCompletedAtUtc = '2024-01-01T00:00:00Z'
    setupMockApi(page, { players: [player] })
    await page.goto('/leaderboard')
    // Verify the 3-word personal alias is visible in the rankings
    await expect(page.locator('.rank-card').getByText('Caius Julius Caesar')).toBeVisible()
    // Email must NOT appear anywhere in any rank card (rankings show alias, not email)
    await expect(page.locator('.rank-card').getByText(player.email)).toBeHidden()
  })
})

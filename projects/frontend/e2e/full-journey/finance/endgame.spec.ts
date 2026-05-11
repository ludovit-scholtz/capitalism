import { expect, test } from '@playwright/test'
import { makeAdminPlayer, makePlayer, setupMockApi } from '../../helpers/mock-api'

test.describe('Endgame UI', () => {
  test('personal ledger shows real-world billionaire race panel', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      personalCash: 250000,
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/personal-ledger')

    await expect(page.getByRole('heading', { name: 'Race to the Top' })).toBeVisible()
    await expect(page.locator('table').getByText('Elon Musk')).toBeVisible()
    await expect(page.locator('table').getByText('Bernard Arnault')).toBeVisible()
    await expect(page.getByText(/Winning threshold: 430000000000 USD/i)).toBeVisible()
  })

  test('personal ledger progress bar is accessible with aria attributes', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/personal-ledger')

    const progressBar = page.getByRole('progressbar')
    await expect(progressBar).toBeVisible()
    await expect(progressBar).toHaveAttribute('aria-valuemin', '0')
    await expect(progressBar).toHaveAttribute('aria-valuemax', '100')
  })

  test('when game ended app shows winner overlay and read-only banner', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.endgameStatus = {
      gameEnded: true,
      winnerPlayerId: 'winner-1',
      winnerDisplayName: 'Alice Winner',
      winnerCompanyName: 'Winner Corp',
      gameEndedAtUtc: '2026-05-08T06:30:00Z',
      winningThresholdUsd: 430000000000,
      topRealWorldRichest: state.endgameStatus.topRealWorldRichest,
    }

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/dashboard')

    await expect(page.getByText('Game Over — Alice Winner has won! This server is now read-only.')).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Game Over' })).toBeVisible()
    await expect(page.getByText('Alice Winner has won this server.')).toBeVisible()
    await expect(page.getByRole('link', { name: 'View Final Rankings' })).toBeVisible()
  })

  test('admin can see and use the End Shard control in the admin dashboard', async ({ page }) => {
    const admin = makeAdminPlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [admin] })
    state.currentUserId = admin.id
    state.currentToken = `token-${admin.id}`

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${admin.id}`)
    await page.goto('/admin')

    // The admin heading is always visible when admin is authorized
    await expect(page.getByRole('heading', { name: 'Operations Dashboard' })).toBeVisible()
    // The End Shard button appears in the admin grid
    await expect(page.getByRole('button', { name: 'End Shard' })).toBeVisible()
  })

  test('admin End Shard confirmation flow: enter reason, confirm, see success', async ({ page }) => {
    const admin = makeAdminPlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [admin] })
    state.currentUserId = admin.id
    state.currentToken = `token-${admin.id}`

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${admin.id}`)
    await page.goto('/admin')

    await expect(page.getByRole('button', { name: 'End Shard' })).toBeVisible()

    // Enter a reason and click the End Shard button to open confirmation
    await page.getByLabel('Reason (optional)').fill('Season finale — ending the shard.')
    await page.getByRole('button', { name: 'End Shard' }).click()

    // Confirmation dialog should appear
    await expect(
      page.getByText('Are you sure you want to end this shard manually? This cannot be undone.'),
    ).toBeVisible()
    await expect(page.getByRole('button', { name: 'Confirm' })).toBeVisible()

    // Confirm the action
    await page.getByRole('button', { name: 'Confirm' }).click()

    // Success message should appear
    await expect(page.getByText('The game shard has been ended.')).toBeVisible()
  })

  test('admin End Shard cancel flow: confirmation dialog can be dismissed', async ({ page }) => {
    const admin = makeAdminPlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [admin] })
    state.currentUserId = admin.id
    state.currentToken = `token-${admin.id}`

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${admin.id}`)
    await page.goto('/admin')

    await expect(page.getByRole('button', { name: 'End Shard' })).toBeVisible()
    await page.getByRole('button', { name: 'End Shard' }).click()

    // Confirmation dialog is visible
    await expect(
      page.getByText('Are you sure you want to end this shard manually? This cannot be undone.'),
    ).toBeVisible()

    // Cancel — confirmation dialog should close, form re-appears
    await page.getByRole('button', { name: 'Cancel' }).click()
    await expect(page.getByRole('button', { name: 'End Shard' })).toBeVisible()
    await expect(
      page.getByText('Are you sure you want to end this shard manually? This cannot be undone.'),
    ).toBeHidden()
  })

  test('non-admin player cannot access the End Shard section', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/admin')

    // Non-admin should be redirected or see access denied
    await expect(page.getByRole('button', { name: 'End Shard' })).toBeHidden()
  })

  test('navbar shows lock icon when game has ended', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.endgameStatus = {
      ...state.endgameStatus,
      gameEnded: true,
      winnerDisplayName: 'Top Player',
    }

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/dashboard')

    // Lock icon tooltip should mention the winner
    await expect(page.getByText("This shard is read-only — Top Player has won.")).toBeVisible()
  })
})

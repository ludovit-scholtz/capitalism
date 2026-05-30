import { expect, test } from '@playwright/test'
import { loginAs, makePlayer, setupMockApi } from './helpers/mock-api'

test.describe('Game settings', () => {
  test('redirects to /login when unauthenticated', async ({ page }) => {
    setupMockApi(page, { servers: [] })
    await page.goto('/settings/game')

    await expect(page).toHaveURL('/login')
  })

  test('authenticated player can set gender, regenerate name, and save account name', async ({
    page,
  }) => {
    const player = makePlayer({
      displayName: 'Old Alias',
      personalAccountName: 'Old Alias',
    })
    const state = setupMockApi(page, {
      currentPlayer: player,
      rankingLeaderboard: [
        {
          playerId: player.id,
          displayName: 'Old Alias',
          personalAccountName: 'Old Alias',
          totalPoints: 320,
          globalRank: 1,
          rankMovement: 1,
        },
      ],
    })

    await loginAs(page, state, player, 'token-player')
    await page.goto('/')

    await page.getByRole('link', { name: 'Game settings' }).click()
    await expect(page).toHaveURL('/settings/game')

    await page.getByRole('radio', { name: 'Select female' }).click()
    const regenerateButton = page.getByRole('button', { name: '🎲' })
    await regenerateButton.click()
    await expect(page.getByRole('radio', { name: 'Select female' })).toHaveAttribute('aria-checked', 'true')
    await page.getByLabel('Generated personal name').fill('Nova Alias')
    await page.getByRole('button', { name: 'Save changes' }).click()
    await expect(page.getByRole('status')).toContainText('Personal account name updated.')

    await page.goto('/ranking')
    await expect(
      page.locator('table[aria-label="Master ranking leaderboard table"]'),
    ).toContainText('Nova Alias')
  })

  test('player can schedule and cancel account deletion in the danger zone', async ({ page }) => {
    const player = makePlayer({ email: 'alice@example.com', displayName: 'Alice' })
    const state = setupMockApi(page, { currentPlayer: player })

    await loginAs(page, state, player, 'token-player')
    await page.goto('/settings/game')

    const dangerZone = page.getByTestId('danger-zone')
    await expect(dangerZone).toBeVisible()

    await page.getByTestId('open-delete-account').click()

    // Mismatched email keeps the confirm button disabled.
    await page.getByTestId('confirm-email-input').fill('wrong@example.com')
    await expect(page.getByTestId('confirm-delete-account')).toBeDisabled()

    // Correct email enables and schedules the deletion.
    await page.getByTestId('confirm-email-input').fill('alice@example.com')
    await page.getByTestId('confirm-delete-account').click()

    const pending = page.getByTestId('deletion-pending')
    await expect(pending).toBeVisible()
    await expect(pending).toContainText('scheduled for deletion')

    // Cancelling restores the active danger-zone state.
    await page.getByTestId('cancel-deletion').click()
    await expect(page.getByTestId('open-delete-account')).toBeVisible()
  })
})

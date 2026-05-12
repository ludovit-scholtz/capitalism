import { expect, test } from '@playwright/test'
import { loginAs, makePlayer, setupMockApi } from './helpers/mock-api'

test.describe('Game settings', () => {
  test('redirects to /login when unauthenticated', async ({ page }) => {
    setupMockApi(page, { servers: [] })
    await page.goto('/settings/game')

    await expect(page).toHaveURL('/login')
  })

  test('authenticated player can rename personal account name from the menu and see it in the master ranking', async ({
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

    await page.getByLabel('Personal account name').fill('Nova Alias')
    await page.getByRole('button', { name: 'Save changes' }).click()
    await expect(page.getByRole('status')).toContainText('Personal account name updated.')

    await page.goto('/ranking')
    await expect(
      page.locator('table[aria-label="Master ranking leaderboard table"]'),
    ).toContainText('Nova Alias')
  })
})

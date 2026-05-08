import { expect, test } from '@playwright/test'
import { makePlayer, setupMockApi } from '../../helpers/mock-api'

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
      winningThresholdUsd: 178000000000,
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
})

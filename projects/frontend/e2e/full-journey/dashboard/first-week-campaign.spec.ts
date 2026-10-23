import { test, expect, type Page } from '@playwright/test'
import { setupMockApi, makePlayer } from '../../helpers/mock-api'
import { completeCampaignGoal, makeCampaignState, mockFirstWeekCampaign, type MockCampaignState } from '../../helpers/mock-api-campaign'

/** The first page load of a run compiles the dev bundle lazily, which can exceed the default 2 s assertion timeout. */
const COLD_START_TIMEOUT = 15_000

async function openDashboard(page: Page, state: MockCampaignState) {
  const player = makePlayer({
    onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
    companies: [
      {
        id: 'comp-1',
        playerId: 'player-1',
        name: 'Campaign Corp',
        cash: 400000,
        foundedAtUtc: '2026-01-01T00:00:00Z',
        buildings: [],
      },
    ],
  })
  const mock = setupMockApi(page, { players: [player] })
  mock.currentUserId = player.id
  mock.currentToken = `token-${player.id}`
  await mockFirstWeekCampaign(page, state)
  await page.addInitScript((value) => {
    localStorage.setItem('auth_token', value)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)
  await page.goto('/dashboard')
}

test.describe('Dashboard — first week campaign card', () => {
  test('shows the 7-day strip and the day 1 goal with a disabled claim button', async ({ page }) => {
    await openDashboard(page, makeCampaignState({ dayNumber: 1 }))

    const card = page.getByRole('region', { name: 'Your first week' })
    await expect(card).toBeVisible({ timeout: COLD_START_TIMEOUT })
    await expect(card.getByText('Day 1 of 7')).toBeVisible()
    await expect(card.getByRole('list', { name: 'First week progress' }).getByRole('listitem')).toHaveCount(7)
    await expect(card.getByLabel('Day 1: in progress')).toBeVisible()
    await expect(card.getByLabel('Day 2: locked')).toBeVisible()
    await expect(card.getByText('Make your first sale')).toBeVisible()
    await expect(card.getByText('Reward: $2,000')).toBeVisible()
    await expect(card.getByRole('button', { name: 'Claim reward' })).toBeDisabled()
  })

  test('claims a finished goal once, then features the next goal with a disabled button', async ({ page }) => {
    const state = makeCampaignState({ dayNumber: 2 })
    completeCampaignGoal(state, 'FIRST_SALE')
    await openDashboard(page, state)

    const card = page.getByRole('region', { name: 'Your first week' })
    await expect(card.getByLabel('Day 1: reward ready')).toBeVisible({ timeout: COLD_START_TIMEOUT })
    const claim = card.getByRole('button', { name: 'Claim reward' })
    await expect(claim).toBeEnabled()

    await claim.click()

    await expect(card.getByText('Reward of $2,000 added to your company account.')).toBeVisible()
    await expect(card.getByLabel('Day 1: reward claimed')).toBeVisible()
    await expect(card.getByText('Own two buildings')).toBeVisible()
    await expect(card.getByRole('button', { name: 'Claim reward' })).toBeDisabled()
    expect(state.claimRequests).toEqual(['FIRST_SALE'])
  })

  test('tells the player to come back tomorrow once every unlocked goal is claimed', async ({ page }) => {
    const state = makeCampaignState({ dayNumber: 1 })
    completeCampaignGoal(state, 'FIRST_SALE')
    state.goals[0]!.isClaimed = true
    state.goals[0]!.canClaim = false
    await openDashboard(page, state)

    const card = page.getByRole('region', { name: 'Your first week' })
    await expect(card.getByText('The goal for day 2 unlocks tomorrow.')).toBeVisible({ timeout: COLD_START_TIMEOUT })
    await expect(card.getByRole('button', { name: 'Claim reward' })).toHaveCount(0)
  })

  test('is hidden when the campaign has not started', async ({ page }) => {
    await openDashboard(page, makeCampaignState({ started: false }))

    await expect(page.getByRole('heading', { name: 'Dashboard' }).first()).toBeVisible({ timeout: COLD_START_TIMEOUT })
    await expect(page.getByRole('region', { name: 'Your first week' })).toHaveCount(0)
  })

  test('is hidden after the seven days are over', async ({ page }) => {
    await openDashboard(page, makeCampaignState({ dayNumber: 7, hoursAgo: 8 * 24 }))

    await expect(page.getByRole('heading', { name: 'Dashboard' }).first()).toBeVisible({ timeout: COLD_START_TIMEOUT })
    await expect(page.getByRole('region', { name: 'Your first week' })).toHaveCount(0)
  })
})

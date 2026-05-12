import { expect, test } from '@playwright/test'
import { makePlayer, setupMockApi } from '../../helpers/mock-api'

test.describe('company settings', () => {
  test('shows tabbed company settings sections', async ({ page }) => {
    const player = makePlayer({
      id: 'player-company-settings-tabs',
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
    })
    const company = {
      id: 'company-settings-tabs',
      playerId: player.id,
      name: 'Tabbed Company',
      cash: 500_000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      foundedAtTick: 0,
      dividendPayoutRatio: 0.2,
      citySalaryMultipliers: { 'city-ba': 1, 'city-pr': 1, 'city-vi': 1 },
      buildings: [],
    }
    player.companies.push(company)

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7_200_000).toISOString())
    }, state.currentToken)

    await page.goto(`/company/${company.id}/settings`)
    await expect(page.getByRole('heading', { name: company.name })).toBeVisible()

    await page.getByRole('button', { name: 'Salaries' }).click()
    await expect(page.locator('.salary-table')).toBeVisible()

    await page.getByRole('button', { name: 'Administration' }).click()
    await expect(page.locator('.overhead-value')).toBeVisible()

    await page.getByRole('button', { name: 'Dividends' }).click()
    await expect(page.getByText('Current annual dividend rate')).toBeVisible()
  })

  test('allows proposing and voting on a dividend policy', async ({ page }) => {
    const owner = makePlayer({
      id: 'player-company-settings-owner',
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
    })
    const minority = makePlayer({
      id: 'player-company-settings-minority',
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
    })
    const company = {
      id: 'company-settings-dividends',
      playerId: owner.id,
      name: 'Dividend Policy Co',
      cash: 500_000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      foundedAtTick: 0,
      dividendPayoutRatio: 0.2,
      citySalaryMultipliers: { 'city-ba': 1 },
      buildings: [],
    }
    owner.companies.push(company)

    const state = setupMockApi(page, {
      players: [owner, minority],
      shareholdings: [
        { companyId: company.id, ownerPlayerId: owner.id, shareCount: 700 },
        { companyId: company.id, ownerPlayerId: minority.id, shareCount: 300 },
      ],
    })
    state.currentUserId = owner.id
    state.currentToken = `token-${owner.id}`

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7_200_000).toISOString())
    }, state.currentToken)

    await page.goto(`/company/${company.id}/settings`)
    await page.getByRole('button', { name: 'Dividends' }).click()
    await page.getByLabel('Propose a new dividend rate (%)').fill('45')
    await page.getByRole('button', { name: 'Propose Change' }).click()

    await expect(page.getByText('Pending proposal')).toBeVisible()
    await page.getByRole('button', { name: 'Approve' }).click()
    await expect(page.getByText('Dividend vote submitted.')).toBeVisible()
  })
})

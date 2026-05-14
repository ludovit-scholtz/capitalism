import { expect, test } from '@playwright/test'
import { makePlayer, setupMockApi } from '../../helpers/mock-api'

function setLocalSession(token: string) {
  localStorage.setItem('auth_token', token)
  localStorage.setItem('auth_expires', new Date(Date.now() + 7_200_000).toISOString())
  localStorage.setItem('auth_provider', 'local')
}

test.describe('forbidden error mapping', () => {
  test('shows generic forbidden message when storing a building upgrade is denied', async ({ page }) => {
    const player = makePlayer({
      id: 'player-forbidden-building',
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
    })

    player.companies.push({
      id: 'company-forbidden-building',
      playerId: player.id,
      name: 'Forbidden Building Co',
      cash: 500_000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      foundedAtTick: 0,
      buildings: [
        {
          id: 'building-forbidden-building',
          companyId: 'company-forbidden-building',
          cityId: 'city-ba',
          type: 'FACTORY',
          name: 'Forbidden Factory',
          latitude: 48.15,
          longitude: 17.11,
          level: 1,
          powerConsumption: 2,
          isForSale: false,
          builtAtUtc: '2026-01-01T00:00:00Z',
          pendingConfiguration: null,
          units: [
            {
              id: 'building-forbidden-unit',
              buildingId: 'building-forbidden-building',
              unitType: 'PURCHASE',
              gridX: 0,
              gridY: 0,
              level: 1,
              linkUp: false,
              linkDown: false,
              linkLeft: false,
              linkRight: false,
              linkUpLeft: false,
              linkUpRight: false,
              linkDownLeft: false,
              linkDownRight: false,
            },
          ],
        },
      ],
    })

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await page.route('**/graphql', async (route) => {
      const body = route.request().postDataJSON() as { query?: string } | null
      const query = body?.query ?? ''
      if (query.includes('storeBuildingConfiguration')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [{ message: 'foreign building probe', extensions: { code: 'FORBIDDEN' } }],
          }),
        })
        return
      }

      await route.fallback()
    })

    await page.addInitScript(setLocalSession, state.currentToken)

    await page.goto('/building/building-forbidden-building')
    await page.getByRole('button', { name: 'Edit Building' }).click()

    const plannedSection = page
      .locator('.grid-section')
      .filter({ has: page.getByRole('heading', { name: 'Planned Upgrade' }) })
      .first()
    await plannedSection.locator('.unit-row').nth(1).locator('.grid-cell').nth(1).click()
    await page.locator('.sidebar').first().getByRole('button', { name: 'Purchase' }).click()

    await page.getByRole('button', { name: 'Store Upgrade' }).click()
    await expect(page.locator('.save-error-banner')).toContainText(
      "You don't have permission to perform this action.",
    )
  })

  test('shows generic forbidden message when saving company settings is denied', async ({ page }) => {
    const player = makePlayer({
      id: 'player-forbidden-company',
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
    })

    const companyId = 'company-forbidden-settings'
    player.companies.push({
      id: companyId,
      playerId: player.id,
      name: 'Forbidden Settings Co',
      cash: 500_000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      foundedAtTick: 0,
      dividendPayoutRatio: 0.2,
      citySalaryMultipliers: { 'city-ba': 1, 'city-pr': 1, 'city-vi': 1 },
      buildings: [],
    })

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await page.route('**/graphql', async (route) => {
      const body = route.request().postDataJSON() as { query?: string } | null
      const query = body?.query ?? ''
      if (query.includes('UpdateCompanySettings')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [{ message: 'foreign company probe', extensions: { code: 'FORBIDDEN' } }],
          }),
        })
        return
      }

      await route.fallback()
    })

    await page.addInitScript(setLocalSession, state.currentToken)
    await page.goto(`/company/${companyId}/settings`)

    await page.getByLabel('Company Name').fill('Forbidden Settings Co Updated')
    await page.getByRole('button', { name: 'Save' }).click()

    await expect(page.getByRole('alert').first()).toContainText(
      "You don't have permission to perform this action.",
    )
  })
})

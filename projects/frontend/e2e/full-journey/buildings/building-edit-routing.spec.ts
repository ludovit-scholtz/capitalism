import { expect, test } from '@playwright/test'
import { makePlayer, setupMockApi } from '../../helpers/mock-api'

test.describe('Building edit route tabs', () => {
  test('syncs edit tabs into URL and restores tab on refresh', async ({ page }) => {
    const player = makePlayer()
    player.companies.push({
      id: 'company-edit-route',
      playerId: player.id,
      name: 'Routing Works Ltd',
      cash: 500000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [
        {
          id: 'building-edit-route',
          companyId: 'company-edit-route',
          cityId: 'city-ba',
          type: 'FACTORY',
          name: 'Routing Factory',
          latitude: 48.15,
          longitude: 17.11,
          level: 1,
          powerConsumption: 2,
          isForSale: false,
          builtAtUtc: '2026-01-01T00:00:00Z',
          pendingConfiguration: null,
          units: [
            {
              id: 'unit-0',
              buildingId: 'building-edit-route',
              unitType: 'PURCHASE',
              gridX: 0,
              gridY: 0,
              level: 1,
              linkUp: false,
              linkDown: false,
              linkLeft: false,
              linkRight: true,
              linkUpLeft: false,
              linkUpRight: false,
              linkDownLeft: false,
              linkDownRight: false,
            },
            {
              id: 'unit-1',
              buildingId: 'building-edit-route',
              unitType: 'MANUFACTURING',
              gridX: 1,
              gridY: 0,
              level: 1,
              linkUp: false,
              linkDown: false,
              linkLeft: true,
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

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
      localStorage.setItem('auth_provider', 'local')
    }, `token-${player.id}`)

    await page.context().addCookies([
      {
        name: 'auth_token',
        value: `token-${player.id}`,
        url: process.env.CI ? 'http://localhost:4173' : 'http://localhost:5173',
        httpOnly: true,
        sameSite: 'Strict',
      },
    ])

    await page.goto('/building/building-edit-route')
    await expect(page.getByRole('heading', { name: 'Routing Factory' })).toBeVisible()

    await page.getByRole('button', { name: 'Edit Building' }).click()
    await expect(page).toHaveURL(/\/building\/building-edit-route$/)

    const plannedSection = page
      .locator('.grid-section')
      .filter({ has: page.getByRole('heading', { name: 'Planned Upgrade' }) })
      .first()
    await plannedSection.locator('.unit-row').nth(0).locator('.grid-cell').nth(0).click()

    await expect(page.getByRole('button', { name: 'Config' })).toHaveClass(/unit-tab-btn--active/)
    await expect(page.getByText('Energy Settings')).toBeVisible()

    await page.getByRole('button', { name: 'Performance' }).click()
    await expect(page).toHaveURL(/\/building\/building-edit-route\/edit\/performance$/)

    await page.getByRole('button', { name: 'Maintenance' }).click()
    await expect(page).toHaveURL(/\/building\/building-edit-route\/edit\/maintenance$/)
    await expect(page.locator('.unit-upgrade-panel')).toBeVisible()

    await page.reload()
    await expect(page).toHaveURL(/\/building\/building-edit-route\/edit\/maintenance$/)
    await plannedSection.locator('.unit-row').nth(0).locator('.grid-cell').nth(0).click()
    await expect(page.getByRole('button', { name: 'Maintenance' })).toHaveClass(/unit-tab-btn--active/)

    await page.goto('/building/building-edit-route')
    await expect(page.getByText('Energy Settings')).toHaveCount(0)
  })
})

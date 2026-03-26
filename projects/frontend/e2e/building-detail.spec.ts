import { expect, test } from '@playwright/test'
import { makePlayer, setupMockApi } from './helpers/mock-api'

function getGridSection(page: Parameters<typeof test>[0]['page'], heading: string) {
  return page.locator('.grid-section').filter({ has: page.getByRole('heading', { name: heading }) }).first()
}

function getGridCell(section: ReturnType<typeof getGridSection>, x: number, y: number) {
  return section.locator('.unit-row').nth(y).locator('.grid-cell').nth(x)
}

test.describe('Building detail upgrades', () => {
  test('queues a diagonal link upgrade and applies it after the required tick', async ({ page }) => {
    const player = makePlayer()
    player.companies.push({
      id: 'company-1',
      playerId: player.id,
      name: 'Link Works Ltd',
      cash: 500000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [
        {
          id: 'building-1',
          companyId: 'company-1',
          cityId: 'city-ba',
          type: 'FACTORY',
          name: 'Link Works Factory',
          latitude: 48.15,
          longitude: 17.11,
          level: 1,
          powerConsumption: 2,
          isForSale: false,
          builtAtUtc: '2026-01-01T00:00:00Z',
          pendingConfiguration: null,
          units: [
            {
              id: 'u-1',
              buildingId: 'building-1',
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
            {
              id: 'u-2',
              buildingId: 'building-1',
              unitType: 'MANUFACTURING',
              gridX: 1,
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
            {
              id: 'u-3',
              buildingId: 'building-1',
              unitType: 'STORAGE',
              gridX: 0,
              gridY: 1,
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
            {
              id: 'u-4',
              buildingId: 'building-1',
              unitType: 'B2B_SALES',
              gridX: 1,
              gridY: 1,
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

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/building/building-1')
    await expect(page.getByRole('heading', { name: 'Link Works Factory' })).toBeVisible()

    const currentSection = getGridSection(page, 'Current Configuration')
    const plannedSection = getGridSection(page, 'Planned Upgrade')
    const currentDiagonal = currentSection.locator('.link-toggle.diagonal').first()
    const plannedDiagonal = plannedSection.locator('.link-toggle.diagonal').first()

    await expect(currentDiagonal).toHaveClass(/state-none/)
    await expect(plannedDiagonal).toHaveClass(/state-none/)
    await expect(plannedSection.getByText('Upgrade time: 0 ticks')).toBeVisible()

    await plannedDiagonal.click()

    await expect(plannedDiagonal).toHaveClass(/state-tl-br/)
    await expect(plannedSection.getByText('Upgrade time: 1 ticks')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Store Upgrade' })).toBeEnabled()

    await page.getByRole('button', { name: 'Store Upgrade' }).click()

    await expect(page.getByRole('status')).toContainText('The current building keeps running until the queued layout activates in 1 ticks.')
    await expect(getGridSection(page, 'Queued Upgrade')).toBeVisible()
    await expect(currentDiagonal).toHaveClass(/state-none/)
    await expect(getGridSection(page, 'Queued Upgrade').locator('.link-toggle.diagonal').first()).toHaveClass(/state-tl-br/)

    state.gameState.currentTick += 1
    await page.reload()

    const refreshedCurrentSection = getGridSection(page, 'Current Configuration')
    const refreshedPlannedSection = getGridSection(page, 'Planned Upgrade')

    await expect(page.getByText('Building upgrade in progress')).toHaveCount(0)
    await expect(refreshedCurrentSection.locator('.link-toggle.diagonal').first()).toHaveClass(/state-tl-br/)

    await getGridCell(refreshedPlannedSection, 0, 0).click()
    await expect(page.getByText('Down-Right')).toBeVisible()
  })
})
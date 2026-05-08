import { test } from '@playwright/test'
import { setupMockApi, makePlayer } from '../../helpers/mock-api'

test('capture sell destroy screenshot', async ({ page }) => {
  const player = makePlayer()
  player.companies.push({
    id: 'company-shot',
    playerId: player.id,
    name: 'Screenshot Co',
    cash: 500000,
    foundedAtUtc: '2026-01-01T00:00:00Z',
    buildings: [
      {
        id: 'building-shot',
        companyId: 'company-shot',
        cityId: 'city-ba',
        type: 'FACTORY',
        name: 'Screenshot Factory',
        latitude: 48.15,
        longitude: 17.11,
        level: 2,
        powerConsumption: 2,
        isForSale: false,
        askingPrice: null,
        listedAtUtc: null,
        builtAtUtc: '2026-01-01T00:00:00Z',
        pendingConfiguration: null,
        units: [],
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

  await page.goto('/building/building-shot/sell')
  await page.locator('.open-destroy-confirm-btn').click()
  await page.screenshot({ path: 'docs/screenshots/sell-building-destroy-workflow-1920x1080.png', fullPage: true })
})

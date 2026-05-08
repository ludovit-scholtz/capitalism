import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer, type MockBuilding } from '../../helpers/mock-api'

async function authenticate(page: Parameters<typeof test>[0]['page'], token: string) {
  await page.addInitScript((value) => {
    localStorage.setItem('auth_token', value)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, token)
}

function makeMediaHouseBuilding(overrides: Partial<MockBuilding> = {}): MockBuilding {
  return {
    id: 'building-media',
    companyId: 'company-media',
    cityId: 'city-ba',
    type: 'MEDIA_HOUSE',
    name: 'Central Media House',
    latitude: 48.15,
    longitude: 17.11,
    level: 2,
    powerConsumption: 0,
    isForSale: false,
    mediaType: 'TV',
    contentValue: 2500,
    contentBudgetPerTick: 400,
    isGovernmentOwned: false,
    isAdvertisingActive: overrides.isAdvertisingActive ?? true,
    units: overrides.units ?? [],
    pendingConfiguration: overrides.pendingConfiguration ?? null,
    ...overrides,
  }
}

test.describe('Media house campaigns', () => {
  test('shows advertising badge on dashboard only when campaign is active', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-media',
          playerId: 'player-1',
          name: 'Media Corp',
          cash: 100000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [makeMediaHouseBuilding({ isAdvertisingActive: true })],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await authenticate(page, `token-${player.id}`)
    await page.goto('/dashboard')
    await page.getByRole('tab', { name: 'Buildings' }).click()
    await expect(page.getByText('📺 Advertising Active')).toBeVisible()

    player.companies[0]!.buildings[0]!.isAdvertisingActive = false
    await page.reload()
    await page.getByRole('tab', { name: 'Buildings' }).click()
    await expect(page.getByText('📺 Advertising Active')).toHaveCount(0)
  })

  test('can open media house detail and save campaign unit config', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-media',
          playerId: 'player-1',
          name: 'Media Corp',
          cash: 100000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [makeMediaHouseBuilding({ isAdvertisingActive: true })],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.cityMediaHouses['city-ba'] = [
      {
        id: 'building-media',
        name: 'Central Media House',
        cityId: 'city-ba',
        cityName: 'Bratislava',
        mediaType: 'TV',
        ownerCompanyId: 'company-media',
        ownerCompanyName: 'Media Corp',
        effectivenessMultiplier: 2,
        powerStatus: 'POWERED',
        isUnderConstruction: false,
        contentRanking: 100,
        contentValue: 2500,
        contentBudgetPerTick: 400,
        isGovernmentOwned: false,
      },
    ]

    await authenticate(page, `token-${player.id}`)
    await page.goto('/building/building-media')
    await expect(page.getByRole('heading', { name: /Media House Management/i })).toBeVisible()
    await page.getByRole('button', { name: /Save Campaign Unit/i }).click()
    await expect(page.getByText(/configuration saved/i)).toBeVisible()
  })

  test('keeps campaign unit configuration interactive while upgrade is pending', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-media',
          playerId: 'player-1',
          name: 'Media Corp',
          cash: 100000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [
            makeMediaHouseBuilding({
              pendingConfiguration: {
                id: 'plan-media-upgrade',
                buildingId: 'building-media',
                submittedAtUtc: '2026-01-01T00:00:00Z',
                submittedAtTick: 10,
                appliesAtTick: 25,
                totalTicksRequired: 15,
                units: [],
                removals: [
                  {
                    id: 'plan-removal-1',
                    gridX: 0,
                    gridY: 0,
                    startedAtTick: 10,
                    appliesAtTick: 25,
                    ticksRequired: 15,
                    isReverting: false,
                  },
                ],
              },
            }),
          ],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.gameState.currentTick = 12
    state.cityMediaHouses['city-ba'] = [
      {
        id: 'building-media',
        name: 'Central Media House',
        cityId: 'city-ba',
        cityName: 'Bratislava',
        mediaType: 'TV',
        ownerCompanyId: 'company-media',
        ownerCompanyName: 'Media Corp',
        effectivenessMultiplier: 2,
        powerStatus: 'POWERED',
        isUnderConstruction: false,
        contentRanking: 100,
        contentValue: 2500,
        contentBudgetPerTick: 400,
        isGovernmentOwned: false,
      },
    ]

    await authenticate(page, `token-${player.id}`)
    await page.goto('/building/building-media')

    await expect(page.getByText(/configuration changes will apply on completion/i)).toBeVisible()
    await expect(page.locator('.media-house-upgrade-config-notice')).toBeVisible()
    await page.getByRole('button', { name: /Save Campaign Unit/i }).click()
    await expect(page.getByText(/configuration saved/i)).toBeVisible()
  })
})

import { test, expect, type Page } from '@playwright/test'
import { makeAdminPlayer, makePlayer, setupMockApi } from '../../helpers/mock-api'

async function bootstrapAuth(page: Page, token: string) {
  await page.addInitScript((storedToken) => {
    localStorage.setItem('auth_token', storedToken)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, token)
}

function seedNpcState(state: ReturnType<typeof setupMockApi>) {
  state.npcCompanies = [
    {
      id: 'npc-1',
      companyId: 'npc-company-1',
      name: 'Bratislava Raw Materials Co.',
      archetype: 'RAW_MATERIALS',
      difficultyLevel: 2,
      homeCityId: 'city-ba',
      homeCityName: 'Bratislava',
      isActive: true,
      createdAtUtc: '2026-01-01T00:00:00Z',
      buildingCount: 1,
    },
    {
      id: 'npc-2',
      companyId: 'npc-company-2',
      name: 'Bratislava Retail Co.',
      archetype: 'RETAILER',
      difficultyLevel: 2,
      homeCityId: 'city-ba',
      homeCityName: 'Bratislava',
      isActive: true,
      createdAtUtc: '2026-01-01T00:00:00Z',
      buildingCount: 1,
    },
  ]
  state.cityCompetitorsByCityId['city-ba'] = [
    {
      companyId: 'npc-company-1',
      companyName: 'Bratislava Raw Materials Co.',
      isNpc: true,
      npcCompanyId: 'npc-1',
      archetype: 'RAW_MATERIALS',
      buildingCount: 1,
      estimatedRevenueLastTicks: 9200,
      marketSharePercent: 37.5,
      marketShareByCategory: [{ category: 'FURNITURE', sharePercent: 37.5 }],
      trend: 'UP',
    },
    {
      companyId: 'company-1',
      companyName: 'Player Foods',
      isNpc: false,
      npcCompanyId: null,
      archetype: null,
      buildingCount: 2,
      estimatedRevenueLastTicks: 11000,
      marketSharePercent: 44.0,
      marketShareByCategory: [{ category: 'FOOD_PROCESSING', sharePercent: 44.0 }],
      trend: 'STABLE',
    },
  ]
  state.npcDecisionLogs = [
    {
      id: 'npc-log-1',
      npcCompanyId: 'npc-1',
      npcCompanyName: 'Bratislava Raw Materials Co.',
      tick: 150,
      actionType: 'PRICE_SET',
      outcome: 'Set Wooden Chair price to 42.00.',
      createdAtUtc: '2026-01-02T00:00:00Z',
    },
  ]
  state.marketOverviewByCityId['city-ba'] = {
    cityId: 'city-ba',
    cityName: 'Bratislava',
    currencyCode: 'EUR',
    fromTick: 50,
    toTick: 150,
    products: [
      {
        productTypeId: 'pt-wooden-chair',
        productName: 'Wooden Chair',
        industry: 'FURNITURE',
        totalDemand: 500,
        totalQuantitySold: 460,
        satisfactionRate: 0.92,
        averageClearingPrice: 45,
        totalRevenue: 20700,
        sellerCount: 3,
        topCompetitorCompanyName: 'Bratislava Raw Materials Co.',
        topCompetitorMarketSharePercent: 37.5,
      },
    ],
  }

  const targetLot = state.buildingLots.find((lot) => lot.cityId === 'city-ba')
  if (targetLot) {
    targetLot.ownerCompanyId = 'npc-company-1'
    targetLot.buildingId = 'npc-building-1'
    targetLot.building = {
      id: 'npc-building-1',
      name: 'NPC Mine',
      type: 'MINE',
      isUnderConstruction: false,
      constructionCompletesAtTick: null,
      constructionCost: 5000,
      isForSale: false,
      askingPrice: null,
      destroyedAtUtc: null,
      destroyedReason: null,
    }
    targetLot.ownerCompany = { id: 'npc-company-1', name: 'Bratislava Raw Materials Co.' }
  }
}

test.describe('NPC competitors surfaces', () => {
  test('shows NPC-owned map markers with orange color', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    seedNpcState(state)
    await bootstrapAuth(page, `token-${player.id}`)

    await page.goto('/city/city-ba/buildings')
    await expect(page.locator('.lot-marker div').first()).toHaveAttribute('style', /#F97316/i)
  })

  test('hovering NPC marker shows tooltip with NPC label and archetype', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    seedNpcState(state)
    await bootstrapAuth(page, `token-${player.id}`)

    await page.goto('/city/city-ba/buildings')
    await page.locator('.lot-marker').first().hover()
    await expect(page.locator('.leaflet-tooltip')).toContainText('NPC: Bratislava Raw Materials Co. (RAW_MATERIALS)')
  })

  test('city competitors tab loads competitor table', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    seedNpcState(state)
    await bootstrapAuth(page, `token-${player.id}`)

    await page.goto('/city/city-ba/competitors')
    await expect(page.getByRole('heading', { name: 'Competitor Intelligence' })).toBeVisible()
    await expect(page.locator('.competitors-table')).toContainText('Bratislava Raw Materials Co.')
  })

  test('competitor rows render archetype badges', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    seedNpcState(state)
    await bootstrapAuth(page, `token-${player.id}`)

    await page.goto('/city/city-ba/competitors')
    await expect(page.locator('.arch-badge.arch-raw').first()).toBeVisible()
  })

  test('competitor panel shows market share values', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    seedNpcState(state)
    await bootstrapAuth(page, `token-${player.id}`)

    await page.goto('/city/city-ba/competitors')
    await expect(page.locator('.share-cell').first()).toContainText('37.5%')
  })

  test('market dashboard shows top competitor badge', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    seedNpcState(state)
    await bootstrapAuth(page, `token-${player.id}`)

    await page.goto('/market')
    await expect(page.locator('.top-competitor-badge')).toContainText('Top competitor: Bratislava Raw Materials Co. (37.5%)')
  })

  test('admin operations view shows NPC decision log entries', async ({ page }) => {
    const admin = makeAdminPlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [admin] })
    state.currentUserId = admin.id
    state.currentToken = `token-${admin.id}`
    state.rootAdminEmails = [admin.email]
    seedNpcState(state)
    await bootstrapAuth(page, `token-${admin.id}`)

    await page.goto('/operations/statistics')
    await expect(page.getByText('NPC Decision Log')).toBeVisible()
    await expect(page.getByText('Set Wooden Chair price to 42.00.')).toBeVisible()
  })

  test('admin can pause and resume NPC company from operations panel', async ({ page }) => {
    const admin = makeAdminPlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [admin] })
    state.currentUserId = admin.id
    state.currentToken = `token-${admin.id}`
    state.rootAdminEmails = [admin.email]
    seedNpcState(state)
    await bootstrapAuth(page, `token-${admin.id}`)

    await page.goto('/operations/statistics')
    await page.getByRole('button', { name: 'Pause NPC' }).first().click()
    await expect(page.getByText('NPC paused.')).toBeVisible()
    await page.getByRole('button', { name: 'Resume NPC' }).first().click()
    await expect(page.getByText('NPC resumed.')).toBeVisible()
  })

  test('competitors tab shows empty state when no competitors in city', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    // Do NOT seed NPC state so city-pr has no competitors
    await bootstrapAuth(page, `token-${player.id}`)

    await page.goto('/city/city-pr/competitors')
    await expect(page.getByRole('heading', { name: 'Competitor Intelligence' })).toBeVisible()
    await expect(page.locator('.competitors-empty')).toBeVisible()
  })

  test('competitors tab shows human competitor without archetype badge', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    seedNpcState(state)
    await bootstrapAuth(page, `token-${player.id}`)

    await page.goto('/city/city-ba/competitors')
    await expect(page.locator('.competitors-table')).toContainText('Player Foods')
    // Human competitor should have .arch-human badge
    await expect(page.locator('.arch-badge.arch-human').first()).toBeVisible()
  })

  test('competitors tab shows trend symbols', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    seedNpcState(state)
    await bootstrapAuth(page, `token-${player.id}`)

    await page.goto('/city/city-ba/competitors')
    // UP trend NPC should show upward arrow
    await expect(page.locator('.trend-cell').first()).toContainText('↗')
  })

  test('competitors tab renders on mobile viewport', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 })
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    seedNpcState(state)
    await bootstrapAuth(page, `token-${player.id}`)

    await page.goto('/city/city-ba/competitors')
    await expect(page.getByRole('heading', { name: 'Competitor Intelligence' })).toBeVisible()
    // Table should be in a scroll container on mobile
    await expect(page.locator('.competitors-table-wrap')).toBeVisible()
  })
})

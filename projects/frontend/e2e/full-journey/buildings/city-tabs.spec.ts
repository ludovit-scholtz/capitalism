import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer } from '../../helpers/mock-api'

async function authenticate(page: import('@playwright/test').Page, token: string) {
  await page.addInitScript((value) => {
    localStorage.setItem('auth_token', value)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, token)
}

function setupAuthenticated(page: import('@playwright/test').Page) {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  return { player, state }
}

test.describe('City tabs routing', () => {
  test('redirects /city/:cityId to overview tab', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/city/city-ba')
    await expect(page).toHaveURL(/\/city\/city-ba\/overview$/)
    await expect(page.getByRole('link', { name: /Overview/i })).toHaveClass(/city-tab--active/)
  })

  test('switches tabs by URL and supports browser back-forward', async ({ page }) => {
    const { player } = setupAuthenticated(page)
    await authenticate(page, `token-${player.id}`)

    await page.goto('/city/city-ba/overview')
    await page.getByRole('link', { name: /Buildings/i }).click()
    await expect(page).toHaveURL(/\/city\/city-ba\/buildings$/)
    await expect(page.locator('.map-container')).toBeVisible()

    await page.getByRole('link', { name: /Economy/i }).click()
    await expect(page).toHaveURL(/\/city\/city-ba\/economy$/)

    await page.goBack()
    await expect(page).toHaveURL(/\/city\/city-ba\/buildings$/)

    await page.goForward()
    await expect(page).toHaveURL(/\/city\/city-ba\/economy$/)
  })

  test('moves economy cycle from dashboard to city economy tab', async ({ page }) => {
    const { player, state } = setupAuthenticated(page)
    state.economicCycle = {
      id: 'cycle-1',
      phase: 'EXPANSION',
      phaseStartedTick: 10,
      expectedDurationTicks: 120,
      intensityFactor: 1.2,
      phaseEndTick: 130,
      ticksRemaining: 95,
    }
    state.activeMarketEvents = [
      {
        id: 'event-city-ba',
        eventType: 'DEMAND_SURGE',
        title: 'Bratislava demand surge',
        description: 'Local demand is accelerating.',
        magnitudeMultiplier: 1.1,
        startsAtTick: 10,
        expiresAtTick: 40,
        ticksRemaining: 30,
        affectedResourceTypeId: null,
        affectedResourceName: null,
        affectedResourceSlug: null,
        affectedCityId: 'city-ba',
        affectedCityName: 'Bratislava',
      },
      {
        id: 'event-city-pr',
        eventType: 'DEMAND_SURGE',
        title: 'Prague demand surge',
        description: 'Local demand is accelerating.',
        magnitudeMultiplier: 1.1,
        startsAtTick: 10,
        expiresAtTick: 40,
        ticksRemaining: 30,
        affectedResourceTypeId: null,
        affectedResourceName: null,
        affectedResourceSlug: null,
        affectedCityId: 'city-pr',
        affectedCityName: 'Prague',
      },
    ]

    await authenticate(page, `token-${player.id}`)

    await page.goto('/dashboard')
    await expect(page.getByText('Economy cycle')).toHaveCount(0)

    await page.goto('/city/city-ba/economy')
    await expect(page.getByRole('heading', { name: /Economy Cycle — Bratislava/i })).toBeVisible()
    await expect(page.getByText('Bratislava demand surge')).toBeVisible()
    await expect(page.getByText('Prague demand surge')).toHaveCount(0)

    await page.goto('/city/city-pr/economy')
    await expect(page).toHaveURL(/\/city\/city-pr\/economy$/)
    await expect(page.getByText('Bratislava demand surge')).toHaveCount(0)
  })
})

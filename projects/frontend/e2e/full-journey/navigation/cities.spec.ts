import { test, expect } from '@playwright/test'
import { makePlayer, setupMockApi, makeDefaultCities } from '../../helpers/mock-api'

test.describe('Cities page', () => {
  test('shows all seeded cities including Berlin and Warsaw', async ({ page }) => {
    const state = setupMockApi(page)
    state.cities = makeDefaultCities()
    await page.goto('/cities')

    await expect(page.getByRole('heading', { name: 'Cities' })).toBeVisible()

    // All 9 cities should be visible
    for (const cityName of [
      'Bratislava',
      'Prague',
      'Vienna',
      'New York',
      'London',
      'Beijing',
      'Delhi',
      'Berlin',
      'Warsaw',
    ]) {
      await expect(page.locator('.city-card', { hasText: cityName })).toBeVisible()
    }
  })

  test('shows Berlin card with EUR currency and key metrics', async ({ page }) => {
    const state = setupMockApi(page)
    state.cities = makeDefaultCities()
    await page.goto('/cities')

    const berlinCard = page.locator('.city-card', { hasText: 'Berlin' })
    await expect(berlinCard).toBeVisible()

    // Currency and country code
    await expect(berlinCard).toContainText('DE')
    await expect(berlinCard).toContainText('EUR')

    // Metrics: salary
    await expect(berlinCard).toContainText('22 EUR/h')

    // Population formatted as millions
    await expect(berlinCard).toContainText('3.7M')

    // Top resources chips are shown
    await expect(berlinCard.locator('.resource-chip').first()).toBeVisible()
  })

  test('shows Warsaw card with PLN currency and key metrics', async ({ page }) => {
    const state = setupMockApi(page)
    state.cities = makeDefaultCities()
    await page.goto('/cities')

    const warsawCard = page.locator('.city-card', { hasText: 'Warsaw' })
    await expect(warsawCard).toBeVisible()

    // Currency and country code
    await expect(warsawCard).toContainText('PL')
    await expect(warsawCard).toContainText('PLN')

    // Metrics: salary
    await expect(warsawCard).toContainText('35 PLN/h')

    // Population formatted as millions
    await expect(warsawCard).toContainText('1.9M')
  })

  test('shows resource chips for Berlin and Warsaw', async ({ page }) => {
    const state = setupMockApi(page)
    state.cities = makeDefaultCities()
    await page.goto('/cities')

    // Berlin has top resources chips
    const berlinCard = page.locator('.city-card', { hasText: 'Berlin' })
    await expect(berlinCard.locator('.resource-chip')).not.toHaveCount(0)

    // Warsaw has top resources chips
    const warsawCard = page.locator('.city-card', { hasText: 'Warsaw' })
    await expect(warsawCard.locator('.resource-chip')).not.toHaveCount(0)
  })

  test('each city card has a View City Map link', async ({ page }) => {
    const state = setupMockApi(page)
    state.cities = makeDefaultCities()
    await page.goto('/cities')

    const berlinCard = page.locator('.city-card', { hasText: 'Berlin' })
    await expect(berlinCard.getByRole('link', { name: /View City Map/i })).toBeVisible()

    const warsawCard = page.locator('.city-card', { hasText: 'Warsaw' })
    await expect(warsawCard.getByRole('link', { name: /View City Map/i })).toBeVisible()
  })

  test('shows retry button when API fails', async ({ page }) => {
    // Override the cities route to return an error before page loads
    setupMockApi(page)
    await page.route('**/graphql', async (route) => {
      const body = route.request().postDataJSON() as { query?: string } | null
      const query = body?.query ?? ''
      if (query.includes('cities') && !query.includes('city(') && !query.includes('GetCity')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ errors: [{ message: 'Service unavailable' }] }),
        })
        return
      }
      await route.fallback()
    })
    await page.goto('/cities')

    await expect(page.getByRole('button', { name: /try again/i })).toBeVisible()
  })

  test('nav link navigates to cities page', async ({ page }) => {
    const state = setupMockApi(page)
    state.cities = makeDefaultCities()
    await page.goto('/')

    await page.getByRole('button', { name: 'Main' }).hover()
    await page.locator('.desktop-section-panel').getByRole('link', { name: 'Cities' }).click()
    await expect(page).toHaveURL('/cities')
  })

  test('context switcher shows lock icon and tooltip for locked cities', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-cities-lock',
          playerId: 'player-cities-lock',
          name: 'Expansion Tracker',
          cash: 220000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    player.activeAccountType = 'COMPANY'
    player.activeCompanyId = 'company-cities-lock'

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.cities = makeDefaultCities().map((city) =>
      city.id === 'city-be'
        ? { ...city, isUnlocked: false, requiredNetWorth: 460000, currentNetWorth: 110000, progressPercent: 24 }
        : city,
    )

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/cities')
    await page.locator('.ctx-trigger').click()

    const berlinOption = page.locator('.ctx-city-option', { hasText: 'Berlin' })
    await expect(berlinOption).toBeVisible()
    await expect(berlinOption).toHaveAttribute('title', /Requires/i)
    await expect(berlinOption.locator('.ctx-city-lock')).toBeVisible()
  })
})

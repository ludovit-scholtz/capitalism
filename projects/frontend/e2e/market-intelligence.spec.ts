import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer } from './helpers/mock-api'

test.describe('Competitive market intelligence', () => {
  test('shows ranked sellers per product in selected city', async ({ page }) => {
    const player = makePlayer({
      id: 'mi-player',
      email: 'mi@test.com',
      password: 'TestPass1!',
      displayName: 'Market Player',
      onboardingCompletedAtUtc: new Date().toISOString(),
    })

    const state = setupMockApi(page, { players: [player] })
    const cityId = 'city-ba'
    const cityName = 'Bratislava'

    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    const marketIntelligenceResult = {
      cityId,
      cityName,
      dataFromTick: 100,
      dataToTick: 267,
      products: [
        {
          productTypeId: 'prod-chair',
          productName: 'Wooden Chair',
          productSlug: 'wooden-chair',
          totalWeeklySalesVolume: 1200,
          sellers: [
            {
              rank: 1,
              companyId: 'comp-alpha',
              displayName: 'Seller Alpha',
              askingPricePerUnit: 45,
              brandQuality: 0.74,
              estimatedWeeklySalesVolume: 760,
              marketShare: 0.6333,
            },
            {
              rank: 2,
              companyId: 'comp-beta',
              displayName: 'Seller Beta',
              askingPricePerUnit: 43,
              brandQuality: 0.42,
              estimatedWeeklySalesVolume: 440,
              marketShare: 0.3667,
            },
          ],
        },
      ],
    }

    await page.route('**/graphql', async (route) => {
      const body = route.request().postDataJSON() as { query?: string }
      if (typeof body.query === 'string' && body.query.includes('marketIntelligence')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ data: { marketIntelligence: marketIntelligenceResult } }),
        })
        return
      }
      await route.fallback()
    })

    await page.addInitScript(
      ({ token, cityId }) => {
        localStorage.setItem('auth_token', token)
        localStorage.setItem('auth_expires', new Date(Date.now() + 7_200_000).toISOString())
        localStorage.setItem('selected_city_id', cityId)
      },
      { token: `token-${player.id}`, cityId },
    )

    await page.goto(`/market-intelligence?city=${cityId}`)

    await expect(page.getByRole('heading', { name: 'Market Intelligence' })).toBeVisible()
    await expect(page.getByText('Seller Alpha')).toBeVisible()
    await expect(page.getByText('Seller Beta')).toBeVisible()
  })

  test('shows resource trend sparkline on global exchange rows', async ({ page }) => {
    const player = makePlayer({
      id: 'ex-player',
      email: 'ex@test.com',
      password: 'TestPass1!',
      displayName: 'Exchange Player',
      onboardingCompletedAtUtc: new Date().toISOString(),
    })

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7_200_000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/exchange')

    await expect(page.locator('.resource-sparkline')).not.toHaveCount(0)
  })
})

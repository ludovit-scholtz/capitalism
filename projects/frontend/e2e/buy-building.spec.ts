import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer } from './helpers/mock-api'

async function authenticate(page: Parameters<typeof test>[0]['page'], playerId: string) {
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${playerId}`)
}

test.describe('Buy Building View', () => {
  test('shows compatible land after selecting city and building type', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Land Corp',
          cash: 500000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-1')

    await page.getByRole('button', { name: /Factory/i }).click()
    await page.getByRole('button', { name: /Bratislava/i }).click()

    await expect(page.getByRole('button', { name: /Industrial Plot A1/i })).toBeVisible()
    await expect(page.getByText(/Population index/i)).toBeVisible()
  })

  test('purchases a selected land parcel and opens the building detail page', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Expansion Group',
          cash: 500000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-1')

    await page.getByRole('button', { name: /Factory/i }).click()
    await page.getByLabel('Building Name').fill('Danube Works')
    await page.getByRole('button', { name: /Bratislava/i }).click()
    await page.getByRole('button', { name: /Industrial Plot A1/i }).click()
    await page.getByRole('button', { name: /^Buy Now$/i }).click()

    await page.waitForURL(/\/building\//)
    await expect(page.getByRole('heading', { name: /Danube Works/i })).toBeVisible()
  })

  test('shows bank setup info panel and initial deposit field when BANK type is selected', async ({
    page,
  }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Financial Group',
          cash: 50000000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-1')

    await page.locator('.type-card', { hasText: 'Bank' }).click()

    // Bank setup info panel should appear
    await expect(page.getByText('Setting up your bank')).toBeVisible()
    await expect(page.getByText('Initial Base Capital Deposit')).toBeVisible()
    await expect(page.getByLabel(/Initial Base Capital Deposit/i)).toBeVisible()
  })

  test('purchasing a BANK lot redirects to /bank/:id, not /building/:id', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Capital Bank Group',
          cash: 50000000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await authenticate(page, player.id)
    await page.goto('/buy-building/company-1')

    await page.locator('.type-card', { hasText: 'Bank' }).click()
    await page.getByRole('button', { name: /Bratislava/i }).click()
    // Select any lot
    await page.locator('.lot-card').first().click()
    await page.getByRole('button', { name: /^Buy Now$/i }).click()

    // Should redirect to bank management page, not generic building page
    await page.waitForURL(/\/bank\//)
    await expect(page).toHaveURL(/\/bank\//)
  })
})

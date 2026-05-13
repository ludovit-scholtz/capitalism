import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer, type MockResearchBrandState } from '../../helpers/mock-api'

const COMPANY_ID = 'co-rd-test-1'

function makeRdBrand(overrides: Partial<MockResearchBrandState> = {}): MockResearchBrandState {
  return {
    id: 'brand-test-1',
    companyId: COMPANY_ID,
    name: 'Wooden Chair Quality Brand',
    scope: 'PRODUCT',
    productTypeId: 'pt-wooden-chair',
    productName: 'Wooden Chair',
    industryCategory: null,
    awareness: 0.6,
    quality: 0.5,
    marketingQuality: 0.2,
    combinedBrandQuality: 0.6,
    marketingEfficiencyMultiplier: 1.2,
    accumulatedResearchBudget: 12500,
    baseResearchBudget: 25000,
    maxCompetitorBudget: 20000,
    ...overrides,
  }
}

function setupResearchTest(page: Parameters<typeof setupMockApi>[0]) {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  player.companies.push({
    id: COMPANY_ID,
    playerId: player.id,
    name: 'Research Test Corp',
    cash: 500000,
    foundedAtUtc: '2026-01-01T00:00:00Z',
    buildings: [],
  })

  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  return { player, state }
}

test.describe('Company Research Dashboard', () => {
  test('shows research dashboard title and product quality table for company with R&D brands', async ({
    page,
  }) => {
    const { player, state } = setupResearchTest(page)
    state.researchBrands[COMPANY_ID] = [makeRdBrand()]

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/company/${COMPANY_ID}/research`)

    // Title is visible
    await expect(page.getByRole('heading', { name: /Company Research/i, level: 1 })).toBeVisible()

    // Product table section heading is visible
    await expect(page.locator('.research-section-title').first()).toBeVisible()
  })

  test('shows quality level badge for product brand', async ({ page }) => {
    const { player, state } = setupResearchTest(page)
    state.researchBrands[COMPANY_ID] = [makeRdBrand({ quality: 0.5 })]

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/company/${COMPANY_ID}/research`)

    // quality level badge visible (e.g. "Lv 5")
    await expect(page.locator('.quality-level-badge').first()).toBeVisible()
    await expect(page.locator('.quality-level-badge').first()).toContainText('Lv')
  })

  test('shows price premium for product brand', async ({ page }) => {
    const { player, state } = setupResearchTest(page)
    // Combined quality 0.6 → premium = 0.6 × 50% = 30%
    state.researchBrands[COMPANY_ID] = [makeRdBrand({ combinedBrandQuality: 0.6 })]

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/company/${COMPANY_ID}/research`)

    // Price premium shows +30.0%
    await expect(page.getByText('+30.0%')).toBeVisible()
  })

  test('shows empty state when company has no R&D brands', async ({ page }) => {
    const { player, state } = setupResearchTest(page)
    state.researchBrands[COMPANY_ID] = []

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/company/${COMPANY_ID}/research`)

    await expect(page.locator('.research-empty-state')).toBeVisible()
  })

  test('shows research dashboard link on company action row in dashboard', async ({ page }) => {
    const { player } = setupResearchTest(page)

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/dashboard')

    // Find the research link on the company actions row
    const companyCard = page.locator('.company-card').first()
    const researchLink = companyCard.getByRole('link').filter({ hasText: /Research/i }).first()
    await expect(researchLink).toBeVisible()
    await expect(researchLink).toHaveAttribute('href', `/company/${COMPANY_ID}/research`)
  })
})

import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer, type MockCityEconomicReport } from '../../helpers/mock-api'

// ── Helpers ──────────────────────────────────────────────────────────────────

function makeEconomicReport(overrides: Partial<MockCityEconomicReport> = {}): MockCityEconomicReport {
  return {
    id: `report-${Date.now()}-${Math.random()}`,
    cityId: 'city-ba',
    taxCycleEnd: 100,
    totalSalaries: 500_000,
    totalPublicRevenue: 1_200_000,
    activeCompanies: 12,
    totalPowerConsumption: 80,
    totalPowerSupply: 100,
    averageProductQuality: 0.72,
    economicIndex: 75,
    computedAtUtc: '2026-05-01T00:00:00Z',
    ...overrides,
  }
}

function setupAuthenticatedCityMap(page: import('@playwright/test').Page) {
  const player = makePlayer({
    onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
    companies: [
      {
        id: 'company-1',
        playerId: 'player-1',
        name: 'Test Empire',
        cash: 500000,
        foundedAtUtc: '2026-01-01T00:00:00Z',
        buildings: [],
      },
    ],
  })
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  return { state, player }
}

async function authenticateViaLocalStorage(page: import('@playwright/test').Page, playerId: string) {
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${playerId}`)
}

// ── Tests ─────────────────────────────────────────────────────────────────────

test.describe('City Economic Health Indicators', () => {
  // ── Panel visibility ─────────────────────────────────────────────────────

  test('shows economic health section heading on city map page', async ({ page }) => {
    const { state, player } = setupAuthenticatedCityMap(page)
    state.cityEconomicReports = {
      'city-ba': [makeEconomicReport()],
    }
    await authenticateViaLocalStorage(page, player.id)
    await page.goto('/city/city-ba/economy')

    await expect(page.getByRole('heading', { name: /Economic Health/i })).toBeVisible()
  })

  test('shows economic index value when report data exists', async ({ page }) => {
    const { state, player } = setupAuthenticatedCityMap(page)
    state.cityEconomicReports = {
      'city-ba': [makeEconomicReport({ economicIndex: 75 })],
    }
    await authenticateViaLocalStorage(page, player.id)
    await page.goto('/city/city-ba/economy')

    // Score ring shows the index value
    await expect(page.locator('.score-value').filter({ hasText: '75' })).toBeVisible()
  })

  test('shows Thriving status for index >= 70', async ({ page }) => {
    const { state, player } = setupAuthenticatedCityMap(page)
    state.cityEconomicReports = {
      'city-ba': [makeEconomicReport({ economicIndex: 80 })],
    }
    await authenticateViaLocalStorage(page, player.id)
    await page.goto('/city/city-ba/economy')

    await expect(page.locator('.status-badge').filter({ hasText: /Thriving/i })).toBeVisible()
  })

  test('shows Stable status for index 40-69', async ({ page }) => {
    const { state, player } = setupAuthenticatedCityMap(page)
    state.cityEconomicReports = {
      'city-ba': [makeEconomicReport({ economicIndex: 55 })],
    }
    await authenticateViaLocalStorage(page, player.id)
    await page.goto('/city/city-ba/economy')

    await expect(page.locator('.status-badge').filter({ hasText: /Stable/i })).toBeVisible()
  })

  test('shows Declining status for index < 40', async ({ page }) => {
    const { state, player } = setupAuthenticatedCityMap(page)
    state.cityEconomicReports = {
      'city-ba': [makeEconomicReport({ economicIndex: 25 })],
    }
    await authenticateViaLocalStorage(page, player.id)
    await page.goto('/city/city-ba/economy')

    await expect(page.locator('.status-badge').filter({ hasText: /Declining/i })).toBeVisible()
  })

  test('shows no data message when no reports exist', async ({ page }) => {
    const { state, player } = setupAuthenticatedCityMap(page)
    state.cityEconomicReports = { 'city-ba': [] }
    await authenticateViaLocalStorage(page, player.id)
    await page.goto('/city/city-ba/economy')

    await expect(page.getByText(/No reports yet/i)).toBeVisible()
  })

  // ── Metrics grid ─────────────────────────────────────────────────────────

  test('shows 4 metric cards with salary, revenue, companies, and quality', async ({ page }) => {
    const { state, player } = setupAuthenticatedCityMap(page)
    state.cityEconomicReports = {
      'city-ba': [
        makeEconomicReport({
          totalSalaries: 500_000,
          totalPublicRevenue: 1_200_000,
          activeCompanies: 12,
          averageProductQuality: 0.72,
        }),
      ],
    }
    await authenticateViaLocalStorage(page, player.id)
    await page.goto('/city/city-ba/economy')

    const panel = page.locator('.health-panel')
    await expect(panel.locator('.metric-card')).toHaveCount(4)
  })

  // ── Detail modal ─────────────────────────────────────────────────────────

  test('opens detail modal with full metrics when View Details is clicked', async ({ page }) => {
    const { state, player } = setupAuthenticatedCityMap(page)
    state.cityEconomicReports = {
      'city-ba': [makeEconomicReport({ economicIndex: 75, taxCycleEnd: 100 })],
    }
    await authenticateViaLocalStorage(page, player.id)
    await page.goto('/city/city-ba/economy')

    await page.getByRole('button', { name: /View Details/i }).click()

    await expect(page.locator('.health-modal')).toBeVisible()
    await expect(page.locator('.health-modal').getByText('Economic Index')).toBeVisible()
  })

  test('modal can be closed with the ✕ button', async ({ page }) => {
    const { state, player } = setupAuthenticatedCityMap(page)
    state.cityEconomicReports = {
      'city-ba': [makeEconomicReport()],
    }
    await authenticateViaLocalStorage(page, player.id)
    await page.goto('/city/city-ba/economy')

    await page.getByRole('button', { name: /View Details/i }).click()
    await expect(page.locator('.health-modal')).toBeVisible()

    await page.locator('.modal-close-btn').click()
    await expect(page.locator('.health-modal')).toBeHidden()
  })

  // ── History sparkline ─────────────────────────────────────────────────────

  test('shows sparkline trend when history has multiple reports', async ({ page }) => {
    const { state, player } = setupAuthenticatedCityMap(page)
    state.cityEconomicReports = {
      'city-ba': [
        makeEconomicReport({ taxCycleEnd: 10, economicIndex: 50 }),
        makeEconomicReport({ id: 'r2', taxCycleEnd: 20, economicIndex: 65 }),
        makeEconomicReport({ id: 'r3', taxCycleEnd: 30, economicIndex: 75 }),
      ],
    }
    await authenticateViaLocalStorage(page, player.id)
    await page.goto('/city/city-ba/economy')

    await expect(page.locator('.sparkline')).toBeVisible()
  })
})

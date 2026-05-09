import { test, expect } from '@playwright/test'
import { makePlayer, setupMockApi, type MockBuilding } from '../../helpers/mock-api'

/**
 * Creates a MINE building for extraction-history tests.
 * The MINING unit is at (0,0) so the first grid-cell selects it.
 */
function makeMineBuilding(
  opts: {
    id?: string
    companyId?: string
    lotMaterialQuantity?: number | null
    lotOriginalMaterialQuantity?: number | null
  } = {},
): MockBuilding {
  return {
    id: opts.id ?? 'building-mine-hist',
    companyId: opts.companyId ?? 'company-hist',
    cityId: 'city-ba',
    type: 'MINE',
    name: 'Iron Mine (History)',
    latitude: 48.15,
    longitude: 17.11,
    level: 1,
    powerConsumption: 2,
    isForSale: false,
    builtAtUtc: '2026-01-01T00:00:00Z',
    pendingConfiguration: null,
    lotMaterialQuantity: opts.lotMaterialQuantity ?? 800,
    lotOriginalMaterialQuantity: opts.lotOriginalMaterialQuantity ?? 1000,
    lotResourceTypeId: 'res-iron',
    lotMaterialQuality: 0.8,
    units: [
      {
        id: 'unit-mining-hist',
        buildingId: opts.id ?? 'building-mine-hist',
        unitType: 'MINING',
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
        resourceTypeId: 'res-iron',
      },
    ],
  }
}

/** Builds 30 synthetic per-tick extraction records, one per game-day (day 0..29). */
function makeExtractionRecords(_buildingId: string = 'building-mine-hist') {
  // Use one record per game-day (each day = 24 ticks) so dailyTotals has 30 buckets.
  // Ticks at the midpoint of each day: day i → tick = i * 24 + 12
  return Array.from({ length: 30 }, (_, i) => ({
    tick: i * 24 + 12,
    extractedAmount: 5 + Math.sin(i * 0.4) * 2,
    efficiencyPercent: 0.8,
    reserveRemaining: 800 - i * 5,
  }))
}

/** Builds a plausible depletion forecast. */
function makeDepletionForecast(tick = 1030) {
  return {
    averageExtractionRatePerTick: 5.1,
    depletionTick: tick + 1568,   // ~65 game days
    critical5PctTick: tick + 1412, // ~59 game days
    critical20PctTick: tick + 980, // ~41 game days
    estimatedGameDaysRemaining: 65.3,
    currentReserve: 800,
    originalReserve: 1000,
  }
}

async function setupMineHistoryPage(
  page: Parameters<typeof setupMockApi>[0],
  opts: {
    records?: ReturnType<typeof makeExtractionRecords>
    forecast?: ReturnType<typeof makeDepletionForecast> | null
    lotMaterialQuantity?: number | null
    lotOriginalMaterialQuantity?: number | null
  } = {},
) {
  const companyId = 'company-hist'
  const building = makeMineBuilding({
    companyId,
    lotMaterialQuantity: opts.lotMaterialQuantity ?? 800,
    lotOriginalMaterialQuantity: opts.lotOriginalMaterialQuantity ?? 1000,
  })
  const player = makePlayer()
  player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
  player.companies.push({
    id: companyId,
    playerId: player.id,
    name: 'History Corp',
    cash: 500000,
    foundedAtUtc: '2026-01-01T00:00:00Z',
    buildings: [building],
  })

  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.mineExtractionRecords = opts.records ?? makeExtractionRecords()
  state.mineDepletionForecast = opts.forecast !== undefined ? opts.forecast : makeDepletionForecast()

  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto(`/building/${building.id}`)
  // Click the MINING unit cell to show the sidebar panel
  await page.locator('.grid-cell').first().click()
  return { player, building, state }
}

// ─────────────────────────────────────────────────────────────────────────────

test.describe('Mine Extraction History Panel', () => {
  test('sparkline chart is visible when extraction records exist', async ({ page }) => {
    await setupMineHistoryPage(page)

    // The sparkline SVG should be rendered
    await expect(page.locator('.sparkline-svg')).toBeVisible()
  })

  test('shows empty state when fewer than 5 extraction records exist', async ({ page }) => {
    await setupMineHistoryPage(page, {
      records: [
        { tick: 100, extractedAmount: 3, efficiencyPercent: 0.9, reserveRemaining: 997 },
      ],
    })

    const emptyMsg = page.locator('.extraction-history-empty')
    await expect(emptyMsg).toBeVisible()
  })

  test('"View Extraction History" button opens the dialog', async ({ page }) => {
    await setupMineHistoryPage(page)

    const btn = page.getByRole('button', { name: 'View Extraction History' })
    await expect(btn).toBeVisible()
    await btn.click()

    // Dialog should open
    const dialog = page.locator('.mine-history-dialog')
    await expect(dialog).toBeVisible()
  })

  test('dialog bar chart renders per-tick extraction bars', async ({ page }) => {
    await setupMineHistoryPage(page)

    await page.getByRole('button', { name: 'View Extraction History' }).click()
    const dialog = page.locator('.mine-history-dialog')
    await expect(dialog).toBeVisible()

    // Bar chart SVG rect elements should be present
    const bars = dialog.locator('svg rect')
    await expect(bars).not.toHaveCount(0)
  })

  test('depletion timeline shows milestone markers', async ({ page }) => {
    await setupMineHistoryPage(page)

    await page.getByRole('button', { name: 'View Extraction History' }).click()
    const dialog = page.locator('.mine-history-dialog')
    await expect(dialog).toBeVisible()

    // Wait for forecast to load (dialog fetches forecast in onMounted)
    // Timeline section should be visible
    const timeline = dialog.locator('.depletion-timeline')
    await expect(timeline).toBeVisible({ timeout: 5000 })

    // At least one milestone marker
    const milestones = dialog.locator('.depletion-milestone')
    await expect(milestones).not.toHaveCount(0)
  })

  test('"Find New Deposit" CTA is visible and navigates to /buy-building with MINE type', async ({
    page,
  }) => {
    await setupMineHistoryPage(page)

    await page.getByRole('button', { name: 'View Extraction History' }).click()
    const dialog = page.locator('.mine-history-dialog')
    await expect(dialog).toBeVisible()

    const cta = dialog.locator('.find-new-deposit-btn')
    await expect(cta).toBeVisible()
    await cta.click()

    // Should navigate to buy-building with type=MINE
    await expect(page).toHaveURL(/\/buy-building/)
    const url = page.url()
    expect(url).toContain('type=MINE')
  })

  test('dialog shows no-forecast message when forecast is null', async ({ page }) => {
    await setupMineHistoryPage(page, { forecast: null })

    await page.getByRole('button', { name: 'View Extraction History' }).click()
    const dialog = page.locator('.mine-history-dialog')
    await expect(dialog).toBeVisible()

    await expect(dialog.getByText('Not enough extraction data for forecast yet.')).toBeVisible()
  })

  test('dialog close button closes the dialog', async ({ page }) => {
    await setupMineHistoryPage(page)

    await page.getByRole('button', { name: 'View Extraction History' }).click()
    const dialog = page.locator('.mine-history-dialog')
    await expect(dialog).toBeVisible()

    await dialog.getByRole('button', { name: 'Close' }).click()
    await expect(dialog).toBeHidden()
  })
})

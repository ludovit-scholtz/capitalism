import { expect, test } from '@playwright/test'
import { makePlayer, setupMockApi } from '../../helpers/mock-api'

const COMPANY_ID = 'company-ledger-test'

function makePlayerWithLedger() {
  const player = makePlayer({
    onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
    companies: [
      {
        id: COMPANY_ID,
        playerId: 'player-ledger',
        name: 'Ledger Test Corp',
        cash: 850000,
        foundedAtUtc: '2026-01-01T00:00:00Z',
        buildings: [
          {
            id: 'building-factory-1',
            companyId: COMPANY_ID,
            cityId: 'city-ba',
            type: 'FACTORY',
            name: 'Main Factory',
            latitude: 48.15,
            longitude: 17.11,
            level: 1,
            powerConsumption: 2,
            isForSale: false,
            builtAtUtc: '2026-01-01T00:00:00Z',
            pendingConfiguration: null,
            units: [],
          },
          {
            id: 'building-shop-1',
            companyId: COMPANY_ID,
            cityId: 'city-ba',
            type: 'SALES_SHOP',
            name: 'Downtown Shop',
            latitude: 48.14,
            longitude: 17.1,
            level: 1,
            powerConsumption: 1,
            isForSale: false,
            builtAtUtc: '2026-01-01T00:00:00Z',
            pendingConfiguration: null,
            units: [],
          },
        ],
      },
    ],
  })
  return player
}

function seedLedgerData(state: ReturnType<typeof setupMockApi>, companyId: string) {
  state.ledgerData[companyId] = {
    companyId,
    companyName: 'Ledger Test Corp',
    gameYear: 1,
    isCurrentGameYear: true,
    currentCash: 850000,
    primaryCurrencyCode: 'EUR',
    primaryCurrencySymbol: '€',
    hasMixedCurrencies: false,
    totalRevenue: 120000,
    totalMediaHouseIncome: 0,
    totalPurchasingCosts: 30000,
    totalShippingCosts: 5000,
    totalLaborCosts: 20000,
    totalEnergyCosts: 8000,
    totalMarketingCosts: 5000,
    totalTaxPaid: 12000,
    totalOtherCosts: 0,
    taxableIncome: 52000,
    estimatedIncomeTax: 10400,
    netIncome: 40000,
    propertyValue: 200000,
    propertyAppreciation: 5000,
    buildingValue: 350000,
    inventoryValue: 25000,
    totalAssets: 1425000,
    totalPropertyPurchases: 200000,
    totalStockPurchaseCashOut: 0,
    totalStockSaleCashIn: 0,
    cashFromOperations: 40000,
    cashFromInvestments: -200000,
    firstRecordedTick: 1,
    lastRecordedTick: 100,
    incomeTaxDueAtTick: 8760,
    incomeTaxDueGameTimeUtc: '2027-01-01T00:00:00Z',
    incomeTaxDueGameYear: 2,
    isIncomeTaxSettled: false,
    history: [
      {
        gameYear: 1,
        isCurrentGameYear: true,
        totalRevenue: 120000,
        totalLaborCosts: 20000,
        totalEnergyCosts: 8000,
        netIncome: 40000,
        totalTaxPaid: 12000,
        taxableIncome: 52000,
        estimatedIncomeTax: 10400,
        firstRecordedTick: 1,
        lastRecordedTick: 100,
      },
    ],
    buildingSummaries: [
      {
        buildingId: 'building-factory-1',
        buildingName: 'Main Factory',
        buildingType: 'FACTORY',
        revenue: 80000,
        costs: 40000,
        currencyCode: 'EUR',
        currencySymbol: '€',
      },
      {
        buildingId: 'building-shop-1',
        buildingName: 'Downtown Shop',
        buildingType: 'SALES_SHOP',
        revenue: 40000,
        costs: 10000,
        currencyCode: 'EUR',
        currencySymbol: '€',
      },
    ],
  }
}

test('ledger shows Income Statement with revenue and expense line items', async ({ page }) => {
  const player = makePlayerWithLedger()
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.gameState.currentTick = 100

  seedLedgerData(state, COMPANY_ID)

  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto(`/ledger/${COMPANY_ID}`)

  await expect(page.getByRole('heading', { name: 'Income Statement' })).toBeVisible()
  await expect(page.locator('.statement-row').first()).toBeVisible()
  await expect(page.locator('.kpi-row')).toBeVisible()

  // Net income KPI card should be present
  await expect(page.locator('.kpi-label').filter({ hasText: 'Net Income' })).toBeVisible()
})

test('ledger shows Balance Sheet section', async ({ page }) => {
  const player = makePlayerWithLedger()
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.gameState.currentTick = 100

  seedLedgerData(state, COMPANY_ID)

  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto(`/ledger/${COMPANY_ID}`)

  await expect(page.getByRole('heading', { name: 'Balance Sheet' })).toBeVisible()
  await expect(page.locator('.statement-row').filter({ hasText: 'Total Assets' })).toBeVisible()
})

test('ledger shows Cash Flow Statement section', async ({ page }) => {
  const player = makePlayerWithLedger()
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.gameState.currentTick = 100

  seedLedgerData(state, COMPANY_ID)

  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto(`/ledger/${COMPANY_ID}`)

  await expect(page.getByRole('heading', { name: 'Cash Flow Statement' })).toBeVisible()
  await expect(page.locator('.statement-row').filter({ hasText: 'From Operations' })).toBeVisible()
})

test('ledger shows Income Tax Schedule banner', async ({ page }) => {
  const player = makePlayerWithLedger()
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.gameState.currentTick = 100

  seedLedgerData(state, COMPANY_ID)

  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto(`/ledger/${COMPANY_ID}`)

  await expect(page.getByRole('heading', { name: 'Income Tax Schedule' })).toBeVisible()
})

test('ledger drilldown shows entries when line item is clicked', async ({ page }) => {
  const player = makePlayerWithLedger()
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.gameState.currentTick = 100

  seedLedgerData(state, COMPANY_ID)

  // Seed drilldown data
  state.drillDownData[`${COMPANY_ID}:REVENUE`] = [
    {
      id: 'entry-rev-1',
      category: 'REVENUE',
      description: 'Wooden Chair sale',
      amount: 45000,
      recordedAtTick: 50,
      buildingId: 'building-shop-1',
      buildingName: 'Downtown Shop',
      buildingUnitId: 'unit-ps-1',
      productTypeId: 'product-chair',
      productName: 'Wooden Chair',
      resourceTypeId: null,
      resourceName: null,
      currencyCode: 'EUR',
      currencySymbol: '€',
      eventTag: null,
      eventDescription: null,
    },
  ]

  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto(`/ledger/${COMPANY_ID}`)

  // Click the drill-down button on the Revenue row
  const drillBtn = page.locator('.drill-btn').first()
  await expect(drillBtn).toBeVisible()
  await drillBtn.click()

  // Drilldown panel should appear
  await expect(page.locator('.drill-panel')).toBeVisible()
})

test('ledger shows Buildings Performance table with building rows', async ({ page }) => {
  const player = makePlayerWithLedger()
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.gameState.currentTick = 100

  seedLedgerData(state, COMPANY_ID)

  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto(`/ledger/${COMPANY_ID}`)

  await expect(page.getByRole('heading', { name: 'Buildings Performance' })).toBeVisible()
  await expect(page.locator('.buildings-table tbody tr')).toHaveCount(2)
  await expect(page.locator('.buildings-table')).toContainText('Main Factory')
  await expect(page.locator('.buildings-table')).toContainText('Downtown Shop')
})

test('ledger shows historical year in Ledger History', async ({ page }) => {
  const player = makePlayerWithLedger()
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.gameState.currentTick = 9000 // game year 2

  // Seed with history for year 1
  state.ledgerData[COMPANY_ID] = {
    companyId: COMPANY_ID,
    companyName: 'Ledger Test Corp',
    gameYear: 2,
    isCurrentGameYear: true,
    currentCash: 900000,
    primaryCurrencyCode: 'EUR',
    primaryCurrencySymbol: '€',
    hasMixedCurrencies: false,
    totalRevenue: 80000,
    totalMediaHouseIncome: 0,
    totalPurchasingCosts: 20000,
    totalShippingCosts: 3000,
    totalLaborCosts: 12000,
    totalEnergyCosts: 5000,
    totalMarketingCosts: 3000,
    totalTaxPaid: 8000,
    totalOtherCosts: 0,
    taxableIncome: 37000,
    estimatedIncomeTax: 7400,
    netIncome: 29000,
    propertyValue: 200000,
    propertyAppreciation: 5000,
    buildingValue: 350000,
    inventoryValue: 20000,
    totalAssets: 1470000,
    totalPropertyPurchases: 0,
    totalStockPurchaseCashOut: 0,
    totalStockSaleCashIn: 0,
    cashFromOperations: 29000,
    cashFromInvestments: 0,
    firstRecordedTick: 8761,
    lastRecordedTick: 9000,
    incomeTaxDueAtTick: 17520,
    incomeTaxDueGameTimeUtc: '2028-01-01T00:00:00Z',
    incomeTaxDueGameYear: 3,
    isIncomeTaxSettled: false,
    history: [
      {
        gameYear: 1,
        isCurrentGameYear: false,
        totalRevenue: 120000,
        totalLaborCosts: 20000,
        totalEnergyCosts: 8000,
        netIncome: 40000,
        totalTaxPaid: 12000,
        taxableIncome: 52000,
        estimatedIncomeTax: 10400,
        firstRecordedTick: 1,
        lastRecordedTick: 8760,
      },
      {
        gameYear: 2,
        isCurrentGameYear: true,
        totalRevenue: 80000,
        totalLaborCosts: 12000,
        totalEnergyCosts: 5000,
        netIncome: 29000,
        totalTaxPaid: 8000,
        taxableIncome: 37000,
        estimatedIncomeTax: 7400,
        firstRecordedTick: 8761,
        lastRecordedTick: 9000,
      },
    ],
    buildingSummaries: [],
  }

  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto(`/ledger/${COMPANY_ID}`)

  await expect(page.getByRole('heading', { name: 'Ledger History' })).toBeVisible()
  // History buttons should show year 1
  await expect(page.locator('.history-buttons')).toContainText('Year 1')
})

test('ledger shows Race to the Top panel linking to personal ledger', async ({ page }) => {
  const player = makePlayerWithLedger()
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.gameState.currentTick = 100

  seedLedgerData(state, COMPANY_ID)

  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto(`/ledger/${COMPANY_ID}`)

  const racePanel = page.locator('.race-panel')
  await expect(racePanel).toBeVisible()
  await expect(racePanel).toContainText('Race to the Top')
  await expect(racePanel.getByRole('link')).toBeVisible()
})

test('ledger is accessible via /company/:id/ledger alias route', async ({ page }) => {
  const player = makePlayerWithLedger()
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.gameState.currentTick = 100

  seedLedgerData(state, COMPANY_ID)

  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto(`/company/${COMPANY_ID}/ledger`)

  await expect(page.getByRole('heading', { name: 'Income Statement' })).toBeVisible()
})

test('ledger shows company name in page header', async ({ page }) => {
  const player = makePlayerWithLedger()
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.gameState.currentTick = 100

  seedLedgerData(state, COMPANY_ID)

  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto(`/ledger/${COMPANY_ID}`)

  await expect(page.locator('.ledger-title')).toContainText('Ledger Test Corp')
})

test('ledger shows error state for unknown company', async ({ page }) => {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`

  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto('/ledger/non-existent-company')

  // Should show error or not-found state
  await expect(page.locator('.state-box')).toBeVisible()
})

test('company settings page has View Ledger link', async ({ page }) => {
  const player = makePlayerWithLedger()
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`

  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto(`/company/${COMPANY_ID}/settings`)

  await expect(page.getByRole('link', { name: /View Ledger/i })).toBeVisible()
})

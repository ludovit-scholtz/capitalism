import { expect, test } from '@playwright/test'
import { makePlayer, setupMockApi } from '../../helpers/mock-api'

test('ledger shows logistics timeline and city financial breakdown', async ({ page }) => {
  const companyId = 'company-ledger-logistics'
  const player = makePlayer({
    onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
    companies: [
      {
        id: companyId,
        playerId: 'player-1',
        name: 'Logistics Co',
        cash: 500000,
        foundedAtUtc: '2026-01-01T00:00:00Z',
        buildings: [
          {
            id: 'building-ba-factory',
            companyId,
            cityId: 'city-ba',
            type: 'FACTORY',
            name: 'Bratislava Factory',
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
            id: 'building-prague-shop',
            companyId,
            cityId: 'city-pr',
            type: 'SALES_SHOP',
            name: 'Prague Shop',
            latitude: 50.08,
            longitude: 14.43,
            level: 1,
            powerConsumption: 2,
            isForSale: false,
            builtAtUtc: '2026-01-01T00:00:00Z',
            pendingConfiguration: null,
            units: [],
          },
        ],
      },
    ],
  })

  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.gameState.currentTick = 44
  state.ledgerData[companyId] = {
    companyId,
    companyName: 'Logistics Co',
    currentCash: 500000,
    totalRevenue: 1600,
    totalPurchasingCosts: 200,
    totalShippingCosts: 150,
    totalLaborCosts: 120,
    totalEnergyCosts: 80,
    totalMarketingCosts: 40,
    totalTaxPaid: 0,
    totalOtherCosts: 0,
    netIncome: 1010,
    propertyValue: 0,
    propertyAppreciation: 0,
    buildingValue: 0,
    inventoryValue: 0,
    totalAssets: 500000,
    totalPropertyPurchases: 0,
    cashFromOperations: 1010,
    cashFromInvestments: 0,
    firstRecordedTick: 1,
    lastRecordedTick: 44,
    buildingSummaries: [
      {
        buildingId: 'building-ba-factory',
        buildingName: 'Bratislava Factory',
        buildingType: 'FACTORY',
        revenue: 1000,
        costs: 300,
        currencyCode: 'EUR',
      },
      {
        buildingId: 'building-prague-shop',
        buildingName: 'Prague Shop',
        buildingType: 'SALES_SHOP',
        revenue: 600,
        costs: 200,
        currencyCode: 'CZK',
      },
    ],
  }
  state.tradeRoutes = [
    {
      id: 'route-ledger-1',
      companyId,
      sourceBuildingId: 'building-ba-factory',
      sourceBuildingName: 'Bratislava Factory',
      sourceCityName: 'Bratislava',
      sourceCurrencyCode: 'EUR',
      destinationBuildingId: 'building-prague-shop',
      destinationBuildingName: 'Prague Shop',
      destinationCityName: 'Prague',
      destinationCurrencyCode: 'CZK',
      productTypeId: null,
      productTypeName: null,
      resourceTypeId: 'res-wood',
      resourceTypeName: 'Wood',
      quantity: 50,
      quality: 0.8,
      pricePerUnit: 15,
      scheduledDepartureTick: 40,
      expectedArrivalTick: 46,
      transitTicks: 6,
      shippingCostEstimate: 120,
      shippingCostActual: 0,
      status: 'IN_TRANSIT',
      failureReason: null,
      createdAtUtc: new Date().toISOString(),
      departedAtUtc: new Date().toISOString(),
      completedAtUtc: null,
    },
  ]

  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto(`/ledger/${companyId}`)

  await expect(page.getByRole('heading', { name: 'Logistics Timeline' })).toBeVisible()
  await expect(page.locator('.logistics-table')).toContainText('Bratislava')
  await expect(page.locator('.logistics-table')).toContainText('Prague')
  await expect(page.locator('.shipment-progress-label')).toContainText(/On schedule|In transit/)

  await expect(page.getByRole('heading', { name: 'City Financial Breakdown' })).toBeVisible()
  await expect(page.locator('.city-financial-card')).toHaveCount(2)
  await expect(page.locator('.city-financial-card').filter({ hasText: 'Bratislava' })).toHaveCount(1)
  await expect(page.locator('.city-financial-card').filter({ hasText: 'Prague' })).toHaveCount(1)
})

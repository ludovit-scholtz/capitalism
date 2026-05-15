import { expect, test } from '@playwright/test'
import { makePlayer, setupMockApi } from '../../helpers/mock-api'

function makePlayerWithCompany() {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  player.companies = [
    {
      id: 'company-contracts-1',
      playerId: player.id,
      name: 'Contracts Corp',
      cash: 150000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [
        {
          id: 'building-contracts-factory',
          companyId: 'company-contracts-1',
          cityId: 'city-ba',
          type: 'FACTORY',
          name: 'Contract Factory',
          latitude: 48.1486,
          longitude: 17.1077,
          level: 1,
          powerConsumption: 10,
          isForSale: false,
          units: [],
          pendingConfiguration: null,
        },
      ],
    },
  ]
  player.activeAccountType = 'COMPANY'
  player.activeCompanyId = 'company-contracts-1'
  return player
}

test.describe('Government contracts', () => {
  test('city contracts tab renders open contract cards', async ({ page }) => {
    const player = makePlayerWithCompany()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.gameState.currentTick = 100
    state.governmentContracts = [
      {
        id: 'contract-open-1',
        cityId: 'city-ba',
        cityName: 'Bratislava',
        currencyCode: 'EUR',
        title: 'Hospital medicine supply',
        description: 'Deliver medicine packs for city clinics.',
        productTypeId: 'prod-medicine',
        productName: 'Basic Medicine',
        quantityRequired: 300,
        minimumQuality: 5.5,
        budgetCap: 95,
        deadlineTick: 160,
        status: 'OPEN',
        winnerCompanyId: null,
        winnerCompanyName: null,
        createdAtTick: 95,
        bidCount: 0,
        awardedBidPricePerUnit: null,
        fulfilledQuantity: 0,
        fulfillmentPercent: 0,
      },
    ]

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/city/city-ba/contracts')
    await expect(page.getByRole('heading', { name: 'Government Tenders' })).toBeVisible()
    await expect(page.getByText('Hospital medicine supply')).toBeVisible()
    await expect(page.getByText('Basic Medicine')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Place Bid' })).toBeVisible()
  })

  test('bid modal shows quality eligibility and submits bid', async ({ page }) => {
    const player = makePlayerWithCompany()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.gameState.currentTick = 100
    state.governmentContracts = [
      {
        id: 'contract-open-1',
        cityId: 'city-ba',
        cityName: 'Bratislava',
        currencyCode: 'EUR',
        title: 'Hospital medicine supply',
        description: 'Deliver medicine packs for city clinics.',
        productTypeId: 'prod-medicine',
        productName: 'Basic Medicine',
        quantityRequired: 300,
        minimumQuality: 5.5,
        budgetCap: 95,
        deadlineTick: 160,
        status: 'OPEN',
        winnerCompanyId: null,
        winnerCompanyName: null,
        createdAtTick: 95,
        bidCount: 0,
        awardedBidPricePerUnit: null,
        fulfilledQuantity: 0,
        fulfillmentPercent: 0,
      },
    ]

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/city/city-ba/contracts')
    await page.getByRole('button', { name: 'Place Bid' }).click()
    await expect(page.getByText('Submit contract bid')).toBeVisible()
    await expect(page.getByText('Quality requirement met')).toBeVisible()
    await page.getByLabel('Bid price per unit').fill('90')
    await page.getByRole('button', { name: 'Submit bid' }).click()
    await expect(page.getByText('Submit contract bid')).toHaveCount(0)
  })

  test('company contracts page shows bid in bidding column', async ({ page }) => {
    const player = makePlayerWithCompany()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    const company = player.companies[0]!
    state.governmentContracts = [
      {
        id: 'contract-open-1',
        cityId: 'city-ba',
        cityName: 'Bratislava',
        currencyCode: 'EUR',
        title: 'Hospital medicine supply',
        description: 'Deliver medicine packs for city clinics.',
        productTypeId: 'prod-medicine',
        productName: 'Basic Medicine',
        quantityRequired: 300,
        minimumQuality: 5.5,
        budgetCap: 95,
        deadlineTick: 160,
        status: 'OPEN',
        winnerCompanyId: null,
        winnerCompanyName: null,
        createdAtTick: 95,
        bidCount: 1,
        awardedBidPricePerUnit: null,
        fulfilledQuantity: 0,
        fulfillmentPercent: 0,
      },
    ]
    state.contractBids = [
      {
        id: 'bid-1',
        contractId: 'contract-open-1',
        companyId: company.id,
        companyName: company.name,
        bidPricePerUnit: 90,
        estimatedDeliveryTick: 150,
        submittedAtTick: 101,
        contractStatus: 'OPEN',
      },
    ]

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/company/${company.id}/contracts`)
    await expect(page.getByRole('heading', { name: 'Bidding' })).toBeVisible()
    await expect(page.getByText('Hospital medicine supply')).toBeVisible()
  })

  test('awarded contract appears in awarded column', async ({ page }) => {
    const player = makePlayerWithCompany()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    const company = player.companies[0]!
    state.governmentContracts = [
      {
        id: 'contract-awarded-1',
        cityId: 'city-ba',
        cityName: 'Bratislava',
        currencyCode: 'EUR',
        title: 'School furniture tender',
        description: 'Deliver desks and chairs.',
        productTypeId: 'prod-chair',
        productName: 'Wooden Chair',
        quantityRequired: 200,
        minimumQuality: 4.5,
        budgetCap: 120,
        deadlineTick: 180,
        status: 'AWARDED',
        winnerCompanyId: company.id,
        winnerCompanyName: company.name,
        createdAtTick: 90,
        bidCount: 2,
        awardedBidPricePerUnit: 110,
        fulfilledQuantity: 0,
        fulfillmentPercent: 0,
      },
    ]

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/company/${company.id}/contracts`)
    await expect(page.getByRole('heading', { name: 'Awarded' })).toBeVisible()
    await expect(page.getByText('School furniture tender')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Ship' })).toBeVisible()
  })

  test('fulfillment moves contract to completed column', async ({ page }) => {
    const player = makePlayerWithCompany()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    const company = player.companies[0]!
    state.governmentContracts = [
      {
        id: 'contract-awarded-1',
        cityId: 'city-ba',
        cityName: 'Bratislava',
        currencyCode: 'EUR',
        title: 'School furniture tender',
        description: 'Deliver desks and chairs.',
        productTypeId: 'prod-chair',
        productName: 'Wooden Chair',
        quantityRequired: 200,
        minimumQuality: 4.5,
        budgetCap: 120,
        deadlineTick: 180,
        status: 'AWARDED',
        winnerCompanyId: company.id,
        winnerCompanyName: company.name,
        createdAtTick: 90,
        bidCount: 2,
        awardedBidPricePerUnit: 110,
        fulfilledQuantity: 150,
        fulfillmentPercent: 75,
      },
    ]

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/company/${company.id}/contracts`)
    await page.locator('.kanban-column').filter({ hasText: 'In Fulfillment' }).getByRole('spinbutton').fill('50')
    await page.locator('.kanban-column').filter({ hasText: 'In Fulfillment' }).getByRole('button', { name: 'Ship' }).click()
    await expect(page.locator('.kanban-column').filter({ hasText: 'Completed' }).getByText('School furniture tender')).toBeVisible()
  })

  test('ledger shows government contract revenue category entries', async ({ page }) => {
    const player = makePlayerWithCompany()
    const company = player.companies[0]!
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.ledgerData[company.id] = {
      companyId: company.id,
      companyName: company.name,
      currentCash: 70000,
      primaryCurrencyCode: 'EUR',
      primaryCurrencySymbol: '€',
      totalRevenue: 25000,
      totalGovernmentContractRevenue: 22000,
      totalPurchasingCosts: 8000,
      totalLaborCosts: 4000,
      totalEnergyCosts: 1200,
      totalMarketingCosts: 600,
      totalTaxPaid: 0,
      totalOtherCosts: 0,
      netIncome: 11200,
      propertyValue: 0,
      propertyAppreciation: 0,
      buildingValue: 0,
      inventoryValue: 0,
      totalAssets: 70000,
      totalPropertyPurchases: 0,
      cashFromOperations: 11200,
      cashFromInvestments: 0,
      firstRecordedTick: 1,
      lastRecordedTick: 120,
      buildingSummaries: [],
    }
    state.drillDownData[`${company.id}:GOVERNMENT_CONTRACT_REVENUE`] = [
      {
        id: 'ledger-ctr-1',
        category: 'GOVERNMENT_CONTRACT_REVENUE',
        description: 'Government contract fulfilled: School furniture tender',
        amount: 22000,
        recordedAtTick: 120,
        buildingId: null,
        buildingName: null,
        buildingUnitId: null,
        productTypeId: null,
        productName: null,
        resourceTypeId: null,
        resourceName: null,
      },
    ]

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/ledger/${company.id}`)
    await page.getByRole('button', { name: 'Detail: Government Contract Revenue' }).click()
    await expect(page.getByText('Government contract fulfilled: School furniture tender')).toBeVisible()
  })
})

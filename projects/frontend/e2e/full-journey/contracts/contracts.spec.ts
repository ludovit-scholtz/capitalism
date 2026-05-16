import { expect, test } from '@playwright/test'
import { makePlayer, setupMockApi } from '../../helpers/mock-api'

test.describe('Supply contracts page', () => {
  test('shows empty state for authenticated player without contracts', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/contracts')
    await expect(page.getByRole('heading', { name: 'Supply Contracts' })).toBeVisible()
    await expect(page.getByText('No pending offers.')).toBeVisible()
    await expect(page.getByText('No active contracts.')).toBeVisible()
  })

  test('renders active contract with delivery health badge', async ({ page }) => {
    const seller = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const buyer = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    seller.companies = [
      {
        id: 'company-seller',
        playerId: seller.id,
        name: 'Seller Co',
        cash: 120000,
        foundedAtUtc: '2026-01-01T00:00:00Z',
        buildings: [],
      },
    ]
    buyer.companies = [
      {
        id: 'company-buyer',
        playerId: buyer.id,
        name: 'Buyer Co',
        cash: 120000,
        foundedAtUtc: '2026-01-01T00:00:00Z',
        buildings: [],
      },
    ]
    const sellerCompany = seller.companies[0]!
    const buyerCompany = buyer.companies[0]!
    const state = setupMockApi(page, { players: [seller, buyer] })
    state.currentUserId = seller.id
    state.currentToken = `token-${seller.id}`
    state.supplyContracts = [
      {
        id: 'supply-contract-1',
        sellerCompanyId: sellerCompany.id,
        sellerCompanyName: sellerCompany.name,
        buyerCompanyId: buyerCompany.id,
        buyerCompanyName: buyerCompany.name,
        sellerBuildingUnitId: 'unit-b2b-1',
        resourceTypeId: 'res-wood',
        resourceTypeName: 'Wood',
        productTypeId: null,
        productTypeName: null,
        quantityPerTick: 500,
        pricePerUnit: 12,
        durationTicks: 100,
        remainingTicks: 90,
        startTick: 10,
        penaltyRatePercent: 10,
        currencyCode: 'EUR',
        status: 'ACTIVE',
        createdAtTick: 5,
        totalDeliveredQuantity: 5000,
        totalUndeliveredQuantity: 100,
        totalPenaltyAmount: 300,
        penaltyCount: 1,
      },
    ]

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${seller.id}`)

    await page.goto('/contracts')
    await expect(page.getByRole('heading', { name: 'Active contracts' })).toBeVisible()
    await expect(page.getByText('Wood')).toBeVisible()
    await expect(page.getByText('Penalties applied')).toBeVisible()
  })
})

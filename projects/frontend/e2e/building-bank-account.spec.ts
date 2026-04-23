import { expect, test } from '@playwright/test'
import { makeDefaultCities, makeDefaultProducts, makeDefaultResources, makePlayer, setupMockApi } from './helpers/mock-api'

// ── Helpers ──────────────────────────────────────────────────────────────────

function makeTestCompanyWithBuilding(
  playerId: string,
  companyId: string,
  buildingId: string,
  overrides?: {
    isSuspendedForFunds?: boolean
    suspendedReason?: string | null
    companyCash?: number
  },
) {
  return {
    id: companyId,
    playerId,
    name: 'Bank Account Test Co',
    cash: overrides?.companyCash ?? 500_000,
    foundedAtUtc: '2026-01-01T00:00:00Z',
    buildings: [
      {
        id: buildingId,
        companyId,
        cityId: 'city-ba',
        type: 'FACTORY',
        name: 'Bank Test Factory',
        latitude: 48.15,
        longitude: 17.11,
        level: 1,
        powerConsumption: 2,
        isForSale: false,
        builtAtUtc: '2026-01-01T00:00:00Z',
        powerStatus: 'POWERED',
        isUnderConstruction: false,
        constructionCompletesAtTick: null,
        constructionCost: 0,
        contentValue: 0,
        contentBudgetPerTick: null,
        isSuspendedForFunds: overrides?.isSuspendedForFunds ?? false,
        suspendedReason: overrides?.suspendedReason ?? null,
        units: [],
        pendingConfiguration: null,
      },
    ],
  }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

test.describe('Building bank account panel', () => {
  test('shows no-account state when building has no bank account', async ({ page }) => {
    const player = makePlayer()
    const companyId = 'company-bba-no-acct'
    const buildingId = 'building-bba-no-acct'

    player.companies.push(makeTestCompanyWithBuilding(player.id, companyId, buildingId))

    const state = setupMockApi(page, {
      players: [player],
      cities: makeDefaultCities(),
      resourceTypes: makeDefaultResources(),
      productTypes: makeDefaultProducts(),
    })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/building/${buildingId}`)

    // When building has no units, the overview is shown automatically.
    await expect(page.getByRole('heading', { name: 'Building Overview' })).toBeVisible({ timeout: 10000 })

    // Bank Account panel heading should appear
    await expect(page.locator('.building-bank-account-card').getByRole('heading', { name: 'Bank Account' })).toBeVisible()

    // No account message
    await expect(page.locator('.building-bank-account-panel')).toContainText(/no bank account assigned/i)
  })

  test('shows account number and balance when building has a bank account', async ({ page }) => {
    const player = makePlayer()
    const companyId = 'company-bba-has-acct'
    const buildingId = 'building-bba-has-acct'

    player.companies.push(makeTestCompanyWithBuilding(player.id, companyId, buildingId))

    const state = setupMockApi(page, {
      players: [player],
      cities: makeDefaultCities(),
      resourceTypes: makeDefaultResources(),
      productTypes: makeDefaultProducts(),
    })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    // Pre-seed a bank account for this building
    state.buildingBankAccounts[buildingId] = {
      hasBankAccount: true,
      bankAccountId: 'acc-bba-has-1',
      accountNumber: '1234567890123456',
      balance: 25_000,
      isSuspendedForFunds: false,
      suspendedReason: null,
      currencyCode: 'EUR',
    }

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/building/${buildingId}`)

    await expect(page.getByRole('heading', { name: 'Building Overview' })).toBeVisible({ timeout: 10000 })
    await expect(page.locator('.building-bank-account-card').getByRole('heading', { name: 'Bank Account' })).toBeVisible()

    // Account number should be visible
    await expect(page.locator('.bba-account-number code')).toContainText('1234567890123456')
  })

  test('shows insufficient-funds danger alert with guidance links', async ({ page }) => {
    const player = makePlayer()
    const companyId = 'company-bba-suspended'
    const buildingId = 'building-bba-suspended'

    player.companies.push(
      makeTestCompanyWithBuilding(player.id, companyId, buildingId, {
        isSuspendedForFunds: true,
        suspendedReason: 'INSUFFICIENT_FUNDS:150.00',
        companyCash: 0,
      }),
    )

    const state = setupMockApi(page, {
      players: [player],
      cities: makeDefaultCities(),
      resourceTypes: makeDefaultResources(),
      productTypes: makeDefaultProducts(),
    })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    // Bank account seeded with zero balance
    state.buildingBankAccounts[buildingId] = {
      hasBankAccount: true,
      bankAccountId: 'acc-bba-suspended',
      accountNumber: '9876543210987654',
      balance: 0,
      isSuspendedForFunds: true,
      suspendedReason: 'INSUFFICIENT_FUNDS:150.00',
      currencyCode: 'EUR',
    }

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/building/${buildingId}`)

    await expect(page.getByRole('heading', { name: 'Building Overview' })).toBeVisible({ timeout: 10000 })

    // Danger alert visible
    await expect(page.locator('.bba-alert-danger')).toBeVisible()
    await expect(page.locator('.bba-alert-danger')).toContainText(/suspended/i)

    // Guidance links visible
    await expect(page.getByRole('link', { name: /convert currency/i })).toBeVisible()
    await expect(page.getByRole('link', { name: /bank management/i })).toBeVisible()
  })

  test('fund account transfer succeeds and shows success message', async ({ page }) => {
    const player = makePlayer()
    const companyId = 'company-bba-fund'
    const buildingId = 'building-bba-fund'

    player.companies.push(
      makeTestCompanyWithBuilding(player.id, companyId, buildingId, {
        isSuspendedForFunds: true,
        suspendedReason: 'INSUFFICIENT_FUNDS:150.00',
        companyCash: 500_000,
      }),
    )

    const state = setupMockApi(page, {
      players: [player],
      cities: makeDefaultCities(),
      resourceTypes: makeDefaultResources(),
      productTypes: makeDefaultProducts(),
    })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    state.buildingBankAccounts[buildingId] = {
      hasBankAccount: true,
      bankAccountId: 'acc-bba-fund',
      accountNumber: '1111222233334444',
      balance: 0,
      isSuspendedForFunds: true,
      suspendedReason: 'INSUFFICIENT_FUNDS:150.00',
      currencyCode: 'EUR',
    }

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/building/${buildingId}`)

    await expect(page.getByRole('heading', { name: 'Building Overview' })).toBeVisible({ timeout: 10000 })
    await expect(page.locator('.building-bank-account-panel')).toBeVisible()

    // Expand the Fund Account details panel
    await page.locator('.bba-fund-summary').click()

    // Fill in an amount and submit
    const amountInput = page.locator('.bba-fund-input')
    await expect(amountInput).toBeVisible()
    await amountInput.fill('10000')

    await page.getByRole('button', { name: /transfer/i }).click()

    // Success message should appear
    await expect(page.locator('.bba-fund-success')).toBeVisible({ timeout: 5000 })
    await expect(page.locator('.bba-fund-success')).toContainText(/successful/i)
  })
})

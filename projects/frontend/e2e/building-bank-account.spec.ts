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

  test('assigns an existing company bank account to a building', async ({ page }) => {
    const player = makePlayer()
    const companyId = 'company-bba-assign'
    const buildingId = 'building-bba-assign'

    player.companies.push(makeTestCompanyWithBuilding(player.id, companyId, buildingId))

    const state = setupMockApi(page, {
      players: [player],
      cities: makeDefaultCities(),
      resourceTypes: makeDefaultResources(),
      productTypes: makeDefaultProducts(),
    })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.myBankAccounts = [
      {
        id: 'acc-company-eur',
        accountNumber: '2222333344445555',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 32000,
        companyId,
        companyName: 'Bank Account Test Co',
      },
    ]

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/building/${buildingId}`)

    await expect(page.getByRole('heading', { name: 'Building Overview' })).toBeVisible({ timeout: 10000 })
    await expect(page.locator('.building-bank-account-panel')).toContainText(/no bank account assigned/i)
    await expect(page.locator('.bba-account-select')).toHaveValue('acc-company-eur')

    await page.getByRole('button', { name: /assign account/i }).click()

    await expect(page.locator('.bba-manage-success')).toContainText(/assignment updated/i)
    await expect(page.locator('.bba-account-number code')).toContainText('2222333344445555')
  })

  test('creates and assigns a company bank account when no matching currency account exists', async ({ page }) => {
    const player = makePlayer()
    const companyId = 'company-bba-create-assign'
    const buildingId = 'building-bba-create-assign'

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

    await expect(page.getByRole('heading', { name: 'Building Overview' })).toBeVisible({ timeout: 10000 })
    await expect(page.locator('.building-bank-account-panel')).toContainText(/no bank account assigned/i)
    await expect(page.getByRole('button', { name: /create eur account and assign/i })).toBeVisible()

    await page.getByRole('button', { name: /create eur account and assign/i }).click()

    await expect(page.locator('.bba-manage-success')).toContainText(/created and assigned/i)
    await expect(page.locator('.bba-account-number code')).toBeVisible()
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

    const panel = page.locator('.building-bank-account-panel')
    const fundPanel = panel.locator('.bba-fund-panel')

    // Expand the Fund Account details panel
    await fundPanel.locator('.bba-fund-summary').click()

    // Fill in an amount and submit
    const amountInput = fundPanel.locator('.bba-fund-input')
    await expect(amountInput).toBeVisible()
    await amountInput.fill('10000')

    await fundPanel.getByRole('button', { name: /^transfer$/i }).click()

    await expect(fundPanel.locator('.bba-fund-success')).toBeVisible()
    await expect(fundPanel.locator('.bba-fund-success')).toContainText(/successful/i)
  })

  test('suspension alert clears after funding the building bank account', async ({ page }) => {
    // Full player flow: building suspended → danger alert visible →
    // player funds the account → suspension alert disappears (recovery).
    const player = makePlayer()
    const companyId = 'company-bba-recovery'
    const buildingId = 'building-bba-recovery'

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
      bankAccountId: 'acc-bba-recovery',
      accountNumber: '5555666677778888',
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

    // Step 1: Verify the suspension alert is visible with insufficient-funds reason.
    await expect(page.locator('.bba-alert-danger')).toBeVisible()
    await expect(page.locator('.bba-alert-danger')).toContainText(/suspended/i)

    // Step 2: Fund the account using the panel.
    const panel = page.locator('.building-bank-account-panel')
    const fundPanel = panel.locator('.bba-fund-panel')
    await fundPanel.locator('.bba-fund-summary').click()

    const amountInput = fundPanel.locator('.bba-fund-input')
    await expect(amountInput).toBeVisible()
    await amountInput.fill('50000')
    await fundPanel.getByRole('button', { name: /^transfer$/i }).click()

    // Step 3: Funding should succeed.
    await expect(fundPanel.locator('.bba-fund-success')).toBeVisible()
    await expect(fundPanel.locator('.bba-fund-success')).toContainText(/successful/i)

    // Step 4: The danger alert must be gone — the building has recovered.
    await expect(page.locator('.bba-alert-danger')).toBeHidden()
  })

  test('saves low-balance alert threshold for a building bank account', async ({ page }) => {
    const player = makePlayer()
    const companyId = 'company-bba-threshold'
    const buildingId = 'building-bba-threshold'

    player.companies.push(makeTestCompanyWithBuilding(player.id, companyId, buildingId))

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
      bankAccountId: 'acc-bba-threshold',
      accountNumber: '3333444455556666',
      balance: 12500,
      alertMinBalanceThreshold: null,
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

    const panel = page.locator('.building-bank-account-panel')
    const thresholdInput = panel.locator('.bba-threshold-input')

    await expect(thresholdInput).toBeVisible()
    await thresholdInput.fill('5000')
    await panel.getByRole('button', { name: /save threshold/i }).click()

    await expect.poll(() => state.buildingBankAccounts[buildingId]?.alertMinBalanceThreshold ?? null).toBe(5000)
    await expect(thresholdInput).toHaveValue('5000')
  })
})

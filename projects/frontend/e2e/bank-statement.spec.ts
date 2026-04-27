import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer } from './helpers/mock-api'

const COMPANY_ID = 'company-test-1'
const COMPANY_ID_2 = 'company-test-2'
const ACCOUNT_ID = 'account-test-1'
const ACCOUNT_ID_2 = 'account-test-2'

function makePlayerWithCompany() {
  const player = makePlayer()
  player.companies = [
    {
      id: COMPANY_ID,
      playerId: player.id,
      name: 'Test Trading Co.',
      cash: 200000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      foundedAtTick: 1,
      buildings: [],
    },
  ]
  player.activeAccountType = 'COMPANY'
  player.activeCompanyId = COMPANY_ID
  return player
}

function seedBankAccounts(state: ReturnType<typeof setupMockApi>, playerId: string) {
  state.myBankAccounts = [
      {
        id: ACCOUNT_ID,
        accountNumber: '1111222233334444',
        currencyCode: 'EUR',
      currencySymbol: '€',
      balance: 100000,
        companyId: COMPANY_ID,
        companyName: 'Test Trading Co.',
        ownerType: 'COMPANY',
        ownerDisplayName: 'Test Trading Co.',
        cityId: 'city-ba',
      },
      {
        id: ACCOUNT_ID_2,
        accountNumber: '5555666677778888',
      currencyCode: 'CZK',
      currencySymbol: 'Kč',
      balance: 250000,
        companyId: COMPANY_ID_2,
        companyName: 'Prague Imports',
        ownerType: 'COMPANY',
        ownerDisplayName: 'Prague Imports',
        cityId: 'city-pr',
      },
    ]

  const companyTwo = {
    id: COMPANY_ID_2,
    playerId,
    name: 'Prague Imports',
    cash: 150000,
    foundedAtUtc: '2026-01-02T00:00:00Z',
    foundedAtTick: 2,
    buildings: [],
  }

  if (!state.players[0]?.companies.find((company) => company.id === COMPANY_ID_2)) {
    state.players[0]?.companies.push(companyTwo)
  }
}

// ── Bank Statement Review page ────────────────────────────────────────────────

test.describe('Bank Statement Review', () => {
  test('redirects unauthenticated users to /login', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/bank-statement/some-company-id')
    await page.waitForURL(/\/login/)
    await expect(page).toHaveURL(/\/login/)
  })

  test('shows bank statement title and company name for authenticated player', async ({ page }) => {
    const player = makePlayerWithCompany()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    seedBankAccounts(state, player.id)
    state.bankStatementRows[COMPANY_ID] = []

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/bank-statement/${COMPANY_ID}`)

    await expect(page.getByRole('heading', { name: 'Bank Statement Review' })).toBeVisible()
    await expect(page.getByText('Review all financial transactions')).toBeVisible()
    await expect(page.locator('#account-select')).toHaveValue(ACCOUNT_ID)
  })

  test('shows empty state when no transactions', async ({ page }) => {
    const player = makePlayerWithCompany()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    seedBankAccounts(state, player.id)
    state.bankStatementRows[COMPANY_ID] = []

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/bank-statement/${COMPANY_ID}`)

    await expect(page.getByText('No transactions found for this account.')).toBeVisible()
  })

  test('shows transaction rows with credit and debit columns', async ({ page }) => {
    const player = makePlayerWithCompany()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    seedBankAccounts(state, player.id)

    const now = new Date().toISOString()
    state.bankStatementRows[COMPANY_ID] = [
      {
        id: 'row-1',
        recordedAtTick: 10,
        recordedAtUtc: now,
        description: 'Product sales revenue',
        category: 'REVENUE',
        amount: 5000,
        runningBalance: 205000,
        buildingId: null,
        buildingName: null,
      },
      {
        id: 'row-2',
        recordedAtTick: 9,
        recordedAtUtc: now,
        description: 'Material purchase',
        category: 'PURCHASING_COST',
        amount: -1500,
        runningBalance: 200000,
        buildingId: 'building-1',
        buildingName: 'My Factory',
      },
    ]

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/bank-statement/${ACCOUNT_ID}`)

    // Check table is visible
    const table = page.locator('.statement-table')
    await expect(table).toBeVisible()

    const rows = table.locator('.statement-row')
    await expect(rows).toHaveCount(2)

    // Credit row (first - highest tick shown first / newest first ordering)
    const creditRow = rows.first()
    await expect(creditRow.locator('.credit-cell')).toContainText('5,000.00')
    await expect(creditRow.locator('.debit-cell .empty-cell-dash')).toBeVisible()

    // Debit row
    const debitRow = rows.nth(1)
    await expect(debitRow.locator('.debit-cell')).toContainText('1,500.00')
    await expect(debitRow.locator('.credit-cell .empty-cell-dash')).toBeVisible()
  })

  test('shows account summary with company name and balance', async ({ page }) => {
    const player = makePlayerWithCompany()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    seedBankAccounts(state, player.id)

    state.bankStatementRows[COMPANY_ID] = [
      {
        id: 'row-1',
        recordedAtTick: 1,
        recordedAtUtc: new Date().toISOString(),
        description: 'Initial funding',
        category: 'REVENUE',
        amount: 100000,
        runningBalance: 100000,
        buildingId: null,
        buildingName: null,
      },
    ]

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/bank-statement/${ACCOUNT_ID}`)

    const summary = page.locator('[aria-label="Account summary"]')
    await expect(summary).toBeVisible()
    await expect(summary.getByText('Test Trading Co.')).toBeVisible()
    await expect(summary.getByText('Account number: 1111222233334444')).toBeVisible()
    await expect(summary.locator('.balance-amount')).toContainText('100,000')
  })

  test('shows building name sub-label when present', async ({ page }) => {
    const player = makePlayerWithCompany()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    seedBankAccounts(state, player.id)

    state.bankStatementRows[COMPANY_ID] = [
      {
        id: 'row-1',
        recordedAtTick: 5,
        recordedAtUtc: new Date().toISOString(),
        description: 'Labor costs',
        category: 'LABOR_COST',
        amount: -800,
        runningBalance: 199200,
        buildingId: 'bld-1',
        buildingName: 'Central Factory',
      },
    ]

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/bank-statement/${ACCOUNT_ID}`)

    await expect(page.locator('.description-sub').filter({ hasText: 'Central Factory' })).toBeVisible()
  })

  test('reacts to navbar account-context switch', async ({ page }) => {
    const player = makePlayerWithCompany()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    seedBankAccounts(state, player.id)
    state.bankStatementRows[COMPANY_ID] = [
      {
        id: 'row-1',
        recordedAtTick: 12,
        recordedAtUtc: new Date().toISOString(),
        description: 'Factory income',
        category: 'REVENUE',
        amount: 3000,
        runningBalance: 3000,
        buildingId: null,
        buildingName: null,
      },
    ]
    state.bankStatementRows[COMPANY_ID_2] = [
      {
        id: 'row-2',
        recordedAtTick: 18,
        recordedAtUtc: new Date().toISOString(),
        description: 'Prague export sale',
        category: 'REVENUE',
        amount: 9000,
        runningBalance: 9000,
        buildingId: null,
        buildingName: null,
      },
    ]

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/bank-statement/${COMPANY_ID}`)

    const selector = page.locator('#account-select')
    await expect(selector).toHaveValue(ACCOUNT_ID)
    await expect(selector.locator('option')).toHaveCount(1)

    await page.locator('.ctx-trigger').click()
    await page.locator('.ctx-account-option').filter({ hasText: 'Prague Imports' }).click()

    await expect(page.locator('#account-select')).toHaveValue(ACCOUNT_ID_2)
    await expect(page).toHaveURL(/\/bank-statement\/account-test-2/)
    await expect(page.locator('[aria-label="Account summary"]')).toContainText('Prague Imports')
    await expect(page.locator('[aria-label="Account summary"]')).toContainText('5555666677778888')
    await expect(page.locator('.statement-row').first()).toContainText('Prague export sale')
  })

  test('paginates bank statement rows', async ({ page }) => {
    const player = makePlayerWithCompany()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    seedBankAccounts(state, player.id)
    state.bankStatementRows[COMPANY_ID] = Array.from({ length: 60 }, (_, index) => ({
      id: `row-${index + 1}`,
      recordedAtTick: 60 - index,
      recordedAtUtc: new Date(Date.now() - index * 60000).toISOString(),
      description: `Statement row ${index + 1}`,
      category: 'REVENUE',
      amount: 100,
      runningBalance: (60 - index) * 100,
      buildingId: null,
      buildingName: null,
    }))

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/bank-statement/${ACCOUNT_ID}`)

    await expect(page.locator('.statement-row')).toHaveCount(50)
    await expect(page.getByText('Page 1 of 2')).toBeVisible()

    await page.getByRole('button', { name: 'Next' }).click()

    await expect(page.locator('.statement-row')).toHaveCount(10)
    await expect(page.getByText('Page 2 of 2')).toBeVisible()
    await expect(page.locator('.statement-row').first()).toContainText('Statement row 51')
  })
})

// ── Funding guidance in Buy Building ──────────────────────────────────────────

test.describe('Funding guidance in Buy Building', () => {
  test('shows funding gap warning when selecting Prague (CZK) with no CZK balance', async ({ page }) => {
    const player = makePlayerWithCompany()
    player.personalCash = 500000
    player.companies[0]!.cash = 500000
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    // No CZK balance — only EUR
    state.playerCurrencyBalances = []

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/buy-building/${COMPANY_ID}`)

    // Select a building type first
    await page.locator('.type-card').first().click()

    // Select Prague
    await page.locator('.city-option').filter({ hasText: 'Prague' }).click()

    // Funding guidance should appear
    const guidance = page.locator('.funding-guidance')
    await expect(guidance).toBeVisible()
    await expect(guidance).toContainText('CZK')
  })

  test('does not show funding gap warning for EUR city (Bratislava)', async ({ page }) => {
    const player = makePlayerWithCompany()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.playerCurrencyBalances = []

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/buy-building/${COMPANY_ID}`)

    // Select a building type
    await page.locator('.type-card').first().click()

    // Select Bratislava (EUR city)
    await page.locator('.city-option').filter({ hasText: 'Bratislava' }).click()

    // No funding warning for EUR city
    await expect(page.locator('.funding-guidance')).toBeHidden()
  })

  test('funding guidance has Forex and Bank Statement CTA links', async ({ page }) => {
    const player = makePlayerWithCompany()
    player.personalCash = 500000
    player.companies[0]!.cash = 500000
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.playerCurrencyBalances = []

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/buy-building/${COMPANY_ID}`)

    // Select a building type
    await page.locator('.type-card').first().click()

    // Select Prague
    await page.locator('.city-option').filter({ hasText: 'Prague' }).click()

    const guidance = page.locator('.funding-guidance')
    await expect(guidance).toBeVisible()

    // Forex CTA link
    const forexLink = guidance.locator('.btn-guidance-primary')
    await expect(forexLink).toBeVisible()
    await expect(forexLink).toHaveAttribute('href', '/forex')

    // Bank statement link
    const stmtLink = guidance.locator('.btn-guidance-secondary')
    await expect(stmtLink).toBeVisible()
    await expect(stmtLink).toHaveAttribute('href', `/bank-statement/${COMPANY_ID}`)
  })

  test('does not show funding gap when player has sufficient CZK balance', async ({ page }) => {
    const player = makePlayerWithCompany()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.myBankAccounts = [
      {
        id: 'account-czk-1',
        accountNumber: '9999000011112222',
        currencyCode: 'CZK',
        currencySymbol: 'Kč',
        balance: 50000,
        companyId: COMPANY_ID,
        companyName: 'Test Trading Co.',
        ownerType: 'COMPANY',
        ownerDisplayName: 'Test Trading Co.',
        cityId: 'city-pr',
      },
    ]

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto(`/buy-building/${COMPANY_ID}`)

    // Select a building type
    await page.locator('.type-card').first().click()

    // Select Prague
    await page.locator('.city-option').filter({ hasText: 'Prague' }).click()

    // Should NOT show funding warning since player has CZK
    await expect(page.locator('.funding-guidance')).toBeHidden()
  })
})

// ── Forex page bank statement link ────────────────────────────────────────────

test.describe('Forex page bank statement link', () => {
  test('shows View Bank Statement link in balances section', async ({ page }) => {
    const player = makePlayerWithCompany()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/forex')

    const stmtLink = page.locator('.statement-link')
    await expect(stmtLink).toBeVisible()
    await expect(stmtLink).toContainText('View Bank Statement')
  })
})

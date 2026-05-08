import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer, makeFxRateHistory } from '../../helpers/mock-api'
import type { MockGoldAmmPool } from '../../helpers/mock-api'

// ── Forex Exchange page ───────────────────────────────────────────────────────

test.describe('Forex Exchange page', () => {
  test('redirects unauthenticated users to /login', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/forex')
    await page.waitForURL(/\/login/)
    await expect(page).toHaveURL(/\/login/)
  })

  test('shows forex page heading and subtitle for authenticated player', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex')

    await expect(page.locator('.forex-hero').getByRole('heading', { name: 'Forex Exchange' })).toBeVisible()
    await expect(page.getByText('Swap currencies between city economies')).toBeVisible()
  })

  test('shows EUR balance in balances section', async ({ page }) => {
    const player = makePlayer()
    player.personalCash = 50000
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex')

    await expect(page.getByText('Your Currency Balances')).toBeVisible()
    await expect(page.locator('.balance-card').filter({ hasText: 'EUR' })).toBeVisible()
  })

  test('shows source and target currency selectors', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex')

    await expect(page.getByLabel('You send')).toBeVisible()
    await expect(page.getByLabel('You receive')).toBeVisible()
    await expect(page.getByLabel('Amount')).toBeVisible()
  })

  test('opens with Swap tab active by default and shows the Rate List tab', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex')

    await expect(page.getByRole('tab', { name: 'Swap' })).toHaveAttribute('aria-selected', 'true')
    await expect(page.getByRole('tab', { name: 'Rate List' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Swap' })).toBeVisible()
  })

  test('shows live rates on the Rate List tab with EUR as default base', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.fxRates = [
      {
        baseCurrencyCode: 'EUR',
        quoteCurrencyCode: 'CZK',
        rate: 25.1234,
        rateDate: '2026-04-22',
        source: 'ECB',
        quoteCurrencySymbol: 'Kč',
      },
      {
        baseCurrencyCode: 'EUR',
        quoteCurrencyCode: 'USD',
        rate: 1.08,
        rateDate: '2026-04-22',
        source: 'ECB',
        quoteCurrencySymbol: '$',
      },
    ]
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex')

    await page.getByRole('tab', { name: 'Rate List' }).click()

    await expect(page.getByRole('heading', { name: 'Live Rate List' })).toBeVisible()
    // City rate board context banner is visible
    await expect(page.locator('.city-rate-context')).toBeVisible()
    // When Bratislava (EUR) is selected (default), base currency shown is EUR
    await expect(page.locator('.city-rate-context')).toContainText('EUR')
    // Table shows target currencies
    await expect(page.locator('.rates-table')).toContainText('CZK')
    await expect(page.locator('.rates-table')).toContainText('USD')
  })

  test('supports deep links that preselect the destination currency', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?toCurrency=USD')

    await expect(page.getByRole('tab', { name: 'Swap' })).toHaveAttribute('aria-selected', 'true')
    await expect(page.getByLabel('You receive')).toHaveValue('USD')
  })

  test('shows validation error when same currency is selected', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex')

    // Set both currencies to EUR
    await page.getByLabel('You send').selectOption('EUR')
    await page.getByLabel('You receive').selectOption('EUR')
    await page.getByLabel('Amount').fill('100')

    await expect(page.getByText('Please select different source and target currencies.')).toBeVisible()
  })

  test('shows validation error for insufficient funds', async ({ page }) => {
    const player = makePlayer()
    player.personalCash = 10
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex')

    await page.getByLabel('You send').selectOption('EUR')
    await page.getByLabel('You receive').selectOption('CZK')
    await page.getByLabel('Amount').fill('1000')

    await expect(page.getByText('Insufficient balance for this swap.')).toBeVisible()
  })

  test('happy path: get quote and confirm swap', async ({ page }) => {
    const player = makePlayer()
    player.personalCash = 100000
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex')

    // Fill the form
    await page.getByLabel('You send').selectOption('EUR')
    await page.getByLabel('You receive').selectOption('CZK')
    await page.getByLabel('Amount').fill('500')

    // Get quote
    await page.getByRole('button', { name: 'Get Quote' }).click()

    // Quote card should appear
    await expect(page.getByRole('region', { name: 'Exchange Quote' })).toBeVisible()
    await expect(page.getByText('Exchange Quote')).toBeVisible()
    await expect(page.locator('.quote-table').getByText('Rate')).toBeVisible()
    await expect(page.locator('.quote-table').getByText('Fee (1%)')).toBeVisible()
    await expect(page.locator('.quote-table').getByText('You receive')).toBeVisible()

    // Confirm swap
    await page.getByRole('button', { name: 'Confirm Swap' }).click()

    // Result banner should appear
    await expect(page.getByRole('status')).toBeVisible()
    await expect(page.locator('.swap-result-banner')).toBeVisible()
  })

  test('cancel quote hides the confirmation panel', async ({ page }) => {
    const player = makePlayer()
    player.personalCash = 100000
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex')

    await page.getByLabel('You send').selectOption('EUR')
    await page.getByLabel('You receive').selectOption('USD')
    await page.getByLabel('Amount').fill('200')

    await page.getByRole('button', { name: 'Get Quote' }).click()
    await expect(page.getByRole('region', { name: 'Exchange Quote' })).toBeVisible()

    await page.getByRole('button', { name: 'Cancel' }).click()
    await expect(page.getByRole('region', { name: 'Exchange Quote' })).toBeHidden()
  })

  test('shows trade history after successful swap', async ({ page }) => {
    const player = makePlayer()
    player.personalCash = 100000
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    // Pre-populate history
    state.forexTradeHistory = [
      {
        id: 'trade-1',
        fromCurrencyCode: 'EUR',
        toCurrencyCode: 'USD',
        fromAmount: 100,
        toAmount: 107.5,
        feeAmount: 1,
        rate: 1.086,
        executedAtTick: 42,
        executedAtUtc: new Date().toISOString(),
        fromCurrencySymbol: '€',
        toCurrencySymbol: '$',
      },
    ]
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex')

    await page.getByRole('tab', { name: 'History' }).click()

    await expect(page.getByText('Recent Trades')).toBeVisible()
    await expect(page.locator('.history-row').first()).toBeVisible()
    await expect(page.locator('.history-row').first().getByText('EUR')).toBeVisible()
  })

  test('swap currencies button swaps source and target', async ({ page }) => {
    const player = makePlayer()
    player.personalCash = 100000
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex')

    // Set EUR -> CZK
    await page.getByLabel('You send').selectOption('EUR')
    await page.getByLabel('You receive').selectOption('CZK')

    // Swap
    await page.getByRole('button', { name: 'Swap currencies' }).click()

    // Now should be CZK -> EUR
    await expect(page.getByLabel('You send')).toHaveValue('CZK')
    await expect(page.getByLabel('You receive')).toHaveValue('EUR')
  })

  test('forex nav link is present in navigation for authenticated users', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/')

    // The forex link exists in the DOM (may be hidden in mobile nav)
    await expect(page.locator('a[href="/forex"]')).toHaveCount(1)
    await expect(page.locator('a[href="/forex"]')).toHaveAttribute('title', 'Forex')
  })

  // ── Bank-account-native swap flow ─────────────────────────────────────────

  test('shows bank account selectors when player has company bank accounts', async ({ page }) => {
    const player = makePlayer()
    player.personalCash = 0
    const activeCompanyId = player.companies[0]?.id ?? 'comp-1'
    player.activeAccountType = 'COMPANY'
    player.activeCompanyId = activeCompanyId
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.myBankAccounts = [
      {
        id: 'ba-eur-001',
        accountNumber: '1111111111111111',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 5000,
        companyId: activeCompanyId,
        companyName: 'My Corp',
        ownerType: 'COMPANY',
        ownerDisplayName: 'My Corp',
      },
      {
        id: 'ba-czk-001',
        accountNumber: '2222222222222222',
        currencyCode: 'CZK',
        currencySymbol: 'Kč',
        balance: 80000,
        companyId: activeCompanyId,
        companyName: 'My Corp',
        ownerType: 'COMPANY',
        ownerDisplayName: 'My Corp',
      },
    ]
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex')

    // Bank account selectors should be visible
    await expect(page.getByLabel('Source account')).toBeVisible()
    await expect(page.getByLabel('Destination account')).toBeVisible()

    // The bank-account mode notice should appear
    await expect(page.locator('.ba-notice')).toBeVisible()

    // The legacy personal balance section should NOT appear when bank accounts are present
    await expect(page.getByText('Your Currency Balances')).toBeHidden()
  })

  test('company context shows only active company accounts in swap and transfer selectors', async ({ page }) => {
    const player = makePlayer()
    const activeCompany = {
      id: 'company-active',
      name: 'Active Holdings',
      cash: 0,
      foundedAtUtc: new Date().toISOString(),
      foundedAtTick: 1,
      buildings: [],
    }
    const otherCompany = {
      id: 'company-other',
      name: 'Other Holdings',
      cash: 0,
      foundedAtUtc: new Date().toISOString(),
      foundedAtTick: 1,
      buildings: [],
    }
    player.companies = [activeCompany, otherCompany]
    const activeCompanyId = activeCompany.id
    player.activeAccountType = 'COMPANY'
    player.activeCompanyId = activeCompanyId

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.myBankAccounts = [
      {
        id: 'personal-usd',
        accountNumber: '1000000000000001',
        currencyCode: 'USD',
        currencySymbol: '$',
        balance: 2000,
        companyId: null,
        ownerType: 'PERSON',
        ownerDisplayName: player.displayName,
      },
      {
        id: 'active-eur',
        accountNumber: '1000000000000002',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 5000,
        companyId: activeCompanyId,
        companyName: activeCompany.name,
        ownerType: 'COMPANY',
        ownerDisplayName: activeCompany.name,
      },
      {
        id: 'active-czk',
        accountNumber: '1000000000000003',
        currencyCode: 'CZK',
        currencySymbol: 'Kč',
        balance: 10000,
        companyId: activeCompanyId,
        companyName: activeCompany.name,
        ownerType: 'COMPANY',
        ownerDisplayName: activeCompany.name,
      },
      {
        id: 'other-eur',
        accountNumber: '1000000000000004',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 9000,
        companyId: 'company-other',
        companyName: 'Other Holdings',
        ownerType: 'COMPANY',
        ownerDisplayName: 'Other Holdings',
      },
    ]

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/forex')

    await expect(page.locator('#from-bank-account option', { hasText: activeCompany.name })).toHaveCount(2)
    await expect(page.locator('#from-bank-account option', { hasText: player.displayName })).toHaveCount(0)
    await expect(page.locator('#from-bank-account option', { hasText: 'Other Holdings' })).toHaveCount(0)

    await page.getByRole('tab', { name: 'Transfer' }).click()
    await expect(page.locator('#bank-transfer-from option', { hasText: activeCompany.name })).toHaveCount(2)
    await expect(page.locator('#bank-transfer-from option', { hasText: player.displayName })).toHaveCount(0)
    await expect(page.locator('#bank-transfer-from option', { hasText: 'Other Holdings' })).toHaveCount(0)
  })

  test('person context shows only personal accounts in swap and transfer selectors', async ({ page }) => {
    const player = makePlayer()
    player.activeAccountType = 'PERSON'
    player.activeCompanyId = null

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.myBankAccounts = [
      {
        id: 'personal-usd',
        accountNumber: '2000000000000001',
        currencyCode: 'USD',
        currencySymbol: '$',
        balance: 2000,
        companyId: null,
        ownerType: 'PERSON',
        ownerDisplayName: player.displayName,
      },
      {
        id: 'personal-eur',
        accountNumber: '2000000000000002',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 500,
        companyId: null,
        ownerType: 'PERSON',
        ownerDisplayName: player.displayName,
      },
      {
        id: 'company-eur',
        accountNumber: '2000000000000003',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 12000,
        companyId: player.companies[0]?.id ?? 'company-1',
        companyName: player.companies[0]?.name ?? 'My Company',
        ownerType: 'COMPANY',
        ownerDisplayName: player.companies[0]?.name ?? 'My Company',
      },
    ]

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/forex')

    await expect(page.locator('#from-bank-account option', { hasText: player.displayName })).toHaveCount(2)
    await expect(page.locator('#from-bank-account option', { hasText: player.companies[0]?.name ?? 'My Company' })).toHaveCount(0)

    await page.getByRole('tab', { name: 'Transfer' }).click()
    await expect(page.locator('#bank-transfer-from option', { hasText: player.displayName })).toHaveCount(2)
    await expect(page.locator('#bank-transfer-from option', { hasText: player.companies[0]?.name ?? 'My Company' })).toHaveCount(0)
  })

  test('successfully completes a bank account swap and shows result', async ({ page }) => {
    const player = makePlayer()
    player.personalCash = 0
    const companyId = player.companies[0]?.id ?? 'comp-1'
    player.activeAccountType = 'COMPANY'
    player.activeCompanyId = companyId
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.myBankAccounts = [
      {
        id: 'ba-eur-swap',
        accountNumber: '3333333333333333',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 10000,
        companyId,
        companyName: 'Swap Corp',
        ownerType: 'COMPANY',
        ownerDisplayName: 'Swap Corp',
      },
      {
        id: 'ba-czk-swap',
        accountNumber: '4444444444444444',
        currencyCode: 'CZK',
        currencySymbol: 'Kč',
        balance: 0,
        companyId,
        companyName: 'Swap Corp',
        ownerType: 'COMPANY',
        ownerDisplayName: 'Swap Corp',
      },
    ]
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex')

    // Select EUR source account and CZK dest account
    await page.getByLabel('Source account').selectOption('ba-eur-swap')
    await page.getByLabel('Destination account').selectOption('ba-czk-swap')

    // Enter amount
    await page.getByLabel('Amount').fill('500')

    // Get quote
    await page.getByRole('button', { name: 'Get Quote' }).click()

    // Quote card should appear
    await expect(page.getByRole('region', { name: 'Exchange Quote' })).toBeVisible()
    await expect(page.getByText('Exchange Quote')).toBeVisible()

    // Confirm the swap
    await page.getByRole('button', { name: 'Confirm Swap' }).click()

    // Success banner should appear
    await expect(page.locator('.swap-result-banner')).toBeVisible()
    await expect(page.locator('.swap-result-banner').getByText('EUR')).toBeVisible()
    await expect(page.locator('.swap-result-banner').getByText('CZK')).toBeVisible()
  })

  test('blocks swap when source bank account has insufficient funds', async ({ page }) => {
    const player = makePlayer()
    player.personalCash = 0
    const companyId = player.companies[0]?.id ?? 'comp-1'
    player.activeAccountType = 'COMPANY'
    player.activeCompanyId = companyId
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.myBankAccounts = [
      {
        id: 'ba-low-eur',
        accountNumber: '5555555555555555',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 50,
        companyId,
        companyName: 'Low Corp',
        ownerType: 'COMPANY',
        ownerDisplayName: 'Low Corp',
      },
      {
        id: 'ba-czk-low',
        accountNumber: '6666666666666666',
        currencyCode: 'CZK',
        currencySymbol: 'Kč',
        balance: 0,
        companyId,
        companyName: 'Low Corp',
        ownerType: 'COMPANY',
        ownerDisplayName: 'Low Corp',
      },
    ]
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex')

    await page.getByLabel('Source account').selectOption('ba-low-eur')
    await page.getByLabel('Destination account').selectOption('ba-czk-low')
    await page.getByLabel('Amount').fill('500')

    // Validation error should appear (amount > balance)
    await expect(page.locator('.validation-error')).toBeVisible()
    await expect(page.locator('.validation-error')).toContainText('Insufficient balance')

    // Get Quote button should be disabled
    await expect(page.getByRole('button', { name: 'Get Quote' })).toBeDisabled()
  })

  test('shows balance of selected source bank account below selector', async ({ page }) => {
    const player = makePlayer()
    player.personalCash = 0
    const companyId = player.companies[0]?.id ?? 'comp-1'
    player.activeAccountType = 'COMPANY'
    player.activeCompanyId = companyId
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.myBankAccounts = [
      {
        id: 'ba-bal-test',
        accountNumber: '7777777777777777',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 3750.5,
        companyId,
        companyName: 'Balance Corp',
        ownerType: 'COMPANY',
        ownerDisplayName: 'Balance Corp',
      },
    ]
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex')

    await page.getByLabel('Source account').selectOption('ba-bal-test')

    // The source bank account selector shows the balance
    await expect(page.locator('.forex-ba-selector').first().locator('.balance-display')).toBeVisible()
    await expect(page.locator('.field-hint').filter({ hasText: 'Available balance' })).toBeVisible()
  })

  // ── City-based FX rate board ──────────────────────────────────────────────

  test('Rate List shows CZK as base currency when Prague is selected city', async ({ page }) => {
    const player = makePlayer({
      companies: [
        {
          id: 'company-prague-rates',
          playerId: 'player-1',
          name: 'Prague Rates Corp',
          cash: 500000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [
            {
              id: 'building-prague-rates',
              companyId: 'company-prague-rates',
              cityId: 'city-pr',
              type: 'FACTORY',
              name: 'Prague Rates Factory',
              latitude: 50.08,
              longitude: 14.44,
              level: 1,
              powerConsumption: 10,
              isForSale: false,
              units: [],
              pendingConfiguration: null,
            },
          ],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.fxRates = [
      { baseCurrencyCode: 'EUR', quoteCurrencyCode: 'CZK', rate: 25.2, rateDate: '2026-04-27', source: 'FALLBACK', quoteCurrencySymbol: 'Kč' },
      { baseCurrencyCode: 'EUR', quoteCurrencyCode: 'USD', rate: 1.08, rateDate: '2026-04-27', source: 'FALLBACK', quoteCurrencySymbol: '$' },
    ]
    await page.addInitScript(
      ({ token, cityId }) => {
        localStorage.setItem('auth_token', token)
        localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
        localStorage.setItem('selected_city_id', cityId)
      },
      { token: `token-${player.id}`, cityId: 'city-pr' },
    )
    await page.goto('/forex')

    await page.getByRole('tab', { name: 'Rate List' }).click()

    // City rate context banner should show CZK (Prague's currency)
    await expect(page.locator('.city-rate-context')).toBeVisible()
    await expect(page.locator('.city-rate-context')).toContainText('CZK')
    await expect(page.locator('.city-rate-context')).toContainText('Prague')

    // Table should show EUR and USD as targets
    await expect(page.locator('.rates-table')).toContainText('EUR')
    await expect(page.locator('.rates-table')).toContainText('USD')
    // CZK should NOT appear as a target row (it's the base) — verify no tbody row contains only CZK
    const rows = page.locator('.rates-table tbody tr')
    await expect(rows).toHaveCount(2) // EUR and USD only
  })

  test('Rate List shows EUR as base currency when Bratislava is selected city', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.fxRates = [
      { baseCurrencyCode: 'EUR', quoteCurrencyCode: 'CZK', rate: 25.2, rateDate: '2026-04-27', source: 'FALLBACK', quoteCurrencySymbol: 'Kč' },
      { baseCurrencyCode: 'EUR', quoteCurrencyCode: 'USD', rate: 1.08, rateDate: '2026-04-27', source: 'FALLBACK', quoteCurrencySymbol: '$' },
    ]
    await page.addInitScript(
      ({ token, cityId }) => {
        localStorage.setItem('auth_token', token)
        localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
        localStorage.setItem('selected_city_id', cityId)
      },
      { token: `token-${player.id}`, cityId: 'city-ba' },
    )
    await page.goto('/forex')

    await page.getByRole('tab', { name: 'Rate List' }).click()

    // City context banner shows EUR and Bratislava
    await expect(page.locator('.city-rate-context')).toBeVisible()
    await expect(page.locator('.city-rate-context')).toContainText('EUR')
    await expect(page.locator('.city-rate-context')).toContainText('Bratislava')

    // Table should include CZK and USD as targets
    await expect(page.locator('.rates-table')).toContainText('CZK')
    await expect(page.locator('.rates-table')).toContainText('USD')
  })

  test('cross rate is correctly computed for stronger-first non-EUR base pair (USDCZK)', async ({ page }) => {
    const player = makePlayer({
      companies: [
        {
          id: 'company-prague-cross',
          playerId: 'player-1',
          name: 'Prague Cross Corp',
          cash: 500000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [
            {
              id: 'building-prague-cross',
              companyId: 'company-prague-cross',
              cityId: 'city-pr',
              type: 'FACTORY',
              name: 'Prague Cross Factory',
              latitude: 50.08,
              longitude: 14.44,
              level: 1,
              powerConsumption: 10,
              isForSale: false,
              units: [],
              pendingConfiguration: null,
            },
          ],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    // EUR/CZK = 25, EUR/USD = 1.0 → USDCZK = 25
    state.fxRates = [
      { baseCurrencyCode: 'EUR', quoteCurrencyCode: 'CZK', rate: 25, rateDate: '2026-04-27', source: 'FALLBACK', quoteCurrencySymbol: 'Kč' },
      { baseCurrencyCode: 'EUR', quoteCurrencyCode: 'USD', rate: 1.0, rateDate: '2026-04-27', source: 'FALLBACK', quoteCurrencySymbol: '$' },
    ]
    await page.addInitScript(
      ({ token, cityId }) => {
        localStorage.setItem('auth_token', token)
        localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
        localStorage.setItem('selected_city_id', cityId)
      },
      { token: `token-${player.id}`, cityId: 'city-pr' },
    )
    await page.goto('/forex')

    await page.getByRole('tab', { name: 'Rate List' }).click()

    // Mid rate for USD row should be 25.00
    const usdRow = page.locator('.rates-table tbody tr').filter({ hasText: 'USD' })
    await expect(usdRow).toBeVisible()
    await expect(usdRow.locator('.rate-pair-label').filter({ hasText: 'USDCZK' })).toHaveCount(1)
    // The mid rate column should show 25.00
    await expect(usdRow).toContainText('25.00')
    // The after-fee column shows 25.00 * 0.99 = 24.75
    await expect(usdRow).toContainText('24.75')
  })

  test('city context badge is visible on the Swap tab', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript(
      ({ token, cityId }) => {
        localStorage.setItem('auth_token', token)
        localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
        localStorage.setItem('selected_city_id', cityId)
      },
      { token: `token-${player.id}`, cityId: 'city-ba' },
    )
    await page.goto('/forex')

    // Swap tab is active by default — check city badge is visible
    await expect(page.locator('.swap-city-badge')).toBeVisible()
    await expect(page.locator('.swap-city-badge')).toContainText('EUR')
    await expect(page.locator('.swap-city-badge')).toContainText('Bratislava')
  })

  test('after-fee note is visible on Rate List tab', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex')

    await page.getByRole('tab', { name: 'Rate List' }).click()

    await expect(page.locator('.rates-table thead').getByText('Mid rate')).toBeVisible()
    await expect(page.getByText('After 1% fee')).toBeVisible()
  })

  // ── Gold AMM tab ──────────────────────────────────────────────────────────

  test('shows Gold AMM tab and navigates to it', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.goldBalance = { balance: 5.0, blockedInPools: 0, availableBalance: 5.0 }
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex')

    await page.getByRole('tab', { name: 'Gold AMM' }).click()

    await expect(page.getByRole('heading', { name: '🥇 Gold Token Exchange (AMM)' })).toBeVisible()
    await expect(page.getByText('XAU Gold', { exact: true })).toBeVisible()
  })

  test('Gold AMM swap tab shows direction and currency selectors', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.goldBalance = { balance: 2.5, blockedInPools: 0, availableBalance: 2.5 }
    state.goldAmmPools = [
      {
        id: 'pool-eur',
        currencyCode: 'EUR',
        currencySymbol: '€',
        fiatReserve: 10000,
        goldReserve: 5.0,
        totalLiquidityShares: 1000,
        impliedGoldPrice: 2000,
        myPosition: null,
      } satisfies MockGoldAmmPool,
    ]
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=gold')

    const swapPanel = page.locator('[aria-label="Gold Swap"]')
    // Two selects: direction and currency
    await expect(swapPanel.locator('select').first()).toBeVisible()
    await expect(swapPanel.locator('select').nth(1)).toBeVisible()
  })

  test('Gold AMM My Positions tab shows empty state and CTA when no positions', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.goldBalance = { balance: 0, blockedInPools: 0, availableBalance: 0 }
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=gold')

    // Navigate to positions sub-tab
    await page.getByRole('tab', { name: 'My Positions' }).click()

    await expect(page.locator('[aria-label="My Liquidity Positions"]')).toBeVisible()
    await expect(page.getByText('You have no open liquidity positions.')).toBeVisible()
    // CTA button to add liquidity
    await expect(page.getByRole('button', { name: /Add Liquidity/ })).toBeVisible()
  })

  test('Gold AMM My Positions tab shows existing position with share percentage', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.goldBalance = { balance: 3.0, blockedInPools: 1.0, availableBalance: 2.0 }
    state.goldAmmPools = [
      {
        id: 'pool-czk',
        currencyCode: 'CZK',
        currencySymbol: 'Kč',
        fiatReserve: 50000,
        goldReserve: 2.0,
        totalLiquidityShares: 500,
        impliedGoldPrice: 25000,
        myPosition: {
          id: 'pos-czk-1',
          poolId: 'pool-czk',
          currencyCode: 'CZK',
          liquidityShares: 250,
          sharePercent: 50,
          claimableFiat: 12.5,
          claimableGold: 0.0005,
          fiatProvided: 25000,
          goldProvided: 1.0,
        },
      } satisfies MockGoldAmmPool,
    ]
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=gold')

    await page.getByRole('tab', { name: 'My Positions' }).click()

    // Position card visible
    await expect(page.locator('[aria-label="My Liquidity Positions"]').getByText('CZK/XAU', { exact: true })).toBeVisible()
    await expect(page.getByText('50.00% Your share')).toBeVisible()
    // Claimable fees section visible
    await expect(page.getByText('Claimable', { exact: true })).toBeVisible()
    // Remove button visible
    await expect(page.getByRole('button', { name: 'Remove' })).toBeVisible()
  })

  test('Gold AMM blocked gold shows warning in gold balance card', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.goldBalance = { balance: 5.0, blockedInPools: 2.0, availableBalance: 3.0 }
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=gold')

    const goldCard = page.locator('[aria-label="Gold Balance"]')
    await expect(goldCard).toBeVisible()
    await expect(goldCard.getByText('Locked in pools', { exact: true })).toBeVisible()
    // Blocked warning message visible
    await expect(goldCard.getByText(/Some of your gold is locked/)).toBeVisible()
  })

  test('Gold AMM Add Liquidity tab shows pool selector when pools exist', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.goldBalance = { balance: 10.0, blockedInPools: 0, availableBalance: 10.0 }
    state.goldAmmPools = [
      {
        id: 'pool-eur-add',
        currencyCode: 'EUR',
        currencySymbol: '€',
        fiatReserve: 20000,
        goldReserve: 10.0,
        totalLiquidityShares: 1000,
        impliedGoldPrice: 2000,
        myPosition: null,
      } satisfies MockGoldAmmPool,
    ]
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=gold')

    await page.getByRole('tab', { name: 'Add Liquidity' }).click()

    const addSection = page.locator('[aria-label="Add Liquidity"]')
    await expect(addSection).toBeVisible()
    await expect(addSection.getByText('Add Liquidity').first()).toBeVisible()
    // Pool selector shows the EUR/XAU pool
    await expect(addSection.locator('select').first()).toBeVisible()
    await expect(addSection.locator('select').filter({ hasText: 'EUR/XAU' })).toBeVisible()
  })

  test('Gold AMM Add Liquidity succeeds and shows success message', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.playerCurrencyBalances = [{ currencyCode: 'EUR', currencySymbol: '€', balance: 50000 }]
    state.goldBalance = { balance: 10.0, blockedInPools: 0, availableBalance: 10.0 }
    state.goldAmmPools = [
      {
        id: 'pool-eur-liq',
        currencyCode: 'EUR',
        currencySymbol: '€',
        fiatReserve: 40000,
        goldReserve: 20.0,
        totalLiquidityShares: 2000,
        impliedGoldPrice: 2000,
        myPosition: null,
      } satisfies MockGoldAmmPool,
    ]
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=gold')

    await page.getByRole('tab', { name: 'Add Liquidity' }).click()

    const addSection = page.locator('[aria-label="Add Liquidity"]')
    await addSection.locator('select').filter({ hasText: 'EUR/XAU' }).selectOption('pool-eur-liq')

    const inputs = addSection.locator('input[type="number"]')
    await inputs.nth(0).fill('2000')
    await inputs.nth(1).fill('1.0')

    await addSection.getByRole('button', { name: 'Add Liquidity' }).click()

    // After successful add, navigate to My Positions to confirm the position was created
    await page.getByRole('tab', { name: 'My Positions' }).click()
    await expect(page.locator('[aria-label="My Liquidity Positions"]').getByText('EUR/XAU', { exact: true })).toBeVisible()
  })

  test('Gold AMM blocked-funds rejection: locked gold shows warning when adding liquidity', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    // All gold is locked — none available for adding liquidity
    state.goldBalance = { balance: 5.0, blockedInPools: 5.0, availableBalance: 0 }
    state.goldAmmPools = [
      {
        id: 'pool-eur-blocked',
        currencyCode: 'EUR',
        currencySymbol: '€',
        fiatReserve: 10000,
        goldReserve: 5.0,
        totalLiquidityShares: 1000,
        impliedGoldPrice: 2000,
        myPosition: null,
      } satisfies MockGoldAmmPool,
    ]
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=gold')

    await page.getByRole('tab', { name: 'Add Liquidity' }).click()

    const addSection = page.locator('[aria-label="Add Liquidity"]')
    // Warning about blocked gold is visible (first occurrence in the locked gold header section)
    await expect(addSection.locator('[role="note"]').first()).toBeVisible()
    await expect(addSection.locator('[role="note"]').first()).toContainText('Some of your gold is locked')
  })

  test('Gold AMM Create Pool form shown when no pools exist', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.goldBalance = { balance: 10.0, blockedInPools: 0, availableBalance: 10.0 }
    // No pools exist
    state.goldAmmPools = []
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=gold')

    await page.getByRole('tab', { name: 'Add Liquidity' }).click()

    const addSection = page.locator('[aria-label="Add Liquidity"]')
    await expect(addSection.getByText('Create New Pool')).toBeVisible()
    await expect(addSection.getByRole('button', { name: 'Create Pool' })).toBeVisible()
  })

  test('Gold AMM inner tabs switch correctly', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.goldBalance = { balance: 0, blockedInPools: 0, availableBalance: 0 }
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=gold')

    // Default: AMM Swap tab
    await expect(page.getByRole('tab', { name: 'AMM Swap' })).toHaveAttribute('aria-selected', 'true')
    await expect(page.locator('[aria-label="Gold Swap"]')).toBeVisible()

    // Switch to My Positions
    await page.getByRole('tab', { name: 'My Positions' }).click()
    await expect(page.locator('[aria-label="My Liquidity Positions"]')).toBeVisible()

    // Switch to Add Liquidity
    await page.getByRole('tab', { name: 'Add Liquidity' }).click()
    await expect(page.locator('[aria-label="Add Liquidity"]')).toBeVisible()

    // Switch back to AMM Swap
    await page.getByRole('tab', { name: 'AMM Swap' }).click()
    await expect(page.locator('[aria-label="Gold Swap"]')).toBeVisible()
  })

  test('Gold AMM swap execution: get quote and confirm swap updates balances', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.playerCurrencyBalances = [{ currencyCode: 'EUR', currencySymbol: '€', balance: 50000 }]
    state.goldBalance = { balance: 2.0, blockedInPools: 0, availableBalance: 2.0 }
    state.goldAmmPools = [
      {
        id: 'pool-eur-swap',
        currencyCode: 'EUR',
        currencySymbol: '€',
        fiatReserve: 20000,
        goldReserve: 10.0,
        totalLiquidityShares: 1000,
        impliedGoldPrice: 2000,
        myPosition: null,
      } satisfies MockGoldAmmPool,
    ]
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=gold')

    const swapSection = page.locator('[aria-label="Gold Swap"]')

    // Fill in swap amount and request quote
    await swapSection.locator('input[type="number"]').fill('1000')
    await swapSection.getByRole('button', { name: 'Get Quote' }).click()

    // Quote confirmation panel appears
    const quotePanel = page.locator('[aria-label="Swap Quote"]')
    await expect(quotePanel).toBeVisible()
    await expect(quotePanel.getByText('1,000')).toBeVisible() // input amount
    // The "You receive" row contains XAU (gold output)
    await expect(quotePanel.getByText(/XAU$/).first()).toBeVisible()

    // Confirm the swap
    await quotePanel.getByRole('button', { name: 'Confirm Swap' }).click()

    // Success message with received gold amount
    await expect(swapSection.locator('[role="status"]')).toBeVisible()
    await expect(swapSection.locator('[role="status"]')).toContainText('XAU')
  })

  test('Gold AMM remove liquidity: confirms removal and shows success', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.goldBalance = { balance: 0, blockedInPools: 5.0, availableBalance: 0 }
    state.goldAmmPools = [
      {
        id: 'pool-eur-remove',
        currencyCode: 'EUR',
        currencySymbol: '€',
        fiatReserve: 10000,
        goldReserve: 5.0,
        totalLiquidityShares: 1000,
        impliedGoldPrice: 2000,
        myPosition: {
          id: 'pos-remove-1',
          poolId: 'pool-eur-remove',
          currencyCode: 'EUR',
          liquidityShares: 1000,
          sharePercent: 100,
          claimableFiat: 10000,
          claimableGold: 5.0,
          fiatProvided: 10000,
          goldProvided: 5.0,
        },
      } satisfies MockGoldAmmPool,
    ]
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=gold')

    // Navigate to My Positions tab
    await page.getByRole('tab', { name: 'My Positions' }).click()

    const positionsPanel = page.locator('[aria-label="My Liquidity Positions"]')
    await expect(positionsPanel.getByText('EUR/XAU', { exact: true })).toBeVisible()

    // Click Remove Liquidity (button text is 'Remove' per i18n removeLiquidity key)
    await positionsPanel.getByRole('button', { name: 'Remove', exact: true }).click()

    // Success message appears
    await expect(positionsPanel.locator('[role="status"]')).toBeVisible()
    await expect(positionsPanel.locator('[role="status"]')).toContainText('Liquidity removed')
  })

  test('Gold AMM create pool: submitting form creates pool and shows success', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.playerCurrencyBalances = [{ currencyCode: 'EUR', currencySymbol: '€', balance: 50000 }]
    state.goldBalance = { balance: 10.0, blockedInPools: 0, availableBalance: 10.0 }
    // No existing pools so Create Pool form is shown
    state.goldAmmPools = []
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=gold')

    await page.getByRole('tab', { name: 'Add Liquidity' }).click()

    const addSection = page.locator('[aria-label="Add Liquidity"]')
    await expect(addSection.getByText('Create New Pool')).toBeVisible()

    // Fill in pool creation form
    const inputs = addSection.locator('input[type="number"]')
    await inputs.nth(0).fill('5000')
    await inputs.nth(1).fill('2.5')

    await addSection.getByRole('button', { name: 'Create Pool' }).click()

    // Success message appears (rendered outside the collapsed showCreateForm block)
    await expect(page.locator('[aria-label="Add Liquidity"]').locator('[role="status"]')).toBeVisible()
    await expect(page.locator('[aria-label="Add Liquidity"]').locator('[role="status"]')).toContainText('Liquidity pool created')
  })
})

// ── FX Rate History Chart tests ───────────────────────────────────────────────

test.describe('FX Rate History Chart on Rate List tab', () => {
  function setupChartTab(page: Parameters<typeof setupMockApi>[0], withHistory = true) {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    if (withHistory) {
      state.fxRateHistorySnapshots = makeFxRateHistory('CZK', 25.19, 20, 1)
    }
    return { player, state }
  }

  test('shows rate chart section on Rate List tab', async ({ page }) => {
    const { player } = setupChartTab(page)
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=rates')
    await expect(page.locator('.rates-chart-section')).toBeVisible()
  })

  test('shows chart legend with buy, mid, sell labels', async ({ page }) => {
    const { player } = setupChartTab(page)
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=rates')
    await expect(page.locator('.chart-legend')).toBeVisible()
    await expect(page.locator('.legend-buy')).toBeVisible()
    await expect(page.locator('.legend-mid')).toBeVisible()
    await expect(page.locator('.legend-sell')).toBeVisible()
  })

  test('shows pair selector with stronger-first roadmap pair format option (EURCZK)', async ({ page }) => {
    const { player } = setupChartTab(page)
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=rates')
    const selector = page.locator('.chart-pair-selector')
    await expect(selector).toBeVisible()
    await expect(selector).toContainText('EURCZK')
  })

  test('shows time range buttons (24h, 7d, 30d)', async ({ page }) => {
    const { player } = setupChartTab(page)
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=rates')
    await expect(page.getByRole('button', { name: '24h' })).toBeVisible()
    await expect(page.getByRole('button', { name: '7d' })).toBeVisible()
    await expect(page.getByRole('button', { name: '30d' })).toBeVisible()
  })

  test('shows empty state message when no history data available', async ({ page }) => {
    const { player } = setupChartTab(page, false)
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=rates')
    await expect(page.locator('.chart-empty')).toBeVisible()
  })

  test('rate table shows buy, mid and sell column headers', async ({ page }) => {
    const { player } = setupChartTab(page)
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=rates')
    const table = page.locator('.rates-table')
    await expect(table).toBeVisible()
    await expect(table.locator('thead').getByText('Mid rate')).toBeVisible()
    await expect(table.locator('thead').getByText('Buy')).toBeVisible()
    await expect(table.locator('thead').getByText('Sell')).toBeVisible()
  })

  test('rate table shows compact stronger-first forex pairs without slash formatting', async ({ page }) => {
    const { player } = setupChartTab(page)
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=rates')
    const table = page.locator('.rates-table')
    await expect(table).toBeVisible()
    await expect(table.locator('.rate-pair-label').filter({ hasText: 'EURCZK' })).toHaveCount(1)
    await expect(table.locator('.rate-pair-label').filter({ hasText: 'EURUSD' })).toHaveCount(1)
    await expect(table.locator('.rate-pair-label').filter({ hasText: '/' })).toHaveCount(0)
  })

  test('Prague context shows stronger-first pair labels USDCZK and EURCZK', async ({ page }) => {
    const player = makePlayer({
      companies: [
        {
          id: 'company-prague-pairs',
          playerId: 'player-1',
          name: 'Prague Pair Corp',
          cash: 500000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [
            {
              id: 'building-prague-pairs',
              companyId: 'company-prague-pairs',
              cityId: 'city-pr',
              type: 'FACTORY',
              name: 'Prague Pair Factory',
              latitude: 50.08,
              longitude: 14.44,
              level: 1,
              powerConsumption: 10,
              isForSale: false,
              units: [],
              pendingConfiguration: null,
            },
          ],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript(
      ({ token, cityId }) => {
        localStorage.setItem('auth_token', token)
        localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
        localStorage.setItem('selected_city_id', cityId)
      },
      { token: `token-${player.id}`, cityId: 'city-pr' },
    )
    await page.goto('/forex?tab=rates')
    const table = page.locator('.rates-table')
    await expect(table).toBeVisible()
    await expect(table.locator('.rate-pair-label').filter({ hasText: 'USDCZK' })).toHaveCount(1)
    await expect(table.locator('.rate-pair-label').filter({ hasText: 'EURCZK' })).toHaveCount(1)
  })

  test('Vienna context shows stronger-first pair labels EURUSD and EURCZK', async ({ page }) => {
    const player = makePlayer({
      companies: [
        {
          id: 'company-vienna-pairs',
          playerId: 'player-1',
          name: 'Vienna Pair Corp',
          cash: 500000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [
            {
              id: 'building-vienna-pairs',
              companyId: 'company-vienna-pairs',
              cityId: 'city-vi',
              type: 'FACTORY',
              name: 'Vienna Pair Factory',
              latitude: 48.21,
              longitude: 16.38,
              level: 1,
              powerConsumption: 10,
              isForSale: false,
              units: [],
              pendingConfiguration: null,
            },
          ],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript(
      ({ token, cityId }) => {
        localStorage.setItem('auth_token', token)
        localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
        localStorage.setItem('selected_city_id', cityId)
      },
      { token: `token-${player.id}`, cityId: 'city-vi' },
    )
    await page.goto('/forex?tab=rates')
    const table = page.locator('.rates-table')
    await expect(table).toBeVisible()
    await expect(table.locator('.rate-pair-label').filter({ hasText: 'EURUSD' })).toHaveCount(1)
    await expect(table.locator('.rate-pair-label').filter({ hasText: 'EURCZK' })).toHaveCount(1)
  })

  test('rates table is rendered above chart in DOM order', async ({ page }) => {
    const { player } = setupChartTab(page)
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=rates')
    await expect(page.locator('.rates-table')).toBeVisible()
    await expect(page.locator('.rates-chart-section')).toBeVisible()
    const order = await page.evaluate(() => {
      const table = document.querySelector('.rates-table')
      const chart = document.querySelector('.rates-chart-section')
      if (!table || !chart) return null
      const position = table.compareDocumentPosition(chart)
      return Boolean(position & Node.DOCUMENT_POSITION_FOLLOWING)
    })
    expect(order).not.toBeNull()
    expect(order).toBe(true)
  })

  test('mobile rates table stacks buy, mid, and sell values inside each pair row', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 })
    const { player } = setupChartTab(page)
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=rates')
    const firstRow = page.locator('.rates-table tbody tr').first()
    await expect(firstRow).toBeVisible()
    await expect(firstRow).toContainText('Buy')
    await expect(firstRow).toContainText('Mid rate')
    await expect(firstRow).toContainText('Sell')
  })

  test('chart SVG renders polylines when data is seeded', async ({ page }) => {
    const { player } = setupChartTab(page)
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=rates')
    const svg = page.locator('.chart-svg')
    await expect(svg).toBeVisible()
    const polylines = svg.locator('polyline')
    await expect(polylines).not.toHaveCount(0)
  })

  test('rate chart section is visible on mobile viewport', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 })
    const { player } = setupChartTab(page)
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=rates')
    await expect(page.locator('.rates-chart-section')).toBeVisible()
    await expect(page.locator('.chart-pair-selector')).toBeVisible()
  })

  test('mobile rates view has no horizontal overflow', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 })
    const { player } = setupChartTab(page)
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex?tab=rates')
    const hasOverflow = await page.evaluate(() => {
      const root = document.documentElement
      return root.scrollWidth > root.clientWidth
    })
    expect(hasOverflow).toBe(false)
  })
})

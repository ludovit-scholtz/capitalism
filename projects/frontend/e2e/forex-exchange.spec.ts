import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer } from './helpers/mock-api'

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

  test('shows live rates on the Rate List tab', async ({ page }) => {
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
    ]
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/forex')

    await page.getByRole('tab', { name: 'Rate List' }).click()

    await expect(page.getByRole('heading', { name: 'Live Rate List' })).toBeVisible()
    await expect(page.locator('.rates-table')).toContainText('EUR/CZK')
    await expect(page.locator('.rates-table')).toContainText('25.1234')
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
        companyId: player.companies[0]?.id ?? 'comp-1',
        companyName: 'My Corp',
      },
      {
        id: 'ba-czk-001',
        accountNumber: '2222222222222222',
        currencyCode: 'CZK',
        currencySymbol: 'Kč',
        balance: 80000,
        companyId: player.companies[0]?.id ?? 'comp-1',
        companyName: 'My Corp',
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

  test('successfully completes a bank account swap and shows result', async ({ page }) => {
    const player = makePlayer()
    player.personalCash = 0
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    const companyId = player.companies[0]?.id ?? 'comp-1'
    state.myBankAccounts = [
      {
        id: 'ba-eur-swap',
        accountNumber: '3333333333333333',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 10000,
        companyId,
        companyName: 'Swap Corp',
      },
      {
        id: 'ba-czk-swap',
        accountNumber: '4444444444444444',
        currencyCode: 'CZK',
        currencySymbol: 'Kč',
        balance: 0,
        companyId,
        companyName: 'Swap Corp',
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
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    const companyId = player.companies[0]?.id ?? 'comp-1'
    state.myBankAccounts = [
      {
        id: 'ba-low-eur',
        accountNumber: '5555555555555555',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 50,
        companyId,
        companyName: 'Low Corp',
      },
      {
        id: 'ba-czk-low',
        accountNumber: '6666666666666666',
        currencyCode: 'CZK',
        currencySymbol: 'Kč',
        balance: 0,
        companyId,
        companyName: 'Low Corp',
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
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    const companyId = player.companies[0]?.id ?? 'comp-1'
    state.myBankAccounts = [
      {
        id: 'ba-bal-test',
        accountNumber: '7777777777777777',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 3750.50,
        companyId,
        companyName: 'Balance Corp',
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
})

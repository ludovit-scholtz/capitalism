import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer } from './helpers/mock-api'

// ── Bank Account Close feature (Loan Marketplace → Accounts tab) ─────────────

const COMPANY_ID = 'close-test-company-1'

test.describe('Bank Account Close', () => {
  async function authenticatedSetup(page: import('@playwright/test').Page) {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    player.companies.push({
      id: COMPANY_ID,
      playerId: player.id,
      name: 'Close Test Corp',
      cash: 0,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    return state
  }

  test('shows ready-to-close badge for zero-balance non-deposit account', async ({ page }) => {
    const state = await authenticatedSetup(page)
    state.myBankAccounts = [
      {
        id: 'acc-zero-eur',
        accountNumber: '1111222233334444',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 0,
        companyId: COMPANY_ID,
        companyName: 'Close Test Corp',
        bankBuildingId: null,
        cityId: 'city-ba',
      },
    ]

    await page.goto('/loans')
    await page.getByRole('tab', { name: 'Accounts' }).click()

    const card = page.locator('[data-testid="bank-account-row"]').first()
    await expect(card.locator('.account-ready-close')).toBeVisible()
    await expect(card.getByRole('button', { name: 'Close Account' })).toBeVisible()
  })

  test('shows non-zero balance hint and no close button for funded account', async ({ page }) => {
    const state = await authenticatedSetup(page)
    state.myBankAccounts = [
      {
        id: 'acc-funded-eur',
        accountNumber: '5555666677778888',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 12345,
        companyId: COMPANY_ID,
        companyName: 'Close Test Corp',
        bankBuildingId: null,
        cityId: 'city-ba',
      },
    ]

    await page.goto('/loans')
    await page.getByRole('tab', { name: 'Accounts' }).click()

    const card = page.locator('[data-testid="bank-account-row"]').first()
    await expect(card.locator('.account-nonzero-hint')).toBeVisible()
    await expect(card.getByRole('button', { name: 'Close Account' })).toBeHidden()
  })

  test('closes zero-balance account and removes it from the list on confirm', async ({ page }) => {
    const state = await authenticatedSetup(page)
    state.myBankAccounts = [
      {
        id: 'acc-zero-czk',
        accountNumber: '9999000011112222',
        currencyCode: 'CZK',
        currencySymbol: 'Kč',
        balance: 0,
        companyId: COMPANY_ID,
        companyName: 'Close Test Corp',
        bankBuildingId: null,
        cityId: 'city-pr',
      },
    ]

    await page.goto('/loans')
    await page.getByRole('tab', { name: 'Accounts' }).click()

    await expect(page.locator('[data-testid="bank-account-row"]')).toHaveCount(1)

    // Accept the confirm dialog
    page.once('dialog', (dialog) => dialog.accept())
    await page.locator('[data-testid="bank-account-row"]').first().getByRole('button', { name: 'Close Account' }).click()

    // After closure the account list should be empty
    await expect(page.locator('[data-testid="bank-account-row"]')).toHaveCount(0)
  })

  test('shows inline error when server returns NON_ZERO_BALANCE', async ({ page }) => {
    const state = await authenticatedSetup(page)
    state.myBankAccounts = [
      {
        id: 'acc-zero-eur-trick',
        accountNumber: '3333444455556666',
        currencyCode: 'EUR',
        currencySymbol: '€',
        // Balance shown as 0 so button is visible, but mock will reject at server
        balance: 0,
        companyId: COMPANY_ID,
        companyName: 'Close Test Corp',
        bankBuildingId: null,
        cityId: 'city-ba',
      },
    ]

    // Override only the closeCompanyBankAccount mutation to simulate server-side rejection
    await page.route('**/graphql', async (route) => {
      const body = JSON.parse((await route.request().postData()) ?? '{}')
      const query: string = body?.query ?? ''
      if (query.includes('closeCompanyBankAccount')) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              {
                message: 'The account balance must be exactly zero before it can be closed.',
                extensions: { code: 'NON_ZERO_BALANCE' },
              },
            ],
          }),
        })
      }
      return route.fallback()
    })

    await page.goto('/loans')
    await page.getByRole('tab', { name: 'Accounts' }).click()

    page.once('dialog', (dialog) => dialog.accept())
    await page.locator('[data-testid="bank-account-row"]').first().getByRole('button', { name: 'Close Account' }).click()

    const card = page.locator('[data-testid="bank-account-row"]').first()
    await expect(card.locator('.close-account-error')).toBeVisible()
  })

  test('deposit account with zero balance shows close button via deposit flow', async ({ page }) => {
    const state = await authenticatedSetup(page)
    state.myBankAccounts = [
      {
        id: 'acc-deposit-zero',
        accountNumber: '7777888899990000',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 0,
        companyId: COMPANY_ID,
        companyName: 'Close Test Corp',
        bankBuildingId: 'bank-building-1',
        isDepositAccount: true,
        cityId: 'city-ba',
      },
    ]

    await page.goto('/loans')
    await page.getByRole('tab', { name: 'Accounts' }).click()

    const card = page.locator('[data-testid="bank-account-row"]').first()
    // Deposit accounts do NOT show the ready-to-close badge or non-zero hint
    await expect(card.locator('.account-ready-close')).toBeHidden()
    await expect(card.locator('.account-nonzero-hint')).toBeHidden()
    // But they do show a close button (via the deposit-account branch)
    await expect(card.getByRole('button', { name: 'Close Account' })).toBeVisible()
  })
})

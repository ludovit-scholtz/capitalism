import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer } from './helpers/mock-api'

// ── Bank Account Transfer panel (Forex page → Transfer tab) ──────────────────

test.describe('Bank Account Transfer panel', () => {
  async function authenticatedSetup(page: import('@playwright/test').Page) {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.myBankAccounts = [
      {
        id: 'acc-eur-1',
        accountNumber: '1234567890123456',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 1000,
        companyId: 'company-a',
        companyName: 'Acme Operations',
      },
      {
        id: 'acc-eur-2',
        accountNumber: '6543210987654321',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 250,
        companyId: 'company-b',
        companyName: 'Acme Holdings',
      },
      {
        id: 'acc-czk-1',
        accountNumber: '1111222233334444',
        currencyCode: 'CZK',
        currencySymbol: 'Kč',
        balance: 75000,
        companyId: 'company-a',
        companyName: 'Acme Operations',
      },
    ]
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    return state
  }

  test('Transfer tab is visible and shows the panel heading', async ({ page }) => {
    await authenticatedSetup(page)
    await page.goto('/forex')

    const tab = page.getByRole('tab', { name: 'Transfer' })
    await expect(tab).toBeVisible()
    await tab.click()
    await expect(page.getByRole('heading', { name: 'Transfer Between My Bank Accounts' })).toBeVisible()
  })

  test('successful same-currency transfer shows confirmation', async ({ page }) => {
    await authenticatedSetup(page)
    await page.goto('/forex')

    await page.getByRole('tab', { name: 'Transfer' }).click()
    await page.locator('#bank-transfer-from').selectOption('acc-eur-1')
    await page.locator('#bank-transfer-to').selectOption('acc-eur-2')
    await page.locator('#bank-transfer-amount').fill('300')

    const submit = page.getByRole('button', { name: 'Transfer Funds' })
    await expect(submit).toBeEnabled()
    await submit.click()

    await expect(page.getByRole('status')).toContainText('300.00 EUR')
    await expect(page.getByRole('status')).toContainText('Acme Operations')
    await expect(page.getByRole('status')).toContainText('Acme Holdings')
  })

  test('blocks submit when amount exceeds source balance', async ({ page }) => {
    await authenticatedSetup(page)
    await page.goto('/forex')

    await page.getByRole('tab', { name: 'Transfer' }).click()
    await page.locator('#bank-transfer-from').selectOption('acc-eur-2')
    await page.locator('#bank-transfer-to').selectOption('acc-eur-1')
    await page.locator('#bank-transfer-amount').fill('5000')

    await expect(page.getByRole('alert')).toContainText('does not have enough funds')
    await expect(page.getByRole('button', { name: 'Transfer Funds' })).toBeDisabled()
  })

  test('destination selector hides accounts in a different currency', async ({ page }) => {
    await authenticatedSetup(page)
    await page.goto('/forex')

    await page.getByRole('tab', { name: 'Transfer' }).click()
    await page.locator('#bank-transfer-from').selectOption('acc-czk-1')

    const toOptions = await page.locator('#bank-transfer-to option').allTextContents()
    // Only the empty placeholder should remain (no second CZK account exists in fixture)
    expect(toOptions.filter((t) => t.includes('Acme'))).toHaveLength(0)
    await expect(page.getByRole('alert')).toContainText('No other bank account in the same currency')
  })
})

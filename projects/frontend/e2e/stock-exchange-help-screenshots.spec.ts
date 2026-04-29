import { expect, test, type Page } from '@playwright/test'
import { makePlayer, setupMockApi } from './helpers/mock-api.js'
import path from 'node:path'
import fs from 'node:fs'
import { fileURLToPath } from 'node:url'

const currentDir = path.dirname(fileURLToPath(import.meta.url))
const PUBLIC_OUTPUT_DIR = path.resolve(currentDir, '../public/stock-exchange-help')
const DOCS_OUTPUT_DIR = path.resolve(currentDir, '../docs/screenshots/encyclopedia-help')

function imagePaths(fileName: string) {
  return {
    publicPath: path.join(PUBLIC_OUTPUT_DIR, fileName),
    docsPath: path.join(DOCS_OUTPUT_DIR, fileName),
  }
}

async function saveScreenshot(page: Page, fileName: string) {
  const { publicPath, docsPath } = imagePaths(fileName)
  await page.screenshot({ path: publicPath })
  fs.copyFileSync(publicPath, docsPath)
  expect(fs.existsSync(publicPath)).toBeTruthy()
  expect(fs.existsSync(docsPath)).toBeTruthy()
}

async function switchNavbarAccount(page: Page, accountName: string) {
  const switcher = page.locator('.ctx-switcher, .account-switcher')
  await switcher.locator('.ctx-trigger, .account-trigger').click()
  await switcher.getByRole('menuitemradio', { name: new RegExp(accountName) }).click()
}

async function openTradePanel(page: Page, companyName: string) {
  const row = page.locator('tr.listing-row', { hasText: companyName })
  const tradeButton = row.getByRole('button', { name: 'Trade' })
  if ((await tradeButton.count()) > 0) {
    await tradeButton.click()
  }
}

test.describe('Stock exchange help screenshots', () => {
  test('captures real FullHD walkthrough screenshots for IPO, trading, forex, tax ledger, and dividend flow', async ({ page }) => {
    fs.mkdirSync(PUBLIC_OUTPUT_DIR, { recursive: true })
    fs.mkdirSync(DOCS_OUTPUT_DIR, { recursive: true })

    const player = makePlayer({
      displayName: 'Founder Alex',
      onboardingCompletedAtUtc: '2026-01-01T12:00:00Z',
      personalCash: 350_000,
      personalTaxReserve: 24_500,
      dividendPayments: [
        {
          id: 'div-1',
          companyId: 'company-rival',
          companyName: 'Rival Dynamics',
          shareCount: 320,
          amountPerShare: 2.8,
          totalAmount: 896,
          gameYear: 2001,
          recordedAtTick: 9042,
          recordedAtUtc: '2026-01-03T10:00:00Z',
          description: 'Annual dividend payout',
        },
      ],
      stockTrades: [
        {
          id: 'trade-1',
          companyId: 'company-rival',
          companyName: 'Rival Dynamics',
          direction: 'SELL',
          shareCount: 120,
          pricePerShare: 94,
          totalValue: 11280,
          recordedAtTick: 9051,
          recordedAtUtc: '2026-01-03T12:00:00Z',
        },
        {
          id: 'trade-2',
          companyId: 'company-rival',
          companyName: 'Rival Dynamics',
          direction: 'BUY',
          shareCount: 200,
          pricePerShare: 88,
          totalValue: 17600,
          recordedAtTick: 9002,
          recordedAtUtc: '2026-01-03T08:00:00Z',
        },
      ],
      companies: [
        {
          id: 'company-home',
          playerId: 'player-1',
          name: 'Home Holdings',
          cash: 1_250_000,
          totalSharesIssued: 10000,
          dividendPayoutRatio: 0.2,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          foundedAtTick: 42,
          buildings: [],
        },
      ],
    })

    const rival = makePlayer({
      id: 'player-2',
      email: 'rival@test.com',
      displayName: 'Rival Owner',
      onboardingCompletedAtUtc: '2026-01-01T12:00:00Z',
      companies: [
        {
          id: 'company-rival',
          playerId: 'player-2',
          name: 'Rival Dynamics',
          cash: 1_500_000,
          totalSharesIssued: 10000,
          dividendPayoutRatio: 0.35,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          foundedAtTick: 42,
          buildings: [],
        },
      ],
    })

    const state = setupMockApi(page, {
      players: [player, rival],
      shareholdings: [
        { companyId: 'company-home', ownerPlayerId: 'player-1', ownerCompanyId: null, shareCount: 9000 },
        { companyId: 'company-rival', ownerPlayerId: 'player-2', ownerCompanyId: null, shareCount: 7600 },
        { companyId: 'company-rival', ownerPlayerId: 'player-1', ownerCompanyId: null, shareCount: 320 },
        { companyId: 'company-rival', ownerPlayerId: null, ownerCompanyId: 'company-home', shareCount: 600 },
      ],
    })

    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.myBankAccounts = [
      {
        id: 'bank-person-eur',
        accountNumber: '1000000000000001',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 150_000,
        ownerType: 'PERSON',
        ownerDisplayName: player.displayName,
      },
      {
        id: 'bank-person-usd',
        accountNumber: '1000000000000002',
        currencyCode: 'USD',
        currencySymbol: '$',
        balance: 80_000,
        ownerType: 'PERSON',
        ownerDisplayName: player.displayName,
      },
      {
        id: 'bank-company-eur',
        accountNumber: '2000000000000001',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 340_000,
        ownerType: 'COMPANY',
        ownerDisplayName: 'Home Holdings',
        companyId: 'company-home',
        companyName: 'Home Holdings',
      },
      {
        id: 'bank-company-usd',
        accountNumber: '2000000000000002',
        currencyCode: 'USD',
        currencySymbol: '$',
        balance: 420_000,
        ownerType: 'COMPANY',
        ownerDisplayName: 'Home Holdings',
        companyId: 'company-home',
        companyName: 'Home Holdings',
      },
    ]

    await page.setViewportSize({ width: 1920, height: 1080 })

    await page.goto('/onboarding')
    await page.locator('.industry-card', { hasText: 'Furniture' }).click()
    await page.locator('.product-card', { hasText: 'Wooden Chair' }).click()
    await page.locator('.city-card', { hasText: 'Bratislava' }).click()
    await expect(page.getByRole('heading', { name: 'Choose Your IPO Plan' })).toBeVisible()
    await saveScreenshot(page, 'step-1-ipo-plan-1920x1080.png')

    await page.evaluate((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/stocks')
    await expect(page.getByRole('heading', { name: 'Stock Exchange' })).toBeVisible()

    await switchNavbarAccount(page, 'Home Holdings')
    await openTradePanel(page, 'Rival Dynamics')
    await expect(page.locator('.trade-panel')).toBeVisible()
    await saveScreenshot(page, 'step-2-company-buy-shares-1920x1080.png')

    await switchNavbarAccount(page, 'Founder Alex')
    await openTradePanel(page, 'Rival Dynamics')
    await expect(page.locator('.trade-panel')).toBeVisible()
    await saveScreenshot(page, 'step-3-personal-buy-shares-1920x1080.png')

    await page.getByRole('spinbutton', { name: /Share quantity/i }).fill('120')
    await saveScreenshot(page, 'step-4-sell-shares-1920x1080.png')

    await page.goto('/forex')
    await expect(page.locator('.forex-hero').getByRole('heading', { name: 'Forex Exchange' })).toBeVisible()
    await page.getByLabel('Source account').selectOption({ index: 0 })
    await page.getByLabel('Destination account').selectOption({ index: 1 })
    await page.getByLabel('Amount').fill('15000')
    await page.getByRole('button', { name: 'Get Quote' }).click()
    await saveScreenshot(page, 'step-5-usd-forex-swap-1920x1080.png')

    await page.goto('/personal-ledger')
    await expect(page.getByRole('heading', { name: 'Personal Ledger' })).toBeVisible()
    await saveScreenshot(page, 'step-6-tax-reserve-ledger-1920x1080.png')

    await page.goto('/company/company-home/settings')
    await expect(page.getByRole('heading', { name: 'Home Holdings' })).toBeVisible()
    await saveScreenshot(page, 'step-7-dividend-config-company-settings-1920x1080.png')

    await page.goto('/personal-ledger')
    await expect(page.getByRole('heading', { name: /Dividend (income|history)/i })).toBeVisible()
    await page.getByRole('heading', { name: /Dividend (income|history)/i }).scrollIntoViewIfNeeded()
    await saveScreenshot(page, 'step-8-dividend-effects-personal-account-1920x1080.png')
  })
})

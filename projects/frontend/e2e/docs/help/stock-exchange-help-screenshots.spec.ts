import { expect, test, type Page } from '@playwright/test'
import { makePlayer, setupMockApi } from '../../helpers/mock-api.js'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { SCREENSHOT_LOCALES, openLocalizedScreenshotPage, saveLocalizedScreenshot, type ScreenshotLocale } from '../helpers/localized-screenshot.js'

const currentDir = path.dirname(fileURLToPath(import.meta.url))
const PUBLIC_OUTPUT_DIR = path.resolve(currentDir, '../../../public/stock-exchange-help')
const DOCS_OUTPUT_DIR = path.resolve(currentDir, '../../../docs/screenshots/encyclopedia-help')

async function saveScreenshot(page: Page, locale: ScreenshotLocale, fileName: string) {
  await saveLocalizedScreenshot(page, locale, fileName, PUBLIC_OUTPUT_DIR, [DOCS_OUTPUT_DIR], [PUBLIC_OUTPUT_DIR, DOCS_OUTPUT_DIR])
}

async function switchNavbarAccount(page: Page, accountName: string) {
  const switcher = page.locator('.ctx-switcher, .account-switcher')
  await switcher.locator('.ctx-trigger, .account-trigger').click()
  await switcher.getByRole('menuitemradio', { name: new RegExp(accountName) }).click()
}

async function openTradePanel(page: Page, companyName: string) {
  const existingPanel = page.locator('.trade-panel').first()
  if (await existingPanel.isVisible().catch(() => false)) {
    return
  }

  const row = page.locator('tr.listing-row', { hasText: companyName })
  await row.getByRole('button', { name: /Trade|Obchodovať|Handeln/i }).click()
}

test.describe('Stock exchange help screenshots', () => {
  for (const locale of SCREENSHOT_LOCALES) {
    test(`captures real FullHD walkthrough screenshots for IPO, trading, forex, tax ledger, and dividend flow (${locale})`, async ({ page }) => {
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

      const localizedPage = await openLocalizedScreenshotPage(page.context(), locale)

      try {
        const state = setupMockApi(localizedPage, {
          players: [player, rival],
          shareholdings: [
            { companyId: 'company-home', ownerPlayerId: 'player-1', ownerCompanyId: null, shareCount: 9000 },
            { companyId: 'company-rival', ownerPlayerId: 'player-2', ownerCompanyId: null, shareCount: 7600 },
            { companyId: 'company-rival', ownerPlayerId: 'player-1', ownerCompanyId: null, shareCount: 320 },
            { companyId: 'company-rival', ownerPlayerId: null, ownerCompanyId: 'company-home', shareCount: 600 },
          ],
        })

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

        await localizedPage.goto('/onboarding')
        await expect(localizedPage.locator('.city-card').first()).toBeVisible()
        await localizedPage.locator('.city-card', { hasText: 'Bratislava' }).click()
        await expect(localizedPage.locator('.industry-card').first()).toBeVisible()
        await localizedPage.locator('.industry-card').first().click()
        await expect(localizedPage.locator('.product-card').first()).toBeVisible()
        await localizedPage.locator('.product-card').first().click()
        await expect(localizedPage.locator('.ipo-card').first()).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'step-1-ipo-plan-1920x1080.png')

        state.currentUserId = player.id
        state.currentToken = `token-${player.id}`
        await localizedPage.evaluate((token) => {
          localStorage.setItem('auth_token', token)
          localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
        }, `token-${player.id}`)

        await localizedPage.goto('/stocks')
        await expect(localizedPage.locator('tr.listing-row').first()).toBeVisible()

        await switchNavbarAccount(localizedPage, 'Home Holdings')
        await openTradePanel(localizedPage, 'Rival Dynamics')
        await expect(localizedPage.locator('.trade-panel')).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'step-2-company-buy-shares-1920x1080.png')

        await switchNavbarAccount(localizedPage, 'Founder Alex')
        await openTradePanel(localizedPage, 'Rival Dynamics')
        await expect(localizedPage.locator('.trade-panel')).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'step-3-personal-buy-shares-1920x1080.png')

        await localizedPage.locator('.trade-panel').getByRole('spinbutton').first().fill('120')
        await saveScreenshot(localizedPage, locale, 'step-4-sell-shares-1920x1080.png')

        await localizedPage.goto('/forex')
        await expect(localizedPage.locator('.forex-hero h1')).toBeVisible()
        await localizedPage.locator('#from-bank-account').selectOption('bank-person-eur')
        await localizedPage.locator('#to-bank-account').selectOption('bank-person-usd')
        await localizedPage.locator('#swap-amount').fill('15000')
        await localizedPage
          .getByRole('button', { name: /Get Quote|Získať|Angebot/i })
          .first()
          .click()
        await expect(localizedPage.getByRole('region', { name: 'Exchange Quote' })).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'step-5-usd-forex-swap-1920x1080.png')

        await localizedPage.goto('/personal-ledger')
        await expect(localizedPage.locator('.tax-cell').first()).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'step-6-tax-reserve-ledger-1920x1080.png')

        await localizedPage.goto('/company/company-home/settings')
        await expect(localizedPage.getByRole('heading', { name: 'Home Holdings' })).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'step-7-dividend-config-company-settings-1920x1080.png')

        await localizedPage.goto('/personal-ledger')
        const dividendTable = localizedPage
          .locator('table')
          .filter({ has: localizedPage.locator('tr', { hasText: '2001' }) })
          .first()
        await expect(dividendTable).toBeVisible()
        await dividendTable.scrollIntoViewIfNeeded()
        await saveScreenshot(localizedPage, locale, 'step-8-dividend-effects-personal-account-1920x1080.png')
      } finally {
        await localizedPage.close()
      }
    })
  }
})

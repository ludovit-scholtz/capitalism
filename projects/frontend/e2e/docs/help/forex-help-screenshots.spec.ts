import { expect, test, type Page } from '@playwright/test'
import { makePlayer, setupMockApi } from '../../helpers/mock-api.js'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { SCREENSHOT_LOCALES, openLocalizedScreenshotPage, saveLocalizedScreenshot, type ScreenshotLocale } from '../helpers/localized-screenshot.js'

const currentDir = path.dirname(fileURLToPath(import.meta.url))
const PUBLIC_OUTPUT_DIR = path.resolve(currentDir, '../../../public/forex-help')
const DOCS_OUTPUT_DIR = path.resolve(currentDir, '../../../docs/screenshots/encyclopedia-help')

async function saveScreenshot(page: Page, locale: ScreenshotLocale, fileName: string) {
  await saveLocalizedScreenshot(page, locale, fileName, PUBLIC_OUTPUT_DIR, [DOCS_OUTPUT_DIR], [PUBLIC_OUTPUT_DIR, DOCS_OUTPUT_DIR])
}

test.describe('Forex help screenshots', () => {
  for (const locale of SCREENSHOT_LOCALES) {
    test(`captures real FullHD walkthrough screenshots for swap, transfer, rates, history, and gold AMM (${locale})`, async ({ page }) => {
      const player = makePlayer({
        displayName: 'Treasury Alex',
        onboardingCompletedAtUtc: '2026-01-01T12:00:00Z',
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

      const localizedPage = await openLocalizedScreenshotPage(page.context(), locale)

      try {
        const state = setupMockApi(localizedPage, {
          players: [player],
        })

        state.currentUserId = player.id
        state.currentToken = `token-${player.id}`

        await localizedPage.addInitScript((token) => {
          localStorage.setItem('auth_token', token)
          localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
        }, `token-${player.id}`)

        state.myBankAccounts = [
          {
            id: 'bank-person-eur-primary',
            accountNumber: '1000000000000011',
            currencyCode: 'EUR',
            currencySymbol: '€',
            balance: 220_000,
            ownerType: 'PERSON',
            ownerDisplayName: player.displayName,
          },
          {
            id: 'bank-person-eur-secondary',
            accountNumber: '1000000000000013',
            currencyCode: 'EUR',
            currencySymbol: '€',
            balance: 45_000,
            ownerType: 'PERSON',
            ownerDisplayName: `${player.displayName} Savings`,
          },
          {
            id: 'bank-person-usd',
            accountNumber: '1000000000000012',
            currencyCode: 'USD',
            currencySymbol: '$',
            balance: 85_000,
            ownerType: 'PERSON',
            ownerDisplayName: player.displayName,
          },
          {
            id: 'bank-company-eur-primary',
            accountNumber: '2000000000000011',
            currencyCode: 'EUR',
            currencySymbol: '€',
            balance: 480_000,
            ownerType: 'COMPANY',
            ownerDisplayName: 'Home Holdings',
            companyId: 'company-home',
            companyName: 'Home Holdings',
          },
          {
            id: 'bank-company-eur-secondary',
            accountNumber: '2000000000000012',
            currencyCode: 'EUR',
            currencySymbol: '€',
            balance: 135_000,
            ownerType: 'COMPANY',
            ownerDisplayName: 'Home Holdings',
            companyId: 'company-home',
            companyName: 'Home Holdings',
          },
          {
            id: 'bank-company-usd',
            accountNumber: '2000000000000014',
            currencyCode: 'USD',
            currencySymbol: '$',
            balance: 95_000,
            ownerType: 'COMPANY',
            ownerDisplayName: 'Home Holdings',
            companyId: 'company-home',
            companyName: 'Home Holdings',
          },
        ]

        state.forexTradeHistory = [
          {
            id: 'fx-1',
            fromCurrencyCode: 'EUR',
            fromCurrencySymbol: '€',
            fromAmount: 10000,
            toCurrencyCode: 'USD',
            toCurrencySymbol: '$',
            toAmount: 10890,
            rate: 1.1,
            feeAmount: 100,
            feeRate: 0.01,
            executedAtTick: 9302,
          },
          {
            id: 'fx-2',
            fromCurrencyCode: 'USD',
            fromCurrencySymbol: '$',
            fromAmount: 5000,
            toCurrencyCode: 'EUR',
            toCurrencySymbol: '€',
            toAmount: 4450,
            rate: 0.9,
            feeAmount: 50,
            feeRate: 0.01,
            executedAtTick: 9271,
          },
        ]

        state.goldBalance = {
          balance: 82.75,
          blockedInPools: 11.5,
          availableBalance: 71.25,
        }

        state.goldAmmPools = [
          {
            id: 'pool-usd',
            currencyCode: 'USD',
            currencySymbol: '$',
            fiatReserve: 1_500_000,
            goldReserve: 750,
            totalLiquidityShares: 8_500,
            impliedGoldPrice: 2000,
            myPosition: {
              id: 'pos-usd-1',
              poolId: 'pool-usd',
              currencyCode: 'USD',
              liquidityShares: 620,
              sharePercent: 7.29,
              claimableFiat: 109350,
              claimableGold: 54.75,
              fiatProvided: 100000,
              goldProvided: 50,
            },
          },
          {
            id: 'pool-eur',
            currencyCode: 'EUR',
            currencySymbol: '€',
            fiatReserve: 1_300_000,
            goldReserve: 700,
            totalLiquidityShares: 7_900,
            impliedGoldPrice: 1857.14,
            myPosition: null,
          },
        ]

        await localizedPage.goto('/forex')
        await expect(localizedPage.locator('.forex-hero h1')).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'step-1-swap-overview-1920x1080.png')

        await localizedPage.locator('#from-bank-account').selectOption('bank-company-eur-primary')
        await localizedPage.locator('#to-bank-account').selectOption('bank-company-usd')
        await localizedPage.locator('#swap-amount').fill('15000')
        await localizedPage
          .getByRole('button', { name: /Get Quote|Získať|Angebot|Kurs abrufen/i })
          .first()
          .click()
        await expect(localizedPage.getByRole('region', { name: 'Exchange Quote' })).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'step-2-quote-and-confirm-1920x1080.png')

        await localizedPage.locator('[role="tab"]').nth(1).click()
        await expect(localizedPage.locator('#bank-transfer-from')).toBeVisible()
        await localizedPage.locator('#bank-transfer-from').selectOption('bank-company-eur-primary')
        await localizedPage.locator('#bank-transfer-to').selectOption('bank-company-eur-secondary')
        await localizedPage.locator('#bank-transfer-amount').fill('5000')
        await saveScreenshot(localizedPage, locale, 'step-3-account-transfer-1920x1080.png')

        await localizedPage.locator('[role="tab"]').nth(2).click()
        await expect(localizedPage.getByRole('region', { name: 'Rate List' })).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'step-4-fx-rates-board-1920x1080.png')

        await localizedPage.locator('[role="tab"]').nth(3).click()
        await expect(localizedPage.getByRole('region', { name: 'Trade History' })).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'step-5-swap-history-1920x1080.png')

        await localizedPage.locator('[role="tab"]').nth(4).click()
        await expect(localizedPage.getByRole('region', { name: 'Gold AMM Exchange' })).toBeVisible()
        await localizedPage.getByRole('spinbutton').first().fill('1000')
        await localizedPage
          .getByRole('button', { name: /Get Quote|Získať|Angebot|Kurs abrufen/i })
          .first()
          .click()
        await expect(localizedPage.getByRole('region', { name: 'Swap Quote' })).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'step-6-gold-amm-swap-1920x1080.png')

        await localizedPage.locator('.amm-tab').nth(1).click()
        await expect(localizedPage.getByLabel('My Liquidity Positions')).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'step-7-gold-amm-positions-1920x1080.png')

        await localizedPage.locator('.amm-tab').nth(2).click()
        await expect(localizedPage.locator('.form-select').first()).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'step-8-gold-amm-liquidity-1920x1080.png')
      } finally {
        await localizedPage.close()
      }
    })
  }
})

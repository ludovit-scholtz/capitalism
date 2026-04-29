import { expect, test, type Page } from '@playwright/test'
import { makePlayer, setupMockApi } from './helpers/mock-api.js'
import path from 'node:path'
import fs from 'node:fs'
import { fileURLToPath } from 'node:url'

const currentDir = path.dirname(fileURLToPath(import.meta.url))
const PUBLIC_OUTPUT_DIR = path.resolve(currentDir, '../public/forex-help')
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

test.describe('Forex help screenshots', () => {
  test('captures real FullHD walkthrough screenshots for swap, transfer, rates, history, and gold AMM', async ({ page }) => {
    fs.mkdirSync(PUBLIC_OUTPUT_DIR, { recursive: true })
    fs.mkdirSync(DOCS_OUTPUT_DIR, { recursive: true })

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

    const state = setupMockApi(page, {
      players: [player],
    })

    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    state.myBankAccounts = [
      {
        id: 'bank-person-eur',
        accountNumber: '1000000000000011',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 220_000,
        ownerType: 'PERSON',
        ownerDisplayName: player.displayName,
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
        id: 'bank-company-eur',
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
        id: 'bank-company-czk',
        accountNumber: '2000000000000012',
        currencyCode: 'CZK',
        currencySymbol: 'Kč',
        balance: 1_200_000,
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

    await page.setViewportSize({ width: 1920, height: 1080 })

    await page.goto('/forex')
    await expect(page.locator('.forex-hero').getByRole('heading', { name: 'Forex Exchange' })).toBeVisible()
    await saveScreenshot(page, 'step-1-swap-overview-1920x1080.png')

    await page.getByLabel('Source account').selectOption('bank-person-eur')
    await page.getByLabel('Destination account').selectOption('bank-person-usd')
    await page.getByLabel('Amount').fill('15000')
    await page.getByRole('button', { name: 'Get Quote' }).click()
    await expect(page.getByRole('region', { name: 'Exchange Quote' })).toBeVisible()
    await saveScreenshot(page, 'step-2-quote-and-confirm-1920x1080.png')

    await page.getByRole('tab', { name: /Transfers|Prevod|Transfer/i }).click()
    await expect(page.getByRole('region', { name: /Transfer/i })).toBeVisible()
    await page.locator('#bank-transfer-from').selectOption('bank-person-eur')
    await page.locator('#bank-transfer-to').selectOption('bank-company-eur')
    await page.locator('#bank-transfer-amount').fill('5000')
    await saveScreenshot(page, 'step-3-account-transfer-1920x1080.png')

    await page.getByRole('tab', { name: /Rate List|Kurzy|Ratenliste/i }).click()
    await expect(page.getByRole('region', { name: 'Rate List' })).toBeVisible()
    await saveScreenshot(page, 'step-4-fx-rates-board-1920x1080.png')

    await page.getByRole('tab', { name: /History|História|Historie/i }).click()
    await expect(page.getByRole('region', { name: 'Trade History' })).toBeVisible()
    await saveScreenshot(page, 'step-5-swap-history-1920x1080.png')

    await page.getByRole('tab', { name: /Gold AMM/i }).click()
    await expect(page.getByRole('region', { name: 'Gold AMM Exchange' })).toBeVisible()
    await page.getByRole('spinbutton').first().fill('1000')
    await page.getByRole('button', { name: /Get Quote|Získať|Quote/i }).click()
    await saveScreenshot(page, 'step-6-gold-amm-swap-1920x1080.png')

    await page.getByRole('tab', { name: /Positions|Pozície|Positionen/i }).click()
    await expect(page.getByText(/claimable|nárokovateľ|einforderbar/i).first()).toBeVisible()
    await saveScreenshot(page, 'step-7-gold-amm-positions-1920x1080.png')

    await page.getByRole('tab', { name: /Add Liquidity|Pridať likviditu|Liquidität hinzufügen/i }).click()
    await expect(page.getByText(/Add Liquidity|Pridať likviditu|Liquidität hinzufügen/i).first()).toBeVisible()
    await saveScreenshot(page, 'step-8-gold-amm-liquidity-1920x1080.png')
  })
})

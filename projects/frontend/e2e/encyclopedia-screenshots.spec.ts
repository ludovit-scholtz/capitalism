import { expect, test } from '@playwright/test'
import { setupMockApi, makeDefaultResources, makeDefaultProducts } from './helpers/mock-api.js'
import path from 'node:path'
import fs from 'node:fs'
import { fileURLToPath } from 'node:url'

const currentDir = path.dirname(fileURLToPath(import.meta.url))
const OUTPUT_DIR = path.resolve(currentDir, '../docs/screenshots/encyclopedia-help')

test.describe('Encyclopedia FullHD screenshots', () => {
  test('capture real 1920x1080 pages and fullscreen preview', async ({ page }) => {
    fs.mkdirSync(OUTPUT_DIR, { recursive: true })

    setupMockApi(page, {
      resourceTypes: makeDefaultResources(),
      productTypes: makeDefaultProducts(),
    })

    await page.setViewportSize({ width: 1920, height: 1080 })

    await page.goto('/encyclopedia/onboarding-help')
    const onboardingPath = path.join(OUTPUT_DIR, 'encyclopedia-onboarding-help-1920x1080.png')
    await page.screenshot({ path: onboardingPath })
    expect(fs.existsSync(onboardingPath)).toBeTruthy()

    await page.locator('.onboarding-help-card .help-image-trigger').first().click()
    const onboardingDialogPath = path.join(OUTPUT_DIR, 'encyclopedia-onboarding-help-fullscreen-dialog-1920x1080.png')
    await page.screenshot({ path: onboardingDialogPath })
    expect(fs.existsSync(onboardingDialogPath)).toBeTruthy()
    await page.getByRole('button', { name: /close preview|zavrieť náhľad|vorschau schließen/i }).click()

    await page.goto('/encyclopedia/factory-layout-help')
    const factoryLayoutPath = path.join(OUTPUT_DIR, 'encyclopedia-factory-layout-help-1920x1080.png')
    await page.screenshot({ path: factoryLayoutPath })
    expect(fs.existsSync(factoryLayoutPath)).toBeTruthy()

    await page.locator('.manufacturing-help-card .help-image-trigger').first().click()
    const factoryLayoutPurchaseDialogPath = path.join(OUTPUT_DIR, 'encyclopedia-factory-layout-help-purchase-fullscreen-1920x1080.png')
    await page.screenshot({ path: factoryLayoutPurchaseDialogPath })
    expect(fs.existsSync(factoryLayoutPurchaseDialogPath)).toBeTruthy()
    await page.getByRole('button', { name: /close preview|zavrieť náhľad|vorschau schließen/i }).click()

    await page.locator('.manufacturing-help-card .help-image-trigger').nth(2).click()
    const factoryLayoutStorageDialogPath = path.join(OUTPUT_DIR, 'encyclopedia-factory-layout-help-storage-fullscreen-1920x1080.png')
    await page.screenshot({ path: factoryLayoutStorageDialogPath })
    expect(fs.existsSync(factoryLayoutStorageDialogPath)).toBeTruthy()
    await page.getByRole('button', { name: /close preview|zavrieť náhľad|vorschau schließen/i }).click()

    await page.locator('.manufacturing-help-card .help-image-trigger').last().click()
    const factoryLayoutUnitTypesDialogPath = path.join(OUTPUT_DIR, 'encyclopedia-factory-layout-help-unit-types-fullscreen-1920x1080.png')
    await page.screenshot({ path: factoryLayoutUnitTypesDialogPath })
    expect(fs.existsSync(factoryLayoutUnitTypesDialogPath)).toBeTruthy()
    await page.getByRole('button', { name: /close preview|zavrieť náhľad|vorschau schließen/i }).click()

    await page.goto('/encyclopedia/resources-definition')
    const resourcesDefinitionPath = path.join(OUTPUT_DIR, 'encyclopedia-resources-definition-1920x1080.png')
    await page.screenshot({ path: resourcesDefinitionPath })
    expect(fs.existsSync(resourcesDefinitionPath)).toBeTruthy()

    await page.goto('/encyclopedia/sales-shop-help')
    const salesShopPath = path.join(OUTPUT_DIR, 'encyclopedia-sales-shop-help-1920x1080.png')
    await page.screenshot({ path: salesShopPath })
    expect(fs.existsSync(salesShopPath)).toBeTruthy()

    await page.locator('.sales-shop-help-card .help-image-trigger').first().click()
    const salesShopDialogPath = path.join(OUTPUT_DIR, 'encyclopedia-sales-shop-help-fullscreen-dialog-1920x1080.png')
    await page.screenshot({ path: salesShopDialogPath })
    expect(fs.existsSync(salesShopDialogPath)).toBeTruthy()

    await page.goto('/encyclopedia/stock-exchange-help')
    const stockExchangePath = path.join(OUTPUT_DIR, 'encyclopedia-stock-exchange-help-1920x1080.png')
    await page.screenshot({ path: stockExchangePath })
    expect(fs.existsSync(stockExchangePath)).toBeTruthy()

    await page.locator('.stock-exchange-help-card .help-image-trigger').first().click()
    const stockExchangeDialogPath = path.join(OUTPUT_DIR, 'encyclopedia-stock-exchange-help-fullscreen-dialog-1920x1080.png')
    await page.screenshot({ path: stockExchangeDialogPath })
    expect(fs.existsSync(stockExchangeDialogPath)).toBeTruthy()
  })
})

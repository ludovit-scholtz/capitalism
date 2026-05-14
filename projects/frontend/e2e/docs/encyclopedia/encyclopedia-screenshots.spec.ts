import { expect, test } from '@playwright/test'
import { setupMockApi, makeDefaultResources, makeDefaultProducts } from '../../helpers/mock-api.js'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { SCREENSHOT_LOCALES, openLocalizedScreenshotPage, saveLocalizedScreenshot, type ScreenshotLocale } from '../helpers/localized-screenshot.js'

const currentDir = path.dirname(fileURLToPath(import.meta.url))
const OUTPUT_DIR = path.resolve(currentDir, '../../../docs/screenshots/encyclopedia-help')

async function saveScreenshot(page: Parameters<typeof saveLocalizedScreenshot>[0], locale: ScreenshotLocale, fileName: string) {
  await saveLocalizedScreenshot(page, locale, fileName, OUTPUT_DIR, [], [OUTPUT_DIR])
}

test.describe('Encyclopedia FullHD screenshots', () => {
  for (const locale of SCREENSHOT_LOCALES) {
    test(`capture real 1920x1080 pages and fullscreen preview (${locale})`, async ({ page }) => {
      const localizedPage = await openLocalizedScreenshotPage(page.context(), locale)

      try {
        setupMockApi(localizedPage, {
          resourceTypes: makeDefaultResources(),
          productTypes: makeDefaultProducts(),
        })

        await localizedPage.goto('/encyclopedia/onboarding-help')
        await expect(localizedPage.locator('.onboarding-help-card').first()).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'encyclopedia-onboarding-help-1920x1080.png')

        await localizedPage.locator('.onboarding-help-card .help-image-trigger').first().click()
        await saveScreenshot(localizedPage, locale, 'encyclopedia-onboarding-help-fullscreen-dialog-1920x1080.png')
        await localizedPage.getByRole('button', { name: /close preview|zavrieť náhľad|vorschau schließen/i }).click()

        await localizedPage.goto('/encyclopedia/factory-layout-help')
        await expect(localizedPage.locator('.manufacturing-help-card').first()).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'encyclopedia-factory-layout-help-1920x1080.png')

        await localizedPage.locator('.manufacturing-help-card .help-image-trigger').first().click()
        await saveScreenshot(localizedPage, locale, 'encyclopedia-factory-layout-help-purchase-fullscreen-1920x1080.png')
        await localizedPage.getByRole('button', { name: /close preview|zavrieť náhľad|vorschau schließen/i }).click()

        await localizedPage.locator('.manufacturing-help-card .help-image-trigger').nth(2).click()
        await saveScreenshot(localizedPage, locale, 'encyclopedia-factory-layout-help-storage-fullscreen-1920x1080.png')
        await localizedPage.getByRole('button', { name: /close preview|zavrieť náhľad|vorschau schließen/i }).click()

        await localizedPage.locator('.manufacturing-help-card .help-image-trigger').last().click()
        await saveScreenshot(localizedPage, locale, 'encyclopedia-factory-layout-help-unit-types-fullscreen-1920x1080.png')
        await localizedPage.getByRole('button', { name: /close preview|zavrieť náhľad|vorschau schließen/i }).click()

        await localizedPage.goto('/encyclopedia/resources-definition')
        await expect(localizedPage.locator('.resource-card--link').first()).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'encyclopedia-resources-definition-1920x1080.png')

        await localizedPage.goto('/encyclopedia/sales-shop-help')
        await expect(localizedPage.locator('.sales-shop-help-card').first()).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'encyclopedia-sales-shop-help-1920x1080.png')

        await localizedPage.locator('.sales-shop-help-card .help-image-trigger').first().click()
        await saveScreenshot(localizedPage, locale, 'encyclopedia-sales-shop-help-fullscreen-dialog-1920x1080.png')
        await localizedPage.getByRole('button', { name: /close preview|zavrieť náhľad|vorschau schließen/i }).click()

        await localizedPage.goto('/encyclopedia/stock-exchange-help')
        await expect(localizedPage.locator('.stock-exchange-help-card').first()).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'encyclopedia-stock-exchange-help-1920x1080.png')

        await localizedPage.locator('.stock-exchange-help-card .help-image-trigger').first().click()
        await saveScreenshot(localizedPage, locale, 'encyclopedia-stock-exchange-help-fullscreen-dialog-1920x1080.png')
        await localizedPage.getByRole('button', { name: /close preview|zavrieť náhľad|vorschau schließen/i }).click()

        await localizedPage.goto('/encyclopedia/forex-trading-help')
        await expect(localizedPage.locator('.forex-help-card').first()).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'encyclopedia-forex-trading-help-1920x1080.png')

        await localizedPage.locator('.forex-help-card .help-image-trigger').first().click()
        await saveScreenshot(localizedPage, locale, 'encyclopedia-forex-trading-help-fullscreen-dialog-1920x1080.png')
      } finally {
        await localizedPage.close()
      }
    })
  }
})

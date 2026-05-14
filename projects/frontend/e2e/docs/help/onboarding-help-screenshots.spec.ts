import { expect, test } from '@playwright/test'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { setupMockApi } from '../../helpers/mock-api.js'
import { SCREENSHOT_LOCALES, openLocalizedScreenshotPage, saveLocalizedScreenshot, type ScreenshotLocale } from '../helpers/localized-screenshot.js'

const currentDir = path.dirname(fileURLToPath(import.meta.url))
const OUTPUT_DIR = path.resolve(currentDir, '../../../public/onboarding-help')

const STARTER_FACTORY_LOT_NAME = /Factory Site B1/i
const STARTER_SHOP_LOT_NAME = /High Street Retail Space/i

async function saveScreenshot(page: Parameters<typeof saveLocalizedScreenshot>[0], locale: ScreenshotLocale, fileName: string) {
  await saveLocalizedScreenshot(page, locale, fileName, OUTPUT_DIR, [], [OUTPUT_DIR])
}

test.describe('Onboarding help FullHD screenshots', () => {
  for (const locale of SCREENSHOT_LOCALES) {
    test(`capture seven real 1920x1080 onboarding steps (${locale})`, async ({ page }) => {
      const localizedPage = await openLocalizedScreenshotPage(page.context(), locale)

      try {
        setupMockApi(localizedPage)
        await localizedPage.goto('/onboarding')

        await expect(localizedPage.locator('.city-card').first()).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'step-1-city.png')

        await localizedPage.locator('.city-card', { hasText: 'Bratislava' }).click()
        await expect(localizedPage.locator('.industry-card').first()).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'step-2-industry.png')

        await localizedPage.locator('.industry-card').first().click()
        await expect(localizedPage.locator('.product-card').first()).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'step-3-product.png')

        await localizedPage.locator('.product-card').first().click()
        await expect(localizedPage.locator('.ipo-card').first()).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'step-4-ipo.png')

        await localizedPage.locator('.ipo-card').first().click()
        await expect(localizedPage.locator('.lot-selector').first()).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'step-5-factory-lot.png')

        await localizedPage.locator('.view-toggle .toggle-btn').last().click()
        await localizedPage.getByRole('button', { name: STARTER_FACTORY_LOT_NAME }).click()
        await localizedPage.locator('.step-actions button').last().click()
        await expect(localizedPage.getByRole('button', { name: /Purchase First Sales Shop|Kúpiť prvú predajňu|Erstes Geschäft kaufen/i })).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'step-6-shop-lot.png')

        await localizedPage.locator('.view-toggle .toggle-btn').last().click()
        await localizedPage.getByRole('button', { name: STARTER_SHOP_LOT_NAME }).click()
        await localizedPage.locator('.step-actions button').last().click()
        await expect(localizedPage.locator('.guest-profit-preview')).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'step-7-save-progress.png')
      } finally {
        await localizedPage.close()
      }
    })
  }
})

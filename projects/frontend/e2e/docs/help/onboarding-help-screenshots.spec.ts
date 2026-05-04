import { expect, test } from '@playwright/test'
import path from 'node:path'
import fs from 'node:fs'
import { fileURLToPath } from 'node:url'
import { setupMockApi } from '../.../../helpers/mock-api.js'

const currentDir = path.dirname(fileURLToPath(import.meta.url))
const OUTPUT_DIR = path.resolve(currentDir, '../../../public/onboarding-help')

const STARTER_FACTORY_LOT_NAME = /Factory Site B1/i
const STARTER_SHOP_LOT_NAME = /High Street Retail Space/i

test.describe('Onboarding help FullHD screenshots', () => {
  test('capture seven real 1920x1080 onboarding steps', async ({ page }) => {
    fs.mkdirSync(OUTPUT_DIR, { recursive: true })

    setupMockApi(page)
    await page.setViewportSize({ width: 1920, height: 1080 })

    await page.goto('/onboarding')

    await expect(page.getByRole('heading', { name: 'Choose Your City' })).toBeVisible()
    const step1Path = path.join(OUTPUT_DIR, 'step-1-city.png')
    await page.screenshot({ path: step1Path })
    expect(fs.existsSync(step1Path)).toBeTruthy()

    await page.locator('.city-card', { hasText: 'Bratislava' }).click()
    await expect(page.getByRole('heading', { name: 'Choose Your Industry' })).toBeVisible()
    const step2Path = path.join(OUTPUT_DIR, 'step-2-industry.png')
    await page.screenshot({ path: step2Path })
    expect(fs.existsSync(step2Path)).toBeTruthy()

    await page.locator('.industry-card', { hasText: 'Furniture' }).click()
    await expect(page.getByRole('heading', { name: 'Choose Your First Product' })).toBeVisible()
    const step3Path = path.join(OUTPUT_DIR, 'step-3-product.png')
    await page.screenshot({ path: step3Path })
    expect(fs.existsSync(step3Path)).toBeTruthy()

    await page.locator('.product-card', { hasText: 'Wooden Chair' }).click()
    await expect(page.getByRole('heading', { name: 'Choose Your IPO Plan' })).toBeVisible()
    const step4Path = path.join(OUTPUT_DIR, 'step-4-ipo.png')
    await page.screenshot({ path: step4Path })
    expect(fs.existsSync(step4Path)).toBeTruthy()

    await page.locator('.ipo-card', { hasText: 'Starter IPO' }).click()
    await expect(page.getByRole('heading', { name: 'Choose Your First Factory Lot' })).toBeVisible()
    const step5Path = path.join(OUTPUT_DIR, 'step-5-factory-lot.png')
    await page.screenshot({ path: step5Path })
    expect(fs.existsSync(step5Path)).toBeTruthy()

    await page.getByRole('button', { name: 'List View' }).click()
    await page.getByRole('button', { name: STARTER_FACTORY_LOT_NAME }).click()
    await page.getByRole('button', { name: 'Purchase First Factory' }).click()
    await expect(page.getByRole('heading', { name: 'Choose Your First Shop Lot' })).toBeVisible()
    const step6Path = path.join(OUTPUT_DIR, 'step-6-shop-lot.png')
    await page.screenshot({ path: step6Path })
    expect(fs.existsSync(step6Path)).toBeTruthy()

    await page.getByRole('button', { name: STARTER_SHOP_LOT_NAME }).click()
    await page.getByRole('button', { name: 'Purchase First Sales Shop' }).click()
    await expect(page.getByRole('heading', { name: 'Save Your Progress' })).toBeVisible()
    const step7Path = path.join(OUTPUT_DIR, 'step-7-save-progress.png')
    await page.screenshot({ path: step7Path })
    expect(fs.existsSync(step7Path)).toBeTruthy()
  })
})

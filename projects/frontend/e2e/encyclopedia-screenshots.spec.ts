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

    await page.goto('/encyclopedia/resources-definition')
    const resourcesDefinitionPath = path.join(OUTPUT_DIR, 'encyclopedia-resources-definition-1920x1080.png')
    await page.screenshot({ path: resourcesDefinitionPath })
    expect(fs.existsSync(resourcesDefinitionPath)).toBeTruthy()
  })
})

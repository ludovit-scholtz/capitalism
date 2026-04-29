import { expect, test, type Page } from '@playwright/test'
import { makePlayer, setupMockApi } from './helpers/mock-api.js'
import path from 'node:path'
import fs from 'node:fs'
import { fileURLToPath } from 'node:url'

const currentDir = path.dirname(fileURLToPath(import.meta.url))
const PUBLIC_OUTPUT_DIR = path.resolve(currentDir, '../public/sales-shop-help')
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

test.describe('Sales shop help screenshots', () => {
  test('captures real FullHD walkthrough screenshots for buy-building and shop units', async ({ page }) => {
    fs.mkdirSync(PUBLIC_OUTPUT_DIR, { recursive: true })
    fs.mkdirSync(DOCS_OUTPUT_DIR, { recursive: true })

    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T12:00:00Z',
      companies: [
        {
          id: 'company-shop-docs',
          playerId: 'player-1',
          name: 'Retail Docs Co',
          cash: 2_500_000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [
            {
              id: 'building-shop-docs',
              companyId: 'company-shop-docs',
              cityId: 'city-ba',
              type: 'SALES_SHOP',
              name: 'Retail Docs Shop',
              latitude: 48.15,
              longitude: 17.11,
              level: 1,
              powerConsumption: 1,
              isForSale: false,
              builtAtUtc: '2026-01-01T00:00:00Z',
              pendingConfiguration: null,
              units: [
                {
                  id: 'shop-u-1',
                  buildingId: 'building-shop-docs',
                  unitType: 'PURCHASE',
                  gridX: 0,
                  gridY: 0,
                  level: 1,
                  linkUp: false,
                  linkDown: false,
                  linkLeft: false,
                  linkRight: true,
                  linkUpLeft: false,
                  linkUpRight: false,
                  linkDownLeft: false,
                  linkDownRight: false,
                },
                {
                  id: 'shop-u-2',
                  buildingId: 'building-shop-docs',
                  unitType: 'PUBLIC_SALES',
                  gridX: 1,
                  gridY: 0,
                  level: 1,
                  linkUp: false,
                  linkDown: false,
                  linkLeft: true,
                  linkRight: true,
                  linkUpLeft: false,
                  linkUpRight: false,
                  linkDownLeft: false,
                  linkDownRight: false,
                },
                {
                  id: 'shop-u-3',
                  buildingId: 'building-shop-docs',
                  unitType: 'MARKETING',
                  gridX: 2,
                  gridY: 0,
                  level: 1,
                  linkUp: false,
                  linkDown: false,
                  linkLeft: true,
                  linkRight: false,
                  linkUpLeft: false,
                  linkUpRight: false,
                  linkDownLeft: false,
                  linkDownRight: false,
                },
              ],
            },
          ],
        },
      ],
    })

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.setViewportSize({ width: 1920, height: 1080 })

    await page.goto('/buy-building/company-shop-docs?type=SALES_SHOP')
    await expect(page.getByRole('heading', { name: /buy building|kúpiť budovu|gebäude kaufen/i })).toBeVisible()
    await saveScreenshot(page, 'step-1-buy-sales-shop-1920x1080.png')

    await page.goto('/building/building-shop-docs')
    await expect(page.getByRole('heading', { name: 'Retail Docs Shop' })).toBeVisible()
    await page.getByRole('button', { name: 'Edit Building' }).click()
    await expect(page.getByRole('heading', { name: 'Planned Upgrade' })).toBeVisible()

    const plannedSection = page
      .locator('.grid-section')
      .filter({ has: page.getByRole('heading', { name: 'Planned Upgrade' }) })
      .first()

    await plannedSection.locator('.unit-row').nth(0).locator('.grid-cell').nth(0).click()
    await saveScreenshot(page, 'step-2-purchase-unit-1920x1080.png')

    await plannedSection.locator('.unit-row').nth(0).locator('.grid-cell').nth(1).click()
    await saveScreenshot(page, 'step-3-public-sales-unit-1920x1080.png')

    await plannedSection.locator('.unit-row').nth(0).locator('.grid-cell').nth(2).click()
    await saveScreenshot(page, 'step-4-marketing-unit-1920x1080.png')
  })
})

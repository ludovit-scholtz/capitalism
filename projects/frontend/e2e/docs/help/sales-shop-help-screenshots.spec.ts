import { expect, test, type Page } from '@playwright/test'
import { makePlayer, setupMockApi } from '../../helpers/mock-api.js'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { SCREENSHOT_LOCALES, openLocalizedScreenshotPage, saveLocalizedScreenshot, type ScreenshotLocale } from '../helpers/localized-screenshot.js'

const currentDir = path.dirname(fileURLToPath(import.meta.url))
const PUBLIC_OUTPUT_DIR = path.resolve(currentDir, '../../../public/sales-shop-help')
const DOCS_OUTPUT_DIR = path.resolve(currentDir, '../../../docs/screenshots/encyclopedia-help')

async function saveScreenshot(page: Page, locale: ScreenshotLocale, fileName: string) {
  await saveLocalizedScreenshot(page, locale, fileName, PUBLIC_OUTPUT_DIR, [DOCS_OUTPUT_DIR], [PUBLIC_OUTPUT_DIR, DOCS_OUTPUT_DIR])
}

async function dismissTutorialTooltipIfPresent(page: Page) {
  const dismissButton = page.getByRole('button', { name: /got it|rozumiem|verstanden/i }).first()
  const visible = await dismissButton.isVisible().catch(() => false)
  if (!visible) {
    return
  }

  await dismissButton.click({ timeout: 2000 })
  await dismissButton.waitFor({ state: 'hidden', timeout: 1000 }).catch(() => {})
}

test.describe('Sales shop help screenshots', () => {
  for (const locale of SCREENSHOT_LOCALES) {
    test(`captures real FullHD walkthrough screenshots for buy-building and shop units (${locale})`, async ({ page }) => {
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

      const localizedPage = await openLocalizedScreenshotPage(page.context(), locale)

      try {
        const state = setupMockApi(localizedPage, { players: [player] })
        state.currentUserId = player.id
        state.currentToken = `token-${player.id}`

        await localizedPage.addInitScript((token) => {
          localStorage.setItem('auth_token', token)
          localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
        }, `token-${player.id}`)

        await localizedPage.goto('/buy-building/company-shop-docs?type=SALES_SHOP')
        await expect(localizedPage.locator('.lot-card, .lot-list-item').first()).toBeVisible()
        await saveScreenshot(localizedPage, locale, 'step-1-buy-sales-shop-1920x1080.png')

        await localizedPage.goto('/building/building-shop-docs')
        await expect(localizedPage.getByRole('heading', { name: 'Retail Docs Shop' })).toBeVisible()
        await localizedPage.getByRole('button', { name: /Edit Building|Upraviť budovu|Gebäude bearbeiten/i }).click()
        await expect(localizedPage.getByRole('heading', { name: /Planned Upgrade|Plánovaný upgrade|Geplanter Ausbau/i })).toBeVisible()
        await dismissTutorialTooltipIfPresent(localizedPage)

        const plannedSection = localizedPage
          .locator('.grid-section')
          .filter({ has: localizedPage.getByRole('heading', { name: /Planned Upgrade|Plánovaný upgrade|Geplanter Ausbau/i }) })
          .first()

        await plannedSection.locator('.unit-row').nth(0).locator('.grid-cell').nth(0).click()
        await saveScreenshot(localizedPage, locale, 'step-2-purchase-unit-1920x1080.png')

        await plannedSection.locator('.unit-row').nth(0).locator('.grid-cell').nth(1).click()
        await saveScreenshot(localizedPage, locale, 'step-3-public-sales-unit-1920x1080.png')

        await plannedSection.locator('.unit-row').nth(0).locator('.grid-cell').nth(2).click()
        await saveScreenshot(localizedPage, locale, 'step-4-marketing-unit-1920x1080.png')
      } finally {
        await localizedPage.close()
      }
    })
  }
})

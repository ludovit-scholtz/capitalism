import { test, expect } from '@playwright/test'
import { setupMockApi } from './helpers/mock-api'

test.describe('Country flag icons — master portal', () => {
  // ──────────────────────────────────────────────────
  // Language switcher flags
  // ──────────────────────────────────────────────────

  test.describe('Language switcher flags', () => {
    test('footer language switcher shows 3 flag buttons', async ({ page }) => {
      setupMockApi(page)
      await page.goto('/')

      // Language switcher is rendered in the footer
      await page.locator('.language-switcher').first().scrollIntoViewIfNeeded()
      const switcher = page.locator('.language-switcher').first()
      await expect(switcher).toBeVisible()

      const buttons = switcher.locator('.language-btn')
      await expect(buttons).toHaveCount(3)
    })

    test('each language button contains a flag image', async ({ page }) => {
      setupMockApi(page)
      await page.goto('/')

      await page.locator('.language-switcher').first().scrollIntoViewIfNeeded()
      const switcher = page.locator('.language-switcher').first()
      const buttons = switcher.locator('.language-btn')
      const count = await buttons.count()

      for (let i = 0; i < count; i++) {
        const flag = buttons.nth(i).locator('.country-flag[role="img"]').first()
        await expect(flag).toBeVisible()
      }
    })

    test('English button shows GB flag', async ({ page }) => {
      setupMockApi(page)
      await page.goto('/')

      await page.locator('.language-switcher').first().scrollIntoViewIfNeeded()
      const switcher = page.locator('.language-switcher').first()
      const enButton = switcher.locator('.language-btn').nth(0)
      const flag = enButton.locator('.country-flag[role="img"]').first()
      await expect(flag).toBeVisible()
      // The portal uses abbreviated locale labels (EN, SK, DE)
      await expect(flag).toHaveAttribute('title', /^EN$/i)
    })

    test('Slovak button shows SK flag', async ({ page }) => {
      setupMockApi(page)
      await page.goto('/')

      await page.locator('.language-switcher').first().scrollIntoViewIfNeeded()
      const switcher = page.locator('.language-switcher').first()
      const skButton = switcher.locator('.language-btn').nth(1)
      // Flag renders an inline SVG for known country codes
      const svg = skButton.locator('.country-flag svg')
      await expect(svg).toBeVisible()
    })

    test('German button shows DE flag', async ({ page }) => {
      setupMockApi(page)
      await page.goto('/')

      await page.locator('.language-switcher').first().scrollIntoViewIfNeeded()
      const switcher = page.locator('.language-switcher').first()
      const deButton = switcher.locator('.language-btn').nth(2)
      const flag = deButton.locator('.country-flag[role="img"]').first()
      await expect(flag).toBeVisible()
      // The portal uses abbreviated locale labels (EN, SK, DE)
      await expect(flag).toHaveAttribute('title', /^DE$/i)
    })

    test('active language button has aria-pressed=true', async ({ page }) => {
      // Ensure English locale is active
      await page.addInitScript(() => localStorage.removeItem('master_locale'))
      setupMockApi(page)
      await page.goto('/')

      await page.locator('.language-switcher').first().scrollIntoViewIfNeeded()
      const switcher = page.locator('.language-switcher').first()
      const enButton = switcher.locator('.language-btn').nth(0)
      await expect(enButton).toHaveAttribute('aria-pressed', 'true')

      // Others must not be active
      await expect(switcher.locator('.language-btn').nth(1)).toHaveAttribute('aria-pressed', 'false')
      await expect(switcher.locator('.language-btn').nth(2)).toHaveAttribute('aria-pressed', 'false')
    })

    test('clicking German button activates it', async ({ page }) => {
      await page.addInitScript(() => localStorage.removeItem('master_locale'))
      setupMockApi(page)
      await page.goto('/')

      await page.locator('.language-switcher').first().scrollIntoViewIfNeeded()
      const switcher = page.locator('.language-switcher').first()
      const deButton = switcher.locator('.language-btn').nth(2)
      await deButton.click()
      await expect(deButton).toHaveAttribute('aria-pressed', 'true')
      await expect(switcher.locator('.language-btn').nth(0)).toHaveAttribute('aria-pressed', 'false')
    })
  })

  // ──────────────────────────────────────────────────
  // Mobile viewport
  // ──────────────────────────────────────────────────

  test.describe('Flags on mobile viewport', () => {
    test('language switcher flags visible on mobile', async ({ page }) => {
      await page.setViewportSize({ width: 375, height: 812 })
      setupMockApi(page)
      await page.goto('/')

      await page.locator('.language-switcher').first().scrollIntoViewIfNeeded()
      const switcher = page.locator('.language-switcher').first()
      await expect(switcher).toBeVisible()

      const buttons = switcher.locator('.language-btn')
      const count = await buttons.count()
      expect(count).toBeGreaterThanOrEqual(3)

      for (let i = 0; i < count; i++) {
        const flag = buttons.nth(i).locator('.country-flag[role="img"]').first()
        await expect(flag).toBeVisible()
      }
    })
  })
})

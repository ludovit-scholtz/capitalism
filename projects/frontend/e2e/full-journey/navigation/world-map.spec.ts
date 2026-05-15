import { test, expect } from '@playwright/test'
import { setupMockApi } from '../../helpers/mock-api'

test.describe('World map expansion view', () => {
  test('renders /map with city pins list and expansion modal for not-yet-unlocked city', async ({ page }) => {
    setupMockApi(page, {})

    await page.goto('/map')

    await expect(page.getByRole('heading', { name: 'World Map' })).toBeVisible()
    const cityButtons = page.locator('.city-item')
    await expect(cityButtons).not.toHaveCount(0)

    await page.locator('.city-item', { hasText: 'Berlin' }).click()
    const modal = page.locator('.expansion-modal')
    await expect(page.getByRole('heading', { name: 'Expand to Berlin' })).toBeVisible()
    await expect(modal).toContainText('Population')
    await expect(modal).toContainText('Unlock Berlin')
    await expect(modal.getByRole('button', { name: 'Grow your company to unlock' })).toBeVisible()
  })

  test('keeps starter cities directly accessible on /map while expansion cities stay locked', async ({ page }) => {
    setupMockApi(page, {})

    await page.goto('/map')

    await page.locator('.city-item', { hasText: 'Bratislava' }).click()
    await expect(page.locator('.city-detail')).toContainText('Bratislava')
    await expect(page.getByRole('button', { name: 'Go to City' })).toBeVisible()
    await expect(page.locator('.expansion-modal')).toHaveCount(0)
  })
})

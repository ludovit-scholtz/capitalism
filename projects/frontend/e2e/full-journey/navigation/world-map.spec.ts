import { test, expect } from '@playwright/test'
import { setupMockApi } from '../../helpers/mock-api'

test.describe('World map expansion view', () => {
  test('renders /map with city pins list and expansion modal for not-yet-unlocked city', async ({ page }) => {
    setupMockApi(page, {})

    await page.goto('/map')

    await expect(page.getByRole('heading', { name: 'World Map' })).toBeVisible()
    const cityButtons = page.locator('.city-item')
    await expect(cityButtons).not.toHaveCount(0)

    await cityButtons.first().click()
    const modal = page.locator('.expansion-modal')
    await expect(page.getByRole('heading', { name: /Expand to/i })).toBeVisible()
    await expect(modal).toContainText('Population')
    await expect(modal.getByRole('button', { name: 'Start expanding here' })).toBeVisible()
  })
})

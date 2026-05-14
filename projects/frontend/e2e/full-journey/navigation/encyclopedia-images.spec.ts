import { expect, test } from '@playwright/test'
import { setupMockApi } from '../../helpers/mock-api'

test.describe('Encyclopedia product and resource images', () => {
  test('renders non-empty resource and product image sources', async ({ page }) => {
    setupMockApi(page)

    await page.goto('/encyclopedia')

    const resourceCard = page.locator('.resource-card--resource').first()
    const productCard = page.locator('.resource-card--product').first()
    await expect(resourceCard).toBeVisible()
    await expect(productCard).toBeVisible()

    const resourceImage = resourceCard.locator('img').first()
    const productImage = productCard.locator('img').first()
    await expect(resourceImage).toBeVisible()
    await expect(productImage).toBeVisible()

    const resourceSrc = await resourceImage.getAttribute('src')
    const productSrc = await productImage.getAttribute('src')
    expect(resourceSrc && resourceSrc.length > 0).toBe(true)
    expect(productSrc && productSrc.length > 0).toBe(true)
  })
})

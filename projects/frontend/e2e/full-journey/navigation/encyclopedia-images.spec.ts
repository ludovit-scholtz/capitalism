import { expect, test } from '@playwright/test'
import { setupMockApi } from '../../helpers/mock-api'

test.describe('Encyclopedia product and resource images', () => {
  test('renders the newly covered healthcare product cards with visible images', async ({ page }) => {
    setupMockApi(page, {
      productTypes: [
        {
          id: 'prod-allergy-tablets',
          name: 'Allergy Tablets',
          slug: 'allergy-tablets',
          industry: 'HEALTHCARE',
          basePrice: 27,
          baseCraftTicks: 2,
          outputQuantity: 14,
          energyConsumptionMwh: 0.8,
          basicLaborHours: 1.5,
          unitName: 'Pack',
          unitSymbol: 'packs',
          isProOnly: false,
          description: 'Seasonal allergy medication for pharmacy shelves.',
          recipes: [
            {
              resourceType: {
                id: 'res-chem',
                name: 'Chemical Minerals',
                slug: 'chemical-minerals',
                unitName: 'Ton',
                unitSymbol: 't',
              },
              inputProductType: null,
              quantity: 1,
            },
          ],
        },
        {
          id: 'prod-antiseptic',
          name: 'Antiseptic',
          slug: 'antiseptic',
          industry: 'HEALTHCARE',
          basePrice: 24,
          baseCraftTicks: 2,
          outputQuantity: 12,
          energyConsumptionMwh: 0.8,
          basicLaborHours: 1.4,
          unitName: 'Pack',
          unitSymbol: 'packs',
          isProOnly: false,
          description: 'Disinfecting liquid used in wound treatment and surgical care.',
          recipes: [
            {
              resourceType: {
                id: 'res-chem',
                name: 'Chemical Minerals',
                slug: 'chemical-minerals',
                unitName: 'Ton',
                unitSymbol: 't',
              },
              inputProductType: null,
              quantity: 1,
            },
          ],
        },
        {
          id: 'prod-cold-pack',
          name: 'Cold Pack',
          slug: 'cold-pack',
          industry: 'HEALTHCARE',
          basePrice: 12,
          baseCraftTicks: 1,
          outputQuantity: 16,
          energyConsumptionMwh: 0.4,
          basicLaborHours: 0.9,
          unitName: 'Pack',
          unitSymbol: 'packs',
          isProOnly: false,
          description: 'Instant cold-compress product for sports and emergency use.',
          recipes: [
            {
              resourceType: {
                id: 'res-chem',
                name: 'Chemical Minerals',
                slug: 'chemical-minerals',
                unitName: 'Ton',
                unitSymbol: 't',
              },
              inputProductType: null,
              quantity: 1,
            },
          ],
        },
      ],
    })

    await page.goto('/encyclopedia')

    for (const productName of ['Allergy Tablets', 'Antiseptic', 'Cold Pack']) {
      const card = page.locator('.resource-card--product', { hasText: productName }).first()
      await expect(card).toBeVisible()

      const image = card.locator('img').first()
      await expect(image).toBeVisible()

      const src = await image.getAttribute('src')
      expect(src && src.length > 0).toBe(true)
    }
  })

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

  test('falls back to placeholder image when an image fails to load', async ({ page }) => {
    setupMockApi(page)
    await page.route('**/does-not-exist.webp', async (route) => route.abort())

    await page.goto('/encyclopedia')

    const productImage = page.locator('.resource-card--product img').first()
    await expect(productImage).toBeVisible()

    await productImage.evaluate((node: Element) => {
      const image = node as HTMLImageElement
      image.src = '/does-not-exist.webp'
    })

    await expect(productImage).toHaveAttribute('src', /fallback|data:image\/svg\+xml/i)
  })
})

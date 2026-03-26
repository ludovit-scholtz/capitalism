import { expect, test } from '@playwright/test'
import { setupMockApi } from './helpers/mock-api'

test.describe('Manufacturing encyclopedia', () => {
  test('shows raw material cards and product recipes', async ({ page }) => {
    setupMockApi(page, {
      resourceTypes: [
        {
          id: 'res-wood',
          name: 'Wood',
          slug: 'wood',
          category: 'ORGANIC',
          basePrice: 10,
          weightPerUnit: 5,
          unitName: 'Ton',
          unitSymbol: 't',
          imageUrl: 'data:image/svg+xml;utf8,<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\" fill=\"brown\"/></svg>',
          description: 'Timber for furniture.',
        },
      ],
      productTypes: [
        {
          id: 'prod-components',
          name: 'Electronic Components',
          slug: 'electronic-components',
          industry: 'ELECTRONICS',
          basePrice: 50,
          baseCraftTicks: 3,
          outputQuantity: 16,
          energyConsumptionMwh: 1.2,
          unitName: 'Pack',
          unitSymbol: 'packs',
          isProOnly: false,
          description: 'Intermediate electronics input.',
          recipes: [],
        },
        {
          id: 'prod-electronic-table',
          name: 'Electronic Table',
          slug: 'electronic-table',
          industry: 'FURNITURE',
          basePrice: 520,
          baseCraftTicks: 6,
          outputQuantity: 1,
          energyConsumptionMwh: 2,
          unitName: 'Piece',
          unitSymbol: 'pcs',
          isProOnly: false,
          description: 'Smart table.',
          recipes: [
            { resourceType: { id: 'res-wood', name: 'Wood', slug: 'wood', unitName: 'Ton', unitSymbol: 't' }, inputProductType: null, quantity: 1 },
            { resourceType: null, inputProductType: { id: 'prod-components', name: 'Electronic Components', slug: 'electronic-components', unitName: 'Pack', unitSymbol: 'packs' }, quantity: 10 },
          ],
        },
      ],
    })

    await page.goto('/encyclopedia')

    await expect(page.getByRole('heading', { name: 'Manufacturing Encyclopedia' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Wood' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Electronic Table' })).toBeVisible()
    await expect(page.getByText('1 t Wood + 10 packs Electronic Components')).toBeVisible()
  })
})

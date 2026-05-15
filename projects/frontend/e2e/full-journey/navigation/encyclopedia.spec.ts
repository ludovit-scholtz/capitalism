import { expect, test } from '@playwright/test'
import { setupMockApi } from '../../helpers/mock-api.js'

const woodResource = {
  id: 'resource-wood',
  name: 'Wood',
  slug: 'wood',
  category: 'ORGANIC',
  basePrice: 10,
  weightPerUnit: 5,
  unitName: 'Ton',
  unitSymbol: 't',
  imageUrl: null,
  description: 'Timber for furniture manufacturing.',
}

const ironResource = {
  id: 'resource-iron',
  name: 'Iron Ore',
  slug: 'iron-ore',
  category: 'MINERAL',
  basePrice: 20,
  weightPerUnit: 7,
  unitName: 'Ton',
  unitSymbol: 't',
  imageUrl: null,
  description: 'Core mineral input for iron goods.',
}

const grainResource = {
  id: 'resource-grain',
  name: 'Grain',
  slug: 'grain',
  category: 'ORGANIC',
  basePrice: 5,
  weightPerUnit: 2,
  unitName: 'Ton',
  unitSymbol: 't',
  imageUrl: null,
  description: 'Agricultural staple for flour and bread.',
}

const flourProduct = {
  id: 'product-flour',
  name: 'Flour',
  slug: 'flour',
  industry: 'FOOD_PROCESSING',
  basePrice: 8,
  baseCraftTicks: 1,
  outputQuantity: 10,
  energyConsumptionMwh: 0.4,
  basicLaborHours: 0.4,
  unitName: 'Bag',
  unitSymbol: 'bags',
  imageUrl: null,
  isProOnly: false,
  isUnlockedForCurrentPlayer: true,
  isPerishable: false,
  description: 'Milled flour for bakery products.',
  recipes: [{ resourceType: { id: 'resource-grain', name: 'Grain', slug: 'grain', unitName: 'Ton', unitSymbol: 't' }, inputProductType: null, quantity: 1 }],
}

const breadProduct = {
  id: 'product-bread',
  name: 'Bread',
  slug: 'bread',
  industry: 'FOOD_PROCESSING',
  basePrice: 3,
  baseCraftTicks: 1,
  outputQuantity: 12,
  energyConsumptionMwh: 0.5,
  basicLaborHours: 0.4,
  unitName: 'Loaf',
  unitSymbol: 'loaves',
  imageUrl: null,
  isProOnly: false,
  isUnlockedForCurrentPlayer: true,
  isPerishable: true,
  description: 'Basic bread loaf.',
  recipes: [{ resourceType: null, inputProductType: { id: 'product-flour', name: 'Flour', slug: 'flour', unitName: 'Bag', unitSymbol: 'bags' }, quantity: 2 }],
}

const chairProduct = {
  id: 'product-chair',
  name: 'Wooden Chair',
  slug: 'wooden-chair',
  industry: 'FURNITURE',
  basePrice: 45,
  baseCraftTicks: 2,
  outputQuantity: 20,
  energyConsumptionMwh: 0.8,
  basicLaborHours: 0.6,
  unitName: 'Chair',
  unitSymbol: 'chairs',
  imageUrl: null,
  isProOnly: false,
  isUnlockedForCurrentPlayer: true,
  isPerishable: false,
  description: 'Starter furniture product.',
  recipes: [{ resourceType: { id: 'resource-wood', name: 'Wood', slug: 'wood', unitName: 'Ton', unitSymbol: 't' }, inputProductType: null, quantity: 1 }],
}

test.describe('Manufacturing encyclopedia catalog', () => {
  test.beforeEach(async ({ page }) => {
    setupMockApi(page, {
      resourceTypes: [woodResource, ironResource, grainResource],
      productTypes: [flourProduct, breadProduct, chairProduct],
    })
  })

  test('renders a searchable encyclopedia grid with multiple cards', async ({ page }) => {
    await page.goto('/encyclopedia')

    await expect(page.getByRole('heading', { name: 'Manufacturing Encyclopedia' })).toBeVisible()
    await expect(page.locator('.resource-card--link')).toHaveCount(6)
  })

  test('filters the catalog after typing into search', async ({ page }) => {
    await page.goto('/encyclopedia')

    await page.getByRole('searchbox').fill('iron')
    await expect(page.locator('.resource-card--link')).toHaveCount(1)
    await expect(page.locator('.resource-card--link h3')).toContainText('Iron Ore')
  })

  test('navigates to the detail route when a catalog card is clicked', async ({ page }) => {
    await page.goto('/encyclopedia')

    await page.getByRole('button', { name: /Flour/ }).click()

    await expect(page).toHaveURL('/encyclopedia/flour')
    await expect(page.getByRole('heading', { name: 'Flour', level: 1 })).toBeVisible()
  })

  test('shows upstream and downstream recipe sections for an intermediate product', async ({ page }) => {
    await page.goto('/encyclopedia/flour')

    await expect(page.getByRole('heading', { name: /Produced as output from/i })).toBeVisible()
    await expect(page.getByRole('heading', { name: /Used as input to manufacture/i })).toBeVisible()
    const recipeCards = page.locator('article.rounded-2xl.border.border-divider.bg-card')
    await expect(recipeCards.filter({ hasText: 'Grain' }).first()).toBeVisible()
    await expect(recipeCards.filter({ hasText: 'Bread Recipe' }).first()).toBeVisible()
    await expect(page.getByRole('link', { name: 'Factory' }).first()).toBeVisible()
  })
})

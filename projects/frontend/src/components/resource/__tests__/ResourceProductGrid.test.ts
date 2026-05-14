// @vitest-environment jsdom
import { createApp, defineComponent, h } from 'vue'
import { createI18n } from 'vue-i18n'
import { describe, expect, it } from 'vitest'
import ResourceProductGrid from '../ResourceProductGrid.vue'
import type { ProductType, ResourceType } from '@/types'

const i18n = createI18n({
  legacy: false,
  locale: 'en',
  messages: {
    en: {
      encyclopedia: {
        viewDetail: 'View detail',
      },
      resourceDetail: {
        noRelatedProducts: 'No products',
        usedInProducts: 'Used in products',
        usedInProductsHelp: 'Help',
        batchOutput: 'Batch output',
        ingredientQuantity: 'Ingredient quantity',
        ingredientQuantityLabel: 'Ingredient',
        craftTicks: 'Craft ticks',
        outputLabel: 'Output',
      },
    },
  },
})

function makeResource(): ResourceType {
  return {
    id: 'resource-wood',
    name: 'Wood',
    slug: 'wood',
    category: 'ORGANIC',
    basePrice: 10,
    weightPerUnit: 5,
    unitName: 'Ton',
    unitSymbol: 't',
    imageUrl: null,
    description: 'Wood resource',
  }
}

function makeProduct(): ProductType {
  return {
    id: 'product-chair',
    name: 'Wooden Chair',
    slug: 'wooden-chair',
    imageUrl: null,
    industry: 'FURNITURE',
    basePrice: 45,
    baseCraftTicks: 2,
    outputQuantity: 20,
    energyConsumptionMwh: 1,
    basicLaborHours: 1,
    unitName: 'Piece',
    unitSymbol: 'pcs',
    isProOnly: false,
    isUnlockedForCurrentPlayer: true,
    description: 'Chair',
    isPerishable: false,
    recipes: [
      {
        resourceType: makeResource(),
        inputProductType: null,
        quantity: 1,
      },
    ],
  }
}

describe('ResourceProductGrid', () => {
  it('renders product image with non-empty src and alt text', () => {
    const container = document.createElement('div')
    const product = makeProduct()

    const App = defineComponent({
      setup() {
        return () =>
          h(ResourceProductGrid, {
            relatedProducts: [product],
            locale: 'en',
            selectedResource: makeResource(),
            selectedProduct: null,
            onNavigateToEntry: () => undefined,
          })
      },
    })

    createApp(App).use(i18n).mount(container)

    const image = container.querySelector('img.product-image') as HTMLImageElement | null
    expect(image).not.toBeNull()
    expect(image?.getAttribute('src')).toMatch(/\.svg|data:image\/svg\+xml/i)
    expect(image?.getAttribute('alt')).toBe('Wooden Chair')
  })
})

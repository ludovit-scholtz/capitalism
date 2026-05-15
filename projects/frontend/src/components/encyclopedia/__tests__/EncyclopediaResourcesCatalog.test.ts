// @vitest-environment jsdom
import { createApp, defineComponent, h, nextTick } from 'vue'
import { createI18n } from 'vue-i18n'
import { createMemoryHistory, createRouter } from 'vue-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import EncyclopediaResourcesCatalog from '../EncyclopediaResourcesCatalog.vue'
import type { EncyclopediaCatalogEntry } from '@/types'

const i18n = createI18n({
  legacy: false,
  locale: 'en',
  messages: {
    en: {
      encyclopedia: {
        resourcesTitle: 'Resources',
        resourcesHelp: 'Browse the catalog',
        proHiddenNotice: '{count} hidden',
        searchPlaceholder: 'Search resources',
        searchLabel: 'Search encyclopedia',
        filterAll: 'All entries',
        filterRawMaterials: 'Raw materials',
        showProProducts: 'Show Pro subscription products',
        resourceTypeRaw: 'Raw material',
        basePrice: 'Base price',
        weight: 'Weight',
        energy: 'Batch energy',
        basicLaborHours: 'Basic labor',
        output: 'Batch output',
        searchNoResults: 'No results',
        viewDetail: 'View detail',
        helpSectionTitle: 'Gameplay Help',
        helpSectionSubtitle: 'Guide',
        gameplayGuideCardMarketsTitle: 'Markets',
        gameplayGuideCardMarketsBody: 'Markets body',
        gameplayGuideCardFlowTitle: 'Flow',
        gameplayGuideCardFlowBody: 'Flow body',
        gameplayGuideCardIterationTitle: 'Iteration',
        gameplayGuideCardIterationBody: 'Iteration body',
        perishable: 'Perishable',
      },
      catalog: {
        free: 'Free',
        proRequired: 'Pro required',
        proUnlocked: 'Pro unlocked',
      },
    },
  },
})

function makeEntries(): EncyclopediaCatalogEntry[] {
  return [
    {
      id: 'resource-wood',
      kind: 'RESOURCE',
      name: 'Wood',
      slug: 'wood',
      category: 'ORGANIC',
      industry: null,
      description: 'Wood resource',
      imageUrl: null,
      isPerishable: false,
      isProOnly: false,
      isUnlockedForCurrentPlayer: true,
      basePrice: 10,
      weightPerUnit: 5,
      baseCraftTicks: null,
      outputQuantity: null,
      energyConsumptionMwh: null,
      basicLaborHours: null,
      unitName: 'Ton',
      unitSymbol: 't',
    },
    {
      id: 'resource-iron',
      kind: 'RESOURCE',
      name: 'Iron Ore',
      slug: 'iron-ore',
      category: 'MINERAL',
      industry: null,
      description: 'Iron resource',
      imageUrl: null,
      isPerishable: false,
      isProOnly: false,
      isUnlockedForCurrentPlayer: true,
      basePrice: 20,
      weightPerUnit: 6,
      baseCraftTicks: null,
      outputQuantity: null,
      energyConsumptionMwh: null,
      basicLaborHours: null,
      unitName: 'Ton',
      unitSymbol: 't',
    },
    {
      id: 'product-bread',
      kind: 'PRODUCT',
      name: 'Bread',
      slug: 'bread',
      category: 'FOOD_PROCESSING',
      industry: 'FOOD_PROCESSING',
      description: 'Bread loaf',
      imageUrl: null,
      isPerishable: true,
      isProOnly: false,
      isUnlockedForCurrentPlayer: true,
      basePrice: 3,
      weightPerUnit: null,
      baseCraftTicks: 1,
      outputQuantity: 12,
      energyConsumptionMwh: 0.5,
      basicLaborHours: 0.4,
      unitName: 'Loaf',
      unitSymbol: 'loaves',
    },
  ]
}

async function renderCatalog(entries: EncyclopediaCatalogEntry[]) {
  const container = document.createElement('div')
  const navigatedSlugs: string[] = []
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/encyclopedia', component: { template: '<div />' } }],
  })

  await router.push('/encyclopedia')
  await router.isReady()

  const App = defineComponent({
    setup() {
      return () =>
        h(EncyclopediaResourcesCatalog, {
          entries,
          onNavigate: (slug: string) => navigatedSlugs.push(slug),
        })
    },
  })

  createApp(App).use(i18n).use(router).mount(container)
  await nextTick()

  return { container, navigatedSlugs }
}

afterEach(() => {
  vi.useRealTimers()
})

describe('EncyclopediaResourcesCatalog', () => {
  it('renders one card per encyclopedia entry', async () => {
    const { container } = await renderCatalog(makeEntries())

    expect(container.querySelectorAll('.resource-card--link')).toHaveLength(3)
  })

  it('filters search results only after the 300ms debounce', async () => {
    vi.useFakeTimers()
    const { container } = await renderCatalog(makeEntries())
    const input = container.querySelector('#encyclopedia-search') as HTMLInputElement | null

    expect(input).not.toBeNull()

    input!.value = 'iron'
    input!.dispatchEvent(new Event('input', { bubbles: true }))
    await nextTick()

    expect(container.querySelectorAll('.resource-card--link')).toHaveLength(3)

    vi.advanceTimersByTime(300)
    await nextTick()

    expect(container.querySelectorAll('.resource-card--link')).toHaveLength(1)
    expect(container.querySelector('.resource-card--link h3')?.textContent).toContain('Iron Ore')
  })

  it('toggles category chips for raw materials and product industries', async () => {
    const { container } = await renderCatalog(makeEntries())
    const rawMaterialsButton = Array.from(container.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Raw materials'),
    )
    const foodButton = Array.from(container.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Food Processing'),
    )

    expect(rawMaterialsButton).toBeTruthy()
    expect(foodButton).toBeTruthy()

    rawMaterialsButton!.dispatchEvent(new MouseEvent('click', { bubbles: true }))
    await nextTick()
    expect(container.querySelectorAll('.resource-card--link')).toHaveLength(2)

    foodButton!.dispatchEvent(new MouseEvent('click', { bubbles: true }))
    await nextTick()
    expect(container.querySelectorAll('.resource-card--link')).toHaveLength(1)
    expect(container.querySelector('.resource-card--link h3')?.textContent).toContain('Bread')
  })
})

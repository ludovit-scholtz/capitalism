<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { useTickRefresh } from '@/composables/useTickRefresh'
import { useGameStateStore } from '@/stores/gameState'
import { useScrollPreservation } from '@/composables/useScrollPreservation'
import { deepEqual } from '@/lib/utils'
import { buildGlobalExchangeProductQuote } from '@/lib/globalExchangeProductQuotes'
import { formatMoney } from '@/lib/currencyFormat'
import { formatInGameTime } from '@/lib/gameTime'
import GlobalExchangeResourcePanel from '@/components/globalExchange/GlobalExchangeResourcePanel.vue'
import GlobalExchangeProductPanel from '@/components/globalExchange/GlobalExchangeProductPanel.vue'
import type { GlobalExchangeOffer, GlobalExchangeProductListing, GlobalExchangeProductQuote, ResourceType, ProductType } from '@/types'

interface City {
  id: string
  name: string
  countryCode: string
  currencyCode: string
  latitude: number
  longitude: number
}

interface ExchangeRow {
  resourceId: string
  resourceName: string
  resourceSlug: string
  unitSymbol: string
  category: string
  offers: GlobalExchangeOffer[]
  bestDeliveredPrice: number
  bestCityId: string
}

interface ProductRow {
  productId: string
  productName: string
  productSlug: string
  productIndustry: string
  unitSymbol: string
  basePrice: number
  marketQuote: GlobalExchangeProductQuote
  listings: GlobalExchangeProductListing[]
  bestPrice: number
}

type MarketMode = 'resources' | 'products'

const { t, locale } = useI18n()
const auth = useAuthStore()
const { selectedCityId } = storeToRefs(auth)
const gameStateStore = useGameStateStore()
const route = useRoute()
const router = useRouter()
const { saveScrollPosition, restoreScrollPosition } = useScrollPreservation()

const loading = ref(true)
const productListingsLoading = ref(false)
const error = ref<string | null>(null)
const productListingsError = ref<string | null>(null)
const cities = ref<City[]>([])
const resources = ref<ResourceType[]>([])
const products = ref<ProductType[]>([])
const allOffers = ref<GlobalExchangeOffer[]>([])
const allProductListings = ref<GlobalExchangeProductListing[]>([])

const search = ref('')
const selectedCategory = ref('ALL')
const marketMode = ref<MarketMode>('resources')
const productSearch = ref('')
const selectedIndustry = ref('ALL')

const CITIES_QUERY = `
  {
    cities {
      id
      name
      countryCode
      currencyCode
      latitude
      longitude
    }
  }
`

const RESOURCES_QUERY = `
  {
    resourceTypes {
      id
      name
      slug
      category
      basePrice
      weightPerUnit
      unitName
      unitSymbol
    }
  }
`

const PRODUCTS_QUERY = `
  {
    productTypes {
      id
      name
      slug
      industry
      basePrice
      unitName
      unitSymbol
      isProOnly
    }
  }
`

const EXCHANGE_QUERY = `
  query GlobalExchangeOffers($destinationCityId: UUID!) {
    globalExchangeOffers(destinationCityId: $destinationCityId) {
      cityId
      cityName
      resourceTypeId
      resourceName
      resourceSlug
      unitSymbol
      localAbundance
      exchangePricePerUnit
      estimatedQuality
      qualityMin
      qualityMax
      transitCostPerUnit
      deliveredPricePerUnit
      distanceKm
      fuelPriceIndex
    }
  }
`

const PRODUCT_LISTINGS_QUERY = `
  query GlobalExchangeProductListings($productTypeId: UUID) {
    globalExchangeProductListings(productTypeId: $productTypeId) {
      orderId
      productTypeId
      productName
      productSlug
      productIndustry
      unitSymbol
      unitName
      basePrice
      pricePerUnit
      remainingQuantity
      sellerCityId
      sellerCityName
      sellerCompanyId
      sellerCompanyName
      createdAtUtc
    }
  }
`

async function loadCitiesAndResources() {
  const [citiesData, resourcesData, productsData] = await Promise.all([
    gqlRequest<{ cities: City[] }>(CITIES_QUERY),
    gqlRequest<{ resourceTypes: ResourceType[] }>(RESOURCES_QUERY),
    gqlRequest<{ productTypes: ProductType[] }>(PRODUCTS_QUERY),
  ])
  if (!deepEqual(cities.value, citiesData.cities)) {
    cities.value = citiesData.cities
  }
  if (!deepEqual(resources.value, resourcesData.resourceTypes)) {
    resources.value = resourcesData.resourceTypes
  }
  if (!deepEqual(products.value, productsData.productTypes)) {
    products.value = productsData.productTypes
  }
  const queryCity = typeof route.query.city === 'string' ? route.query.city : null
  const requestedCity = queryCity ? citiesData.cities.find((city) => city.id === queryCity) : null
  if (requestedCity) {
    auth.switchCity(requestedCity.id)
  } else if (citiesData?.cities && citiesData.cities.length > 0 && !selectedCityId.value) {
    const firstCity = citiesData.cities[0]
    if (firstCity) {
      auth.switchCity(firstCity.id)
    }
  }
  // Pre-fill search from ?resource=<slug> query param
  if (!search.value) {
    const queryResource = typeof route.query.resource === 'string' ? route.query.resource : null
    if (queryResource) {
      search.value = queryResource
    }
  }
  // Restore market mode from query param
  const queryMode = route.query.mode
  if (queryMode === 'products') {
    marketMode.value = 'products'
  }
}

async function loadOffers(isRefresh = false) {
  if (!selectedCityId.value) return
  if (!isRefresh) {
    loading.value = true
  }
  error.value = null
  try {
    const data = await gqlRequest<{ globalExchangeOffers: GlobalExchangeOffer[] }>(EXCHANGE_QUERY, { destinationCityId: selectedCityId.value })
    if (!deepEqual(allOffers.value, data.globalExchangeOffers)) {
      allOffers.value = data.globalExchangeOffers
    }
  } catch (e) {
    error.value = e instanceof Error ? e.message : t('globalExchange.loadFailed')
  } finally {
    loading.value = false
  }
}

async function loadProductListings(isRefresh = false) {
  if (!isRefresh) {
    productListingsLoading.value = true
  }
  productListingsError.value = null
  try {
    const data = await gqlRequest<{ globalExchangeProductListings: GlobalExchangeProductListing[] }>(PRODUCT_LISTINGS_QUERY)
    if (!deepEqual(allProductListings.value, data.globalExchangeProductListings)) {
      allProductListings.value = data.globalExchangeProductListings
    }
  } catch (e) {
    productListingsError.value = e instanceof Error ? e.message : t('globalExchange.loadFailed')
  } finally {
    productListingsLoading.value = false
  }
}

async function refreshAll() {
  // Only refresh the data for the currently visible tab to avoid unnecessary requests
  if (marketMode.value === 'resources') {
    await loadOffers(true)
  } else {
    await loadProductListings(true)
  }
}

onMounted(async () => {
  auth.initFromStorage()
  if (auth.isAuthenticated) {
    void auth.fetchMe()
  }
  gameStateStore.start()
  try {
    await loadCitiesAndResources()
    await Promise.all([loadOffers(), loadProductListings()])
  } catch (e) {
    error.value = e instanceof Error ? e.message : t('globalExchange.loadFailed')
    loading.value = false
  }
})

watch(selectedCityId, async (cityId) => {
  // When the city selection changes in the navbar, reload exchange offers
  if (cityId) {
    await router.replace({
      query: {
        ...route.query,
        city: cityId,
      },
    })
    await loadOffers()
  }
})

watch(marketMode, (mode) => {
  void router.replace({ query: { ...route.query, mode: mode === 'resources' ? undefined : mode } })
})

useTickRefresh(async () => {
  const scrollPos = saveScrollPosition()
  await refreshAll()
  await restoreScrollPosition(scrollPos)
})

const currentTick = computed(() => gameStateStore.gameState?.currentTick ?? null)
const formattedSnapshotTime = computed(() => (gameStateStore.gameState?.currentGameTimeUtc ? formatInGameTime(gameStateStore.gameState.currentGameTimeUtc, locale.value) : ''))

const categories = computed(() => {
  const cats = [...new Set(resources.value.map((r) => r.category))]
  return ['ALL', ...cats]
})

const industries = computed(() => {
  const inds = [...new Set(products.value.map((p) => p.industry))]
  return ['ALL', ...inds]
})

const exchangeRows = computed<ExchangeRow[]>(() => {
  const q = search.value.trim().toLowerCase()
  const filtered = resources.value.filter((r) => {
    const matchesCat = selectedCategory.value === 'ALL' || r.category === selectedCategory.value
    const matchesSearch = !q || r.name.toLowerCase().includes(q) || r.slug.toLowerCase().includes(q)
    return matchesCat && matchesSearch
  })

  return filtered.map((resource) => {
    const offers = allOffers.value.filter((o) => o.resourceTypeId === resource.id)
    const bestOffer = offers.reduce<GlobalExchangeOffer | null>((best, offer) => {
      if (!best) return offer
      return offer.deliveredPricePerUnit < best.deliveredPricePerUnit ? offer : best
    }, null)
    return {
      resourceId: resource.id,
      resourceName: resource.name,
      resourceSlug: resource.slug,
      unitSymbol: resource.unitSymbol,
      category: resource.category,
      offers,
      bestDeliveredPrice: bestOffer?.deliveredPricePerUnit ?? 0,
      bestCityId: bestOffer?.cityId ?? '',
    }
  })
})

const productRows = computed<ProductRow[]>(() => {
  const q = productSearch.value.trim().toLowerCase()
  const filtered = products.value.filter((p) => {
    const matchesInd = selectedIndustry.value === 'ALL' || p.industry === selectedIndustry.value
    const matchesSearch = !q || p.name.toLowerCase().includes(q) || p.slug.toLowerCase().includes(q)
    return matchesInd && matchesSearch
  })

  return filtered.map((product) => {
    const listings = allProductListings.value.filter((l) => l.productTypeId === product.id)
    const marketQuote = buildGlobalExchangeProductQuote(product, currentTick.value ?? 0)
    return {
      productId: product.id,
      productName: product.name,
      productSlug: product.slug,
      productIndustry: product.industry,
      unitSymbol: product.unitSymbol,
      basePrice: product.basePrice,
      marketQuote,
      listings,
      bestPrice: listings.length > 0 ? Math.min(...listings.map((l) => l.pricePerUnit)) : 0,
    }
  })
})

const selectedCityCurrencyCode = computed(() => cities.value.find((c) => c.id === selectedCityId.value)?.currencyCode ?? 'EUR')

function formatPrice(value: number): string {
  return formatMoney(value, selectedCityCurrencyCode.value, locale.value)
}

function formatPercent(value: number): string {
  return `${Math.round(value * 100)}%`
}

function localizedCategory(cat: string): string {
  const map: Record<string, string> = {
    RAW_MATERIAL: t('globalExchange.categoryRaw'),
    MINERAL: t('globalExchange.categoryMineral'),
    ORGANIC: t('globalExchange.categoryOrganic'),
  }
  return map[cat] ?? cat
}

function localizedIndustry(ind: string): string {
  const map: Record<string, string> = {
    FURNITURE: t('globalExchange.industryFurniture'),
    FOOD_PROCESSING: t('globalExchange.industryFoodProcessing'),
    HEALTHCARE: t('globalExchange.industryHealthcare'),
    ELECTRONICS: t('globalExchange.industryElectronics'),
    CONSTRUCTION: t('globalExchange.industryConstruction'),
  }
  return map[ind] ?? ind
}

function priceVsBase(pricePerUnit: number, basePrice: number): string {
  if (basePrice <= 0) return ''
  const diff = pricePerUnit - basePrice
  const pct = Math.round((diff / basePrice) * 100)
  return pct >= 0 ? `+${pct}%` : `${pct}%`
}

function priceVsBaseClass(pricePerUnit: number, basePrice: number): string {
  if (basePrice <= 0) return ''
  const diff = pricePerUnit - basePrice
  return diff > 0 ? 'price-above-base' : diff < 0 ? 'price-below-base' : 'price-at-base'
}
</script>

<template>
  <div class="exchange-view min-h-screen bg-page text-body">
    <div class="exchange-hero border-b border-divider bg-gradient-to-br from-card to-card-raised px-4 py-10 md:py-12">
      <div class="container mx-auto max-w-[1120px]">
        <p class="exchange-eyebrow mb-2 text-xs font-bold uppercase tracking-[0.1em] text-brand">{{ t('globalExchange.eyebrow') }}</p>
        <h1 class="exchange-title mb-2 text-3xl font-bold md:text-4xl">{{ t('globalExchange.title') }}</h1>
        <p class="exchange-subtitle mb-4 max-w-[640px] text-sm text-muted">{{ t('globalExchange.subtitle') }}</p>
        <div class="exchange-hero-meta mt-2 flex flex-wrap items-center gap-3">
          <span
            class="exchange-tick-chip inline-flex cursor-default items-center gap-1.5 rounded-full border border-divider bg-card px-2.5 py-1 text-xs"
            :title="currentTick !== null ? t('globalExchange.tickHint', { tick: currentTick }) : undefined"
          >
            <span class="exchange-tick-label text-[0.7rem] font-semibold uppercase tracking-[0.05em] text-muted">{{ t('globalExchange.snapshotTime') }}</span>
            <span class="exchange-tick-value font-bold text-brand">{{ formattedSnapshotTime || '—' }}</span>
          </span>
          <span
            v-if="marketMode === 'resources'"
            class="exchange-supply-chip inline-flex items-center rounded-full border border-[color-mix(in_srgb,var(--color-success,#22c55e)_30%,transparent)] bg-[color-mix(in_srgb,var(--color-success,#22c55e)_8%,transparent)] px-2.5 py-1 text-[0.7rem] font-semibold text-[var(--color-success,#22c55e)]"
          >
            {{ t('globalExchange.endlessSupply') }}
          </span>
        </div>
      </div>
    </div>

    <div class="container exchange-body mx-auto max-w-[1120px] px-4 py-8 md:py-10">
      <div class="market-mode-tabs mb-6 flex gap-2 border-b-2 border-divider" role="tablist" :aria-label="'Market type'">
        <button
          role="tab"
          :aria-selected="marketMode === 'resources'"
          :class="[
            'mode-tab -mb-0.5 rounded-t-md border-b-2 border-transparent px-5 py-2 text-sm font-semibold text-muted transition-colors',
            marketMode === 'resources' ? 'active border-brand text-brand' : 'hover:text-body',
          ]"
          @click="marketMode = 'resources'"
        >
          {{ t('globalExchange.modeResources') }}
        </button>
        <button
          role="tab"
          :aria-selected="marketMode === 'products'"
          :class="[
            'mode-tab mode-tab-products -mb-0.5 rounded-t-md border-b-2 border-transparent px-5 py-2 text-sm font-semibold text-muted transition-colors',
            marketMode === 'products' ? 'active border-[var(--color-accent,#a855f7)] text-[var(--color-accent,#a855f7)]' : 'hover:text-body',
          ]"
          @click="marketMode = 'products'"
        >
          {{ t('globalExchange.modeProducts') }}
        </button>
      </div>

      <GlobalExchangeResourcePanel
        v-if="marketMode === 'resources'"
        :cities="cities"
        :selected-city-id="selectedCityId"
        :search="search"
        :selected-category="selectedCategory"
        :categories="categories"
        :loading="loading"
        :error="error"
        :exchange-rows="exchangeRows"
        :format-price="formatPrice"
        :format-percent="formatPercent"
        :localized-category="localizedCategory"
        @update:search="search = $event"
        @update:selected-category="selectedCategory = $event"
        @switch-city="auth.switchCity($event)"
        @retry="loadOffers"
      />

      <GlobalExchangeProductPanel
        v-else
        :search="productSearch"
        :selected-industry="selectedIndustry"
        :industries="industries"
        :loading="productListingsLoading"
        :error="productListingsError"
        :product-rows="productRows"
        :format-price="formatPrice"
        :localized-industry="localizedIndustry"
        :price-vs-base="priceVsBase"
        :price-vs-base-class="priceVsBaseClass"
        @update:search="productSearch = $event"
        @update:selected-industry="selectedIndustry = $event"
        @retry="loadProductListings"
      />
    </div>
  </div>
</template>

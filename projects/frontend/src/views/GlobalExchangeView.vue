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
import UiStateLoading from '@/components/ui/UiStateLoading.vue'
import UiStateError from '@/components/ui/UiStateError.vue'
import UiStateEmpty from '@/components/ui/UiStateEmpty.vue'
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

const productRowsEmpty = computed(() => productRows.value.length === 0)

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
  <div class="exchange-view">
    <div class="exchange-hero">
      <div class="container">
        <p class="exchange-eyebrow">{{ t('globalExchange.eyebrow') }}</p>
        <h1 class="exchange-title">{{ t('globalExchange.title') }}</h1>
        <p class="exchange-subtitle">{{ t('globalExchange.subtitle') }}</p>
        <div class="exchange-hero-meta">
          <span class="exchange-tick-chip" :title="currentTick !== null ? t('globalExchange.tickHint', { tick: currentTick }) : undefined">
            <span class="exchange-tick-label">{{ t('globalExchange.snapshotTime') }}</span>
            <span class="exchange-tick-value">{{ formattedSnapshotTime || '—' }}</span>
          </span>
          <span v-if="marketMode === 'resources'" class="exchange-supply-chip">{{ t('globalExchange.endlessSupply') }}</span>
        </div>
      </div>
    </div>

    <div class="container exchange-body">
      <!-- Market mode toggle: Resources / Products -->
      <div class="market-mode-tabs" role="tablist" :aria-label="'Market type'">
        <button role="tab" :aria-selected="marketMode === 'resources'" :class="['mode-tab', { active: marketMode === 'resources' }]" @click="marketMode = 'resources'">
          {{ t('globalExchange.modeResources') }}
        </button>
        <button role="tab" :aria-selected="marketMode === 'products'" :class="['mode-tab', 'mode-tab-products', { active: marketMode === 'products' }]" @click="marketMode = 'products'">
          {{ t('globalExchange.modeProducts') }}
        </button>
      </div>

      <!-- ── Resources mode ── -->
      <template v-if="marketMode === 'resources'">
        <div v-if="cities.length > 0" class="exchange-city-tabs city-tabs" role="tablist" :aria-label="t('common.city')">
          <button
            v-for="city in cities"
            :key="city.id"
            role="tab"
            :aria-selected="selectedCityId === city.id"
            :class="['exchange-city-tab', 'city-tab', { active: selectedCityId === city.id }]"
            @click="auth.switchCity(city.id)"
          >
            {{ city.name }}
          </button>
        </div>

        <!-- Search and filter row -->
        <div class="exchange-filters">
          <div class="search-wrapper">
            <input v-model="search" type="search" class="search-input" :placeholder="t('globalExchange.searchPlaceholder')" :aria-label="t('globalExchange.searchPlaceholder')" />
          </div>
          <div class="category-filter">
            <label for="category-select" class="filter-label">{{ t('globalExchange.filterCategory') }}</label>
            <select id="category-select" v-model="selectedCategory" class="filter-select">
              <option v-for="cat in categories" :key="cat" :value="cat">
                {{ cat === 'ALL' ? t('globalExchange.allCategories') : localizedCategory(cat) }}
              </option>
            </select>
          </div>
        </div>

        <!-- Loading / error -->
        <UiStateLoading v-if="loading" class="exchange-loading" :label="t('common.loading')" />
        <UiStateError v-else-if="error" class="exchange-error" :message="error" :retry-label="t('common.retry')" @retry="loadOffers" />
        <UiStateEmpty v-else-if="exchangeRows.length === 0" class="exchange-empty">
          {{ t('globalExchange.noResults') }}
        </UiStateEmpty>

        <!-- Resource rows -->
        <template v-else>
          <div v-for="row in exchangeRows" :key="row.resourceId" class="resource-row" :data-slug="row.resourceSlug">
            <div class="resource-row-header">
              <span class="resource-name">{{ row.resourceName }}</span>
              <span class="resource-category-badge">{{ localizedCategory(row.category) }}</span>
              <RouterLink :to="`/encyclopedia/resources/${row.resourceSlug}`" class="production-chain-link" :aria-label="`${t('globalExchange.viewProductionChain')}: ${row.resourceName}`">{{
                t('globalExchange.viewProductionChain')
              }}</RouterLink>
            </div>

            <div class="city-offers-grid">
              <div v-for="offer in row.offers" :key="offer.cityId" :class="['city-offer-card', { 'best-offer': offer.cityId === row.bestCityId }]">
                <div class="offer-card-header">
                  <strong class="offer-city-name">{{ offer.cityName }}</strong>
                  <span v-if="offer.cityId === row.bestCityId" class="best-badge" :title="t('globalExchange.bestDeliveredHint')">{{ t('globalExchange.bestDelivered') }}</span>
                </div>

                <div class="offer-metrics">
                  <div class="offer-metric">
                    <span class="metric-label">{{ t('globalExchange.exchangePrice') }}</span>
                    <span class="metric-value exchange-price"> {{ formatPrice(offer.exchangePricePerUnit) }}/{{ offer.unitSymbol }} </span>
                  </div>
                  <div class="offer-metric">
                    <span class="metric-label">{{ t('globalExchange.transitCost') }}</span>
                    <span class="metric-value transit-cost">
                      +{{ formatPrice(offer.transitCostPerUnit) }} · {{ offer.distanceKm }} km
                      <span
                        v-if="offer.fuelPriceIndex && offer.fuelPriceIndex !== 1"
                        class="fuel-badge"
                        :class="offer.fuelPriceIndex > 1 ? 'fuel-high' : 'fuel-low'"
                        :title="t('globalExchange.fuelPriceHint')"
                        >⛽ ×{{ offer.fuelPriceIndex.toFixed(2) }}</span
                      >
                    </span>
                  </div>
                  <div class="offer-metric delivered-metric">
                    <span class="metric-label">{{ t('globalExchange.deliveredPrice') }}</span>
                    <span class="metric-value delivered-price"> {{ formatPrice(offer.deliveredPricePerUnit) }}/{{ offer.unitSymbol }} </span>
                  </div>
                  <div class="offer-metric">
                    <span class="metric-label">{{ t('globalExchange.quality') }}</span>
                    <span class="metric-value quality-value">
                      <span class="quality-range"> {{ formatPercent(offer.qualityMin) }}&nbsp;–&nbsp;{{ formatPercent(offer.qualityMax) }} </span>
                      <span class="quality-band-bar" :title="t('globalExchange.qualityBandHint')">
                        <span
                          class="quality-band-fill"
                          :style="{
                            left: `${offer.qualityMin * 100}%`,
                            width: `${(offer.qualityMax - offer.qualityMin) * 100}%`,
                          }"
                        ></span>
                        <span class="quality-band-center" :style="{ left: `${offer.estimatedQuality * 100}%` }"></span>
                      </span>
                    </span>
                  </div>
                  <div class="offer-metric">
                    <span class="metric-label">{{ t('globalExchange.abundance') }}</span>
                    <span class="metric-value">{{ formatPercent(offer.localAbundance) }}</span>
                  </div>
                </div>
              </div>

              <p v-if="row.offers.length === 0" class="no-offers-hint">
                {{ t('globalExchange.noOffersForResource') }}
              </p>
            </div>
          </div>
        </template>
      </template>

      <!-- ── Products mode ── -->
      <template v-else>
        <!-- Product search and filter -->
        <div class="exchange-filters">
          <div class="search-wrapper">
            <input v-model="productSearch" type="search" class="search-input" :placeholder="t('globalExchange.productSearchPlaceholder')" :aria-label="t('globalExchange.productSearchPlaceholder')" />
          </div>
          <div class="category-filter">
            <label for="industry-select" class="filter-label">{{ t('globalExchange.filterIndustry') }}</label>
            <select id="industry-select" v-model="selectedIndustry" class="filter-select">
              <option v-for="ind in industries" :key="ind" :value="ind">
                {{ ind === 'ALL' ? t('globalExchange.allIndustries') : localizedIndustry(ind) }}
              </option>
            </select>
          </div>
        </div>

        <!-- Products mode hint -->
        <p class="products-mode-hint">{{ t('globalExchange.modeProductsHint') }}</p>

        <!-- Loading / error -->
        <UiStateLoading v-if="productListingsLoading" class="exchange-loading" :label="t('common.loading')" />
        <UiStateError v-else-if="productListingsError" class="exchange-error" :message="productListingsError" :retry-label="t('common.retry')" @retry="loadProductListings" />
        <UiStateEmpty v-else-if="productRowsEmpty" class="exchange-empty">
          {{ t('globalExchange.noProductResults') }}
        </UiStateEmpty>

        <!-- Product rows -->
        <template v-else>
          <div v-for="row in productRows" :key="row.productId" class="product-row" :data-slug="row.productSlug">
            <div class="product-row-header">
              <span class="product-name">{{ row.productName }}</span>
              <span class="product-industry-badge">{{ localizedIndustry(row.productIndustry) }}</span>
              <span class="product-listing-count">{{ t('globalExchange.productListingsCount', { count: row.listings.length }) }}</span>
              <RouterLink :to="`/encyclopedia/products/${row.productSlug}`" class="production-chain-link" :aria-label="`${t('globalExchange.viewProductDetail')}: ${row.productName}`">{{
                t('globalExchange.viewProductDetail')
              }}</RouterLink>
            </div>

            <div class="product-market-quote-grid">
              <div class="product-market-quote">
                <span class="metric-label">{{ t('globalExchange.productBasePrice') }}</span>
                <strong>{{ formatPrice(row.marketQuote.basePrice) }}/{{ row.unitSymbol }}</strong>
              </div>
              <div class="product-market-quote">
                <span class="metric-label">{{ t('globalExchange.productBidPrice') }}</span>
                <strong class="listing-price">{{ formatPrice(row.marketQuote.bidPricePerUnit) }}/{{ row.unitSymbol }}</strong>
              </div>
              <div class="product-market-quote">
                <span class="metric-label">{{ t('globalExchange.productAskPrice') }}</span>
                <strong class="listing-price">{{ formatPrice(row.marketQuote.offerPricePerUnit) }}/{{ row.unitSymbol }}</strong>
              </div>
              <div class="product-market-quote">
                <span class="metric-label">{{ t('globalExchange.quality') }}</span>
                <strong>{{ formatPercent(row.marketQuote.estimatedQuality) }}</strong>
              </div>
            </div>

            <div v-if="row.listings.length > 0" class="product-listings-table">
              <div class="listings-header">
                <span>{{ t('globalExchange.productPlayerAskPrice') }}</span>
                <span>{{ t('globalExchange.productPriceVsBase') }}</span>
                <span>{{ t('globalExchange.productAvailable') }}</span>
                <span>{{ t('globalExchange.productSeller') }}</span>
                <span>{{ t('globalExchange.productCity') }}</span>
              </div>
              <div v-for="listing in row.listings" :key="listing.orderId" class="listing-row">
                <span class="listing-price"> {{ formatPrice(listing.pricePerUnit) }}/{{ listing.unitSymbol }} </span>
                <span :class="['listing-vs-base', priceVsBaseClass(listing.pricePerUnit, row.basePrice)]">
                  {{ priceVsBase(listing.pricePerUnit, row.basePrice) }}
                </span>
                <span class="listing-quantity"> {{ listing.remainingQuantity.toFixed(0) }} {{ listing.unitSymbol }} </span>
                <span class="listing-seller">{{ listing.sellerCompanyName }}</span>
                <span class="listing-city">{{ listing.sellerCityName }}</span>
              </div>
            </div>
            <p v-else class="no-product-listings-hint">{{ t('globalExchange.noProductListingsHint') }}</p>
          </div>
        </template>
      </template>
    </div>
  </div>
</template>

<style scoped src="./GlobalExchangeView.css"></style>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { useTickRefresh } from '@/composables/useTickRefresh'
import { useGameStateStore } from '@/stores/gameState'
import { useScrollPreservation } from '@/composables/useScrollPreservation'
import { deepEqual } from '@/lib/utils'
import { formatMoney } from '@/lib/currencyFormat'
import { formatInGameTime } from '@/lib/gameTime'
import type { MarketIntelligenceResult } from '@/types'

interface City {
  id: string
  name: string
  currencyCode: string
}

const { t, locale } = useI18n()
const auth = useAuthStore()
const gameStateStore = useGameStateStore()
const route = useRoute()
const router = useRouter()
const { selectedCityId } = storeToRefs(auth)
const { saveScrollPosition, restoreScrollPosition } = useScrollPreservation()

const loading = ref(true)
const error = ref<string | null>(null)
const cities = ref<City[]>([])
const intelligence = ref<MarketIntelligenceResult | null>(null)

const CITIES_QUERY = `
  {
    cities {
      id
      name
      currencyCode
    }
  }
`

const MARKET_INTELLIGENCE_QUERY = `
  query MarketIntelligence($cityId: UUID!) {
    marketIntelligence(cityId: $cityId) {
      cityId
      cityName
      dataFromTick
      dataToTick
      products {
        productTypeId
        productName
        productSlug
        totalWeeklySalesVolume
        sellers {
          rank
          companyId
          displayName
          askingPricePerUnit
          brandQuality
          estimatedWeeklySalesVolume
          marketShare
        }
      }
    }
  }
`

async function loadCities() {
  const data = await gqlRequest<{ cities: City[] }>(CITIES_QUERY)
  if (!deepEqual(cities.value, data.cities)) {
    cities.value = data.cities
  }

  const queryCity = typeof route.query.city === 'string' ? route.query.city : null
  const requestedCity = queryCity ? data.cities.find((city) => city.id === queryCity) : null
  if (requestedCity) {
    auth.switchCity(requestedCity.id)
    return
  }

  if (!selectedCityId.value && data.cities.length > 0) {
    const first = data.cities[0]
    if (first) {
      auth.switchCity(first.id)
    }
  }
}

async function loadMarketIntelligence(isRefresh = false) {
  if (!selectedCityId.value) return
  if (!isRefresh) {
    loading.value = true
  }
  error.value = null

  try {
    const data = await gqlRequest<{ marketIntelligence: MarketIntelligenceResult | null }>(MARKET_INTELLIGENCE_QUERY, {
      cityId: selectedCityId.value,
    })
    const next = data.marketIntelligence ?? null
    if (!deepEqual(intelligence.value, next)) {
      intelligence.value = next
    }
  } catch (e) {
    error.value = e instanceof Error ? e.message : t('marketIntelligence.loadFailed')
    intelligence.value = null
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  auth.initFromStorage()
  if (!auth.isAuthenticated) {
    await router.push('/login')
    return
  }

  if (!auth.player) {
    await auth.fetchMe()
  }

  gameStateStore.start()

  try {
    await loadCities()
    await loadMarketIntelligence()
  } catch (e) {
    error.value = e instanceof Error ? e.message : t('marketIntelligence.loadFailed')
  } finally {
    loading.value = false
  }
})

watch(selectedCityId, async (cityId) => {
  if (!cityId) return
  await router.replace({
    query: {
      ...route.query,
      city: cityId,
    },
  })
  await loadMarketIntelligence()
})

useTickRefresh(async () => {
  const scrollPos = saveScrollPosition()
  await loadMarketIntelligence(true)
  await restoreScrollPosition(scrollPos)
})

const selectedCityCurrencyCode = computed(() => cities.value.find((city) => city.id === selectedCityId.value)?.currencyCode ?? 'EUR')
const formattedSnapshotTime = computed(() => (gameStateStore.gameState?.currentGameTimeUtc ? formatInGameTime(gameStateStore.gameState.currentGameTimeUtc, locale.value) : ''))

function formatCurrency(value: number): string {
  return formatMoney(value, selectedCityCurrencyCode.value, locale.value)
}

function formatPercent(value: number): string {
  return `${Math.round(value * 100)}%`
}

function formatWeeklyVolume(value: number): string {
  return Number.isFinite(value) ? value.toFixed(0) : '0'
}
</script>

<template>
  <div class="market-intelligence-view min-h-screen bg-page text-body">
    <div class="border-b border-divider bg-gradient-to-br from-card to-card-raised px-4 py-10 md:py-12">
      <div class="container mx-auto">
        <p class="mb-2 text-xs font-bold uppercase tracking-[0.1em] text-brand">{{ t('marketIntelligence.eyebrow') }}</p>
        <h1 class="mb-2 text-3xl font-bold md:text-4xl">{{ t('marketIntelligence.title') }}</h1>
        <p class="max-w-[760px] text-sm text-muted">{{ t('marketIntelligence.subtitle') }}</p>
        <div class="mt-4 inline-flex items-center gap-2 rounded-full border border-divider bg-card px-3 py-1.5 text-xs">
          <span class="font-semibold uppercase tracking-[0.05em] text-muted">{{ t('marketIntelligence.snapshotTime') }}</span>
          <span class="font-bold text-brand">{{ formattedSnapshotTime || '—' }}</span>
        </div>
      </div>
    </div>

    <div class="container mx-auto px-4 py-8 md:py-10">
      <div v-if="cities.length > 0" class="city-tabs mb-6 flex flex-wrap gap-2" role="tablist" :aria-label="t('common.city')">
        <button
          v-for="city in cities"
          :key="city.id"
          role="tab"
          :aria-selected="selectedCityId === city.id"
          :class="[
            'rounded-md border px-3 py-1.5 text-sm font-medium transition-colors',
            selectedCityId === city.id ? 'border-brand bg-brand/10 text-brand' : 'border-divider bg-card text-muted hover:border-brand hover:text-body',
          ]"
          @click="auth.switchCity(city.id)"
        >
          {{ city.name }}
        </button>
      </div>

      <p v-if="loading" class="text-sm text-muted">{{ t('common.loading') }}</p>
      <p v-else-if="error" class="rounded-md border border-red-400/40 bg-red-500/10 px-3 py-2 text-sm text-red-700 dark:text-red-300">{{ error }}</p>
      <p v-else-if="!intelligence || intelligence.products.length === 0" class="rounded-md border border-divider bg-card px-3 py-2 text-sm text-muted">
        {{ t('marketIntelligence.empty') }}
      </p>

      <div v-else class="space-y-5">
        <article v-for="product in intelligence.products" :key="product.productTypeId" class="overflow-hidden rounded-xl border border-divider bg-card" :data-product-slug="product.productSlug">
          <header class="flex flex-wrap items-center gap-2 border-b border-divider bg-brand/5 px-4 py-3">
            <h2 class="text-base font-bold text-foreground">{{ product.productName }}</h2>
            <span class="rounded-full bg-card px-2 py-0.5 text-[0.68rem] font-semibold uppercase tracking-[0.05em] text-muted">
              {{ t('marketIntelligence.totalWeeklySales') }}: {{ formatWeeklyVolume(product.totalWeeklySalesVolume) }}
            </span>
          </header>

          <div class="overflow-x-auto">
            <table class="min-w-full border-separate border-spacing-0">
              <thead>
                <tr class="bg-surface text-left text-[0.7rem] font-bold uppercase tracking-[0.05em] text-muted">
                  <th class="px-4 py-3">{{ t('marketIntelligence.columns.rank') }}</th>
                  <th class="px-4 py-3">{{ t('marketIntelligence.columns.seller') }}</th>
                  <th class="px-4 py-3">{{ t('marketIntelligence.columns.askingPrice') }}</th>
                  <th class="px-4 py-3">{{ t('marketIntelligence.columns.brandQuality') }}</th>
                  <th class="px-4 py-3">{{ t('marketIntelligence.columns.weeklySales') }}</th>
                  <th class="px-4 py-3">{{ t('marketIntelligence.columns.marketShare') }}</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="seller in product.sellers" :key="`${product.productTypeId}-${seller.companyId}`" class="border-t border-divider text-sm text-body">
                  <td class="px-4 py-3 align-middle">
                    <span class="inline-flex h-6 min-w-6 items-center justify-center rounded-full border border-divider bg-page px-2 text-[0.7rem] font-bold text-brand"> #{{ seller.rank }} </span>
                  </td>
                  <td class="px-4 py-3 font-semibold">{{ seller.displayName }}</td>
                  <td class="px-4 py-3">{{ formatCurrency(seller.askingPricePerUnit) }}</td>
                  <td class="px-4 py-3">{{ seller.brandQuality === null ? '—' : formatPercent(seller.brandQuality) }}</td>
                  <td class="px-4 py-3">{{ formatWeeklyVolume(seller.estimatedWeeklySalesVolume) }}</td>
                  <td class="px-4 py-3">{{ formatPercent(seller.marketShare) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </article>
      </div>
    </div>
  </div>
</template>

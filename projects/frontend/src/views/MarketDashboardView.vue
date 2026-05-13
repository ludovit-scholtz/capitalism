<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { useTickRefresh } from '@/composables/useTickRefresh'
import { formatMoney } from '@/lib/currencyFormat'
import type { CityDemandSummaryResult, ProductDemandEntry } from '@/types/analytics'
import MarketPriceHistoryPanel from '@/components/market/MarketPriceHistoryPanel.vue'

interface City {
  id: string
  name: string
  currencyCode: string
}

const { t } = useI18n()
const auth = useAuthStore()
const { selectedCityId } = storeToRefs(auth)

const loading = ref(true)
const error = ref<string | null>(null)
const cities = ref<City[]>([])
const overviewData = ref<CityDemandSummaryResult[]>([])
const selectedProduct = ref<ProductDemandEntry | null>(null)

interface CompetitorQualityEntry {
  companyId: string
  companyName: string
  qualityLevel: number
  pricePremiumPct: number
  isOwnCompany: boolean
}

const competitorEntries = ref<CompetitorQualityEntry[]>([])
const competitorLoading = ref(false)
const competitorError = ref<string | null>(null)

const CITIES_QUERY = `{ cities { id name currencyCode } }`

const COMPETITOR_INTELLIGENCE_QUERY = `
  query CompetitorIntelligence($cityId: UUID!, $productTypeId: UUID!) {
    competitorQualityIntelligence(cityId: $cityId, productTypeId: $productTypeId) {
      companyId
      companyName
      qualityLevel
      pricePremiumPct
      isOwnCompany
    }
  }
`

const MARKET_OVERVIEW_QUERY = `
  query MarketOverview($topN: Int!, $lastNTicks: Int!) {
    marketOverview(topN: $topN, lastNTicks: $lastNTicks) {
      cityId
      cityName
      currencyCode
      fromTick
      toTick
      products {
        productTypeId
        productName
        industry
        totalDemand
        totalQuantitySold
        satisfactionRate
        averageClearingPrice
        totalRevenue
        sellerCount
      }
    }
  }
`

const activeCityData = computed<CityDemandSummaryResult | null>(() => {
  if (!selectedCityId.value) return overviewData.value[0] ?? null
  return overviewData.value.find((c) => c.cityId === selectedCityId.value) ?? overviewData.value[0] ?? null
})

function satisfactionClass(rate: number): string {
  if (rate >= 0.8) return 'satisfaction-good'
  if (rate >= 0.4) return 'satisfaction-partial'
  return 'satisfaction-poor'
}

function satisfactionLabel(rate: number): string {
  if (rate >= 0.8) return t('marketDashboard.satisfactionGood')
  if (rate >= 0.4) return t('marketDashboard.satisfactionPartial')
  return t('marketDashboard.satisfactionPoor')
}

function selectProduct(product: ProductDemandEntry) {
  if (selectedProduct.value?.productTypeId === product.productTypeId) {
    selectedProduct.value = null
    competitorEntries.value = []
  } else {
    selectedProduct.value = product
    loadCompetitorIntelligence(product.productTypeId)
  }
}

async function loadCompetitorIntelligence(productTypeId: string) {
  const cityId = activeCityData.value?.cityId
  if (!cityId) return

  competitorLoading.value = true
  competitorError.value = null
  competitorEntries.value = []
  try {
    const result = await gqlRequest<{ competitorQualityIntelligence: CompetitorQualityEntry[] }>(
      COMPETITOR_INTELLIGENCE_QUERY,
      { cityId, productTypeId },
    )
    competitorEntries.value = result.competitorQualityIntelligence ?? []
  } catch (err) {
    console.error('[CompetitorIntelligence] Failed to load competitor intelligence:', err)
    competitorError.value = t('research.competitors.loadFailed')
  } finally {
    competitorLoading.value = false
  }
}

function rankMedal(rank: number): string {
  if (rank === 1) return '🥇'
  if (rank === 2) return '🥈'
  if (rank === 3) return '🥉'
  return String(rank)
}

function qualityBadgeClass(level: number): string {
  if (level >= 8) return 'quality-gold'
  if (level >= 5) return 'quality-green'
  if (level >= 2) return 'quality-blue'
  return 'quality-dim'
}

async function loadData(isRefresh = false) {
  if (!isRefresh) loading.value = true
  error.value = null
  try {
    const citiesResult = await gqlRequest<{ cities: City[] }>(CITIES_QUERY)
    cities.value = citiesResult.cities

    const overviewResult = await gqlRequest<{ marketOverview: CityDemandSummaryResult[] }>(
      MARKET_OVERVIEW_QUERY,
      { topN: 10, lastNTicks: 100 },
    )
    overviewData.value = overviewResult.marketOverview
  } catch {
    error.value = t('marketDashboard.loadFailed')
  } finally {
    loading.value = false
  }
}

onMounted(() => loadData())
useTickRefresh(() => loadData(true))
</script>

<template>
  <div class="market-dashboard">
    <header class="page-header">
      <p class="page-eyebrow">{{ t('marketDashboard.eyebrow') }}</p>
      <h1 class="page-title">{{ t('marketDashboard.title') }}</h1>
      <p class="page-subtitle">{{ t('marketDashboard.subtitle') }}</p>
    </header>

    <!-- City tabs -->
    <nav v-if="cities.length > 0" class="city-tabs" aria-label="City tabs">
      <button
        v-for="city in cities"
        :key="city.id"
        class="city-tab"
        :class="{ 'city-tab--active': activeCityData?.cityId === city.id }"
        @click="auth.switchCity(city.id)"
      >
        {{ city.name }}
      </button>
    </nav>

    <!-- Loading / error -->
    <div v-if="loading" class="market-loading">
      <span class="spinner" aria-busy="true" />
    </div>
    <div v-else-if="error" class="market-error">{{ error }}</div>
    <template v-else-if="activeCityData">
      <div v-if="activeCityData.products.length === 0" class="market-empty">
        {{ t('marketDashboard.empty') }}
      </div>
      <div v-else class="products-grid" role="table" :aria-label="t('marketDashboard.allProducts')">
        <!-- Table header -->
        <div class="products-header" role="row">
          <span role="columnheader">{{ t('marketDashboard.product') }}</span>
          <span role="columnheader">{{ t('marketDashboard.industryLabel') }}</span>
          <span role="columnheader" class="col-right">{{ t('marketDashboard.clearingPrice') }}</span>
          <span role="columnheader" class="col-right">{{ t('marketDashboard.demand') }}</span>
          <span role="columnheader" class="col-right">{{ t('marketDashboard.sold') }}</span>
          <span role="columnheader">{{ t('marketDashboard.satisfactionRate') }}</span>
          <span role="columnheader" class="col-right">{{ t('marketDashboard.sellers') }}</span>
        </div>

        <!-- Product rows -->
        <div
          v-for="product in activeCityData.products"
          :key="product.productTypeId"
          class="product-row"
          :class="{ 'product-row--selected': selectedProduct?.productTypeId === product.productTypeId }"
          role="row"
          tabindex="0"
          :aria-selected="selectedProduct?.productTypeId === product.productTypeId"
          @click="selectProduct(product)"
          @keydown.enter="selectProduct(product)"
          @keydown.space.prevent="selectProduct(product)"
        >
          <span class="product-name" role="cell">{{ product.productName }}</span>
          <span class="product-industry" role="cell">{{ product.industry }}</span>
          <span class="product-price col-right" role="cell">
            {{ formatMoney(product.averageClearingPrice, activeCityData.currencyCode) }}
          </span>
          <span class="product-demand col-right" role="cell">
            {{ Math.round(product.totalDemand).toLocaleString() }}
          </span>
          <span class="product-sold col-right" role="cell">
            {{ Math.round(product.totalQuantitySold).toLocaleString() }}
          </span>
          <span class="product-satisfaction" role="cell">
            <span
              class="satisfaction-badge"
              :class="satisfactionClass(product.satisfactionRate)"
            >
              {{ satisfactionLabel(product.satisfactionRate) }}
            </span>
            <span class="satisfaction-bar-wrap">
              <span
                class="satisfaction-bar-fill"
                :class="satisfactionClass(product.satisfactionRate)"
                :style="{ width: `${Math.round(product.satisfactionRate * 100)}%` }"
                :aria-valuenow="Math.round(product.satisfactionRate * 100)"
                aria-valuemin="0"
                aria-valuemax="100"
                role="progressbar"
                :aria-label="`${product.productName} satisfaction: ${Math.round(product.satisfactionRate * 100)}%`"
              />
            </span>
          </span>
          <span class="product-sellers col-right" role="cell">{{ product.sellerCount }}</span>
        </div>
      </div>

      <!-- Price history panel (shown when a product row is clicked) -->
      <MarketPriceHistoryPanel
        v-if="selectedProduct && activeCityData"
        :product="selectedProduct"
        :city-id="activeCityData.cityId"
        :currency-code="activeCityData.currencyCode"
      />

      <!-- Competitor Intelligence (shown when a product row is clicked) -->
      <section v-if="selectedProduct" class="competitor-section">
        <header class="competitor-header">
          <h2 class="competitor-title">{{ t('research.competitors.title') }}</h2>
          <p class="competitor-subtitle">{{ t('research.competitors.subtitle') }}</p>
        </header>

        <div v-if="competitorLoading" class="market-loading">
          <span class="spinner" aria-busy="true" />
        </div>
        <p v-else-if="competitorError" class="market-error">{{ competitorError }}</p>
        <p v-else-if="competitorEntries.length === 0" class="market-empty">
          {{ t('research.competitors.noCompetitors') }}
        </p>
        <div v-else class="competitor-grid" role="table" :aria-label="t('research.competitors.title')">
          <div class="competitor-grid-header" role="row">
            <span role="columnheader">{{ t('research.competitors.rank') }}</span>
            <span role="columnheader">{{ t('research.competitors.company') }}</span>
            <span role="columnheader" class="col-right">{{ t('research.competitors.qualityLevel') }}</span>
            <span role="columnheader" class="col-right">{{ t('research.competitors.pricePremium') }}</span>
          </div>
          <div
            v-for="(entry, index) in competitorEntries"
            :key="entry.companyId"
            class="competitor-row"
            :class="{ 'competitor-row--own': entry.isOwnCompany }"
            role="row"
          >
            <span class="competitor-rank" role="cell">{{ rankMedal(index + 1) }}</span>
            <span class="competitor-name" role="cell">
              {{ entry.companyName }}
              <span v-if="entry.isOwnCompany" class="you-badge">{{ t('research.competitors.you') }}</span>
            </span>
            <span class="competitor-quality col-right" role="cell">
              <span class="quality-badge" :class="qualityBadgeClass(entry.qualityLevel)">
                {{ entry.qualityLevel.toFixed(1) }}
              </span>
            </span>
            <span class="competitor-premium col-right" role="cell">{{ entry.pricePremiumPct > 0 ? '+' : '' }}{{ entry.pricePremiumPct.toFixed(1) }}%</span>
          </div>
        </div>
      </section>
    </template>
    <div v-else class="market-empty">{{ t('marketDashboard.noData') }}</div>
  </div>
</template>

<style scoped>
.market-dashboard {
  max-width: 1200px;
  margin: 0 auto;
  padding: 1.5rem 1.5rem 4rem;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.page-header {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.page-eyebrow {
  font-size: 0.75rem;
  font-weight: 600;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--color-accent);
}

.page-title {
  font-size: 1.75rem;
  font-weight: 700;
  color: var(--color-text-primary);
  margin: 0;
}

.page-subtitle {
  font-size: 0.9rem;
  color: var(--color-text-secondary);
  margin: 0;
}

/* City tabs */
.city-tabs {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
  border-bottom: 1px solid var(--color-divider);
  padding-bottom: 0.5rem;
}

.city-tab {
  padding: 0.4rem 1rem;
  border-radius: 6px 6px 0 0;
  border: 1px solid transparent;
  background: transparent;
  color: var(--color-text-secondary);
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  transition: background 0.15s, color 0.15s;
}

.city-tab:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
}

.city-tab--active {
  border-color: var(--color-divider);
  border-bottom-color: var(--color-bg-primary);
  color: var(--color-accent);
  background: var(--color-bg-primary);
}

/* Loading / error */
.market-loading {
  display: flex;
  justify-content: center;
  padding: 3rem;
}

.spinner {
  width: 2rem;
  height: 2rem;
  border: 3px solid var(--color-divider);
  border-top-color: var(--color-accent);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.market-error,
.market-empty {
  padding: 2rem;
  text-align: center;
  color: var(--color-text-secondary);
  font-size: 0.95rem;
}

.market-error {
  color: var(--color-error);
}

/* Products grid */
.products-grid {
  display: flex;
  flex-direction: column;
  background: var(--color-card-bg);
  border: 1px solid var(--color-divider);
  border-radius: 8px;
  overflow: hidden;
}

.products-header,
.product-row {
  display: grid;
  grid-template-columns: 2fr 1.2fr 1fr 1fr 1fr 2fr 0.8fr;
  gap: 0;
  padding: 0.7rem 1.2rem;
  align-items: center;
}

.products-header {
  background: var(--color-surface-muted);
  border-bottom: 1px solid var(--color-divider);
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--color-text-secondary);
}

.product-row {
  border-bottom: 1px solid var(--color-divider);
  font-size: 0.9rem;
  transition: background 0.1s;
}

.product-row:last-child {
  border-bottom: none;
}

.product-row:hover {
  background: var(--color-surface-hover);
  cursor: pointer;
}

.product-row--selected {
  background: var(--color-surface-hover);
  border-left: 3px solid var(--color-accent);
}

.col-right {
  text-align: right;
}

.product-name {
  font-weight: 600;
  color: var(--color-text-primary);
}

.product-industry {
  color: var(--color-text-secondary);
  font-size: 0.85rem;
}

.product-price {
  font-weight: 600;
  color: var(--color-text-primary);
}

.product-demand,
.product-sold {
  color: var(--color-text-secondary);
  font-variant-numeric: tabular-nums;
}

.product-satisfaction {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.satisfaction-badge {
  font-size: 0.75rem;
  font-weight: 600;
  padding: 0.1rem 0.45rem;
  border-radius: 4px;
  display: inline-block;
  width: fit-content;
}

.satisfaction-good {
  background: rgba(34, 197, 94, 0.15);
  color: #22c55e;
}

.satisfaction-partial {
  background: rgba(234, 179, 8, 0.15);
  color: #eab308;
}

.satisfaction-poor {
  background: rgba(239, 68, 68, 0.15);
  color: #ef4444;
}

.satisfaction-bar-wrap {
  width: 100%;
  height: 4px;
  background: var(--color-divider);
  border-radius: 2px;
  overflow: hidden;
}

.satisfaction-bar-fill {
  height: 100%;
  border-radius: 2px;
  transition: width 0.3s ease;
}

.satisfaction-bar-fill.satisfaction-good { background: #22c55e; }
.satisfaction-bar-fill.satisfaction-partial { background: #eab308; }
.satisfaction-bar-fill.satisfaction-poor { background: #ef4444; }

.product-sellers {
  color: var(--color-text-secondary);
  font-variant-numeric: tabular-nums;
}

/* Mobile responsive */
@media (max-width: 768px) {
  .products-header,
  .product-row {
    grid-template-columns: 2fr 1fr 1fr 2fr;
  }

  .product-industry,
  .product-demand,
  .product-sellers {
    display: none;
  }
}

/* ── Competitor Intelligence ── */
.competitor-section {
  margin-top: 1rem;
  background: var(--color-surface);
  border: 1px solid var(--color-divider);
  border-radius: 10px;
  overflow: hidden;
}

.competitor-header {
  padding: 1rem 1.25rem 0.75rem;
  border-bottom: 1px solid var(--color-divider);
}

.competitor-title {
  font-size: 1rem;
  font-weight: 700;
  margin: 0 0 0.2rem;
  color: var(--color-text-primary);
}

.competitor-subtitle {
  font-size: 0.8rem;
  color: var(--color-text-secondary);
  margin: 0;
}

.competitor-grid {
  display: flex;
  flex-direction: column;
}

.competitor-grid-header,
.competitor-row {
  display: grid;
  grid-template-columns: 2.5rem 1fr 6rem 7rem;
  align-items: center;
  padding: 0.5rem 1.25rem;
  gap: 0.5rem;
}

.competitor-grid-header {
  font-size: 0.72rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--color-text-muted);
  border-bottom: 1px solid var(--color-divider);
}

.competitor-row {
  border-bottom: 1px solid var(--color-divider);
  font-size: 0.875rem;
}

.competitor-row:last-child {
  border-bottom: none;
}

.competitor-row--own {
  background: rgba(99, 102, 241, 0.05);
}

.competitor-rank {
  font-size: 1.1rem;
  text-align: center;
}

.competitor-name {
  color: var(--color-text-primary);
  font-weight: 500;
  display: flex;
  align-items: center;
  gap: 0.4rem;
}

.you-badge {
  font-size: 0.65rem;
  font-weight: 700;
  padding: 0.1rem 0.35rem;
  border-radius: 4px;
  background: rgba(99, 102, 241, 0.2);
  color: #818cf8;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.quality-badge {
  font-size: 0.8rem;
  font-weight: 700;
  padding: 0.15rem 0.5rem;
  border-radius: 4px;
  display: inline-block;
}

.quality-gold  { background: rgba(234, 179, 8, 0.2);  color: #eab308; }
.quality-green { background: rgba(34, 197, 94, 0.15); color: #22c55e; }
.quality-blue  { background: rgba(59, 130, 246, 0.15); color: #60a5fa; }
.quality-dim   { background: var(--color-surface-2); color: var(--color-text-muted); }

.competitor-premium {
  color: var(--color-text-secondary);
  font-variant-numeric: tabular-nums;
  font-size: 0.8rem;
}
</style>

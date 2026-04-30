<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { useTickRefresh } from '@/composables/useTickRefresh'
import { useScrollPreservation } from '@/composables/useScrollPreservation'
import { deepEqual } from '@/lib/utils'
import { formatCurrency } from '@/lib/loanHelpers'
import { getActiveCompany } from '@/lib/accountContext'
import type { CampaignAnalyticsResult, CampaignAnalyticsRow, Company } from '@/types'

const { t } = useI18n()
const auth = useAuthStore()
const { selectedCityId } = storeToRefs(auth)
const { saveScrollPosition, restoreScrollPosition } = useScrollPreservation()

const companies = ref<Company[]>([])
const selectedCompanyId = ref<string | null>(null)
const analytics = ref<CampaignAnalyticsResult | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)
const cities = ref<Array<{ id: string; name: string }>>([])

// ÔöÇÔöÇ Queries ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ

const MY_COMPANIES_QUERY = `
  {
    myCompanies {
      id
      name
      cash
    }
  }
`

function buildCampaignQuery(): string {
  return `
    query CampaignAnalytics($companyId: UUID!) {
      campaignAnalytics(companyId: $companyId) {
        companyId
        windowTicks
        totalRevenue
        totalMarketingSpend
        bestPerformingCity
        bestPerformingProduct
        globalRecommendation
        rows {
          buildingUnitId
          buildingId
          buildingName
          productName
          productTypeId
          cityName
          brandAwareness
          brandQuality
          marketingQuality
          currentPrice
          basePrice
          priceIndex
          pricePremiumPct
          revenueLastTicks
          quantityLastTicks
          utilizationRate
          trendDirection
          trendFactor
          demandSignal
          topPositiveFactor
          topNegativeFactor
          marketingSpendLastTicks
          brandRevenueBoost
          campaignImpact
          brandVsPriceBalance
          recommendation
          cityCurrencyCode
        }
      }
    }
  `
}

// ÔöÇÔöÇ Data loading ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ

async function loadCompanies() {
  if (!auth.isAuthenticated) return
  try {
    // Load cities for city-name mapping
    const citiesData = await gqlRequest<{ cities: Array<{ id: string; name: string }> }>(`{ cities { id name } }`)
    cities.value = citiesData.cities

    const data = await gqlRequest<{ myCompanies: Company[] }>(MY_COMPANIES_QUERY)
    companies.value = data.myCompanies ?? []
    // Auto-select active company.
    const active = getActiveCompany(auth.player, companies.value)
    if (active && !selectedCompanyId.value) {
      selectedCompanyId.value = active.id
    } else if (companies.value.length > 0 && !selectedCompanyId.value) {
      const first = companies.value[0]
      if (first) selectedCompanyId.value = first.id
    }
  } catch {
    // ignore - company load is best-effort
  }
}

async function loadAnalytics(isRefresh = false) {
  if (!selectedCompanyId.value) return
  if (!isRefresh) loading.value = true
  error.value = null
  try {
    const data = await gqlRequest<{ campaignAnalytics: CampaignAnalyticsResult | null }>(buildCampaignQuery(), { companyId: selectedCompanyId.value })
    const result = data.campaignAnalytics ?? null
    if (!deepEqual(analytics.value, result)) {
      analytics.value = result
    }
  } catch (e) {
    error.value = e instanceof Error ? e.message : t('campaignAnalytics.loadFailed')
    analytics.value = null
  } finally {
    loading.value = false
  }
}

// ÔöÇÔöÇ Lifecycle ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ

onMounted(async () => {
  const pos = saveScrollPosition()
  await loadCompanies()
  await loadAnalytics()
  await restoreScrollPosition(pos)
})

watch(selectedCompanyId, async (newId, oldId) => {
  if (newId && newId !== oldId) {
    analytics.value = null
    await loadAnalytics()
  }
})

useTickRefresh(async () => {
  const pos = saveScrollPosition()
  await loadAnalytics(true)
  await restoreScrollPosition(pos)
})

// ÔöÇÔöÇ Computed helpers ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ

const roiLabel = computed(() => {
  if (!analytics.value) return null
  const spend = analytics.value.totalMarketingSpend
  const rev = analytics.value.totalRevenue
  if (spend <= 0) return null
  return (rev / spend).toFixed(1) + 'x'
})

function balanceClass(bvp: string): string {
  switch (bvp) {
    case 'PREMIUM_JUSTIFIED':
      return 'ca-balance-premium-ok'
    case 'PREMIUM_RISKY':
      return 'ca-balance-premium-risk'
    case 'DISCOUNT_WITH_BRAND':
      return 'ca-balance-discount-brand'
    case 'COMPETITIVE_BASELINE':
      return 'ca-balance-baseline'
    case 'BRAND_BUILDING':
      return 'ca-balance-building'
    default:
      return 'ca-balance-none'
  }
}

function impactClass(impact: string): string {
  switch (impact) {
    case 'STRONG':
      return 'ca-impact-strong'
    case 'MODERATE':
      return 'ca-impact-moderate'
    case 'WEAK':
      return 'ca-impact-weak'
    default:
      return 'ca-impact-none'
  }
}

function demandClass(signal: string): string {
  switch (signal) {
    case 'SUPPLY_CONSTRAINED':
    case 'STRONG':
      return 'ca-demand-strong'
    case 'MODERATE':
      return 'ca-demand-moderate'
    case 'WEAK':
      return 'ca-demand-weak'
    default:
      return ''
  }
}

function trendClass(dir: string): string {
  switch (dir) {
    case 'UP':
      return 'ca-trend-up'
    case 'DOWN':
      return 'ca-trend-down'
    default:
      return 'ca-trend-flat'
  }
}
const filteredAnalyticsRows = computed<CampaignAnalyticsRow[]>(() => {
  if (!analytics.value || !analytics.value.rows) {
    return []
  }
  if (!selectedCityId.value) {
    return analytics.value.rows
  }

  // Map city ID to city name
  const selectedCity = cities.value.find((c) => c.id === selectedCityId.value)
  const selectedCityName = selectedCity?.name

  if (!selectedCityName) {
    return analytics.value.rows
  }

  return analytics.value.rows.filter((row) => typeof row.cityName === 'string' && row.cityName.toLowerCase() === selectedCityName.toLowerCase())
})
function formatPct(val: number | null): string {
  if (val === null || val === undefined) return '-'
  return Math.round(val * 100) + '%'
}

function formatPricePremium(val: number | null): string {
  if (val === null || val === undefined) return '-'
  const sign = val >= 0 ? '+' : ''
  return sign + val.toFixed(1) + '%'
}

function rowTitle(row: CampaignAnalyticsRow): string {
  if (row.productName) return t('campaignAnalytics.rowTitle', { product: row.productName, city: row.cityName })
  return t('campaignAnalytics.rowTitleNoProduct', { city: row.cityName })
}

function formatFactor(factor: string | null): string {
  if (!factor) return '-'
  const key = `campaignAnalytics.factor_${factor}`
  const val = t(key)
  return val === key ? factor : val
}
</script>

<template>
<div class="ca-view container">
    <!-- Page header -->
    <div class="ca-header">
      <div>
        <h1 class="ca-title">{{ t('campaignAnalytics.title') }}</h1>
        <p class="ca-subtitle">{{ t('campaignAnalytics.subtitle') }}</p>
      </div>

      <!-- Company selector -->
      <div v-if="auth.isAuthenticated && companies.length > 1" class="ca-company-selector">
        <label class="ca-company-label" for="ca-company-select">{{ t('campaignAnalytics.selectCompany') }}</label>
        <select id="ca-company-select" v-model="selectedCompanyId" class="ca-company-select">
          <option v-for="c in companies" :key="c.id" :value="c.id">{{ c.name }}</option>
        </select>
      </div>
    </div>

    <!-- Not authenticated -->
    <div v-if="!auth.isAuthenticated" class="ca-empty-state">
      <p>{{ t('auth.loginRequired') }}</p>
    </div>

    <!-- Loading -->
    <div v-else-if="loading && !analytics" class="ca-loading">
      {{ t('campaignAnalytics.loading') }}
    </div>

    <!-- Error -->
    <div v-else-if="error" class="ca-error" role="alert">{{ error }}</div>

    <!-- No company selected -->
    <div v-else-if="!selectedCompanyId" class="ca-empty-state">
      <p>{{ t('campaignAnalytics.selectCompany') }}</p>
    </div>

    <!-- Data -->
    <template v-else-if="analytics">
      <!-- Summary KPI row -->
      <div class="ca-kpi-row" aria-label="Campaign summary">
        <div class="ca-kpi-card">
          <span class="ca-kpi-label">{{ t('campaignAnalytics.totalRevenue') }}</span>
          <strong class="ca-kpi-value">
            {{ formatCurrency(analytics.totalRevenue, analytics.rows[0]?.cityCurrencyCode ?? 'EUR') }}
          </strong>
          <span class="ca-kpi-hint">{{ t('campaignAnalytics.windowTicks', { n: analytics.windowTicks }) }}</span>
        </div>
        <div class="ca-kpi-card">
          <span class="ca-kpi-label">{{ t('campaignAnalytics.totalMarketingSpend') }}</span>
          <strong class="ca-kpi-value">
            {{ formatCurrency(analytics.totalMarketingSpend, analytics.rows[0]?.cityCurrencyCode ?? 'EUR') }}
          </strong>
          <span class="ca-kpi-hint">{{ t('campaignAnalytics.windowTicks', { n: analytics.windowTicks }) }}</span>
        </div>
        <div v-if="roiLabel" class="ca-kpi-card">
          <span class="ca-kpi-label">{{ t('campaignAnalytics.marketingRoi') }}</span>
          <strong class="ca-kpi-value ca-kpi-roi">{{ roiLabel }}</strong>
        </div>
        <div v-if="analytics.bestPerformingCity" class="ca-kpi-card">
          <span class="ca-kpi-label">{{ t('campaignAnalytics.bestCity') }}</span>
          <strong class="ca-kpi-value">{{ analytics.bestPerformingCity }}</strong>
        </div>
        <div v-if="analytics.bestPerformingProduct" class="ca-kpi-card">
          <span class="ca-kpi-label">{{ t('campaignAnalytics.bestProduct') }}</span>
          <strong class="ca-kpi-value">{{ analytics.bestPerformingProduct }}</strong>
        </div>
      </div>

      <!-- Global recommendation -->
      <div v-if="analytics.globalRecommendation" class="ca-global-rec" aria-label="Portfolio insight">
        <span class="ca-global-rec-icon" aria-hidden="true">💡</span>
        <div>
          <strong class="ca-global-rec-title">{{ t('campaignAnalytics.globalRecommendation') }}</strong>
          <p class="ca-global-rec-body">{{ analytics.globalRecommendation }}</p>
        </div>
      </div>

      <!-- No units -->
      <div v-if="analytics.rows.length === 0" class="ca-empty-state">
        <p>{{ t('campaignAnalytics.noUnits') }}</p>
      </div>

      <!-- Per-unit analytics cards -->
      <div v-else class="ca-rows">
        <article v-for="row in filteredAnalyticsRows" :key="row.buildingUnitId" class="ca-row-card" :aria-label="rowTitle(row)">
          <!-- Card header -->
          <div class="ca-row-header">
            <div class="ca-row-identity">
              <h3 class="ca-row-title">{{ rowTitle(row) }}</h3>
              <span class="ca-row-building">{{ row.buildingName }}</span>
            </div>
            <div class="ca-row-badges">
              <span class="ca-badge ca-balance-badge" :class="balanceClass(row.brandVsPriceBalance)" :title="t(`campaignAnalytics.balance_${row.brandVsPriceBalance}`)">{{
                t(`campaignAnalytics.balance_${row.brandVsPriceBalance}`)
              }}</span>
              <span class="ca-badge ca-impact-badge" :class="impactClass(row.campaignImpact)" :title="t(`campaignAnalytics.campaignImpact_${row.campaignImpact}`)">{{
                t(`campaignAnalytics.campaignImpact_${row.campaignImpact}`)
              }}</span>
            </div>
          </div>

          <!-- Metrics grid -->
          <div class="ca-metrics-grid">
            <!-- Revenue -->
            <div class="ca-metric">
              <span class="ca-metric-label">{{ t('campaignAnalytics.revenueWindow', { n: analytics.windowTicks }) }}</span>
              <strong class="ca-metric-value">{{ formatCurrency(row.revenueLastTicks, row.cityCurrencyCode) }}</strong>
            </div>
            <!-- Utilization -->
            <div class="ca-metric">
              <span class="ca-metric-label">{{ t('campaignAnalytics.utilization') }}</span>
              <strong class="ca-metric-value">{{ formatPct(row.utilizationRate) }}</strong>
            </div>
            <!-- Demand signal -->
            <div class="ca-metric">
              <span class="ca-metric-label">{{ t('campaignAnalytics.demandSignal') }}</span>
              <strong class="ca-metric-value" :class="demandClass(row.demandSignal)">
                {{ t(`campaignAnalytics.demandSignal_${row.demandSignal}`) }}
              </strong>
            </div>
            <!-- Trend -->
            <div class="ca-metric">
              <span class="ca-metric-label">{{ t('campaignAnalytics.trend') }}</span>
              <strong class="ca-metric-value" :class="trendClass(row.trendDirection)">
                {{ t(`campaignAnalytics.trend_${row.trendDirection}`) }}
              </strong>
            </div>

            <!-- Brand awareness -->
            <div v-if="row.brandAwareness !== null" class="ca-metric">
              <span class="ca-metric-label">{{ t('campaignAnalytics.brandAwareness') }}</span>
              <strong class="ca-metric-value" :class="{ 'ca-quality-high': (row.brandAwareness ?? 0) >= 0.6 }">
                {{ formatPct(row.brandAwareness) }}
              </strong>
            </div>
            <!-- Brand quality -->
            <div v-if="row.brandQuality !== null" class="ca-metric">
              <span class="ca-metric-label">{{ t('campaignAnalytics.brandQuality') }}</span>
              <strong
                class="ca-metric-value"
                :class="{
                  'ca-quality-high': (row.brandQuality ?? 0) >= 0.5,
                  'ca-quality-low': (row.brandQuality ?? 0) < 0.2,
                }"
                >{{ formatPct(row.brandQuality) }}</strong
              >
            </div>
            <!-- Marketing prestige -->
            <div v-if="row.marketingQuality !== null" class="ca-metric">
              <span class="ca-metric-label">{{ t('campaignAnalytics.marketingQuality') }}</span>
              <strong class="ca-metric-value">{{ formatPct(row.marketingQuality) }}</strong>
            </div>

            <!-- Price vs base -->
            <div v-if="row.currentPrice !== null" class="ca-metric">
              <span class="ca-metric-label">{{ t('campaignAnalytics.currentPrice') }}</span>
              <strong class="ca-metric-value">
                {{ formatCurrency(row.currentPrice ?? 0, row.cityCurrencyCode) }}
                <span
                  v-if="row.pricePremiumPct !== null"
                  class="ca-price-delta"
                  :class="{
                    'ca-price-premium': (row.pricePremiumPct ?? 0) > 5,
                    'ca-price-discount': (row.pricePremiumPct ?? 0) < -5,
                  }"
                  >{{ formatPricePremium(row.pricePremiumPct) }}</span
                >
              </strong>
            </div>
            <div v-if="row.basePrice !== null" class="ca-metric">
              <span class="ca-metric-label">{{ t('campaignAnalytics.basePrice') }}</span>
              <strong class="ca-metric-value">{{ formatCurrency(row.basePrice ?? 0, row.cityCurrencyCode) }}</strong>
            </div>

            <!-- Marketing spend -->
            <div v-if="row.marketingSpendLastTicks !== null" class="ca-metric">
              <span class="ca-metric-label">{{ t('campaignAnalytics.marketingSpend') }}</span>
              <strong class="ca-metric-value">{{ formatCurrency(row.marketingSpendLastTicks ?? 0, row.cityCurrencyCode) }}</strong>
            </div>

            <!-- Brand revenue boost -->
            <div v-if="row.brandRevenueBoost !== null && (row.brandRevenueBoost ?? 0) > 0" class="ca-metric">
              <span class="ca-metric-label">{{ t('campaignAnalytics.brandBoost') }}</span>
              <strong class="ca-metric-value ca-quality-high"> +{{ ((row.brandRevenueBoost ?? 0) * 100).toFixed(0) }}% </strong>
            </div>
          </div>

          <!-- Demand driver comparison -->
          <div v-if="row.topPositiveFactor || row.topNegativeFactor" class="ca-factors">
            <div v-if="row.topPositiveFactor" class="ca-factor ca-factor-positive">
              <span class="ca-factor-icon" aria-hidden="true">📈</span>
              <span class="ca-factor-label">{{ t('campaignAnalytics.topPositive') }}:</span>
              <strong>{{ formatFactor(row.topPositiveFactor) }}</strong>
            </div>
            <div v-if="row.topNegativeFactor" class="ca-factor ca-factor-negative">
              <span class="ca-factor-icon" aria-hidden="true">📉</span>
              <span class="ca-factor-label">{{ t('campaignAnalytics.topNegative') }}:</span>
              <strong>{{ formatFactor(row.topNegativeFactor) }}</strong>
            </div>
          </div>

          <!-- Recommendation -->
          <div v-if="row.recommendation" class="ca-recommendation">
            <span class="ca-rec-icon" aria-hidden="true">✅</span>
            <p class="ca-rec-text">{{ row.recommendation }}</p>
          </div>
        </article>
      </div>
    </template>

    <!-- No analytics returned (null) -->
    <div v-else-if="!loading" class="ca-empty-state">
      <p>{{ t('campaignAnalytics.noData') }}</p>
    </div>
  </div>
</template>

<style scoped src="./MarketingAnalyticsView.styles.css"></style>


<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { useTickRefresh } from '@/composables/useTickRefresh'
import { useScrollPreservation } from '@/composables/useScrollPreservation'
import { deepEqual } from '@/lib/utils'
import { getActiveCompany } from '@/lib/accountContext'
import MarketingAnalyticsContent from '@/components/marketing/MarketingAnalyticsContent.vue'
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

// ── Computed helpers ────────────────────────────────────────────────────────────

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
    <MarketingAnalyticsContent v-else-if="analytics" :analytics="analytics" :filtered-rows="filteredAnalyticsRows" />

    <!-- No analytics returned (null) -->
    <div v-else-if="!loading" class="ca-empty-state">
      <p>{{ t('campaignAnalytics.noData') }}</p>
    </div>
  </div>
</template>

<style scoped>
.ca-view {
  margin: 0 auto;
}

.ca-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
  margin-bottom: 2rem;
  flex-wrap: wrap;
}

.ca-title {
  font-size: 1.75rem;
  font-weight: 700;
  color: var(--color-heading);
  margin: 0 0 0.25rem;
}

.ca-subtitle {
  font-size: 0.9rem;
  color: var(--color-text-muted);
  margin: 0;
}

.ca-company-label {
  display: block;
  font-size: 0.8rem;
  color: var(--color-text-muted);
  margin-bottom: 0.3rem;
}

.ca-company-select {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  color: var(--color-text);
  padding: 0.4rem 0.75rem;
  font-size: 0.9rem;
}

/* Loading / error / empty */
.ca-loading,
.ca-error,
.ca-empty-state {
  text-align: center;
  padding: 3rem 1rem;
  color: var(--color-text-muted);
}

.ca-error {
  color: #f87171;
}
</style>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { formatMoney } from '@/lib/currencyFormat'
import type { CityDemandSummaryResult } from '@/types/analytics'

const props = defineProps<{
  cityId: string
  currencyCode: string
  topN?: number
  lastNTicks?: number
}>()

const { t } = useI18n()

const loading = ref(true)
const summary = ref<CityDemandSummaryResult | null>(null)

const CITY_DEMAND_QUERY = `
  query CityDemandSummary($cityId: UUID!, $topN: Int!, $lastNTicks: Int!) {
    cityDemandSummary(cityId: $cityId, topN: $topN, lastNTicks: $lastNTicks) {
      cityId
      cityName
      currencyCode
      products {
        productTypeId
        productName
        industry
        totalDemand
        totalQuantitySold
        satisfactionRate
        averageClearingPrice
        sellerCount
      }
    }
  }
`

async function loadData() {
  loading.value = true
  try {
    const result = await gqlRequest<{ cityDemandSummary: CityDemandSummaryResult | null }>(
      CITY_DEMAND_QUERY,
      {
        cityId: props.cityId,
        topN: props.topN ?? 5,
        lastNTicks: props.lastNTicks ?? 100,
      },
    )
    summary.value = result.cityDemandSummary
  } catch {
    summary.value = null
  } finally {
    loading.value = false
  }
}

function satisfactionClass(rate: number): string {
  if (rate >= 0.8) return 'sat-good'
  if (rate >= 0.4) return 'sat-partial'
  return 'sat-poor'
}

onMounted(() => loadData())
</script>

<template>
  <section class="city-demand-panel">
    <h3 class="demand-panel-title">{{ t('marketDashboard.demandPanel.title') }}</h3>

    <div v-if="loading" class="demand-loading">
      <span class="demand-spinner" />
    </div>

    <ul
      v-else-if="summary && summary.products.length > 0"
      class="demand-product-list"
      role="list"
    >
      <li
        v-for="product in summary.products"
        :key="product.productTypeId"
        class="demand-product-item"
        role="listitem"
      >
        <div class="dp-name-row">
          <span class="dp-name">{{ product.productName }}</span>
          <span class="dp-price">{{ formatMoney(product.averageClearingPrice, summary.currencyCode) }}</span>
        </div>
        <div class="dp-bar-row">
          <div
            class="dp-satisfaction-bar"
            role="progressbar"
            :aria-valuenow="Math.round(product.satisfactionRate * 100)"
            aria-valuemin="0"
            aria-valuemax="100"
            :aria-label="`${product.productName}: ${Math.round(product.satisfactionRate * 100)}% satisfied`"
          >
            <div
              class="dp-satisfaction-fill"
              :class="satisfactionClass(product.satisfactionRate)"
              :style="{ width: `${Math.round(product.satisfactionRate * 100)}%` }"
            />
          </div>
          <span class="dp-pct" :class="satisfactionClass(product.satisfactionRate)">
            {{ Math.round(product.satisfactionRate * 100) }}%
          </span>
        </div>
        <div class="dp-meta">
          <span class="dp-sellers">{{ t('marketDashboard.sellers') }}: {{ product.sellerCount }}</span>
          <span class="dp-sold">{{ t('marketDashboard.sold') }}: {{ Math.round(product.totalQuantitySold).toLocaleString() }}</span>
        </div>
      </li>
    </ul>

    <p v-else class="demand-empty">{{ t('marketDashboard.noData') }}</p>

    <a href="/market" class="demand-more-link">{{ t('marketDashboard.allProducts') }} →</a>
  </section>
</template>

<style scoped>
.city-demand-panel {
  background: var(--color-card-bg, var(--color-surface));
  border: 1px solid var(--color-divider);
  border-radius: 8px;
  padding: 1rem 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.demand-panel-title {
  font-size: 0.9rem;
  font-weight: 700;
  color: var(--color-text-primary);
  margin: 0;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.demand-loading {
  display: flex;
  justify-content: center;
  padding: 1rem 0;
}

.demand-spinner {
  width: 1.25rem;
  height: 1.25rem;
  border: 2px solid var(--color-divider);
  border-top-color: var(--color-accent);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  display: block;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.demand-product-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.demand-product-item {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.dp-name-row {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
}

.dp-name {
  font-size: 0.85rem;
  font-weight: 600;
  color: var(--color-text-primary);
}

.dp-price {
  font-size: 0.8rem;
  color: var(--color-text-secondary);
  font-variant-numeric: tabular-nums;
}

.dp-bar-row {
  display: flex;
  align-items: center;
  gap: 0.4rem;
}

.dp-satisfaction-bar {
  flex: 1;
  height: 6px;
  background: var(--color-divider);
  border-radius: 3px;
  overflow: hidden;
}

.dp-satisfaction-fill {
  height: 100%;
  border-radius: 3px;
  transition: width 0.3s ease;
}

.dp-satisfaction-fill.sat-good { background: #22c55e; }
.dp-satisfaction-fill.sat-partial { background: #eab308; }
.dp-satisfaction-fill.sat-poor { background: #ef4444; }

.dp-pct {
  font-size: 0.75rem;
  font-weight: 600;
  min-width: 2.5rem;
  text-align: right;
}

.dp-pct.sat-good { color: #22c55e; }
.dp-pct.sat-partial { color: #eab308; }
.dp-pct.sat-poor { color: #ef4444; }

.dp-meta {
  display: flex;
  gap: 0.75rem;
  font-size: 0.72rem;
  color: var(--color-text-secondary);
}

.demand-empty {
  font-size: 0.85rem;
  color: var(--color-text-secondary);
  margin: 0;
}

.demand-more-link {
  font-size: 0.8rem;
  color: var(--color-accent);
  text-decoration: none;
  align-self: flex-start;
}

.demand-more-link:hover {
  text-decoration: underline;
}
</style>

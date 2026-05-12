<script setup lang="ts">
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { formatMoney } from '@/lib/currencyFormat'
import type { ProductDemandEntry, MarketPriceHistoryPoint } from '@/types/analytics'

const props = defineProps<{
  product: ProductDemandEntry
  cityId: string
  currencyCode: string
}>()

const { t } = useI18n()

const loading = ref(false)
const error = ref<string | null>(null)
const history = ref<MarketPriceHistoryPoint[]>([])

const PRICE_HISTORY_QUERY = `
  query MarketPriceHistory($cityId: UUID!, $productTypeId: UUID!, $lastNTicks: Int!) {
    marketPriceHistory(cityId: $cityId, productTypeId: $productTypeId, lastNTicks: $lastNTicks) {
      tick
      clearingPrice
      totalVolume
      totalRevenue
      sellerCount
    }
  }
`

async function loadHistory() {
  loading.value = true
  error.value = null
  try {
    const result = await gqlRequest<{ marketPriceHistory: MarketPriceHistoryPoint[] }>(
      PRICE_HISTORY_QUERY,
      { cityId: props.cityId, productTypeId: props.product.productTypeId, lastNTicks: 100 },
    )
    history.value = result.marketPriceHistory
  } catch {
    error.value = t('marketDashboard.loadFailed')
  } finally {
    loading.value = false
  }
}

watch(() => [props.product.productTypeId, props.cityId], loadHistory, { immediate: true })
</script>

<template>
  <div class="price-history-panel">
    <h2 class="history-title">{{ t('marketDashboard.priceHistoryTitle') }}</h2>

    <div v-if="loading" class="history-state">
      <span class="spinner" aria-busy="true" />
    </div>
    <p v-else-if="error" class="history-state history-error">{{ error }}</p>
    <p v-else-if="history.length === 0" class="history-state history-empty">
      {{ t('marketDashboard.priceHistoryEmpty') }}
    </p>
    <div v-else class="history-table-wrap">
      <table class="history-table" :aria-label="`${product.productName} ${t('marketDashboard.priceHistoryTitle')}`">
        <thead>
          <tr>
            <th>{{ t('marketDashboard.tick') }}</th>
            <th class="col-right">{{ t('marketDashboard.clearingPrice') }}</th>
            <th class="col-right">{{ t('marketDashboard.sold') }}</th>
            <th class="col-right">{{ t('marketDashboard.sellers') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="point in [...history].reverse()" :key="point.tick" class="history-row">
            <td class="tick-cell">{{ point.tick }}</td>
            <td class="price-cell col-right">{{ formatMoney(point.clearingPrice, currencyCode) }}</td>
            <td class="volume-cell col-right">{{ Math.round(point.totalVolume).toLocaleString() }}</td>
            <td class="sellers-cell col-right">{{ point.sellerCount }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<style scoped>
.price-history-panel {
  background: var(--color-card-bg);
  border: 1px solid var(--color-divider);
  border-radius: 8px;
  padding: 1.25rem;
}

.history-title {
  font-size: 1rem;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0 0 1rem;
}

.history-state {
  padding: 1.5rem;
  text-align: center;
  color: var(--color-text-secondary);
  font-size: 0.9rem;
}

.history-error {
  color: var(--color-error);
}

.history-empty {
  color: var(--color-text-secondary);
}

.spinner {
  display: inline-block;
  width: 1.5rem;
  height: 1.5rem;
  border: 2px solid var(--color-divider);
  border-top-color: var(--color-accent);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.history-table-wrap {
  overflow-x: auto;
}

.history-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.875rem;
}

.history-table thead th {
  padding: 0.4rem 0.75rem;
  background: var(--color-surface-muted);
  color: var(--color-text-secondary);
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  border-bottom: 1px solid var(--color-divider);
}

.history-row td {
  padding: 0.45rem 0.75rem;
  border-bottom: 1px solid var(--color-divider);
  color: var(--color-text-secondary);
  font-variant-numeric: tabular-nums;
}

.history-row:last-child td {
  border-bottom: none;
}

.tick-cell {
  color: var(--color-text-muted) !important;
  font-size: 0.8rem;
}

.price-cell {
  font-weight: 600;
  color: var(--color-text-primary) !important;
}

.col-right {
  text-align: right;
}
</style>

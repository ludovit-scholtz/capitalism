<template>
  <div class="tr-page">
    <div class="tr-header">
      <h1 class="tr-title">{{ t('tradeRoutes.title') }}</h1>
      <p class="tr-subtitle">{{ t('tradeRoutes.emptyHint') }}</p>
    </div>

    <!-- Summary strip -->
    <div v-if="activeRoutes.length > 0" class="tr-summary">
      <div class="tr-summary-item">
        <span class="tr-summary-label">{{ t('tradeRoutes.activeRoutes') }}</span>
        <span class="tr-summary-value">{{ activeRoutes.length }}</span>
      </div>
      <div class="tr-summary-item">
        <span class="tr-summary-label">{{ t('tradeRoutes.nextDelivery') }}</span>
        <span class="tr-summary-value">{{ nextDeliveryTick ?? '—' }}</span>
      </div>
    </div>

    <!-- Filter tabs -->
    <div class="tr-filter-tabs" role="tablist">
      <button
        v-for="tab in filterTabs"
        :key="tab.key"
        role="tab"
        :aria-selected="filterTab === tab.key"
        :class="['tr-tab', { 'tr-tab--active': filterTab === tab.key }]"
        @click="filterTab = tab.key"
      >
        {{ tab.label }}
      </button>
    </div>

    <!-- Loading state -->
    <div v-if="loading" class="tr-loading">
      <span>{{ t('common.loading') }}</span>
    </div>

    <!-- Error state -->
    <div v-else-if="error" class="tr-error">{{ error }}</div>

    <!-- Empty state -->
    <div v-else-if="filteredRoutes.length === 0" class="tr-empty">
      <div class="tr-empty-icon">🚚</div>
      <p class="tr-empty-msg">{{ t('tradeRoutes.empty') }}</p>
      <p class="tr-empty-hint">{{ t('tradeRoutes.emptyHint') }}</p>
    </div>

    <!-- Routes table -->
    <div v-else class="tr-table-wrap">
      <table class="tr-table" aria-label="Trade Routes">
        <thead>
          <tr>
            <th>{{ t('tradeRoutes.sourceCity') }}</th>
            <th>{{ t('tradeRoutes.destCity') }}</th>
            <th>{{ t('tradeRoutes.item') }}</th>
            <th>{{ t('tradeRoutes.quantity') }}</th>
            <th>{{ t('tradeRoutes.pricePerUnit') }}</th>
            <th>{{ t('tradeRoutes.shippingEstimate') }}</th>
            <th>{{ t('tradeRoutes.transitTicks') }}</th>
            <th>{{ t('tradeRoutes.arrivalTick') }}</th>
            <th>{{ t('tradeRoutes.statusLabel') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="route in filteredRoutes"
            :key="route.id"
            :class="['tr-row', `tr-row--${route.status.toLowerCase()}`]"
          >
            <td>{{ route.sourceCityName }}</td>
            <td>{{ route.destinationCityName }}</td>
            <td>{{ route.resourceTypeName ?? route.productTypeName ?? '—' }}</td>
            <td>{{ route.quantity }}</td>
            <td>{{ formatCurrency(route.pricePerUnit, route.sourceCurrencyCode) }}</td>
            <td>{{ formatCurrency(route.shippingCostEstimate, route.sourceCurrencyCode) }}</td>
            <td>{{ route.transitTicks }}</td>
            <td>{{ route.expectedArrivalTick }}</td>
            <td>
              <span :class="['tr-badge', `tr-badge--${route.status.toLowerCase()}`]">
                {{ t(`tradeRoutes.status.${route.status}`) }}
              </span>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import type { TradeRouteResult } from '@/types'

const { t } = useI18n()

const routes = ref<TradeRouteResult[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const filterTab = ref<'all' | 'active' | 'completed'>('all')

const filterTabs = computed(() => [
  { key: 'all' as const, label: t('tradeRoutes.filter.all') },
  { key: 'active' as const, label: t('tradeRoutes.filter.active') },
  { key: 'completed' as const, label: t('tradeRoutes.filter.completed') },
])

const activeRoutes = computed(() =>
  routes.value.filter((r) => r.status === 'IN_TRANSIT' || r.status === 'SCHEDULED'),
)

const nextDeliveryTick = computed(() => {
  const active = activeRoutes.value
  if (active.length === 0) return null
  return Math.min(...active.map((r) => r.expectedArrivalTick))
})

const filteredRoutes = computed(() => {
  switch (filterTab.value) {
    case 'active':
      return activeRoutes.value
    case 'completed':
      return routes.value.filter((r) => r.status === 'DELIVERED' || r.status === 'FAILED')
    default:
      return routes.value
  }
})

function formatCurrency(value: number, currency: string): string {
  return new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(value)
}

async function fetchRoutes() {
  loading.value = true
  error.value = null
  try {
    const query = `
      query MyTradeRoutes {
        myTradeRoutes {
          id companyId
          sourceBuildingId sourceBuildingName sourceCityName sourceCurrencyCode
          destinationBuildingId destinationBuildingName destinationCityName destinationCurrencyCode
          productTypeId productTypeName
          resourceTypeId resourceTypeName
          quantity quality pricePerUnit
          scheduledDepartureTick expectedArrivalTick transitTicks
          shippingCostEstimate shippingCostActual
          status failureReason
          createdAtUtc departedAtUtc completedAtUtc
        }
      }
    `
    const data = await gqlRequest<{ myTradeRoutes: TradeRouteResult[] }>(query)
    routes.value = data.myTradeRoutes ?? []
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load trade routes.'
  } finally {
    loading.value = false
  }
}

onMounted(fetchRoutes)
</script>

<style scoped>
.tr-page {
  max-width: 1200px;
  margin: 0 auto;
  padding: 2rem 1.5rem 4rem;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.tr-header {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.tr-title {
  font-size: 1.75rem;
  font-weight: 700;
  color: var(--color-text-primary);
}

.tr-subtitle {
  font-size: 0.9rem;
  color: var(--color-text-secondary);
}

.tr-summary {
  display: flex;
  gap: 2rem;
  padding: 1rem 1.5rem;
  background: var(--color-surface);
  border: 1px solid var(--color-divider);
  border-radius: 8px;
}

.tr-summary-item {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.tr-summary-label {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.tr-summary-value {
  font-size: 1.25rem;
  font-weight: 600;
  color: var(--color-text-primary);
}

.tr-filter-tabs {
  display: flex;
  gap: 0.5rem;
  border-bottom: 1px solid var(--color-divider);
  padding-bottom: 0;
}

.tr-tab {
  padding: 0.6rem 1.2rem;
  background: transparent;
  border: none;
  border-bottom: 2px solid transparent;
  color: var(--color-text-secondary);
  cursor: pointer;
  font-size: 0.9rem;
  transition: color 0.15s, border-color 0.15s;
}

.tr-tab--active {
  color: var(--color-primary);
  border-bottom-color: var(--color-primary);
}

.tr-loading,
.tr-error,
.tr-empty {
  padding: 3rem;
  text-align: center;
  color: var(--color-text-secondary);
}

.tr-empty-icon {
  font-size: 3rem;
  margin-bottom: 1rem;
}

.tr-empty-msg {
  font-size: 1.1rem;
  font-weight: 500;
  color: var(--color-text-primary);
  margin-bottom: 0.5rem;
}

.tr-empty-hint {
  font-size: 0.9rem;
  color: var(--color-text-secondary);
}

.tr-table-wrap {
  overflow-x: auto;
}

.tr-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.875rem;
}

.tr-table th {
  text-align: left;
  padding: 0.75rem 1rem;
  color: var(--color-text-secondary);
  font-weight: 600;
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  border-bottom: 1px solid var(--color-divider);
  white-space: nowrap;
}

.tr-table td {
  padding: 0.75rem 1rem;
  border-bottom: 1px solid var(--color-divider);
  color: var(--color-text-primary);
}

.tr-row--failed td {
  color: var(--color-text-secondary);
  opacity: 0.7;
}

.tr-badge {
  display: inline-block;
  padding: 0.2rem 0.6rem;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
}

.tr-badge--scheduled {
  background: var(--color-primary-dim, rgba(59, 130, 246, 0.15));
  color: var(--color-primary, #3b82f6);
}

.tr-badge--in_transit {
  background: rgba(234, 179, 8, 0.15);
  color: #ca8a04;
}

.tr-badge--delivered {
  background: rgba(34, 197, 94, 0.15);
  color: #16a34a;
}

.tr-badge--failed {
  background: rgba(239, 68, 68, 0.15);
  color: #dc2626;
}
</style>

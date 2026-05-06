<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'

const { t, locale } = useI18n()

interface MoneyFlowItem {
  category: string
  label: string
  amount: number
  percentage: number
  entryCount: number
}

interface OperationsStatistics {
  currentTick: number
  windowTicks: number
  inflowItems: MoneyFlowItem[]
  outflowItems: MoneyFlowItem[]
  totalInflow: number
  totalOutflow: number
  netFlow: number
  totalPlayerCount: number
  totalCompanyCount: number
  totalBuildingCount: number
}

const stats = ref<OperationsStatistics | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)

function formatCurrency(value: number) {
  return new Intl.NumberFormat(locale.value, {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 0,
  }).format(value)
}

async function loadStatistics() {
  loading.value = true
  error.value = null
  try {
    const data = await gqlRequest<{ operationsStatistics: OperationsStatistics }>(`
      query OperationsStatistics {
        operationsStatistics {
          currentTick
          windowTicks
          totalInflow
          totalOutflow
          netFlow
          totalPlayerCount
          totalCompanyCount
          totalBuildingCount
          inflowItems {
            category
            label
            amount
            percentage
            entryCount
          }
          outflowItems {
            category
            label
            amount
            percentage
            entryCount
          }
        }
      }
    `)
    stats.value = data.operationsStatistics
  } catch (caughtError) {
    error.value = caughtError instanceof Error ? caughtError.message : t('common.unknownError')
  } finally {
    loading.value = false
  }
}

onMounted(loadStatistics)
</script>

<template>
  <div class="ops-statistics">
    <div class="ops-section-header">
      <h2>{{ t('operations.statistics.title') }}</h2>
      <p v-if="stats">{{ t('operations.statistics.subtitle', { window: stats.windowTicks }) }}</p>
    </div>

    <div v-if="loading" class="ops-loading">{{ t('common.loading') }}</div>
    <div v-else-if="error" class="ops-error card">
      <p>{{ error }}</p>
      <button type="button" class="btn btn-secondary" @click="loadStatistics">{{ t('common.retry') }}</button>
    </div>

    <template v-else-if="stats">
      <!-- Server metrics row -->
      <div class="ops-metrics-row">
        <div class="ops-metric-card card">
          <span class="ops-metric-label">{{ t('operations.statistics.totalPlayers') }}</span>
          <span class="ops-metric-value">{{ stats.totalPlayerCount }}</span>
        </div>
        <div class="ops-metric-card card">
          <span class="ops-metric-label">{{ t('operations.statistics.totalCompanies') }}</span>
          <span class="ops-metric-value">{{ stats.totalCompanyCount }}</span>
        </div>
        <div class="ops-metric-card card">
          <span class="ops-metric-label">{{ t('operations.statistics.totalBuildings') }}</span>
          <span class="ops-metric-value">{{ stats.totalBuildingCount }}</span>
        </div>
        <div class="ops-metric-card card">
          <span class="ops-metric-label">{{ t('operations.statistics.totalInflow') }}</span>
          <span class="ops-metric-value ops-metric-positive">{{ formatCurrency(stats.totalInflow) }}</span>
        </div>
        <div class="ops-metric-card card">
          <span class="ops-metric-label">{{ t('operations.statistics.totalOutflow') }}</span>
          <span class="ops-metric-value ops-metric-negative">{{ formatCurrency(stats.totalOutflow) }}</span>
        </div>
        <div class="ops-metric-card card">
          <span class="ops-metric-label">{{ t('operations.statistics.netFlow') }}</span>
          <span class="ops-metric-value" :class="stats.netFlow >= 0 ? 'ops-metric-positive' : 'ops-metric-negative'">
            {{ formatCurrency(stats.netFlow) }}
          </span>
        </div>
      </div>

      <!-- Money flow columns -->
      <div class="ops-flow-grid">
        <!-- Inflow -->
        <div class="card ops-flow-panel">
          <h3 class="ops-flow-title ops-inflow-title">
            <span class="ops-flow-icon" aria-hidden="true">↑</span>
            {{ t('operations.statistics.inflowTitle') }}
            <span class="ops-flow-total">{{ formatCurrency(stats.totalInflow) }}</span>
          </h3>
          <div v-if="stats.inflowItems.length === 0" class="ops-empty">
            {{ t('operations.statistics.noData', { window: stats.windowTicks }) }}
          </div>
          <ul class="ops-flow-list">
            <li v-for="item in stats.inflowItems" :key="item.category" class="ops-flow-item">
              <div class="ops-flow-item-header">
                <span class="ops-flow-item-label">{{ item.label }}</span>
                <span class="ops-flow-item-amount ops-inflow-amount">{{ formatCurrency(item.amount) }}</span>
              </div>
              <div class="ops-flow-bar-row">
                <div class="ops-flow-bar ops-inflow-bar" :style="{ width: item.percentage + '%' }"></div>
                <span class="ops-flow-pct">{{ item.percentage }}%</span>
              </div>
            </li>
          </ul>
        </div>

        <!-- Outflow -->
        <div class="card ops-flow-panel">
          <h3 class="ops-flow-title ops-outflow-title">
            <span class="ops-flow-icon" aria-hidden="true">↓</span>
            {{ t('operations.statistics.outflowTitle') }}
            <span class="ops-flow-total">{{ formatCurrency(stats.totalOutflow) }}</span>
          </h3>
          <div v-if="stats.outflowItems.length === 0" class="ops-empty">
            {{ t('operations.statistics.noData', { window: stats.windowTicks }) }}
          </div>
          <ul class="ops-flow-list">
            <li v-for="item in stats.outflowItems" :key="item.category" class="ops-flow-item">
              <div class="ops-flow-item-header">
                <span class="ops-flow-item-label">{{ item.label }}</span>
                <span class="ops-flow-item-amount ops-outflow-amount">{{ formatCurrency(item.amount) }}</span>
              </div>
              <div class="ops-flow-bar-row">
                <div class="ops-flow-bar ops-outflow-bar" :style="{ width: item.percentage + '%' }"></div>
                <span class="ops-flow-pct">{{ item.percentage }}%</span>
              </div>
            </li>
          </ul>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.ops-statistics {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.ops-section-header h2 {
  margin-bottom: 0.25rem;
}

.ops-section-header p {
  color: var(--color-text-secondary);
  font-size: 0.9rem;
}

.ops-loading {
  text-align: center;
  padding: 3rem;
  color: var(--color-text-secondary);
}

.ops-error {
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.75rem;
}

.ops-metrics-row {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
  gap: 0.75rem;
}

.ops-metric-card {
  padding: 1rem 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

.ops-metric-label {
  font-size: 0.78rem;
  color: var(--color-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.ops-metric-value {
  font-size: 1.4rem;
  font-weight: 700;
  letter-spacing: -0.02em;
}

.ops-metric-positive {
  color: #4ade80;
}

.ops-metric-negative {
  color: #f87171;
}

.ops-flow-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.25rem;
}

.ops-flow-panel {
  padding: 1.25rem;
}

.ops-flow-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 1rem;
  font-weight: 600;
  margin-bottom: 1rem;
}

.ops-flow-icon {
  font-size: 1.1rem;
}

.ops-inflow-title {
  color: #4ade80;
}

.ops-outflow-title {
  color: #f87171;
}

.ops-flow-total {
  margin-left: auto;
  font-size: 0.95rem;
  font-weight: 700;
}

.ops-flow-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
}

.ops-flow-item-header {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 0.5rem;
  margin-bottom: 0.3rem;
}

.ops-flow-item-label {
  font-size: 0.88rem;
  color: var(--color-text);
}

.ops-flow-item-amount {
  font-size: 0.9rem;
  font-weight: 600;
  white-space: nowrap;
}

.ops-inflow-amount {
  color: #4ade80;
}

.ops-outflow-amount {
  color: #f87171;
}

.ops-flow-bar-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.ops-flow-bar {
  height: 6px;
  border-radius: 3px;
  min-width: 2px;
  max-width: 100%;
  transition: width 0.3s;
}

.ops-inflow-bar {
  background: rgba(74, 222, 128, 0.6);
}

.ops-outflow-bar {
  background: rgba(248, 113, 113, 0.6);
}

.ops-flow-pct {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
  white-space: nowrap;
}

.ops-empty {
  color: var(--color-text-secondary);
  font-size: 0.9rem;
  padding: 1rem 0;
}

@media (max-width: 720px) {
  .ops-flow-grid {
    grid-template-columns: 1fr;
  }
}
</style>

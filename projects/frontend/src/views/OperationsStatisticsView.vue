<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'

const { t, locale } = useI18n()

type OperationsRange = 'LAST_24_HOURS' | 'LAST_7_DAYS' | 'LAST_30_DAYS' | 'ALL_TIME'

interface MoneyFlowItem {
  category: string
  label: string
  amount: number
  percentage: number
  entryCount: number
}

interface OperationsStatistics {
  currentTick: number
  range: OperationsRange
  inflowItems: MoneyFlowItem[]
  outflowItems: MoneyFlowItem[]
  totalInflow: number
  totalOutflow: number
  netFlow: number
  totalPlayerCount: number
  totalCompanyCount: number
  totalBuildingCount: number
}

const range = ref<OperationsRange>('LAST_7_DAYS')
const stats = ref<OperationsStatistics | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)

const rangeOptions: Array<{ value: OperationsRange; labelKey: string }> = [
  { value: 'LAST_24_HOURS', labelKey: 'operations.moneyFlow.ranges.last24Hours' },
  { value: 'LAST_7_DAYS', labelKey: 'operations.moneyFlow.ranges.last7Days' },
  { value: 'LAST_30_DAYS', labelKey: 'operations.moneyFlow.ranges.last30Days' },
  { value: 'ALL_TIME', labelKey: 'operations.moneyFlow.ranges.allTime' },
]

const selectedRangeLabel = computed(() => {
  const selected = rangeOptions.find((option) => option.value === range.value)
  return selected ? t(selected.labelKey) : t('operations.moneyFlow.ranges.last7Days')
})

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
    const data = await gqlRequest<{ operationsStatistics: OperationsStatistics }>(
      `
        query OperationsStatistics($input: OperationsStatisticsInput) {
          operationsStatistics(input: $input) {
            currentTick
            range
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
      `,
      { input: { range: range.value } },
    )

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
      <div>
        <h2>{{ t('operations.moneyFlow.title') }}</h2>
        <p>{{ t('operations.moneyFlow.subtitle', { range: selectedRangeLabel }) }}</p>
      </div>
      <label class="ops-range-filter">
        <span>{{ t('operations.moneyFlow.rangeLabel') }}</span>
        <select v-model="range" class="form-select" @change="loadStatistics">
          <option v-for="option in rangeOptions" :key="option.value" :value="option.value">
            {{ t(option.labelKey) }}
          </option>
        </select>
      </label>
    </div>

    <div v-if="loading" class="ops-loading">{{ t('common.loading') }}</div>
    <div v-else-if="error" class="ops-error card">
      <p>{{ error }}</p>
      <button type="button" class="btn btn-secondary" @click="loadStatistics">{{ t('common.retry') }}</button>
    </div>
    <template v-else-if="stats">
      <div class="ops-metrics-row">
        <div class="ops-metric-card card">
          <span class="ops-metric-label">{{ t('operations.moneyFlow.totalPlayers') }}</span>
          <span class="ops-metric-value">{{ stats.totalPlayerCount }}</span>
        </div>
        <div class="ops-metric-card card">
          <span class="ops-metric-label">{{ t('operations.moneyFlow.totalCompanies') }}</span>
          <span class="ops-metric-value">{{ stats.totalCompanyCount }}</span>
        </div>
        <div class="ops-metric-card card">
          <span class="ops-metric-label">{{ t('operations.moneyFlow.totalBuildings') }}</span>
          <span class="ops-metric-value">{{ stats.totalBuildingCount }}</span>
        </div>
        <div class="ops-metric-card card">
          <span class="ops-metric-label">{{ t('operations.moneyFlow.netFlow') }}</span>
          <span class="ops-metric-value" :class="stats.netFlow >= 0 ? 'ops-positive' : 'ops-negative'">
            {{ formatCurrency(stats.netFlow) }}
          </span>
        </div>
      </div>

      <div class="ops-flow-grid">
        <article class="card ops-flow-panel">
          <div class="ops-flow-title">
            <div>
              <h3>{{ t('operations.moneyFlow.inflowTitle') }}</h3>
              <p>{{ t('operations.moneyFlow.inflowBody') }}</p>
            </div>
            <strong class="ops-positive">{{ formatCurrency(stats.totalInflow) }}</strong>
          </div>
          <div v-if="stats.inflowItems.length === 0" class="ops-empty">
            {{ t('operations.moneyFlow.noData') }}
          </div>
          <ul v-else class="ops-flow-list">
            <li v-for="item in stats.inflowItems" :key="item.category" class="ops-flow-item">
              <div class="ops-flow-item-header">
                <div>
                  <span class="ops-flow-item-label">{{ item.label }}</span>
                  <p>{{ t('operations.moneyFlow.entryCount', { count: item.entryCount }) }}</p>
                </div>
                <span class="ops-positive">{{ formatCurrency(item.amount) }}</span>
              </div>
              <div class="ops-flow-bar-row">
                <div class="ops-flow-bar ops-flow-bar-in" :style="{ width: `${item.percentage}%` }"></div>
                <span>{{ item.percentage.toFixed(1) }}%</span>
              </div>
            </li>
          </ul>
          <footer class="ops-flow-footer">
            <span>{{ t('operations.moneyFlow.totalInflow') }}</span>
            <strong class="ops-positive">{{ formatCurrency(stats.totalInflow) }}</strong>
          </footer>
        </article>

        <article class="card ops-flow-panel">
          <div class="ops-flow-title">
            <div>
              <h3>{{ t('operations.moneyFlow.outflowTitle') }}</h3>
              <p>{{ t('operations.moneyFlow.outflowBody') }}</p>
            </div>
            <strong class="ops-negative">{{ formatCurrency(stats.totalOutflow) }}</strong>
          </div>
          <div v-if="stats.outflowItems.length === 0" class="ops-empty">
            {{ t('operations.moneyFlow.noData') }}
          </div>
          <ul v-else class="ops-flow-list">
            <li v-for="item in stats.outflowItems" :key="item.category" class="ops-flow-item">
              <div class="ops-flow-item-header">
                <div>
                  <span class="ops-flow-item-label">{{ item.label }}</span>
                  <p>{{ t('operations.moneyFlow.entryCount', { count: item.entryCount }) }}</p>
                </div>
                <span class="ops-negative">{{ formatCurrency(item.amount) }}</span>
              </div>
              <div class="ops-flow-bar-row">
                <div class="ops-flow-bar ops-flow-bar-out" :style="{ width: `${item.percentage}%` }"></div>
                <span>{{ item.percentage.toFixed(1) }}%</span>
              </div>
            </li>
          </ul>
          <footer class="ops-flow-footer">
            <span>{{ t('operations.moneyFlow.totalOutflow') }}</span>
            <strong class="ops-negative">{{ formatCurrency(stats.totalOutflow) }}</strong>
          </footer>
        </article>
      </div>
    </template>
  </div>
</template>

<style scoped>
.ops-statistics {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.ops-section-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
}

.ops-section-header p,
.ops-flow-title p,
.ops-flow-item p {
  color: var(--color-text-secondary);
  margin-top: 0.25rem;
}

.ops-range-filter {
  display: grid;
  gap: 0.35rem;
  min-width: 220px;
}

.ops-loading {
  text-align: center;
  padding: 3rem;
  color: var(--color-text-secondary);
}

.ops-error {
  padding: 1.5rem;
  display: grid;
  gap: 0.75rem;
}

.ops-metrics-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  gap: 0.75rem;
}

.ops-metric-card {
  padding: 1rem 1.1rem;
  display: grid;
  gap: 0.35rem;
}

.ops-metric-label {
  font-size: 0.8rem;
  color: var(--color-text-secondary);
  text-transform: uppercase;
}

.ops-metric-value {
  font-size: 1.35rem;
  font-weight: 700;
}

.ops-positive {
  color: #4ade80;
}

.ops-negative {
  color: #f87171;
}

.ops-flow-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 1rem;
}

.ops-flow-panel {
  padding: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.ops-flow-title,
.ops-flow-item-header,
.ops-flow-footer {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
}

.ops-flow-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  gap: 0.85rem;
}

.ops-flow-item {
  display: grid;
  gap: 0.45rem;
}

.ops-flow-item-label {
  font-weight: 600;
}

.ops-flow-bar-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 0.75rem;
  align-items: center;
}

.ops-flow-bar {
  height: 0.55rem;
  border-radius: 999px;
}

.ops-flow-bar-in {
  background: linear-gradient(90deg, rgba(74, 222, 128, 0.35), rgba(74, 222, 128, 0.9));
}

.ops-flow-bar-out {
  background: linear-gradient(90deg, rgba(248, 113, 113, 0.35), rgba(248, 113, 113, 0.9));
}

.ops-flow-footer {
  padding-top: 0.9rem;
  border-top: 1px solid var(--color-border);
}

.ops-empty {
  padding: 1rem;
  border: 1px dashed var(--color-border);
  border-radius: var(--radius-md);
  color: var(--color-text-secondary);
}

@media (max-width: 900px) {
  .ops-section-header,
  .ops-flow-grid {
    grid-template-columns: 1fr;
    display: grid;
  }

  .ops-range-filter {
    min-width: 0;
  }
}
</style>

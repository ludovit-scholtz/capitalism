<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import {
  buildOperationsAnalyticsCsv,
  filterAndSortOperationsAnalyticsRows,
  type OperationsAnalyticsRow,
  type OperationsAnalyticsSortKey,
} from '@/lib/operationsDashboard'

const { t, locale } = useI18n()

interface AnalyticsResult {
  windowTicks: number
  currentTick: number
  rows: OperationsAnalyticsRow[]
}

const result = ref<AnalyticsResult | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)
const searchQuery = ref('')
const industryFilter = ref('ALL')
const sortKey = ref<OperationsAnalyticsSortKey>('totalRevenue')
const sortAsc = ref(false)

const industries = computed(() => {
  const values = new Set<string>()
  result.value?.rows.forEach((row) => values.add(row.industry))
  return [...values].sort()
})

const filtered = computed(() =>
  filterAndSortOperationsAnalyticsRows(result.value?.rows ?? [], {
    searchQuery: searchQuery.value,
    industryFilter: industryFilter.value,
    sortKey: sortKey.value,
    sortAsc: sortAsc.value,
  }),
)

function setSort(key: OperationsAnalyticsSortKey) {
  if (sortKey.value === key) {
    sortAsc.value = !sortAsc.value
    return
  }

  sortKey.value = key
  sortAsc.value = false
}

function sortIndicator(key: OperationsAnalyticsSortKey) {
  if (sortKey.value !== key) return ''
  return sortAsc.value ? ' ↑' : ' ↓'
}

function formatNumber(value: number) {
  return new Intl.NumberFormat(locale.value, { maximumFractionDigits: 0 }).format(value)
}

function formatCurrency(value: number) {
  return new Intl.NumberFormat(locale.value, {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 0,
  }).format(value)
}

function formatPercent(value: number) {
  return `${value.toFixed(1)}%`
}

function exportCsv() {
  if (!filtered.value.length) return

  const csv = buildOperationsAnalyticsCsv(filtered.value)
  const blob = new Blob([csv], { type: 'text/csv' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `operations-product-analytics-${result.value?.currentTick ?? 0}.csv`
  link.click()
  URL.revokeObjectURL(url)
}

async function loadAnalytics() {
  loading.value = true
  error.value = null

  try {
    const data = await gqlRequest<{ adminProductAnalytics: AnalyticsResult }>(`
      query AdminProductAnalytics {
        adminProductAnalytics {
          windowTicks
          currentTick
          rows {
            productTypeId
            productName
            industry
            basePrice
            totalProduced
            activeManufacturerCount
            totalSold
            totalRevenue
            avgSellingPrice
            marketSize
            activeSellerCount
            activeCityCount
            totalMaterialCost
            totalLaborCost
            totalEnergyCost
            totalCost
            marketSaturation
            totalMarketingSpend
            totalResearchSpend
            marketingScore
            researchScore
          }
        }
      }
    `)

    result.value = data.adminProductAnalytics
  } catch (caughtError) {
    error.value = caughtError instanceof Error ? caughtError.message : t('operations.productAnalytics.loadFailed')
  } finally {
    loading.value = false
  }
}

onMounted(loadAnalytics)
</script>

<template>
  <div class="ops-analytics">
    <div class="ops-analytics-header">
      <div>
        <h2>{{ t('operations.productAnalytics.title') }}</h2>
        <p v-if="result">{{ t('operations.productAnalytics.subtitle', { window: result.windowTicks }) }}</p>
      </div>
      <button type="button" class="btn btn-secondary" :disabled="!filtered.length" @click="exportCsv">
        {{ t('operations.productAnalytics.exportCsv') }}
      </button>
    </div>

    <div class="ops-analytics-controls">
      <input
        v-model="searchQuery"
        class="form-input ops-search"
        :placeholder="t('operations.productAnalytics.searchPlaceholder')"
      />
      <select v-model="industryFilter" class="form-select ops-industry-select">
        <option value="ALL">{{ t('operations.productAnalytics.filterAll') }}</option>
        <option v-for="industry in industries" :key="industry" :value="industry">{{ industry }}</option>
      </select>
    </div>

    <div v-if="loading" class="ops-loading">{{ t('common.loading') }}</div>
    <div v-else-if="error" class="ops-error card">
      <p>{{ error }}</p>
      <button type="button" class="btn btn-secondary" @click="loadAnalytics">{{ t('common.retry') }}</button>
    </div>
    <template v-else-if="result">
      <div class="ops-table-wrap">
        <table class="ops-table ops-analytics-table" aria-label="Product analytics">
          <thead>
            <tr>
              <th class="col-product">{{ t('operations.productAnalytics.colProduct') }}</th>
              <th class="col-industry">{{ t('operations.productAnalytics.colIndustry') }}</th>
              <th class="col-num sortable" @click="setSort('totalProduced')">
                {{ t('operations.productAnalytics.colProduced') }}{{ sortIndicator('totalProduced') }}
              </th>
              <th class="col-num sortable" @click="setSort('activeManufacturerCount')">
                {{ t('operations.productAnalytics.colManufacturers') }}{{ sortIndicator('activeManufacturerCount') }}
              </th>
              <th class="col-num sortable" @click="setSort('totalSold')">
                {{ t('operations.productAnalytics.colSold') }}{{ sortIndicator('totalSold') }}
              </th>
              <th class="col-num sortable" @click="setSort('marketSize')">
                {{ t('operations.productAnalytics.colMarketSize') }}{{ sortIndicator('marketSize') }}
              </th>
              <th class="col-num sortable" @click="setSort('marketSaturation')">
                {{ t('operations.productAnalytics.colSaturation') }}{{ sortIndicator('marketSaturation') }}
              </th>
              <th class="col-num sortable" @click="setSort('totalMaterialCost')">
                {{ t('operations.productAnalytics.colMaterials') }}{{ sortIndicator('totalMaterialCost') }}
              </th>
              <th class="col-num sortable" @click="setSort('totalLaborCost')">
                {{ t('operations.productAnalytics.colLabor') }}{{ sortIndicator('totalLaborCost') }}
              </th>
              <th class="col-num sortable" @click="setSort('totalEnergyCost')">
                {{ t('operations.productAnalytics.colEnergy') }}{{ sortIndicator('totalEnergyCost') }}
              </th>
              <th class="col-num sortable" @click="setSort('totalRevenue')">
                {{ t('operations.productAnalytics.colRevenue') }}{{ sortIndicator('totalRevenue') }}
              </th>
              <th class="col-num sortable" @click="setSort('marketingScore')">
                {{ t('operations.productAnalytics.colMarketingScore') }}{{ sortIndicator('marketingScore') }}
              </th>
              <th class="col-num sortable" @click="setSort('researchScore')">
                {{ t('operations.productAnalytics.colResearchScore') }}{{ sortIndicator('researchScore') }}
              </th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="filtered.length === 0">
              <td colspan="13" class="ops-table-empty">{{ t('operations.productAnalytics.noProducts') }}</td>
            </tr>
            <tr v-for="row in filtered" :key="row.productTypeId">
              <td class="col-product">
                <span class="ops-product-name">{{ row.productName }}</span>
                <span class="ops-product-subtitle">{{ formatCurrency(row.basePrice) }}</span>
              </td>
              <td class="col-industry">
                <span class="badge badge-primary badge-sm">{{ row.industry }}</span>
              </td>
              <td class="col-num">{{ formatNumber(row.totalProduced) }}</td>
              <td class="col-num">{{ formatNumber(row.activeManufacturerCount) }}</td>
              <td class="col-num">{{ formatNumber(row.totalSold) }}</td>
              <td class="col-num">{{ formatNumber(row.marketSize) }}</td>
              <td class="col-num">{{ formatPercent(row.marketSaturation) }}</td>
              <td class="col-num">{{ formatCurrency(row.totalMaterialCost) }}</td>
              <td class="col-num">{{ formatCurrency(row.totalLaborCost) }}</td>
              <td class="col-num">{{ formatCurrency(row.totalEnergyCost) }}</td>
              <td class="col-num ops-highlight">{{ formatCurrency(row.totalRevenue) }}</td>
              <td class="col-num">{{ formatPercent(row.marketingScore ?? 0) }}</td>
              <td class="col-num">{{ formatPercent(row.researchScore ?? 0) }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>
  </div>
</template>

<style scoped>
.ops-analytics {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.ops-analytics-header,
.ops-analytics-controls {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 0.75rem;
  align-items: center;
}

.ops-analytics-header p {
  color: var(--color-text-secondary);
  margin-top: 0.25rem;
}

.ops-search {
  flex: 1;
  min-width: 220px;
}

.ops-industry-select {
  min-width: 180px;
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

.ops-table-wrap {
  overflow: auto;
  max-height: 70vh;
}

.ops-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.88rem;
}

.ops-table th,
.ops-table td {
  padding: 0.8rem 1rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  white-space: nowrap;
}

.ops-table thead th {
  position: sticky;
  top: 0;
  z-index: 1;
  background: var(--color-card);
  text-align: left;
}

.sortable {
  cursor: pointer;
}

.col-num {
  text-align: right;
}

.ops-product-name {
  display: block;
  font-weight: 600;
}

.ops-product-subtitle {
  display: block;
  margin-top: 0.2rem;
  color: var(--color-text-secondary);
  font-size: 0.8rem;
}

.ops-highlight {
  color: #4ade80;
}

.ops-table-empty {
  text-align: center;
  padding: 2rem;
  color: var(--color-text-secondary);
}
</style>

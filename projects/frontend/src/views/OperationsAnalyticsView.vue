<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'

const { t, locale } = useI18n()

interface ProductRow {
  productTypeId: string
  productName: string
  industry: string
  basePrice: number
  totalProduced: number
  activeManufacturerCount: number
  totalSold: number
  totalRevenue: number
  avgSellingPrice: number | null
  activeSellerCount: number
  activeCityCount: number
  totalMaterialCost: number
  totalLaborCost: number
  totalEnergyCost: number
  totalCost: number
  marketSaturation: number
  totalMarketingSpend: number
}

interface AnalyticsResult {
  windowTicks: number
  currentTick: number
  rows: ProductRow[]
}

type SortKey = keyof Omit<ProductRow, 'productTypeId' | 'productName' | 'industry' | 'avgSellingPrice'>

const result = ref<AnalyticsResult | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)

// ── Filters ───────────────────────────────────────────────────────────────
const searchQuery = ref('')
const industryFilter = ref('ALL')
const sortKey = ref<SortKey>('totalRevenue')
const sortAsc = ref(false)

const industries = computed(() => {
  const set = new Set<string>()
  result.value?.rows.forEach((r) => set.add(r.industry))
  return Array.from(set).sort()
})

const filtered = computed(() => {
  const q = searchQuery.value.toLowerCase()
  let rows = result.value?.rows ?? []

  if (industryFilter.value !== 'ALL') {
    rows = rows.filter((r) => r.industry === industryFilter.value)
  }
  if (q) {
    rows = rows.filter((r) => r.productName.toLowerCase().includes(q))
  }

  return [...rows].sort((a, b) => {
    const va = a[sortKey.value] as number
    const vb = b[sortKey.value] as number
    return sortAsc.value ? va - vb : vb - va
  })
})

function setSort(key: SortKey) {
  if (sortKey.value === key) {
    sortAsc.value = !sortAsc.value
  } else {
    sortKey.value = key
    sortAsc.value = false
  }
}

function sortIndicator(key: SortKey) {
  if (sortKey.value !== key) return ''
  return sortAsc.value ? ' ↑' : ' ↓'
}

function formatNum(value: number) {
  return new Intl.NumberFormat(locale.value, { maximumFractionDigits: 0 }).format(value)
}

function formatCurrency(value: number) {
  return new Intl.NumberFormat(locale.value, {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 0,
  }).format(value)
}

function formatPct(value: number) {
  return value.toFixed(1) + '%'
}

// ── CSV export ─────────────────────────────────────────────────────────────
function csvEscape(value: string | number | undefined | null): string {
  const str = value == null ? '' : String(value)
  // Wrap in double-quotes and double any internal double-quotes
  if (str.includes(',') || str.includes('"') || str.includes('\n')) {
    return `"${str.replace(/"/g, '""')}"`
  }
  return str
}
function exportCsv() {
  if (!filtered.value.length) return
  const headers = [
    'Product',
    'Industry',
    'BasePrice',
    'Produced',
    'Sold',
    'Revenue',
    'AvgPrice',
    'Sellers',
    'Cities',
    'TotalCost',
    'Materials',
    'Labor',
    'Energy',
    'Marketing',
    'Saturation%',
  ]
  const rows = filtered.value.map((r) => [
    csvEscape(r.productName),
    csvEscape(r.industry),
    r.basePrice,
    r.totalProduced,
    r.totalSold,
    r.totalRevenue,
    r.avgSellingPrice ?? '',
    r.activeSellerCount,
    r.activeCityCount,
    r.totalCost,
    r.totalMaterialCost,
    r.totalLaborCost,
    r.totalEnergyCost,
    r.totalMarketingSpend,
    r.marketSaturation,
  ])
  const csv = [headers, ...rows].map((r) => r.join(',')).join('\n')
  const blob = new Blob([csv], { type: 'text/csv' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `product-analytics-tick-${result.value?.currentTick ?? 0}.csv`
  a.click()
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
            activeSellerCount
            activeCityCount
            totalMaterialCost
            totalLaborCost
            totalEnergyCost
            totalCost
            marketSaturation
            totalMarketingSpend
          }
        }
      }
    `)
    result.value = data.adminProductAnalytics
  } catch (caughtError) {
    error.value = caughtError instanceof Error ? caughtError.message : t('operations.analytics.loadFailed')
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
        <h2>{{ t('operations.analytics.title') }}</h2>
        <p v-if="result">{{ t('operations.analytics.subtitle', { window: result.windowTicks }) }}</p>
      </div>
      <button type="button" class="btn btn-secondary" :disabled="!filtered.length" @click="exportCsv">
        📥 {{ t('operations.analytics.exportCsv') }}
      </button>
    </div>

    <!-- Controls -->
    <div class="ops-analytics-controls">
      <input
        v-model="searchQuery"
        class="form-input ops-search"
        :placeholder="t('operations.analytics.searchPlaceholder')"
      />
      <select v-model="industryFilter" class="form-select ops-industry-select">
        <option value="ALL">{{ t('operations.analytics.filterAll') }}</option>
        <option v-for="ind in industries" :key="ind" :value="ind">{{ ind }}</option>
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
              <th class="col-product">{{ t('operations.analytics.colProduct') }}</th>
              <th class="col-industry">{{ t('operations.analytics.colIndustry') }}</th>
              <th class="col-num sortable" @click="setSort('totalProduced')">
                {{ t('operations.analytics.colProduced') }}{{ sortIndicator('totalProduced') }}
              </th>
              <th class="col-num sortable" @click="setSort('totalSold')">
                {{ t('operations.analytics.colSold') }}{{ sortIndicator('totalSold') }}
              </th>
              <th class="col-num sortable" @click="setSort('totalRevenue')">
                {{ t('operations.analytics.colRevenue') }}{{ sortIndicator('totalRevenue') }}
              </th>
              <th class="col-num sortable" @click="setSort('activeSellerCount')">
                {{ t('operations.analytics.colSellers') }}{{ sortIndicator('activeSellerCount') }}
              </th>
              <th class="col-num sortable" @click="setSort('totalCost')">
                {{ t('operations.analytics.colTotalCost') }}{{ sortIndicator('totalCost') }}
              </th>
              <th class="col-num sortable" @click="setSort('totalMaterialCost')">
                {{ t('operations.analytics.colMaterials') }}{{ sortIndicator('totalMaterialCost') }}
              </th>
              <th class="col-num sortable" @click="setSort('totalLaborCost')">
                {{ t('operations.analytics.colLabor') }}{{ sortIndicator('totalLaborCost') }}
              </th>
              <th class="col-num sortable" @click="setSort('totalEnergyCost')">
                {{ t('operations.analytics.colEnergy') }}{{ sortIndicator('totalEnergyCost') }}
              </th>
              <th class="col-num sortable" @click="setSort('totalMarketingSpend')">
                {{ t('operations.analytics.colMarketing') }}{{ sortIndicator('totalMarketingSpend') }}
              </th>
              <th class="col-num sortable" @click="setSort('marketSaturation')">
                {{ t('operations.analytics.colSaturation') }}{{ sortIndicator('marketSaturation') }}
              </th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="filtered.length === 0">
              <td colspan="12" class="ops-table-empty">{{ t('operations.analytics.noProducts') }}</td>
            </tr>
            <tr v-for="row in filtered" :key="row.productTypeId">
              <td class="col-product">
                <span class="ops-product-name">{{ row.productName }}</span>
              </td>
              <td class="col-industry"><span class="badge badge-primary badge-sm">{{ row.industry }}</span></td>
              <td class="col-num">{{ formatNum(row.totalProduced) }}</td>
              <td class="col-num">{{ formatNum(row.totalSold) }}</td>
              <td class="col-num ops-highlight">{{ formatCurrency(row.totalRevenue) }}</td>
              <td class="col-num">{{ row.activeSellerCount }}</td>
              <td class="col-num ops-negative">{{ formatCurrency(row.totalCost) }}</td>
              <td class="col-num">{{ formatCurrency(row.totalMaterialCost) }}</td>
              <td class="col-num">{{ formatCurrency(row.totalLaborCost) }}</td>
              <td class="col-num">{{ formatCurrency(row.totalEnergyCost) }}</td>
              <td class="col-num">{{ formatCurrency(row.totalMarketingSpend) }}</td>
              <td class="col-num">
                <div class="saturation-bar-wrap">
                  <div
                    class="saturation-bar"
                    :class="{ 'saturation-high': row.marketSaturation > 70, 'saturation-medium': row.marketSaturation > 30 }"
                    :style="{ width: Math.min(row.marketSaturation, 100) + '%' }"
                  ></div>
                  <span>{{ formatPct(row.marketSaturation) }}</span>
                </div>
              </td>
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

.ops-analytics-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
}

.ops-analytics-header h2 {
  margin-bottom: 0.2rem;
}

.ops-analytics-header p {
  color: var(--color-text-secondary);
  font-size: 0.9rem;
}

.ops-analytics-controls {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
}

.ops-search {
  flex: 1;
  min-width: 200px;
  max-width: 280px;
}

.ops-industry-select {
  min-width: 160px;
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

.ops-table-wrap {
  overflow-x: auto;
}

.ops-analytics-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.83rem;
}

.ops-analytics-table th {
  text-align: left;
  padding: 0.55rem 0.75rem;
  border-bottom: 1px solid var(--color-border);
  color: var(--color-text-secondary);
  font-weight: 500;
  white-space: nowrap;
}

.ops-analytics-table td {
  padding: 0.65rem 0.75rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.04);
  vertical-align: middle;
}

.ops-analytics-table tr:last-child td {
  border-bottom: none;
}

.ops-analytics-table tr:hover td {
  background: rgba(255, 255, 255, 0.02);
}

.sortable {
  cursor: pointer;
  user-select: none;
}

.sortable:hover {
  color: var(--color-text);
}

.col-product {
  min-width: 140px;
}

.col-industry {
  min-width: 110px;
}

.col-num {
  text-align: right;
  min-width: 90px;
}

.ops-product-name {
  font-weight: 500;
}

.ops-highlight {
  color: #4ade80;
  font-weight: 600;
}

.ops-negative {
  color: #f87171;
}

.ops-table-empty {
  text-align: center;
  padding: 2rem;
  color: var(--color-text-secondary);
}

.badge-sm {
  font-size: 0.68rem;
  padding: 0.1rem 0.45rem;
}

/* Saturation indicator */
.saturation-bar-wrap {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  justify-content: flex-end;
}

.saturation-bar {
  height: 6px;
  border-radius: 3px;
  min-width: 2px;
  max-width: 60px;
  background: rgba(74, 222, 128, 0.5);
  transition: width 0.2s;
}

.saturation-bar.saturation-medium {
  background: rgba(251, 191, 36, 0.6);
}

.saturation-bar.saturation-high {
  background: rgba(248, 113, 113, 0.7);
}
</style>

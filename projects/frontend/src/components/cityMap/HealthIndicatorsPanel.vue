<script setup lang="ts">
import { ref, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { CityEconomicReportResult, CityEconomicReport } from '@/types'

const props = defineProps<{
  data: CityEconomicReportResult | null
  loading: boolean
}>()

const { t } = useI18n()
const showModal = ref(false)

const latest = computed(() => props.data?.latest ?? null)
const history = computed(() => props.data?.history ?? [])

/** Return Tailwind-equivalent CSS variable color for index band. */
function indexColor(index: number | null): string {
  if (index === null) return 'var(--color-text-muted)'
  if (index >= 70) return '#22c55e' // green
  if (index >= 40) return '#f59e0b' // amber
  return '#ef4444' // red
}

function indexLabel(index: number | null): string {
  if (index === null) return '—'
  if (index >= 70) return t('cityHealth.statusThriving')
  if (index >= 40) return t('cityHealth.statusNeutral')
  return t('cityHealth.statusDeclining')
}

/** SVG ring circumference math. */
const RADIUS = 36
const CIRCUMFERENCE = 2 * Math.PI * RADIUS

function ringOffset(index: number | null): number {
  if (index === null) return CIRCUMFERENCE
  const pct = Math.max(0, Math.min(100, index)) / 100
  return CIRCUMFERENCE * (1 - pct)
}

function formatNumber(n: number | null | undefined, decimals = 0): string {
  if (n === null || n === undefined) return '—'
  return n.toLocaleString(undefined, {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  })
}

function formatPower(mw: number | null | undefined): string {
  if (mw === null || mw === undefined) return '—'
  return `${mw.toFixed(1)} MW`
}

/** Sparkline helper: normalise history indexes to a 0-30 height range. */
const sparkPoints = computed(() => {
  const items = history.value.slice(-10)
  if (items.length === 0) return ''
  const maxVal = items.reduce((m: number, r: CityEconomicReport) => Math.max(m, r.economicIndex), 1)
  const w = 100 / Math.max(items.length - 1, 1)
  return items
    .map((r: CityEconomicReport, i: number) => {
      const x = i * w
      const y = 30 - (r.economicIndex / maxVal) * 30
      return `${x},${y}`
    })
    .join(' ')
})
</script>

<template>
  <div class="health-panel">
    <div v-if="loading" class="health-loading">{{ t('common.loading') }}</div>

    <div v-else-if="!latest" class="health-empty">
      <span>{{ t('cityHealth.noData') }}</span>
    </div>

    <div v-else class="health-content">
      <!-- Score ring -->
      <div
        class="score-ring-wrap"
        role="button"
        tabindex="0"
        :aria-label="t('cityHealth.detailsAriaLabel')"
        @click="showModal = true"
        @keydown.enter.prevent="showModal = true"
        @keydown.space.prevent="showModal = true"
      >
        <svg class="score-ring" viewBox="0 0 88 88" width="88" height="88">
          <circle cx="44" cy="44" :r="RADIUS" fill="none" stroke="var(--color-border)" stroke-width="8" />
          <circle
            class="ring-progress"
            cx="44"
            cy="44"
            :r="RADIUS"
            fill="none"
            :stroke="indexColor(latest.economicIndex)"
            stroke-width="8"
            stroke-linecap="round"
            :stroke-dasharray="CIRCUMFERENCE"
            :stroke-dashoffset="ringOffset(latest.economicIndex)"
          />
        </svg>
        <div class="score-center">
          <span class="score-value" :style="{ color: indexColor(latest.economicIndex) }">
            {{ latest.economicIndex.toFixed(0) }}
          </span>
          <span class="score-label">{{ t('cityHealth.index') }}</span>
        </div>
      </div>

      <!-- Status badge -->
      <div class="status-badge" :style="{ color: indexColor(latest.economicIndex) }">
        {{ indexLabel(latest.economicIndex) }}
      </div>

      <!-- 2×2 metrics grid -->
      <div class="metrics-grid">
        <div class="metric-card">
          <span class="metric-label">{{ t('cityHealth.salaries') }}</span>
          <span class="metric-value">{{ formatNumber(latest.totalSalaries) }}</span>
        </div>
        <div class="metric-card">
          <span class="metric-label">{{ t('cityHealth.revenue') }}</span>
          <span class="metric-value">{{ formatNumber(latest.totalPublicRevenue) }}</span>
        </div>
        <div class="metric-card">
          <span class="metric-label">{{ t('cityHealth.companies') }}</span>
          <span class="metric-value">{{ latest.activeCompanies }}</span>
        </div>
        <div class="metric-card">
          <span class="metric-label">{{ t('cityHealth.quality') }}</span>
          <span class="metric-value">{{ (latest.averageProductQuality * 100).toFixed(0) }}%</span>
        </div>
      </div>

      <!-- Sparkline trend -->
      <div v-if="history.length > 1" class="sparkline-wrap">
        <span class="sparkline-label">{{ t('cityHealth.trend') }}</span>
        <svg class="sparkline" viewBox="0 0 100 32" preserveAspectRatio="none">
          <polyline
            :points="sparkPoints"
            fill="none"
            :stroke="indexColor(latest.economicIndex)"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
          />
        </svg>
      </div>

      <!-- Detail link -->
      <button class="btn btn-sm btn-secondary health-detail-btn" @click="showModal = true">
        {{ t('cityHealth.viewDetails') }}
      </button>
    </div>

    <!-- Detail modal -->
    <Teleport to="body">
      <div v-if="showModal" class="health-modal-overlay" @click.self="showModal = false">
        <div class="health-modal" role="dialog" :aria-label="t('cityHealth.detailsAriaLabel')">
          <div class="health-modal-header">
            <h3>{{ t('cityHealth.detailsTitle') }}</h3>
            <button class="modal-close-btn" @click="showModal = false">✕</button>
          </div>

          <div v-if="latest" class="health-modal-body">
            <!-- Full metric table -->
            <table class="metrics-table">
              <tbody>
                <tr>
                  <td>{{ t('cityHealth.economicIndex') }}</td>
                  <td :style="{ color: indexColor(latest.economicIndex) }">
                    <strong>{{ latest.economicIndex.toFixed(1) }}</strong>
                    — {{ indexLabel(latest.economicIndex) }}
                  </td>
                </tr>
                <tr>
                  <td>{{ t('cityHealth.salaries') }}</td>
                  <td>{{ formatNumber(latest.totalSalaries, 0) }}</td>
                </tr>
                <tr>
                  <td>{{ t('cityHealth.revenue') }}</td>
                  <td>{{ formatNumber(latest.totalPublicRevenue, 0) }}</td>
                </tr>
                <tr>
                  <td>{{ t('cityHealth.companies') }}</td>
                  <td>{{ latest.activeCompanies }}</td>
                </tr>
                <tr>
                  <td>{{ t('cityHealth.power') }}</td>
                  <td>
                    {{ formatPower(latest.totalPowerSupply) }} /
                    {{ formatPower(latest.totalPowerConsumption) }}
                  </td>
                </tr>
                <tr>
                  <td>{{ t('cityHealth.quality') }}</td>
                  <td>{{ (latest.averageProductQuality * 100).toFixed(1) }}%</td>
                </tr>
                <tr>
                  <td>{{ t('cityHealth.cycle') }}</td>
                  <td>{{ t('cityHealth.cycleValue', { tick: latest.taxCycleEnd }) }}</td>
                </tr>
              </tbody>
            </table>

            <!-- History list -->
            <div v-if="history.length > 1" class="history-section">
              <h4>{{ t('cityHealth.historyTitle') }}</h4>
              <div class="history-list">
                <div
                  v-for="report in history"
                  :key="report.id"
                  class="history-row"
                  :class="{ latest: report.id === latest.id }"
                >
                  <span class="history-tick">{{ t('cityHealth.cycleValue', { tick: report.taxCycleEnd }) }}</span>
                  <span class="history-index" :style="{ color: indexColor(report.economicIndex) }">
                    {{ report.economicIndex.toFixed(1) }}
                  </span>
                  <span class="history-status" :style="{ color: indexColor(report.economicIndex) }">
                    {{ indexLabel(report.economicIndex) }}
                  </span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.health-panel {
  background: var(--color-card);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 16px;
  min-width: 220px;
}

.health-loading,
.health-empty {
  color: var(--color-text-muted);
  font-size: 0.875rem;
  text-align: center;
  padding: 12px 0;
}

.health-content {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10px;
}

/* Score ring */
.score-ring-wrap {
  position: relative;
  cursor: pointer;
  width: 88px;
  height: 88px;
}

.score-ring {
  transform: rotate(-90deg);
}

.ring-progress {
  transition: stroke-dashoffset 0.4s ease;
}

.score-center {
  position: absolute;
  inset: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  pointer-events: none;
}

.score-value {
  font-size: 1.5rem;
  font-weight: 700;
  line-height: 1;
}

.score-label {
  font-size: 0.6rem;
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.06em;
}

/* Status badge */
.status-badge {
  font-size: 0.8rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

/* Metrics grid */
.metrics-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 6px;
  width: 100%;
}

.metric-card {
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 6px 8px;
  display: flex;
  flex-direction: column;
}

.metric-label {
  font-size: 0.65rem;
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.metric-value {
  font-size: 0.85rem;
  font-weight: 600;
  color: var(--color-text);
  margin-top: 2px;
}

/* Sparkline */
.sparkline-wrap {
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.sparkline-label {
  font-size: 0.65rem;
  color: var(--color-text-muted);
  text-transform: uppercase;
}

.sparkline {
  width: 100%;
  height: 32px;
}

/* Detail button */
.health-detail-btn {
  width: 100%;
  font-size: 0.75rem;
}

/* Modal */
.health-modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 9999;
}

.health-modal {
  background: var(--color-card);
  border: 1px solid var(--color-border);
  border-radius: 16px;
  padding: 24px;
  width: 480px;
  max-width: 95vw;
  max-height: 80vh;
  overflow-y: auto;
}

.health-modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.health-modal-header h3 {
  font-size: 1.1rem;
  font-weight: 600;
}

.modal-close-btn {
  background: none;
  border: none;
  cursor: pointer;
  color: var(--color-text-muted);
  font-size: 1.2rem;
  padding: 0;
  line-height: 1;
}

.metrics-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.875rem;
}

.metrics-table td {
  padding: 8px 6px;
  border-bottom: 1px solid var(--color-border);
}

.metrics-table td:first-child {
  color: var(--color-text-muted);
  width: 40%;
}

/* History */
.history-section {
  margin-top: 16px;
}

.history-section h4 {
  font-size: 0.875rem;
  font-weight: 600;
  margin-bottom: 8px;
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.history-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.history-row {
  display: flex;
  gap: 12px;
  align-items: center;
  font-size: 0.8rem;
  padding: 4px 6px;
  border-radius: 6px;
}

.history-row.latest {
  background: var(--color-bg);
}

.history-tick {
  color: var(--color-text-muted);
  flex: 1;
}

.history-index {
  font-weight: 700;
  min-width: 32px;
  text-align: right;
}

.history-status {
  min-width: 80px;
  text-align: right;
  font-size: 0.7rem;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}
</style>

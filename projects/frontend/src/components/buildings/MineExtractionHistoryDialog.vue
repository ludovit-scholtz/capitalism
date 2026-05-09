<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { gqlRequest } from '@/lib/graphql'
import type { Building } from '@/types'

const { t } = useI18n()
const router = useRouter()

// ── Props & emits ─────────────────────────────────────────────────────────────

interface ExtractionRecord {
  tick: number
  extractedAmount: number
  efficiencyPercent: number
  reserveRemaining: number
}

interface DepletionForecast {
  averageExtractionRatePerTick: number | null
  depletionTick: number | null
  critical5PctTick: number | null
  critical20PctTick: number | null
  estimatedGameDaysRemaining: number | null
  currentReserve: number | null
  originalReserve: number | null
}

const props = defineProps<{
  building: Building
  initialRecords: ExtractionRecord[]
}>()

const emit = defineEmits<{
  (e: 'close'): void
}>()

// ── State ─────────────────────────────────────────────────────────────────────

const records = ref<ExtractionRecord[]>(props.initialRecords)
const forecast = ref<DepletionForecast | null>(null)
const loadingForecast = ref(false)

// ── Load forecast on mount ────────────────────────────────────────────────────

onMounted(() => {
  void loadForecast()
})

watch(() => props.building.id, () => { void loadForecast() })

async function loadForecast() {
  loadingForecast.value = true
  try {
    const result = await gqlRequest<{ getMineDepletionForecast: DepletionForecast | null }>(`
      query GetMineDepletionForecast($buildingId: UUID!) {
        getMineDepletionForecast(buildingId: $buildingId) {
          averageExtractionRatePerTick
          depletionTick
          critical5PctTick
          critical20PctTick
          estimatedGameDaysRemaining
          currentReserve
          originalReserve
        }
      }
    `, { buildingId: props.building.id })
    forecast.value = result.getMineDepletionForecast ?? null
  } catch {
    forecast.value = null
  } finally {
    loadingForecast.value = false
  }
}

// ── Bar chart: per-tick extraction (last 720 ticks = 30 game days) ────────────

/** Records sorted ascending for chart */
const sortedRecords = computed(() =>
  [...records.value].sort((a, b) => a.tick - b.tick)
)

const maxExtracted = computed(() => {
  const vals = sortedRecords.value.map((r) => r.extractedAmount)
  return vals.length ? Math.max(...vals, 0.001) : 1
})

const CHART_H = 160

function barHeight(extracted: number): number {
  return Math.max(2, (extracted / maxExtracted.value) * CHART_H)
}

function barColor(record: ExtractionRecord): string {
  const original = props.building.lotOriginalMaterialQuantity ?? null
  if (!original || original <= 0) return '#22c55e'
  const pct = (record.reserveRemaining / original) * 100
  if (pct < 5) return '#ef4444'
  if (pct < 20) return '#f59e0b'
  return '#22c55e'
}

// ── Depletion timeline ────────────────────────────────────────────────────────

interface Milestone {
  key: string
  label: string
  tick: number | null
  gameDays: number | null
  colorClass: string
  icon: string
}

const currentTick = computed(() => {
  if (records.value.length === 0) return 0
  return Math.max(...records.value.map((r) => r.tick))
})

const milestones = computed<Milestone[]>(() => {
  const f = forecast.value
  if (!f) return []

  const ticksPerDay = 24

  function daysFromNow(tick: number | null): number | null {
    if (tick === null) return null
    return Math.ceil((tick - currentTick.value) / ticksPerDay)
  }

  return [
    {
      key: 'today',
      label: t('mining.milestoneToday'),
      tick: currentTick.value,
      gameDays: 0,
      colorClass: 'text-body',
      icon: '📍',
    },
    {
      key: '20pct',
      label: t('mining.milestone20pct'),
      tick: f.critical20PctTick,
      gameDays: daysFromNow(f.critical20PctTick),
      colorClass: 'text-warning',
      icon: '⚠️',
    },
    {
      key: '5pct',
      label: t('mining.milestone5pct'),
      tick: f.critical5PctTick,
      gameDays: daysFromNow(f.critical5PctTick),
      colorClass: 'text-error',
      icon: '🚨',
    },
    {
      key: 'depletion',
      label: t('mining.milestoneDepletion'),
      tick: f.depletionTick,
      gameDays: daysFromNow(f.depletionTick),
      colorClass: 'text-error font-bold',
      icon: '💀',
    },
  ].filter((m) => m.tick !== null || m.key === 'today')
})

// ── Reserve percent ───────────────────────────────────────────────────────────

const reservePercent = computed<number | null>(() => {
  const f = forecast.value
  if (!f?.currentReserve || !f.originalReserve || f.originalReserve <= 0) return null
  return Math.round((f.currentReserve / f.originalReserve) * 100)
})

// ── CTA: find new deposit ──────────────────────────────────────────────────────

function findNewDeposit() {
  // Navigate to city map / buy-building with MINE type filter
  void router.push({ path: '/buy-building', query: { type: 'MINE' } })
  emit('close')
}
</script>

<template>
  <!-- Backdrop -->
  <div
    class="mine-history-dialog-backdrop fixed inset-0 z-50 flex items-end justify-center bg-black/70 sm:items-center"
    @click.self="emit('close')"
  >
    <!-- Dialog sheet -->
    <div
      class="mine-history-dialog relative flex max-h-[90vh] w-full max-w-3xl flex-col overflow-hidden rounded-t-2xl bg-card sm:rounded-2xl"
      role="dialog"
      :aria-label="t('mining.depletionTimelineTitle')"
    >
      <!-- Header -->
      <div class="flex items-start justify-between border-b border-divider px-5 py-4">
        <div>
          <h3 class="text-base font-bold text-heading">{{ t('mining.depletionTimelineTitle') }}</h3>
          <p class="mt-0.5 text-sm text-muted">{{ building.name }}</p>
        </div>
        <div class="flex items-center gap-3">
          <!-- Reserve badge -->
          <span
            v-if="reservePercent !== null"
            class="rounded-full px-3 py-1 text-sm font-semibold"
            :class="{
              'bg-success/15 text-success': reservePercent >= 20,
              'bg-warning/15 text-warning': reservePercent >= 5 && reservePercent < 20,
              'bg-error/15 text-error': reservePercent < 5,
            }"
          >
            {{ reservePercent }}% {{ t('mining.remaining', { percent: '' }).trim() }}
          </span>
          <button
            class="rounded-md p-1 text-muted transition-colors hover:text-body"
            :aria-label="t('mining.dialogClose')"
            @click="emit('close')"
          >
            ✕
          </button>
        </div>
      </div>

      <!-- Scrollable body -->
      <div class="flex-1 overflow-y-auto px-5 py-4">

        <!-- Forecast summary -->
        <div v-if="loadingForecast" class="mb-5 text-sm text-muted">{{ t('common.loading') }}</div>
        <div v-else-if="forecast?.estimatedGameDaysRemaining !== null && forecast?.estimatedGameDaysRemaining !== undefined" class="mb-5 rounded-lg border border-divider bg-surface p-4">
          <div class="grid grid-cols-2 gap-3 text-sm sm:grid-cols-3">
            <div>
              <span class="block text-xs text-muted">{{ t('mining.avgExtractionRate') }}</span>
              <strong class="text-body">
                {{ t('mining.avgExtractionRateValue', {
                  rate: forecast.averageExtractionRatePerTick?.toLocaleString(undefined, { maximumFractionDigits: 2 }) ?? '—',
                  unit: 't',
                }) }}
              </strong>
            </div>
            <div>
              <span class="block text-xs text-muted">{{ t('mining.estimatedDepletion') }}</span>
              <strong class="text-body">
                {{ t('mining.gameDaysRemaining', { days: Math.ceil(forecast.estimatedGameDaysRemaining ?? 0).toLocaleString() }) }}
              </strong>
            </div>
            <div v-if="forecast.currentReserve !== null">
              <span class="block text-xs text-muted">{{ t('mining.remainingQuantity', { quantity: '', unit: '' }).trim() }}</span>
              <strong class="text-body">
                {{ forecast.currentReserve?.toLocaleString(undefined, { maximumFractionDigits: 1 }) ?? '—' }} t
              </strong>
            </div>
          </div>
        </div>
        <div v-else-if="!loadingForecast" class="mb-5 rounded-lg border border-divider bg-surface px-4 py-3 text-sm text-muted">
          {{ t('mining.noForecastData') }}
        </div>

        <!-- Depletion timeline -->
        <section v-if="milestones.length > 0" class="depletion-timeline mb-6">
          <h4 class="mb-3 text-xs font-semibold uppercase tracking-wide text-muted">
            {{ t('mining.depletionTimeline') }}
          </h4>
          <div class="flex flex-col gap-2">
            <div
              v-for="m in milestones"
              :key="m.key"
              class="depletion-milestone flex items-start gap-3 rounded-lg border border-divider bg-surface px-4 py-2.5"
            >
              <span class="mt-0.5 text-base">{{ m.icon }}</span>
              <div class="flex-1">
                <span class="font-medium text-sm" :class="m.colorClass">{{ m.label }}</span>
                <span v-if="m.gameDays !== null && m.gameDays > 0" class="ml-2 text-xs text-muted">
                  (+{{ m.gameDays.toLocaleString() }} {{ t('mining.extractionHistoryDays', { days: '' }).trim() }})
                </span>
                <span v-else-if="m.gameDays === 0" class="ml-2 text-xs text-muted">
                  {{ t('mining.milestoneToday') }}
                </span>
              </div>
              <span v-if="m.tick !== null" class="text-xs text-muted">tick {{ m.tick.toLocaleString() }}</span>
            </div>
          </div>
        </section>

        <!-- Bar chart: per-tick extraction -->
        <section class="mb-4">
          <h4 class="mb-3 text-xs font-semibold uppercase tracking-wide text-muted">
            {{ t('mining.perTickExtraction') }}
          </h4>

          <div v-if="sortedRecords.length === 0" class="rounded-lg border border-divider bg-surface px-4 py-6 text-center text-sm text-muted">
            {{ t('mining.extractionHistoryEmpty') }}
          </div>

          <div v-else class="extraction-bar-chart overflow-x-auto">
            <svg
              :width="Math.max(sortedRecords.length * 6, 300)"
              :height="CHART_H + 16"
              role="img"
              :aria-label="t('mining.extractionHistoryTitle')"
            >
              <g>
                <rect
                  v-for="(record, i) in sortedRecords"
                  :key="record.tick"
                  :x="i * 6"
                  :y="CHART_H - barHeight(record.extractedAmount)"
                  :width="5"
                  :height="barHeight(record.extractedAmount)"
                  :fill="barColor(record)"
                  :title="`Tick ${record.tick}: ${record.extractedAmount.toLocaleString(undefined, { maximumFractionDigits: 2 })} t`"
                  opacity="0.85"
                />
              </g>
            </svg>
          </div>

          <!-- Legend -->
          <div class="mt-2 flex flex-wrap gap-3 text-xs text-muted">
            <span class="flex items-center gap-1">
              <span class="inline-block h-2 w-2 rounded-full bg-success" />
              {{ t('mining.reserveColorGreen') }}
            </span>
            <span class="flex items-center gap-1">
              <span class="inline-block h-2 w-2 rounded-full bg-warning" />
              {{ t('mining.reserveColorAmber') }}
            </span>
            <span class="flex items-center gap-1">
              <span class="inline-block h-2 w-2 rounded-full bg-error" />
              {{ t('mining.reserveColorRed') }}
            </span>
          </div>
        </section>
      </div>

      <!-- Sticky footer -->
      <div class="border-t border-divider px-5 py-4">
        <button
          class="find-new-deposit-btn w-full rounded-lg bg-accent px-4 py-2.5 text-sm font-semibold text-white transition-opacity hover:opacity-90"
          @click="findNewDeposit"
        >
          {{ t('mining.findNewDeposit') }}
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.extraction-bar-chart {
  border-radius: 0.5rem;
  background: var(--color-surface);
  border: 1px solid var(--color-divider);
  padding: 0.75rem;
}
</style>

<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import type { Building } from '@/types'
import type { MineExtractionDailyPoint } from '@/lib/mineExtractionIntelligence'

const { t } = useI18n()
const router = useRouter()

// ── Props & emits ─────────────────────────────────────────────────────────────

interface ExtractionIntelligence {
  currentTick: number
  burnRatePerTick: number | null
  burnRatePerDay: number | null
  expectedDepletionTick: number | null
  qualityDecayInflectionTick: number | null
  estimatedGameDaysRemaining: number | null
  currentReserve: number | null
  originalReserve: number | null
  dailyExtraction: MineExtractionDailyPoint[]
}

const props = defineProps<{
  building: Building
  intelligence: ExtractionIntelligence | null
  loading: boolean
}>()

const emit = defineEmits<{
  (e: 'close'): void
}>()

// ── State ─────────────────────────────────────────────────────────────────────

const dailyRecords = computed(() => props.intelligence?.dailyExtraction ?? [])

const maxExtracted = computed(() => {
  const vals = dailyRecords.value.map((r) => r.extractedAmount)
  return vals.length ? Math.max(...vals, 0.001) : 1
})

const CHART_H = 160

function barHeight(extracted: number): number {
  return Math.max(2, (extracted / maxExtracted.value) * CHART_H)
}

function barColor(record: MineExtractionDailyPoint): string {
  const original = props.intelligence?.originalReserve ?? props.building.lotOriginalMaterialQuantity ?? null
  if (!original || original <= 0) return '#22c55e'
  const pct = (record.reserveRemaining / original) * 100
  if (pct < 5) return '#ef4444'
  if (pct < 20) return '#f59e0b'
  return '#22c55e'
}

// ── Depletion timeline ────────────────────────────────────────────────────────

const intelligenceAdvice = computed(() => {
  const remainingDays = props.intelligence?.estimatedGameDaysRemaining ?? null
  if (remainingDays === null || remainingDays === undefined) {
    return t('mining.intelligenceAdviceNoData')
  }
  return t('mining.intelligenceAdvice', { days: Math.ceil(remainingDays).toLocaleString() })
})

// ── Reserve percent ───────────────────────────────────────────────────────────

const reservePercent = computed<number | null>(() => {
  const currentReserve = props.intelligence?.currentReserve
  const originalReserve = props.intelligence?.originalReserve
  if (currentReserve === null || currentReserve === undefined || !originalReserve || originalReserve <= 0) return null
  return Math.round((currentReserve / originalReserve) * 100)
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
      :aria-label="t('mining.extractionIntelligenceTitle')"
    >
      <!-- Header -->
      <div class="flex items-start justify-between border-b border-divider px-5 py-4">
        <div>
          <h3 class="text-base font-bold text-heading">{{ t('mining.extractionIntelligenceTitle') }}</h3>
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
        <div v-if="loading" class="mb-5 text-sm text-muted">{{ t('common.loading') }}</div>
        <div v-else-if="intelligence" class="mb-5 rounded-lg border border-divider bg-surface p-4">
          <div class="grid grid-cols-2 gap-3 text-sm sm:grid-cols-3">
            <div>
              <span class="block text-xs text-muted">{{ t('mining.burnRatePerDay') }}</span>
              <strong class="text-body">
                {{ t('mining.avgExtractionRateValue', {
                  rate: intelligence.burnRatePerDay?.toLocaleString(undefined, { maximumFractionDigits: 2 }) ?? '—',
                  unit: 't',
                }) }}
              </strong>
            </div>
            <div>
              <span class="block text-xs text-muted">{{ t('mining.estimatedDepletion') }}</span>
              <strong class="text-body">
                <template v-if="intelligence.expectedDepletionTick !== null">
                  {{ t('mining.tickWithGameDays', {
                    tick: intelligence.expectedDepletionTick.toLocaleString(),
                    days: Math.ceil(intelligence.estimatedGameDaysRemaining ?? 0).toLocaleString(),
                  }) }}
                </template>
                <template v-else>—</template>
              </strong>
            </div>
            <div v-if="intelligence.currentReserve !== null">
              <span class="block text-xs text-muted">{{ t('mining.remainingQuantity', { quantity: '', unit: '' }).trim() }}</span>
              <strong class="text-body">
                {{ intelligence.currentReserve?.toLocaleString(undefined, { maximumFractionDigits: 1 }) ?? '—' }} t
              </strong>
            </div>
          </div>
          <p class="mt-3 rounded-md border border-divider bg-card px-3 py-2 text-xs text-muted">
            {{ intelligenceAdvice }}
          </p>
        </div>
        <div v-else-if="!loading" class="mb-5 rounded-lg border border-divider bg-surface px-4 py-3 text-sm text-muted">
          {{ t('mining.noForecastData') }}
        </div>

        <section v-if="intelligence" class="depletion-timeline mb-6">
          <h4 class="mb-3 text-xs font-semibold uppercase tracking-wide text-muted">
            {{ t('mining.extractionIntelligenceTitle') }}
          </h4>
          <div class="flex flex-col gap-2">
            <div class="depletion-milestone flex items-start gap-3 rounded-lg border border-divider bg-surface px-4 py-2.5">
              <span class="mt-0.5 text-base">📉</span>
              <div class="flex-1">
                <span class="font-medium text-sm text-body">{{ t('mining.burnRatePerTick') }}</span>
              </div>
              <span class="text-xs text-muted">
                {{ intelligence.burnRatePerTick?.toLocaleString(undefined, { maximumFractionDigits: 2 }) ?? '—' }} t/tick
              </span>
            </div>
            <div class="depletion-milestone flex items-start gap-3 rounded-lg border border-divider bg-surface px-4 py-2.5">
              <span class="mt-0.5 text-base">⏳</span>
              <div class="flex-1">
                <span class="font-medium text-sm text-warning">{{ t('mining.depletionTick') }}</span>
              </div>
              <span class="text-xs text-muted">
                <template v-if="intelligence.expectedDepletionTick !== null">
                  tick {{ intelligence.expectedDepletionTick.toLocaleString() }}
                </template>
                <template v-else>—</template>
              </span>
            </div>
            <div class="depletion-milestone flex items-start gap-3 rounded-lg border border-divider bg-surface px-4 py-2.5">
              <span class="mt-0.5 text-base">🧪</span>
              <div class="flex-1">
                <span class="font-medium text-sm text-warning">{{ t('mining.qualityDecayInflectionTitle') }}</span>
                <p class="text-xs text-muted">{{ t('mining.qualityDecayInflectionDescription') }}</p>
              </div>
              <span class="text-xs text-muted">
                <template v-if="intelligence.qualityDecayInflectionTick !== null">
                  tick {{ intelligence.qualityDecayInflectionTick.toLocaleString() }}
                </template>
                <template v-else>—</template>
              </span>
            </div>
          </div>
        </section>

        <!-- Bar chart: per-day extraction -->
        <section class="mb-4">
          <h4 class="mb-3 text-xs font-semibold uppercase tracking-wide text-muted">
            {{ t('mining.perDayExtraction') }}
          </h4>

          <div v-if="dailyRecords.length === 0" class="rounded-lg border border-divider bg-surface px-4 py-6 text-center text-sm text-muted">
            {{ t('mining.extractionHistoryEmpty') }}
          </div>

          <div v-else class="extraction-bar-chart overflow-x-auto">
            <svg
              :width="Math.max(dailyRecords.length * 10, 300)"
              :height="CHART_H + 16"
              role="img"
              :aria-label="t('mining.extractionHistoryTitle')"
            >
              <g>
                <rect
                  v-for="(record, i) in dailyRecords"
                  :key="record.dayIndex"
                  :x="i * 10"
                  :y="CHART_H - barHeight(record.extractedAmount)"
                  :width="8"
                  :height="barHeight(record.extractedAmount)"
                  :fill="barColor(record)"
                  :title="`${t('mining.gameDayLabel', { day: record.dayIndex.toLocaleString() })}: ${record.extractedAmount.toLocaleString(undefined, { maximumFractionDigits: 2 })} t`"
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

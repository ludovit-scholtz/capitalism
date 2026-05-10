<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import type { Building } from '@/types'
import MineExtractionHistoryDialog from './MineExtractionHistoryDialog.vue'
import {
  buildDepletionTrendlinePath,
  buildSparklinePath,
  summarizeExtractionTrend,
  type MineExtractionDailyPoint,
} from '@/lib/mineExtractionIntelligence'

const { t } = useI18n()

const props = defineProps<{
  building: Building
}>()

// ── Types ────────────────────────────────────────────────────────────────────

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

// ── State ────────────────────────────────────────────────────────────────────

const intelligence = ref<ExtractionIntelligence | null>(null)
const loading = ref(false)
const showDialog = ref(false)

// ── Data loading ─────────────────────────────────────────────────────────────

async function loadHistory() {
  loading.value = true
  try {
    const result = await gqlRequest<{ getMineExtractionIntelligence: ExtractionIntelligence | null }>(`
      query GetMineExtractionIntelligence($buildingId: UUID!, $days: Int!) {
        getMineExtractionIntelligence(buildingId: $buildingId, days: $days) {
          currentTick
          burnRatePerTick
          burnRatePerDay
          expectedDepletionTick
          qualityDecayInflectionTick
          estimatedGameDaysRemaining
          currentReserve
          originalReserve
          dailyExtraction {
            dayIndex
            extractedAmount
            efficiencyPercent
            reserveRemaining
          }
        }
      }
    `, { buildingId: props.building.id, days: 30 })
    intelligence.value = result.getMineExtractionIntelligence ?? null
  } catch {
    intelligence.value = null
  } finally {
    loading.value = false
  }
}

watch(() => props.building.id, () => { void loadHistory() }, { immediate: true })

// ── Sparkline computations ────────────────────────────────────────────────────

/** Aggregate per-day totals (last 30 days, ascending order) */
const dailyTotals = computed<number[]>(() => {
  return (intelligence.value?.dailyExtraction ?? []).map((point) => point.extractedAmount)
})

const hasSufficientData = computed(() => dailyTotals.value.length >= 5)

/** Reserve percent from the most recent record */
const currentReservePercent = computed<number | null>(() => {
  const reserve = intelligence.value?.currentReserve ?? null
  const original = intelligence.value?.originalReserve ?? props.building.lotOriginalMaterialQuantity ?? null
  if (reserve === null) return null
  if (!original || original <= 0) return null
  return Math.round((reserve / original) * 100)
})

/** Colour class based on reserve level */
const sparklineColor = computed(() => {
  const p = currentReservePercent.value
  if (p === null) return '#6b7280'
  if (p < 5) return '#ef4444'  // red
  if (p < 20) return '#f59e0b' // amber
  return '#22c55e'             // green
})

// ── SVG sparkline path ────────────────────────────────────────────────────────

const SPARK_W = 280
const SPARK_H = 80
const maxProjectedDays = 30

const projectedDaysToDepletion = computed(() => {
  const reserve = intelligence.value?.currentReserve ?? null
  const burnRatePerDay = intelligence.value?.burnRatePerDay ?? null
  if (!reserve || !burnRatePerDay || burnRatePerDay <= 0) return 0
  return Math.min(Math.ceil(reserve / burnRatePerDay), maxProjectedDays)
})

const sparklinePath = computed(() => {
  return buildSparklinePath(
    intelligence.value?.dailyExtraction ?? [],
    SPARK_W,
    SPARK_H,
    projectedDaysToDepletion.value,
  )
})

const sparklineFill = computed(() => {
  const data = dailyTotals.value
  if (data.length < 2) return ''
  return `${sparklinePath.value} L ${SPARK_W} ${SPARK_H} L 0 ${SPARK_H} Z`
})

const depletionTrendlinePath = computed(() =>
  buildDepletionTrendlinePath(
    intelligence.value?.dailyExtraction ?? [],
    SPARK_W,
    SPARK_H,
    projectedDaysToDepletion.value,
  ),
)

const sparklineSummary = computed(() => {
  const trend = summarizeExtractionTrend(
    intelligence.value?.dailyExtraction ?? [],
    intelligence.value?.estimatedGameDaysRemaining ?? null,
  )
  if (trend === 'empty') {
    return t('mining.extractionSummaryEmpty')
  }
  return t('mining.extractionSummaryTrend', {
    trend: t(`mining.trend.${trend}`),
    days: Math.ceil(intelligence.value?.estimatedGameDaysRemaining ?? 0),
  })
})
</script>

<template>
  <div class="mine-extraction-history-panel mt-3 rounded-lg border border-divider bg-card px-4 pb-4 pt-3">
    <div class="mb-2 flex items-center justify-between">
      <h5 class="text-xs font-semibold uppercase tracking-wide text-muted">
        <span aria-hidden="true">⛏️</span> {{ t('mining.extractionIntelligenceTitle') }}
      </h5>
      <span v-if="!loading && dailyTotals.length > 0" class="text-xs text-muted">
        {{ t('mining.extractionHistoryDays', { days: Math.min(dailyTotals.length, 30) }) }}
      </span>
    </div>

    <!-- Loading skeleton -->
    <div v-if="loading" class="flex h-20 items-center justify-center">
      <span class="text-xs text-muted">{{ t('common.loading') }}</span>
    </div>

    <!-- Empty state (fewer than 5 ticks) -->
    <div v-else-if="!hasSufficientData" class="extraction-history-empty flex h-20 items-center justify-center text-center">
      <p class="text-xs text-muted">{{ t('mining.extractionHistoryEmpty') }}</p>
    </div>

    <!-- Sparkline chart -->
    <div v-else class="sparkline-container">
      <svg
        :width="SPARK_W"
        :height="SPARK_H"
        :viewBox="`0 0 ${SPARK_W} ${SPARK_H}`"
        class="sparkline-svg w-full"
        role="img"
        :aria-label="sparklineSummary"
        preserveAspectRatio="none"
      >
        <!-- Filled area -->
        <path
          v-if="sparklineFill"
          :d="sparklineFill"
          :fill="sparklineColor"
          fill-opacity="0.15"
        />
        <!-- Line -->
        <path
          v-if="sparklinePath"
          :d="sparklinePath"
          :stroke="sparklineColor"
          stroke-width="2"
          fill="none"
          stroke-linecap="round"
          stroke-linejoin="round"
        />
        <path
          v-if="depletionTrendlinePath"
          :d="depletionTrendlinePath"
          stroke="var(--color-warning)"
          stroke-width="2"
          fill="none"
          stroke-dasharray="4 4"
          stroke-linecap="round"
        />
      </svg>

      <!-- Colour legend dots -->
      <div class="mt-1 flex gap-3 text-xs text-muted">
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
      <p class="sr-only">{{ sparklineSummary }}</p>
    </div>

    <!-- View full history button -->
    <button
      v-if="!loading"
      class="view-extraction-history-btn mt-3 w-full rounded-md border border-accent px-3 py-1.5 text-xs font-medium text-accent transition-colors hover:bg-accent hover:text-white"
      @click="showDialog = true"
    >
      {{ t('mining.viewExtractionDetails') }}
    </button>

    <!-- History dialog -->
    <MineExtractionHistoryDialog
      v-if="showDialog"
      :building="building"
      :intelligence="intelligence"
      :loading="loading"
      @close="showDialog = false"
    />
  </div>
</template>

<style scoped>
.sparkline-svg {
  display: block;
  height: 80px;
}

@media (max-width: 640px) {
  .sparkline-svg {
    height: 60px;
  }
}
</style>

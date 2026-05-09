<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import type { Building } from '@/types'
import MineExtractionHistoryDialog from './MineExtractionHistoryDialog.vue'

const { t } = useI18n()

const props = defineProps<{
  building: Building
}>()

// ── Types ────────────────────────────────────────────────────────────────────

interface ExtractionRecord {
  tick: number
  extractedAmount: number
  efficiencyPercent: number
  reserveRemaining: number
}

// ── State ────────────────────────────────────────────────────────────────────

const records = ref<ExtractionRecord[]>([])
const loading = ref(false)
const showDialog = ref(false)

// ── Data loading ─────────────────────────────────────────────────────────────

async function loadHistory() {
  loading.value = true
  try {
    const result = await gqlRequest<{ getMineExtractionHistory: ExtractionRecord[] }>(`
      query GetMineExtractionHistory($buildingId: UUID!, $days: Int!) {
        getMineExtractionHistory(buildingId: $buildingId, days: $days) {
          tick
          extractedAmount
          efficiencyPercent
          reserveRemaining
        }
      }
    `, { buildingId: props.building.id, days: 30 })
    records.value = result.getMineExtractionHistory ?? []
  } catch {
    records.value = []
  } finally {
    loading.value = false
  }
}

watch(() => props.building.id, () => { void loadHistory() }, { immediate: true })

// ── Sparkline computations ────────────────────────────────────────────────────

/** Aggregate per-day totals (last 30 days, ascending order) */
const dailyTotals = computed<number[]>(() => {
  if (records.value.length === 0) return []

  // Group by game-day (tick / 24 = day index)
  const dayMap = new Map<number, number>()
  for (const r of records.value) {
    const day = Math.floor(r.tick / 24)
    dayMap.set(day, (dayMap.get(day) ?? 0) + r.extractedAmount)
  }

  // Sort by day ascending and return amounts
  const sorted = [...dayMap.entries()].sort((a, b) => a[0] - b[0])
  return sorted.map(([, amount]) => amount)
})

const hasSufficientData = computed(() => dailyTotals.value.length >= 5)

const maxDaily = computed(() => {
  const vals = dailyTotals.value
  return vals.length ? Math.max(...vals, 0.001) : 1
})

/** Reserve percent from the most recent record */
const currentReservePercent = computed<number | null>(() => {
  const r = records.value[0]
  if (!r) return null
  const original = props.building.lotOriginalMaterialQuantity ?? null
  if (!original || original <= 0) return null
  return Math.round((r.reserveRemaining / original) * 100)
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

const sparklinePath = computed(() => {
  const data = dailyTotals.value
  if (data.length < 2) return ''

  const max = maxDaily.value
  const step = SPARK_W / (data.length - 1)

  return data.map((v, i) => {
    const x = i * step
    const y = SPARK_H - (v / max) * SPARK_H
    return `${i === 0 ? 'M' : 'L'} ${x.toFixed(1)} ${y.toFixed(1)}`
  }).join(' ')
})

const sparklineFill = computed(() => {
  const data = dailyTotals.value
  if (data.length < 2) return ''
  return `${sparklinePath.value} L ${SPARK_W} ${SPARK_H} L 0 ${SPARK_H} Z`
})
</script>

<template>
  <div class="mine-extraction-history-panel mt-3 rounded-lg border border-divider bg-card px-4 pb-4 pt-3">
    <div class="mb-2 flex items-center justify-between">
      <h5 class="text-xs font-semibold uppercase tracking-wide text-muted">
        {{ t('mining.extractionHistoryTitle') }}
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
        :aria-label="t('mining.extractionHistoryTitle')"
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
    </div>

    <!-- View full history button -->
    <button
      v-if="!loading"
      class="view-extraction-history-btn mt-3 w-full rounded-md border border-accent px-3 py-1.5 text-xs font-medium text-accent transition-colors hover:bg-accent hover:text-white"
      @click="showDialog = true"
    >
      {{ t('mining.viewExtractionHistory') }}
    </button>

    <!-- History dialog -->
    <MineExtractionHistoryDialog
      v-if="showDialog"
      :building="building"
      :initial-records="records"
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

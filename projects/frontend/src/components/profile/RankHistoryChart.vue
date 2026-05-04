<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

export interface RankSnapshot {
  snapshotTick: number
  snapshotUtc?: string
  leaderboardRank: number
  wealthUsd: number
  percentileRank: number
  positionChange?: number | null
}

const props = defineProps<{
  snapshots: RankSnapshot[]
  loading?: boolean
}>()

// ── Reactive state ──────────────────────────────────────────────────────────

type TimeFilter = 30 | 90 | 365
const selectedFilter = ref<TimeFilter>(365)

const tooltip = ref<{
  visible: boolean
  x: number
  y: number
  snapshot: RankSnapshot | null
}>({ visible: false, x: 0, y: 0, snapshot: null })

// ── Chart dimensions ────────────────────────────────────────────────────────

const CHART_MARGIN = { top: 20, right: 20, bottom: 48, left: 52 }
const chartWidth = ref(600)
const chartHeight = 280
const svgRef = ref<SVGSVGElement | null>(null)
const containerRef = ref<HTMLDivElement | null>(null)

let resizeObserver: ResizeObserver | null = null

onMounted(() => {
  if (containerRef.value) {
    resizeObserver = new ResizeObserver((entries) => {
      const entry = entries[0]
      if (entry) {
        chartWidth.value = Math.max(320, entry.contentRect.width)
      }
    })
    resizeObserver.observe(containerRef.value)
  }
})

onBeforeUnmount(() => {
  resizeObserver?.disconnect()
})

// ── Filtered data ───────────────────────────────────────────────────────────

const filteredSnapshots = computed<RankSnapshot[]>(() => {
  const all = [...props.snapshots].sort((a, b) => a.snapshotTick - b.snapshotTick)
  if (all.length === 0) return []
  const maxTick = all[all.length - 1]!.snapshotTick
  const minTick = maxTick - selectedFilter.value * 144 // 144 ticks ≈ 1 game day
  return all.filter((s) => s.snapshotTick >= minTick)
})

// ── Summary stats ───────────────────────────────────────────────────────────

const summaryStats = computed(() => {
  const snaps = props.snapshots
  if (snaps.length === 0) {
    return { bestRank: null, currentRank: null, avgPercentile: null, volatility: null }
  }
  const sorted = [...snaps].sort((a, b) => a.snapshotTick - b.snapshotTick)
  const bestRank = Math.min(...snaps.map((s) => s.leaderboardRank))
  const currentRank = sorted[sorted.length - 1]!.leaderboardRank
  const avgPercentile =
    snaps.reduce((sum, s) => sum + s.percentileRank, 0) / snaps.length

  // Rank volatility = std dev of rank changes
  const ranks = snaps.map((s) => s.leaderboardRank)
  const mean = ranks.reduce((a, b) => a + b, 0) / ranks.length
  const variance = ranks.reduce((sum, r) => sum + Math.pow(r - mean, 2), 0) / ranks.length
  const volatility = Math.sqrt(variance)

  return { bestRank, currentRank, avgPercentile, volatility }
})

// ── SVG chart computations ──────────────────────────────────────────────────

const innerWidth = computed(
  () => chartWidth.value - CHART_MARGIN.left - CHART_MARGIN.right,
)
const innerHeight = computed(() => chartHeight - CHART_MARGIN.top - CHART_MARGIN.bottom)

const scaleX = computed(() => {
  const snaps = filteredSnapshots.value
  if (snaps.length < 2) return () => 0
  const minT = snaps[0]!.snapshotTick
  const maxT = snaps[snaps.length - 1]!.snapshotTick
  const range = maxT - minT || 1
  return (tick: number) => ((tick - minT) / range) * innerWidth.value
})

// Y scale: rank 1 maps to top (0), higher rank → lower on chart
const scaleY = computed(() => {
  const snaps = filteredSnapshots.value
  if (snaps.length === 0) return () => innerHeight.value / 2
  const maxRank = Math.max(...snaps.map((s) => s.leaderboardRank))
  const minRank = Math.min(...snaps.map((s) => s.leaderboardRank))
  const range = maxRank - minRank || 1
  return (rank: number) =>
    ((rank - minRank) / range) * (innerHeight.value * 0.85) + innerHeight.value * 0.075
})

const linePath = computed(() => {
  const snaps = filteredSnapshots.value
  if (snaps.length === 0) return ''
  return snaps
    .map((s, i) => `${i === 0 ? 'M' : 'L'}${scaleX.value(s.snapshotTick)},${scaleY.value(s.leaderboardRank)}`)
    .join(' ')
})

const areaPath = computed(() => {
  const snaps = filteredSnapshots.value
  if (snaps.length === 0) return ''
  const line = linePath.value
  const lastSnap = snaps[snaps.length - 1]!
  const firstSnap = snaps[0]!
  return `${line} L${scaleX.value(lastSnap.snapshotTick)},${innerHeight.value} L${scaleX.value(firstSnap.snapshotTick)},${innerHeight.value} Z`
})

// X-axis ticks (up to 6 labels)
const xAxisTicks = computed(() => {
  const snaps = filteredSnapshots.value
  if (snaps.length === 0) return []
  const total = snaps.length
  const step = Math.max(1, Math.floor(total / 5))
  const result: { x: number; label: string }[] = []
  for (let i = 0; i < total; i += step) {
    const snap = snaps[i]!
    result.push({
      x: scaleX.value(snap.snapshotTick),
      label: snap.snapshotUtc
        ? new Date(snap.snapshotUtc).toLocaleDateString(undefined, {
            month: 'short',
            day: 'numeric',
          })
        : `T${snap.snapshotTick.toLocaleString()}`,
    })
  }
  return result
})

// Y-axis ticks (rank labels)
const yAxisTicks = computed(() => {
  const snaps = filteredSnapshots.value
  if (snaps.length === 0) return []
  const ranks = snaps.map((s) => s.leaderboardRank)
  const minRank = Math.min(...ranks)
  const maxRank = Math.max(...ranks)
  const range = maxRank - minRank
  const step = Math.max(1, Math.ceil(range / 4))
  const ticks: { y: number; label: string }[] = []
  for (let r = minRank; r <= maxRank; r += step) {
    ticks.push({ y: scaleY.value(r), label: `#${r}` })
  }
  return ticks
})

// ── Tooltip interaction ──────────────────────────────────────────────────────

function onMouseMove(event: MouseEvent) {
  if (!svgRef.value || filteredSnapshots.value.length === 0) return
  const rect = svgRef.value.getBoundingClientRect()
  const mouseX = event.clientX - rect.left - CHART_MARGIN.left
  // Find closest data point.
  let closest: RankSnapshot | null = null
  let closestDist = Infinity
  for (const snap of filteredSnapshots.value) {
    const d = Math.abs(scaleX.value(snap.snapshotTick) - mouseX)
    if (d < closestDist) {
      closestDist = d
      closest = snap
    }
  }
  if (closest && closestDist < 30) {
    tooltip.value = {
      visible: true,
      x: scaleX.value(closest.snapshotTick) + CHART_MARGIN.left + 8,
      y: scaleY.value(closest.leaderboardRank) + CHART_MARGIN.top - 10,
      snapshot: closest,
    }
  } else {
    tooltip.value.visible = false
  }
}

function onMouseLeave() {
  tooltip.value.visible = false
}

// ── Helpers ──────────────────────────────────────────────────────────────────

function formatWealth(usd: number): string {
  if (usd >= 1_000_000) return `$${(usd / 1_000_000).toFixed(2)}M`
  if (usd >= 1_000) return `$${(usd / 1_000).toFixed(1)}K`
  return `$${usd.toFixed(0)}`
}

const filters: { label: string; value: TimeFilter }[] = [
  { label: '30d', value: 30 },
  { label: '90d', value: 90 },
  { label: '365d', value: 365 },
]
</script>

<template>
  <div class="rank-chart-container" ref="containerRef">
    <!-- Summary KPI boxes -->
    <div class="rank-summary-grid">
      <div class="rank-kpi-card">
        <span class="rank-kpi-value">{{
          summaryStats.bestRank !== null ? `#${summaryStats.bestRank}` : '—'
        }}</span>
        <span class="rank-kpi-label">{{ t('playerProfile.bestRank') }}</span>
      </div>
      <div class="rank-kpi-card">
        <span class="rank-kpi-value">{{
          summaryStats.currentRank !== null ? `#${summaryStats.currentRank}` : '—'
        }}</span>
        <span class="rank-kpi-label">{{ t('playerProfile.currentRank') }}</span>
      </div>
      <div class="rank-kpi-card">
        <span class="rank-kpi-value">{{
          summaryStats.avgPercentile !== null
            ? `${summaryStats.avgPercentile.toFixed(1)}%`
            : '—'
        }}</span>
        <span class="rank-kpi-label">{{ t('playerProfile.avgPercentile') }}</span>
      </div>
      <div class="rank-kpi-card">
        <span class="rank-kpi-value">{{
          summaryStats.volatility !== null ? summaryStats.volatility.toFixed(1) : '—'
        }}</span>
        <span class="rank-kpi-label">{{ t('playerProfile.rankVolatility') }}</span>
      </div>
    </div>

    <!-- Time filter buttons -->
    <div class="rank-filter-row" role="group" :aria-label="t('playerProfile.timeFilter')">
      <button
        v-for="f in filters"
        :key="f.value"
        class="rank-filter-btn"
        :class="{ active: selectedFilter === f.value }"
        @click="selectedFilter = f.value"
      >
        {{ f.label }}
      </button>
    </div>

    <!-- Loading state -->
    <div v-if="loading" class="rank-chart-skeleton" aria-busy="true" />

    <!-- Empty state -->
    <div v-else-if="filteredSnapshots.length === 0" class="rank-chart-empty">
      <span aria-hidden="true">📊</span>
      <p>{{ t('playerProfile.noRankHistory') }}</p>
    </div>

    <!-- SVG Chart -->
    <div v-else class="rank-chart-svg-wrapper">
      <svg
        ref="svgRef"
        :width="chartWidth"
        :height="chartHeight"
        :viewBox="`0 0 ${chartWidth} ${chartHeight}`"
        class="rank-chart-svg"
        role="img"
        :aria-label="t('playerProfile.rankHistoryChartLabel')"
        @mousemove="onMouseMove"
        @mouseleave="onMouseLeave"
      >
        <defs>
          <linearGradient id="rank-area-gradient" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stop-color="#3b82f6" stop-opacity="0.3" />
            <stop offset="100%" stop-color="#3b82f6" stop-opacity="0.02" />
          </linearGradient>
        </defs>

        <!-- Chart area (translated to inner area) -->
        <g :transform="`translate(${CHART_MARGIN.left},${CHART_MARGIN.top})`">
          <!-- Grid lines -->
          <line
            v-for="tick in yAxisTicks"
            :key="tick.label"
            :x1="0"
            :y1="tick.y"
            :x2="innerWidth"
            :y2="tick.y"
            stroke="#334155"
            stroke-dasharray="4 4"
            stroke-width="1"
          />

          <!-- Area fill -->
          <path :d="areaPath" fill="url(#rank-area-gradient)" />

          <!-- Line -->
          <path
            :d="linePath"
            fill="none"
            stroke="#3b82f6"
            stroke-width="2.5"
            stroke-linecap="round"
            stroke-linejoin="round"
          />

          <!-- Data points -->
          <circle
            v-for="(snap, i) in filteredSnapshots"
            :key="i"
            :cx="scaleX(snap.snapshotTick)"
            :cy="scaleY(snap.leaderboardRank)"
            r="3"
            fill="#3b82f6"
            stroke="#0f172a"
            stroke-width="1.5"
          />

          <!-- X-axis labels -->
          <g :transform="`translate(0,${innerHeight + 16})`">
            <text
              v-for="tick in xAxisTicks"
              :key="tick.label"
              :x="tick.x"
              y="0"
              text-anchor="middle"
              font-size="10"
              fill="#64748b"
            >
              {{ tick.label }}
            </text>
          </g>

          <!-- Y-axis labels -->
          <g>
            <text
              v-for="tick in yAxisTicks"
              :key="tick.label"
              :x="-8"
              :y="tick.y + 4"
              text-anchor="end"
              font-size="10"
              fill="#64748b"
            >
              {{ tick.label }}
            </text>
          </g>

          <!-- Y-axis label -->
          <text
            :x="-(innerHeight / 2)"
            y="-38"
            text-anchor="middle"
            font-size="10"
            fill="#64748b"
            transform="rotate(-90)"
          >
            {{ t('playerProfile.chartYAxis') }}
          </text>

          <!-- Tooltip vertical line -->
          <line
            v-if="tooltip.visible && tooltip.snapshot"
            :x1="scaleX(tooltip.snapshot.snapshotTick)"
            :y1="0"
            :x2="scaleX(tooltip.snapshot.snapshotTick)"
            :y2="innerHeight"
            stroke="#60a5fa"
            stroke-width="1"
            stroke-dasharray="4 2"
            opacity="0.7"
          />
        </g>
      </svg>

      <!-- Tooltip overlay -->
      <div
        v-if="tooltip.visible && tooltip.snapshot"
        class="rank-tooltip"
        :style="{ left: `${tooltip.x}px`, top: `${tooltip.y}px` }"
        role="tooltip"
      >
        <div class="rank-tooltip-rank">#{{ tooltip.snapshot.leaderboardRank }}</div>
        <div class="rank-tooltip-wealth">{{ formatWealth(tooltip.snapshot.wealthUsd) }}</div>
        <div class="rank-tooltip-pct">{{ tooltip.snapshot.percentileRank.toFixed(1) }}%</div>
        <div v-if="tooltip.snapshot.positionChange !== null" class="rank-tooltip-change">
          <span
            v-if="tooltip.snapshot.positionChange && tooltip.snapshot.positionChange > 0"
            class="rank-change-up"
          >▲ {{ tooltip.snapshot.positionChange }}</span>
          <span
            v-else-if="tooltip.snapshot.positionChange && tooltip.snapshot.positionChange < 0"
            class="rank-change-down"
          >▼ {{ Math.abs(tooltip.snapshot.positionChange!) }}</span>
          <span v-else class="rank-change-flat">= 0</span>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.rank-chart-container {
  width: 100%;
}

/* Summary KPI grid */
.rank-summary-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 10px;
  margin-bottom: 16px;
}

@media (max-width: 640px) {
  .rank-summary-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

.rank-kpi-card {
  background: var(--color-surface-card, #1e293b);
  border: 1px solid var(--color-border, #334155);
  border-radius: 10px;
  padding: 12px 10px;
  text-align: center;
}

.rank-kpi-value {
  display: block;
  font-size: 22px;
  font-weight: 700;
  color: #3b82f6;
  line-height: 1.2;
}

.rank-kpi-label {
  display: block;
  font-size: 11px;
  color: var(--color-text-muted, #94a3b8);
  text-transform: uppercase;
  letter-spacing: 0.05em;
  margin-top: 2px;
}

/* Filter buttons */
.rank-filter-row {
  display: flex;
  gap: 8px;
  margin-bottom: 12px;
}

.rank-filter-btn {
  padding: 5px 14px;
  border-radius: 20px;
  border: 1px solid var(--color-border, #334155);
  background: transparent;
  color: var(--color-text-muted, #94a3b8);
  font-size: 12px;
  cursor: pointer;
  transition: all 0.15s ease;
}

.rank-filter-btn:hover {
  border-color: #3b82f6;
  color: #3b82f6;
}

.rank-filter-btn.active {
  background: #1d4ed8;
  border-color: #3b82f6;
  color: #f0f9ff;
}

/* Chart */
.rank-chart-svg-wrapper {
  position: relative;
  width: 100%;
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
}

.rank-chart-svg {
  display: block;
  min-width: 320px;
}

.rank-chart-skeleton {
  height: 280px;
  border-radius: 8px;
  background: linear-gradient(90deg, #1e293b 25%, #2d3f5e 50%, #1e293b 75%);
  background-size: 200% 100%;
  animation: shimmer 1.5s infinite;
}

@keyframes shimmer {
  0% {
    background-position: 200% 0;
  }
  100% {
    background-position: -200% 0;
  }
}

.rank-chart-empty {
  height: 280px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  border: 1px dashed var(--color-border, #334155);
  border-radius: 8px;
  color: var(--color-text-muted, #94a3b8);
  font-size: 14px;
}

.rank-chart-empty span {
  font-size: 28px;
  opacity: 0.5;
}

/* Tooltip */
.rank-tooltip {
  position: absolute;
  background: var(--color-surface-elevated, #0f172a);
  border: 1px solid #334155;
  border-radius: 8px;
  padding: 8px 12px;
  pointer-events: none;
  z-index: 50;
  min-width: 90px;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.5);
}

.rank-tooltip-rank {
  font-size: 18px;
  font-weight: 700;
  color: #3b82f6;
}

.rank-tooltip-wealth {
  font-size: 12px;
  color: #94a3b8;
}

.rank-tooltip-pct {
  font-size: 11px;
  color: #64748b;
}

.rank-tooltip-change {
  font-size: 11px;
  margin-top: 2px;
}

.rank-change-up {
  color: #22c55e;
}

.rank-change-down {
  color: #ef4444;
}

.rank-change-flat {
  color: #64748b;
}
</style>

<script setup lang="ts">
import { computed, ref } from 'vue'
import type { FxRateSnapshot } from '@/types'
import {
  snapshotsToPoints,
  computeBounds,
  buildChartPaths,
  buildYAxisLabels,
  nearestPointToX,
  pointToX,
  DEFAULT_DIMENSIONS,
} from '@/lib/fxRateChart'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

const props = defineProps<{
  snapshots: FxRateSnapshot[]
  pairLabel: string
}>()

const dims = DEFAULT_DIMENSIONS

const points = computed(() => snapshotsToPoints(props.snapshots))
const bounds = computed(() => computeBounds(points.value))
const paths = computed(() =>
  bounds.value ? buildChartPaths(points.value, bounds.value, dims) : null
)
const yLabels = computed(() =>
  bounds.value ? buildYAxisLabels(bounds.value, dims) : []
)

// ── Hover tooltip ────────────────────────────────────────────────────────────
const hoverX = ref<number | null>(null)
const hoveredPoint = computed(() => {
  if (hoverX.value === null || !bounds.value) return null
  return nearestPointToX(hoverX.value, points.value, bounds.value, dims)
})
const tooltipX = computed(() => {
  if (!hoveredPoint.value || !bounds.value) return 0
  const x = pointToX(hoveredPoint.value, bounds.value, dims)
  // Clamp so tooltip stays inside the chart.
  return Math.max(dims.paddingLeft, Math.min(x, dims.width - 80))
})

function onMouseMove(event: MouseEvent) {
  const svg = event.currentTarget as SVGElement
  const rect = svg.getBoundingClientRect()
  hoverX.value = ((event.clientX - rect.left) / rect.width) * dims.width
}

function onMouseLeave() {
  hoverX.value = null
}

function fmt(v: number) {
  return v.toFixed(4)
}
</script>

<template>
  <div class="fx-rate-chart" role="img" :aria-label="`${pairLabel} ${t('forex.rateChart')}`">
    <div v-if="!snapshots.length" class="chart-empty">
      {{ t('forex.rateChartEmpty') }}
    </div>
    <div v-else class="chart-wrapper">
      <!-- Legend -->
      <div class="chart-legend">
        <span class="legend-buy">
          <span class="legend-dot buy-dot" />{{ t('forex.rateBuy') }}
        </span>
        <span class="legend-mid">
          <span class="legend-dot mid-dot" />{{ t('forex.rateMid') }}
        </span>
        <span class="legend-sell">
          <span class="legend-dot sell-dot" />{{ t('forex.rateSell') }}
        </span>
      </div>
      <!-- SVG Chart -->
      <svg
        :viewBox="`0 0 ${dims.width} ${dims.height}`"
        :width="dims.width"
        :height="dims.height"
        class="chart-svg"
        preserveAspectRatio="xMidYMid meet"
        @mousemove="onMouseMove"
        @mouseleave="onMouseLeave"
      >
        <!-- Y-axis labels -->
        <g class="y-axis" aria-hidden="true">
          <line
            :x1="dims.paddingLeft"
            :y1="dims.paddingTop"
            :x2="dims.paddingLeft"
            :y2="dims.height - dims.paddingBottom"
            stroke="var(--color-border)"
            stroke-width="1"
          />
          <g v-for="label in yLabels" :key="label.label">
            <line
              :x1="dims.paddingLeft - 4"
              :y1="label.y"
              :x2="dims.width - dims.paddingRight"
              :y2="label.y"
              stroke="var(--color-border)"
              stroke-width="0.5"
              stroke-dasharray="2,4"
            />
            <text
              :x="dims.paddingLeft - 6"
              :y="label.y + 4"
              text-anchor="end"
              font-size="9"
              fill="var(--color-text-secondary)"
            >{{ label.label }}</text>
          </g>
        </g>

        <!-- Chart lines -->
        <g v-if="paths">
          <!-- Sell line (red) -->
          <polyline
            class="line-sell"
            :points="paths.sellPoints"
            fill="none"
            stroke="var(--color-danger, #ef4444)"
            stroke-width="1.5"
            stroke-linejoin="round"
            stroke-linecap="round"
          />
          <!-- Mid line (blue) -->
          <polyline
            class="line-mid"
            :points="paths.midPoints"
            fill="none"
            stroke="var(--color-info, #3b82f6)"
            stroke-width="2"
            stroke-linejoin="round"
            stroke-linecap="round"
          />
          <!-- Buy line (green) -->
          <polyline
            class="line-buy"
            :points="paths.buyPoints"
            fill="none"
            stroke="var(--color-success, #22c55e)"
            stroke-width="1.5"
            stroke-linejoin="round"
            stroke-linecap="round"
          />
        </g>

        <!-- Hover crosshair + tooltip -->
        <g v-if="hoveredPoint && bounds" aria-hidden="true">
          <line
            :x1="tooltipX"
            :y1="dims.paddingTop"
            :x2="tooltipX"
            :y2="dims.height - dims.paddingBottom"
            stroke="var(--color-text-secondary)"
            stroke-width="1"
            stroke-dasharray="3,3"
            opacity="0.6"
          />
          <!-- Tooltip background -->
          <rect
            :x="tooltipX + 4"
            :y="dims.paddingTop"
            width="78"
            height="60"
            rx="3"
            fill="var(--color-card)"
            stroke="var(--color-border)"
            stroke-width="1"
            opacity="0.95"
          />
          <text :x="tooltipX + 8" :y="dims.paddingTop + 12" font-size="9" fill="var(--color-text-secondary)">
            Tick {{ hoveredPoint.tick }}
          </text>
          <text :x="tooltipX + 8" :y="dims.paddingTop + 24" font-size="9" fill="var(--color-success, #22c55e)">
            B: {{ fmt(hoveredPoint.buy) }}
          </text>
          <text :x="tooltipX + 8" :y="dims.paddingTop + 36" font-size="9" fill="var(--color-info, #3b82f6)">
            M: {{ fmt(hoveredPoint.mid) }}
          </text>
          <text :x="tooltipX + 8" :y="dims.paddingTop + 48" font-size="9" fill="var(--color-danger, #ef4444)">
            S: {{ fmt(hoveredPoint.sell) }}
          </text>
        </g>
      </svg>
    </div>
  </div>
</template>

<style scoped>
.fx-rate-chart {
  width: 100%;
}

.chart-wrapper {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.chart-legend {
  display: flex;
  gap: 1rem;
  font-size: 0.75rem;
  color: var(--color-text-secondary);
  flex-wrap: wrap;
}

.legend-dot {
  display: inline-block;
  width: 10px;
  height: 10px;
  border-radius: 50%;
  margin-right: 4px;
  vertical-align: middle;
}

.buy-dot { background: var(--color-success, #22c55e); }
.mid-dot { background: var(--color-info, #3b82f6); }
.sell-dot { background: var(--color-danger, #ef4444); }

.legend-buy,
.legend-mid,
.legend-sell {
  display: flex;
  align-items: center;
}

.chart-svg {
  width: 100%;
  height: auto;
  cursor: crosshair;
  display: block;
}

.chart-empty {
  padding: 2rem;
  text-align: center;
  color: var(--color-text-secondary);
  font-size: 0.875rem;
}
</style>

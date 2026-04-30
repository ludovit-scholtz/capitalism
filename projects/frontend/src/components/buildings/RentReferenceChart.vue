<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

const props = defineProps<{
  cityReferenceRentPerSqm: number
  adjustedMarketRentPerSqm: number
  currentRentPerSqm: number | null
  buildingType: 'APARTMENT' | 'COMMERCIAL'
  formatCurrency: (v: number) => string
}>()

const { t } = useI18n()

// ── SVG layout constants ────────────────────────────────────────────────────
const SVG_W = 400
const SVG_H = 140
const PLOT_X = 42 // left margin (y-axis labels)
const PLOT_Y = 18 // top margin (room for reference-rate labels above chart)
const PLOT_W = 340 // plot width
const PLOT_H = 100 // plot height
const PLOT_X2 = PLOT_X + PLOT_W // right edge
const PLOT_Y2 = PLOT_Y + PLOT_H // bottom edge

// ── X-axis: ratio 0 to MAX_RATIO relative to adjustedMarketRate ─────────────
const MAX_RATIO = 1.5

function xFor(ratio: number): number {
  return PLOT_X + Math.min(1, Math.max(0, ratio / MAX_RATIO)) * PLOT_W
}

// ── Y-axis: occupancy % ──────────────────────────────────────────────────────
function yFor(pct: number): number {
  return PLOT_Y2 - (pct / 100) * PLOT_H
}

// ── Occupancy-curve zone boundaries (ratios of adjusted market rate) ─────────
const ZONE_FULL_CAP = 0.6 // below this: 100% occupancy
const ZONE_NINETY_CAP = 1.1 // above this: drops to 50%
const BREAKEVEN_PCT = 75 // 75% occupancy = property breaks even

// ── Computed SVG x-coordinates for fixed zone boundaries ────────────────────
const xZoneFull = xFor(ZONE_FULL_CAP)
const xZoneNinety = xFor(ZONE_NINETY_CAP)
const xAdjustedRateFixed = xFor(1.0) // always at ratio 1.0

// ── Occupancy curve path (piecewise linear, mirrors RentPhase.ComputeMaxOccupancy) ─
//   0 → 0.6:  100% (flat)
//   0.6 → 1.1: linear 100% → 90%
//   1.1 → 1.5: 50% (flat, with a vertical drop at 1.1)
const curve = (() => {
  const y100 = yFor(100)
  const y90 = yFor(90)
  const y50 = yFor(50)
  return `M ${PLOT_X} ${y100} L ${xZoneFull} ${y100} L ${xZoneNinety} ${y90} L ${xZoneNinety} ${y50} L ${PLOT_X2} ${y50}`
})()

// Area above the breakeven line, under the occupancy curve (profitable zone)
const curveProfitFill = (() => {
  const y100 = yFor(100)
  const y90 = yFor(90)
  const yBreakeven = yFor(BREAKEVEN_PCT)
  return (
    `M ${PLOT_X} ${y100} ` +
    `L ${xZoneFull} ${y100} ` +
    `L ${xZoneNinety} ${y90} ` +
    `L ${xZoneNinety} ${yBreakeven} ` +
    `L ${PLOT_X} ${yBreakeven} Z`
  )
})()

// ── Reactive positions for data-driven markers ───────────────────────────────
const cityRateRatio = computed(() => {
  if (props.adjustedMarketRentPerSqm <= 0) return 1
  return props.cityReferenceRentPerSqm / props.adjustedMarketRentPerSqm
})

const currentRentRatio = computed(() => {
  if (props.currentRentPerSqm == null || props.adjustedMarketRentPerSqm <= 0) return null
  return props.currentRentPerSqm / props.adjustedMarketRentPerSqm
})

const xCityRate = computed(() => xFor(cityRateRatio.value))
const xCurrentRent = computed(() =>
  currentRentRatio.value !== null ? xFor(currentRentRatio.value) : null,
)

// Current-rent marker color tracks the price zone
const currentRentMarkerColor = computed(() => {
  const r = currentRentRatio.value
  if (r === null) return '#f59e0b'
  if (r > ZONE_NINETY_CAP) return '#ef4444'
  if (r > 1.0) return '#f59e0b'
  if (r >= ZONE_FULL_CAP) return '#3b82f6'
  return '#16a34a'
})

// ── Chart title varies by building type ─────────────────────────────────────
const chartTitle = computed(() =>
  props.buildingType === 'APARTMENT'
    ? t('property.rentChartTitleApartment')
    : t('property.rentChartTitleCommercial'),
)

// Whether city-rate and adjusted-rate markers are close enough to share a label
const markersOverlap = computed(() => Math.abs(xCityRate.value - xAdjustedRateFixed) < 24)

// City-rate label anchor: prefer showing it to the left of the marker, unless too close to edge
const cityRateLabelAnchor = computed(() =>
  xCityRate.value < PLOT_X + 30 ? 'start' : 'end',
)
</script>
<template>
  <div
    class="rent-reference-chart mt-5 rounded-xl border border-divider bg-surface p-4"
    role="region"
    :aria-label="chartTitle"
  >
    <h3 class="rent-chart-title mb-3 text-sm font-semibold uppercase tracking-wide text-muted">
      {{ chartTitle }}
    </h3>

    <svg
      :viewBox="`0 0 ${SVG_W} ${SVG_H}`"
      width="100%"
      class="rent-chart-svg block"
      role="img"
      :aria-label="chartTitle"
    >
      <!-- ── Zone background bands ───────────────────────────────────────── -->
      <!-- Very Attractive (< 60 % of market) -->
      <rect
        :x="PLOT_X"
        :y="PLOT_Y"
        :width="xZoneFull - PLOT_X"
        :height="PLOT_H"
        fill="rgba(22,163,74,0.09)"
      />
      <!-- Good (60 %–100 %) -->
      <rect
        :x="xZoneFull"
        :y="PLOT_Y"
        :width="xAdjustedRateFixed - xZoneFull"
        :height="PLOT_H"
        fill="rgba(59,130,246,0.08)"
      />
      <!-- Above Market (100 %–110 %) -->
      <rect
        :x="xAdjustedRateFixed"
        :y="PLOT_Y"
        :width="xZoneNinety - xAdjustedRateFixed"
        :height="PLOT_H"
        fill="rgba(245,158,11,0.09)"
      />
      <!-- Overpriced (> 110 %) -->
      <rect
        :x="xZoneNinety"
        :y="PLOT_Y"
        :width="PLOT_X2 - xZoneNinety"
        :height="PLOT_H"
        fill="rgba(239,68,68,0.09)"
      />

      <!-- ── Profitable fill above breakeven line ────────────────────────── -->
      <path :d="curveProfitFill" fill="rgba(22,163,74,0.10)" />

      <!-- ── Breakeven dashed line (75 % occupancy) ─────────────────────── -->
      <line
        :x1="PLOT_X"
        :y1="yFor(BREAKEVEN_PCT)"
        :x2="PLOT_X2"
        :y2="yFor(BREAKEVEN_PCT)"
        stroke="#9ca3af"
        stroke-width="1"
        stroke-dasharray="3,3"
      />
      <text
        :x="PLOT_X2 + 2"
        :y="yFor(BREAKEVEN_PCT)"
        font-size="8"
        fill="#9ca3af"
        dominant-baseline="middle"
      >
        75%
      </text>

      <!-- ── Occupancy curve ──────────────────────────────────────────────── -->
      <path :d="curve" fill="none" stroke="#22c55e" stroke-width="2" stroke-linejoin="round" />

      <!-- ── Plot-area border ────────────────────────────────────────────── -->
      <rect
        :x="PLOT_X"
        :y="PLOT_Y"
        :width="PLOT_W"
        :height="PLOT_H"
        fill="none"
        stroke="rgba(156,163,175,0.25)"
        stroke-width="1"
      />

      <!-- ── Y-axis labels (occupancy %) ────────────────────────────────── -->
      <text
        :x="PLOT_X - 3"
        :y="yFor(100)"
        text-anchor="end"
        font-size="9"
        fill="currentColor"
        opacity="0.55"
        dominant-baseline="middle"
      >
        100%
      </text>
      <text
        :x="PLOT_X - 3"
        :y="yFor(90)"
        text-anchor="end"
        font-size="9"
        fill="currentColor"
        opacity="0.55"
        dominant-baseline="middle"
      >
        90%
      </text>
      <text
        :x="PLOT_X - 3"
        :y="yFor(50)"
        text-anchor="end"
        font-size="9"
        fill="currentColor"
        opacity="0.55"
        dominant-baseline="middle"
      >
        50%
      </text>

      <!-- ── X-axis tick labels (zone boundaries as % of market rate) ────── -->
      <text
        :x="xZoneFull"
        :y="PLOT_Y2 + 10"
        text-anchor="middle"
        font-size="8"
        fill="currentColor"
        opacity="0.55"
      >
        60%
      </text>
      <text
        :x="xAdjustedRateFixed"
        :y="PLOT_Y2 + 10"
        text-anchor="middle"
        font-size="8"
        fill="#3b82f6"
        font-weight="600"
      >
        100%
      </text>
      <text
        :x="xZoneNinety"
        :y="PLOT_Y2 + 10"
        text-anchor="middle"
        font-size="8"
        fill="currentColor"
        opacity="0.55"
      >
        110%
      </text>

      <!-- ── City reference rate marker (dashed gray line) ─────────────── -->
      <line
        :x1="xCityRate"
        :y1="PLOT_Y"
        :x2="xCityRate"
        :y2="PLOT_Y2"
        stroke="#9ca3af"
        stroke-width="1.5"
        stroke-dasharray="5,3"
      />
      <!-- City rate label above chart — hide when it would overlap adjusted-rate label -->
      <text
        v-if="!markersOverlap"
        :x="xCityRate - 3"
        :y="PLOT_Y - 4"
        :text-anchor="cityRateLabelAnchor"
        font-size="8"
        fill="#9ca3af"
      >
        {{ t('property.chartCityRateLabel') }}
      </text>

      <!-- ── Adjusted market rate marker (solid blue line) ──────────────── -->
      <line
        :x1="xAdjustedRateFixed"
        :y1="PLOT_Y"
        :x2="xAdjustedRateFixed"
        :y2="PLOT_Y2"
        stroke="#3b82f6"
        stroke-width="2"
        stroke-dasharray="5,3"
      />
      <!-- Adjusted rate label above chart -->
      <text
        :x="xAdjustedRateFixed + 3"
        :y="PLOT_Y - 4"
        text-anchor="start"
        font-size="8"
        fill="#3b82f6"
        font-weight="600"
      >
        {{
          markersOverlap ? t('property.chartRatesLabel') : t('property.chartAdjustedRateLabel')
        }}
      </text>

      <!-- ── Current-rent marker ─────────────────────────────────────────── -->
      <template v-if="xCurrentRent !== null">
        <line
          :x1="xCurrentRent"
          :y1="PLOT_Y"
          :x2="xCurrentRent"
          :y2="PLOT_Y2"
          :stroke="currentRentMarkerColor"
          stroke-width="2"
        />
        <!-- Diamond marker at the top of the line -->
        <polygon
          :points="`${xCurrentRent},${PLOT_Y - 3} ${xCurrentRent + 4},${PLOT_Y + 4} ${xCurrentRent},${PLOT_Y + 11} ${xCurrentRent - 4},${PLOT_Y + 4}`"
          :fill="currentRentMarkerColor"
        />
      </template>
    </svg>

    <!-- ── HTML legend below the SVG (avoids SVG text truncation) ─────────────── -->
    <div class="rent-chart-legend mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs">
      <!-- City reference rate -->
      <span class="flex items-center gap-1.5 text-muted">
        <span
          class="inline-block h-0.5 w-4"
          style="background: repeating-linear-gradient(90deg,#9ca3af 0,#9ca3af 5px,transparent 5px,transparent 8px)"
        ></span>
        {{ t('property.cityReferenceRate') }}:
        <strong class="text-foreground">{{ formatCurrency(cityReferenceRentPerSqm) }}/m²</strong>
      </span>
      <!-- Adjusted market rate -->
      <span class="flex items-center gap-1.5" style="color:#3b82f6">
        <span
          class="inline-block h-0.5 w-4"
          style="background: repeating-linear-gradient(90deg,#3b82f6 0,#3b82f6 5px,transparent 5px,transparent 8px)"
        ></span>
        {{ t('property.adjustedMarketRate') }}:
        <strong>{{ formatCurrency(adjustedMarketRentPerSqm) }}/m²</strong>
      </span>
      <!-- Your current rent (conditional) -->
      <span
        v-if="currentRentPerSqm != null"
        class="flex items-center gap-1.5"
        :style="`color:${currentRentMarkerColor}`"
      >
        <span
          class="inline-block h-0.5 w-4"
          :style="`background:${currentRentMarkerColor}`"
        ></span>
        {{ t('property.yourRent') }}:
        <strong>{{ formatCurrency(currentRentPerSqm) }}/m²</strong>
      </span>
    </div>

    <!-- ── Contextual hint below chart ──────────────────────────────────────── -->
    <p class="rent-chart-hint mt-2 text-xs text-muted">
      {{
        buildingType === 'APARTMENT'
          ? t('property.rentChartHintApartment')
          : t('property.rentChartHintCommercial')
      }}
    </p>
  </div>
</template>


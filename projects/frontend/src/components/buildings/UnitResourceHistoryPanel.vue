<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { formatUnitQuantity } from '@/lib/gridTileHelpers'
import {
  buildUnitResourceHistoryChartModel,
  getRecentUnitResourceHistoryRows,
  type UnitResourceHistoryItemOption,
  type UnitResourceHistoryMetricKey,
} from '@/lib/unitResourceHistory'
import type { BuildingUnitResourceHistoryPoint } from '@/types'

const props = defineProps<{
  items: UnitResourceHistoryItemOption[]
  selectedItemKey: string | null
  history: BuildingUnitResourceHistoryPoint[]
}>()

const emit = defineEmits<{
  'update:selectedItemKey': [value: string]
}>()

const { t } = useI18n()

const plotWidth = 520
const plotHeight = 170
const gridLines = [0.25, 0.5, 0.75, 1]

const metricDisplay: Record<
  UnitResourceHistoryMetricKey,
  { labelKey: string; color: string }
> = {
  inflowQuantity: {
    labelKey: 'buildingDetail.inventory.history.inflow',
    color: '#2563eb',
  },
  outflowQuantity: {
    labelKey: 'buildingDetail.inventory.history.outflow',
    color: '#dc2626',
  },
  consumedQuantity: {
    labelKey: 'buildingDetail.inventory.history.consumed',
    color: '#d97706',
  },
  producedQuantity: {
    labelKey: 'buildingDetail.inventory.history.produced',
    color: '#059669',
  },
}

const chartModel = computed(() => buildUnitResourceHistoryChartModel(props.history, plotWidth, plotHeight))
const visibleSeries = computed(() => chartModel.value.series.filter((series) => series.total > 0 || series.lastValue > 0))
const recentRows = computed(() => getRecentUnitResourceHistoryRows(props.history, 6))

function updateSelectedItemKey(value: string) {
  emit('update:selectedItemKey', value)
}

function getMetricLabel(metricKey: UnitResourceHistoryMetricKey): string {
  return t(metricDisplay[metricKey].labelKey)
}
</script>

<template>
  <div class="unit-insight-card history-card">
    <div class="history-header">
      <div>
        <h5>{{ t('buildingDetail.inventory.history.title') }}</h5>
        <p class="config-help">{{ t('buildingDetail.inventory.history.subtitle') }}</p>
      </div>
      <div v-if="items.length > 0" class="history-item-picker" role="tablist" :aria-label="t('buildingDetail.inventory.history.selector')">
        <button
          v-for="item in items"
          :key="item.key"
          type="button"
          class="history-item-chip"
          :class="{ selected: item.key === selectedItemKey }"
          :aria-pressed="item.key === selectedItemKey"
          @click="updateSelectedItemKey(item.key)"
        >
          {{ item.label }}
        </button>
      </div>
    </div>

    <p v-if="items.length === 0" class="inventory-empty">{{ t('buildingDetail.inventory.history.noTrackedItems') }}</p>
    <template v-else-if="!chartModel.hasData">
      <p class="inventory-empty">{{ t('buildingDetail.inventory.history.empty') }}</p>
    </template>
    <template v-else>
      <div class="history-summary-grid">
        <div v-for="series in visibleSeries" :key="series.key" class="history-summary-stat">
          <span class="history-summary-label">{{ getMetricLabel(series.key) }}</span>
          <strong>{{ formatUnitQuantity(series.total) }}</strong>
        </div>
        <div class="history-summary-stat">
          <span class="history-summary-label">{{ t('buildingDetail.inventory.history.tickRange') }}</span>
          <strong>
            {{ t('buildingDetail.inventory.history.tickWindow', { start: chartModel.tickRange.start ?? 0, end: chartModel.tickRange.end ?? 0 }) }}
          </strong>
        </div>
      </div>

      <div class="history-chart-shell">
        <svg class="history-chart" :viewBox="`0 0 ${plotWidth} ${plotHeight}`" role="img" :aria-label="t('buildingDetail.inventory.history.chartAriaLabel')">
          <line
            v-for="ratio in gridLines"
            :key="ratio"
            class="history-grid-line"
            x1="0"
            :y1="plotHeight - plotHeight * ratio"
            :x2="plotWidth"
            :y2="plotHeight - plotHeight * ratio"
          />
          <line class="history-grid-line baseline" x1="0" :y1="plotHeight" :x2="plotWidth" :y2="plotHeight" />

          <g v-for="series in visibleSeries" :key="series.key">
            <polyline
              class="history-series-line"
              fill="none"
              stroke-linecap="round"
              stroke-linejoin="round"
              :stroke="metricDisplay[series.key].color"
              :points="series.polylinePoints"
            />
            <circle
              v-for="point in series.points"
              :key="`${series.key}-${point.tick}`"
              class="history-series-point"
              :cx="point.x"
              :cy="point.y"
              r="4"
              :fill="metricDisplay[series.key].color"
            >
              <title>{{ `${getMetricLabel(series.key)} · ${t('buildingDetail.inventory.history.tickShort', { tick: point.tick })} · ${formatUnitQuantity(point.value)}` }}</title>
            </circle>
          </g>
        </svg>
      </div>

      <div class="history-legend">
        <div v-for="series in visibleSeries" :key="series.key" class="history-legend-item">
          <span class="history-legend-swatch" :style="{ background: metricDisplay[series.key].color }"></span>
          <span>{{ getMetricLabel(series.key) }}</span>
        </div>
      </div>

      <div class="history-table">
        <div class="history-table-header">
          <span>{{ t('buildingDetail.inventory.history.tickColumn') }}</span>
          <span>{{ t('buildingDetail.inventory.history.inflow') }}</span>
          <span>{{ t('buildingDetail.inventory.history.outflow') }}</span>
          <span>{{ t('buildingDetail.inventory.history.consumed') }}</span>
          <span>{{ t('buildingDetail.inventory.history.produced') }}</span>
        </div>
        <div v-for="row in recentRows" :key="`${row.tick}-${row.resourceTypeId ?? row.productTypeId}`" class="history-table-row">
          <span>{{ t('buildingDetail.inventory.history.tickShort', { tick: row.tick }) }}</span>
          <span>{{ formatUnitQuantity(row.inflowQuantity) }}</span>
          <span>{{ formatUnitQuantity(row.outflowQuantity) }}</span>
          <span>{{ formatUnitQuantity(row.consumedQuantity) }}</span>
          <span>{{ formatUnitQuantity(row.producedQuantity) }}</span>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.history-card {
  display: grid;
  gap: 1rem;
}

.history-header {
  display: grid;
  gap: 0.75rem;
}

.history-item-picker {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.history-item-chip {
  border: 1px solid var(--color-border);
  background: var(--color-surface-muted);
  color: var(--color-text);
  border-radius: 999px;
  padding: 0.45rem 0.85rem;
  font: inherit;
  cursor: pointer;
}

.history-item-chip.selected {
  border-color: var(--color-primary);
  background: color-mix(in srgb, var(--color-primary) 12%, var(--color-surface));
}

.history-summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(110px, 1fr));
  gap: 0.75rem;
}

.history-summary-stat {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-surface-muted);
  padding: 0.75rem;
}

.history-summary-label {
  display: block;
  margin-bottom: 0.25rem;
  font-size: 0.8rem;
  color: var(--color-text-secondary);
}

.history-chart-shell {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--color-surface-muted) 84%, white 16%), var(--color-surface));
  padding: 0.75rem;
}

.history-chart {
  width: 100%;
  height: auto;
  display: block;
}

.history-grid-line {
  stroke: color-mix(in srgb, var(--color-border) 80%, transparent 20%);
  stroke-width: 1;
  stroke-dasharray: 4 6;
}

.history-grid-line.baseline {
  stroke-dasharray: none;
}

.history-series-line {
  stroke-width: 3;
}

.history-series-point {
  stroke: var(--color-surface);
  stroke-width: 2;
}

.history-legend {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem 1rem;
}

.history-legend-item {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  color: var(--color-text-secondary);
  font-size: 0.9rem;
}

.history-legend-swatch {
  width: 0.75rem;
  height: 0.75rem;
  border-radius: 999px;
}

.history-table {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  overflow: hidden;
}

.history-table-header,
.history-table-row {
  display: grid;
  grid-template-columns: minmax(72px, 1fr) repeat(4, minmax(0, 1fr));
  gap: 0.75rem;
  align-items: center;
  padding: 0.65rem 0.85rem;
}

.history-table-header {
  background: var(--color-surface-muted);
  font-size: 0.78rem;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-secondary);
}

.history-table-row {
  border-top: 1px solid var(--color-border);
}

@media (max-width: 640px) {
  .history-table-header,
  .history-table-row {
    grid-template-columns: repeat(5, minmax(0, 1fr));
    font-size: 0.8rem;
  }
}
</style>
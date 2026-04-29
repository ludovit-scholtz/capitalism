<script setup lang="ts">
import { computed, inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'
import RentReferenceChart from '@/components/buildings/RentReferenceChart.vue'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const { locale, building, currentTick, showRentDialog, newRentPerSqm, savingRent, rentSaveError, openRentDialog, closeRentDialog, saveRentPerSqm, formatCurrency, formatTickDuration } = bd

/** Price ratio of current rent vs. adjusted market rate (location-adjusted). */
const priceRatio = computed(() => {
  const price = building.value?.pricePerSqm
  const market = building.value?.adjustedMarketRentPerSqm
  if (price == null || market == null || market <= 0) return null
  return price / market
})

// Occupancy-cap tier thresholds (aligned with backend RentPhase constants):
// > 1.1  → overpriced: occupancy drifts to 50% floor
// 1.0–1.1 → above market: max 90% occupancy
// 0.6–1.0 → good range: linear interpolation 90%–100%
// < 0.6  → very attractive: can reach 100% occupancy
const OVERPRICED_THRESHOLD = 1.1
const ABOVE_MARKET_THRESHOLD = 1.0
const GOOD_RANGE_LOWER = 0.6
const GOOD_RANGE_UPPER = 0.9

/** Human-readable price position label and CSS class. */
const rentPosition = computed((): { key: string; cls: string } | null => {
  const r = priceRatio.value
  if (r === null) return null
  if (r > OVERPRICED_THRESHOLD) return { key: 'property.rentPositionOverpriced', cls: 'position-overpriced' }
  if (r > ABOVE_MARKET_THRESHOLD) return { key: 'property.rentPositionAboveMarket', cls: 'position-above' }
  if (r >= GOOD_RANGE_UPPER) return { key: 'property.rentPositionAtMarket', cls: 'position-good' }
  if (r >= GOOD_RANGE_LOWER) return { key: 'property.rentPositionGood', cls: 'position-good' }
  return { key: 'property.rentPositionBelow60', cls: 'position-great' }
})

const rentVsMarketPct = computed(() => {
  if (priceRatio.value === null) return null
  return ((priceRatio.value - 1) * 100).toFixed(0)
})

/** Narrowed type for the RentReferenceChart component (avoids template-level `|` pipe). */
const propertyBuildingType = computed((): 'APARTMENT' | 'COMMERCIAL' => {
  return (building.value?.type === 'COMMERCIAL' ? 'COMMERCIAL' : 'APARTMENT')
})

const fallbackPropertyAreaSqm = computed<number>(() => {
  return building.value?.type === 'COMMERCIAL' ? 1400 : 1800
})

const propertyAreaSqm = computed<number>(() => {
  const area = building.value?.totalAreaSqm
  if (area != null && area > 0) return area
  return fallbackPropertyAreaSqm.value
})

const occupancyPercent = computed<number>(() => {
  const occupancy = building.value?.occupancyPercent
  if (occupancy == null) return 0
  return occupancy
})
</script>

<template>
  <div
    v-if="building?.type === 'APARTMENT' || building?.type === 'COMMERCIAL'"
    class="property-panel mb-4 rounded-xl border border-divider bg-card p-4 sm:p-5"
    role="region"
    :aria-label="t('buildingDetail.accessibility.propertyManagement')"
  >
    <div class="property-panel-header mb-4 flex flex-wrap items-center justify-between gap-3">
      <h2 class="property-panel-title text-lg font-semibold text-foreground">{{ t('property.panelTitle') }}</h2>
      <button class="btn btn-primary btn-sm" @click="openRentDialog">
        {{ t('property.setRentBtn') }}
      </button>
    </div>

    <!-- Key metrics row -->
    <div class="property-metrics grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
      <div class="property-metric rounded-lg border border-divider bg-surface p-3">
        <span class="property-metric-label text-xs uppercase tracking-wide text-muted">{{ t('property.totalArea') }}</span>
        <span class="property-metric-value mt-1 block text-sm font-semibold text-foreground">
          {{ propertyAreaSqm.toLocaleString() }} m²
        </span>
      </div>
      <div class="property-metric rounded-lg border border-divider bg-surface p-3">
        <span class="property-metric-label text-xs uppercase tracking-wide text-muted">{{ t('property.occupancy') }}</span>
        <span
          class="property-metric-value mt-1 block text-sm font-semibold"
          :class="{
            'text-warning': occupancyPercent < 60,
            'text-success': occupancyPercent >= 75,
            'text-foreground': occupancyPercent >= 60 && occupancyPercent < 75,
          }"
        >
          {{ occupancyPercent.toFixed(1) }}%
        </span>
      </div>
      <div class="property-metric rounded-lg border border-divider bg-surface p-3">
        <span class="property-metric-label text-xs uppercase tracking-wide text-muted">{{ t('property.activeRent') }}</span>
        <span class="property-metric-value mt-1 block text-sm font-semibold text-foreground">
          {{ building?.pricePerSqm != null ? formatCurrency(building?.pricePerSqm) + ' / m²' : t('property.noRentSet') }}
        </span>
      </div>
      <div class="property-metric rounded-lg border border-divider bg-surface p-3">
        <span class="property-metric-label text-xs uppercase tracking-wide text-muted">{{ t('property.occupiedArea') }}</span>
        <span class="property-metric-value mt-1 block text-sm font-semibold text-foreground">
          {{ Math.round(propertyAreaSqm * (occupancyPercent / 100)).toLocaleString() }} m² / {{ propertyAreaSqm.toLocaleString() }} m²
        </span>
      </div>
    </div>

    <!-- ── Market Rate Guidance ───────────────────────────────────────────── -->
    <div
      v-if="building?.adjustedMarketRentPerSqm != null"
      class="market-guidance mt-5 rounded-xl border border-divider bg-surface p-4"
      role="region"
      :aria-label="t('property.marketGuidanceTitle')"
    >
      <h3 class="market-guidance-title mb-3 text-sm font-semibold uppercase tracking-wide text-muted">
        {{ t('property.marketGuidanceTitle') }}
      </h3>

      <!-- Rate comparison table -->
      <div class="market-rates grid grid-cols-1 gap-2 sm:grid-cols-3">
        <div class="market-rate-item flex flex-col">
          <span class="market-rate-label text-xs text-muted">{{ t('property.cityReferenceRate') }}</span>
          <span class="market-rate-value text-sm font-semibold text-foreground">
            {{ building?.cityReferenceRentPerSqm != null ? formatCurrency(building.cityReferenceRentPerSqm) + ' / m²' : t('common.notAvailable') }}
          </span>
        </div>
        <div class="market-rate-item flex flex-col">
          <span class="market-rate-label text-xs text-muted">
            {{ t('property.adjustedMarketRate') }}
            <span
              v-if="building?.populationIndex != null"
              class="location-index-badge ml-1 rounded bg-surface-muted px-1 text-xs text-muted"
              :title="t('property.locationIndexTooltip')"
            >×{{ building.populationIndex.toFixed(2) }}</span>
          </span>
          <span class="market-rate-value text-sm font-bold text-foreground">
            {{ formatCurrency(building.adjustedMarketRentPerSqm) }} / m²
          </span>
        </div>
        <div class="market-rate-item flex flex-col">
          <span class="market-rate-label text-xs text-muted">{{ t('property.yourRent') }}</span>
          <span class="market-rate-value text-sm font-semibold text-foreground">
            {{ building?.pricePerSqm != null ? formatCurrency(building.pricePerSqm) + ' / m²' : t('property.noRentSet') }}
          </span>
        </div>
      </div>

      <!-- Price position indicator -->
      <div v-if="rentPosition" class="price-position mt-3">
        <div class="price-position-bar flex items-center gap-3">
          <span class="price-position-label text-xs text-muted">{{ t('property.rentPositionLabel') }}</span>
          <span
            class="price-position-value rounded-full px-2 py-0.5 text-xs font-semibold"
            :class="{
              'bg-success/15 text-success': rentPosition.cls === 'position-great',
              'bg-blue-500/15 text-blue-600 dark:text-blue-400': rentPosition.cls === 'position-good',
              'bg-warning/15 text-warning': rentPosition.cls === 'position-above',
              'bg-error/15 text-error': rentPosition.cls === 'position-overpriced',
            }"
          >
            {{ t(rentPosition.key) }}
          </span>
          <span v-if="rentVsMarketPct !== null" class="rent-vs-market text-xs text-muted">
            ({{ rentVsMarketPct }}% {{ t('property.rentVsMarket') }})
          </span>
        </div>
      </div>

      <!-- Breakeven info bar -->
      <div class="market-guidance-notes mt-3 space-y-1">
        <p class="market-note text-xs text-muted">{{ t('property.occupancyTrendNote') }}</p>
        <p class="market-note text-xs text-muted">💡 {{ t('property.breakevenNote') }}</p>
      </div>
    </div>

    <!-- ── Rent Reference Chart ─────────────────────────────────────────────── -->
    <RentReferenceChart
      v-if="
        building?.adjustedMarketRentPerSqm != null &&
        building?.cityReferenceRentPerSqm != null &&
        (building?.type === 'APARTMENT' || building?.type === 'COMMERCIAL')
      "
      :city-reference-rent-per-sqm="building.cityReferenceRentPerSqm"
      :adjusted-market-rent-per-sqm="building.adjustedMarketRentPerSqm"
      :current-rent-per-sqm="building.pricePerSqm ?? null"
      :building-type="propertyBuildingType"
      :format-currency="formatCurrency"
    />

    <div
      v-else-if="building?.pricePerSqm != null && building?.adjustedMarketRentPerSqm == null"
      class="market-guidance-unavailable mt-4 rounded-lg border border-dashed border-divider bg-surface-muted px-3 py-2 text-sm text-muted"
    >
      {{ t('property.noMarketDataAvailable') }}
    </div>

    <!-- Pending rent change notice -->
    <div
      v-if="building?.pendingPricePerSqm != null"
      class="pending-rent-notice mt-4 flex items-start gap-2 rounded-lg border border-amber-300/60 bg-amber-500/10 p-3 text-sm text-amber-800 dark:text-amber-300"
      role="status"
    >
      <span class="pending-rent-icon">⏳</span>
      <span class="pending-rent-text">
        {{
          t('property.pendingRentNotice', {
            rent: formatCurrency(building?.pendingPricePerSqm),
            time: building?.pendingPriceActivationTick != null ? formatTickDuration(Math.max(0, building?.pendingPriceActivationTick - currentTick), locale) : '—',
          })
        }}
      </span>
    </div>

    <!-- Occupancy empty-state hint -->
    <div
      v-if="building?.occupancyPercent === 0 && building?.pricePerSqm == null"
      class="property-empty-state mt-4 rounded-lg border border-dashed border-divider bg-surface-muted px-3 py-2 text-sm text-muted"
    >
      {{ t('property.noRentHint') }}
    </div>

    <!-- Rent dialog -->
    <div v-if="showRentDialog" class="rent-dialog">
      <div class="rent-dialog-header">
        <h3>{{ t('property.rentDialogTitle') }}</h3>
        <button class="btn btn-ghost" @click="closeRentDialog">{{ t('common.close') }}</button>
      </div>
      <div class="rent-dialog-body">
        <p class="rent-dialog-hint">{{ t('property.rentDelayHint') }}</p>

        <!-- Inline market rate hint inside dialog -->
        <div
          v-if="building?.adjustedMarketRentPerSqm != null"
          class="rent-dialog-market-hint mb-3 rounded-lg bg-surface-muted px-3 py-2 text-xs text-muted"
        >
          {{ t('property.adjustedMarketRate') }}: <strong>{{ formatCurrency(building.adjustedMarketRentPerSqm) }} / m²</strong>
          <span v-if="building.populationIndex != null"> ({{ t('property.locationIndex') }}: ×{{ building.populationIndex.toFixed(2) }})</span>
        </div>

        <label class="form-label">{{ t('property.rentLabel') }}</label>
        <input
          type="number"
          class="form-input"
          :placeholder="t('property.rentPlaceholder')"
          :value="newRentPerSqm"
          @input="newRentPerSqm = isNaN(($event.target as HTMLInputElement).valueAsNumber) ? null : ($event.target as HTMLInputElement).valueAsNumber"
          min="0"
          step="0.5"
        />
        <p v-if="rentSaveError" class="rent-dialog-error">{{ rentSaveError }}</p>
        <div class="rent-dialog-actions">
          <button class="btn btn-primary" :disabled="savingRent || newRentPerSqm === null || newRentPerSqm < 0" @click="saveRentPerSqm">
            {{ savingRent ? t('common.saving') : t('property.scheduleRentBtn') }}
          </button>
          <button class="btn btn-secondary" @click="closeRentDialog">{{ t('common.cancel') }}</button>
        </div>
      </div>
    </div>
  </div>
</template>


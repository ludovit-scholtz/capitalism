<script setup lang="ts">
import { inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const { locale, building, currentTick, showRentDialog, newRentPerSqm, savingRent, rentSaveError, openRentDialog, closeRentDialog, saveRentPerSqm, formatCurrency, formatTickDuration } = bd
</script>

<template>
  <div
    v-if="building?.type === 'APARTMENT' || building?.type === 'COMMERCIAL'"
    class="property-panel mb-4 rounded-xl border border-divider bg-card p-4 sm:p-5"
    role="region"
    aria-label="property management"
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
          {{ building?.totalAreaSqm != null ? building?.totalAreaSqm.toLocaleString() + ' m²' : t('common.notAvailable') }}
        </span>
      </div>
      <div class="property-metric rounded-lg border border-divider bg-surface p-3">
        <span class="property-metric-label text-xs uppercase tracking-wide text-muted">{{ t('property.occupancy') }}</span>
        <span class="property-metric-value mt-1 block text-sm font-semibold text-foreground" :class="{ 'property-metric-zero text-warning': building?.occupancyPercent === 0 }">
          {{ building?.occupancyPercent != null ? building?.occupancyPercent.toFixed(1) + '%' : t('common.notAvailable') }}
        </span>
      </div>
      <div class="property-metric rounded-lg border border-divider bg-surface p-3">
        <span class="property-metric-label text-xs uppercase tracking-wide text-muted">{{ t('property.activeRent') }}</span>
        <span class="property-metric-value mt-1 block text-sm font-semibold text-foreground">
          {{ building?.pricePerSqm != null ? formatCurrency(building?.pricePerSqm) + ' / m²' : t('property.noRentSet') }}
        </span>
      </div>
      <div v-if="building?.occupancyPercent != null && building?.totalAreaSqm != null" class="property-metric rounded-lg border border-divider bg-surface p-3">
        <span class="property-metric-label text-xs uppercase tracking-wide text-muted">{{ t('property.occupiedArea') }}</span>
        <span class="property-metric-value mt-1 block text-sm font-semibold text-foreground">
          {{ Math.round(building?.totalAreaSqm * (building?.occupancyPercent / 100)).toLocaleString() }} m² / {{ building?.totalAreaSqm.toLocaleString() }} m²
        </span>
      </div>
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

<script setup lang="ts">
import { inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const { locale, building, currentTick, showRentDialog, rentSaveError, openRentDialog, closeRentDialog, saveRentPerSqm, formatCurrency, formatTickDuration } = bd
</script>

<template>
<div v-if="building?.type === 'APARTMENT' || building?.type === 'COMMERCIAL'" class="property-panel" role="region" aria-label="property management">
  <div class="property-panel-header">
    <h2 class="property-panel-title">{{ t('property.panelTitle') }}</h2>
    <button class="btn btn-primary btn-sm" @click="openRentDialog">
      {{ t('property.setRentBtn') }}
    </button>
  </div>

  <!-- Key metrics row -->
  <div class="property-metrics">
    <div class="property-metric">
      <span class="property-metric-label">{{ t('property.totalArea') }}</span>
      <span class="property-metric-value">
        {{ building?.totalAreaSqm != null ? building?.totalAreaSqm.toLocaleString() + ' m²' : t('common.notAvailable') }}
      </span>
    </div>
    <div class="property-metric">
      <span class="property-metric-label">{{ t('property.occupancy') }}</span>
      <span class="property-metric-value" :class="{ 'property-metric-zero': building?.occupancyPercent === 0 }">
        {{ building?.occupancyPercent != null ? building?.occupancyPercent.toFixed(1) + '%' : t('common.notAvailable') }}
      </span>
    </div>
    <div class="property-metric">
      <span class="property-metric-label">{{ t('property.activeRent') }}</span>
      <span class="property-metric-value">
        {{ building?.pricePerSqm != null ? formatCurrency(building?.pricePerSqm) + ' / m²' : t('property.noRentSet') }}
      </span>
    </div>
    <div v-if="building?.occupancyPercent != null && building?.totalAreaSqm != null" class="property-metric">
      <span class="property-metric-label">{{ t('property.occupiedArea') }}</span>
      <span class="property-metric-value">
        {{ Math.round(building?.totalAreaSqm * (building?.occupancyPercent / 100)).toLocaleString() }} m² / {{ building?.totalAreaSqm.toLocaleString() }} m²
      </span>
    </div>
  </div>

  <!-- Pending rent change notice -->
  <div v-if="building?.pendingPricePerSqm != null" class="pending-rent-notice" role="status">
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
  <div v-if="building?.occupancyPercent === 0 && building?.pricePerSqm == null" class="property-empty-state">
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

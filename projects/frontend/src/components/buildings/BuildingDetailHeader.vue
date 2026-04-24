<script setup lang="ts">
import { inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const { building, showSaleDialog, salePrice, savingSale, openSaleDialog, closeSaleDialog, setBuildingForSale, formatBuildingType } = bd
</script>

<template>
<div class="building-header">
  <div class="building-title">
    <h1>{{ building?.name }}</h1>
    <span class="building-type-badge">{{ formatBuildingType(building?.type) }}</span>
  </div>
  <div class="building-meta">
    <span class="meta-pill">
      <span class="meta-label">{{ t('common.level') }}</span>
      <span class="meta-value">{{ building?.level }}</span>
    </span>
    <span class="meta-pill">
      <span class="meta-label">{{ t('buildings.power') }}</span>
      <span class="meta-value">{{ building?.powerConsumption }} {{ t('buildings.powerUnit') }}</span>
    </span>
    <span
      v-if="building?.powerStatus"
      class="meta-pill power-status-pill"
      :class="{
        'power-status-powered': building?.powerStatus === 'POWERED',
        'power-status-constrained': building?.powerStatus === 'CONSTRAINED',
        'power-status-offline': building?.powerStatus === 'OFFLINE',
      }"
      :title="t(`powerGrid.buildingStatusHint.${building?.powerStatus}`, { percent: 50 })"
      role="status"
    >
      <span class="meta-label">{{ t('powerGrid.powerCardTitle') }}</span>
      <span class="meta-value">{{ t(`powerGrid.buildingStatus.${building?.powerStatus}`) }}</span>
    </span>
    <span class="meta-pill" :class="building?.isForSale ? 'for-sale' : ''">
      {{ building?.isForSale ? t('buildingDetail.forSale') : t('buildingDetail.notForSale') }}
    </span>
    <button class="btn btn-secondary btn-sm" @click="openSaleDialog">
      {{ building?.isForSale ? t('buildingDetail.editSale') : t('buildingDetail.sellBuilding') }}
    </button>
  </div>
</div>

<!-- Sale dialog -->
<div v-if="showSaleDialog" class="sale-dialog">
  <div class="sale-dialog-header">
    <h3>{{ t('buildingDetail.sellBuilding') }}</h3>
    <button class="btn btn-ghost" @click="closeSaleDialog">{{ t('common.close') }}</button>
  </div>
  <div class="sale-dialog-body">
    <label class="form-label">{{ t('buildingDetail.askingPrice') }}</label>
    <input
      type="number"
      class="form-input"
      :placeholder="t('buildingDetail.askingPricePlaceholder')"
      :value="salePrice"
      @input="salePrice = isNaN(($event.target as HTMLInputElement).valueAsNumber) ? null : ($event.target as HTMLInputElement).valueAsNumber"
      min="0"
      step="1000"
    />
    <div class="sale-dialog-actions">
      <button class="btn btn-primary" :disabled="savingSale || !salePrice || salePrice <= 0" @click="setBuildingForSale(true)">
        {{ t('buildingDetail.listForSale') }}
      </button>
      <button v-if="building?.isForSale" class="btn btn-danger" :disabled="savingSale" @click="setBuildingForSale(false)">
        {{ t('buildingDetail.cancelSale') }}
      </button>
    </div>
  </div>
</div>

</template>

<script setup lang="ts">
import { inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const {
  building,
  showSaleDialog,
  salePrice,
  savingSale,
  openSaleDialog,
  closeSaleDialog,
  setBuildingForSale,
  formatBuildingType,
  isBuildingUsedAsCollateral,
} = bd
</script>

<template>
  <div class="building-header mb-4 rounded-xl border border-divider bg-card p-4 sm:p-5">
    <div class="building-title mb-3 flex flex-wrap items-center gap-3">
      <h1 class="m-0 text-2xl font-semibold leading-tight text-foreground">{{ building?.name }}</h1>
      <span class="building-type-badge inline-flex items-center rounded-full bg-primary px-3 py-1 text-xs font-semibold uppercase tracking-wide text-primary-foreground">{{
        formatBuildingType(building?.type ?? '')
      }}</span>
    </div>
    <div class="building-meta flex flex-wrap items-center gap-2">
      <span class="meta-pill inline-flex items-center gap-1.5 rounded-full border border-divider bg-surface px-3 py-1.5 text-sm">
        <span class="meta-label text-xs text-muted">{{ t('common.level') }}</span>
        <span class="meta-value font-semibold text-foreground">{{ building?.level }}</span>
      </span>
      <span class="meta-pill inline-flex items-center gap-1.5 rounded-full border border-divider bg-surface px-3 py-1.5 text-sm">
        <span class="meta-label text-xs text-muted">{{ t('buildings.power') }}</span>
        <span class="meta-value font-semibold text-foreground">{{ building?.powerConsumption }} {{ t('buildings.powerUnit') }}</span>
      </span>
      <span
        v-if="building?.powerStatus"
        class="meta-pill power-status-pill inline-flex items-center gap-1.5 rounded-full border border-divider bg-surface px-3 py-1.5 text-sm"
        :class="{
          'power-status-powered border-green-300/60 bg-green-500/10 text-green-700 dark:text-green-300': building?.powerStatus === 'POWERED',
          'power-status-constrained border-amber-300/60 bg-amber-500/15 text-amber-700 dark:text-amber-300': building?.powerStatus === 'CONSTRAINED',
          'power-status-offline border-red-300/60 bg-red-500/10 text-red-700 dark:text-red-300': building?.powerStatus === 'OFFLINE',
        }"
        :title="t(`powerGrid.buildingStatusHint.${building?.powerStatus}`, { percent: 50 })"
        role="status"
      >
        <span class="meta-label text-xs text-current/80">{{ t('powerGrid.powerCardTitle') }}</span>
        <span class="meta-value font-semibold">{{ t(`powerGrid.buildingStatus.${building?.powerStatus}`) }}</span>
      </span>
      <span
        class="meta-pill inline-flex items-center rounded-full border border-divider bg-surface px-3 py-1.5 text-sm font-medium text-foreground"
        :class="building?.isForSale ? 'for-sale border-green-300/60 bg-green-500/10 text-green-700 dark:text-green-300' : ''"
      >
        {{ building?.isForSale ? t('buildingDetail.forSale') : t('buildingDetail.notForSale') }}
      </span>
      <button class="btn btn-secondary btn-sm ml-auto" :disabled="isBuildingUsedAsCollateral" @click="openSaleDialog">
        {{ building?.isForSale ? t('buildingDetail.editSale') : t('buildingDetail.sellBuilding') }}
      </button>
    </div>
    <p v-if="isBuildingUsedAsCollateral" class="collateral-warning mt-2 text-xs text-amber-600 dark:text-amber-400">
      {{ t('buildingDetail.collateralRestrictionWarning') }}
    </p>
  </div>

  <!-- Sale dialog modal -->
  <Teleport to="body">
    <div
      v-if="showSaleDialog"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/60"
      role="dialog"
      aria-modal="true"
      @click.self="closeSaleDialog"
    >
      <div class="sale-dialog w-full max-w-md rounded-xl border border-divider bg-card p-6 shadow-xl">
        <div class="mb-4 flex items-center justify-between">
          <h3 class="text-lg font-semibold text-foreground">{{ t('buildingDetail.sellBuilding') }}</h3>
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
    </div>
  </Teleport>
</template>

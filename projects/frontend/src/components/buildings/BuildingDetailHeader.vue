<script setup lang="ts">
import { inject, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'
import { formatCurrency } from '@/lib/loanHelpers'

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
  estimatedMarketValue,
  cityCurrencyCode,
  formatBuildingType,
  isBuildingUsedAsCollateral,
  collateralLoanCount,
} = bd

/** True when the asking price exceeds 150% of the estimated market value. */
const isPriceHigh = computed(() => {
  if (!salePrice.value || !estimatedMarketValue.value) return false
  return salePrice.value > estimatedMarketValue.value * 1.5
})
</script>

<template>
  <div class="building-header mb-4 rounded-xl border border-divider bg-card p-4 sm:p-5">
    <div class="building-title mb-3 flex flex-wrap items-center gap-3">
      <h1 class="m-0 text-2xl font-semibold leading-tight text-foreground">{{ building?.name }}</h1>
      <span class="building-type-badge inline-flex items-center rounded-full bg-primary px-3 py-1 text-xs font-semibold uppercase tracking-wide text-primary-foreground">{{
        formatBuildingType(building?.type ?? '')
      }}</span>
      <!-- Destroyed badge -->
      <span
        v-if="building?.destroyedAtUtc"
        class="destroyed-badge inline-flex items-center gap-1 rounded-full border border-red-300/60 bg-red-500/15 px-3 py-1 text-xs font-semibold text-red-700 dark:text-red-300"
        :title="t('buildingDetail.destroyedHint')"
      >
        <font-awesome-icon icon="skull" class="text-[10px]" />
        {{ t('buildingDetail.destroyedBadge') }}
      </span>
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
        v-if="!building?.destroyedAtUtc"
        class="meta-pill inline-flex items-center rounded-full border border-divider bg-surface px-3 py-1.5 text-sm font-medium text-foreground"
        :class="building?.isForSale ? 'for-sale border-green-300/60 bg-green-500/10 text-green-700 dark:text-green-300' : ''"
      >
        {{ building?.isForSale ? t('buildingDetail.forSale') : t('buildingDetail.notForSale') }}
      </span>
      <!-- Sell button — hidden for destroyed buildings -->
      <button
        v-if="!building?.destroyedAtUtc"
        class="btn btn-secondary btn-sm ml-auto"
        :disabled="isBuildingUsedAsCollateral"
        @click="openSaleDialog"
      >
        {{ building?.isForSale ? t('buildingDetail.editSale') : t('buildingDetail.sellBuilding') }}
      </button>
    </div>

    <!-- Collateral warning with loan count -->
    <p v-if="isBuildingUsedAsCollateral && !building?.destroyedAtUtc" class="collateral-warning mt-2 rounded-lg border border-amber-300/60 bg-amber-500/10 px-3 py-2 text-xs text-amber-700 dark:text-amber-300">
      <font-awesome-icon icon="lock" class="mr-1" />
      {{ t('buildingDetail.collateralBlockedByLoans', { count: collateralLoanCount }) }}
    </p>

    <!-- Destroyed notice -->
    <p v-if="building?.destroyedAtUtc" class="mt-2 rounded-lg border border-red-300/60 bg-red-500/10 px-3 py-2 text-xs text-red-700 dark:text-red-300">
      <font-awesome-icon icon="skull" class="mr-1" />
      {{ t('buildingDetail.destroyedHint') }}
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
        <div class="sale-dialog-body space-y-4">
          <!-- Building name -->
          <div class="rounded-lg border border-divider bg-surface px-4 py-3">
            <p class="mb-0.5 text-xs text-muted">{{ t('common.name') }}</p>
            <p class="text-base font-semibold text-foreground">{{ building?.name }}</p>
            <p class="text-xs text-muted">{{ formatBuildingType(building?.type ?? '') }} · {{ t('common.level') }} {{ building?.level }}</p>
          </div>

          <!-- Estimated market value reference -->
          <div
            v-if="estimatedMarketValue"
            class="estimated-market-value rounded-lg border border-divider bg-surface px-4 py-3"
          >
            <p class="mb-0.5 text-xs text-muted">{{ t('buildingDetail.estimatedMarketValue') }}</p>
            <p class="estimated-value text-base font-semibold text-foreground">
              {{ formatCurrency(estimatedMarketValue ?? 0, cityCurrencyCode) }}
            </p>
            <p class="mt-0.5 text-xs text-muted">{{ t('buildingDetail.estimatedValueHint') }}</p>
          </div>

          <div>
            <label class="form-label">{{ t('buildingDetail.askingPrice') }}</label>
            <input
              type="number"
              class="form-input"
              :placeholder="t('buildingDetail.askingPricePlaceholder')"
              :value="salePrice"
              @input="salePrice = isNaN(($event.target as HTMLInputElement).valueAsNumber) ? null : ($event.target as HTMLInputElement).valueAsNumber"
              min="1"
              step="1000"
            />
            <!-- Validation: must be positive -->
            <p v-if="salePrice !== null && salePrice <= 0" class="mt-1 text-xs text-red-500">
              {{ t('buildingDetail.askingPriceMustBePositive') }}
            </p>
            <!-- Warning: price > 150% estimated market value -->
            <p v-else-if="isPriceHigh" class="price-high-warning mt-1 rounded border border-amber-300/60 bg-amber-500/10 px-2 py-1 text-xs text-amber-700 dark:text-amber-300">
              <font-awesome-icon icon="triangle-exclamation" class="mr-1" />
              {{ t('buildingDetail.askingPriceHighWarning') }}
            </p>
          </div>

          <div class="sale-dialog-actions flex gap-2">
            <button class="btn btn-primary flex-1" :disabled="savingSale || !salePrice || salePrice <= 0" @click="setBuildingForSale(true)">
              {{ t('buildingDetail.listForSale') }}
            </button>
            <button v-if="building?.isForSale" class="btn btn-danger" :disabled="savingSale" @click="setBuildingForSale(false)">
              {{ t('buildingDetail.cancelSale') }}
            </button>
            <button class="btn btn-secondary" :disabled="savingSale" @click="closeSaleDialog">
              {{ t('common.cancel') }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </Teleport>
</template>

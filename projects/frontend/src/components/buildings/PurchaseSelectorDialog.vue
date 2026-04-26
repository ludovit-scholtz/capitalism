<script setup lang="ts">
import { inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'
import AdvancedItemSelector from '@/components/buildings/AdvancedItemSelector.vue'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const {
  building,
  showPurchaseSelector,
  selectedDraftPurchaseUnit,
  purchaseSelectorItems,
  selectedPurchaseSelection,
  purchaseVendorOptions,
  closePurchaseSelector,
  applyPurchaseSelection,
  selectPurchaseVendor,
  getPurchaseVendorTransitLabel,
  formatCurrency,
} = bd
</script>

<template>
  <div
    v-if="showPurchaseSelector"
    class="purchase-selector-page fixed inset-0 z-50 overflow-y-auto bg-black/45 p-4 backdrop-blur-sm sm:p-6"
    role="dialog"
    :aria-label="t('buildingDetail.purchaseSelector.title')"
  >
    <div class="purchase-selector-shell mx-auto mt-2 w-full max-w-6xl rounded-2xl border border-divider bg-card p-4 shadow-2xl sm:mt-6 sm:p-6">
      <div class="purchase-selector-header mb-4 flex flex-wrap items-start justify-between gap-3 border-b border-divider pb-4">
        <div>
          <p class="purchase-selector-eyebrow text-xs font-semibold uppercase tracking-[0.08em] text-muted">{{ t('buildingDetail.purchaseSelector.eyebrow') }}</p>
          <h2 class="mt-1 text-xl font-semibold text-foreground">{{ t('buildingDetail.purchaseSelector.title') }}</h2>
        </div>
        <button class="btn btn-ghost" @click="closePurchaseSelector">{{ t('common.close') }}</button>
      </div>

      <div class="purchase-selector-grid grid grid-cols-1 gap-4 lg:grid-cols-2 lg:gap-5">
        <section class="purchase-selector-card rounded-xl border border-divider bg-surface p-4">
          <AdvancedItemSelector
            :model-value="selectedPurchaseSelection"
            :items="purchaseSelectorItems"
            :label="t('buildingDetail.config.inputItem')"
            :placeholder="t('buildingDetail.selector.searchPlaceholder')"
            :empty-text="t('buildingDetail.selector.noItems')"
            @update:model-value="applyPurchaseSelection"
          />
        </section>

        <section class="purchase-selector-card rounded-xl border border-divider bg-surface p-4">
          <h3 class="text-lg font-semibold text-foreground">{{ t('buildingDetail.purchaseSelector.vendorTitle') }}</h3>
          <p class="config-help mt-1 text-sm text-muted">{{ t('buildingDetail.purchaseSelector.vendorHelp') }}</p>

          <button
            type="button"
            class="purchase-vendor-card mt-3 grid w-full gap-0.5 rounded-xl border border-divider bg-card p-3 text-left transition-colors hover:border-primary/40 hover:bg-primary/5"
            :class="{ selected: selectedDraftPurchaseUnit?.vendorLockCompanyId == null, 'border-primary bg-primary/10': selectedDraftPurchaseUnit?.vendorLockCompanyId == null }"
            @click="selectPurchaseVendor(null)"
          >
            <strong>{{ t('buildingDetail.purchaseSelector.vendorAutoTitle') }}</strong>
            <span class="text-sm text-muted">{{ t('buildingDetail.purchaseSelector.vendorAuto') }}</span>
          </button>

          <button
            type="button"
            class="purchase-vendor-card mt-2 grid w-full gap-0.5 rounded-xl border border-divider bg-card p-3 text-left transition-colors hover:border-primary/40 hover:bg-primary/5"
            :class="{
              selected: selectedDraftPurchaseUnit?.vendorLockCompanyId === building?.companyId,
              'border-primary bg-primary/10': selectedDraftPurchaseUnit?.vendorLockCompanyId === building?.companyId,
            }"
            @click="selectPurchaseVendor(building!.companyId)"
          >
            <strong>{{ t('buildingDetail.purchaseSelector.vendorOwnCompany') }}</strong>
            <span class="text-sm text-muted">{{ t('buildingDetail.purchaseSelector.vendorOwnCompanyHelp') }}</span>
          </button>

          <div v-if="purchaseVendorOptions.length > 0" class="purchase-vendor-list mt-3 grid gap-2">
            <button
              v-for="option in purchaseVendorOptions"
              :key="`${option.companyId}-${option.buildingId}`"
              type="button"
              class="purchase-vendor-card grid w-full gap-0.5 rounded-xl border border-divider bg-card p-3 text-left transition-colors hover:border-primary/40 hover:bg-primary/5"
              :class="{
                selected: selectedDraftPurchaseUnit?.vendorLockCompanyId === option.companyId,
                'border-primary bg-primary/10': selectedDraftPurchaseUnit?.vendorLockCompanyId === option.companyId,
              }"
              @click="selectPurchaseVendor(option.companyId)"
            >
              <strong>{{ option.companyName }}</strong>
              <span class="text-sm text-muted">{{ option.buildingName }}</span>
              <span v-if="option.pricePerUnit != null" class="purchase-vendor-pricing text-xs font-medium text-muted">
                {{ t('buildingDetail.purchaseSelector.vendorPrice', { price: formatCurrency(option.pricePerUnit) }) }}
              </span>
              <span class="purchase-vendor-pricing text-xs font-medium text-muted">
                {{ getPurchaseVendorTransitLabel(option.transitCostPerUnit) }}
              </span>
            </button>
          </div>
          <p v-else class="config-help mt-3 rounded-lg border border-dashed border-divider bg-surface-muted px-3 py-2 text-sm text-muted">{{ t('buildingDetail.purchaseSelector.vendorEmpty') }}</p>
        </section>
      </div>

      <div class="purchase-selector-actions mt-5 flex justify-end border-t border-divider pt-4">
        <button class="btn btn-primary" @click="closePurchaseSelector">{{ t('buildingDetail.purchaseSelector.done') }}</button>
      </div>
    </div>
  </div>
</template>

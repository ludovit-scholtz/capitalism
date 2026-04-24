<script setup lang="ts">
import { inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'
import AdvancedItemSelector from '@/components/buildings/AdvancedItemSelector.vue'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const { building, showPurchaseSelector, selectedDraftPurchaseUnit, purchaseSelectorItems, selectedPurchaseSelection, purchaseVendorOptions, closePurchaseSelector, applyPurchaseSelection, selectPurchaseVendor, getPurchaseVendorTransitLabel, formatCurrency } = bd
</script>

<template>

<div v-if="showPurchaseSelector" class="purchase-selector-page" role="dialog" :aria-label="t('buildingDetail.purchaseSelector.title')">
  <div class="purchase-selector-shell">
    <div class="purchase-selector-header">
      <div>
        <p class="purchase-selector-eyebrow">{{ t('buildingDetail.purchaseSelector.eyebrow') }}</p>
        <h2>{{ t('buildingDetail.purchaseSelector.title') }}</h2>
      </div>
      <button class="btn btn-ghost" @click="closePurchaseSelector">{{ t('common.close') }}</button>
    </div>

    <div class="purchase-selector-grid">
      <section class="purchase-selector-card">
        <AdvancedItemSelector
          :model-value="selectedPurchaseSelection"
          :items="purchaseSelectorItems"
          :label="t('buildingDetail.config.inputItem')"
          :placeholder="t('buildingDetail.selector.searchPlaceholder')"
          :empty-text="t('buildingDetail.selector.noItems')"
          @update:model-value="applyPurchaseSelection"
        />
      </section>

      <section class="purchase-selector-card">
        <h3>{{ t('buildingDetail.purchaseSelector.vendorTitle') }}</h3>
        <p class="config-help">{{ t('buildingDetail.purchaseSelector.vendorHelp') }}</p>

        <button type="button" class="purchase-vendor-card" :class="{ selected: selectedDraftPurchaseUnit?.vendorLockCompanyId == null }" @click="selectPurchaseVendor(null)">
          <strong>{{ t('buildingDetail.purchaseSelector.vendorAutoTitle') }}</strong>
          <span>{{ t('buildingDetail.purchaseSelector.vendorAuto') }}</span>
        </button>

        <button
          type="button"
          class="purchase-vendor-card"
          :class="{ selected: selectedDraftPurchaseUnit?.vendorLockCompanyId === building?.companyId }"
          @click="selectPurchaseVendor(building!.companyId)"
        >
          <strong>{{ t('buildingDetail.purchaseSelector.vendorOwnCompany') }}</strong>
          <span>{{ t('buildingDetail.purchaseSelector.vendorOwnCompanyHelp') }}</span>
        </button>

        <div v-if="purchaseVendorOptions.length > 0" class="purchase-vendor-list">
          <button
            v-for="option in purchaseVendorOptions"
            :key="`${option.companyId}-${option.buildingId}`"
            type="button"
            class="purchase-vendor-card"
            :class="{ selected: selectedDraftPurchaseUnit?.vendorLockCompanyId === option.companyId }"
            @click="selectPurchaseVendor(option.companyId)"
          >
            <strong>{{ option.companyName }}</strong>
            <span>{{ option.buildingName }}</span>
            <span v-if="option.pricePerUnit != null" class="purchase-vendor-pricing">
              {{ t('buildingDetail.purchaseSelector.vendorPrice', { price: formatCurrency(option.pricePerUnit) }) }}
            </span>
            <span class="purchase-vendor-pricing">
              {{ getPurchaseVendorTransitLabel(option.transitCostPerUnit) }}
            </span>
          </button>
        </div>
        <p v-else class="config-help">{{ t('buildingDetail.purchaseSelector.vendorEmpty') }}</p>
      </section>
    </div>

    <div class="purchase-selector-actions">
      <button class="btn btn-primary" @click="closePurchaseSelector">{{ t('buildingDetail.purchaseSelector.done') }}</button>
    </div>
  </div>
</div>

</template>

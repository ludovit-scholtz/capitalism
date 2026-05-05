<script setup lang="ts">
import { computed, inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'
import AdvancedItemSelector from '@/components/buildings/AdvancedItemSelector.vue'
import ProductPicker from '@/components/buildings/ProductPicker.vue'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const {
  locale,
  building,
  selectedCell,
  resourceTypes,
  rankedProducts,
  rankedProductsLoading,
  cities,
  cityMediaHouses,
  cityMediaHousesLoading,
  cityCurrencyCode,
  cityFxRate,
  b2bPriceSource,
  b2bSuggestedPrice,
  b2bHasUpstreamSource,
  publicSalesFilteredRankedProducts,
  b2bSalesFilteredRankedProducts,
  selectedDraftPurchaseUnit,
  selectedDraftMediaHouse,
  selectedPurchaseSelection,
  selectedPurchaseVendorSummary,
  getDraftUnitAt,
  getItemSelection,
  setItemSelection,
  getManufacturingSelectableItems,
  openPurchaseSelector,
  updateSelectedUnitConfig,
  formatCurrency,
  formatPercent,
  formatUnitQuantity,
  getResourceName,
  getProductName,
  getLocalizedIndustry,
  SUPPORTED_INDUSTRIES,
} = bd

/** City-average reference price for the selected PUBLIC_SALES draft unit's product (local currency). */
const publicSalesCityAveragePrice = computed<number | null>(() => {
  if (!selectedCell.value) return null
  const unit = getDraftUnitAt(selectedCell.value.x, selectedCell.value.y)
  if (unit?.unitType !== 'PUBLIC_SALES' || !unit.productTypeId) return null
  const product = rankedProducts.value.find((r) => r.productType.id === unit.productTypeId)?.productType
  if (!product) return null
  return Math.round(product.basePrice * cityFxRate.value * 100) / 100
})

/** Price tier relative to city average for the selected PUBLIC_SALES draft unit. */
const publicSalesPriceTier = computed<'below' | 'at' | 'above' | null>(() => {
  if (!selectedCell.value) return null
  const unit = getDraftUnitAt(selectedCell.value.x, selectedCell.value.y)
  if (unit?.unitType !== 'PUBLIC_SALES') return null
  const avg = publicSalesCityAveragePrice.value
  const price = unit.minPrice
  if (avg == null || price == null) return null
  if (price < avg * 0.98) return 'below'
  if (price > avg * 1.02) return 'above'
  return 'at'
})

const mineLotResource = computed(() => {
  const resourceTypeId = building.value?.lotResourceTypeId ?? null
  if (!resourceTypeId) return null
  return resourceTypes.value.find((resource) => resource.id === resourceTypeId) ?? null
})

const mineOutputResourceOptions = computed(() => {
  if (building.value?.type !== 'MINE') {
    return resourceTypes.value
  }

  if (!mineLotResource.value) {
    return resourceTypes.value
  }

  return [mineLotResource.value]
})

const mineLotQuality = computed(() => {
  return building.value?.lotMaterialQuality ?? null
})

const mineLotReserve = computed(() => {
  return building.value?.lotMaterialQuantity ?? null
})
</script>

<template>
  <div class="unit-config-fields" v-if="selectedCell && getDraftUnitAt(selectedCell.x, selectedCell.y)">
    <h5>{{ t('buildingDetail.unitSettings') }}</h5>

    <!-- Purchase unit config -->
    <template v-if="getDraftUnitAt(selectedCell.x, selectedCell.y)!.unitType === 'PURCHASE'">
      <!-- Factory-specific onboarding guide for the Purchase unit -->
      <p v-if="building?.type === 'FACTORY'" class="config-onboarding-hint">
        {{ t('buildingDetail.config.factoryPurchaseGuide') }}
      </p>
      <div class="config-field">
        <label class="config-label">{{ t('buildingDetail.config.inputItem') }}</label>
        <button type="button" class="btn btn-secondary purchase-selector-trigger" @click="openPurchaseSelector">
          {{ selectedPurchaseSelection ? t('buildingDetail.purchaseSelector.changeSelection') : t('buildingDetail.purchaseSelector.chooseSelection') }}
        </button>
        <div class="purchase-selection-summary">
          <strong>
            {{
              selectedPurchaseSelection
                ? selectedPurchaseSelection.kind === 'resource'
                  ? getResourceName(selectedDraftPurchaseUnit?.resourceTypeId ?? null)
                  : getProductName(selectedDraftPurchaseUnit?.productTypeId ?? null)
                : t('buildingDetail.purchaseSelector.notSelected')
            }}
          </strong>
          <span v-if="selectedPurchaseVendorSummary" class="purchase-selection-meta">
            {{ selectedPurchaseVendorSummary }}
          </span>
          <span v-else class="purchase-selection-meta">
            {{ t('buildingDetail.purchaseSelector.vendorAuto') }}
          </span>
        </div>
      </div>
      <p class="config-help">{{ t('buildingDetail.proAccessHint') }}</p>
      <div class="config-field">
        <label class="config-label"
          >{{ t('buildingDetail.config.maxPrice') }} <span class="currency-badge">{{ cityCurrencyCode }}</span></label
        >
        <input
          type="number"
          class="form-input"
          :value="getDraftUnitAt(selectedCell.x, selectedCell.y)!.maxPrice"
          @input="updateSelectedUnitConfig('maxPrice', ($event.target as HTMLInputElement).valueAsNumber || null)"
          min="0"
          step="0.01"
        />
      </div>
      <div class="config-field">
        <label class="config-label">{{ t('buildingDetail.config.minQuality') }}</label>
        <input
          type="number"
          class="form-input"
          :value="getDraftUnitAt(selectedCell.x, selectedCell.y)!.minQuality"
          @input="updateSelectedUnitConfig('minQuality', ($event.target as HTMLInputElement).valueAsNumber || null)"
          min="0"
          max="1"
          step="0.01"
        />
      </div>
      <div class="config-field">
        <label class="config-label">{{ t('buildingDetail.config.procurementMode') }}</label>
        <p class="config-help">{{ t('buildingDetail.config.procurementModeHelp') }}</p>
        <div class="procurement-mode-options">
          <label
            v-for="mode in ['OPTIMAL', 'EXCHANGE', 'LOCAL']"
            :key="mode"
            class="procurement-mode-option"
            :class="{ selected: (getDraftUnitAt(selectedCell.x, selectedCell.y)!.purchaseSource ?? 'OPTIMAL') === mode }"
          >
            <input
              type="radio"
              :name="`procurement-mode-${selectedCell.x}-${selectedCell.y}`"
              :value="mode"
              :checked="(getDraftUnitAt(selectedCell.x, selectedCell.y)!.purchaseSource ?? 'OPTIMAL') === mode"
              @change="updateSelectedUnitConfig('purchaseSource', mode)"
              class="procurement-mode-radio"
            />
            <span class="procurement-mode-label">{{ t(`buildingDetail.config.procurementMode_${mode}`) }}</span>
            <span class="procurement-mode-desc">{{ t(`buildingDetail.config.procurementModeDesc_${mode}`) }}</span>
          </label>
        </div>
      </div>

      <!-- City lock (shown when EXCHANGE mode is selected) -->
      <div class="config-field" v-if="(getDraftUnitAt(selectedCell.x, selectedCell.y)!.purchaseSource ?? 'OPTIMAL') === 'EXCHANGE'">
        <label class="config-label">{{ t('buildingDetail.config.lockedCity') }}</label>
        <p class="config-help">{{ t('buildingDetail.config.lockedCityHelp') }}</p>
        <select
          class="form-input"
          :value="getDraftUnitAt(selectedCell.x, selectedCell.y)!.lockedCityId ?? ''"
          @change="updateSelectedUnitConfig('lockedCityId', ($event.target as HTMLSelectElement).value || null)"
        >
          <option value="">{{ t('buildingDetail.config.lockedCityAny') }}</option>
          <option v-for="city in cities" :key="city.id" :value="city.id">{{ city.name }}</option>
        </select>
      </div>
    </template>

    <!-- Manufacturing unit config -->
    <template v-if="getDraftUnitAt(selectedCell.x, selectedCell.y)!.unitType === 'MANUFACTURING'">
      <!-- Factory-specific onboarding guide for the Manufacturing unit -->
      <p v-if="building?.type === 'FACTORY'" class="config-onboarding-hint">
        {{ t('buildingDetail.config.factoryManufacturingGuide') }}
      </p>
      <div class="config-field">
        <AdvancedItemSelector
          :model-value="getItemSelection(getDraftUnitAt(selectedCell.x, selectedCell.y))"
          :items="getManufacturingSelectableItems(getDraftUnitAt(selectedCell.x, selectedCell.y))"
          :label="t('buildingDetail.config.outputProduct')"
          :placeholder="t('buildingDetail.selector.searchPlaceholder')"
          :empty-text="t('buildingDetail.selector.noLinkedOutputs')"
          @update:model-value="setItemSelection(getDraftUnitAt(selectedCell.x, selectedCell.y), $event)"
        />
      </div>
      <p class="config-help">
        {{ t('buildingDetail.config.outputProductHelp') }}
      </p>
      <p class="config-help">{{ t('buildingDetail.proAccessHint') }}</p>
    </template>

    <!-- B2B Sales unit config -->
    <template v-if="getDraftUnitAt(selectedCell.x, selectedCell.y)!.unitType === 'B2B_SALES'">
      <!-- No-source warning: shown when no MANUFACTURING or MINING unit has an item configured -->
      <div v-if="!b2bHasUpstreamSource" class="b2b-no-source-warning" role="alert" :aria-label="t('buildingDetail.accessibility.noUpstreamSource')">
        <span class="b2b-no-source-icon" aria-hidden="true">⚠️</span>
        <div class="b2b-no-source-content">
          <p class="b2b-no-source-title">{{ t('buildingDetail.config.b2bNoSourceTitle') }}</p>
          <p class="b2b-no-source-body">{{ t('buildingDetail.config.b2bNoSourceBody') }}</p>
        </div>
      </div>
      <div class="config-field">
        <label class="config-label">{{ t('buildingDetail.config.productType') }}</label>
        <ProductPicker
          :model-value="getDraftUnitAt(selectedCell.x, selectedCell.y)!.productTypeId ?? null"
          :ranked-products="b2bSalesFilteredRankedProducts"
          :loading="rankedProductsLoading"
          :allow-none="true"
          none-label-key="buildingDetail.config.none"
          help-text-key="buildingDetail.config.b2bProductPickerHelp"
          empty-state-key="buildingDetail.config.b2bProductPickerEmpty"
          @update:model-value="updateSelectedUnitConfig('productTypeId', $event)"
        />
      </div>
      <div class="config-field">
        <label class="config-label"
          >{{ t('buildingDetail.config.minPrice') }} <span class="currency-badge">{{ cityCurrencyCode }}</span></label
        >
        <input
          type="number"
          class="form-input"
          :value="getDraftUnitAt(selectedCell.x, selectedCell.y)!.minPrice"
          @input="updateSelectedUnitConfig('minPrice', ($event.target as HTMLInputElement).value !== '' ? ($event.target as HTMLInputElement).valueAsNumber : null)"
          min="0.01"
          step="0.01"
        />
        <p v-if="b2bPriceSource !== null" class="config-help config-price-hint">
          {{
            t(b2bPriceSource!.sourceType === 'manufacturing' ? 'buildingDetail.config.b2bPriceFromMfg' : 'buildingDetail.config.b2bPriceFromMining', {
              item: b2bPriceSource!.itemName ?? '?',
              price: formatCurrency(b2bPriceSource!.price),
            })
          }}
          <button type="button" class="btn-link" @click="updateSelectedUnitConfig('minPrice', b2bSuggestedPrice)">{{ t('buildingDetail.config.b2bUseSuggested') }}</button>
        </p>
      </div>
      <div class="config-field">
        <label class="config-label">{{ t('buildingDetail.config.saleVisibility') }}</label>
        <select
          class="form-input"
          :value="getDraftUnitAt(selectedCell.x, selectedCell.y)!.saleVisibility ?? ''"
          @change="updateSelectedUnitConfig('saleVisibility', ($event.target as HTMLSelectElement).value || null)"
        >
          <option value="">{{ t('buildingDetail.config.none') }}</option>
          <option value="PUBLIC">{{ t('buildingDetail.config.visibilityPublic') }}</option>
          <option value="COMPANY">{{ t('buildingDetail.config.visibilityCompany') }}</option>
          <option value="GROUP">{{ t('buildingDetail.config.visibilityGroup') }}</option>
        </select>
      </div>
    </template>

    <!-- Public Sales unit config -->
    <template v-if="getDraftUnitAt(selectedCell.x, selectedCell.y)!.unitType === 'PUBLIC_SALES'">
      <div class="config-field">
        <label class="config-label">{{ t('buildingDetail.config.productType') }}</label>
        <ProductPicker
          :model-value="getDraftUnitAt(selectedCell.x, selectedCell.y)!.productTypeId ?? null"
          :ranked-products="publicSalesFilteredRankedProducts"
          :loading="rankedProductsLoading"
          :allow-none="true"
          none-label-key="buildingDetail.config.none"
          help-text-key="buildingDetail.config.publicSalesProductPickerHelp"
          empty-state-key="buildingDetail.config.publicSalesProductPickerEmpty"
          @update:model-value="updateSelectedUnitConfig('productTypeId', $event)"
        />
      </div>
      <p class="config-help">{{ t('buildingDetail.proAccessHint') }}</p>
      <div class="config-field">
        <label class="config-label"
          >{{ t('buildingDetail.config.minPrice') }} <span class="currency-badge">{{ cityCurrencyCode }}</span></label
        >
        <input
          type="number"
          class="form-input"
          :value="getDraftUnitAt(selectedCell.x, selectedCell.y)!.minPrice"
          @input="updateSelectedUnitConfig('minPrice', ($event.target as HTMLInputElement).value !== '' ? ($event.target as HTMLInputElement).valueAsNumber : null)"
          :min="publicSalesCityAveragePrice ?? 0.01"
          step="0.01"
        />
      </div>

      <!-- Pricing guidance panel -->
      <div v-if="publicSalesCityAveragePrice != null" class="public-sales-pricing-guide">
        <div class="pricing-guide-header">
          <span class="pricing-guide-label">{{ t('buildingDetail.config.cityAveragePriceLabel') }}</span>
          <span class="pricing-guide-value">{{ formatCurrency(publicSalesCityAveragePrice) }} {{ cityCurrencyCode }}</span>
        </div>
        <div v-if="publicSalesPriceTier != null" class="pricing-tier-badge" :class="`pricing-tier-badge--${publicSalesPriceTier}`">
          {{ t(`buildingDetail.config.priceTier.${publicSalesPriceTier}`) }}
        </div>
        <p class="pricing-guide-hint">
          <template v-if="publicSalesPriceTier === 'below'">{{ t('buildingDetail.config.priceTierHint.below') }}</template>
          <template v-else-if="publicSalesPriceTier === 'above'">{{ t('buildingDetail.config.priceTierHint.above') }}</template>
          <template v-else>{{ t('buildingDetail.config.priceTierHint.at') }}</template>
        </p>
        <p class="pricing-guide-brand-hint">{{ t('buildingDetail.config.brandMomentumHint') }}</p>
      </div>
    </template>

    <!-- Marketing unit config -->
    <template v-if="getDraftUnitAt(selectedCell.x, selectedCell.y)!.unitType === 'MARKETING'">
      <div class="config-field">
        <label class="config-label">{{ t('buildingDetail.config.budget') }}</label>
        <input
          type="number"
          class="form-input"
          :value="getDraftUnitAt(selectedCell.x, selectedCell.y)!.budget"
          @input="updateSelectedUnitConfig('budget', ($event.target as HTMLInputElement).valueAsNumber || null)"
          min="0"
          step="100"
        />
      </div>
      <div class="config-field">
        <label class="config-label">{{ t('buildingDetail.config.mediaHouse') }}</label>
        <div v-if="cityMediaHousesLoading" class="config-loading">{{ t('common.loading') }}</div>
        <div v-else-if="cityMediaHouses.length === 0" class="config-hint">
          {{ t('buildingDetail.config.noMediaHouseAvailable') }}
        </div>
        <div v-else class="media-house-picker flex flex-col gap-2">
          <select
            class="form-input media-house-combobox"
            :value="getDraftUnitAt(selectedCell.x, selectedCell.y)!.mediaHouseBuildingId ?? ''"
            @change="updateSelectedUnitConfig('mediaHouseBuildingId', ($event.target as HTMLSelectElement).value || null)"
          >
            <option value="">{{ t('buildingDetail.config.noMediaHouse') }}</option>
            <option v-for="mh in cityMediaHouses" :key="mh.id" :value="mh.id" :disabled="mh.isUnderConstruction || mh.powerStatus === 'OFFLINE'">
              {{ mh.name }} · {{ mh.mediaType ?? '?' }} · ×{{ mh.effectivenessMultiplier.toFixed(1) }} · {{ t('buildingDetail.config.contentRanking') }} {{ mh.contentRanking.toFixed(0) }}%
              {{ mh.isGovernmentOwned ? ` · ${t('buildingDetail.config.govBadge')}` : '' }}
              {{ mh.ownerCompanyId === building?.companyId ? ` · ${t('buildingDetail.config.yourStation')}` : '' }}
              {{ mh.isUnderConstruction ? ` · ${t('buildingDetail.config.underConstruction')}` : '' }}
              {{ mh.powerStatus === 'OFFLINE' ? ` · ${t('buildingDetail.config.offline')}` : '' }}
            </option>
          </select>

          <div v-if="selectedDraftMediaHouse" class="rounded-lg border border-divider bg-surface p-2 text-xs text-muted">
            <p class="font-medium text-foreground">{{ selectedDraftMediaHouse.name }} ({{ selectedDraftMediaHouse.mediaType ?? '?' }})</p>
            <p>{{ selectedDraftMediaHouse.cityName }} · ×{{ selectedDraftMediaHouse.effectivenessMultiplier.toFixed(1) }}</p>
            <p>{{ t('buildingDetail.config.contentRanking') }}: {{ selectedDraftMediaHouse.contentRanking.toFixed(0) }}%</p>
          </div>
        </div>
        <p v-if="selectedDraftMediaHouse" class="config-hint">{{ t('buildingDetail.config.channelEffect') }} ×{{ selectedDraftMediaHouse.effectivenessMultiplier.toFixed(1) }}</p>
      </div>
    </template>

    <!-- Branding unit config -->
    <template v-if="getDraftUnitAt(selectedCell.x, selectedCell.y)!.unitType === 'BRANDING'">
      <div class="config-field">
        <label class="config-label">{{ t('buildingDetail.config.brandScope') }}</label>
        <select
          class="form-input"
          :value="getDraftUnitAt(selectedCell.x, selectedCell.y)!.brandScope ?? ''"
          @change="updateSelectedUnitConfig('brandScope', ($event.target as HTMLSelectElement).value || null)"
        >
          <option value="">{{ t('buildingDetail.config.none') }}</option>
          <option value="PRODUCT">{{ t('buildingDetail.config.scopeProduct') }}</option>
          <option value="CATEGORY">{{ t('buildingDetail.config.scopeCategory') }}</option>
          <option value="COMPANY">{{ t('buildingDetail.config.scopeCompany') }}</option>
        </select>
      </div>
    </template>

    <template v-if="getDraftUnitAt(selectedCell.x, selectedCell.y)!.unitType === 'PRODUCT_QUALITY'">
      <div class="config-field">
        <label class="config-label">{{ t('buildingDetail.config.researchProduct') }}</label>
        <ProductPicker
          :model-value="getDraftUnitAt(selectedCell.x, selectedCell.y)!.productTypeId ?? null"
          :ranked-products="rankedProducts"
          :loading="rankedProductsLoading"
          :allow-none="true"
          :rd-context="true"
          none-label-key="buildingDetail.config.none"
          help-text-key="productPicker.rdProductHelp"
          empty-state-key="productPicker.rdProductEmpty"
          @update:model-value="updateSelectedUnitConfig('productTypeId', $event)"
        />
      </div>
      <p class="config-help">{{ t('buildingDetail.config.researchProductHelp') }}</p>
      <p class="config-help">{{ t('buildingDetail.proAccessHint') }}</p>
    </template>

    <template v-if="getDraftUnitAt(selectedCell.x, selectedCell.y)!.unitType === 'BRAND_QUALITY'">
      <div class="config-field">
        <label class="config-label">{{ t('buildingDetail.config.brandScope') }}</label>
        <select
          class="form-input"
          :value="getDraftUnitAt(selectedCell.x, selectedCell.y)!.brandScope ?? ''"
          @change="updateSelectedUnitConfig('brandScope', ($event.target as HTMLSelectElement).value || null)"
        >
          <option value="">{{ t('buildingDetail.config.none') }}</option>
          <option value="PRODUCT">{{ t('buildingDetail.config.scopeProduct') }}</option>
          <option value="CATEGORY">{{ t('buildingDetail.config.scopeCategory') }}</option>
          <option value="COMPANY">{{ t('buildingDetail.config.scopeCompany') }}</option>
        </select>
      </div>
      <!-- PRODUCT scope: pick a specific product -->
      <div v-if="getDraftUnitAt(selectedCell.x, selectedCell.y)!.brandScope === 'PRODUCT'" class="config-field">
        <label class="config-label">{{ t('buildingDetail.config.researchAnchorProduct') }}</label>
        <ProductPicker
          :model-value="getDraftUnitAt(selectedCell.x, selectedCell.y)!.productTypeId ?? null"
          :ranked-products="rankedProducts"
          :loading="rankedProductsLoading"
          :allow-none="true"
          :rd-context="true"
          none-label-key="buildingDetail.config.none"
          help-text-key="productPicker.rdProductHelp"
          empty-state-key="productPicker.rdProductEmpty"
          @update:model-value="updateSelectedUnitConfig('productTypeId', $event)"
        />
        <p class="config-help">{{ t('buildingDetail.config.researchAnchorProductHelp') }}</p>
      </div>
      <!-- CATEGORY scope: pick an industry category directly -->
      <div v-if="getDraftUnitAt(selectedCell.x, selectedCell.y)!.brandScope === 'CATEGORY'" class="config-field">
        <label class="config-label">{{ t('buildingDetail.config.researchIndustryCategory') }}</label>
        <select
          class="form-input"
          :value="getDraftUnitAt(selectedCell.x, selectedCell.y)!.industryCategory ?? ''"
          @change="updateSelectedUnitConfig('industryCategory', ($event.target as HTMLSelectElement).value || null)"
        >
          <option value="">{{ t('buildingDetail.config.none') }}</option>
          <option v-for="ind in SUPPORTED_INDUSTRIES" :key="ind" :value="ind">
            {{ getLocalizedIndustry(ind, locale) }}
          </option>
        </select>
        <p class="config-help">{{ t('buildingDetail.config.researchIndustryCategoryHelp') }}</p>
      </div>
      <p class="config-help">{{ t('buildingDetail.config.researchBrandHelp') }}</p>
      <p class="config-help">{{ t('buildingDetail.proAccessHint') }}</p>
    </template>

    <!-- Storage unit config — no configuration needed; storage is universal -->
    <template v-if="getDraftUnitAt(selectedCell.x, selectedCell.y)!.unitType === 'STORAGE'">
      <p class="config-help">{{ t('buildingDetail.config.storageUniversalInfo') }}</p>
    </template>

    <template v-if="getDraftUnitAt(selectedCell.x, selectedCell.y)!.unitType === 'MINING'">
      <div class="config-field">
        <label class="config-label">{{ t('buildingDetail.config.outputResource') }}</label>
        <select
          class="form-input"
          :value="getDraftUnitAt(selectedCell.x, selectedCell.y)!.resourceTypeId ?? ''"
          @change="updateSelectedUnitConfig('resourceTypeId', ($event.target as HTMLSelectElement).value || null)"
        >
          <option value="">{{ t('buildingDetail.config.none') }}</option>
          <option v-for="rt in mineOutputResourceOptions" :key="rt.id" :value="rt.id">{{ rt.name }} ({{ rt.unitSymbol }})</option>
        </select>
        <p v-if="building?.type === 'MINE' && mineLotResource" class="config-help">
          {{ t('buildingDetail.config.mineOutputLockedToLot', { resource: mineLotResource.name }) }}
        </p>
        <p v-else-if="building?.type === 'MINE'" class="config-help">
          {{ t('buildingDetail.config.mineOutputMissingLotResource') }}
        </p>
      </div>
      <div v-if="building?.type === 'MINE'" class="config-field">
        <label class="config-label">{{ t('cityMap.rawMaterialQuality') }}</label>
        <p class="config-help">
          {{ mineLotQuality != null ? formatPercent(mineLotQuality) : t('common.notAvailable') }}
        </p>
        <label class="config-label">{{ t('cityMap.rawMaterialQuantity') }}</label>
        <p class="config-help">
          {{ mineLotReserve != null ? `${formatUnitQuantity(mineLotReserve)} ${t('cityMap.rawMaterialQuantityUnit')}` : t('common.notAvailable') }}
        </p>
      </div>
    </template>
  </div>
</template>

<style scoped src="./BuildingSidebar.shared.css"></style>

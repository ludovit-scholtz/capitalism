<script setup lang="ts">
import { inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'
import type { EditableGridUnit } from '@/composables/useBuildingDetail'
import type { ExchangeSortBy } from '@/lib/globalExchange'
import AdvancedItemSelector from '@/components/buildings/AdvancedItemSelector.vue'
import BuildingBankAccountPanel from '@/components/buildings/BuildingBankAccountPanel.vue'
import ProductPicker from '@/components/buildings/ProductPicker.vue'
import UnitResourceHistoryPanel from '@/components/buildings/UnitResourceHistoryPanel.vue'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const {
  locale,
  building,
  isEditing,
  selectedCell,
  showUnitPicker,
  resourceTypes,
  rankedProducts,
  rankedProductsLoading,
  cities,
  exchangeOffersLoading,
  exchangeSortBy,
  cityMediaHouses,
  cityMediaHousesLoading,
  selectedHistoryItemKey,
  schedulingUpgrade,
  unitUpgradeError,
  plannedUnits,
  allowedUnits,
  selectedPurchaseUnit,
  selectedDraftPurchaseUnit,
  publicSalesFilteredRankedProducts,
  b2bSalesFilteredRankedProducts,
  selectedHistoryItemOptions,
  selectedUnitResourceHistory,
  cityCurrencyCode,
  b2bPriceSource,
  b2bSuggestedPrice,
  b2bHasUpstreamSource,
  exchangeOfferItems,
  allExchangeOffersBlocked,
  bestExchangeOfferCityId,
  logisticsTrapWarning,
  selectedPurchaseResourceSlug,
  selectedPurchaseSelection,
  selectedPurchaseVendorSummary,
  selectedDraftMediaHouse,
  selectedCellPendingUpgrade,
  selectedCellUpgradeInfo,
  isSelectedCellStaged,
  selectedPlannedUnitFlowSegments,
  setReadOnlySelectedCell,
  placeUnit,
  removeDraftUnit,
  getDraftUnitAt,
  getDisplayedTicks,
  getItemSelection,
  setItemSelection,
  openPurchaseSelector,
  getManufacturingSelectableItems,
  getResourceName,
  getProductName,
  getBrandScopeLabel,
  getUnitInventorySummary,
  getUnitInventories,
  getUnitInventoryItemCount,
  toggleStagedUpgrade,
  updateSelectedUnitConfig,
  formatCurrency,
  formatTickDuration,
  formatPercent,
  formatUnitQuantity,
  getUnitColor,
  getUnitAtFrom,
  getInventoryItemImageUrl,
  getInventoryItemMonogram,
  getInventoryItemSourcingCostPerUnitLabel,
  getInventoryItemName,
  getInventoryItemSourcingCostLabel,
  getDraftUnitConstructionCostLabel,
  getUnitInventoryCostLabel,
  getUnitConstructionCost,
  getLocalizedIndustry,
  loadBuilding,
  submitUnitUpgrade,
  SUPPORTED_INDUSTRIES,
} = bd
</script>

<template>
  <div class="sidebar" v-if="selectedCell && isEditing">
    <div v-if="showUnitPicker" class="unit-picker">
      <div class="picker-header">
        <h3>{{ t('buildingDetail.selectUnitType') }}</h3>
        <button class="btn btn-ghost" @click="showUnitPicker = false">{{ t('common.close') }}</button>
      </div>
      <p class="picker-subtitle">{{ t('buildingDetail.allowedUnits') }}</p>
      <div class="picker-grid">
        <button v-for="unitType in allowedUnits" :key="unitType" class="picker-option" @click="placeUnit(unitType)">
          <span class="picker-color" :style="{ background: getUnitColor(unitType) }"></span>
          <div class="picker-info">
            <span class="picker-name">{{ t(`buildingDetail.unitTypes.${unitType}`) }}</span>
            <span class="picker-desc">{{ t(`buildingDetail.unitDescriptions.${unitType}`) }}</span>
            <span class="picker-cost">{{ t('buildingDetail.unitCost', { cost: formatCurrency(getUnitConstructionCost(unitType)) }) }}</span>
          </div>
        </button>
      </div>
    </div>

    <div v-if="!showUnitPicker && getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)" class="unit-config">
      <div class="unit-config-header">
        <h3>{{ t('buildingDetail.unitConfiguration') }}</h3>
        <button class="btn btn-ghost" @click="setReadOnlySelectedCell(null)">{{ t('common.close') }}</button>
      </div>
      <div class="unit-detail">
        <h4>{{ t(`buildingDetail.unitTypes.${getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.unitType}`) }}</h4>
        <p class="unit-desc">{{ t(`buildingDetail.unitDescriptions.${getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.unitType}`) }}</p>
        <div class="unit-stats">
          <span class="stat">{{ t('common.level') }}: {{ getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.level }}</span>
          <span class="stat">{{ t('buildingDetail.gridPosition', { x: selectedCell.x, y: selectedCell.y }) }}</span>
          <span v-if="getDraftUnitConstructionCostLabel(getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y))" class="stat">
            {{ t('buildingDetail.unitCost', { cost: getDraftUnitConstructionCostLabel(getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)) }) }}
          </span>
          <span
            class="stat"
            v-if="getDisplayedTicks(getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!) > 0"
            :title="getDisplayedTicks(getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!) + ' ticks'"
          >
            {{ t('buildingDetail.unitUnavailableFor', { time: formatTickDuration(getDisplayedTicks(getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!), locale) }) }}
          </span>
        </div>
        <div class="unit-links">
          <span class="link-label">{{ t('buildingDetail.links') }}:</span>
          <span v-if="getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.linkUp" class="link-badge">{{ t('buildingDetail.linkUp') }}</span>
          <span v-if="getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.linkDown" class="link-badge">{{ t('buildingDetail.linkDown') }}</span>
          <span v-if="getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.linkLeft" class="link-badge">{{ t('buildingDetail.linkLeft') }}</span>
          <span v-if="getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.linkRight" class="link-badge">{{ t('buildingDetail.linkRight') }}</span>
          <span v-if="getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.linkUpLeft" class="link-badge">{{ t('buildingDetail.linkUpLeft') }}</span>
          <span v-if="getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.linkUpRight" class="link-badge">{{ t('buildingDetail.linkUpRight') }}</span>
          <span v-if="getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.linkDownLeft" class="link-badge">{{ t('buildingDetail.linkDownLeft') }}</span>
          <span v-if="getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.linkDownRight" class="link-badge">{{ t('buildingDetail.linkDownRight') }}</span>
        </div>

        <div class="unit-insight-card">
          <h5>{{ t('buildingBankAccount.assignmentTitle') }}</h5>
          <BuildingBankAccountPanel
            :building-id="building?.id ?? ''"
            :company-id="building?.companyId ?? ''"
            :currency-code="cityCurrencyCode"
            :loading="false"
            @updated="loadBuilding"
          />
        </div>

        <!-- Unit-specific configuration -->
        <div class="unit-config-fields" v-if="isEditing && getDraftUnitAt(selectedCell.x, selectedCell.y)">
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
              <span class="b2b-no-source-icon" aria-hidden="true">⚠</span>
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
                min="0.01"
                step="0.01"
              />
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
                <option v-for="rt in resourceTypes" :key="rt.id" :value="rt.id">{{ rt.name }} ({{ rt.unitSymbol }})</option>
              </select>
            </div>
          </template>
        </div>

        <!-- Read-only unit details for non-editing mode -->
        <div class="unit-config-readonly" v-if="!isEditing && getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)">
          <template v-if="getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.unitType === 'PURCHASE' && 'resourceTypeId' in getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!">
            <span class="stat" v-if="(getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y) as EditableGridUnit).resourceTypeId"
              >{{ t('buildingDetail.config.resourceType') }}: {{ getResourceName((getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y) as EditableGridUnit).resourceTypeId) }}</span
            >
            <span class="stat" v-if="(getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y) as EditableGridUnit).productTypeId"
              >{{ t('buildingDetail.config.productType') }}: {{ getProductName((getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y) as EditableGridUnit).productTypeId) }}</span
            >
          </template>
          <template v-if="getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.unitType === 'PRODUCT_QUALITY'">
            <span class="stat" v-if="(getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y) as EditableGridUnit).productTypeId">
              {{ t('buildingDetail.config.researchProduct') }}: {{ getProductName((getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y) as EditableGridUnit).productTypeId) }}
            </span>
          </template>
          <template v-if="getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.unitType === 'BRAND_QUALITY'">
            <span class="stat" v-if="(getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y) as EditableGridUnit).brandScope">
              {{ t('buildingDetail.config.brandScope') }}: {{ getBrandScopeLabel((getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y) as EditableGridUnit).brandScope) }}
            </span>
            <span
              class="stat"
              v-if="
                (getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y) as EditableGridUnit).productTypeId &&
                (getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y) as EditableGridUnit).brandScope === 'PRODUCT'
              "
            >
              {{ t('buildingDetail.config.researchAnchorProduct') }}: {{ getProductName((getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y) as EditableGridUnit).productTypeId) }}
            </span>
            <span
              class="stat"
              v-if="
                (getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y) as EditableGridUnit).industryCategory &&
                (getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y) as EditableGridUnit).brandScope === 'CATEGORY'
              "
            >
              {{ t('buildingDetail.config.researchIndustryCategory') }}:
              {{ getLocalizedIndustry((getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y) as EditableGridUnit).industryCategory!, locale) }}
            </span>
          </template>
        </div>

        <div v-if="getUnitInventorySummary(getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y))" class="unit-insight-card">
          <h5>{{ t('buildingDetail.inventory.title') }}</h5>
          <div class="inventory-summary-grid">
            <div class="inventory-summary-stat">
              <span class="inventory-summary-label">{{ t('buildingDetail.inventory.load') }}</span>
              <strong>
                {{
                  t('buildingDetail.inventory.quantity', {
                    quantity: formatUnitQuantity(getUnitInventorySummary(getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y))!.quantity),
                    capacity: formatUnitQuantity(getUnitInventorySummary(getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y))!.capacity),
                  })
                }}
              </strong>
            </div>
            <div class="inventory-summary-stat">
              <span class="inventory-summary-label">{{ t('buildingDetail.inventory.distinctItems') }}</span>
              <strong>{{ getUnitInventoryItemCount(getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)) }}</strong>
            </div>
            <div class="inventory-summary-stat" v-if="getUnitInventorySummary(getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y))!.averageQuality != null">
              <span class="inventory-summary-label">{{ t('buildingDetail.inventory.averageQuality') }}</span>
              <strong>{{ formatPercent(getUnitInventorySummary(getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y))!.averageQuality) }}</strong>
            </div>
            <div class="inventory-summary-stat" v-if="getUnitInventoryCostLabel(getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y))">
              <span class="inventory-summary-label">{{ t('buildingDetail.inventory.sourcingCosts') }}</span>
              <strong>{{ getUnitInventoryCostLabel(getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)) }}</strong>
            </div>
          </div>
          <div v-if="getUnitInventories(getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)).length > 0" class="inventory-table">
            <div class="inventory-table-header">
              <span class="inventory-col-item">{{ t('buildingDetail.inventory.item') }}</span>
              <span class="inventory-col-quantity">{{ t('buildingDetail.inventory.amount') }}</span>
              <span class="inventory-col-quality">{{ t('buildingDetail.inventory.quality') }}</span>
              <span class="inventory-col-cost">{{ t('buildingDetail.inventory.sourcingCost') }}</span>
            </div>
            <div v-for="inventory in getUnitInventories(getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y))" :key="inventory.id" class="inventory-table-row">
              <div class="inventory-col-item">
                <img v-if="getInventoryItemImageUrl(inventory)" class="inventory-item-image" :src="getInventoryItemImageUrl(inventory)!" :alt="getInventoryItemName(inventory)" />
                <span v-else class="inventory-item-avatar">{{ getInventoryItemMonogram(inventory) }}</span>
                <div class="inventory-item-stack">
                  <span class="inventory-item-name">{{ getInventoryItemName(inventory) }}</span>
                </div>
              </div>
              <div class="inventory-col-quantity">
                <span class="inventory-item-quantity">{{ formatUnitQuantity(inventory.quantity) }}</span>
              </div>
              <div class="inventory-col-quality">
                <span class="inventory-item-quality">{{ formatPercent(inventory.quality) }}</span>
              </div>
              <div class="inventory-col-cost">
                <span class="inventory-item-cost">{{ getInventoryItemSourcingCostLabel(inventory) }}</span>
                <span v-if="getInventoryItemSourcingCostPerUnitLabel(inventory)" class="inventory-item-secondary">
                  {{ getInventoryItemSourcingCostPerUnitLabel(inventory) }}
                </span>
              </div>
            </div>
          </div>
          <p v-else class="inventory-empty">{{ t('buildingDetail.inventory.empty') }}</p>
          <div class="detail-capacity">
            <span class="detail-capacity-fill" :style="{ width: `${selectedPlannedUnitFlowSegments.fillWidth}%` }"></span>
            <span
              v-if="selectedPlannedUnitFlowSegments.inflowWidth > 0"
              class="detail-capacity-inflow"
              :style="{ left: `${selectedPlannedUnitFlowSegments.inflowLeft}%`, width: `${selectedPlannedUnitFlowSegments.inflowWidth}%` }"
            ></span>
            <span
              v-if="selectedPlannedUnitFlowSegments.outflowWidth > 0"
              class="detail-capacity-outflow"
              :style="{ left: `${selectedPlannedUnitFlowSegments.outflowLeft}%`, width: `${selectedPlannedUnitFlowSegments.outflowWidth}%` }"
            ></span>
          </div>
        </div>

        <UnitResourceHistoryPanel
          v-if="selectedHistoryItemOptions.length > 0"
          :items="selectedHistoryItemOptions"
          :selected-item-key="selectedHistoryItemKey"
          :history="selectedUnitResourceHistory"
          @update:selected-item-key="selectedHistoryItemKey = $event"
        />

        <div v-if="getDraftUnitConstructionCostLabel(getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y))" class="unit-insight-card">
          <h5>{{ t('buildingDetail.costSummaryTitle') }}</h5>
          <div class="unit-stats">
            <span class="stat">
              {{ t('buildingDetail.unitCost', { cost: getDraftUnitConstructionCostLabel(getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)) }) }}
            </span>
          </div>
        </div>

        <div
          v-if="selectedPurchaseUnit && 'resourceTypeId' in selectedPurchaseUnit && selectedPurchaseUnit.resourceTypeId && ['EXCHANGE', 'OPTIMAL'].includes(selectedPurchaseUnit.purchaseSource ?? '')"
          class="unit-insight-card"
        >
          <h5>{{ t('buildingDetail.exchange.title') }}</h5>
          <p class="config-help">{{ t('buildingDetail.exchange.subtitle') }}</p>
          <p class="config-help exchange-selection-hint">{{ t('buildingDetail.exchange.selectionHint') }}</p>
          <p class="config-help" v-if="exchangeOffersLoading">{{ t('common.loading') }}</p>
          <template v-else>
            <p v-if="allExchangeOffersBlocked" class="config-help exchange-no-valid-offers">
              {{ t('buildingDetail.exchange.noValidOffers') }}
            </p>
            <!-- Logistics trap warning -->
            <div v-if="logisticsTrapWarning" class="logistics-trap-warning" role="alert">
              {{
                t('buildingDetail.exchange.logisticsTrap', {
                  cheapCity: logisticsTrapWarning.cheaperStickerCityName,
                  cheapExchange: formatCurrency(logisticsTrapWarning.cheaperStickerExchangePrice),
                  cheapDelivered: formatCurrency(logisticsTrapWarning.cheaperStickerDeliveredPrice),
                  bestCity: logisticsTrapWarning.recommendedCityName,
                  bestDelivered: formatCurrency(logisticsTrapWarning.recommendedDeliveredPrice),
                })
              }}
            </div>
            <!-- Sort controls -->
            <div class="exchange-sort-controls" v-if="exchangeOfferItems.length > 1">
              <span class="exchange-sort-label">{{ t('buildingDetail.exchange.sortBy') }}</span>
              <button
                v-for="dim in ['deliveredPrice', 'exchangePrice', 'quality'] as ExchangeSortBy[]"
                :key="dim"
                :class="['exchange-sort-btn', { active: exchangeSortBy === dim }]"
                @click="exchangeSortBy = dim"
              >
                {{ t('buildingDetail.exchange.sortOption.' + dim) }}
              </button>
            </div>
            <ul class="exchange-offers-list">
              <li
                v-for="offer in exchangeOfferItems"
                :key="`${offer.cityId}-${offer.resourceTypeId}`"
                :class="['exchange-offer-item', { 'offer-blocked': offer.blocked, 'offer-best': offer.cityId === bestExchangeOfferCityId }]"
              >
                <div class="exchange-offer-header">
                  <strong>{{ offer.cityName }}</strong>
                  <span class="offer-best-badge" v-if="offer.cityId === bestExchangeOfferCityId">{{ t('buildingDetail.exchange.bestOffer') }}</span>
                  <span>{{ t('buildingDetail.exchange.quality', { quality: formatPercent(offer.estimatedQuality) }) }}</span>
                </div>
                <div class="exchange-offer-metrics">
                  <span>{{ t('buildingDetail.exchange.exchangePrice', { price: formatCurrency(offer.exchangePricePerUnit), unit: offer.unitSymbol }) }}</span>
                  <span>{{ t('buildingDetail.exchange.transit', { price: formatCurrency(offer.transitCostPerUnit), distance: offer.distanceKm }) }}</span>
                  <span>{{ t('buildingDetail.exchange.deliveredPrice', { price: formatCurrency(offer.deliveredPricePerUnit), unit: offer.unitSymbol }) }}</span>
                </div>
                <p v-if="offer.blockedReason === 'maxPrice'" class="offer-blocked-reason">
                  {{ t('buildingDetail.exchange.blockedMaxPrice', { maxPrice: formatCurrency(selectedPurchaseUnit?.maxPrice ?? 0), unit: offer.unitSymbol }) }}
                </p>
                <p v-else-if="offer.blockedReason === 'minQuality'" class="offer-blocked-reason">
                  {{ t('buildingDetail.exchange.blockedMinQuality', { minQuality: formatPercent(selectedPurchaseUnit?.minQuality ?? 0) }) }}
                </p>
              </li>
            </ul>
            <!-- Link to Global Exchange -->
            <RouterLink v-if="selectedPurchaseResourceSlug" :to="{ name: 'exchange', query: { resource: selectedPurchaseResourceSlug, city: building?.cityId } }" class="exchange-view-link">
              {{ t('buildingDetail.exchange.viewOnExchange') }}
            </RouterLink>
          </template>
        </div>

        <div class="unit-actions" v-if="isEditing">
          <button class="btn btn-danger btn-sm" @click="removeDraftUnit(selectedCell.x, selectedCell.y)">
            {{ t('buildingDetail.removeUnit') }}
          </button>
        </div>

        <!-- Unit Upgrade Panel (edit-mode only) -->
        <div v-if="isEditing && selectedCellUpgradeInfo !== null" class="unit-insight-card unit-upgrade-panel" :aria-label="t('buildingDetail.accessibility.unitUpgrade')">
          <h5>{{ t('buildingDetail.unitUpgrade.sectionTitle') }}</h5>

          <!-- Upgrade in progress (from pending configuration) -->
          <div v-if="selectedCellPendingUpgrade" class="unit-upgrade-in-progress">
            <div class="unit-upgrade-progress-badge">⏳</div>
            <div class="unit-upgrade-progress-body">
              <strong>{{ t('buildingDetail.unitUpgrade.pendingTitle') }}</strong>
              <p class="unit-upgrade-progress-desc" :title="selectedCellPendingUpgrade.ticksRemaining + ' ticks remaining'">
                {{
                  t('buildingDetail.unitUpgrade.pendingBody', {
                    level: selectedCellPendingUpgrade.level,
                    time: formatTickDuration(selectedCellPendingUpgrade.ticksRemaining, locale),
                  })
                }}
              </p>
              <p class="unit-upgrade-downtime-notice">
                {{ t('buildingDetail.unitUpgrade.pendingDowntimeNotice') }}
              </p>
            </div>
          </div>

          <!-- Max level state -->
          <div v-else-if="selectedCellUpgradeInfo.isMaxLevel" class="unit-upgrade-max-level">
            <span class="unit-upgrade-max-badge">★</span>
            <span>{{ t('buildingDetail.unitUpgrade.maxLevel') }}</span>
            <p class="unit-upgrade-max-note">{{ t('buildingDetail.unitUpgrade.maxLevelNote') }}</p>
          </div>

          <!-- Not upgradable -->
          <div v-else-if="!selectedCellUpgradeInfo.isUpgradable" class="unit-upgrade-not-available">
            <p>{{ t('buildingDetail.unitUpgrade.notUpgradable') }}</p>
          </div>

          <!-- Upgrade available -->
          <div v-else class="unit-upgrade-available">
            <div class="unit-upgrade-levels">
              <span class="unit-upgrade-level current-level">{{ t('buildingDetail.unitUpgrade.currentLevel', { level: selectedCellUpgradeInfo.currentLevel }) }}</span>
              <span class="unit-upgrade-arrow">→</span>
              <span class="unit-upgrade-level next-level">{{ t('buildingDetail.unitUpgrade.nextLevel', { level: selectedCellUpgradeInfo.nextLevel }) }}</span>
            </div>
            <!-- Full before/after stat table -->
            <div class="unit-upgrade-stats" :aria-label="t('buildingDetail.accessibility.upgradeImpact')">
              <div class="unit-upgrade-stat-row">
                <span class="unit-upgrade-stat-label">{{ selectedCellUpgradeInfo.statLabel }}</span>
                <span class="unit-upgrade-stat-values">
                  <span class="stat-current">{{ selectedCellUpgradeInfo.currentStat.toFixed(1) }}</span>
                  <span class="stat-arrow"> → </span>
                  <span class="stat-next">{{ selectedCellUpgradeInfo.nextStat.toFixed(1) }}</span>
                </span>
              </div>
              <!-- Storage capacity row — shown for all unit types that buffer inventory -->
              <div class="unit-upgrade-stat-row" :aria-label="t('buildingDetail.accessibility.storageCapacityDelta')">
                <span class="unit-upgrade-stat-label">{{ t('buildingDetail.unitUpgrade.storageCapacity') }}</span>
                <span class="unit-upgrade-stat-values">
                  <span class="stat-current">{{ selectedCellUpgradeInfo.currentStorageCapacity.toFixed(0) }}</span>
                  <span class="stat-arrow"> → </span>
                  <span class="stat-next">{{ selectedCellUpgradeInfo.nextStorageCapacity.toFixed(0) }}</span>
                  <span class="stat-delta stat-delta-positive">+{{ (selectedCellUpgradeInfo.nextStorageCapacity - selectedCellUpgradeInfo.currentStorageCapacity).toFixed(0) }}</span>
                </span>
              </div>
              <div class="unit-upgrade-stat-row" :aria-label="t('buildingDetail.accessibility.laborCostDelta')">
                <span class="unit-upgrade-stat-label">{{ t('buildingDetail.unitUpgrade.laborCost') }}</span>
                <span class="unit-upgrade-stat-values">
                  <span class="stat-current">{{ formatCurrency(selectedCellUpgradeInfo.currentLaborCostPerTick) }}</span>
                  <span class="stat-arrow"> → </span>
                  <span class="stat-next">{{ formatCurrency(selectedCellUpgradeInfo.nextLaborCostPerTick) }}</span>
                  <span class="stat-delta stat-delta-negative">+{{ formatCurrency(selectedCellUpgradeInfo.nextLaborCostPerTick - selectedCellUpgradeInfo.currentLaborCostPerTick) }}</span>
                </span>
              </div>
              <div class="unit-upgrade-stat-row" :aria-label="t('buildingDetail.accessibility.energyCostDelta')">
                <span class="unit-upgrade-stat-label">{{ t('buildingDetail.unitUpgrade.energyCost') }}</span>
                <span class="unit-upgrade-stat-values">
                  <span class="stat-current">{{ formatCurrency(selectedCellUpgradeInfo.currentEnergyCostPerTick) }}</span>
                  <span class="stat-arrow"> → </span>
                  <span class="stat-next">{{ formatCurrency(selectedCellUpgradeInfo.nextEnergyCostPerTick) }}</span>
                  <span class="stat-delta stat-delta-negative">+{{ formatCurrency(selectedCellUpgradeInfo.nextEnergyCostPerTick - selectedCellUpgradeInfo.currentEnergyCostPerTick) }}</span>
                </span>
              </div>
            </div>
            <!-- Downtime notice shown before confirming the upgrade -->
            <p class="unit-upgrade-downtime-notice available" :title="selectedCellUpgradeInfo.upgradeTicks + ' ticks'">
              {{ t('buildingDetail.unitUpgrade.availableDowntimeNotice', { time: formatTickDuration(selectedCellUpgradeInfo.upgradeTicks, locale) }) }}
            </p>
            <div class="unit-upgrade-meta">
              <span class="unit-upgrade-cost">{{ t('buildingDetail.unitUpgrade.cost', { cost: formatCurrency(selectedCellUpgradeInfo.upgradeCost) }) }}</span>
              <span class="unit-upgrade-duration" :title="t('buildingDetail.unitUpgrade.durationTicks', { ticks: selectedCellUpgradeInfo.upgradeTicks })">{{
                t('buildingDetail.unitUpgrade.duration', { time: formatTickDuration(selectedCellUpgradeInfo.upgradeTicks, locale) })
              }}</span>
            </div>
            <p v-if="unitUpgradeError" class="form-error">{{ unitUpgradeError }}</p>
            <!-- Staged state: unit has been queued for upgrade via Store Upgrade -->
            <div v-if="isSelectedCellStaged" class="unit-upgrade-staged">
              <span class="unit-upgrade-staged-badge">✓ {{ t('buildingDetail.unitUpgrade.stagedBadge') }}</span>
              <p class="unit-upgrade-stage-info">{{ t('buildingDetail.unitUpgrade.stageInfo') }}</p>
              <button class="btn btn-ghost btn-sm" @click="toggleStagedUpgrade(selectedCellUpgradeInfo!.unitId)">
                {{ t('buildingDetail.unitUpgrade.removeStagedUpgrade') }}
              </button>
            </div>
            <!-- Not yet staged: show Stage and Upgrade Now buttons -->
            <div v-else class="unit-upgrade-actions">
              <button class="btn btn-primary btn-sm unit-upgrade-stage-btn" @click="toggleStagedUpgrade(selectedCellUpgradeInfo!.unitId)">
                {{ t('buildingDetail.unitUpgrade.stageButton') }}
              </button>
              <button class="btn btn-ghost btn-sm unit-upgrade-confirm-btn" :disabled="schedulingUpgrade" @click="submitUnitUpgrade(selectedCellUpgradeInfo!.unitId)">
                {{ schedulingUpgrade ? t('buildingDetail.unitUpgrade.confirmingButton') : t('buildingDetail.unitUpgrade.confirmButton') }}
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped src="./BuildingSidebar.shared.css"></style>

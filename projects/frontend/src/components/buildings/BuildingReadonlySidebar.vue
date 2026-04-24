<script setup lang="ts">
import { inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'
import UnitResourceHistoryPanel from '@/components/buildings/UnitResourceHistoryPanel.vue'
import type { BuildingUnit } from '@/types'
import type { ExchangeSortBy } from '@/lib/globalExchange'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const { locale, building, selectedCell, exchangeOffersLoading, exchangeSortBy, procurementPreview, procurementPreviewLoading, sourcingCandidates, sourcingCandidatesLoading, recentActivity, recentActivityLoading, publicSalesAnalytics, publicSalesAnalyticsLoading, unitProductAnalytics, unitProductAnalyticsLoading, quickPriceInput, quickPriceSaving, quickPriceSuccess, quickPriceError, selectedHistoryItemKey, showFlushConfirmDialog, flushingStorage, flushStorageError, flushStorageSuccess, selectedUnitTab, activeUnits, selectedPurchaseUnit, selectedPublicSalesUnit, selectedManufacturingUnit, selectedHistoryItemOptions, selectedUnitResourceHistory, cityCurrencyCode, miMaxRevenue, miMaxQuantitySold, miMaxPricePerUnit, miMaxAbsProfit, upaMaxAbsProfit, upaMaxCost, upaMaxEstRevenue, currentPublicSalesMinPrice, unitDetailTabs, exchangeOfferItems, allExchangeOffersBlocked, bestExchangeOfferCityId, logisticsTrapWarning, sourcingCheapestStickerDiffersFromBestLanded, selectedPurchaseResourceSlug, selectedActiveUnitOperationalStatus, selectedActiveUnitFlowSegments, setReadOnlySelectedCell, getResourceName, getProductName, getBrandScopeLabel, getUnitInventorySummary, getUnitInventories, getUnitInventoryItemCount, formatCurrency, formatGameTickTime, formatPercent, formatUnitQuantity, getUnitAtFrom, getInventoryItemImageUrl, getInventoryItemMonogram, getInventoryItemSourcingCostPerUnitLabel, getInventoryItemName, getInventoryItemSourcingCostLabel, getUnitInventoryCostLabel, getLocalizedIndustry, submitQuickPriceUpdate, submitFlushStorage } = bd
</script>

<template>
<!-- Read-only unit detail sidebar (click on active grid) -->
<div class="sidebar">
  <div class="unit-config">
    <div class="unit-config-header">
      <h3>{{ t('buildingDetail.unitDetails') }}</h3>
      <button class="btn btn-ghost" @click="setReadOnlySelectedCell(null)">{{ t('common.close') }}</button>
    </div>
    <!-- Unit detail tab navigation -->
    <nav class="unit-detail-tabs" aria-label="Unit detail sections" v-if="unitDetailTabs.length > 0">
      <button
        v-for="tab in unitDetailTabs"
        :key="tab.key"
        class="unit-tab-btn"
        :class="{ 'unit-tab-btn--active': selectedUnitTab === tab.key }"
        @click="selectedUnitTab = tab.key"
      >{{ t(`buildingDetail.unitTabs.${tab.key}`) }}</button>
    </nav>
    <div class="unit-detail">
      <!-- ── Basic Info tab ───────────────────────────────────── -->
      <template v-if="selectedUnitTab === 'basicInfo'">
      <h4>{{ t(`buildingDetail.unitTypes.${getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y)!.unitType}`) }}</h4>
      <p class="unit-desc">{{ t(`buildingDetail.unitDescriptions.${getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y)!.unitType}`) }}</p>
      <div class="unit-stats">
        <span class="stat">{{ t('common.level') }}: {{ getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y)!.level }}</span>
        <span class="stat">{{ t('buildingDetail.gridPosition', { x: selectedCell!.x, y: selectedCell!.y }) }}</span>
      </div>
      <div class="unit-config-readonly-details">
        <span class="stat" v-if="(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).resourceTypeId">
          {{ t('buildingDetail.config.resourceType') }}: {{ getResourceName((getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).resourceTypeId) }}
        </span>
        <span class="stat" v-if="(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).productTypeId">
          {{ t('buildingDetail.config.productType') }}: {{ getProductName((getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).productTypeId) }}
        </span>
        <span class="stat" v-if="(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).minPrice != null">
          {{ t('buildingDetail.config.minPrice') }}: {{ formatCurrency((getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).minPrice) }}
        </span>
        <span class="stat" v-if="(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).maxPrice != null">
          {{ t('buildingDetail.config.maxPrice') }}: {{ formatCurrency((getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).maxPrice) }}
        </span>
        <span class="stat" v-if="(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).purchaseSource">
          {{ t('buildingDetail.config.procurementMode') }}:
          {{ t(`buildingDetail.config.procurementMode_${(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).purchaseSource}`) }}
        </span>
        <span class="stat" v-if="(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).saleVisibility">
          {{ t('buildingDetail.config.saleVisibility') }}: {{ (getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).saleVisibility }}
        </span>
        <span class="stat" v-if="(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).budget != null">
          {{ t('buildingDetail.config.budget') }}: {{ formatCurrency((getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).budget) }}
        </span>
        <span class="stat" v-if="(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).brandScope">
          {{ t('buildingDetail.config.brandScope') }}: {{ getBrandScopeLabel((getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).brandScope) }}
        </span>
        <span
          class="stat"
          v-if="
            (getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).industryCategory &&
            (getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).brandScope === 'CATEGORY'
          "
        >
          {{ t('buildingDetail.config.researchIndustryCategory') }}:
          {{ getLocalizedIndustry((getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).industryCategory!, locale) }}
        </span>
      </div>

      <!-- Operational status badge for active units -->
      <div
        v-if="selectedActiveUnitOperationalStatus"
        class="unit-insight-card operational-status-card"
        :data-status="selectedActiveUnitOperationalStatus.status"
        aria-label="Unit operational status"
      >
        <h5>{{ t('buildingDetail.operationalStatus.title') }}</h5>
        <div class="operational-status-row">
          <span class="status-badge" :class="`status-${selectedActiveUnitOperationalStatus.status.toLowerCase()}`">
            {{ t(`buildingDetail.operationalStatus.${selectedActiveUnitOperationalStatus.status}`) }}
          </span>
          <span v-if="selectedActiveUnitOperationalStatus.idleTicks > 0" class="idle-ticks-label">
            {{ t('buildingDetail.operationalStatus.idleTicks', { count: selectedActiveUnitOperationalStatus.idleTicks }) }}
          </span>
        </div>
        <p v-if="selectedActiveUnitOperationalStatus.blockedReason" class="blocked-reason-text">
          {{ selectedActiveUnitOperationalStatus.blockedReason }}
        </p>
        <!-- Next-tick operating costs breakdown -->
        <div v-if="selectedActiveUnitOperationalStatus.nextTickLaborCost != null || selectedActiveUnitOperationalStatus.nextTickEnergyCost != null" class="operating-costs-row">
          <span class="operating-cost-label">{{ t('buildingDetail.operatingCost.title') }}</span>
          <span v-if="selectedActiveUnitOperationalStatus.nextTickLaborCost != null" class="operating-cost-item">
            {{ t('buildingDetail.operatingCost.labor', { cost: formatCurrency(selectedActiveUnitOperationalStatus.nextTickLaborCost) }) }}
          </span>
          <span v-if="selectedActiveUnitOperationalStatus.nextTickEnergyCost != null" class="operating-cost-item">
            {{ t('buildingDetail.operatingCost.energy', { cost: formatCurrency(selectedActiveUnitOperationalStatus.nextTickEnergyCost) }) }}
          </span>
        </div>
      </div>

      <!-- Unit Upgrade Panel removed from read-only view; it now lives in edit mode only. -->
      </template>
      <!-- ── Quick Actions tab (PUBLIC_SALES only) ──────────── -->
      <template v-else-if="selectedUnitTab === 'quickActions'">
        <div class="unit-insight-card" aria-label="Quick Actions">
          <h5>{{ t('buildingDetail.unitTabs.quickActionsHeading') }}</h5>
          <p class="unit-desc">{{ t('buildingDetail.unitTabs.quickActionsDesc') }}</p>
          <div v-if="selectedPublicSalesUnit && selectedPublicSalesUnit.minPrice != null" class="quick-action-current-price">
            <span class="mi-metric-label">{{ t('buildingDetail.marketIntelligence.configuredPrice') }}</span>
            <strong class="mi-metric-value">{{ formatCurrency(currentPublicSalesMinPrice) }}</strong>
          </div>
          <div aria-label="Quick Price Update">
          <!-- Directional impact hint derived from elasticity -->
          <div
            v-if="publicSalesAnalytics && publicSalesAnalytics.elasticityIndex !== null && quickPriceInput !== null && currentPublicSalesMinPrice > 0"
            class="mi-price-impact-hint"
            :class="{
              'mi-price-impact-raise': quickPriceInput > currentPublicSalesMinPrice,
              'mi-price-impact-lower': quickPriceInput < currentPublicSalesMinPrice,
            }"
          >
            <template v-if="quickPriceInput > currentPublicSalesMinPrice">
              {{
                t('buildingDetail.marketIntelligence.priceUpdate.raisingHint', {
                  elasticity: Math.abs(publicSalesAnalytics.elasticityIndex).toFixed(1),
                })
              }}
            </template>
            <template v-else-if="quickPriceInput < currentPublicSalesMinPrice">
              {{
                t('buildingDetail.marketIntelligence.priceUpdate.loweringHint', {
                  elasticity: Math.abs(publicSalesAnalytics.elasticityIndex).toFixed(1),
                })
              }}
            </template>
          </div>
          <div class="mi-price-update-row">
            <label class="mi-price-update-label" for="quick-price-input">
              {{ t('buildingDetail.marketIntelligence.priceUpdate.newPrice') }}
              <span class="currency-badge">{{ cityCurrencyCode }}</span>
            </label>
            <input
              id="quick-price-input"
              type="number"
              class="mi-price-input"
              :placeholder="selectedPublicSalesUnit?.minPrice?.toString() ?? ''"
              :min="0.01"
              :step="0.01"
              v-model.number="quickPriceInput"
            />
            <button class="btn btn-primary mi-price-update-btn" :disabled="quickPriceSaving || quickPriceInput === null || quickPriceInput <= 0" @click="submitQuickPriceUpdate">
              {{ quickPriceSaving ? t('buildingDetail.marketIntelligence.priceUpdate.saving') : t('buildingDetail.marketIntelligence.priceUpdate.apply') }}
            </button>
          </div>
          <p v-if="quickPriceSuccess" class="mi-price-success">
            {{ t('buildingDetail.marketIntelligence.priceUpdate.success') }}
          </p>
          <p v-if="quickPriceError" class="mi-price-error">{{ quickPriceError }}</p>
          </div>
        </div>
      </template>
      <!-- ── Inventory tab ────────────────────────────────────── -->
      <template v-else-if="selectedUnitTab === 'inventory'">

      <div v-if="getUnitInventorySummary(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y))" class="unit-insight-card">
        <h5>{{ t('buildingDetail.inventory.title') }}</h5>
        <div class="inventory-summary-grid">
          <div class="inventory-summary-stat">
            <span class="inventory-summary-label">{{ t('buildingDetail.inventory.load') }}</span>
            <strong>
              {{
                t('buildingDetail.inventory.quantity', {
                  quantity: formatUnitQuantity(getUnitInventorySummary(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y))!.quantity),
                  capacity: formatUnitQuantity(getUnitInventorySummary(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y))!.capacity),
                })
              }}
            </strong>
          </div>
          <div class="inventory-summary-stat">
            <span class="inventory-summary-label">{{ t('buildingDetail.inventory.distinctItems') }}</span>
            <strong>{{ getUnitInventoryItemCount(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y)) }}</strong>
          </div>
          <div class="inventory-summary-stat" v-if="getUnitInventorySummary(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y))!.averageQuality != null">
            <span class="inventory-summary-label">{{ t('buildingDetail.inventory.averageQuality') }}</span>
            <strong>{{ formatPercent(getUnitInventorySummary(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y))!.averageQuality) }}</strong>
          </div>
          <div class="inventory-summary-stat" v-if="getUnitInventoryCostLabel(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y))">
            <span class="inventory-summary-label">{{ t('buildingDetail.inventory.sourcingCosts') }}</span>
            <strong>{{ getUnitInventoryCostLabel(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y)) }}</strong>
          </div>
        </div>
        <div v-if="getUnitInventories(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y)).length > 0" class="inventory-table">
          <div class="inventory-table-header">
            <span class="inventory-col-item">{{ t('buildingDetail.inventory.item') }}</span>
            <span class="inventory-col-quantity">{{ t('buildingDetail.inventory.amount') }}</span>
            <span class="inventory-col-quality">{{ t('buildingDetail.inventory.quality') }}</span>
            <span class="inventory-col-cost">{{ t('buildingDetail.inventory.sourcingCost') }}</span>
          </div>
          <div v-for="inventory in getUnitInventories(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y))" :key="inventory.id" class="inventory-table-row">
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
          <span class="detail-capacity-fill" :style="{ width: `${selectedActiveUnitFlowSegments.fillWidth}%` }"></span>
          <span
            v-if="selectedActiveUnitFlowSegments.inflowWidth > 0"
            class="detail-capacity-inflow"
            :style="{ left: `${selectedActiveUnitFlowSegments.inflowLeft}%`, width: `${selectedActiveUnitFlowSegments.inflowWidth}%` }"
          ></span>
          <span
            v-if="selectedActiveUnitFlowSegments.outflowWidth > 0"
            class="detail-capacity-outflow"
            :style="{ left: `${selectedActiveUnitFlowSegments.outflowLeft}%`, width: `${selectedActiveUnitFlowSegments.outflowWidth}%` }"
          ></span>
        </div>
        <!-- Flush storage action for STORAGE, MINING, and MANUFACTURING units -->
        <div v-if="['STORAGE', 'MINING', 'MANUFACTURING'].includes(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y)!.unitType)" class="flush-storage-section">
          <button
            class="btn btn-danger btn-sm"
            :disabled="flushingStorage || getUnitInventorySummary(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y))!.quantity === 0"
            @click="showFlushConfirmDialog = true"
          >
            {{ flushingStorage ? t('buildingDetail.flushStorage.flushing') : t('buildingDetail.flushStorage.title') }}
          </button>
          <p v-if="flushStorageError" class="form-error">{{ flushStorageError }}</p>
          <p v-if="flushStorageSuccess" class="form-success">{{ t('buildingDetail.flushStorage.success') }}</p>
          <!-- Confirmation dialog -->
          <div v-if="showFlushConfirmDialog" class="flush-confirm-dialog" role="dialog" :aria-label="t('buildingDetail.flushStorage.confirmTitle')">
            <p class="flush-confirm-msg">{{ t('buildingDetail.flushStorage.confirmBody') }}</p>
            <div class="flush-confirm-actions">
              <button class="btn btn-danger btn-sm" @click="submitFlushStorage(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y)!.id)">
                {{ t('buildingDetail.flushStorage.confirmYes') }}
              </button>
              <button class="btn btn-ghost btn-sm" @click="showFlushConfirmDialog = false">{{ t('common.cancel') }}</button>
            </div>
          </div>
        </div>
      </div>
      <p v-if="!getUnitInventorySummary(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y))" class="unit-desc">
        {{ t('buildingDetail.inventory.empty') }}
      </p>
      </template>
      <!-- ── Movement History tab ─────────────────────────────── -->
      <template v-else-if="selectedUnitTab === 'history'">
      <UnitResourceHistoryPanel
        v-if="selectedHistoryItemOptions.length > 0"
        :items="selectedHistoryItemOptions"
        :selected-item-key="selectedHistoryItemKey"
        :history="selectedUnitResourceHistory"
        @update:selected-item-key="selectedHistoryItemKey = $event"
      />
      <p v-else class="unit-desc">{{ t('buildingDetail.unitTabs.noHistory') }}</p>
      </template>
      <!-- ── Market Intelligence tab ──────────────────────────── -->
      <template v-else-if="selectedUnitTab === 'marketIntelligence'">
      <div
        v-if="
          selectedPurchaseUnit && 'resourceTypeId' in selectedPurchaseUnit && selectedPurchaseUnit.resourceTypeId && ['EXCHANGE', 'OPTIMAL'].includes(selectedPurchaseUnit.purchaseSource ?? '')
        "
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

      <!-- Procurement Preview Card (shown in view mode for PURCHASE units) -->
      <div v-if="selectedPurchaseUnit" class="procurement-preview unit-insight-card">
        <h5 class="procurement-preview-title">{{ t('buildingDetail.procurementPreview.title') }}</h5>
        <div v-if="procurementPreviewLoading" class="procurement-preview-loading">{{ t('common.loading') }}…</div>
        <div v-else-if="procurementPreview" class="procurement-preview-content">
          <div v-if="procurementPreview.canExecute" class="procurement-preview-ok">
            <span class="preview-status ok">✓ {{ t('buildingDetail.procurementPreview.willExecute') }}</span>
            <div class="preview-details">
              <div class="preview-row" v-if="procurementPreview.sourceCityName">
                <span class="preview-label">{{ t('buildingDetail.procurementPreview.source') }}</span>
                <span class="preview-value">{{ procurementPreview.sourceCityName }} ({{ t(`buildingDetail.procurementPreview.sourceType_${procurementPreview.sourceType}`) }})</span>
              </div>
              <div class="preview-row" v-if="procurementPreview.sourceVendorName">
                <span class="preview-label">{{ t('buildingDetail.procurementPreview.vendor') }}</span>
                <span class="preview-value">{{ procurementPreview.sourceVendorName }}</span>
              </div>
              <div class="preview-row" v-if="procurementPreview.exchangePricePerUnit !== null">
                <span class="preview-label">{{ t('buildingDetail.procurementPreview.exchangePrice') }}</span>
                <span class="preview-value">{{ formatCurrency(procurementPreview.exchangePricePerUnit) }}</span>
              </div>
              <div class="preview-row" v-if="procurementPreview.transitCostPerUnit !== null">
                <span class="preview-label">{{ t('buildingDetail.procurementPreview.transitCost') }}</span>
                <span class="preview-value">{{ formatCurrency(procurementPreview.transitCostPerUnit) }}</span>
              </div>
              <div class="preview-row" v-if="procurementPreview.deliveredPricePerUnit !== null">
                <span class="preview-label">{{ t('buildingDetail.procurementPreview.deliveredPrice') }}</span>
                <span class="preview-value preview-delivered">{{ formatCurrency(procurementPreview.deliveredPricePerUnit) }}</span>
              </div>
              <div class="preview-row" v-if="procurementPreview.estimatedQuality !== null">
                <span class="preview-label">{{ t('buildingDetail.procurementPreview.quality') }}</span>
                <span class="preview-value">{{ formatPercent(procurementPreview.estimatedQuality ?? 0) }}</span>
              </div>
            </div>
          </div>
          <div v-else class="procurement-preview-blocked">
            <span class="preview-status blocked">✗ {{ t('buildingDetail.procurementPreview.blocked') }}</span>
            <div class="preview-block-details">
              <span class="preview-block-reason">{{ t(`buildingDetail.procurementPreview.blockReason_${procurementPreview.blockReason ?? 'UNKNOWN'}`) }}</span>
              <p class="preview-block-message" v-if="procurementPreview.blockMessage">{{ procurementPreview.blockMessage }}</p>
            </div>
            <div class="preview-details" v-if="procurementPreview.deliveredPricePerUnit !== null">
              <div class="preview-row">
                <span class="preview-label">{{ t('buildingDetail.procurementPreview.nearestOffer') }}</span>
                <span class="preview-value preview-blocked-price">{{ formatCurrency(procurementPreview.deliveredPricePerUnit) }}</span>
              </div>
              <div class="preview-row" v-if="procurementPreview.sourceCityName">
                <span class="preview-label">{{ t('buildingDetail.procurementPreview.source') }}</span>
                <span class="preview-value">{{ procurementPreview.sourceCityName }}</span>
              </div>
            </div>
          </div>
        </div>
        <div v-else class="procurement-preview-empty">
          {{ t('buildingDetail.procurementPreview.notAvailable') }}
        </div>
      </div>

      <!-- Sourcing Comparison Panel (shown in view mode for PURCHASE units with a resource configured) -->
      <div
        v-if="selectedPurchaseUnit && (selectedPurchaseUnit.resourceTypeId || selectedPurchaseUnit.productTypeId)"
        class="sourcing-comparison unit-insight-card"
        aria-label="Sourcing Comparison"
      >
        <h5 class="sourcing-comparison-title">{{ t('buildingDetail.sourcingComparison.title') }}</h5>
        <p class="sourcing-comparison-subtitle config-help">{{ t('buildingDetail.sourcingComparison.subtitle') }}</p>

        <div v-if="sourcingCandidatesLoading" class="sourcing-comparison-loading">
          {{ t('buildingDetail.sourcingComparison.loading') }}
        </div>

        <template v-else-if="sourcingCandidates.length > 0">
          <!-- Logistics note: cheapest sticker ≠ best landed -->
          <p v-if="sourcingCheapestStickerDiffersFromBestLanded" class="sourcing-trap-note">ℹ️ {{ t('buildingDetail.sourcingComparison.cheapestNotBest') }}</p>

          <!-- Candidate table -->
          <div class="sourcing-table-wrapper">
            <table class="sourcing-table">
              <thead>
                <tr>
                  <th>{{ t('buildingDetail.sourcingComparison.colSource') }}</th>
                  <th>{{ t('buildingDetail.sourcingComparison.colOfferPrice') }}</th>
                  <th>{{ t('buildingDetail.sourcingComparison.colTransit') }}</th>
                  <th class="col-landed">{{ t('buildingDetail.sourcingComparison.colLanded') }}</th>
                  <th>{{ t('buildingDetail.sourcingComparison.colQuality') }}</th>
                  <th>{{ t('buildingDetail.sourcingComparison.colStatus') }}</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="candidate in sourcingCandidates"
                  :key="`${candidate.rank}-${candidate.sourceCityId ?? candidate.sourceVendorCompanyId}`"
                  :class="['sourcing-row', candidate.isRecommended ? 'recommended' : '', !candidate.isEligible ? 'ineligible' : '']"
                >
                  <td class="sourcing-col-source">
                    <span class="source-type-badge">{{ t(`buildingDetail.sourcingComparison.sourceType_${candidate.sourceType}`) }}</span>
                    <span class="source-name">
                      {{ candidate.sourceCityName ?? candidate.sourceVendorName ?? '—' }}
                    </span>
                    <span v-if="candidate.distanceKm && candidate.distanceKm > 0" class="source-distance">
                      {{ t('buildingDetail.sourcingComparison.distanceKm', { km: Math.round(candidate.distanceKm) }) }}
                    </span>
                  </td>
                  <td class="sourcing-col-offer">
                    <span v-if="candidate.exchangePricePerUnit !== null"> {{ formatCurrency(candidate.exchangePricePerUnit) }} </span>
                    <span v-else-if="candidate.deliveredPricePerUnit !== null"> {{ formatCurrency(candidate.deliveredPricePerUnit) }} </span>
                    <span v-else>—</span>
                  </td>
                  <td class="sourcing-col-transit">
                    <span v-if="candidate.transitCostPerUnit !== null" class="transit-cost"> +{{ formatCurrency(candidate.transitCostPerUnit) }} </span>
                    <span v-else>—</span>
                  </td>
                  <td class="sourcing-col-landed col-landed">
                    <strong v-if="candidate.deliveredPricePerUnit !== null"> {{ formatCurrency(candidate.deliveredPricePerUnit) }} </strong>
                    <span v-else>—</span>
                  </td>
                  <td class="sourcing-col-quality">
                    <span v-if="candidate.estimatedQuality !== null">{{ formatPercent(candidate.estimatedQuality) }}</span>
                    <span v-else>—</span>
                  </td>
                  <td class="sourcing-col-status">
                    <span v-if="candidate.isRecommended" class="sc-badge sc-badge--recommended"> ★ {{ t('buildingDetail.sourcingComparison.recommended') }} </span>
                    <span v-else-if="candidate.isEligible" class="sc-badge sc-badge--eligible">
                      {{ t('buildingDetail.sourcingComparison.eligible') }}
                    </span>
                    <span v-else class="sc-badge sc-badge--blocked" :title="candidate.blockMessage ?? ''">
                      {{ t(`buildingDetail.sourcingComparison.blockReason_${candidate.blockReason ?? 'UNKNOWN'}`) }}
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- Filter hint when some candidates are blocked -->
          <p v-if="sourcingCandidates.some((c) => !c.isEligible)" class="sourcing-filter-hint config-help">
            {{ t('buildingDetail.sourcingComparison.filterHint') }}
          </p>
        </template>

        <div v-else class="sourcing-comparison-empty">
          {{ t('buildingDetail.sourcingComparison.empty') }}
        </div>
      </div>
      <div v-if="selectedPublicSalesUnit" class="unit-insight-card market-intelligence-panel" aria-label="Market Intelligence">
        <h5>{{ t('buildingDetail.marketIntelligence.title') }}</h5>

        <!-- Product identity + data window row -->
        <div class="mi-context-row">
          <span v-if="publicSalesAnalytics?.productName" class="mi-product-chip" aria-label="Currently selling product">
            {{ publicSalesAnalytics.productName }}
          </span>
          <span
            v-if="publicSalesAnalytics && publicSalesAnalytics.dataFromTick > 0"
            class="mi-tick-window"
            :title="`T${publicSalesAnalytics.dataFromTick}–T${publicSalesAnalytics.dataToTick}`"
          >
            {{ formatGameTickTime(publicSalesAnalytics.dataFromTick, locale) }} – {{ formatGameTickTime(publicSalesAnalytics.dataToTick, locale) }}
          </span>
        </div>

        <p v-if="publicSalesAnalyticsLoading" class="config-help">{{ t('buildingDetail.marketIntelligence.loading') }}</p>

        <template v-else-if="publicSalesAnalytics">
          <!-- Summary metrics -->
          <div class="mi-summary-grid">
            <div class="mi-metric">
              <span class="mi-metric-label">{{ t('buildingDetail.marketIntelligence.totalRevenue') }}</span>
              <strong class="mi-metric-value">{{ formatCurrency(publicSalesAnalytics.totalRevenue) }}</strong>
            </div>
            <div class="mi-metric" v-if="publicSalesAnalytics.totalProfit !== null">
              <span class="mi-metric-label">{{ t('buildingDetail.marketIntelligence.totalProfit') }}</span>
              <strong
                class="mi-metric-value"
                :class="{
                  'building-profit-positive-text': publicSalesAnalytics.totalProfit >= 0,
                  'building-profit-negative-text': publicSalesAnalytics.totalProfit < 0,
                }"
                >{{ formatCurrency(publicSalesAnalytics.totalProfit) }}</strong
              >
            </div>
            <div class="mi-metric">
              <span class="mi-metric-label">{{ t('buildingDetail.marketIntelligence.totalSold') }}</span>
              <strong class="mi-metric-value">{{ formatUnitQuantity(publicSalesAnalytics.totalQuantitySold) }}</strong>
            </div>
            <div class="mi-metric" v-if="publicSalesAnalytics.averagePricePerUnit > 0">
              <span class="mi-metric-label">{{ t('buildingDetail.marketIntelligence.avgPrice') }}</span>
              <strong class="mi-metric-value">{{ formatCurrency(publicSalesAnalytics.averagePricePerUnit) }}</strong>
            </div>
            <div class="mi-metric" v-if="selectedPublicSalesUnit.minPrice != null">
              <span class="mi-metric-label">{{ t('buildingDetail.marketIntelligence.configuredPrice') }}</span>
              <strong class="mi-metric-value">{{ formatCurrency(currentPublicSalesMinPrice) }}</strong>
            </div>
            <div class="mi-metric" v-if="publicSalesAnalytics.revenueHistory.length > 0">
              <span class="mi-metric-label">{{ t('buildingDetail.marketIntelligence.recentUtilization') }}</span>
              <strong class="mi-metric-value">{{ Math.round(publicSalesAnalytics.recentUtilization * 100) }}%</strong>
            </div>
            <!-- Trend direction (only shown when there are at least 2 ticks of history) -->
            <div v-if="publicSalesAnalytics.trendDirection && publicSalesAnalytics.trendDirection !== 'NO_DATA'" class="mi-metric">
              <span class="mi-metric-label">{{ t('buildingDetail.marketIntelligence.trend') }}</span>
              <strong
                class="mi-metric-value mi-trend"
                :class="{
                  'mi-trend-up': publicSalesAnalytics.trendDirection === 'UP',
                  'mi-trend-down': publicSalesAnalytics.trendDirection === 'DOWN',
                  'mi-trend-flat': publicSalesAnalytics.trendDirection === 'FLAT',
                }"
              >
                {{
                  publicSalesAnalytics.trendDirection === 'UP'
                    ? t('buildingDetail.marketIntelligence.trendUp')
                    : publicSalesAnalytics.trendDirection === 'DOWN'
                      ? t('buildingDetail.marketIntelligence.trendDown')
                      : t('buildingDetail.marketIntelligence.trendFlat')
                }}
              </strong>
            </div>
            <!-- Market trend factor (live trend multiplier from the simulation) -->
            <div v-if="publicSalesAnalytics.trendFactor !== null" class="mi-metric">
              <span class="mi-metric-label">{{ t('buildingDetail.marketIntelligence.trendFactor') }}</span>
              <strong
                class="mi-metric-value mi-trend"
                :class="{
                  'mi-trend-up': publicSalesAnalytics.trendFactor > 1.05,
                  'mi-trend-down': publicSalesAnalytics.trendFactor < 0.95,
                  'mi-trend-flat': publicSalesAnalytics.trendFactor >= 0.95 && publicSalesAnalytics.trendFactor <= 1.05,
                }"
              >
                {{ publicSalesAnalytics.trendFactor > 1 ? '+' : '' }}{{ ((publicSalesAnalytics.trendFactor - 1) * 100).toFixed(0) }}%
              </strong>
            </div>
          </div>

          <!-- No-history empty state -->
          <p v-if="publicSalesAnalytics.revenueHistory.length === 0" class="mi-empty-state">
            {{ t('buildingDetail.marketIntelligence.noHistory') }}
          </p>

          <template v-else>
            <!-- Revenue mini chart -->
            <div class="mi-chart-section">
              <span class="mi-chart-label">{{ t('buildingDetail.marketIntelligence.revenueChart') }}</span>
              <div class="mi-bar-chart" role="img" :aria-label="t('buildingDetail.marketIntelligence.revenueChart')">
                <div
                  v-for="snap in publicSalesAnalytics.revenueHistory"
                  :key="snap.tick"
                  class="mi-bar mi-bar-revenue"
                  :style="{
                    height: `${Math.max(2, miMaxRevenue > 0 ? (snap.revenue / miMaxRevenue) * 100 : 0).toFixed(1)}%`,
                  }"
                  :title="`T${snap.tick}: ${formatCurrency(snap.revenue)}`"
                ></div>
              </div>
            </div>

            <!-- Quantity mini chart -->
            <div class="mi-chart-section">
              <span class="mi-chart-label">{{ t('buildingDetail.marketIntelligence.quantityChart') }}</span>
              <div class="mi-bar-chart" role="img" :aria-label="t('buildingDetail.marketIntelligence.quantityChart')">
                <div
                  v-for="snap in publicSalesAnalytics.revenueHistory"
                  :key="snap.tick"
                  class="mi-bar mi-bar-quantity"
                  :style="{
                    height: `${Math.max(2, miMaxQuantitySold > 0 ? (snap.quantitySold / miMaxQuantitySold) * 100 : 0).toFixed(1)}%`,
                  }"
                  :title="`T${snap.tick}: ${formatUnitQuantity(snap.quantitySold)}`"
                ></div>
              </div>
            </div>

            <!-- Price history chart -->
            <div v-if="publicSalesAnalytics.priceHistory.length > 0" class="mi-chart-section">
              <span class="mi-chart-label">{{ t('buildingDetail.marketIntelligence.priceChart') }}</span>
              <div class="mi-bar-chart mi-bar-chart-price" role="img" :aria-label="t('buildingDetail.marketIntelligence.priceChart')">
                <div
                  v-for="snap in publicSalesAnalytics.priceHistory"
                  :key="snap.tick"
                  class="mi-bar mi-bar-price"
                  :style="{
                    height: `${Math.max(2, miMaxPricePerUnit > 0 ? (snap.pricePerUnit / miMaxPricePerUnit) * 100 : 0).toFixed(1)}%`,
                  }"
                  :title="`T${snap.tick}: ${formatCurrency(snap.pricePerUnit)}`"
                ></div>
              </div>
            </div>

            <!-- Profit history chart -->
            <div v-if="publicSalesAnalytics.profitHistory && publicSalesAnalytics.profitHistory.length > 0" class="mi-chart-section">
              <span class="mi-chart-label">{{ t('buildingDetail.marketIntelligence.profitChart') }}</span>
              <div class="mi-bar-chart mi-bar-chart-profit" role="img" :aria-label="t('buildingDetail.marketIntelligence.profitChart')">
                <div
                  v-for="snap in publicSalesAnalytics.profitHistory"
                  :key="snap.tick"
                  class="mi-bar"
                  :class="snap.profit >= 0 ? 'mi-bar-profit-positive' : 'mi-bar-profit-negative'"
                  :style="{
                    height: `${Math.max(2, miMaxAbsProfit > 0 ? (Math.abs(snap.profit) / miMaxAbsProfit) * 100 : 0).toFixed(1)}%`,
                  }"
                  :title="`T${snap.tick}: ${formatCurrency(snap.profit)}${snap.grossMarginPct !== null ? ` (${snap.grossMarginPct.toFixed(1)}% margin)` : ''}`"
                ></div>
              </div>
            </div>
          </template>

          <!-- Market share -->
          <div class="mi-section">
            <span class="mi-chart-label">{{ t('buildingDetail.marketIntelligence.marketShare') }}</span>
            <p v-if="publicSalesAnalytics.marketShare.length === 0" class="config-help">
              {{ t('buildingDetail.marketIntelligence.noMarketShare') }}
            </p>
            <div v-else class="mi-market-share">
              <div
                v-for="entry in publicSalesAnalytics.marketShare"
                :key="entry.label"
                class="mi-share-row"
                :class="{ 'mi-share-row-you': entry.companyId === building?.companyId, 'mi-share-row-unmet': entry.isUnmet }"
              >
                <span class="mi-share-label"> {{ entry.label }}{{ entry.companyId === building?.companyId ? ' ★' : '' }}{{ entry.isUnmet ? ' ⬚' : '' }} </span>
                <div class="mi-share-bar-wrap">
                  <div class="mi-share-bar" :class="{ 'mi-share-bar-unmet': entry.isUnmet }" :style="{ width: `${(entry.share * 100).toFixed(1)}%` }"></div>
                </div>
                <span class="mi-share-pct">{{ (entry.share * 100).toFixed(1) }}%</span>
              </div>
            </div>
          </div>

          <!-- Demand Drivers -->
          <div v-if="publicSalesAnalytics.demandDrivers.length > 0" class="mi-demand-drivers" aria-label="Demand Drivers">
            <span class="mi-chart-label">{{ t('buildingDetail.marketIntelligence.demandDrivers.title') }}</span>
            <div class="mi-driver-list">
              <div v-for="driver in publicSalesAnalytics.demandDrivers" :key="driver.factor" class="mi-driver-entry" :class="`mi-driver-${driver.impact.toLowerCase()}`">
                <span class="mi-driver-icon">
                  {{ driver.impact === 'POSITIVE' ? '↑' : driver.impact === 'NEGATIVE' ? '↓' : '→' }}
                </span>
                <div class="mi-driver-content">
                  <strong class="mi-driver-factor">{{ t(`buildingDetail.marketIntelligence.demandDrivers.factor_${driver.factor}`) }}</strong>
                  <span class="mi-driver-desc">{{ driver.description }}</span>
                </div>
              </div>
            </div>
          </div>

          <!-- Elasticity index + context card -->
          <div class="mi-context-card">
            <div class="mi-context-grid">
              <div v-if="publicSalesAnalytics.elasticityIndex !== null" class="mi-context-item">
                <span class="mi-context-label">{{ t('buildingDetail.marketIntelligence.elasticityIndex') }}</span>
                <strong
                  class="mi-context-value"
                  :class="{ 'mi-elastic-high': (publicSalesAnalytics.elasticityIndex ?? 0) < -1.5, 'mi-elastic-low': (publicSalesAnalytics.elasticityIndex ?? 0) > -0.5 }"
                >
                  {{ publicSalesAnalytics.elasticityIndex.toFixed(2) }}
                </strong>
                <span class="mi-context-hint">{{ t('buildingDetail.marketIntelligence.elasticityHint') }}</span>
              </div>
              <div v-if="publicSalesAnalytics.populationIndex !== null" class="mi-context-item">
                <span class="mi-context-label">{{ t('buildingDetail.marketIntelligence.populationIndex') }}</span>
                <strong class="mi-context-value">{{ publicSalesAnalytics.populationIndex.toFixed(2) }}×</strong>
                <span class="mi-context-hint">{{ t('buildingDetail.marketIntelligence.populationIndexHint') }}</span>
              </div>
              <div v-if="publicSalesAnalytics.inventoryQuality !== null" class="mi-context-item">
                <span class="mi-context-label">{{ t('buildingDetail.marketIntelligence.productQuality') }}</span>
                <strong class="mi-context-value" :class="{ 'mi-quality-high': publicSalesAnalytics.inventoryQuality >= 0.7, 'mi-quality-low': publicSalesAnalytics.inventoryQuality < 0.4 }">
                  {{ Math.round(publicSalesAnalytics.inventoryQuality * 100) }}%
                </strong>
                <span class="mi-context-hint">{{ t('buildingDetail.marketIntelligence.productQualityHint') }}</span>
              </div>
              <div v-if="publicSalesAnalytics.brandAwareness !== null" class="mi-context-item">
                <span class="mi-context-label">{{ t('buildingDetail.marketIntelligence.brandAwareness') }}</span>
                <strong class="mi-context-value" :class="{ 'mi-quality-high': publicSalesAnalytics.brandAwareness >= 0.6 }">
                  {{ Math.round(publicSalesAnalytics.brandAwareness * 100) }}%
                </strong>
                <span class="mi-context-hint">{{ t('buildingDetail.marketIntelligence.brandAwarenessHint') }}</span>
              </div>
              <div v-if="publicSalesAnalytics.brandQuality !== null" class="mi-context-item">
                <span class="mi-context-label">{{ t('buildingDetail.marketIntelligence.brandQuality') }}</span>
                <strong
                  class="mi-context-value"
                  :class="{
                    'mi-quality-high': publicSalesAnalytics.brandQuality >= 0.5,
                    'mi-quality-low': publicSalesAnalytics.brandQuality < 0.2,
                  }"
                >
                  {{ Math.round(publicSalesAnalytics.brandQuality * 100) }}%
                  <span v-if="publicSalesAnalytics.brandQuality >= 0.5" class="mi-quality-badge mi-quality-badge-premium">{{
                    t('buildingDetail.marketIntelligence.brandQualityPremium')
                  }}</span>
                  <span v-else-if="publicSalesAnalytics.brandQuality >= 0.2" class="mi-quality-badge mi-quality-badge-growing">{{
                    t('buildingDetail.marketIntelligence.brandQualityGrowing')
                  }}</span>
                </strong>
                <span class="mi-context-hint">{{ t('buildingDetail.marketIntelligence.brandQualityHint') }}</span>
              </div>
            </div>
          </div>

          <!-- Demand signal -->
          <div class="mi-demand-card" :class="`mi-demand-${publicSalesAnalytics.demandSignal.toLowerCase().replace(/_/g, '-')}`">
            <div class="mi-demand-header">
              <span class="mi-demand-title">{{ t('buildingDetail.marketIntelligence.demandSignal.title') }}</span>
              <span class="mi-demand-badge">{{ t(`buildingDetail.marketIntelligence.demandSignal.${publicSalesAnalytics.demandSignal}`) }}</span>
            </div>
            <p class="mi-action-hint" v-if="publicSalesAnalytics.actionHint">
              <strong>{{ t('buildingDetail.marketIntelligence.actionHint') }}:</strong>
              {{ publicSalesAnalytics.actionHint }}
            </p>
          </div>
        </template>

        <p v-else class="config-help">{{ t('buildingDetail.marketIntelligence.loadFailed') }}</p>
      </div>

      <!-- Manufacturing Unit Product Analytics Panel -->
      <div
        v-if="selectedManufacturingUnit && (selectedManufacturingUnit.productTypeId || unitProductAnalytics)"
        class="unit-insight-card unit-product-analytics-panel"
        aria-label="Product Performance Analytics"
      >
        <h5>{{ t('buildingDetail.unitProductAnalytics.title') }}</h5>

        <!-- Product identity + data window row -->
        <div class="mi-context-row">
          <span v-if="unitProductAnalytics?.productName" class="mi-product-chip" aria-label="Currently producing product">
            {{ unitProductAnalytics.productName }}
          </span>
          <span v-else-if="selectedManufacturingUnit.productTypeId" class="mi-product-chip">
            {{ t('buildingDetail.unitProductAnalytics.productConfigured') }}
          </span>
          <span
            v-if="unitProductAnalytics && unitProductAnalytics.dataFromTick > 0"
            class="mi-tick-window"
            :title="`T${unitProductAnalytics.dataFromTick}–T${unitProductAnalytics.dataToTick}`"
          >
            {{ formatGameTickTime(unitProductAnalytics.dataFromTick, locale) }} – {{ formatGameTickTime(unitProductAnalytics.dataToTick, locale) }}
          </span>
        </div>

        <p v-if="unitProductAnalyticsLoading" class="config-help">{{ t('buildingDetail.unitProductAnalytics.loading') }}</p>

        <template v-else-if="unitProductAnalytics && unitProductAnalytics.snapshots.length > 0">
          <!-- Summary metrics -->
          <div class="mi-summary-grid">
            <div class="mi-metric">
              <span class="mi-metric-label">{{ t('buildingDetail.unitProductAnalytics.totalProduced') }}</span>
              <strong class="mi-metric-value">{{ formatUnitQuantity(unitProductAnalytics.totalQuantityProduced) }}</strong>
            </div>
            <div class="mi-metric">
              <span class="mi-metric-label">{{ t('buildingDetail.unitProductAnalytics.totalCost') }}</span>
              <strong class="mi-metric-value building-profit-negative-text">{{ formatCurrency(unitProductAnalytics.totalCost) }}</strong>
            </div>
            <div v-if="unitProductAnalytics.estimatedRevenue !== null" class="mi-metric">
              <span class="mi-metric-label">{{ t('buildingDetail.unitProductAnalytics.estimatedRevenue') }}</span>
              <strong class="mi-metric-value">{{ formatCurrency(unitProductAnalytics.estimatedRevenue) }}</strong>
            </div>
            <div v-if="unitProductAnalytics.estimatedProfit !== null" class="mi-metric">
              <span class="mi-metric-label">{{ t('buildingDetail.unitProductAnalytics.estimatedProfit') }}</span>
              <strong
                class="mi-metric-value"
                :class="{
                  'building-profit-positive-text': unitProductAnalytics.estimatedProfit >= 0,
                  'building-profit-negative-text': unitProductAnalytics.estimatedProfit < 0,
                }"
                >{{ formatCurrency(unitProductAnalytics.estimatedProfit) }}</strong
              >
            </div>
          </div>

          <!-- Cost history chart -->
          <div v-if="unitProductAnalytics.snapshots.length > 0" class="mi-chart-section">
            <span class="mi-chart-label">{{ t('buildingDetail.unitProductAnalytics.costChart') }}</span>
            <div class="mi-bar-chart mi-bar-chart-cost" role="img" :aria-label="t('buildingDetail.unitProductAnalytics.costChart')">
              <div
                v-for="snap in unitProductAnalytics.snapshots"
                :key="snap.tick"
                class="mi-bar mi-bar-cost"
                :style="{
                  height: `${Math.max(2, upaMaxCost > 0 ? (snap.totalCost / upaMaxCost) * 100 : 0).toFixed(1)}%`,
                }"
                :title="`T${snap.tick}: ${formatCurrency(snap.totalCost)} (${t('buildingDetail.unitProductAnalytics.labor')}: ${formatCurrency(snap.laborCost)}, ${t('buildingDetail.unitProductAnalytics.energy')}: ${formatCurrency(snap.energyCost)})`"
              ></div>
            </div>
          </div>

          <!-- Estimated revenue chart -->
          <div v-if="unitProductAnalytics.snapshots.some((s) => s.estimatedRevenue !== null && s.estimatedRevenue > 0)" class="mi-chart-section">
            <span class="mi-chart-label">{{ t('buildingDetail.unitProductAnalytics.estimatedRevenueChart') }}</span>
            <div class="mi-bar-chart mi-bar-chart-revenue" role="img" :aria-label="t('buildingDetail.unitProductAnalytics.estimatedRevenueChart')">
              <div
                v-for="snap in unitProductAnalytics.snapshots"
                :key="snap.tick"
                class="mi-bar mi-bar-revenue"
                :style="{
                  height: `${Math.max(2, upaMaxEstRevenue > 0 ? ((snap.estimatedRevenue ?? 0) / upaMaxEstRevenue) * 100 : 0).toFixed(1)}%`,
                }"
                :title="`T${snap.tick}: ${formatCurrency(snap.estimatedRevenue ?? 0)}`"
              ></div>
            </div>
          </div>

          <!-- Estimated profit chart -->
          <div v-if="unitProductAnalytics.snapshots.some((s) => s.estimatedProfit !== null)" class="mi-chart-section">
            <span class="mi-chart-label">{{ t('buildingDetail.unitProductAnalytics.estimatedProfitChart') }}</span>
            <div class="mi-bar-chart mi-bar-chart-profit" role="img" :aria-label="t('buildingDetail.unitProductAnalytics.estimatedProfitChart')">
              <div
                v-for="snap in unitProductAnalytics.snapshots"
                :key="snap.tick"
                class="mi-bar"
                :class="(snap.estimatedProfit ?? 0) >= 0 ? 'mi-bar-profit-positive' : 'mi-bar-profit-negative'"
                :style="{
                  height: `${Math.max(2, upaMaxAbsProfit > 0 ? (Math.abs(snap.estimatedProfit ?? 0) / upaMaxAbsProfit) * 100 : 0).toFixed(1)}%`,
                }"
                :title="`T${snap.tick}: ${formatCurrency(snap.estimatedProfit ?? 0)}`"
              ></div>
            </div>
          </div>

          <!-- Profitability note -->
          <p class="config-help mi-hint">{{ t('buildingDetail.unitProductAnalytics.profitNote') }}</p>
        </template>

        <template v-else-if="unitProductAnalytics && unitProductAnalytics.snapshots.length === 0">
          <p class="config-help">{{ t('buildingDetail.unitProductAnalytics.noData') }}</p>
        </template>

        <template v-else-if="!unitProductAnalytics && !unitProductAnalyticsLoading">
          <p class="config-help">{{ t('buildingDetail.unitProductAnalytics.noProduct') }}</p>
        </template>
      </div>
      </template>
      <!-- ── Recent Activity tab ─────────────────────────────── -->
      <template v-else-if="selectedUnitTab === 'recentActivity'">
      <div class="unit-insight-card recent-activity-panel" aria-label="Recent Activity">
        <h5>{{ t('buildingDetail.recentActivity.title') }}</h5>
        <p class="config-help">{{ t('buildingDetail.recentActivity.subtitle') }}</p>
        <p v-if="recentActivityLoading" class="config-help">…</p>
        <template v-else-if="recentActivity.length > 0">
          <ul class="activity-list">
            <li
              v-for="(event, idx) in recentActivity"
              :key="`${event.tick}-${event.buildingUnitId}-${event.eventType}-${idx}`"
              class="activity-item"
              :class="`activity-${event.eventType.toLowerCase()}`"
            >
              <span class="activity-tick" :title="t('buildingDetail.recentActivity.tickLabel', { tick: event.tick })">{{ formatGameTickTime(event.tick, locale) }}</span>
              <span class="activity-desc">{{ event.description }}</span>
            </li>
          </ul>
        </template>
        <p v-else class="config-help">{{ t('buildingDetail.recentActivity.empty') }}</p>
      </div>
      </template>
    </div>
  </div>

</div>
</template>

<style scoped src="./BuildingSidebar.shared.css"></style>

<script setup lang="ts">
import { computed, inject, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'
import type { ExchangeSortBy } from '@/lib/globalExchange'
import BuildingUnitConfigFields from '@/components/buildings/BuildingUnitConfigFields.vue'
import UnitResourceHistoryPanel from '@/components/buildings/UnitResourceHistoryPanel.vue'

const { t, locale } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const {
  building,
  isEditing,
  selectedCell,
  plannedUnits,
  exchangeOffersLoading,
  exchangeSortBy,
  selectedHistoryItemKey,
  selectedHistoryItemOptions,
  selectedUnitResourceHistory,
  selectedCellPendingUpgrade,
  selectedCellUpgradeInfo,
  isSelectedCellStaged,
  selectedPlannedUnitFlowSegments,
  schedulingUpgrade,
  unitUpgradeError,
  exchangeOfferItems,
  allExchangeOffersBlocked,
  bestExchangeOfferCityId,
  logisticsTrapWarning,
  selectedPurchaseResourceSlug,
  selectedPurchaseUnit,
  sourcingCandidates,
  sourcingCandidatesLoading,
  getUnitAtFrom,
  getDraftUnitConstructionCostLabel,
  getUnitInventorySummary,
  getUnitInventories,
  getUnitInventoryItemCount,
  getInventoryItemImageUrl,
  getInventoryItemMonogram,
  getInventoryItemName,
  getInventoryItemSourcingCostLabel,
  getInventoryItemSourcingCostPerUnitLabel,
  getUnitInventoryCostLabel,
  getDisplayedTicks,
  removeDraftUnit,
  toggleStagedUpgrade,
  submitUnitUpgrade,
  formatCurrency,
  formatTickDuration,
  formatPercent,
  formatUnitQuantity,
} = bd

const selectedConfigTab = ref<'config' | 'performance' | 'maintenance'>('config')

const editTabs = computed(() => [
  { key: 'config', label: t('buildingDetail.editTabConfig') },
  { key: 'performance', label: t('buildingDetail.editTabPerformance') },
  { key: 'maintenance', label: t('buildingDetail.editTabMaintenance') },
])

const currentUnit = computed(() => {
  if (!selectedCell.value) return null
  return getUnitAtFrom(plannedUnits.value, selectedCell.value.x, selectedCell.value.y)
})

const purchaseSourcingHistory = computed(() => {
  return [...sourcingCandidates.value]
    .filter((candidate) => candidate.deliveredPricePerUnit != null || candidate.estimatedQuality != null)
    .sort((left, right) => {
      const leftPrice = left.deliveredPricePerUnit ?? Number.MAX_SAFE_INTEGER
      const rightPrice = right.deliveredPricePerUnit ?? Number.MAX_SAFE_INTEGER
      return leftPrice - rightPrice
    })
    .slice(0, 8)
})
</script>

<template>
  <div v-if="selectedCell && currentUnit" class="unit-configuration-tab-view">
    <!-- Tab navigation -->
    <nav
      class="unit-detail-tabs flex flex-nowrap items-center gap-1 overflow-x-auto bg-bg px-4 py-1 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden"
      :aria-label="t('buildingDetail.unitConfiguration')"
    >
      <button
        v-for="tab in editTabs"
        :key="tab.key"
        class="unit-tab-btn inline-flex shrink-0 items-center rounded-md border border-transparent px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted transition-colors hover:text-foreground"
        :class="selectedConfigTab === tab.key ? 'unit-tab-btn--active border-primary/40 bg-primary/10 text-primary' : 'hover:border-divider hover:bg-surface'"
        @click="selectedConfigTab = tab.key as 'config' | 'performance' | 'maintenance'"
      >
        {{ tab.label }}
      </button>
    </nav>

    <div class="unit-detail">

      <!-- ── Tab: General Settings ── -->
      <template v-if="selectedConfigTab === 'config'">
        <div class="unit-basic-info mt-3">
        <h4>{{ t(`buildingDetail.unitTypes.${currentUnit.unitType}`) }}</h4>
        <p class="unit-desc">{{ t(`buildingDetail.unitDescriptions.${currentUnit.unitType}`) }}</p>
        <div class="unit-stats">
          <span class="stat">{{ t('common.level') }}: {{ currentUnit.level }}</span>
          <span class="stat">{{ t('buildingDetail.gridPosition', { x: selectedCell.x, y: selectedCell.y }) }}</span>
          <span v-if="getDraftUnitConstructionCostLabel(currentUnit)" class="stat">
            {{ t('buildingDetail.unitCost', { cost: getDraftUnitConstructionCostLabel(currentUnit) }) }}
          </span>
          <span
            v-if="getDisplayedTicks(currentUnit) > 0"
            class="stat"
            :title="getDisplayedTicks(currentUnit) + ' ticks'"
          >
            {{ t('buildingDetail.unitUnavailableFor', { time: formatTickDuration(getDisplayedTicks(currentUnit), locale) }) }}
          </span>
        </div>
        <div class="unit-links">
          <span class="link-label">{{ t('buildingDetail.links') }}:</span>
          <span v-if="currentUnit.linkUp" class="link-badge">{{ t('buildingDetail.linkUp') }}</span>
          <span v-if="currentUnit.linkDown" class="link-badge">{{ t('buildingDetail.linkDown') }}</span>
          <span v-if="currentUnit.linkLeft" class="link-badge">{{ t('buildingDetail.linkLeft') }}</span>
          <span v-if="currentUnit.linkRight" class="link-badge">{{ t('buildingDetail.linkRight') }}</span>
          <span v-if="currentUnit.linkUpLeft" class="link-badge">{{ t('buildingDetail.linkUpLeft') }}</span>
          <span v-if="currentUnit.linkUpRight" class="link-badge">{{ t('buildingDetail.linkUpRight') }}</span>
          <span v-if="currentUnit.linkDownLeft" class="link-badge">{{ t('buildingDetail.linkDownLeft') }}</span>
          <span v-if="currentUnit.linkDownRight" class="link-badge">{{ t('buildingDetail.linkDownRight') }}</span>
        </div>

        <!-- Unit-specific configuration (shown on default tab so settings are immediately visible) -->
        <BuildingUnitConfigFields />

        <div v-if="currentUnit.unitType === 'PURCHASE'" class="unit-insight-card purchase-history-panel">
          <h5>{{ t('buildingDetail.purchasePriceQualityHistory.title') }}</h5>
          <p class="config-help">{{ t('buildingDetail.purchasePriceQualityHistory.subtitle') }}</p>
          <p v-if="sourcingCandidatesLoading" class="config-help">{{ t('common.loading') }}</p>
          <div v-else-if="purchaseSourcingHistory.length > 0" class="inventory-table mt-2">
            <div class="inventory-table-header">
              <span class="inventory-col-item">{{ t('common.city') }}</span>
              <span class="inventory-col-cost">{{ t('buildingDetail.purchasePriceQualityHistory.purchasePrice') }}</span>
              <span class="inventory-col-quality">{{ t('buildingDetail.purchasePriceQualityHistory.quality') }}</span>
            </div>
            <div v-for="entry in purchaseSourcingHistory" :key="`${entry.sourceType}-${entry.sourceCityId ?? 'none'}-${entry.rank}`" class="inventory-table-row">
              <div class="inventory-col-item">
                <span class="inventory-item-name">{{ entry.sourceCityName }}</span>
              </div>
              <div class="inventory-col-cost">
                <span class="inventory-item-cost">{{ entry.deliveredPricePerUnit != null ? formatCurrency(entry.deliveredPricePerUnit) : '—' }}</span>
              </div>
              <div class="inventory-col-quality">
                <span class="inventory-item-quality">{{ entry.estimatedQuality != null ? formatPercent(entry.estimatedQuality) : '—' }}</span>
              </div>
            </div>
          </div>
          <p v-else class="config-help">{{ t('buildingDetail.purchasePriceQualityHistory.empty') }}</p>
        </div>

        <UnitResourceHistoryPanel
          v-if="currentUnit.unitType === 'PURCHASE' && selectedHistoryItemOptions.length > 0"
          :items="selectedHistoryItemOptions"
          :selected-item-key="selectedHistoryItemKey"
          :history="selectedUnitResourceHistory"
          borderless
          @update:selected-item-key="selectedHistoryItemKey = $event"
        />
        </div>

        <!-- Exchange offers panel — shown on General tab when PURCHASE unit has EXCHANGE/OPTIMAL source -->
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
            <RouterLink v-if="selectedPurchaseResourceSlug" :to="{ name: 'exchange', query: { resource: selectedPurchaseResourceSlug, city: building?.cityId } }" class="exchange-view-link">
              {{ t('buildingDetail.exchange.viewOnExchange') }}
            </RouterLink>
          </template>
        </div>

        <div v-if="getDraftUnitConstructionCostLabel(currentUnit)" class="unit-insight-card">
          <h5>{{ t('buildingDetail.costSummaryTitle') }}</h5>
          <div class="unit-stats">
            <span class="stat">{{ t('buildingDetail.unitCost', { cost: getDraftUnitConstructionCostLabel(currentUnit) }) }}</span>
          </div>
        </div>
        <div class="unit-actions" v-if="isEditing">
          <button class="btn btn-danger btn-sm" @click="removeDraftUnit(selectedCell.x, selectedCell.y)">
            {{ t('buildingDetail.removeUnit') }}
          </button>
        </div>
      </template>

      <!-- ── Tab: Production (future use) ── -->
      <!-- ── Tab: Inventory ── -->
      <template v-else-if="selectedConfigTab === 'performance'">
        <div v-if="getUnitInventorySummary(currentUnit)" class="unit-insight-card mt-0 border-0 pt-0">
          <h5>{{ t('buildingDetail.inventory.title') }}</h5>
          <div class="inventory-summary-grid">
            <div class="inventory-summary-stat">
              <span class="inventory-summary-label">{{ t('buildingDetail.inventory.load') }}</span>
              <strong>
                {{
                  t('buildingDetail.inventory.quantity', {
                    quantity: formatUnitQuantity(getUnitInventorySummary(currentUnit)!.quantity),
                    capacity: formatUnitQuantity(getUnitInventorySummary(currentUnit)!.capacity),
                  })
                }}
              </strong>
            </div>
            <div class="inventory-summary-stat">
              <span class="inventory-summary-label">{{ t('buildingDetail.inventory.distinctItems') }}</span>
              <strong>{{ getUnitInventoryItemCount(currentUnit) }}</strong>
            </div>
            <div class="inventory-summary-stat" v-if="getUnitInventorySummary(currentUnit)!.averageQuality != null">
              <span class="inventory-summary-label">{{ t('buildingDetail.inventory.averageQuality') }}</span>
              <strong>{{ formatPercent(getUnitInventorySummary(currentUnit)!.averageQuality) }}</strong>
            </div>
            <div class="inventory-summary-stat" v-if="getUnitInventoryCostLabel(currentUnit)">
              <span class="inventory-summary-label">{{ t('buildingDetail.inventory.sourcingCosts') }}</span>
              <strong>{{ getUnitInventoryCostLabel(currentUnit) }}</strong>
            </div>
          </div>
          <div v-if="getUnitInventories(currentUnit).length > 0" class="inventory-table">
            <div class="inventory-table-header">
              <span class="inventory-col-item">{{ t('buildingDetail.inventory.item') }}</span>
              <span class="inventory-col-quantity">{{ t('buildingDetail.inventory.amount') }}</span>
              <span class="inventory-col-quality">{{ t('buildingDetail.inventory.quality') }}</span>
              <span class="inventory-col-cost">{{ t('buildingDetail.inventory.sourcingCost') }}</span>
            </div>
            <div v-for="inventory in getUnitInventories(currentUnit)" :key="inventory.id" class="inventory-table-row">
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
          borderless
          @update:selected-item-key="selectedHistoryItemKey = $event"
        />
      </template>

      <!-- ── Tab: Sales / Upgrade ── -->
      <template v-else-if="selectedConfigTab === 'maintenance'">
        <div v-if="isEditing && selectedCellUpgradeInfo !== null" class="unit-insight-card unit-upgrade-panel mt-0 border-0 pt-0" :aria-label="t('buildingDetail.accessibility.unitUpgrade')">
          <h5>{{ t('buildingDetail.unitUpgrade.sectionTitle') }}</h5>

          <!-- Upgrade in progress -->
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
              <p class="unit-upgrade-downtime-notice">{{ t('buildingDetail.unitUpgrade.pendingDowntimeNotice') }}</p>
            </div>
          </div>

          <!-- Max level -->
          <div v-else-if="selectedCellUpgradeInfo.isMaxLevel" class="unit-upgrade-max-level">
            <span class="unit-upgrade-max-badge">✅</span>
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
            <div class="unit-upgrade-stats" :aria-label="t('buildingDetail.accessibility.upgradeImpact')">
              <div class="unit-upgrade-stat-row">
                <span class="unit-upgrade-stat-label">{{ selectedCellUpgradeInfo.statLabel }}</span>
                <span class="unit-upgrade-stat-values">
                  <span class="stat-current">{{ selectedCellUpgradeInfo.currentStat.toFixed(1) }}</span>
                  <span class="stat-arrow"> → </span>
                  <span class="stat-next">{{ selectedCellUpgradeInfo.nextStat.toFixed(1) }}</span>
                </span>
              </div>
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
            <p class="unit-upgrade-downtime-notice available" :title="selectedCellUpgradeInfo.upgradeTicks + ' ticks'">
              {{ t('buildingDetail.unitUpgrade.availableDowntimeNotice', { time: formatTickDuration(selectedCellUpgradeInfo.upgradeTicks, locale) }) }}
            </p>
            <div class="unit-upgrade-meta">
              <span class="unit-upgrade-cost">{{ t('buildingDetail.unitUpgrade.cost', { cost: formatCurrency(selectedCellUpgradeInfo.upgradeCost) }) }}</span>
              <span class="unit-upgrade-duration" :title="t('buildingDetail.unitUpgrade.durationTicks', { ticks: selectedCellUpgradeInfo.upgradeTicks })">
                {{ t('buildingDetail.unitUpgrade.duration', { time: formatTickDuration(selectedCellUpgradeInfo.upgradeTicks, locale) }) }}
              </span>
            </div>
            <p v-if="unitUpgradeError" class="form-error">{{ unitUpgradeError }}</p>
            <div v-if="isSelectedCellStaged" class="unit-upgrade-staged">
              <span class="unit-upgrade-staged-badge">✅ {{ t('buildingDetail.unitUpgrade.stagedBadge') }}</span>
              <p class="unit-upgrade-stage-info">{{ t('buildingDetail.unitUpgrade.stageInfo') }}</p>
              <button class="btn btn-ghost btn-sm" @click="toggleStagedUpgrade(selectedCellUpgradeInfo!.unitId)">
                {{ t('buildingDetail.unitUpgrade.removeStagedUpgrade') }}
              </button>
            </div>
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
        <p v-else class="unit-desc">{{ t('buildingDetail.unitUpgrade.notUpgradable') }}</p>
      </template>

    </div>
  </div>
</template>

<style scoped src="./BuildingSidebar.shared.css"></style>
<style scoped src="./BuildingSidebar.analytics.css"></style>
<style scoped src="./BuildingSidebar.exchange.css"></style>

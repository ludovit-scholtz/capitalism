<script setup lang="ts">
import { computed, inject, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'
import type { ExchangeSortBy } from '@/lib/globalExchange'
import BuildingUnitConfigFields from '@/components/buildings/BuildingUnitConfigFields.vue'
import UnitResourceHistoryPanel from '@/components/buildings/UnitResourceHistoryPanel.vue'
import BuildingEnergyPanel from '@/components/buildings/BuildingEnergyPanel.vue'

const { t, locale } = useI18n()
const route = useRoute()
const router = useRouter()
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

type EditConfigTab = 'config' | 'performance' | 'maintenance'
const selectedConfigTab = ref<EditConfigTab>('config')

const editTabs = computed(() => [
  { key: 'config', label: t('buildingDetail.editTabConfig') },
  { key: 'performance', label: t('buildingDetail.editTabPerformance') },
  { key: 'maintenance', label: t('buildingDetail.editTabMaintenance') },
])

function normalizeEditTab(value: unknown): EditConfigTab {
  if (value === 'performance' || value === 'maintenance') {
    return value
  }
  return 'config'
}

function selectConfigTab(tab: EditConfigTab) {
  selectedConfigTab.value = tab
  if (!isEditing.value) return
  void router.replace({
    path: `/building/${route.params.id as string}/edit/${tab}`,
    query: route.query,
  })
}

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

/** Returns the freshness percentage (0–100) for an inventory item based on quality. */
function freshnessPercent(quality: number): number {
  return Math.round(Math.max(0, Math.min(1, quality)) * 100)
}

function qualityBadgeClass(quality: number): string {
  const pct = freshnessPercent(quality)
  if (pct > 66) return 'border-emerald-400/30 bg-emerald-500/10 text-emerald-200'
  if (pct > 33) return 'border-amber-400/30 bg-amber-500/10 text-amber-200'
  return 'border-rose-400/30 bg-rose-500/10 text-rose-200'
}

function fillHealthBadgeClass(quantity: number, capacity: number): string {
  const ratio = capacity > 0 ? quantity / capacity : 0
  if (ratio <= 0.5) return 'border-emerald-400/30 bg-emerald-500/10 text-emerald-200'
  if (ratio <= 0.85) return 'border-amber-400/30 bg-amber-500/10 text-amber-200'
  return 'border-rose-400/30 bg-rose-500/10 text-rose-200'
}

const currentInventorySummary = computed(() => {
  if (!currentUnit.value) return null
  return getUnitInventorySummary(currentUnit.value)
})

const confirmScheduleRepairUnitId = ref<string | null>(null)

function openScheduleRepairConfirm(unitId: string) {
  confirmScheduleRepairUnitId.value = unitId
}

function closeScheduleRepairConfirm() {
  confirmScheduleRepairUnitId.value = null
}

async function confirmScheduleRepair() {
  if (!confirmScheduleRepairUnitId.value) return
  await submitUnitUpgrade(confirmScheduleRepairUnitId.value)
  confirmScheduleRepairUnitId.value = null
}

watch(
  () => route.params.tab,
  (routeTab) => {
    selectedConfigTab.value = normalizeEditTab(routeTab)
  },
  { immediate: true },
)
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
        @click="selectConfigTab(tab.key as EditConfigTab)"
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
            <span v-if="getDisplayedTicks(currentUnit) > 0" class="stat" :title="getDisplayedTicks(currentUnit) + ' ticks'">
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
          <BuildingEnergyPanel v-if="isEditing" />
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
        <section class="mt-3 space-y-4" :aria-label="t('buildingDetail.inventory.title')">
          <div v-if="currentInventorySummary" class="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <article class="rounded-xl border border-divider bg-card p-4">
              <p class="text-xs font-semibold uppercase tracking-wide text-muted">{{ t('buildingDetail.inventory.load') }}</p>
              <p class="mt-2 text-lg font-semibold text-foreground">
                {{
                  t('buildingDetail.inventory.quantity', {
                    quantity: formatUnitQuantity(currentInventorySummary.quantity),
                    capacity: formatUnitQuantity(currentInventorySummary.capacity),
                  })
                }}
              </p>
              <span class="mt-2 inline-flex rounded-full border px-2 py-1 text-xs font-semibold" :class="fillHealthBadgeClass(currentInventorySummary.quantity, currentInventorySummary.capacity)">
                {{ formatPercent(Math.min(1, currentInventorySummary.quantity / Math.max(1, currentInventorySummary.capacity))) }}
              </span>
            </article>

            <article class="rounded-xl border border-divider bg-card p-4">
              <p class="text-xs font-semibold uppercase tracking-wide text-muted">{{ t('buildingDetail.inventory.distinctItems') }}</p>
              <p class="mt-2 text-lg font-semibold text-foreground">{{ getUnitInventoryItemCount(currentUnit) }}</p>
              <p v-if="getUnitInventoryCostLabel(currentUnit)" class="mt-2 text-sm text-muted">
                {{ t('buildingDetail.inventory.sourcingCosts') }}: <span class="font-semibold text-foreground">{{ getUnitInventoryCostLabel(currentUnit) }}</span>
              </p>
            </article>

            <article v-if="currentInventorySummary.averageQuality != null" class="rounded-xl border border-divider bg-card p-4">
              <p class="text-xs font-semibold uppercase tracking-wide text-muted">{{ t('buildingDetail.inventory.averageQuality') }}</p>
              <div class="mt-2 flex items-center gap-2">
                <p class="text-lg font-semibold text-foreground">{{ formatPercent(currentInventorySummary.averageQuality) }}</p>
                <span class="inline-flex rounded-full border px-2 py-1 text-xs font-semibold" :class="qualityBadgeClass(currentInventorySummary.averageQuality)">
                  {{ freshnessPercent(currentInventorySummary.averageQuality) }}%
                </span>
              </div>
            </article>

            <article class="rounded-xl border border-divider bg-card p-4">
              <p class="text-xs font-semibold uppercase tracking-wide text-muted">{{ t('buildingDetail.unitFlow') }}</p>
              <p class="mt-2 text-sm text-muted">
                {{ t('buildingDetail.inventory.load') }}: <span class="font-semibold text-foreground">{{ selectedPlannedUnitFlowSegments.fillWidth.toFixed(0) }}%</span>
              </p>
              <p class="mt-1 text-sm text-muted">
                {{ t('buildingDetail.inventory.history.inflow') }}: <span class="font-semibold text-foreground">{{ selectedPlannedUnitFlowSegments.inflowWidth.toFixed(0) }}%</span>
              </p>
              <p class="mt-1 text-sm text-muted">
                {{ t('buildingDetail.inventory.history.outflow') }}: <span class="font-semibold text-foreground">{{ selectedPlannedUnitFlowSegments.outflowWidth.toFixed(0) }}%</span>
              </p>
            </article>
          </div>

          <div v-if="getUnitInventories(currentUnit).length > 0" class="inventory-table rounded-xl border border-divider bg-card">
            <div
              class="grid grid-cols-[minmax(0,2fr)_minmax(0,1fr)_minmax(0,1fr)_minmax(0,1fr)] gap-2 border-b border-divider px-4 py-3 text-xs font-semibold uppercase tracking-wide text-muted max-sm:hidden"
            >
              <span>{{ t('buildingDetail.inventory.item') }}</span>
              <span>{{ t('buildingDetail.inventory.amount') }}</span>
              <span>{{ t('buildingDetail.inventory.quality') }}</span>
              <span>{{ t('buildingDetail.inventory.sourcingCost') }}</span>
            </div>
            <ul class="divide-y divide-divider">
              <li
                v-for="inventory in getUnitInventories(currentUnit)"
                :key="inventory.id"
                class="grid gap-3 px-4 py-3 sm:grid-cols-[minmax(0,2fr)_minmax(0,1fr)_minmax(0,1fr)_minmax(0,1fr)] sm:items-center"
              >
                <div class="flex items-center gap-2">
                  <img
                    v-if="getInventoryItemImageUrl(inventory)"
                    class="inventory-item-image h-8 w-8 rounded-md object-cover"
                    :src="getInventoryItemImageUrl(inventory)!"
                    :alt="getInventoryItemName(inventory)"
                  />
                  <span v-else class="inventory-item-avatar inline-flex h-8 w-8 items-center justify-center rounded-md border border-divider bg-surface text-xs font-semibold text-muted">{{
                    getInventoryItemMonogram(inventory)
                  }}</span>
                  <span class="inventory-item-name text-sm font-semibold text-foreground">{{ getInventoryItemName(inventory) }}</span>
                </div>
                <div class="text-sm text-foreground">
                  <span class="mr-2 text-xs font-semibold uppercase tracking-wide text-muted sm:hidden">{{ t('buildingDetail.inventory.amount') }}:</span>
                  {{ formatUnitQuantity(inventory.quantity) }}
                </div>
                <div class="text-sm text-foreground">
                  <span class="mr-2 text-xs font-semibold uppercase tracking-wide text-muted sm:hidden">{{ t('buildingDetail.inventory.quality') }}:</span>
                  <span class="inline-flex rounded-full border px-2 py-0.5 text-xs font-semibold" :class="qualityBadgeClass(inventory.quality)">
                    {{ formatPercent(inventory.quality) }}
                  </span>
                </div>
                <div class="text-sm text-foreground">
                  <span class="mr-2 text-xs font-semibold uppercase tracking-wide text-muted sm:hidden">{{ t('buildingDetail.inventory.sourcingCost') }}:</span>
                  {{ getInventoryItemSourcingCostLabel(inventory) }}
                </div>
              </li>
            </ul>
          </div>
          <p v-else class="rounded-xl border border-dashed border-divider bg-surface/60 px-4 py-3 text-sm text-muted">{{ t('buildingDetail.inventory.empty') }}</p>

          <UnitResourceHistoryPanel
            v-if="selectedHistoryItemOptions.length > 0"
            :items="selectedHistoryItemOptions"
            :selected-item-key="selectedHistoryItemKey"
            :history="selectedUnitResourceHistory"
            borderless
            @update:selected-item-key="selectedHistoryItemKey = $event"
          />
        </section>
      </template>

      <!-- ── Tab: Sales / Upgrade ── -->
      <template v-else-if="selectedConfigTab === 'maintenance'">
        <div
          v-if="isEditing && selectedCellUpgradeInfo !== null"
          class="unit-insight-card unit-upgrade-panel mt-3 space-y-4 rounded-xl border border-divider bg-card p-4"
          :aria-label="t('buildingDetail.accessibility.unitUpgrade')"
        >
          <h5 class="mb-4 text-sm font-semibold uppercase tracking-wide text-muted">{{ t('buildingDetail.unitUpgrade.sectionTitle') }}</h5>

          <!-- Upgrade in progress -->
          <div v-if="selectedCellPendingUpgrade" class="unit-upgrade-in-progress flex items-start gap-3 rounded-xl border border-amber-400/30 bg-amber-500/10 p-4">
            <div class="unit-upgrade-progress-badge inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-full border border-amber-300/30 bg-amber-400/15 text-lg">⏳</div>
            <div class="unit-upgrade-progress-body min-w-0 space-y-2">
              <strong class="block text-sm font-semibold text-foreground">{{ t('buildingDetail.unitUpgrade.pendingTitle') }}</strong>
              <p class="unit-upgrade-progress-desc text-sm leading-6 text-foreground/90" :title="selectedCellPendingUpgrade.ticksRemaining + ' ticks remaining'">
                {{
                  t('buildingDetail.unitUpgrade.pendingBody', {
                    level: selectedCellPendingUpgrade.level,
                    time: formatTickDuration(selectedCellPendingUpgrade.ticksRemaining, locale),
                  })
                }}
              </p>
              <p class="unit-upgrade-downtime-notice text-sm leading-6 text-amber-100/95">{{ t('buildingDetail.unitUpgrade.pendingDowntimeNotice') }}</p>
            </div>
          </div>

          <!-- Max level -->
          <div v-else-if="selectedCellUpgradeInfo.isMaxLevel" class="unit-upgrade-max-level flex items-start gap-3 rounded-xl border border-emerald-400/30 bg-emerald-500/10 p-4">
            <span class="unit-upgrade-max-badge inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-full border border-emerald-300/30 bg-emerald-400/15 text-lg"> ✅ </span>
            <div class="space-y-1.5">
              <span class="block text-sm font-semibold text-foreground">{{ t('buildingDetail.unitUpgrade.maxLevel') }}</span>
              <p class="unit-upgrade-max-note text-sm leading-6 text-emerald-100/95">{{ t('buildingDetail.unitUpgrade.maxLevelNote') }}</p>
            </div>
          </div>

          <!-- Not upgradable -->
          <div v-else-if="!selectedCellUpgradeInfo.isUpgradable" class="unit-upgrade-not-available rounded-xl border border-divider bg-surface p-3">
            <p class="text-sm leading-6 text-muted">{{ t('buildingDetail.unitUpgrade.notUpgradable') }}</p>
          </div>

          <!-- Upgrade available -->
          <div v-else class="unit-upgrade-available space-y-4">
            <div class="unit-upgrade-levels flex flex-wrap items-center gap-3 rounded-xl border border-divider bg-surface px-4 py-3">
              <span class="unit-upgrade-level current-level inline-flex items-center rounded-full border border-divider bg-card px-3 py-1.5 text-sm font-semibold text-foreground">
                {{ t('buildingDetail.unitUpgrade.currentLevel', { level: selectedCellUpgradeInfo.currentLevel }) }}
              </span>
              <span class="unit-upgrade-arrow text-lg font-semibold text-muted">→</span>
              <span class="unit-upgrade-level next-level inline-flex items-center rounded-full border border-primary/30 bg-primary/10 px-3 py-1.5 text-sm font-semibold text-primary">
                {{ t('buildingDetail.unitUpgrade.nextLevel', { level: selectedCellUpgradeInfo.nextLevel }) }}
              </span>
            </div>
            <div class="unit-upgrade-stats space-y-3 rounded-xl border border-divider bg-surface p-4" :aria-label="t('buildingDetail.accessibility.upgradeImpact')">
              <div class="unit-upgrade-stat-row grid gap-2 border-b border-divider/70 pb-3 last:border-b-0 last:pb-0 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center">
                <span class="unit-upgrade-stat-label text-xs font-semibold uppercase tracking-wide text-muted">{{ selectedCellUpgradeInfo.statLabel }}</span>
                <span class="unit-upgrade-stat-values flex flex-wrap items-center gap-x-2 gap-y-1 text-sm font-medium text-foreground sm:justify-end">
                  <span class="stat-current">{{ selectedCellUpgradeInfo.currentStat.toFixed(1) }}</span>
                  <span class="stat-arrow text-muted">→</span>
                  <span class="stat-next font-semibold text-primary">{{ selectedCellUpgradeInfo.nextStat.toFixed(1) }}</span>
                </span>
              </div>
              <div
                class="unit-upgrade-stat-row grid gap-2 border-b border-divider/70 pb-3 last:border-b-0 last:pb-0 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center"
                :aria-label="t('buildingDetail.accessibility.storageCapacityDelta')"
              >
                <span class="unit-upgrade-stat-label text-xs font-semibold uppercase tracking-wide text-muted">{{ t('buildingDetail.unitUpgrade.storageCapacity') }}</span>
                <span class="unit-upgrade-stat-values flex flex-wrap items-center gap-x-2 gap-y-1 text-sm font-medium text-foreground sm:justify-end">
                  <span class="stat-current">{{ selectedCellUpgradeInfo.currentStorageCapacity.toFixed(0) }}</span>
                  <span class="stat-arrow text-muted">→</span>
                  <span class="stat-next font-semibold text-primary">{{ selectedCellUpgradeInfo.nextStorageCapacity.toFixed(0) }}</span>
                  <span class="stat-delta stat-delta-positive rounded-full border border-emerald-400/20 bg-emerald-500/10 px-2 py-0.5 text-xs font-semibold text-emerald-200">
                    +{{ (selectedCellUpgradeInfo.nextStorageCapacity - selectedCellUpgradeInfo.currentStorageCapacity).toFixed(0) }}
                  </span>
                </span>
              </div>
              <div
                class="unit-upgrade-stat-row grid gap-2 border-b border-divider/70 pb-3 last:border-b-0 last:pb-0 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center"
                :aria-label="t('buildingDetail.accessibility.laborCostDelta')"
              >
                <span class="unit-upgrade-stat-label text-xs font-semibold uppercase tracking-wide text-muted">{{ t('buildingDetail.unitUpgrade.laborCost') }}</span>
                <span class="unit-upgrade-stat-values flex flex-wrap items-center gap-x-2 gap-y-1 text-sm font-medium text-foreground sm:justify-end">
                  <span class="stat-current">{{ formatCurrency(selectedCellUpgradeInfo.currentLaborCostPerTick) }}</span>
                  <span class="stat-arrow text-muted">→</span>
                  <span class="stat-next font-semibold text-primary">{{ formatCurrency(selectedCellUpgradeInfo.nextLaborCostPerTick) }}</span>
                  <span class="stat-delta stat-delta-negative rounded-full border border-rose-400/20 bg-rose-500/10 px-2 py-0.5 text-xs font-semibold text-rose-200">
                    +{{ formatCurrency(selectedCellUpgradeInfo.nextLaborCostPerTick - selectedCellUpgradeInfo.currentLaborCostPerTick) }}
                  </span>
                </span>
              </div>
              <div class="unit-upgrade-stat-row grid gap-2 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center" :aria-label="t('buildingDetail.accessibility.energyCostDelta')">
                <span class="unit-upgrade-stat-label text-xs font-semibold uppercase tracking-wide text-muted">{{ t('buildingDetail.unitUpgrade.energyCost') }}</span>
                <span class="unit-upgrade-stat-values flex flex-wrap items-center gap-x-2 gap-y-1 text-sm font-medium text-foreground sm:justify-end">
                  <span class="stat-current">{{ formatCurrency(selectedCellUpgradeInfo.currentEnergyCostPerTick) }}</span>
                  <span class="stat-arrow text-muted">→</span>
                  <span class="stat-next font-semibold text-primary">{{ formatCurrency(selectedCellUpgradeInfo.nextEnergyCostPerTick) }}</span>
                  <span class="stat-delta stat-delta-negative rounded-full border border-rose-400/20 bg-rose-500/10 px-2 py-0.5 text-xs font-semibold text-rose-200">
                    +{{ formatCurrency(selectedCellUpgradeInfo.nextEnergyCostPerTick - selectedCellUpgradeInfo.currentEnergyCostPerTick) }}
                  </span>
                </span>
              </div>
            </div>
            <p
              class="unit-upgrade-downtime-notice available rounded-xl border border-divider bg-surface px-4 py-3 text-sm leading-6 text-foreground"
              :title="selectedCellUpgradeInfo.upgradeTicks + ' ticks'"
            >
              {{ t('buildingDetail.unitUpgrade.availableDowntimeNotice', { time: formatTickDuration(selectedCellUpgradeInfo.upgradeTicks, locale) }) }}
            </p>
            <div class="unit-upgrade-meta flex flex-wrap gap-3">
              <span class="unit-upgrade-cost inline-flex items-center rounded-full border border-divider bg-surface px-3 py-1.5 text-sm font-medium text-foreground">
                {{ t('buildingDetail.unitUpgrade.cost', { cost: formatCurrency(selectedCellUpgradeInfo.upgradeCost) }) }}
              </span>
              <span
                class="unit-upgrade-duration inline-flex items-center rounded-full border border-divider bg-surface px-3 py-1.5 text-sm font-medium text-foreground"
                :title="t('buildingDetail.unitUpgrade.durationTicks', { ticks: selectedCellUpgradeInfo.upgradeTicks })"
              >
                {{ t('buildingDetail.unitUpgrade.duration', { time: formatTickDuration(selectedCellUpgradeInfo.upgradeTicks, locale) }) }}
              </span>
            </div>
            <p v-if="unitUpgradeError" class="form-error rounded-lg border border-rose-400/30 bg-rose-500/10 px-3 py-2 text-sm text-rose-100">{{ unitUpgradeError }}</p>
            <div v-if="isSelectedCellStaged" class="unit-upgrade-staged space-y-3 rounded-xl border border-emerald-400/30 bg-emerald-500/10 p-4">
              <span class="unit-upgrade-staged-badge inline-flex items-center gap-2 rounded-full border border-emerald-300/30 bg-emerald-400/15 px-3 py-1.5 text-sm font-semibold text-emerald-100">
                ✅ {{ t('buildingDetail.unitUpgrade.stagedBadge') }}
              </span>
              <p class="unit-upgrade-stage-info text-sm leading-6 text-emerald-100/95">{{ t('buildingDetail.unitUpgrade.stageInfo') }}</p>
              <button class="btn btn-ghost btn-sm" @click="toggleStagedUpgrade(selectedCellUpgradeInfo.unitId)">
                {{ t('buildingDetail.unitUpgrade.removeStagedUpgrade') }}
              </button>
            </div>
            <div v-else class="unit-upgrade-actions flex flex-wrap gap-2">
              <button class="btn btn-primary btn-sm unit-upgrade-stage-btn" @click="toggleStagedUpgrade(selectedCellUpgradeInfo.unitId)">
                {{ t('buildingDetail.unitUpgrade.stageButton') }}
              </button>
              <button class="btn btn-ghost btn-sm unit-upgrade-confirm-btn" :disabled="schedulingUpgrade" @click="openScheduleRepairConfirm(selectedCellUpgradeInfo.unitId)">
                {{ schedulingUpgrade ? t('buildingDetail.unitUpgrade.confirmingButton') : t('buildingDetail.unitUpgrade.confirmButton') }}
              </button>
            </div>
          </div>

          <div
            v-if="confirmScheduleRepairUnitId"
            class="mt-3 space-y-3 rounded-xl border border-primary/40 bg-primary/10 p-4"
            role="dialog"
            aria-modal="true"
            :aria-label="t('buildingDetail.unitUpgrade.confirmButton')"
          >
            <p class="text-sm leading-6 text-foreground">{{ t('buildingDetail.unitUpgrade.availableDowntimeNotice', { time: formatTickDuration(selectedCellUpgradeInfo.upgradeTicks, locale) }) }}</p>
            <div class="flex flex-wrap gap-2">
              <button class="btn btn-primary btn-sm" :disabled="schedulingUpgrade" @click="confirmScheduleRepair">
                {{ schedulingUpgrade ? t('buildingDetail.unitUpgrade.confirmingButton') : t('common.confirm') }}
              </button>
              <button class="btn btn-ghost btn-sm" @click="closeScheduleRepairConfirm">
                {{ t('common.cancel') }}
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

<script setup lang="ts">
import { inject, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'
import UnitResourceHistoryPanel from '@/components/buildings/UnitResourceHistoryPanel.vue'
import SeasonalOutlookPanel from '@/components/buildings/SeasonalOutlookPanel.vue'
import MiningResourceStatusPanel from '@/components/buildings/MiningResourceStatusPanel.vue'
import type { BuildingUnit } from '@/types'
import type { ExchangeSortBy } from '@/lib/globalExchange'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const {
  locale,
  building,
  selectedCell,
  exchangeOffersLoading,
  exchangeSortBy,
  procurementPreview,
  procurementPreviewLoading,
  sourcingCandidates,
  sourcingCandidatesLoading,
  recentActivity,
  recentActivityLoading,
  publicSalesAnalytics,
  publicSalesMarketEvents,
  publicSalesAnalyticsLoading,
  unitProductAnalytics,
  unitProductAnalyticsLoading,
  quickPriceInput,
  quickPriceSaving,
  quickPriceSuccess,
  quickPriceError,
  quickInventoryThresholdInput,
  quickInventoryThresholdSaving,
  quickInventoryThresholdSuccess,
  quickInventoryThresholdError,
  selectedHistoryItemKey,
  showFlushConfirmDialog,
  flushingStorage,
  flushStorageError,
  flushStorageSuccess,
  selectedUnitTab,
  activeUnits,
  selectedPurchaseUnit,
  selectedPublicSalesUnit,
  selectedManufacturingUnit,
  selectedHistoryItemOptions,
  selectedUnitResourceHistory,
  cityCurrencyCode,
  miMaxRevenue,
  miMaxQuantitySold,
  miMaxPricePerUnit,
  miMaxAbsProfit,
  upaMaxAbsProfit,
  upaMaxCost,
  upaMaxEstRevenue,
  currentPublicSalesMinPrice,
  unitDetailTabs,
  exchangeOfferItems,
  allExchangeOffersBlocked,
  bestExchangeOfferCityId,
  logisticsTrapWarning,
  sourcingCheapestStickerDiffersFromBestLanded,
  selectedPurchaseResourceSlug,
  selectedActiveUnitOperationalStatus,
  selectedActiveUnitFlowSegments,
  setReadOnlySelectedCell,
  getResourceName,
  getProductName,
  getBrandScopeLabel,
  getUnitInventorySummary,
  getUnitInventories,
  getUnitInventoryItemCount,
  formatCurrency,
  formatGameTickTime,
  formatPercent,
  formatUnitQuantity,
  getUnitAtFrom,
  getInventoryItemImageUrl,
  getInventoryItemMonogram,
  getInventoryItemSourcingCostPerUnitLabel,
  getInventoryItemName,
  getInventoryItemSourcingCostLabel,
  getUnitInventoryCostLabel,
  getLocalizedIndustry,
  submitQuickPriceUpdate,
  submitPublicSalesInventoryAlertThreshold,
  submitFlushStorage,
} = bd

/** Mining rate table (units/tick) matching backend GameConstants.MiningRate. */
function getMiningRateForLevel(level: number): number {
  if (level <= 1) return 10
  if (level === 2) return 25
  if (level === 3) return 50
  if (level === 4) return 100
  return 10 * Math.pow(2, Math.max(level - 1, 0))
}

/** Mining rate per tick for the currently selected MINING unit (null when not a mining unit). */
const selectedMiningRate = computed<number | null>(() => {
  if (!selectedCell.value) return null
  const unit = getUnitAtFrom(activeUnits.value, selectedCell.value.x, selectedCell.value.y) as BuildingUnit | null
  if (!unit || unit.unitType !== 'MINING') return null
  return getMiningRateForLevel(unit.level)
})

/** Price recommendation state relative to the city market clearing price. */
const priceRecState = computed<'noData' | 'competitive' | 'slightlyAbove' | 'overpriced'>(() => {
  const marketPrice = publicSalesAnalytics.value?.cityMarketClearingPrice
  if (marketPrice == null) return 'noData'
  const playerPrice = currentPublicSalesMinPrice.value
  if (playerPrice <= 0 || playerPrice <= marketPrice) return 'competitive'
  if (playerPrice <= marketPrice * 1.3) return 'slightlyAbove'
  return 'overpriced'
})

function operationalStatusCardClass(status: string): string {
  const normalizedStatus = status.toLowerCase()

  if (normalizedStatus === 'running' || normalizedStatus === 'active') {
    return 'border-emerald-400/40 bg-emerald-500/10'
  }

  if (normalizedStatus === 'blocked' || normalizedStatus === 'offline' || normalizedStatus === 'suspended') {
    return 'border-rose-400/40 bg-rose-500/10'
  }

  if (normalizedStatus === 'idle' || normalizedStatus === 'waiting' || normalizedStatus === 'starved') {
    return 'border-amber-400/40 bg-amber-500/10'
  }

  return 'border-sky-400/40 bg-sky-500/10'
}

function operationalStatusBadgeClass(status: string): string {
  const normalizedStatus = status.toLowerCase()

  if (normalizedStatus === 'running' || normalizedStatus === 'active') {
    return 'border-emerald-300/60 bg-emerald-500/15 text-emerald-700 dark:text-emerald-300'
  }

  if (normalizedStatus === 'blocked' || normalizedStatus === 'offline' || normalizedStatus === 'suspended') {
    return 'border-rose-300/60 bg-rose-500/15 text-rose-700 dark:text-rose-300'
  }

  if (normalizedStatus === 'idle' || normalizedStatus === 'waiting' || normalizedStatus === 'starved') {
    return 'border-amber-300/60 bg-amber-500/15 text-amber-700 dark:text-amber-300'
  }

  return 'border-sky-300/60 bg-sky-500/15 text-sky-700 dark:text-sky-300'
}

type ShareEntry = {
  label: string
  companyId: string | null
  share: number
  isUnmet: boolean
}

type CompetitionLegendEntry = {
  label: string
  share: number
  color: string
  isSelf: boolean
  isUnmet: boolean
}

function buildCompetitionLegend(entries: ShareEntry[] | null | undefined, ownCompanyId: string | null | undefined): CompetitionLegendEntry[] {
  if (!entries || entries.length === 0) {
    return []
  }

  const palette = ['#2563eb', '#0ea5e9', '#10b981', '#f59e0b', '#f97316', '#ec4899', '#8b5cf6', '#22c55e']
  const sellers = entries.filter((entry) => !entry.isUnmet).sort((left, right) => right.share - left.share)
  const unmet = entries.find((entry) => entry.isUnmet)
  let anonymizedIndex = 0

  const ranked = sellers.map((entry, index) => {
    const isSelf = !!ownCompanyId && entry.companyId === ownCompanyId
    const label = !isSelf && index >= 3 ? t('buildingDetail.marketIntelligence.competition.anonymousPlayer', { code: String.fromCharCode(65 + anonymizedIndex++) }) : entry.label
    return {
      label,
      share: Math.max(0, entry.share),
      color: palette[index % palette.length] ?? '#6b7280',
      isSelf,
      isUnmet: false,
    }
  })

  if (unmet) {
    ranked.push({
      label: t('buildingDetail.marketIntelligence.competition.unmetDemand'),
      share: Math.max(0, unmet.share),
      color: '#94a3b8',
      isSelf: false,
      isUnmet: true,
    })
  }

  return ranked
}

function buildCompetitionPieGradient(entries: CompetitionLegendEntry[]): string {
  if (entries.length === 0) {
    return 'conic-gradient(#cbd5e1 0deg 360deg)'
  }

  const total = entries.reduce((sum, entry) => sum + entry.share, 0)
  if (total <= 0) {
    return 'conic-gradient(#cbd5e1 0deg 360deg)'
  }

  let cursor = 0
  const segments = entries.map((entry) => {
    const degrees = (entry.share / total) * 360
    const start = cursor
    const end = cursor + degrees
    cursor = end
    return `${entry.color} ${start.toFixed(2)}deg ${end.toFixed(2)}deg`
  })

  if (cursor < 360) {
    segments.push(`var(--color-divider) ${cursor.toFixed(2)}deg 360deg`)
  }

  return `conic-gradient(${segments.join(', ')})`
}
</script>

<template>
  <!-- Read-only unit detail sidebar (click on active grid) -->
  <div class="sidebar">
    <div class="unit-config">
      <div class="unit-config-header">
        <h3>{{ selectedCell ? t('buildingDetail.unitDetails') : t('buildingDetail.buildingDetails') }}</h3>
        <button v-if="selectedCell" class="btn btn-ghost" @click="setReadOnlySelectedCell(null)">{{ t('common.close') }}</button>
      </div>
      <!-- Unit detail tab navigation -->
      <nav
        v-if="unitDetailTabs.length > 0"
        class="unit-detail-tabs flex flex-nowrap items-center gap-1 overflow-x-auto bg-bg px-4 py-1 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden"
        :aria-label="t('buildingDetail.accessibility.unitDetailSections')"
      >
        <button
          v-for="tab in unitDetailTabs"
          :key="tab.key"
          class="unit-tab-btn inline-flex shrink-0 items-center rounded-md border border-transparent px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted transition-colors hover:text-foreground"
          :class="selectedUnitTab === tab.key ? 'unit-tab-btn--active border-primary/40 bg-primary/10 text-primary' : 'hover:border-divider hover:bg-surface'"
          @click="selectedUnitTab = tab.key"
        >
          {{ t(`buildingDetail.unitTabs.${tab.key}`) }}
        </button>
      </nav>
      <div class="unit-detail">
        <!-- ── Basic Info tab ───────────────────────────────────── --><template v-if="selectedUnitTab === 'basicInfo'"
          ><h4>{{ t(`buildingDetail.unitTypes.${getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y)!.unitType}`) }}</h4>
          <p class="unit-desc">{{ t(`buildingDetail.unitDescriptions.${getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y)!.unitType}`) }}</p>
          <div class="unit-stats">
            <span class="stat">{{ t('common.level') }}: {{ getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y)!.level }}</span
            ><span class="stat">{{ t('buildingDetail.gridPosition', { x: selectedCell!.x, y: selectedCell!.y }) }}</span>
          </div>
          <div class="unit-config-readonly-details">
            <span class="stat" v-if="(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).resourceTypeId">
              {{ t('buildingDetail.config.resourceType') }}: {{ getResourceName((getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).resourceTypeId) }} </span
            ><span class="stat" v-if="(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).productTypeId">
              {{ t('buildingDetail.config.productType') }}: {{ getProductName((getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).productTypeId) }} </span
            ><span class="stat" v-if="(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).minPrice != null">
              {{ t('buildingDetail.config.minPrice') }}: {{ formatCurrency((getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).minPrice) }} </span
            ><span class="stat" v-if="(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).maxPrice != null">
              {{ t('buildingDetail.config.maxPrice') }}: {{ formatCurrency((getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).maxPrice) }} </span
            ><span class="stat" v-if="(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).purchaseSource">
              {{ t('buildingDetail.config.procurementMode') }}:
              {{ t(`buildingDetail.config.procurementMode_${(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).purchaseSource}`) }} </span
            ><span class="stat" v-if="(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).saleVisibility">
              {{ t('buildingDetail.config.saleVisibility') }}: {{ (getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).saleVisibility }} </span
            ><span class="stat" v-if="(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).budget != null">
              {{ t('buildingDetail.config.budget') }}: {{ formatCurrency((getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).budget) }} </span
            ><span class="stat" v-if="(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).brandScope">
              {{ t('buildingDetail.config.brandScope') }}: {{ getBrandScopeLabel((getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y) as BuildingUnit).brandScope) }} </span
            ><span
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
            class="unit-insight-card rounded-2xl border p-4 sm:p-5"
            :class="operationalStatusCardClass(selectedActiveUnitOperationalStatus.status)"
            :aria-label="t('buildingDetail.accessibility.unitOperationalStatus')"
          >
            <h5 class="mb-3 text-sm font-semibold uppercase tracking-wide text-foreground">{{ t('buildingDetail.operationalStatus.title') }}</h5>
            <div class="flex flex-wrap items-center gap-2.5">
              <span
                class="status-badge inline-flex items-center rounded-full border px-2.5 py-1 text-xs font-semibold uppercase tracking-wide"
                :class="[operationalStatusBadgeClass(selectedActiveUnitOperationalStatus.status), `status-${selectedActiveUnitOperationalStatus.status.toLowerCase()}`]"
              >
                {{ t(`buildingDetail.operationalStatus.${selectedActiveUnitOperationalStatus.status}`) }} </span
              ><span v-if="selectedActiveUnitOperationalStatus.idleTicks > 0" class="inline-flex items-center rounded-full border border-divider bg-card px-2.5 py-1 text-xs font-medium text-muted">
                {{ t('buildingDetail.operationalStatus.idleTicks', { count: selectedActiveUnitOperationalStatus.idleTicks }) }}
              </span>
            </div>
            <p v-if="selectedActiveUnitOperationalStatus.blockedReason" class="mt-3 text-sm leading-6 text-foreground/90">{{ selectedActiveUnitOperationalStatus.blockedReason }}</p>
            <!-- Next-tick operating costs breakdown -->
            <div
              v-if="selectedActiveUnitOperationalStatus.nextTickLaborCost != null || selectedActiveUnitOperationalStatus.nextTickEnergyCost != null"
              class="operating-costs-row mt-4 grid gap-2 rounded-xl border border-divider bg-card/80 p-3"
            >
              <span class="text-[0.7rem] font-semibold uppercase tracking-[0.18em] text-muted">{{ t('buildingDetail.operatingCost.title') }}</span
              ><span v-if="selectedActiveUnitOperationalStatus.nextTickLaborCost != null" class="operating-cost-item text-sm text-foreground/90">
                {{ t('buildingDetail.operatingCost.labor', { cost: formatCurrency(selectedActiveUnitOperationalStatus.nextTickLaborCost) }) }} </span
              ><span v-if="selectedActiveUnitOperationalStatus.nextTickEnergyCost != null" class="operating-cost-item text-sm text-foreground/90">
                {{ t('buildingDetail.operatingCost.energy', { cost: formatCurrency(selectedActiveUnitOperationalStatus.nextTickEnergyCost) }) }}
              </span>
            </div>
          </div>
          <!-- Unit Upgrade Panel removed from read-only view; it now lives in edit mode only. -->
          <!-- Mining Resource Status Panel — shown only for MINING units -->
          <MiningResourceStatusPanel
            v-if="building && getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y)?.unitType === 'MINING'"
            :building="building"
            :mining-rate-per-tick="selectedMiningRate" /></template
        ><!-- ── Quick Actions tab (PUBLIC_SALES only) ──────────── --><template v-else-if="selectedUnitTab === 'quickActions'"
          ><div class="unit-insight-card mt-0 border-0 pt-0" :aria-label="t('buildingDetail.accessibility.quickActions')">
            <div class="rounded-xl border border-divider bg-surface p-4 sm:p-5">
              <h5 class="m-0 text-sm font-semibold text-foreground">{{ t('buildingDetail.unitTabs.quickActionsHeading') }}</h5>
              <p class="unit-desc mt-2 text-sm text-muted">{{ t('buildingDetail.unitTabs.quickActionsDesc') }}</p>
              <div v-if="selectedPublicSalesUnit && selectedPublicSalesUnit.minPrice != null" class="quick-action-current-price mt-3 grid gap-1 rounded-lg border border-divider bg-card px-3 py-2">
                <span class="mi-metric-label text-[0.65rem] font-semibold uppercase tracking-wide text-muted">{{ t('buildingDetail.marketIntelligence.configuredPrice') }}</span
                ><strong class="mi-metric-value text-base font-semibold text-foreground">{{ formatCurrency(currentPublicSalesMinPrice) }}</strong>
              </div>
              <!-- Price Recommendation Badge — shows player price vs city-wide market clearing price -->
              <div
                v-if="publicSalesAnalytics"
                class="price-recommendation-badge mt-3 flex items-center justify-between gap-2 rounded-lg border px-3 py-2 text-xs"
                :class="{
                  'border-divider bg-surface text-muted': priceRecState === 'noData',
                  'border-emerald-400/50 bg-emerald-500/10 text-emerald-800 dark:text-emerald-300': priceRecState === 'competitive',
                  'border-amber-400/50 bg-amber-500/10 text-amber-700 dark:text-amber-300': priceRecState === 'slightlyAbove',
                  'border-red-400/50 bg-red-500/10 text-red-700 dark:text-red-300': priceRecState === 'overpriced',
                }"
                :title="t('buildingDetail.marketIntelligence.priceRecommendation.tooltip')"
              >
                <span class="font-semibold uppercase tracking-wide">{{ t('buildingDetail.marketIntelligence.priceRecommendation.title') }}</span>
                <span v-if="priceRecState === 'noData'" class="text-muted">
                  {{ t('buildingDetail.marketIntelligence.priceRecommendation.noData') }}
                </span>
                <span v-else class="flex items-center gap-1.5">
                  <strong>{{ formatCurrency(publicSalesAnalytics.cityMarketClearingPrice!) }}</strong>
                  <span
                    class="price-rec-label rounded-full border px-1.5 py-0.5 text-[0.6rem] font-bold uppercase"
                    :class="{
                      'border-emerald-400/50 bg-emerald-500/15': priceRecState === 'competitive',
                      'border-amber-400/50 bg-amber-500/15': priceRecState === 'slightlyAbove',
                      'border-red-400/50 bg-red-500/15': priceRecState === 'overpriced',
                    }"
                  >
                    {{
                      priceRecState === 'competitive'
                        ? t('buildingDetail.marketIntelligence.priceRecommendation.competitive')
                        : priceRecState === 'slightlyAbove'
                          ? t('buildingDetail.marketIntelligence.priceRecommendation.slightlyAbove')
                          : t('buildingDetail.marketIntelligence.priceRecommendation.overpriced')
                    }}
                  </span>
                </span>
              </div>
              <div class="mt-3" :aria-label="t('buildingDetail.accessibility.quickPriceUpdate')">
                <!-- Directional impact hint derived from elasticity -->
                <div
                  v-if="publicSalesAnalytics && publicSalesAnalytics.elasticityIndex !== null && quickPriceInput !== null && currentPublicSalesMinPrice > 0"
                  class="mi-price-impact-hint mb-3 rounded-lg border px-3 py-2 text-xs"
                  :class="
                    quickPriceInput > currentPublicSalesMinPrice
                      ? 'mi-price-impact-raise border-amber-400/50 bg-amber-500/10 text-amber-700 dark:text-amber-300'
                      : quickPriceInput < currentPublicSalesMinPrice
                        ? 'mi-price-impact-lower border-emerald-400/50 bg-emerald-500/10 text-emerald-700 dark:text-emerald-300'
                        : 'border-divider bg-surface text-muted'
                  "
                >
                  <template v-if="quickPriceInput > currentPublicSalesMinPrice">
                    {{ t('buildingDetail.marketIntelligence.priceUpdate.raisingHint', { elasticity: Math.abs(publicSalesAnalytics.elasticityIndex).toFixed(1) }) }} </template
                  ><template v-else-if="quickPriceInput < currentPublicSalesMinPrice">
                    {{ t('buildingDetail.marketIntelligence.priceUpdate.loweringHint', { elasticity: Math.abs(publicSalesAnalytics.elasticityIndex).toFixed(1) }) }}
                  </template>
                </div>
                <div class="mi-price-update-row grid gap-2 md:grid-cols-[minmax(0,1fr)_180px_auto] md:items-end">
                  <label class="mi-price-update-label flex flex-col gap-1 text-xs font-semibold uppercase tracking-wide text-muted" for="quick-price-input"
                    ><span> {{ t('buildingDetail.marketIntelligence.priceUpdate.newPrice') }} </span
                    ><span class="currency-badge w-fit rounded-full border border-divider bg-bg px-2 py-0.5 text-[0.65rem] text-foreground">{{ cityCurrencyCode }}</span></label
                  ><input
                    id="quick-price-input"
                    type="number"
                    class="mi-price-input form-input"
                    :placeholder="selectedPublicSalesUnit?.minPrice?.toString() ?? ''"
                    :min="0.01"
                    :step="0.01"
                    v-model.number="quickPriceInput"
                  /><button class="btn btn-primary mi-price-update-btn" :disabled="quickPriceSaving || quickPriceInput === null || quickPriceInput <= 0" @click="submitQuickPriceUpdate">
                    {{ quickPriceSaving ? t('buildingDetail.marketIntelligence.priceUpdate.saving') : t('buildingDetail.marketIntelligence.priceUpdate.apply') }}
                  </button>
                </div>
                <p v-if="quickPriceSuccess" class="mi-price-success mt-2 rounded-md border border-emerald-300/50 bg-emerald-500/10 px-2.5 py-2 text-xs text-emerald-800 dark:text-emerald-300">
                  {{ t('buildingDetail.marketIntelligence.priceUpdate.success') }}
                </p>
                <p v-if="quickPriceError" class="mi-price-error mt-2 rounded-md border border-red-300/50 bg-red-500/10 px-2.5 py-2 text-xs text-red-700 dark:text-red-300">{{ quickPriceError }}</p>
              </div>

              <div class="mt-4 rounded-lg border border-divider bg-card px-3 py-3">
                <div class="flex flex-wrap items-center justify-between gap-2">
                  <span class="text-xs font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.marketIntelligence.inventoryAlert.title') }}</span>
                  <span class="currency-badge rounded-full border border-divider bg-bg px-2 py-0.5 text-[0.65rem] text-foreground">{{
                    t('buildingDetail.marketIntelligence.inventoryAlert.unitHint')
                  }}</span>
                </div>
                <p class="mt-2 text-xs text-muted">{{ t('buildingDetail.marketIntelligence.inventoryAlert.help') }}</p>
                <div class="mt-3 grid gap-2 md:grid-cols-[minmax(0,1fr)_auto] md:items-end">
                  <input
                    id="quick-inventory-threshold-input"
                    v-model="quickInventoryThresholdInput"
                    type="number"
                    min="0"
                    step="0.01"
                    class="mi-price-input form-input"
                    :placeholder="t('buildingDetail.marketIntelligence.inventoryAlert.placeholder')"
                  />
                  <button class="btn btn-secondary" :disabled="quickInventoryThresholdSaving" @click="submitPublicSalesInventoryAlertThreshold">
                    {{ quickInventoryThresholdSaving ? t('common.loading') : t('buildingDetail.marketIntelligence.inventoryAlert.save') }}
                  </button>
                </div>
                <p
                  v-if="quickInventoryThresholdSuccess"
                  class="mi-price-success mt-2 rounded-md border border-emerald-300/50 bg-emerald-500/10 px-2.5 py-2 text-xs text-emerald-800 dark:text-emerald-300"
                >
                  {{ t('buildingDetail.marketIntelligence.inventoryAlert.saved') }}
                </p>
                <p v-if="quickInventoryThresholdError" class="mi-price-error mt-2 rounded-md border border-red-300/50 bg-red-500/10 px-2.5 py-2 text-xs text-red-700 dark:text-red-300">
                  {{ quickInventoryThresholdError }}
                </p>
              </div>
            </div>
          </div></template
        ><!-- ── Inventory tab ────────────────────────────────────── --><template v-else-if="selectedUnitTab === 'inventory'"
          ><div v-if="getUnitInventorySummary(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y))" class="unit-insight-card">
            <h5>{{ t('buildingDetail.inventory.title') }}</h5>
            <div class="grid grid-cols-[repeat(auto-fit,minmax(120px,1fr))] gap-3 mb-4">
              <div class="inventory-summary-stat rounded-lg border border-divider bg-card px-3 py-2 flex flex-col gap-0.5">
                <span class="text-[0.7rem] font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.inventory.load') }}</span
                ><strong class="text-sm text-foreground">
                  {{
                    t('buildingDetail.inventory.quantity', {
                      quantity: formatUnitQuantity(getUnitInventorySummary(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y))!.quantity),
                      capacity: formatUnitQuantity(getUnitInventorySummary(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y))!.capacity),
                    })
                  }}
                </strong>
              </div>
              <div class="inventory-summary-stat rounded-lg border border-divider bg-card px-3 py-2 flex flex-col gap-0.5">
                <span class="text-[0.7rem] font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.inventory.distinctItems') }}</span
                ><strong class="text-sm text-foreground">{{ getUnitInventoryItemCount(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y)) }}</strong>
              </div>
              <div
                v-if="getUnitInventorySummary(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y))!.averageQuality != null"
                class="inventory-summary-stat rounded-lg border border-divider bg-card px-3 py-2 flex flex-col gap-0.5"
              >
                <span class="text-[0.7rem] font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.inventory.averageQuality') }}</span
                ><strong class="text-sm text-foreground">{{ formatPercent(getUnitInventorySummary(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y))!.averageQuality) }}</strong>
              </div>
              <div
                v-if="getUnitInventoryCostLabel(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y))"
                class="inventory-summary-stat rounded-lg border border-divider bg-card px-3 py-2 flex flex-col gap-0.5"
              >
                <span class="text-[0.7rem] font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.inventory.sourcingCosts') }}</span
                ><strong class="text-sm text-foreground">{{ getUnitInventoryCostLabel(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y)) }}</strong>
              </div>
            </div>
            <div v-if="getUnitInventories(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y)).length > 0" class="inventory-table mt-3 border border-divider rounded-md overflow-hidden">
              <div class="inventory-table-header grid grid-cols-[minmax(0,1.4fr)_90px_90px_minmax(110px,0.9fr)] gap-2 px-3 py-2 bg-surface border-b border-divider">
                <span class="text-[0.75rem] font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.inventory.item') }}</span
                ><span class="text-[0.75rem] font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.inventory.amount') }}</span
                ><span class="text-[0.75rem] font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.inventory.quality') }}</span
                ><span class="text-[0.75rem] font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.inventory.sourcingCost') }}</span>
              </div>
              <div
                v-for="inventory in getUnitInventories(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y))"
                :key="inventory.id"
                class="inventory-table-row grid grid-cols-[minmax(0,1.4fr)_90px_90px_minmax(110px,0.9fr)] gap-2 px-3 py-2 border-b border-divider last:border-b-0 items-center"
              >
                <div class="flex items-center gap-3 min-w-0">
                  <img
                    v-if="getInventoryItemImageUrl(inventory)"
                    class="w-8 h-8 rounded object-cover flex-shrink-0"
                    :src="getInventoryItemImageUrl(inventory)!"
                    :alt="getInventoryItemName(inventory)"
                  /><span v-else class="inline-flex items-center justify-center w-8 h-8 text-sm font-bold rounded-md bg-primary text-white flex-shrink-0">{{
                    getInventoryItemMonogram(inventory)
                  }}</span>
                  <div class="flex flex-col gap-0.5 min-w-0">
                    <span class="font-semibold text-foreground truncate">{{ getInventoryItemName(inventory) }}</span>
                  </div>
                </div>
                <div class="text-[0.8125rem] text-muted">
                  <span>{{ formatUnitQuantity(inventory.quantity) }}</span>
                </div>
                <div class="text-[0.8125rem] text-muted">
                  <span>{{ formatPercent(inventory.quality) }}</span>
                </div>
                <div class="flex flex-col items-end gap-0.5">
                  <span class="font-bold text-foreground">{{ getInventoryItemSourcingCostLabel(inventory) }}</span
                  ><span v-if="getInventoryItemSourcingCostPerUnitLabel(inventory)" class="text-[0.75rem] text-muted"> {{ getInventoryItemSourcingCostPerUnitLabel(inventory) }} </span>
                </div>
              </div>
            </div>
            <p v-else class="mt-3 rounded-md bg-surface border border-divider px-3 py-2 text-xs text-muted">{{ t('buildingDetail.inventory.empty') }}</p>
            <div class="detail-capacity">
              <span class="detail-capacity-fill" :style="{ width: `${selectedActiveUnitFlowSegments.fillWidth}%` }"></span
              ><span
                v-if="selectedActiveUnitFlowSegments.inflowWidth > 0"
                class="detail-capacity-inflow"
                :style="{ left: `${selectedActiveUnitFlowSegments.inflowLeft}%`, width: `${selectedActiveUnitFlowSegments.inflowWidth}%` }"
              ></span
              ><span
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
                    {{ t('buildingDetail.flushStorage.confirmYes') }}</button
                  ><button class="btn btn-ghost btn-sm" @click="showFlushConfirmDialog = false">{{ t('common.cancel') }}</button>
                </div>
              </div>
            </div>
          </div>
          <p v-if="!getUnitInventorySummary(getUnitAtFrom(activeUnits, selectedCell!.x, selectedCell!.y))" class="unit-desc">{{ t('buildingDetail.inventory.empty') }}</p></template
        ><!-- ── Movement History tab ─────────────────────────────── --><template v-else-if="selectedUnitTab === 'history'"
          ><UnitResourceHistoryPanel
            v-if="selectedHistoryItemOptions.length > 0"
            :items="selectedHistoryItemOptions"
            :selected-item-key="selectedHistoryItemKey"
            :history="selectedUnitResourceHistory"
            @update:selected-item-key="selectedHistoryItemKey = $event"
          />
          <p v-else class="unit-desc">{{ t('buildingDetail.unitTabs.noHistory') }}</p></template
        ><!-- ── Market Intelligence tab ──────────────────────────── --><template v-else-if="selectedUnitTab === 'marketIntelligence'"
          ><div
            v-if="
              selectedPurchaseUnit && 'resourceTypeId' in selectedPurchaseUnit && selectedPurchaseUnit.resourceTypeId && ['EXCHANGE', 'OPTIMAL'].includes(selectedPurchaseUnit.purchaseSource ?? '')
            "
            class="unit-insight-card"
          >
            <h5>{{ t('buildingDetail.exchange.title') }}</h5>
            <p class="config-help">{{ t('buildingDetail.exchange.subtitle') }}</p>
            <p class="config-help exchange-selection-hint">{{ t('buildingDetail.exchange.selectionHint') }}</p>
            <p class="config-help" v-if="exchangeOffersLoading">{{ t('common.loading') }}</p>
            <template v-else
              ><p v-if="allExchangeOffersBlocked" class="config-help exchange-no-valid-offers">{{ t('buildingDetail.exchange.noValidOffers') }}</p>
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
                <span class="exchange-sort-label">{{ t('buildingDetail.exchange.sortBy') }}</span
                ><button
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
                    <strong>{{ offer.cityName }}</strong
                    ><span class="offer-best-badge" v-if="offer.cityId === bestExchangeOfferCityId">{{ t('buildingDetail.exchange.bestOffer') }}</span
                    ><span>{{ t('buildingDetail.exchange.quality', { quality: formatPercent(offer.estimatedQuality) }) }}</span>
                  </div>
                  <div class="exchange-offer-metrics">
                    <span>{{ t('buildingDetail.exchange.exchangePrice', { price: formatCurrency(offer.exchangePricePerUnit), unit: offer.unitSymbol }) }}</span
                    ><span>{{ t('buildingDetail.exchange.transit', { price: formatCurrency(offer.transitCostPerUnit), distance: offer.distanceKm }) }}</span
                    ><span>{{ t('buildingDetail.exchange.deliveredPrice', { price: formatCurrency(offer.deliveredPricePerUnit), unit: offer.unitSymbol }) }}</span>
                  </div>
                  <p v-if="offer.blockedReason === 'maxPrice'" class="offer-blocked-reason">
                    {{ t('buildingDetail.exchange.blockedMaxPrice', { maxPrice: formatCurrency(selectedPurchaseUnit?.maxPrice ?? 0), unit: offer.unitSymbol }) }}
                  </p>
                  <p v-else-if="offer.blockedReason === 'minQuality'" class="offer-blocked-reason">
                    {{ t('buildingDetail.exchange.blockedMinQuality', { minQuality: formatPercent(selectedPurchaseUnit?.minQuality ?? 0) }) }}
                  </p>
                </li>
              </ul>
              <!-- Link to Global Exchange --><RouterLink
                v-if="selectedPurchaseResourceSlug"
                :to="{ name: 'exchange', query: { resource: selectedPurchaseResourceSlug, city: building?.cityId } }"
                class="exchange-view-link"
              >
                {{ t('buildingDetail.exchange.viewOnExchange') }}
              </RouterLink></template
            >
          </div>
          <!-- Procurement Preview Card (shown in view mode for PURCHASE units) -->
          <div v-if="selectedPurchaseUnit" class="procurement-preview unit-insight-card">
            <h5 class="procurement-preview-title">{{ t('buildingDetail.procurementPreview.title') }}</h5>
            <div v-if="procurementPreviewLoading" class="procurement-preview-loading">{{ t('common.loading') }}...</div>
            <div v-else-if="procurementPreview" class="procurement-preview-content">
              <div v-if="procurementPreview.canExecute" class="procurement-preview-ok">
                <span class="preview-status ok">✅ {{ t('buildingDetail.procurementPreview.willExecute') }}</span>
                <div class="preview-details">
                  <div class="preview-row" v-if="procurementPreview.sourceCityName">
                    <span class="preview-label">{{ t('buildingDetail.procurementPreview.source') }}</span
                    ><span class="preview-value">{{ procurementPreview.sourceCityName }} ({{ t(`buildingDetail.procurementPreview.sourceType_${procurementPreview.sourceType}`) }})</span>
                  </div>
                  <div class="preview-row" v-if="procurementPreview.sourceVendorName">
                    <span class="preview-label">{{ t('buildingDetail.procurementPreview.vendor') }}</span
                    ><span class="preview-value">{{ procurementPreview.sourceVendorName }}</span>
                  </div>
                  <div class="preview-row" v-if="procurementPreview.exchangePricePerUnit !== null">
                    <span class="preview-label">{{ t('buildingDetail.procurementPreview.exchangePrice') }}</span
                    ><span class="preview-value">{{ formatCurrency(procurementPreview.exchangePricePerUnit) }}</span>
                  </div>
                  <div class="preview-row" v-if="procurementPreview.transitCostPerUnit !== null">
                    <span class="preview-label">{{ t('buildingDetail.procurementPreview.transitCost') }}</span
                    ><span class="preview-value">{{ formatCurrency(procurementPreview.transitCostPerUnit) }}</span>
                  </div>
                  <div class="preview-row" v-if="procurementPreview.deliveredPricePerUnit !== null">
                    <span class="preview-label">{{ t('buildingDetail.procurementPreview.deliveredPrice') }}</span
                    ><span class="preview-value preview-delivered">{{ formatCurrency(procurementPreview.deliveredPricePerUnit) }}</span>
                  </div>
                  <div class="preview-row" v-if="procurementPreview.estimatedQuality !== null">
                    <span class="preview-label">{{ t('buildingDetail.procurementPreview.quality') }}</span
                    ><span class="preview-value">{{ formatPercent(procurementPreview.estimatedQuality ?? 0) }}</span>
                  </div>
                </div>
              </div>
              <div v-else class="procurement-preview-blocked">
                <span class="preview-status blocked">⚠️ {{ t('buildingDetail.procurementPreview.blocked') }}</span>
                <div class="preview-block-details">
                  <span class="preview-block-reason">{{ t(`buildingDetail.procurementPreview.blockReason_${procurementPreview.blockReason ?? 'UNKNOWN'}`) }}</span>
                  <p class="preview-block-message" v-if="procurementPreview.blockMessage">{{ procurementPreview.blockMessage }}</p>
                </div>
                <div class="preview-details" v-if="procurementPreview.deliveredPricePerUnit !== null">
                  <div class="preview-row">
                    <span class="preview-label">{{ t('buildingDetail.procurementPreview.nearestOffer') }}</span
                    ><span class="preview-value preview-blocked-price">{{ formatCurrency(procurementPreview.deliveredPricePerUnit) }}</span>
                  </div>
                  <div class="preview-row" v-if="procurementPreview.sourceCityName">
                    <span class="preview-label">{{ t('buildingDetail.procurementPreview.source') }}</span
                    ><span class="preview-value">{{ procurementPreview.sourceCityName }}</span>
                  </div>
                </div>
              </div>
            </div>
            <div v-else class="procurement-preview-empty">{{ t('buildingDetail.procurementPreview.notAvailable') }}</div>
          </div>
          <!-- Sourcing Comparison Panel (shown in view mode for PURCHASE units with a resource configured) -->
          <div
            v-if="selectedPurchaseUnit && (selectedPurchaseUnit.resourceTypeId || selectedPurchaseUnit.productTypeId)"
            class="sourcing-comparison unit-insight-card"
            :aria-label="t('buildingDetail.accessibility.sourcingComparison')"
          >
            <h5 class="sourcing-comparison-title">{{ t('buildingDetail.sourcingComparison.title') }}</h5>
            <p class="sourcing-comparison-subtitle config-help">{{ t('buildingDetail.sourcingComparison.subtitle') }}</p>
            <div v-if="sourcingCandidatesLoading" class="sourcing-comparison-loading">{{ t('buildingDetail.sourcingComparison.loading') }}</div>
            <template v-else-if="sourcingCandidates.length > 0"
              ><!-- Logistics note: cheapest sticker ├ö├ź├í best landed -->
              <p v-if="sourcingCheapestStickerDiffersFromBestLanded" class="sourcing-trap-note">⚠️ {{ t('buildingDetail.sourcingComparison.cheapestNotBest') }}</p>
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
                        <span class="source-type-badge">{{ t(`buildingDetail.sourcingComparison.sourceType_${candidate.sourceType}`) }}</span
                        ><span class="source-name"> {{ candidate.sourceCityName ?? candidate.sourceVendorName ?? '—' }} </span
                        ><span v-if="candidate.distanceKm && candidate.distanceKm > 0" class="source-distance">
                          {{ t('buildingDetail.sourcingComparison.distanceKm', { km: Math.round(candidate.distanceKm) }) }}
                        </span>
                      </td>
                      <td class="sourcing-col-offer">
                        <span v-if="candidate.exchangePricePerUnit !== null"> {{ formatCurrency(candidate.exchangePricePerUnit) }} </span
                        ><span v-else-if="candidate.deliveredPricePerUnit !== null"> {{ formatCurrency(candidate.deliveredPricePerUnit) }} </span><span v-else>—</span>
                      </td>
                      <td class="sourcing-col-transit">
                        <span v-if="candidate.transitCostPerUnit !== null" class="transit-cost"> +{{ formatCurrency(candidate.transitCostPerUnit) }} </span><span v-else>—</span>
                      </td>
                      <td class="sourcing-col-landed col-landed">
                        <strong v-if="candidate.deliveredPricePerUnit !== null"> {{ formatCurrency(candidate.deliveredPricePerUnit) }} </strong><span v-else>—</span>
                      </td>
                      <td class="sourcing-col-quality">
                        <span v-if="candidate.estimatedQuality !== null">{{ formatPercent(candidate.estimatedQuality) }}</span
                        ><span v-else>—</span>
                      </td>
                      <td class="sourcing-col-status">
                        <span v-if="candidate.isRecommended" class="sc-badge sc-badge--recommended"> ★ {{ t('buildingDetail.sourcingComparison.recommended') }} </span
                        ><span v-else-if="candidate.isEligible" class="sc-badge sc-badge--eligible"> {{ t('buildingDetail.sourcingComparison.eligible') }} </span
                        ><span v-else class="sc-badge sc-badge--blocked" :title="candidate.blockMessage ?? ''">
                          {{ t(`buildingDetail.sourcingComparison.blockReason_${candidate.blockReason ?? 'UNKNOWN'}`) }}
                        </span>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
              <!-- Filter hint when some candidates are blocked -->
              <p v-if="sourcingCandidates.some((c) => !c.isEligible)" class="sourcing-filter-hint config-help">{{ t('buildingDetail.sourcingComparison.filterHint') }}</p></template
            >
            <div v-else class="sourcing-comparison-empty">{{ t('buildingDetail.sourcingComparison.empty') }}</div>
          </div>
          <div v-if="selectedPublicSalesUnit" class="unit-insight-card market-intelligence-panel" :aria-label="t('buildingDetail.accessibility.marketIntelligence')">
            <h5>{{ t('buildingDetail.marketIntelligence.title') }}</h5>
            <!-- Product identity + data window row -->
            <div class="mi-context-row">
              <span v-if="publicSalesAnalytics?.productName" class="mi-product-chip" :aria-label="t('buildingDetail.accessibility.currentlySellingProduct')">
                {{ publicSalesAnalytics.productName }} </span
              ><span v-if="publicSalesAnalytics && publicSalesAnalytics.dataFromTick > 0" class="mi-tick-window" :title="`T${publicSalesAnalytics.dataFromTick}–T${publicSalesAnalytics.dataToTick}`">
                {{ formatGameTickTime(publicSalesAnalytics.dataFromTick, locale) }} – {{ formatGameTickTime(publicSalesAnalytics.dataToTick, locale) }}
              </span>
            </div>
            <p v-if="publicSalesAnalyticsLoading" class="config-help">{{ t('buildingDetail.marketIntelligence.loading') }}</p>
            <template v-else-if="publicSalesAnalytics">
              <div v-if="publicSalesMarketEvents.length > 0" class="mb-3 rounded-lg border border-amber-400/30 bg-amber-500/10 px-3 py-2 text-xs">
                <strong class="block uppercase tracking-wide text-amber-300">{{ publicSalesMarketEvents[0]?.title }}</strong>
                <p class="m-0 mt-1 text-amber-200">{{ publicSalesMarketEvents[0]?.description }}</p>
              </div>
              <!-- Summary metrics -->
              <div class="mi-summary-grid grid grid-cols-[repeat(auto-fit,minmax(120px,1fr))] gap-3 mb-4">
                <div class="mi-metric rounded-lg border border-divider bg-card px-3 py-2 flex flex-col gap-0.5">
                  <span class="text-[0.7rem] font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.marketIntelligence.totalRevenue') }}</span
                  ><strong class="mi-metric-value text-sm text-foreground">{{ formatCurrency(publicSalesAnalytics.totalRevenue) }}</strong>
                </div>
                <div v-if="publicSalesAnalytics.totalProfit !== null" class="mi-metric rounded-lg border border-divider bg-card px-3 py-2 flex flex-col gap-0.5">
                  <span class="text-[0.7rem] font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.marketIntelligence.totalProfit') }}</span
                  ><strong
                    class="mi-metric-value text-sm"
                    :class="{
                      'building-profit-positive-text text-emerald-500': publicSalesAnalytics.totalProfit >= 0,
                      'building-profit-negative-text text-red-500': publicSalesAnalytics.totalProfit < 0,
                    }"
                    >{{ formatCurrency(publicSalesAnalytics.totalProfit) }}</strong
                  >
                </div>
                <div class="mi-metric rounded-lg border border-divider bg-card px-3 py-2 flex flex-col gap-0.5">
                  <span class="text-[0.7rem] font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.marketIntelligence.totalSold') }}</span
                  ><strong class="mi-metric-value text-sm text-foreground">{{ formatUnitQuantity(publicSalesAnalytics.totalQuantitySold) }}</strong>
                </div>
                <div v-if="publicSalesAnalytics.averagePricePerUnit > 0" class="mi-metric rounded-lg border border-divider bg-card px-3 py-2 flex flex-col gap-0.5">
                  <span class="text-[0.7rem] font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.marketIntelligence.avgPrice') }}</span
                  ><strong class="mi-metric-value text-sm text-foreground">{{ formatCurrency(publicSalesAnalytics.averagePricePerUnit) }}</strong>
                </div>
                <div v-if="selectedPublicSalesUnit.minPrice != null" class="mi-metric rounded-lg border border-divider bg-card px-3 py-2 flex flex-col gap-0.5">
                  <span class="text-[0.7rem] font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.marketIntelligence.configuredPrice') }}</span
                  ><strong class="mi-metric-value text-sm text-foreground">{{ formatCurrency(currentPublicSalesMinPrice) }}</strong>
                </div>
                <div v-if="publicSalesAnalytics.revenueHistory.length > 0" class="mi-metric rounded-lg border border-divider bg-card px-3 py-2 flex flex-col gap-0.5">
                  <span class="text-[0.7rem] font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.marketIntelligence.recentUtilization') }}</span
                  ><strong class="mi-metric-value text-sm text-foreground">{{ Math.round(publicSalesAnalytics.recentUtilization * 100) }}%</strong>
                </div>
                <!-- Trend direction (only shown when there are at least 2 ticks of history) -->
                <div
                  v-if="publicSalesAnalytics.trendDirection && publicSalesAnalytics.trendDirection !== 'NO_DATA'"
                  class="mi-metric rounded-lg border border-divider bg-card px-3 py-2 flex flex-col gap-0.5"
                >
                  <span class="text-[0.7rem] font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.marketIntelligence.trend') }}</span
                  ><strong
                    class="mi-metric-value text-sm"
                    :class="{
                      'mi-trend-up text-emerald-500': publicSalesAnalytics.trendDirection === 'UP',
                      'mi-trend-down text-red-500': publicSalesAnalytics.trendDirection === 'DOWN',
                      'mi-trend-flat text-neutral-500': publicSalesAnalytics.trendDirection === 'FLAT',
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
                <div v-if="publicSalesAnalytics.trendFactor !== null" class="mi-metric rounded-lg border border-divider bg-card px-3 py-2 flex flex-col gap-0.5">
                  <span class="text-[0.7rem] font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.marketIntelligence.trendFactor') }}</span
                  ><strong
                    class="mi-metric-value text-sm"
                    :class="{
                      'mi-trend-up text-emerald-500': publicSalesAnalytics.trendFactor > 1.05,
                      'mi-trend-down text-red-500': publicSalesAnalytics.trendFactor < 0.95,
                      'mi-trend-flat text-neutral-500': publicSalesAnalytics.trendFactor >= 0.95 && publicSalesAnalytics.trendFactor <= 1.05,
                    }"
                  >
                    {{ publicSalesAnalytics.trendFactor > 1 ? '+' : '' }}{{ ((publicSalesAnalytics.trendFactor - 1) * 100).toFixed(0) }}%
                  </strong>
                </div>
              </div>
              <!-- No-history empty state -->
              <p v-if="publicSalesAnalytics.revenueHistory.length === 0" class="mi-empty-state">{{ t('buildingDetail.marketIntelligence.noHistory') }}</p>
              <template v-else
                ><!-- Revenue mini chart -->
                <div class="mt-4">
                  <span class="text-xs font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.marketIntelligence.revenueChart') }}</span>
                  <div
                    class="mi-bar-chart flex items-end justify-center gap-0.5 mt-2 h-16 p-1 rounded-md border border-divider bg-surface"
                    role="img"
                    :aria-label="t('buildingDetail.marketIntelligence.revenueChart')"
                  >
                    <div
                      v-for="snap in publicSalesAnalytics.revenueHistory"
                      :key="snap.tick"
                      class="mi-bar-revenue flex-1 bg-blue-500 rounded-sm transition-all duration-300"
                      :style="{ height: `${Math.max(4, miMaxRevenue > 0 ? (snap.revenue / miMaxRevenue) * 100 : 0).toFixed(1)}%` }"
                      :title="`T${snap.tick}: ${formatCurrency(snap.revenue)}`"
                    ></div>
                  </div>
                </div>
                <!-- Quantity mini chart -->
                <div class="mt-4">
                  <span class="text-xs font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.marketIntelligence.quantityChart') }}</span>
                  <div
                    class="mi-bar-chart flex items-end justify-center gap-0.5 mt-2 h-16 p-1 rounded-md border border-divider bg-surface"
                    role="img"
                    :aria-label="t('buildingDetail.marketIntelligence.quantityChart')"
                  >
                    <div
                      v-for="snap in publicSalesAnalytics.revenueHistory"
                      :key="snap.tick"
                      class="mi-bar-quantity flex-1 bg-amber-500 rounded-sm transition-all duration-300"
                      :style="{ height: `${Math.max(4, miMaxQuantitySold > 0 ? (snap.quantitySold / miMaxQuantitySold) * 100 : 0).toFixed(1)}%` }"
                      :title="`T${snap.tick}: ${formatUnitQuantity(snap.quantitySold)}`"
                    ></div>
                  </div>
                </div>
                <!-- Profit history chart -->
                <div v-if="publicSalesAnalytics.profitHistory && publicSalesAnalytics.profitHistory.length > 0" class="mt-4">
                  <span class="text-xs font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.marketIntelligence.profitChart') }}</span>
                  <div
                    class="mi-bar-chart flex items-end justify-center gap-0.5 mt-2 h-16 p-1 rounded-md border border-divider bg-surface"
                    role="img"
                    :aria-label="t('buildingDetail.marketIntelligence.profitChart')"
                  >
                    <div
                      v-for="snap in publicSalesAnalytics.profitHistory"
                      :key="snap.tick"
                      :class="snap.profit >= 0 ? 'mi-bar-profit-positive bg-emerald-500' : 'mi-bar-profit-negative bg-red-500'"
                      class="flex-1 rounded-sm transition-all duration-300"
                      :style="{ height: `${Math.max(4, miMaxAbsProfit > 0 ? (Math.abs(snap.profit) / miMaxAbsProfit) * 100 : 0).toFixed(1)}%` }"
                      :title="`T${snap.tick}: ${formatCurrency(snap.profit)}${snap.grossMarginPct !== null ? ` (${snap.grossMarginPct.toFixed(1)}% margin)` : ''}`"
                    ></div>
                  </div></div></template
              ><!-- Competition section -->
              <div class="mt-4 mi-competition-section">
                <span class="text-xs font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.marketIntelligence.competition.title') }}</span>
                <p v-if="publicSalesAnalytics.marketShare.length === 0" class="text-xs text-muted mt-2">{{ t('buildingDetail.marketIntelligence.noMarketShare') }}</p>
                <div v-else class="mt-2 grid gap-3 md:grid-cols-[140px_minmax(0,1fr)] md:items-start">
                  <div
                    class="mi-competition-pie mx-auto h-28 w-28 rounded-full border border-divider"
                    role="img"
                    :aria-label="t('buildingDetail.marketIntelligence.competition.pieAria')"
                    :style="{ background: buildCompetitionPieGradient(buildCompetitionLegend(publicSalesAnalytics.marketShare, building?.companyId)) }"
                  ></div>
                  <div class="flex flex-col gap-2">
                    <div
                      v-for="entry in buildCompetitionLegend(publicSalesAnalytics.marketShare, building?.companyId)"
                      :key="`${entry.label}-${entry.isUnmet ? 'unmet' : 'seller'}`"
                      class="mi-share-row flex items-center gap-2"
                      :class="{ 'opacity-70 mi-share-row-unmet': entry.isUnmet, 'mi-share-row-you': entry.isSelf }"
                    >
                      <span class="h-2.5 w-2.5 shrink-0 rounded-full" :style="{ backgroundColor: entry.color }"></span>
                      <span class="mi-share-label text-[0.7rem] font-semibold flex-shrink-0 w-32 truncate">{{ entry.label }}{{ entry.isSelf ? ' ★' : '' }}</span>
                      <div class="flex-1 h-2 rounded-full bg-surface border border-divider overflow-hidden">
                        <div class="h-full transition-all duration-300" :style="{ width: `${(entry.share * 100).toFixed(1)}%`, backgroundColor: entry.color }"></div>
                      </div>
                      <span class="mi-share-pct text-[0.7rem] text-muted flex-shrink-0 w-10 text-right">{{ (entry.share * 100).toFixed(1) }}%</span>
                    </div>
                  </div>
                </div>
                <div v-if="publicSalesAnalytics.priceHistory.length > 0" class="mt-3">
                  <span class="text-[0.68rem] font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.marketIntelligence.competition.priceTrend') }}</span>
                  <div
                    class="mi-competition-price-chart mt-2 flex items-end justify-center gap-[1px] h-14 p-1 rounded-md border border-divider bg-surface"
                    role="img"
                    :aria-label="t('buildingDetail.marketIntelligence.priceChart')"
                  >
                    <div
                      v-for="snap in publicSalesAnalytics.priceHistory"
                      :key="snap.tick"
                      class="mi-competition-price-bar flex-1 rounded-sm bg-violet-500/80"
                      :style="{ height: `${Math.max(3, miMaxPricePerUnit > 0 ? (snap.pricePerUnit / miMaxPricePerUnit) * 100 : 0).toFixed(1)}%` }"
                      :title="`T${snap.tick}: ${formatCurrency(snap.pricePerUnit)}`"
                    ></div>
                  </div>
                </div>
              </div>
              <!-- Demand Drivers -->
              <div v-if="publicSalesAnalytics.demandDrivers.length > 0" class="mt-4" :aria-label="t('buildingDetail.accessibility.demandDrivers')">
                <span class="text-xs font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.marketIntelligence.demandDrivers.title') }}</span>
                <div class="flex flex-col gap-2 mt-2">
                  <div
                    v-for="driver in publicSalesAnalytics.demandDrivers"
                    :key="driver.factor"
                    class="flex gap-2 px-2 py-1.5 rounded-md border border-divider text-xs"
                    :class="
                      driver.impact === 'POSITIVE'
                        ? 'mi-driver-positive border-emerald-500/30 bg-emerald-500/10'
                        : driver.impact === 'NEGATIVE'
                          ? 'mi-driver-negative border-red-500/30 bg-red-500/10'
                          : 'mi-driver-neutral border-neutral-500/30 bg-neutral-500/10'
                    "
                  >
                    <span
                      class="font-bold flex-shrink-0 w-4 text-center"
                      :class="driver.impact === 'POSITIVE' ? 'text-emerald-500' : driver.impact === 'NEGATIVE' ? 'text-red-500' : 'text-neutral-500'"
                    >
                      {{ driver.impact === 'POSITIVE' ? '↑' : driver.impact === 'NEGATIVE' ? '↓' : '→' }}
                    </span>
                    <div class="flex flex-col gap-0.5 flex-1 min-w-0">
                      <strong class="mi-driver-factor text-[0.7rem] font-semibold">{{ t(`buildingDetail.marketIntelligence.demandDrivers.factor_${driver.factor}`) }}</strong
                      ><span class="text-[0.65rem] text-muted">{{ driver.description }}</span>
                    </div>
                  </div>
                </div>
              </div>
              <!-- Elasticity index + context card -->
              <div class="mi-context-card mt-4 rounded-lg border border-divider bg-card p-3">
                <div class="grid grid-cols-[repeat(auto-fit,minmax(140px,1fr))] gap-3">
                  <div v-if="publicSalesAnalytics.elasticityIndex !== null" class="flex flex-col gap-0.5">
                    <span class="mi-context-label text-[0.7rem] font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.marketIntelligence.elasticityIndex') }}</span
                    ><strong
                      class="mi-context-value text-sm"
                      :class="{ 'text-red-500': (publicSalesAnalytics.elasticityIndex ?? 0) < -1.5, 'text-emerald-500': (publicSalesAnalytics.elasticityIndex ?? 0) > -0.5 }"
                    >
                      {{ publicSalesAnalytics.elasticityIndex.toFixed(2) }} </strong
                    ><span class="text-[0.65rem] text-muted">{{ t('buildingDetail.marketIntelligence.elasticityHint') }}</span>
                  </div>
                  <div v-if="publicSalesAnalytics.populationIndex !== null" class="flex flex-col gap-0.5">
                    <span class="mi-context-label text-[0.7rem] font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.marketIntelligence.populationIndex') }}</span
                    ><strong class="mi-context-value text-sm text-foreground">{{ publicSalesAnalytics.populationIndex.toFixed(2) }}×</strong
                    ><span class="text-[0.65rem] text-muted">{{ t('buildingDetail.marketIntelligence.populationIndexHint') }}</span>
                  </div>
                  <div v-if="publicSalesAnalytics.inventoryQuality !== null" class="flex flex-col gap-0.5">
                    <span class="mi-context-label text-[0.7rem] font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.marketIntelligence.productQuality') }}</span
                    ><strong
                      class="mi-context-value text-sm"
                      :class="{ 'text-emerald-500': publicSalesAnalytics.inventoryQuality >= 0.7, 'text-red-500': publicSalesAnalytics.inventoryQuality < 0.4 }"
                    >
                      {{ Math.round(publicSalesAnalytics.inventoryQuality * 100) }}% </strong
                    ><span class="text-[0.65rem] text-muted">{{ t('buildingDetail.marketIntelligence.productQualityHint') }}</span>
                  </div>
                  <div v-if="publicSalesAnalytics.brandAwareness !== null" class="flex flex-col gap-0.5">
                    <span class="mi-context-label text-[0.7rem] font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.marketIntelligence.brandAwareness') }}</span
                    ><strong class="mi-context-value text-sm" :class="{ 'text-emerald-500': publicSalesAnalytics.brandAwareness >= 0.6 }">
                      {{ Math.round(publicSalesAnalytics.brandAwareness * 100) }}% </strong
                    ><span class="text-[0.65rem] text-muted">{{ t('buildingDetail.marketIntelligence.brandAwarenessHint') }}</span>
                  </div>
                  <div v-if="publicSalesAnalytics.brandQuality !== null" class="flex flex-col gap-0.5">
                    <span class="mi-context-label text-[0.7rem] font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.marketIntelligence.brandQuality') }}</span
                    ><strong class="mi-context-value text-sm" :class="{ 'text-emerald-500': publicSalesAnalytics.brandQuality >= 0.5, 'text-red-500': publicSalesAnalytics.brandQuality < 0.2 }">
                      {{ Math.round(publicSalesAnalytics.brandQuality * 100) }}%
                      <span v-if="publicSalesAnalytics.brandQuality >= 0.5" class="text-[0.6rem] ml-1 px-1.5 py-0.5 rounded bg-emerald-500/20 text-emerald-500 font-semibold">{{
                        t('buildingDetail.marketIntelligence.brandQualityPremium')
                      }}</span
                      ><span v-else-if="publicSalesAnalytics.brandQuality >= 0.2" class="text-[0.6rem] ml-1 px-1.5 py-0.5 rounded bg-amber-500/20 text-amber-500 font-semibold">{{
                        t('buildingDetail.marketIntelligence.brandQualityGrowing')
                      }}</span></strong
                    ><span class="text-[0.65rem] text-muted">{{ t('buildingDetail.marketIntelligence.brandQualityHint') }}</span>
                  </div>
                </div>
              </div>
              <!-- Demand signal -->
              <div
                class="mi-demand-card mt-4 rounded-lg border border-divider px-3 py-2"
                :class="[
                  `mi-demand-${publicSalesAnalytics.demandSignal.toLowerCase().replace(/_/g, '-')}`,
                  publicSalesAnalytics.demandSignal === 'STRONG'
                    ? 'bg-emerald-500/10 border-emerald-500/30'
                    : publicSalesAnalytics.demandSignal === 'WEAK'
                      ? 'bg-red-500/10 border-red-500/30'
                      : 'bg-neutral-500/10 border-neutral-500/30',
                ]"
              >
                <div class="flex items-center gap-2 mb-1">
                  <span class="text-xs font-bold uppercase tracking-wide text-muted">{{ t('buildingDetail.marketIntelligence.demandSignal.title') }}</span
                  ><span
                    class="mi-demand-badge text-[0.65rem] font-bold px-2 py-0.5 rounded"
                    :class="
                      publicSalesAnalytics.demandSignal === 'STRONG'
                        ? 'bg-emerald-500/30 text-emerald-500'
                        : publicSalesAnalytics.demandSignal === 'WEAK'
                          ? 'bg-red-500/30 text-red-500'
                          : 'bg-neutral-500/30 text-neutral-500'
                    "
                    >{{ t(`buildingDetail.marketIntelligence.demandSignal.${publicSalesAnalytics.demandSignal}`) }}</span
                  >
                </div>
                <p v-if="publicSalesAnalytics.actionHint" class="mi-action-hint text-[0.75rem] text-muted">
                  <strong>{{ t('buildingDetail.marketIntelligence.actionHint') }}:</strong> {{ publicSalesAnalytics.actionHint }}
                </p>
              </div>
              <!-- Seasonal Outlook Panel -->
              <SeasonalOutlookPanel v-if="publicSalesAnalytics.seasonalOutlook" :seasonal-outlook="publicSalesAnalytics.seasonalOutlook"
            /></template>
            <p v-else class="config-help">{{ t('buildingDetail.marketIntelligence.loadFailed') }}</p>
          </div>
          <!-- Manufacturing Unit Product Analytics Panel -->
          <div
            v-if="selectedManufacturingUnit && (selectedManufacturingUnit.productTypeId || unitProductAnalytics)"
            class="unit-insight-card unit-product-analytics-panel"
            :aria-label="t('buildingDetail.accessibility.productPerformanceAnalytics')"
          >
            <h5>{{ t('buildingDetail.unitProductAnalytics.title') }}</h5>
            <!-- Product identity + data window row -->
            <div class="mi-context-row">
              <span v-if="unitProductAnalytics?.productName" class="mi-product-chip" :aria-label="t('buildingDetail.accessibility.currentlyProducingProduct')">
                {{ unitProductAnalytics.productName }} </span
              ><span v-else-if="selectedManufacturingUnit.productTypeId" class="mi-product-chip"> {{ t('buildingDetail.unitProductAnalytics.productConfigured') }} </span
              ><span v-if="unitProductAnalytics && unitProductAnalytics.dataFromTick > 0" class="mi-tick-window" :title="`T${unitProductAnalytics.dataFromTick}–T${unitProductAnalytics.dataToTick}`">
                {{ formatGameTickTime(unitProductAnalytics.dataFromTick, locale) }} – {{ formatGameTickTime(unitProductAnalytics.dataToTick, locale) }}
              </span>
            </div>
            <p v-if="unitProductAnalyticsLoading" class="config-help">{{ t('buildingDetail.unitProductAnalytics.loading') }}</p>
            <template v-else-if="unitProductAnalytics && unitProductAnalytics.snapshots.length > 0"
              ><!-- Summary metrics -->
              <div class="mi-summary-grid">
                <div class="mi-metric">
                  <span class="mi-metric-label">{{ t('buildingDetail.unitProductAnalytics.totalProduced') }}</span
                  ><strong class="mi-metric-value">{{ formatUnitQuantity(unitProductAnalytics.totalQuantityProduced) }}</strong>
                </div>
                <div class="mi-metric">
                  <span class="mi-metric-label">{{ t('buildingDetail.unitProductAnalytics.totalCost') }}</span
                  ><strong class="mi-metric-value building-profit-negative-text">{{ formatCurrency(unitProductAnalytics.totalCost) }}</strong>
                </div>
                <div v-if="unitProductAnalytics.estimatedRevenue !== null" class="mi-metric">
                  <span class="mi-metric-label">{{ t('buildingDetail.unitProductAnalytics.estimatedRevenue') }}</span
                  ><strong class="mi-metric-value">{{ formatCurrency(unitProductAnalytics.estimatedRevenue) }}</strong>
                </div>
                <div v-if="unitProductAnalytics.estimatedProfit !== null" class="mi-metric">
                  <span class="mi-metric-label">{{ t('buildingDetail.unitProductAnalytics.estimatedProfit') }}</span
                  ><strong
                    class="mi-metric-value"
                    :class="{ 'building-profit-positive-text': unitProductAnalytics.estimatedProfit >= 0, 'building-profit-negative-text': unitProductAnalytics.estimatedProfit < 0 }"
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
                    :style="{ height: `${Math.max(2, upaMaxCost > 0 ? (snap.totalCost / upaMaxCost) * 100 : 0).toFixed(1)}%` }"
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
                    :style="{ height: `${Math.max(2, upaMaxEstRevenue > 0 ? ((snap.estimatedRevenue ?? 0) / upaMaxEstRevenue) * 100 : 0).toFixed(1)}%` }"
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
                    :style="{ height: `${Math.max(2, upaMaxAbsProfit > 0 ? (Math.abs(snap.estimatedProfit ?? 0) / upaMaxAbsProfit) * 100 : 0).toFixed(1)}%` }"
                    :title="`T${snap.tick}: ${formatCurrency(snap.estimatedProfit ?? 0)}`"
                  ></div>
                </div>
              </div>
              <!-- Profitability note -->
              <p class="config-help mi-hint">{{ t('buildingDetail.unitProductAnalytics.profitNote') }}</p></template
            ><template v-else-if="unitProductAnalytics && unitProductAnalytics.snapshots.length === 0"
              ><p class="config-help">{{ t('buildingDetail.unitProductAnalytics.noData') }}</p></template
            ><template v-else-if="!unitProductAnalytics && !unitProductAnalyticsLoading"
              ><p class="config-help">{{ t('buildingDetail.unitProductAnalytics.noProduct') }}</p></template
            >
          </div></template
        ><!-- ── Recent Activity tab ─────────────────────────────── --><template v-else-if="selectedUnitTab === 'recentActivity'"
          ><div class="unit-insight-card recent-activity-panel mt-0 border-0 pt-0" :aria-label="t('buildingDetail.accessibility.recentActivity')">
            <h5 class="mb-2">{{ t('buildingDetail.recentActivity.title') }}</h5>
            <p class="text-xs text-muted mb-3">{{ t('buildingDetail.recentActivity.subtitle') }}</p>
            <p v-if="recentActivityLoading" class="text-xs text-muted">...</p>
            <template v-else-if="recentActivity.length > 0"
              ><ul class="flex flex-col gap-2 list-none m-0 p-0">
                <li
                  v-for="(event, idx) in recentActivity"
                  :key="`${event.tick}-${event.buildingUnitId}-${event.eventType}-${idx}`"
                  class="flex gap-2 py-2 px-2 rounded-md border border-divider text-xs"
                >
                  <span class="font-semibold text-muted flex-shrink-0" :title="t('buildingDetail.recentActivity.tickLabel', { tick: event.tick })">{{ formatGameTickTime(event.tick, locale) }}</span
                  ><span class="text-foreground">{{ event.description }}</span>
                </li>
              </ul></template
            >
            <p v-else class="text-xs text-muted py-2 px-2 rounded-md bg-surface">{{ t('buildingDetail.recentActivity.empty') }}</p>
          </div></template
        >
      </div>
    </div>
  </div>
</template>

<style scoped src="./BuildingSidebar.shared.css"></style>
<style scoped src="./BuildingSidebar.analytics.css"></style>
<style scoped src="./BuildingSidebar.exchange.css"></style>

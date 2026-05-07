<template>
  <aside class="detail-panel">
    <div class="detail-header">
      <h2>{{ lot.name }}</h2>
      <span class="status-badge" :class="lotStatus">
        {{ lotStatus === 'available' ? t('cityMap.available') : lotStatus === 'yours' ? t('cityMap.yourProperty') : t('cityMap.owned') }}
      </span>
    </div>
    <p class="lot-description">{{ lot.description }}</p>

    <!-- Strategic recommendation badge -->
    <div class="strategic-recommendation" :class="recommendation.cssClass" data-testid="strategic-recommendation">
      <span class="rec-icon">🎯</span><span class="rec-label">{{ t(`cityMap.${recommendation.key}`) }}</span>
    </div>

    <div class="detail-grid">
      <div class="detail-item">
        <span class="detail-label">{{ t('cityMap.district') }}</span
        ><span class="detail-value">{{ lot.district }}</span>
      </div>
      <div class="detail-item">
        <span class="detail-label">{{ t('cityMap.appraisedValue') }}</span
        ><span class="detail-value" data-testid="appraised-value">{{ fmtCurrency(lot.basePrice) }}</span>
      </div>
      <div class="detail-item">
        <span class="detail-label">{{ t('cityMap.price') }}</span
        ><span class="detail-value price" data-testid="asking-price">
          {{ fmtCurrency(lot.price) }}
          <span v-if="lot.resourceType && lot.price > lot.basePrice" class="resource-premium-badge" :title="t('cityMap.resourcePremiumTooltip')">
            {{ t('cityMap.resourcePremium') }}
          </span></span
        >
      </div>
      <div class="detail-item full-width population-index-item">
        <span class="detail-label">{{ t('cityMap.populationIndex') }}</span>
        <div class="population-index-display">
          <span class="population-index-value">{{ formatPopulationIndex(lot.populationIndex) }}</span
          ><span class="population-index-tag" :class="populationIndexClass(lot.populationIndex)"> {{ populationIndexLabel(lot.populationIndex) }} </span>
        </div>
        <p class="population-index-hint">{{ t('cityMap.populationIndexHint') }}</p>
      </div>
      <div class="detail-item full-width">
        <span class="detail-label">{{ t('cityMap.suitableFor') }}</span>
        <div class="suitable-types">
          <span v-for="type in suitableTypes" :key="type" class="type-tag"> {{ fmtBuildingType(type) }} </span>
        </div>
      </div>
      <div class="detail-item full-width coordinates-item">
        <span class="detail-label">{{ t('cityMap.coordinates') }}</span
        ><span class="detail-value coordinates-value" data-testid="lot-coordinates">
          {{ Math.abs(lot.latitude).toFixed(5) }}°{{ lot.latitude >= 0 ? 'N' : 'S' }}, {{ Math.abs(lot.longitude).toFixed(5) }}°{{ lot.longitude >= 0 ? 'E' : 'W' }}
        </span>
        <p class="coordinates-hint">{{ t('cityMap.coordinatesHint') }}</p>
      </div>
    </div>

    <!-- Raw material deposit panel -->
    <div v-if="lot.resourceType && lot.materialQuality != null && lot.materialQuantity != null" class="raw-material-panel" data-testid="raw-material-panel">
      <h3 class="raw-material-title">⛏ {{ t('cityMap.rawMaterialTitle') }}</h3>
      <div class="raw-material-grid">
        <div class="raw-material-item">
          <span class="detail-label">{{ t('cityMap.rawMaterialResource') }}</span
          ><span class="detail-value">{{ lot.resourceType.name }}</span>
        </div>
        <div class="raw-material-item">
          <span class="detail-label">{{ t('cityMap.rawMaterialQuality') }}</span
          ><span class="quality-badge" :class="materialQualityClass(lot.materialQuality)"> {{ materialQualityLabel(lot.materialQuality) }} ({{ Math.round(lot.materialQuality * 100) }}%) </span>
        </div>
        <div class="raw-material-item full-width">
          <span class="detail-label">{{ t('cityMap.rawMaterialQuantity') }}</span
          ><span class="detail-value"> {{ lot.materialQuantity.toLocaleString(locale) }} {{ t('cityMap.rawMaterialQuantityUnit') }} </span>
        </div>
      </div>
      <p class="raw-material-hint">{{ t('cityMap.rawMaterialHint') }}</p>
    </div>

    <!-- Placement guidance panel -->
    <div class="placement-guidance-panel" data-testid="placement-guidance-panel">
      <h3 class="guidance-title">{{ t('cityMap.placementGuidanceTitle') }}</h3>
      <ul class="guidance-list">
        <li v-for="type in suitableTypes" :key="type" class="guidance-item">
          <span class="guidance-building-type">{{ fmtBuildingType(type) }}</span
          ><span class="guidance-text">{{ t(`cityMap.${placementGuidanceKey(type)}`) }}</span>
        </li>
      </ul>
      <p class="transport-cost-note"><span class="transport-icon">🚚</span> {{ t('cityMap.transportCostNote') }}</p>
    </div>

    <!-- Weather outlook: shown for POWER_PLANT lots -->
    <div v-if="suitableTypes.includes('POWER_PLANT') && cityWeather" class="weather-outlook-panel" data-testid="weather-outlook-panel">
      <h3 class="weather-outlook-title">🌤️ {{ t('powerPlant.weatherOutlook') }}</h3>
      <div class="weather-outlook-row">
        <span class="weather-badge solar-badge">☀️ {{ t('powerPlant.solarPercent', { percent: Math.round(cityWeather.currentSolarPercent) }) }}</span
        ><span class="weather-badge wind-badge">💨 {{ t('powerPlant.windPercent', { percent: Math.round(cityWeather.currentWindPercent) }) }}</span>
      </div>
      <div v-if="cityWeather.forecast.length > 0" class="weather-forecast-bars">
        <div
          v-for="(tick, i) in cityWeather.forecast.slice(0, 12)"
          :key="tick.tick"
          class="forecast-bar-group"
          :title="`Tick ${tick.tick}: ☀️${Math.round(tick.solarPercent)}% 💨${Math.round(tick.windPercent)}%`"
        >
          <div class="forecast-bar solar-bar" :style="{ height: Math.round(tick.solarPercent) + '%' }"></div>
          <div class="forecast-bar wind-bar" :style="{ height: Math.round(tick.windPercent) + '%' }"></div>
          <span v-if="i === 0 || i === 11" class="forecast-bar-label">{{ i === 0 ? 'Now' : '+12' }}</span>
        </div>
      </div>
    </div>

    <!-- Owner info -->
    <div v-if="lot.ownerCompany" class="owner-info">
      <span class="detail-label">{{ t('cityMap.owner') }}</span
      ><span class="detail-value">{{ lot.ownerCompany.name }}</span>
    </div>
    <div v-if="lot.building" class="building-info">
      <span class="detail-label">{{ t('cityMap.building') }}</span
      ><span class="detail-value"> {{ lot.building.name }} ({{ fmtBuildingType(lot.building.type) }}) </span>
    </div>
    <!-- For Sale badge on occupied lot -->
    <div v-if="lot.building?.isForSale" class="for-sale-info" data-testid="lot-for-sale-info">
      <span class="for-sale-badge-panel">🏪 {{ t('buildingMarket.forSaleBadge') }}</span>
      <span v-if="lot.building.askingPrice" class="for-sale-price">{{ fmtCurrency(lot.building.askingPrice) }}</span>
      <RouterLink to="/buildings/market" class="for-sale-link">{{ t('buildingMarket.viewOnMarket') }}</RouterLink>
    </div>

    <!-- Purchase flow -->
    <div v-if="!isAuthenticated" class="purchase-notice">{{ t('cityMap.loginRequired') }}</div>
    <div v-else-if="companies.length === 0" class="purchase-notice">{{ t('cityMap.noCompany') }}</div>
    <div v-else-if="!isCompanyAccountActive" class="purchase-notice">{{ t('cityMap.companyAccountRequired') }}</div>
    <template v-else>
      <div v-if="purchaseError && !purchaseMode" class="error-message purchase-error-notice" role="alert" aria-live="polite">{{ purchaseError }}</div>
      <template v-if="canPurchase">
        <div v-if="!purchaseMode" class="purchase-actions">
          <button class="btn btn-primary" @click="startPurchase()">{{ t('cityMap.purchase') }}</button>
        </div>
        <div v-else class="purchase-form">
          <div class="form-group">
            <label>{{ t('cityMap.buildingType') }}</label>
            <div class="building-type-cards" role="radiogroup" :aria-label="t('cityMap.buildingType')">
              <button
                v-for="type in suitableTypes"
                :key="type"
                class="building-type-card"
                :class="{ selected: selectedBuildingType === type }"
                type="button"
                role="radio"
                :aria-checked="selectedBuildingType === type"
                @click="selectedBuildingType = type"
              >
                <span class="card-type-icon">{{ t(`buildings.typeIcons.${type}`) }}</span
                ><span class="card-type-name">{{ fmtBuildingType(type) }}</span
                ><span class="card-type-desc">{{ t(`buildings.typeDescriptions.${type}`) }}</span>
              </button>
            </div>
            <p v-if="selectedBuildingType" class="selected-type-guidance">{{ t(`cityMap.${placementGuidanceKey(selectedBuildingType)}`) }}</p>
          </div>
          <div class="form-group">
            <label
              >{{ t('cityMap.buildingName') }} <span class="optional-hint">({{ t('common.optional') }})</span></label
            ><input v-model="buildingName" type="text" class="form-input" :placeholder="t('cityMap.buildingNamePlaceholder')" />
          </div>
          <div class="form-group">
            <label>{{ t('cityMap.company') }}</label>
            <div class="active-company-summary">
              <strong>{{ activeCompany?.name }}</strong
              ><span>{{ activeCompany ? fmtCurrency(activeCompany.cash) : '' }}</span>
            </div>
          </div>
          <!-- Media house channel type -->
          <div v-if="selectedBuildingType === 'MEDIA_HOUSE'" class="form-group">
            <label>{{ t('cityMap.mediaHouseChannelType') }}</label
            ><select v-model="selectedMediaType" class="form-select" required>
              <option value="">{{ t('cityMap.selectMediaType') }}</option>
              <option value="NEWSPAPER">📰 {{ t('cityMap.mediaTypespaper') }} (×1.0)</option>
              <option value="RADIO">📻 {{ t('cityMap.mediaTypeRadio') }} (×1.5)</option>
              <option value="TV">📺 {{ t('cityMap.mediaTypeTv') }} (×2.0)</option>
            </select>
            <p class="form-hint">{{ t('cityMap.mediaTypeHint') }}</p>
          </div>
          <!-- Power plant type picker -->
          <div v-if="selectedBuildingType === 'POWER_PLANT'" class="form-group">
            <label>{{ t('powerPlant.plantTypeLabel') }}</label>
            <div class="plant-type-cards" role="radiogroup" :aria-label="t('powerPlant.plantTypeLabel')">
              <button
                v-for="pt in POWER_PLANT_TYPES"
                :key="pt.type"
                class="plant-type-card"
                :class="{ selected: selectedPowerPlantType === pt.type }"
                type="button"
                role="radio"
                :aria-checked="selectedPowerPlantType === pt.type"
                :aria-label="`${pt.type}${t('powerPlant.outputMw', { output: pt.mw })}`"
                @click="selectedPowerPlantType = pt.type"
              >
                <span class="plant-type-name">{{ t(pt.labelKey) }}</span
                ><span class="plant-type-mw">{{ t('powerPlant.outputMw', { output: pt.mw }) }}</span
                ><span v-if="pt.type === 'SOLAR' && cityWeather" class="plant-weather-badge solar"> ☀️ {{ Math.round(cityWeather.currentSolarPercent) }}% </span
                ><span v-else-if="pt.type === 'WIND' && cityWeather" class="plant-weather-badge wind"> 💨 {{ Math.round(cityWeather.currentWindPercent) }}% </span
                ><span v-else-if="pt.type === 'SOLAR' || pt.type === 'WIND'" class="plant-type-badge renewable"> {{ t('powerPlant.renewableBadge') }} </span
                ><span v-else class="plant-type-badge fuel">{{ t('powerPlant.fuelBadge') }}</span
                ><span class="plant-type-desc">{{ t(pt.descKey) }}</span>
              </button>
            </div>
            <p v-if="!selectedPowerPlantType" class="form-hint">{{ t('powerPlant.noPlantTypeSelected') }}</p>
          </div>
          <!-- Mining deposit summary -->
          <div v-if="selectedBuildingType === 'MINE' && lot.resourceType" class="mining-deposit-summary" data-testid="mining-deposit-summary">
            <h4 class="deposit-summary-title">⛏ {{ t('cityMap.miningDepositSummaryTitle') }}</h4>
            <div class="deposit-summary-grid">
              <div class="deposit-summary-item">
                <span class="deposit-label">{{ t('cityMap.rawMaterialResource') }}</span
                ><span class="deposit-value deposit-resource-name">{{ lot.resourceType.name }}</span>
              </div>
              <div v-if="lot.materialQuality !== null" class="deposit-summary-item">
                <span class="deposit-label">{{ t('cityMap.rawMaterialQuality') }}</span
                ><span class="quality-badge" :class="materialQualityClass(lot.materialQuality!)">
                  {{ materialQualityLabel(lot.materialQuality!) }} ({{ Math.round(lot.materialQuality! * 100) }}%)
                </span>
              </div>
              <div v-if="lot.materialQuantity !== null" class="deposit-summary-item">
                <span class="deposit-label">{{ t('cityMap.rawMaterialQuantity') }}</span
                ><span class="deposit-value">{{ lot.materialQuantity!.toLocaleString(locale) }} {{ t('cityMap.rawMaterialQuantityUnit') }}</span>
              </div>
            </div>
            <p class="deposit-investment-hint">{{ t('cityMap.miningInvestmentHint') }}</p>
          </div>
          <!-- Purchase cost summary -->
          <div class="purchase-cost-summary" aria-label="Purchase cost summary">
            <div class="cost-row">
              <span class="cost-label">{{ t('cityMap.costLotPrice') }}</span
              ><span class="cost-value cost-debit">{{ fmtCurrency(lot.price) }}</span>
            </div>
            <div v-if="selectedBuildingType" class="cost-row">
              <span class="cost-label">{{ t('cityMap.costConstruction') }}</span
              ><span class="cost-value cost-debit">{{ fmtCurrency(constructionCostForType(selectedBuildingType)) }}</span>
            </div>
            <div v-if="selectedBuildingType" class="cost-row construction-time-row">
              <span class="cost-label">{{ t('cityMap.constructionTime') }}</span
              ><span class="cost-value construction-ticks" :title="constructionTicksForType(selectedBuildingType) + ' ticks'">
                {{ t('cityMap.constructionTicks', { time: formatTickDuration(constructionTicksForType(selectedBuildingType), locale) }) }}
              </span>
            </div>
            <div v-if="activeCompany" class="cost-row">
              <span class="cost-label">{{ t('cityMap.costCurrentCash') }}</span
              ><span class="cost-value">{{ fmtCurrency(activeCompany.cash) }}</span>
            </div>
            <div v-if="cashAfterPurchase !== null" class="cost-row cost-row-result">
              <span class="cost-label">{{ t('cityMap.costRemainingCash') }}</span
              ><span class="cost-value" :class="cashAfterPurchase < 0 ? 'cost-negative' : 'cost-positive'"> {{ fmtCurrency(cashAfterPurchase) }} </span>
            </div>
          </div>
          <div v-if="purchaseError" class="error-message" role="alert">{{ purchaseError }}</div>
          <div class="purchase-actions">
            <button class="btn btn-secondary" @click="purchaseMode = false">{{ t('common.cancel') }}</button
            ><button class="btn btn-primary" :disabled="!canSubmitPurchase" @click="confirmPurchase()">{{ purchasing ? t('cityMap.purchasing') : t('cityMap.confirmPurchase') }}</button>
          </div>
        </div>
      </template>
    </template>

    <!-- Post-purchase banner: under-construction -->
    <div v-if="justPurchasedBuildingId && isOwnedByActiveCompany && justPurchasedIsUnderConstruction" class="post-purchase-banner construction-banner" role="status" data-testid="construction-banner">
      <div class="post-purchase-body">
        <strong class="post-purchase-title"> 🏗️ {{ t('cityMap.constructionStartedTitle') }} </strong>
        <p class="post-purchase-text">
          {{
            t('cityMap.constructionStartedBody', {
              type: fmtBuildingType(justPurchasedBuildingType ?? 'FACTORY'),
              time: formatTickDuration(
                justPurchasedConstructionCompletesAtTick ? constructionTicksRemaining(justPurchasedConstructionCompletesAtTick) : constructionTicksForType(justPurchasedBuildingType ?? 'FACTORY'),
                locale,
              ),
            })
          }}
        </p>
        <div class="construction-progress-bar" aria-label="Construction progress"><div class="construction-progress-fill" style="width: 0%"></div></div>
        <p class="construction-hint">{{ t('cityMap.constructionHint') }}</p>
      </div>
    </div>

    <!-- Post-purchase setup guidance (operational) -->
    <div v-else-if="justPurchasedBuildingId && isOwnedByActiveCompany" class="post-purchase-banner" role="status">
      <div class="post-purchase-body">
        <strong class="post-purchase-title">{{ t(`buildings.typeIcons.${justPurchasedBuildingType ?? 'FACTORY'}`) }} {{ t('cityMap.postPurchaseTitle') }}</strong>
        <p class="post-purchase-text">{{ t(`cityMap.${postPurchaseBodyKey(justPurchasedBuildingType ?? 'FACTORY')}`) }}</p>
      </div>
      <RouterLink :to="justPurchasedBuildingType === 'BANK' ? `/bank/${justPurchasedBuildingId}` : `/building/${justPurchasedBuildingId}`" class="btn btn-primary">
        {{ t('cityMap.setupBuilding') }} →
      </RouterLink>
    </div>

    <div v-else-if="isOwnedByDifferentControlledCompany" class="purchase-notice">
      {{ t('cityMap.switchCompanyToManage', { company: lot.ownerCompany?.name ?? t('cityMap.company') }) }}
    </div>

    <!-- Under construction (no just-purchased state) -->
    <div v-else-if="isOwnedByActiveCompany && lot.building && lot.building.isUnderConstruction" class="your-building-actions construction-state" data-testid="under-construction-panel">
      <div class="construction-info">
        <span class="construction-badge">🏗️ {{ t('cityMap.underConstruction') }}</span>
        <p class="construction-detail">{{ lot.building.name }} ({{ fmtBuildingType(lot.building.type) }})</p>
        <p class="construction-ticks-info" data-testid="construction-ticks-remaining" :title="constructionTicksRemaining(lot.building.constructionCompletesAtTick) + ' ticks'">
          {{ t('cityMap.ticksRemaining', { time: formatTickDuration(constructionTicksRemaining(lot.building.constructionCompletesAtTick), locale) }) }}
        </p>
      </div>
      <RouterLink :to="lot.building?.type === 'BANK' ? `/bank/${lot.buildingId}` : `/building/${lot.buildingId}`" class="btn btn-ghost">
        {{ t('cityMap.viewConstruction') }}
      </RouterLink>
    </div>

    <!-- Already owned (operational) -->
    <div v-else-if="isOwnedByActiveCompany && lot.buildingId" class="your-building-actions">
      <RouterLink :to="lot.building?.type === 'BANK' ? `/bank/${lot.buildingId}` : `/building/${lot.buildingId}`" class="btn btn-primary">
        {{ t('cityMap.manageBuilding') }}
      </RouterLink>
    </div>
  </aside>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { RouterLink } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useGameStateStore } from '@/stores/gameState'
import { gqlRequest, GraphQLError } from '@/lib/graphql'
import { formatTickDuration } from '@/lib/gameTime'
import { formatMoney } from '@/lib/currencyFormat'
import {
  formatPopulationIndex,
  populationIndexClass,
  canPurchaseLot as isPurchasable,
  canSubmitPurchaseForm as isFormSubmittable,
  constructionCostForType,
  constructionTicksForType,
  constructionTicksRemaining as computeConstructionTicksRemaining,
} from '@/lib/cityMapHelpers'
import type { BuildingLot, Company, City, CityWeatherForecast, PurchaseLotResult } from '@/types'

const props = defineProps<{
  lot: BuildingLot
  city: City | null
  cityWeather: CityWeatherForecast | null
  isAuthenticated: boolean
  companies: Company[]
  isCompanyAccountActive: boolean
  activeCompany: Company | null
}>()

const emit = defineEmits<{
  'purchase-complete': [result: PurchaseLotResult]
  'lot-refreshed': [lot: BuildingLot]
}>()

const { t, locale } = useI18n()
const gameStateStore = useGameStateStore()

// Purchase flow state
const purchaseMode = ref(false)
const selectedBuildingType = ref('')
const selectedPowerPlantType = ref('')
const buildingName = ref('')
const selectedMediaType = ref('')
const purchasing = ref(false)
const purchaseError = ref<string | null>(null)
const justPurchasedBuildingId = ref<string | null>(null)
const justPurchasedBuildingType = ref<string | null>(null)
const justPurchasedIsUnderConstruction = ref(false)
const justPurchasedConstructionCompletesAtTick = ref<number | null>(null)

const POWER_PLANT_TYPES = [
  { type: 'COAL', labelKey: 'powerGrid.plantTypes.COAL', mw: 50, descKey: 'powerPlant.coalDescription' },
  { type: 'GAS', labelKey: 'powerGrid.plantTypes.GAS', mw: 40, descKey: 'powerPlant.gasDescription' },
  { type: 'SOLAR', labelKey: 'powerGrid.plantTypes.SOLAR', mw: 20, descKey: 'powerPlant.solarDescription' },
  { type: 'WIND', labelKey: 'powerGrid.plantTypes.WIND', mw: 25, descKey: 'powerPlant.windDescription' },
  { type: 'NUCLEAR', labelKey: 'powerGrid.plantTypes.NUCLEAR', mw: 200, descKey: 'powerPlant.nuclearDescription' },
]

const suitableTypes = computed(() => props.lot.suitableTypes.split(',').map((s) => s.trim()))

const lotStatus = computed(() => {
  if (!props.lot.ownerCompanyId) return 'available'
  if (props.companies.some((c) => c.id === props.lot.ownerCompanyId)) return 'yours'
  return 'owned'
})

const isOwnedByActiveCompany = computed(() => !!props.lot.ownerCompanyId && props.lot.ownerCompanyId === props.activeCompany?.id)

const isOwnedByDifferentControlledCompany = computed(() => lotStatus.value === 'yours' && !!props.lot.ownerCompanyId && props.lot.ownerCompanyId !== props.activeCompany?.id)

const canPurchase = computed(() => props.isCompanyAccountActive && isPurchasable(props.isAuthenticated, props.companies.length, props.lot.ownerCompanyId))

const canSubmitPurchase = computed(() => {
  const baseValid = isFormSubmittable(selectedBuildingType.value, buildingName.value, props.activeCompany?.id ?? '', purchasing.value)
  if (selectedBuildingType.value === 'MEDIA_HOUSE' && !selectedMediaType.value) return false
  if (selectedBuildingType.value === 'POWER_PLANT' && !selectedPowerPlantType.value) return false
  return baseValid
})

const cashAfterPurchase = computed(() => {
  if (!props.activeCompany || !props.lot) return null
  const constructionCost = selectedBuildingType.value ? constructionCostForType(selectedBuildingType.value) : 0
  return props.activeCompany.cash - props.lot.price - constructionCost
})

const recommendation = computed(() => {
  const suitable = props.lot.suitableTypes.split(',').map((s) => s.trim())
  const hasMine = suitable.includes('MINE')
  const hasRetail = suitable.includes('SALES_SHOP')
  const hasFactory = suitable.includes('FACTORY')
  if (hasMine && props.lot.resourceType) return { key: 'recommendationResourceOriented', cssClass: 'rec-resource' }
  if (hasRetail && props.lot.populationIndex >= 1.3) return { key: 'recommendationStrongRetail', cssClass: 'rec-retail' }
  if (hasFactory && props.lot.populationIndex < 0.9) return { key: 'recommendationIndustrialEfficiency', cssClass: 'rec-industrial' }
  return { key: 'recommendationBalancedStarter', cssClass: 'rec-balanced' }
})

function constructionTicksRemaining(completesAtTick: number | null): number {
  const currentTick = gameStateStore.gameState?.currentTick ?? 0
  return computeConstructionTicksRemaining(completesAtTick, currentTick)
}

function fmtCurrency(value: number): string {
  return formatMoney(value, props.city?.currencyCode ?? 'EUR', locale.value)
}

function fmtBuildingType(type: string): string {
  const key = `buildings.types.${type}`
  const translated = t(key)
  if (translated !== key) return translated
  return type.replace(/_/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase())
}

function populationIndexLabel(value: number): string {
  if (value >= 1.8) return t('cityMap.populationIndexVeryHigh')
  if (value >= 1.3) return t('cityMap.populationIndexHigh')
  if (value >= 0.9) return t('cityMap.populationIndexMedium')
  return t('cityMap.populationIndexLow')
}

function materialQualityLabel(quality: number): string {
  if (quality >= 0.8) return t('cityMap.rawMaterialQualityExcellent')
  if (quality >= 0.6) return t('cityMap.rawMaterialQualityGood')
  if (quality >= 0.4) return t('cityMap.rawMaterialQualityFair')
  return t('cityMap.rawMaterialQualityPoor')
}

function materialQualityClass(quality: number): string {
  if (quality >= 0.8) return 'quality-excellent'
  if (quality >= 0.6) return 'quality-good'
  if (quality >= 0.4) return 'quality-fair'
  return 'quality-poor'
}

function placementGuidanceKey(buildingType: string): string {
  const map: Record<string, string> = {
    SALES_SHOP: 'placementGuidanceSalesShop',
    COMMERCIAL: 'placementGuidanceCommercial',
    FACTORY: 'placementGuidanceFactory',
    MINE: 'placementGuidanceMine',
    APARTMENT: 'placementGuidanceApartment',
    RESEARCH_DEVELOPMENT: 'placementGuidanceResearchDevelopment',
    POWER_PLANT: 'placementGuidancePowerPlant',
    BANK: 'placementGuidanceBank',
    EXCHANGE: 'placementGuidanceExchange',
    MEDIA_HOUSE: 'placementGuidanceMediaHouse',
  }
  return map[buildingType] ?? 'placementGuidanceGeneric'
}

function postPurchaseBodyKey(buildingType: string): string {
  const map: Record<string, string> = {
    FACTORY: 'postPurchaseBodyFactory',
    MINE: 'postPurchaseBodyMine',
    SALES_SHOP: 'postPurchaseBodySalesShop',
    RESEARCH_DEVELOPMENT: 'postPurchaseBodyResearchDevelopment',
    APARTMENT: 'postPurchaseBodyApartment',
    COMMERCIAL: 'postPurchaseBodyCommercial',
    MEDIA_HOUSE: 'postPurchaseBodyMediaHouse',
    BANK: 'postPurchaseBodyBank',
    EXCHANGE: 'postPurchaseBodyExchange',
    POWER_PLANT: 'postPurchaseBodyPowerPlant',
  }
  return map[buildingType] ?? 'postPurchaseBody'
}

function startPurchase() {
  purchaseMode.value = true
  purchaseError.value = null
}

async function confirmPurchase() {
  if (!canSubmitPurchase.value || !props.activeCompany) return

  purchasing.value = true
  purchaseError.value = null

  try {
    const data = await gqlRequest<{ purchaseLot: PurchaseLotResult }>(
      `mutation PurchaseLot($input: PurchaseLotInput!) {
        purchaseLot(input: $input) {
          lot {
            id cityId name description district latitude longitude price basePrice suitableTypes populationIndex
            ownerCompanyId buildingId
            ownerCompany { id name }
            building { id name type isUnderConstruction constructionCompletesAtTick constructionCost }
            resourceType { id name }
            materialQuality materialQuantity
          }
          building { id name type isUnderConstruction constructionCompletesAtTick constructionCost }
          company { id name cash }
        }
      }`,
      {
        input: {
          companyId: props.activeCompany.id,
          lotId: props.lot.id,
          buildingType: selectedBuildingType.value,
          buildingName: buildingName.value.trim() || null,
          mediaType: selectedBuildingType.value === 'MEDIA_HOUSE' ? selectedMediaType.value || null : null,
          powerPlantType: selectedBuildingType.value === 'POWER_PLANT' ? selectedPowerPlantType.value || null : null,
        },
      },
    )

    justPurchasedBuildingId.value = data.purchaseLot.building.id
    justPurchasedBuildingType.value = data.purchaseLot.building.type
    justPurchasedIsUnderConstruction.value = data.purchaseLot.building.isUnderConstruction ?? false
    justPurchasedConstructionCompletesAtTick.value = data.purchaseLot.building.constructionCompletesAtTick ?? null
    purchaseMode.value = false
    emit('purchase-complete', data.purchaseLot)
  } catch (e: unknown) {
    if (e instanceof GraphQLError) {
      if (e.code === 'LOT_ALREADY_OWNED') {
        purchaseError.value = t('cityMap.purchaseErrorAlreadyOwned')
        purchaseMode.value = false
        try {
          const refreshedLot = await gqlRequest<{ lot: BuildingLot | null }>(
            `query GetLot($id: UUID!) {
              lot(id: $id) {
                id cityId name description district latitude longitude price basePrice suitableTypes populationIndex
                ownerCompanyId buildingId
                ownerCompany { id name }
                building { id name type isUnderConstruction constructionCompletesAtTick constructionCost }
                resourceType { id name }
                materialQuality materialQuantity
              }
            }`,
            { id: props.lot.id },
          )
          if (refreshedLot.lot) {
            emit('lot-refreshed', refreshedLot.lot)
          }
        } catch {
          // Silently ignore refresh errors
        }
      } else if (e.code === 'INSUFFICIENT_FUNDS') {
        purchaseError.value = t('cityMap.purchaseErrorInsufficientFunds')
      } else if (e.code === 'UNSUITABLE_BUILDING_TYPE') {
        purchaseError.value = t('cityMap.purchaseErrorUnsuitable')
      } else {
        purchaseError.value = e.message
      }
    } else {
      purchaseError.value = t('common.unknownError')
    }
  } finally {
    purchasing.value = false
  }
}
</script>

<style scoped>
.detail-panel {
  width: 340px;
  min-width: 280px;
  max-height: calc(100vh - 180px);
  overflow-y: auto;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 1.25rem;
  flex-shrink: 0;
  align-self: start;
  position: sticky;
  top: 80px;
}

.detail-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 0.5rem;
  margin-bottom: 0.5rem;
}

.detail-header h2 {
  font-size: 1.1rem;
  font-weight: 700;
  flex: 1;
}

.status-badge {
  font-size: 0.7rem;
  font-weight: 700;
  padding: 0.2em 0.6em;
  border-radius: 6px;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  flex-shrink: 0;
}

.status-badge.available {
  background: rgba(52, 211, 153, 0.15);
  color: #34d399;
}

.status-badge.owned {
  background: rgba(148, 163, 184, 0.12);
  color: #94a3b8;
}

.status-badge.yours {
  background: rgba(59, 130, 246, 0.15);
  color: #60a5fa;
}

.lot-description {
  font-size: 0.8rem;
  color: var(--color-text-muted);
  margin-bottom: 0.75rem;
  line-height: 1.4;
}

.strategic-recommendation {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  padding: 0.375rem 0.75rem;
  border-radius: 8px;
  font-size: 0.8rem;
  font-weight: 600;
  margin-bottom: 0.875rem;
}

.rec-retail {
  background: rgba(52, 211, 153, 0.12);
  color: #34d399;
}
.rec-resource {
  background: rgba(251, 191, 36, 0.12);
  color: #f59e0b;
}
.rec-industrial {
  background: rgba(99, 102, 241, 0.12);
  color: #818cf8;
}
.rec-balanced {
  background: rgba(148, 163, 184, 0.1);
  color: var(--color-text-muted);
}

.detail-grid {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-bottom: 1rem;
}

.detail-item {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  font-size: 0.85rem;
  gap: 0.5rem;
}

.detail-item.full-width {
  flex-direction: column;
  gap: 0.25rem;
}

.detail-label {
  color: var(--color-text-muted);
  font-size: 0.78rem;
  flex-shrink: 0;
}

.detail-value {
  font-weight: 600;
  text-align: right;
}

.detail-value.price {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  flex-wrap: wrap;
  justify-content: flex-end;
}

.resource-premium-badge {
  font-size: 0.65rem;
  font-weight: 700;
  background: rgba(251, 191, 36, 0.15);
  color: #f59e0b;
  padding: 0.1em 0.4em;
  border-radius: 4px;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.suitable-types {
  display: flex;
  flex-wrap: wrap;
  gap: 0.375rem;
  margin-top: 0.25rem;
}

.type-tag {
  background: var(--color-accent-alpha, rgba(59, 130, 246, 0.12));
  color: var(--color-accent, #3b82f6);
  font-size: 0.75rem;
  font-weight: 600;
  padding: 0.2em 0.6em;
  border-radius: 6px;
}

.owner-info,
.building-info {
  display: flex;
  gap: 0.5rem;
  font-size: 0.85rem;
  margin-bottom: 0.5rem;
}

.raw-material-panel {
  background: var(--color-surface-secondary, rgba(251, 191, 36, 0.05));
  border: 1px solid rgba(251, 191, 36, 0.2);
  border-radius: 8px;
  padding: 0.875rem;
  margin-bottom: 0.875rem;
}

.raw-material-title {
  font-size: 0.9rem;
  font-weight: 700;
  margin-bottom: 0.5rem;
}

.raw-material-grid {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.raw-material-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 0.85rem;
}

.raw-material-item.full-width {
  flex-direction: column;
  align-items: flex-start;
  gap: 0.125rem;
}

.raw-material-hint {
  font-size: 0.75rem;
  color: var(--color-text-muted);
  margin-top: 0.5rem;
  font-style: italic;
}

.quality-badge {
  font-size: 0.75rem;
  font-weight: 700;
  padding: 0.15em 0.5em;
  border-radius: 4px;
}

.quality-badge.quality-excellent {
  background: rgba(52, 211, 153, 0.15);
  color: #34d399;
}
.quality-badge.quality-good {
  background: rgba(59, 130, 246, 0.15);
  color: #60a5fa;
}
.quality-badge.quality-fair {
  background: rgba(251, 191, 36, 0.15);
  color: #f59e0b;
}
.quality-badge.quality-poor {
  background: rgba(239, 68, 68, 0.12);
  color: #f87171;
}

.placement-guidance-panel {
  background: var(--color-surface-secondary, rgba(0, 0, 0, 0.03));
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 0.875rem;
  margin-bottom: 0.875rem;
}

.guidance-title {
  font-size: 0.9rem;
  font-weight: 700;
  margin-bottom: 0.5rem;
}

.guidance-list {
  list-style: none;
  padding: 0;
  margin: 0 0 0.5rem;
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.guidance-item {
  font-size: 0.83rem;
  display: flex;
  gap: 0.375rem;
  align-items: flex-start;
}

.guidance-building-type {
  font-weight: 600;
  flex-shrink: 0;
  min-width: 80px;
}

.guidance-text {
  color: var(--color-text-secondary);
}

.transport-cost-note {
  font-size: 0.75rem;
  color: var(--color-text-muted);
  margin-top: 0.5rem;
}

.weather-outlook-panel {
  background: rgba(99, 102, 241, 0.05);
  border: 1px solid rgba(99, 102, 241, 0.2);
  border-radius: 8px;
  padding: 0.875rem;
  margin-bottom: 0.875rem;
}

.weather-outlook-title {
  font-size: 0.9rem;
  font-weight: 700;
  margin-bottom: 0.5rem;
}

.weather-outlook-row {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 0.5rem;
}

.weather-badge {
  font-size: 0.82rem;
  font-weight: 600;
  padding: 0.2em 0.5em;
  border-radius: 6px;
}

.solar-badge {
  background: rgba(251, 191, 36, 0.15);
  color: #f59e0b;
}
.wind-badge {
  background: rgba(99, 102, 241, 0.12);
  color: #818cf8;
}

.weather-forecast-bars {
  display: flex;
  align-items: flex-end;
  gap: 2px;
  height: 36px;
  overflow: hidden;
}

.forecast-bar-group {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1px;
  flex: 1;
  height: 100%;
  justify-content: flex-end;
  position: relative;
}

.forecast-bar {
  width: 100%;
  min-height: 2px;
  border-radius: 2px 2px 0 0;
}

.forecast-bar.solar-bar {
  background: #fbbf24;
}
.forecast-bar.wind-bar {
  background: #818cf8;
}

.forecast-bar-label {
  position: absolute;
  bottom: -12px;
  font-size: 0.6rem;
  color: var(--color-text-muted);
  white-space: nowrap;
}

.purchase-notice {
  font-size: 0.85rem;
  color: var(--color-text-muted);
  padding: 0.75rem;
  background: var(--color-surface-secondary, rgba(0, 0, 0, 0.04));
  border-radius: 8px;
  text-align: center;
  margin-top: 0.5rem;
}

.purchase-actions {
  display: flex;
  gap: 0.5rem;
  margin-top: 0.75rem;
}

.purchase-form {
  margin-top: 0.75rem;
}

.form-group {
  margin-bottom: 0.875rem;
}

.form-group label {
  display: block;
  font-size: 0.82rem;
  font-weight: 600;
  margin-bottom: 0.375rem;
  color: var(--color-text-secondary);
}

.form-input,
.form-select {
  width: 100%;
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-input-bg, var(--color-surface));
  color: var(--color-text-primary);
  font-size: 0.875rem;
}

.form-hint {
  font-size: 0.75rem;
  color: var(--color-text-muted);
  margin-top: 0.375rem;
}

.active-company-summary {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.5rem 0.75rem;
  background: var(--color-surface-secondary, rgba(0, 0, 0, 0.04));
  border-radius: 6px;
  font-size: 0.875rem;
}

.building-type-cards {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-bottom: 0.5rem;
}

.building-type-card {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  padding: 0.625rem 0.75rem;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-surface);
  cursor: pointer;
  text-align: left;
  transition:
    border-color 0.15s,
    background 0.15s;
  flex-wrap: wrap;
}

.building-type-card:hover {
  border-color: var(--color-accent, #3b82f6);
}

.building-type-card.selected {
  border-color: var(--color-accent, #3b82f6);
  background: var(--color-accent-alpha, rgba(59, 130, 246, 0.08));
}

.card-type-icon {
  font-size: 1.1rem;
  flex-shrink: 0;
}
.card-type-name {
  font-weight: 600;
  font-size: 0.85rem;
}
.card-type-desc {
  font-size: 0.75rem;
  color: var(--color-text-muted);
  width: 100%;
}

.plant-type-cards {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-bottom: 0.5rem;
}

.plant-type-card {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.625rem 0.75rem;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-surface);
  cursor: pointer;
  text-align: left;
  transition: border-color 0.15s;
  flex-wrap: wrap;
}

.plant-type-card.selected {
  border-color: var(--color-accent, #3b82f6);
  background: var(--color-accent-alpha, rgba(59, 130, 246, 0.08));
}

.plant-type-name {
  font-weight: 600;
  font-size: 0.85rem;
}
.plant-type-mw {
  font-size: 0.8rem;
  color: var(--color-text-muted);
}
.plant-type-badge,
.plant-weather-badge {
  font-size: 0.7rem;
  font-weight: 600;
  padding: 0.1em 0.4em;
  border-radius: 4px;
}
.plant-type-badge.renewable {
  background: rgba(52, 211, 153, 0.12);
  color: #34d399;
}
.plant-type-badge.fuel {
  background: rgba(148, 163, 184, 0.12);
  color: #94a3b8;
}
.plant-weather-badge.solar {
  background: rgba(251, 191, 36, 0.15);
  color: #f59e0b;
}
.plant-weather-badge.wind {
  background: rgba(99, 102, 241, 0.12);
  color: #818cf8;
}
.plant-type-desc {
  font-size: 0.75rem;
  color: var(--color-text-muted);
  width: 100%;
}

.selected-type-guidance {
  font-size: 0.8rem;
  color: var(--color-text-secondary);
  margin-top: 0.375rem;
}

.optional-hint {
  font-weight: 400;
  color: var(--color-text-muted);
}

.mining-deposit-summary {
  background: rgba(251, 191, 36, 0.06);
  border: 1px solid rgba(251, 191, 36, 0.2);
  border-radius: 8px;
  padding: 0.75rem;
  margin-bottom: 0.75rem;
}

.deposit-summary-title {
  font-size: 0.85rem;
  font-weight: 700;
  margin-bottom: 0.5rem;
}
.deposit-summary-grid {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}
.deposit-summary-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 0.82rem;
}
.deposit-label {
  color: var(--color-text-muted);
}
.deposit-value {
  font-weight: 600;
}
.deposit-resource-name {
  color: var(--color-accent, #3b82f6);
}
.deposit-investment-hint {
  font-size: 0.75rem;
  color: var(--color-text-muted);
  margin-top: 0.375rem;
  font-style: italic;
}

.purchase-cost-summary {
  background: var(--color-surface-secondary, rgba(0, 0, 0, 0.04));
  border-radius: 8px;
  padding: 0.75rem;
  margin-bottom: 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.cost-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 0.85rem;
}

.cost-row-result {
  border-top: 1px solid var(--color-border);
  padding-top: 0.375rem;
  margin-top: 0.125rem;
  font-weight: 600;
}

.cost-label {
  color: var(--color-text-muted);
}
.cost-value {
  font-weight: 500;
}
.cost-debit {
  color: #f87171;
}
.cost-positive {
  color: #34d399;
}
.cost-negative {
  color: #ef4444;
}

.error-message {
  background: rgba(239, 68, 68, 0.1);
  border: 1px solid rgba(239, 68, 68, 0.2);
  color: #ef4444;
  font-size: 0.85rem;
  padding: 0.625rem 0.875rem;
  border-radius: 8px;
  margin-bottom: 0.5rem;
}

.purchase-error-notice {
  margin-top: 0.5rem;
}

.post-purchase-banner {
  background: var(--color-surface-secondary, rgba(52, 211, 153, 0.05));
  border: 1px solid rgba(52, 211, 153, 0.25);
  border-radius: 10px;
  padding: 1rem;
  margin-top: 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.construction-banner {
  border-color: rgba(251, 191, 36, 0.3);
  background: rgba(251, 191, 36, 0.05);
}

.post-purchase-body {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}
.post-purchase-title {
  font-size: 0.95rem;
}
.post-purchase-text {
  font-size: 0.82rem;
  color: var(--color-text-secondary);
  line-height: 1.5;
}

.construction-progress-bar {
  height: 4px;
  background: var(--color-border);
  border-radius: 2px;
  overflow: hidden;
  margin: 0.25rem 0;
}

.construction-progress-fill {
  height: 100%;
  background: #f59e0b;
  border-radius: 2px;
}

.construction-hint {
  font-size: 0.75rem;
  color: var(--color-text-muted);
}

.your-building-actions {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-top: 0.75rem;
}

.your-building-actions.construction-state {
  background: rgba(251, 191, 36, 0.05);
  border: 1px solid rgba(251, 191, 36, 0.2);
  border-radius: 10px;
  padding: 1rem;
}

.construction-info {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}
.construction-badge {
  font-size: 0.85rem;
  font-weight: 700;
}
.construction-detail {
  font-size: 0.82rem;
  color: var(--color-text-secondary);
}
.construction-ticks-info {
  font-size: 0.8rem;
  color: #f59e0b;
}

.population-index-item {
  flex-direction: column;
  gap: 0.25rem;
}
.population-index-display {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
.population-index-value {
  font-weight: 700;
  font-size: 1rem;
}
.population-index-tag {
  font-size: 0.72rem;
  font-weight: 700;
  padding: 0.15em 0.5em;
  border-radius: 4px;
  text-transform: uppercase;
}
.pop-very-high {
  background: rgba(52, 211, 153, 0.15);
  color: #34d399;
}
.pop-high {
  background: rgba(59, 130, 246, 0.15);
  color: #60a5fa;
}
.pop-medium {
  background: rgba(148, 163, 184, 0.12);
  color: var(--color-text-muted);
}
.pop-low {
  background: rgba(239, 68, 68, 0.1);
  color: #f87171;
}

.population-index-hint {
  font-size: 0.75rem;
  color: var(--color-text-muted);
}

.coordinates-value {
  font-family: monospace;
  font-size: 0.82rem;
}
.coordinates-hint {
  font-size: 0.72rem;
  color: var(--color-text-muted);
}

.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 44px;
  padding: 0.5rem 1rem;
  border-radius: 8px;
  font-weight: 600;
  font-size: 0.875rem;
  cursor: pointer;
  border: none;
  text-decoration: none;
  transition:
    background 0.15s,
    opacity 0.15s;
  white-space: nowrap;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
.btn-primary {
  background: var(--color-accent, #3b82f6);
  color: #fff;
}
.btn-primary:hover:not(:disabled) {
  background: var(--color-accent-hover, #2563eb);
}
.btn-secondary {
  background: var(--color-surface-secondary, rgba(148, 163, 184, 0.12));
  color: var(--color-text-secondary);
  border: 1px solid var(--color-border);
}
.btn-ghost {
  background: transparent;
  color: var(--color-accent, #3b82f6);
  border: 1px solid var(--color-accent, #3b82f6);
}

@media (max-width: 768px) {
  .detail-panel {
    position: fixed;
    left: 0.5rem;
    right: 0.5rem;
    bottom: 0.5rem;
    width: auto;
    min-width: 0;
    max-height: min(68vh, 42rem);
    border-radius: 14px 14px 10px 10px;
    z-index: 140;
    box-shadow: 0 -14px 40px rgba(0, 0, 0, 0.45);
    padding-bottom: calc(1.1rem + env(safe-area-inset-bottom, 0px));
    -webkit-overflow-scrolling: touch;
  }
}
</style>

<script setup lang="ts">
import { inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'
import BuildingFinancialTimelineChart from '@/components/buildings/BuildingFinancialTimelineChart.vue'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const {
  locale,
  building,
  buildingFinancialTimeline,
  buildingFinancialTimelineLoading,
  buildingOverviewCityName,
  cityCurrencyCode,
  buildingOverviewMapRoute,
  buildingFinancialSnapshots,
  buildingFinancialHasActivity,
  formatCurrency,
  formatBuildingType,
  formatGameTickTime,
  formatGpsLocation,
} = bd
</script>

<template>
  <div class="building-overview-tab">
    <p class="building-overview-name">{{ building?.name }}</p>
    <p class="unit-desc">{{ t('buildingDetail.overview.subtitle', { type: formatBuildingType(building?.type ?? '') }) }}</p>

    <!-- P&L statistics card -->
    <div class="unit-insight-card building-financial-card">
      <h5>{{ t('buildingDetail.overview.statsTitle') }}</h5>
      <p
        v-if="buildingFinancialTimeline"
        class="config-help"
        :title="t('buildingDetail.overview.tickWindow', { start: buildingFinancialTimeline.dataFromTick, end: buildingFinancialTimeline.dataToTick })"
      >
        {{ formatGameTickTime(buildingFinancialTimeline.dataFromTick, locale) }} – {{ formatGameTickTime(buildingFinancialTimeline.dataToTick, locale) }}
      </p>
      <p v-else-if="buildingFinancialTimelineLoading" class="config-help">{{ t('common.loading') }}</p>

      <div class="mi-summary-grid">
        <div class="mi-metric">
          <span class="mi-metric-label">{{ t('buildingDetail.overview.sales') }}</span>
          <strong class="mi-metric-value">{{ formatCurrency(buildingFinancialTimeline?.totalSales ?? 0) }}</strong>
        </div>
        <div class="mi-metric">
          <span class="mi-metric-label">{{ t('buildingDetail.overview.costs') }}</span>
          <strong class="mi-metric-value">{{ formatCurrency(buildingFinancialTimeline?.totalCosts ?? 0) }}</strong>
        </div>
        <div class="mi-metric">
          <span class="mi-metric-label">{{ t('buildingDetail.overview.profit') }}</span>
          <strong
            class="mi-metric-value"
            :class="{
              'building-profit-positive-text': (buildingFinancialTimeline?.totalProfit ?? 0) >= 0,
              'building-profit-negative-text': (buildingFinancialTimeline?.totalProfit ?? 0) < 0,
            }"
          >
            {{ formatCurrency(buildingFinancialTimeline?.totalProfit ?? 0) }}
          </strong>
        </div>
      </div>

      <template v-if="buildingFinancialTimeline">
        <BuildingFinancialTimelineChart
          v-if="buildingFinancialHasActivity"
          :timeline="buildingFinancialSnapshots"
          :currency-code="cityCurrencyCode"
        />
        <p v-else class="mi-empty-state">{{ t('buildingDetail.overview.noFinancialData') }}</p>
      </template>
      <p v-else-if="!buildingFinancialTimelineLoading" class="mi-empty-state">{{ t('buildingDetail.overview.loadFailed') }}</p>
    </div>

    <!-- Location card -->
    <div class="unit-insight-card building-location-card">
      <h5>{{ t('buildingDetail.overview.locationTitle') }}</h5>
      <div class="building-overview-location-grid">
        <div class="building-overview-location-row">
          <span class="building-overview-label">{{ t('buildingDetail.overview.city') }}</span>
          <strong>{{ buildingOverviewCityName }}</strong>
        </div>
        <div class="building-overview-location-row">
          <span class="building-overview-label">{{ t('buildingDetail.overview.gps') }}</span>
          <strong>{{ formatGpsLocation(building?.latitude, building?.longitude) }}</strong>
        </div>
      </div>
      <RouterLink v-if="buildingOverviewMapRoute" :to="buildingOverviewMapRoute" class="btn btn-secondary btn-sm building-overview-map-link">
        {{ t('buildingDetail.overview.showOnMap') }}
      </RouterLink>
    </div>
  </div>
</template>

<style scoped src="./BuildingSidebar.shared.css"></style>
<style scoped src="./BuildingSidebar.analytics.css"></style>

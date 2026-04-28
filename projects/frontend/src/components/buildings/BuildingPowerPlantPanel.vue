<script setup lang="ts">
import { inject, onMounted, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const {
  building,
  powerPlantAnalytics,
  powerPlantAnalyticsLoading,
  cityPowerBalance,
  cityPowerBalanceLoading,
  loadCityPowerBalance,
  formatCurrency,
} = bd

function loadBalanceIfNeeded() {
  if (building.value?.type === 'POWER_PLANT' && building.value?.cityId) {
    loadCityPowerBalance(building.value.cityId)
  }
}

onMounted(loadBalanceIfNeeded)
watch(() => building.value?.cityId, loadBalanceIfNeeded)
</script>

<template>
  <div
    v-if="building?.type === 'POWER_PLANT'"
    class="power-plant-analytics-panel mb-4 rounded-xl border border-divider bg-card p-4 sm:p-5"
    role="region"
    :aria-label="t('buildingDetail.accessibility.powerPlantAnalytics')"
  >
    <div class="power-plant-analytics-header mb-3 flex flex-wrap items-center justify-between gap-2">
      <h2 class="power-plant-analytics-title text-lg font-semibold text-foreground">{{ t('powerPlant.analytics.panelTitle') }}</h2>
      <span class="meta-pill inline-flex items-center gap-1.5 rounded-full border border-divider bg-surface px-3 py-1.5 text-xs font-medium text-muted">
        {{ building?.powerOutput != null ? building?.powerOutput : '' }} MW · {{ building?.powerPlantType ?? '—' }}
      </span>
    </div>

    <!-- City power status block -->
    <div
      v-if="cityPowerBalanceLoading"
      class="ppa-city-status mb-3 rounded-lg border border-divider bg-surface px-3 py-2 text-sm text-muted"
    >{{ t('common.loading') }}</div>
    <div
      v-else-if="cityPowerBalance"
      class="ppa-city-status mb-3 rounded-lg border border-divider bg-surface px-3 py-2"
      :class="{
        'border-green-600/40': cityPowerBalance.status === 'BALANCED',
        'border-yellow-500/40': cityPowerBalance.status === 'CONSTRAINED',
        'border-red-600/40': cityPowerBalance.status === 'CRITICAL',
      }"
    >
      <div class="flex flex-wrap items-center justify-between gap-2">
        <span class="text-xs font-semibold uppercase tracking-wide text-muted">{{ t('powerPlant.cityPowerStatus.title') }}</span>
        <span
          class="rounded-full px-2 py-0.5 text-xs font-bold"
          :class="{
            'bg-green-600/20 text-green-400': cityPowerBalance.status === 'BALANCED',
            'bg-yellow-500/20 text-yellow-400': cityPowerBalance.status === 'CONSTRAINED',
            'bg-red-600/20 text-red-400': cityPowerBalance.status === 'CRITICAL',
          }"
        >
          {{
            cityPowerBalance.status === 'BALANCED'
              ? t('powerPlant.cityPowerStatus.statusPowered')
              : cityPowerBalance.status === 'CONSTRAINED'
                ? t('powerPlant.cityPowerStatus.statusConstrained')
                : t('powerPlant.cityPowerStatus.statusOffline')
          }}
        </span>
      </div>
      <div class="mt-2 flex flex-wrap gap-4 text-sm">
        <span>⚡ {{ t('powerPlant.cityPowerStatus.totalSupply') }}: <strong>{{ cityPowerBalance.totalSupplyMw.toFixed(1) }} MW</strong></span>
        <span>🏭 {{ t('powerPlant.cityPowerStatus.totalDemand') }}: <strong>{{ cityPowerBalance.totalDemandMw.toFixed(1) }} MW</strong></span>
        <span>
          {{ t('powerPlant.cityPowerStatus.balance') }}:
          <strong
            :class="{
              'text-green-400': cityPowerBalance.reserveMw >= 0,
              'text-red-400': cityPowerBalance.reserveMw < 0,
            }"
          >{{ cityPowerBalance.reserveMw >= 0 ? '+' : '' }}{{ cityPowerBalance.reserveMw.toFixed(1) }} MW</strong>
        </span>
      </div>
      <p class="ppa-city-hint mt-1.5 text-xs text-muted">
        {{
          cityPowerBalance.reserveMw > 0
            ? t('powerPlant.cityPowerStatus.surplusHint')
            : cityPowerBalance.reserveMw < 0
              ? t('powerPlant.cityPowerStatus.shortageHint')
              : t('powerPlant.cityPowerStatus.balancedHint')
        }}
      </p>
    </div>

    <div v-if="powerPlantAnalyticsLoading" class="ppa-loading py-2 text-sm text-muted">{{ t('common.loading') }}</div>

    <template v-else-if="powerPlantAnalytics">
      <p class="ppa-tick-window config-help mb-3 text-sm text-muted">
        {{ t('powerPlant.analytics.tickWindow', { start: powerPlantAnalytics.dataFromTick, end: powerPlantAnalytics.dataToTick }) }}
      </p>

      <div class="ppa-summary-grid grid grid-cols-2 gap-3 sm:grid-cols-4">
        <div class="ppa-metric rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-metric-label" :title="t('powerPlant.analytics.surplusHint', { rate: 5 })">
            {{ t('powerPlant.analytics.surplusIncome') }}
          </span>
          <strong class="ppa-metric-value ppa-income mt-1 block text-base">{{ formatCurrency(powerPlantAnalytics.totalSurplusIncome) }}</strong>
        </div>
        <div class="ppa-metric rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-metric-label" :title="t('powerPlant.analytics.fineHint', { rate: 8 })">
            {{ t('powerPlant.analytics.gridFine') }}
          </span>
          <strong class="ppa-metric-value ppa-fine mt-1 block text-base">{{ formatCurrency(powerPlantAnalytics.totalGridFines) }}</strong>
        </div>
        <div class="ppa-metric rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-metric-label">{{ t('powerPlant.analytics.operatingCosts') }}</span>
          <strong class="ppa-metric-value ppa-cost mt-1 block text-base">{{ formatCurrency(powerPlantAnalytics.totalOperatingCosts) }}</strong>
        </div>
        <div class="ppa-metric rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-metric-label">{{ t('powerPlant.analytics.netProfit') }}</span>
          <strong
            class="ppa-metric-value mt-1 block text-base"
            :class="{
              'building-profit-positive-text': powerPlantAnalytics.totalNetProfit >= 0,
              'building-profit-negative-text': powerPlantAnalytics.totalNetProfit < 0,
            }"
          >
            {{ formatCurrency(powerPlantAnalytics.totalNetProfit) }}
          </strong>
        </div>
      </div>

      <!-- Per-tick P&L bar chart -->
      <div
        v-if="powerPlantAnalytics.timeline.some((s) => s.surplusIncome > 0 || s.gridFine > 0 || s.operatingCosts > 0)"
        class="ppa-chart mt-4 flex h-14 items-end gap-px overflow-hidden rounded-md border border-divider bg-surface px-1 py-1"
        role="img"
        :aria-label="t('powerPlant.analytics.panelTitle')"
      >
        <div
          v-for="snap in powerPlantAnalytics.timeline"
          :key="snap.tick"
          class="ppa-bar-group flex min-w-[2px] flex-1 items-end gap-px"
          :title="
            t('powerPlant.analytics.tickTooltip', {
              tick: snap.tick,
              income: formatCurrency(snap.surplusIncome),
              costs: formatCurrency(snap.gridFine + snap.operatingCosts),
            })
          "
        >
          <div
            v-if="snap.surplusIncome > 0"
            class="ppa-bar ppa-bar-income min-w-[1px] flex-1 rounded-t-sm"
            :style="{
              height: `${Math.min(Math.round((snap.surplusIncome / (Math.max(...powerPlantAnalytics.timeline.map((s) => Math.max(s.surplusIncome, s.gridFine + s.operatingCosts))) || 1)) * 50), 50)}px`,
            }"
          />
          <div
            v-if="snap.gridFine + snap.operatingCosts > 0"
            class="ppa-bar ppa-bar-cost min-w-[1px] flex-1 rounded-t-sm"
            :style="{
              height: `${Math.min(Math.round(((snap.gridFine + snap.operatingCosts) / (Math.max(...powerPlantAnalytics.timeline.map((s) => Math.max(s.surplusIncome, s.gridFine + s.operatingCosts))) || 1)) * 50), 50)}px`,
            }"
          />
        </div>
      </div>
      <p v-else class="ppa-empty-state mt-3 text-sm text-muted">{{ t('powerPlant.analytics.noData') }}</p>
    </template>

    <p v-else class="ppa-empty-state mt-3 text-sm text-muted">{{ t('powerPlant.analytics.noData') }}</p>

    <!-- Unit description cards for all power plant unit types -->
    <div class="ppa-unit-guide mt-4 grid gap-3 border-t border-divider pt-4">
      <h3 class="text-xs font-semibold uppercase tracking-wide text-muted">⚡ Generation Units</h3>
      <div class="grid gap-2 sm:grid-cols-2">
        <div class="ppa-unit-card flex items-start gap-3 rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-unit-icon text-2xl">⚡</span>
          <div>
            <strong>{{ t('powerPlant.units.POWER_GENERATION.label') }}</strong>
            <p class="ppa-unit-desc mt-1 text-sm text-muted">{{ t('powerPlant.units.POWER_GENERATION.description', { boost: 10 }) }}</p>
          </div>
        </div>
        <div class="ppa-unit-card flex items-start gap-3 rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-unit-icon text-2xl">🔥</span>
          <div>
            <strong>{{ t('powerPlant.units.ENERGY_PRODUCING.label') }}</strong>
            <p class="ppa-unit-desc mt-1 text-sm text-muted">{{ t('powerPlant.units.ENERGY_PRODUCING.description', { boost: 20 }) }}</p>
          </div>
        </div>
        <div class="ppa-unit-card flex items-start gap-3 rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-unit-icon text-2xl">💨</span>
          <div>
            <strong>{{ t('powerPlant.units.WIND_TURBINE.label') }}</strong>
            <p class="ppa-unit-desc mt-1 text-sm text-muted">{{ t('powerPlant.units.WIND_TURBINE.description', { boost: 8 }) }}</p>
          </div>
        </div>
        <div class="ppa-unit-card flex items-start gap-3 rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-unit-icon text-2xl">💧</span>
          <div>
            <strong>{{ t('powerPlant.units.WATER_TURBINE.label') }}</strong>
            <p class="ppa-unit-desc mt-1 text-sm text-muted">{{ t('powerPlant.units.WATER_TURBINE.description', { boost: 12 }) }}</p>
          </div>
        </div>
      </div>
      <h3 class="mt-1 text-xs font-semibold uppercase tracking-wide text-muted">🔋 Support Units</h3>
      <div class="grid gap-2 sm:grid-cols-3">
        <div class="ppa-unit-card flex items-start gap-3 rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-unit-icon text-2xl">🔋</span>
          <div>
            <strong>{{ t('powerPlant.units.BATTERY_STORAGE.label') }}</strong>
            <p class="ppa-unit-desc mt-1 text-sm text-muted">{{ t('powerPlant.units.BATTERY_STORAGE.description', { buffer: 5 }) }}</p>
          </div>
        </div>
        <div class="ppa-unit-card flex items-start gap-3 rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-unit-icon text-2xl">🪨</span>
          <div>
            <strong>{{ t('powerPlant.units.ENERGY_STORAGE.label') }}</strong>
            <p class="ppa-unit-desc mt-1 text-sm text-muted">{{ t('powerPlant.units.ENERGY_STORAGE.description', { buffer: 8 }) }}</p>
          </div>
        </div>
        <div class="ppa-unit-card flex items-start gap-3 rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-unit-icon text-2xl">⛽</span>
          <div>
            <strong>{{ t('powerPlant.units.FUEL_PURCHASE.label') }}</strong>
            <p class="ppa-unit-desc mt-1 text-sm text-muted">{{ t('powerPlant.units.FUEL_PURCHASE.description', { boost: 10 }) }}</p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

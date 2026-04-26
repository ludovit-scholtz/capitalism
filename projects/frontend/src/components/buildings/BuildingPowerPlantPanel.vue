<script setup lang="ts">
import { inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const { building, powerPlantAnalytics, powerPlantAnalyticsLoading, formatCurrency } = bd
</script>

<template>
  <div v-if="building?.type === 'POWER_PLANT'" class="power-plant-analytics-panel mb-4 rounded-xl border border-divider bg-card p-4 sm:p-5" role="region" aria-label="power plant analytics">
    <div class="power-plant-analytics-header mb-3 flex flex-wrap items-center justify-between gap-2">
      <h2 class="power-plant-analytics-title text-lg font-semibold text-foreground">{{ t('powerPlant.analytics.panelTitle') }}</h2>
      <span class="meta-pill inline-flex items-center gap-1.5 rounded-full border border-divider bg-surface px-3 py-1.5 text-xs font-medium text-muted">
        {{ building?.powerOutput != null ? building?.powerOutput : '' }} MW · {{ building?.powerPlantType ?? '—' }}
      </span>
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
          :title="`Tick ${snap.tick}: +${formatCurrency(snap.surplusIncome)} income, -${formatCurrency(snap.gridFine + snap.operatingCosts)} costs`"
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

    <!-- Unit description cards for POWER_GENERATION and BATTERY_STORAGE -->
    <div class="ppa-unit-guide mt-4 grid gap-3 border-t border-divider pt-4">
      <div class="ppa-unit-card flex items-start gap-3 rounded-lg border border-divider bg-surface p-3">
        <span class="ppa-unit-icon text-2xl">⚡</span>
        <div>
          <strong>{{ t('powerPlant.units.POWER_GENERATION.label') }}</strong>
          <p class="ppa-unit-desc mt-1 text-sm text-muted">{{ t('powerPlant.units.POWER_GENERATION.description', { boost: 10 }) }}</p>
        </div>
      </div>
      <div class="ppa-unit-card flex items-start gap-3 rounded-lg border border-divider bg-surface p-3">
        <span class="ppa-unit-icon text-2xl">🔋</span>
        <div>
          <strong>{{ t('powerPlant.units.BATTERY_STORAGE.label') }}</strong>
          <p class="ppa-unit-desc mt-1 text-sm text-muted">{{ t('powerPlant.units.BATTERY_STORAGE.description', { buffer: 5 }) }}</p>
        </div>
      </div>
    </div>
  </div>
</template>

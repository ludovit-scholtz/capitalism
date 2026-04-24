<script setup lang="ts">
import { inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const { building, powerPlantAnalytics, powerPlantAnalyticsLoading, formatCurrency } = bd
</script>

<template>
<div v-if="building?.type === 'POWER_PLANT'" class="power-plant-analytics-panel" role="region" aria-label="power plant analytics">
  <div class="power-plant-analytics-header">
    <h2 class="power-plant-analytics-title">{{ t('powerPlant.analytics.panelTitle') }}</h2>
    <span class="meta-pill" style="font-size: 0.8rem">
      {{ building?.powerOutput != null ? building?.powerOutput : '' }} MW
      · {{ building?.powerPlantType ?? '—' }}
    </span>
  </div>

  <div v-if="powerPlantAnalyticsLoading" class="ppa-loading">{{ t('common.loading') }}</div>

  <template v-else-if="powerPlantAnalytics">
    <p class="ppa-tick-window config-help">
      {{ t('powerPlant.analytics.tickWindow', { start: powerPlantAnalytics.dataFromTick, end: powerPlantAnalytics.dataToTick }) }}
    </p>

    <div class="ppa-summary-grid">
      <div class="ppa-metric">
        <span class="ppa-metric-label" :title="t('powerPlant.analytics.surplusHint', { rate: 5 })">
          {{ t('powerPlant.analytics.surplusIncome') }}
        </span>
        <strong class="ppa-metric-value ppa-income">{{ formatCurrency(powerPlantAnalytics.totalSurplusIncome) }}</strong>
      </div>
      <div class="ppa-metric">
        <span class="ppa-metric-label" :title="t('powerPlant.analytics.fineHint', { rate: 8 })">
          {{ t('powerPlant.analytics.gridFine') }}
        </span>
        <strong class="ppa-metric-value ppa-fine">{{ formatCurrency(powerPlantAnalytics.totalGridFines) }}</strong>
      </div>
      <div class="ppa-metric">
        <span class="ppa-metric-label">{{ t('powerPlant.analytics.operatingCosts') }}</span>
        <strong class="ppa-metric-value ppa-cost">{{ formatCurrency(powerPlantAnalytics.totalOperatingCosts) }}</strong>
      </div>
      <div class="ppa-metric">
        <span class="ppa-metric-label">{{ t('powerPlant.analytics.netProfit') }}</span>
        <strong
          class="ppa-metric-value"
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
    <div v-if="powerPlantAnalytics.timeline.some(s => s.surplusIncome > 0 || s.gridFine > 0 || s.operatingCosts > 0)" class="ppa-chart" role="img" :aria-label="t('powerPlant.analytics.panelTitle')">
      <div
        v-for="snap in powerPlantAnalytics.timeline"
        :key="snap.tick"
        class="ppa-bar-group"
        :title="`Tick ${snap.tick}: +${formatCurrency(snap.surplusIncome)} income, -${formatCurrency(snap.gridFine + snap.operatingCosts)} costs`"
      >
        <div
          v-if="snap.surplusIncome > 0"
          class="ppa-bar ppa-bar-income"
          :style="{ height: `${Math.min(Math.round((snap.surplusIncome / (Math.max(...powerPlantAnalytics.timeline.map(s => Math.max(s.surplusIncome, s.gridFine + s.operatingCosts))) || 1)) * 50), 50)}px` }"
        />
        <div
          v-if="snap.gridFine + snap.operatingCosts > 0"
          class="ppa-bar ppa-bar-cost"
          :style="{ height: `${Math.min(Math.round(((snap.gridFine + snap.operatingCosts) / (Math.max(...powerPlantAnalytics.timeline.map(s => Math.max(s.surplusIncome, s.gridFine + s.operatingCosts))) || 1)) * 50), 50)}px` }"
        />
      </div>
    </div>
    <p v-else class="ppa-empty-state">{{ t('powerPlant.analytics.noData') }}</p>
  </template>

  <p v-else class="ppa-empty-state">{{ t('powerPlant.analytics.noData') }}</p>

  <!-- Unit description cards for POWER_GENERATION and BATTERY_STORAGE -->
  <div class="ppa-unit-guide">
    <div class="ppa-unit-card">
      <span class="ppa-unit-icon">⚡</span>
      <div>
        <strong>{{ t('powerPlant.units.POWER_GENERATION.label') }}</strong>
        <p class="ppa-unit-desc">{{ t('powerPlant.units.POWER_GENERATION.description', { boost: 10 }) }}</p>
      </div>
    </div>
    <div class="ppa-unit-card">
      <span class="ppa-unit-icon">🔋</span>
      <div>
        <strong>{{ t('powerPlant.units.BATTERY_STORAGE.label') }}</strong>
        <p class="ppa-unit-desc">{{ t('powerPlant.units.BATTERY_STORAGE.description', { buffer: 5 }) }}</p>
      </div>
    </div>
  </div>
</div>

</template>

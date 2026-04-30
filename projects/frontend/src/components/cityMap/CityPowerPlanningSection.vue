<template>
  <section class="city-power-section" aria-labelledby="city-power-heading" data-testid="city-power-section">
    <h2 id="city-power-heading" class="section-heading">⚡ {{ t('powerGrid.weatherSectionTitle') }}</h2>
    <div class="power-planning-grid">
      <!-- Weather forecast card -->
      <div v-if="cityWeather" class="power-card weather-card" data-testid="city-weather-card">
        <h3 class="power-card-title">🌤️ {{ t('powerGrid.currentConditions') }}</h3>
        <div class="weather-badges">
          <span class="weather-big-badge solar" data-testid="solar-badge"> ☀️ {{ Math.round(cityWeather.currentSolarPercent) }}% </span
          ><span class="weather-big-badge wind" data-testid="wind-badge"> 💨 {{ Math.round(cityWeather.currentWindPercent) }}% </span>
        </div>
        <div v-if="cityWeather.forecast.length > 0" class="forecast-chart">
          <p class="forecast-chart-label">{{ t('powerGrid.forecastBarsLabel', { count: Math.min(cityWeather.forecast.length, 24) }) }}</p>
          <div class="forecast-bars-row" aria-label="Weather forecast chart">
            <div
              v-for="(tick, i) in cityWeather.forecast.slice(0, 24)"
              :key="tick.tick"
              class="forecast-bar-group"
              :title="`Tick ${tick.tick}: ☀️${Math.round(tick.solarPercent)}% 💨${Math.round(tick.windPercent)}%`"
            >
              <div class="forecast-bar solar-bar" :style="{ height: Math.round(tick.solarPercent) + '%' }"></div>
              <div class="forecast-bar wind-bar" :style="{ height: Math.round(tick.windPercent) + '%' }"></div>
              <span v-if="i === 0 || i === 23 || (i === cityWeather.forecast.slice(0, 24).length - 1 && i !== 23)" class="forecast-bar-label">
                {{ i === 0 ? t('powerGrid.forecastNow') : t('powerGrid.forecastTickLabel', { count: i + 1 }) }}
              </span>
            </div>
          </div>
        </div>
      </div>

      <!-- Power balance card -->
      <div class="power-card balance-card" data-testid="city-power-balance-card">
        <h3 class="power-card-title">🏭 {{ t('powerGrid.planningTitle') }}</h3>
        <template v-if="cityPowerBalance">
          <div class="balance-status-row">
            <span
              class="balance-status-badge"
              :class="{
                'status-balanced': cityPowerBalance.status === 'BALANCED',
                'status-constrained': cityPowerBalance.status === 'CONSTRAINED',
                'status-critical': cityPowerBalance.status === 'CRITICAL',
              }"
            >
              {{ t(`powerGrid.status.${cityPowerBalance.status}`) }} </span
            ><span v-if="cityPowerBalance.powerPlantCount === 0" class="legacy-badge">{{ t('powerGrid.legacyGrid') }}</span>
          </div>
          <div class="balance-metrics">
            <div class="balance-metric">
              <span class="balance-metric-label">{{ t('powerGrid.supply') }}</span
              ><span class="balance-metric-value supply">{{ cityPowerBalance.totalSupplyMw.toFixed(1) }} MW</span>
            </div>
            <div class="balance-metric">
              <span class="balance-metric-label">{{ t('powerGrid.demand') }}</span
              ><span class="balance-metric-value demand">{{ cityPowerBalance.totalDemandMw.toFixed(1) }} MW</span>
            </div>
            <div class="balance-metric">
              <span class="balance-metric-label">{{ t('powerGrid.reserve') }}</span
              ><span class="balance-metric-value" :class="cityPowerBalance.reserveMw >= 0 ? 'reserve-ok' : 'reserve-low'">
                {{ cityPowerBalance.reserveMw >= 0 ? '+' : '' }}{{ cityPowerBalance.reserveMw.toFixed(1) }} MW
              </span>
            </div>
          </div>
          <p class="balance-guidance">
            {{
              cityPowerBalance.powerPlantCount === 0
                ? t('powerGrid.guidanceLegacy')
                : cityPowerBalance.status === 'BALANCED'
                  ? t('powerGrid.guidanceBalanced')
                  : cityPowerBalance.status === 'CONSTRAINED'
                    ? t('powerGrid.guidanceConstrained')
                    : t('powerGrid.guidanceCritical')
            }}
          </p>
        </template>
        <p v-else class="balance-loading">{{ t('common.loading') }}</p>
      </div>

      <!-- Why it matters card -->
      <div class="power-card why-card" data-testid="why-matters-card">
        <h3 class="power-card-title">💡 {{ t('powerGrid.whyMattersTitle') }}</h3>
        <ul class="why-list">
          <li class="why-item solar-item">☀️ {{ t('powerGrid.whyMattersSolar') }}</li>
          <li class="why-item wind-item">💨 {{ t('powerGrid.whyMattersWind') }}</li>
          <li class="why-item power-item">⚡ {{ t('powerGrid.whyMattersPower') }}</li>
        </ul>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { CityWeatherForecast, CityPowerBalance } from '@/types'

defineProps<{
  cityWeather: CityWeatherForecast | null
  cityPowerBalance: CityPowerBalance | null
}>()

const { t } = useI18n()
</script>

<style scoped>
.city-power-section {
  margin-top: 2.5rem;
}

.section-heading {
  font-size: 1.25rem;
  font-weight: 700;
  margin-bottom: 1rem;
  color: var(--color-text-primary);
}

.power-planning-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 1.25rem;
}

.power-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 10px;
  padding: 1.25rem;
}

.power-card-title {
  font-size: 1rem;
  font-weight: 600;
  margin-bottom: 0.875rem;
}

.weather-badges {
  display: flex;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.weather-big-badge {
  padding: 0.375rem 0.875rem;
  border-radius: 8px;
  font-size: 1rem;
  font-weight: 700;
}

.weather-big-badge.solar {
  background: rgba(251, 191, 36, 0.15);
  color: #f59e0b;
}

.weather-big-badge.wind {
  background: rgba(99, 102, 241, 0.12);
  color: #818cf8;
}

.forecast-chart {
  margin-top: 0.5rem;
}

.forecast-chart-label {
  font-size: 0.75rem;
  color: var(--color-text-muted);
  margin-bottom: 0.375rem;
}

.forecast-bars-row {
  display: flex;
  align-items: flex-end;
  gap: 2px;
  height: 48px;
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
  transition: height 0.3s;
}

.forecast-bar.solar-bar {
  background: #fbbf24;
}

.forecast-bar.wind-bar {
  background: #818cf8;
}

.forecast-bar-label {
  position: absolute;
  bottom: -14px;
  font-size: 0.6rem;
  color: var(--color-text-muted);
  white-space: nowrap;
}

.balance-status-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.875rem;
}

.balance-status-badge {
  font-size: 0.8rem;
  font-weight: 600;
  padding: 0.2em 0.6em;
  border-radius: 6px;
}

.status-balanced {
  background: rgba(52, 211, 153, 0.15);
  color: #34d399;
}

.status-constrained {
  background: rgba(251, 191, 36, 0.15);
  color: #f59e0b;
}

.status-critical {
  background: rgba(239, 68, 68, 0.15);
  color: #ef4444;
}

.legacy-badge {
  font-size: 0.7rem;
  padding: 0.15em 0.5em;
  background: rgba(148, 163, 184, 0.15);
  color: var(--color-text-muted);
  border-radius: 4px;
}

.balance-metrics {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
  margin-bottom: 0.875rem;
}

.balance-metric {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 0.875rem;
}

.balance-metric-label {
  color: var(--color-text-muted);
}

.balance-metric-value {
  font-weight: 600;
}

.balance-metric-value.supply {
  color: #34d399;
}

.balance-metric-value.demand {
  color: #f87171;
}

.balance-metric-value.reserve-ok {
  color: #34d399;
}

.balance-metric-value.reserve-low {
  color: #ef4444;
}

.balance-guidance {
  font-size: 0.8rem;
  color: var(--color-text-secondary);
  line-height: 1.5;
}

.balance-loading {
  color: var(--color-text-muted);
  font-size: 0.875rem;
}

.why-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.why-item {
  font-size: 0.875rem;
  line-height: 1.5;
  padding-left: 0.25rem;
}

.why-item.solar-item {
  color: #f59e0b;
}

.why-item.wind-item {
  color: #818cf8;
}

.why-item.power-item {
  color: var(--color-text-secondary);
}
</style>

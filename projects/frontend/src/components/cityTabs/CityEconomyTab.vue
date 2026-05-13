<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import EconomyCycleWidget from '@/components/dashboard/EconomyCycleWidget.vue'
import CityPowerPlanningSection from '@/components/cityMap/CityPowerPlanningSection.vue'
import HealthIndicatorsPanel from '@/components/cityMap/HealthIndicatorsPanel.vue'
import type { City, EconomicCycleView, MarketEventView, EconomicCycleHistoryPoint, CityWeatherForecast, CityPowerBalance, CityEconomicReportResult } from '@/types'

const { t } = useI18n()

const props = defineProps<{
  city: City
  economyCycle: EconomicCycleView | null
  activeMarketEvents: MarketEventView[]
  economicHistory: EconomicCycleHistoryPoint[]
  cityWeather: CityWeatherForecast | null
  cityPowerBalance: CityPowerBalance | null
  cityEconomicReport: CityEconomicReportResult | null
  economicReportLoading: boolean
}>()

const cityHeading = computed(() => t('cityMap.tabs.economyHeading', { city: props.city.name }))
</script>

<template>
  <section class="city-economy-tab">
    <h2 class="section-heading">{{ cityHeading }}</h2>
    <p class="section-subtitle">{{ t('cityMap.tabs.economyDescription') }}</p>

    <EconomyCycleWidget :economic-cycle="economyCycle" :active-market-events="activeMarketEvents" :economic-history="economicHistory" />

    <CityPowerPlanningSection :city-weather="cityWeather" :city-power-balance="cityPowerBalance" />

    <section class="city-economic-health-section">
      <h3 class="section-heading">{{ t('cityHealth.panelTitle') }}</h3>
      <HealthIndicatorsPanel :data="cityEconomicReport" :loading="economicReportLoading" />
    </section>
  </section>
</template>

<style scoped>
.city-economy-tab {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.section-heading {
  margin: 0;
  font-size: 1.125rem;
  font-weight: 700;
}

.section-subtitle {
  margin: 0;
  color: var(--color-text-secondary);
}
</style>

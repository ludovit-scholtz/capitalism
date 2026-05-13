<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import CityMediaHousesSection from '@/components/cityMap/CityMediaHousesSection.vue'
import CityDemandPanel from '@/components/cityMap/CityDemandPanel.vue'
import type { City, CityMediaHouseInfo } from '@/types'

const { t } = useI18n()

defineProps<{
  city: City
  cityId: string
  cityMediaHouses: CityMediaHouseInfo[]
  mediaHousesLoading: boolean
}>()
</script>

<template>
  <section class="city-market-tab">
    <h2 class="section-heading">{{ t('cityMap.tabs.marketHeading') }}</h2>
    <p class="section-subtitle">{{ t('cityMap.tabs.marketDescription') }}</p>

    <CityDemandPanel :city-id="cityId" :currency-code="city.currencyCode ?? 'EUR'" :top-n="5" :last-n-ticks="100" />
    <CityMediaHousesSection :media-houses="cityMediaHouses" :loading="mediaHousesLoading" />
  </section>
</template>

<style scoped>
.city-market-tab {
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

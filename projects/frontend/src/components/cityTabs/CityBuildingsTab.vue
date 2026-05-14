<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import CityMapContent from '@/components/cityMap/CityMapContent.vue'
import type { City, BuildingLot, Company, PurchaseLotResult, CityWeatherForecast, NpcCompanySummary } from '@/types'

const { t } = useI18n()

const props = defineProps<{
  city: City
  lots: BuildingLot[]
  companies: Company[]
  npcCompanies: NpcCompanySummary[]
  cityWeather: CityWeatherForecast | null
  highlightedBuildingId: string | null
}>()

const emit = defineEmits<{
  (e: 'purchase-complete', result: PurchaseLotResult): void
  (e: 'lot-refreshed', lot: BuildingLot): void
}>()

const showAvailableOnly = ref(false)
const showResourceLayer = ref(false)
const viewMode = ref<'map' | 'list'>('map')

const filteredLots = computed(() => {
  if (showAvailableOnly.value) {
    return props.lots.filter((lot) => !lot.ownerCompanyId)
  }
  return props.lots
})

watch(
  () => props.city.id,
  () => {
    showAvailableOnly.value = false
    showResourceLayer.value = false
    viewMode.value = 'map'
  },
)
</script>

<template>
  <section class="city-buildings-tab">
    <div class="header-controls">
      <div class="view-toggle">
        <button class="toggle-btn" :class="{ active: viewMode === 'map' }" @click="viewMode = 'map'">🗺️ {{ t('cityMap.mapView') }}</button>
        <button class="toggle-btn" :class="{ active: viewMode === 'list' }" @click="viewMode = 'list'">📋 {{ t('cityMap.listView') }}</button>
      </div>
      <div class="filter-toggle">
        <button class="toggle-btn" :class="{ active: !showAvailableOnly }" @click="showAvailableOnly = false">{{ t('cityMap.filterAll') }}</button>
        <button class="toggle-btn" :class="{ active: showAvailableOnly }" @click="showAvailableOnly = true">{{ t('cityMap.filterAvailable') }}</button>
      </div>
      <button class="toggle-btn resource-layer-toggle" :class="{ active: showResourceLayer }" @click="showResourceLayer = !showResourceLayer">
        ⛏️ {{ t('cityMap.resourceLayer') }}
      </button>
      <span class="lot-count">{{ t('cityMap.lotCount', { count: filteredLots.length }) }}</span>
    </div>

    <CityMapContent
      :city="city"
      :filtered-lots="filteredLots"
      :lots="lots"
      :companies="companies"
      :npc-companies="npcCompanies"
      :view-mode="viewMode"
      :city-weather="cityWeather"
      :highlighted-building-id="highlightedBuildingId"
      :show-resource-layer="showResourceLayer"
      @purchase-complete="emit('purchase-complete', $event)"
      @lot-refreshed="emit('lot-refreshed', $event)"
    />
  </section>
</template>

<style scoped>
.city-buildings-tab {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.header-controls {
  display: flex;
  align-items: center;
  gap: 1rem;
  flex-wrap: wrap;
}

.view-toggle,
.filter-toggle {
  display: flex;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  overflow: hidden;
}

.toggle-btn {
  padding: 0.375rem 0.75rem;
  font-size: 0.8125rem;
  background: var(--color-bg);
  color: var(--color-text-secondary);
  border: none;
  cursor: pointer;
  transition: all 0.15s;
}

.toggle-btn.active {
  background: var(--color-primary);
  color: #fff;
}

.toggle-btn:not(:last-child) {
  border-right: 1px solid var(--color-border);
}

.lot-count {
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
}
</style>

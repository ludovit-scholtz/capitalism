<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { formatMoney } from '@/lib/currencyFormat'
import { getLotStatus as lotStatusFromOwnership, getLotMarkerColor as markerColorFromStatus } from '@/lib/cityMapHelpers'
import { getActiveCompany } from '@/lib/accountContext'
import CityLotDetailPanel from '@/components/cityMap/CityLotDetailPanel.vue'
import CityMediaHousesSection from '@/components/cityMap/CityMediaHousesSection.vue'
import CityPowerPlanningSection from '@/components/cityMap/CityPowerPlanningSection.vue'
import HealthIndicatorsPanel from '@/components/cityMap/HealthIndicatorsPanel.vue'
import type { City, BuildingLot, Company, PurchaseLotResult, CityMediaHouseInfo, CityWeatherForecast, CityPowerBalance, CityEconomicReportResult } from '@/types'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'

const { t, locale } = useI18n()
const auth = useAuthStore()

const props = defineProps<{
  city: City
  filteredLots: BuildingLot[]
  lots: BuildingLot[]
  companies: Company[]
  viewMode: 'map' | 'list'
  cityWeather: CityWeatherForecast | null
  cityPowerBalance: CityPowerBalance | null
  cityEconomicReport: CityEconomicReportResult | null
  economicReportLoading: boolean
  cityMediaHouses: CityMediaHouseInfo[]
  mediaHousesLoading: boolean
  highlightedBuildingId: string | null
  cityId: string
}>()

const emit = defineEmits<{
  (e: 'purchase-complete', result: PurchaseLotResult): void
  (e: 'lot-refreshed', lot: BuildingLot): void
}>()

// Internal state owned by this component
const selectedLot = ref<BuildingLot | null>(null)
const mapContainer = ref<HTMLDivElement | null>(null)
let map: L.Map | null = null
let markers: L.Marker[] = []

const activeCompany = computed(() => getActiveCompany(auth.player, props.companies))
const isCompanyAccountActive = computed(() => auth.player?.activeAccountType === 'COMPANY' && !!activeCompany.value)

function getLotStatus(lot: BuildingLot): 'available' | 'owned' | 'yours' {
  return lotStatusFromOwnership(
    lot.ownerCompanyId,
    props.companies.map((c) => c.id),
  )
}

function getLotMarkerColor(lot: BuildingLot): string {
  return markerColorFromStatus(getLotStatus(lot))
}

function formatCurrency(value: number): string {
  return formatMoney(value, props.city.currencyCode ?? 'EUR', locale.value)
}

function createMarkerIcon(color: string, isSelected: boolean): L.DivIcon {
  const size = isSelected ? 18 : 12
  const border = isSelected ? '3px solid #fff' : '2px solid rgba(255,255,255,0.8)'
  return L.divIcon({
    className: 'lot-marker',
    html: `<div style="
      width:${size}px;height:${size}px;
      background:${color};
      border-radius:50%;
      border:${border};
      box-shadow:0 2px 6px rgba(0,0,0,0.4);
    "></div>`,
    iconSize: [size + 6, size + 6],
    iconAnchor: [(size + 6) / 2, (size + 6) / 2],
  })
}

function initMap() {
  if (!mapContainer.value || !props.city) return

  map = L.map(mapContainer.value, {
    center: [props.city.latitude, props.city.longitude],
    zoom: 14,
    zoomControl: true,
  })

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; OpenStreetMap contributors',
    maxZoom: 19,
  }).addTo(map)

  updateMarkers()
}

function updateMarkers() {
  if (!map) return

  markers.forEach((m) => m.remove())
  markers = []

  for (const lot of props.filteredLots) {
    const color = getLotMarkerColor(lot)
    const isSelected = selectedLot.value?.id === lot.id
    const icon = createMarkerIcon(color, isSelected)

    const marker = L.marker([lot.latitude, lot.longitude], { icon }).addTo(map)

    marker.bindTooltip(lot.name, {
      direction: 'top',
      offset: [0, -10],
    })

    marker.on('click', () => {
      selectLot(lot)
    })

    markers.push(marker)
  }

  if (props.filteredLots.length > 0) {
    const bounds = L.latLngBounds(props.filteredLots.map((lot) => [lot.latitude, lot.longitude] as [number, number]))
    map.fitBounds(bounds.pad(0.15))
  }
}

function selectLot(lot: BuildingLot) {
  selectedLot.value = lot
  updateMarkers()

  if (map) {
    map.panTo([lot.latitude, lot.longitude])
  }
}

function selectRequestedBuildingLot() {
  const buildingId = props.highlightedBuildingId
  if (!buildingId) return

  const matchingLot = props.lots.find((lot) => lot.buildingId === buildingId)
  if (!matchingLot) return

  if (selectedLot.value?.id === matchingLot.id) {
    if (map) {
      map.panTo([matchingLot.latitude, matchingLot.longitude])
    }
    return
  }

  selectLot(matchingLot)
}

function handlePurchaseCompleteInternal(result: PurchaseLotResult) {
  selectedLot.value = result.lot
  updateMarkers()
  emit('purchase-complete', result)
}

function handleLotRefreshedInternal(lot: BuildingLot) {
  selectedLot.value = lot
  updateMarkers()
  emit('lot-refreshed', lot)
}

watch(
  () => props.filteredLots,
  () => {
    if (map) {
      updateMarkers()
    }
  },
)

watch(
  () => [props.highlightedBuildingId, props.lots.map((lot) => `${lot.id}:${lot.buildingId ?? ''}`).join('|')],
  () => {
    if (props.highlightedBuildingId) {
      selectRequestedBuildingLot()
    }
  },
)

watch(
  () => props.viewMode,
  async (mode) => {
    if (mode === 'map') {
      await nextTick()
      if (!map) {
        initMap()
      } else {
        map.invalidateSize()
      }
    }
  },
)

onMounted(async () => {
  await nextTick()
  if (props.viewMode === 'map') {
    initMap()
  }
  selectRequestedBuildingLot()
})

onUnmounted(() => {
  if (map) {
    map.remove()
    map = null
  }
})
</script>

<template>
  <div class="city-content" :class="{ 'has-selection': !!selectedLot }">
    <div class="map-area">
      <div v-show="viewMode === 'map'" ref="mapContainer" class="map-container"></div>
      <div v-show="viewMode === 'list'" class="lot-list">
        <button
          v-for="lot in filteredLots"
          :key="lot.id"
          class="lot-list-item"
          :class="{ selected: selectedLot?.id === lot.id, available: getLotStatus(lot) === 'available', owned: getLotStatus(lot) === 'owned', yours: getLotStatus(lot) === 'yours' }"
          @click="selectLot(lot)"
        >
          <div class="lot-status-dot" :style="{ background: getLotMarkerColor(lot) }"></div>
          <div class="lot-list-info">
            <span class="lot-list-name">{{ lot.name }}</span>
            <span class="lot-list-district">{{ lot.district }}</span>
            <span v-if="lot.resourceType" class="lot-list-resource-badge" data-testid="lot-resource-badge">⛏ {{ lot.resourceType.name }}</span>
            <span v-if="lot.building?.isForSale" class="lot-for-sale-badge" data-testid="lot-for-sale-badge">🏪 {{ t('buildingMarket.forSaleBadge') }}</span>
          </div>
          <div class="lot-list-meta">
            <span class="lot-list-price">{{ formatCurrency(lot.price) }}</span>
            <span class="lot-list-status" :class="getLotStatus(lot)">
              {{ getLotStatus(lot) === 'available' ? t('cityMap.available') : getLotStatus(lot) === 'yours' ? t('cityMap.yourProperty') : t('cityMap.owned') }}
            </span>
          </div>
        </button>
        <div v-if="filteredLots.length === 0" class="empty-state">{{ t('cityMap.noLotsAvailable') }}</div>
      </div>
    </div>

    <CityLotDetailPanel
      v-if="selectedLot"
      :lot="selectedLot"
      :city="city"
      :city-weather="cityWeather"
      :is-authenticated="auth.isAuthenticated"
      :companies="companies"
      :is-company-account-active="isCompanyAccountActive"
      :active-company="activeCompany"
      @purchase-complete="handlePurchaseCompleteInternal"
      @lot-refreshed="handleLotRefreshedInternal"
    />

    <aside v-else class="detail-panel empty-panel">
      <p class="select-prompt">{{ t('cityMap.selectLot') }}</p>
    </aside>
  </div>

  <CityMediaHousesSection :media-houses="cityMediaHouses" :loading="mediaHousesLoading" />
  <CityPowerPlanningSection :city-weather="cityWeather" :city-power-balance="cityPowerBalance" />
  <section class="city-economic-health-section">
    <h2 class="section-heading">{{ t('cityHealth.panelTitle') }}</h2>
    <HealthIndicatorsPanel :data="cityEconomicReport" :loading="economicReportLoading" />
  </section>
</template>

<style scoped>
.city-content {
  display: grid;
  grid-template-columns: 1fr 380px;
  gap: 1.5rem;
  min-height: 500px;
}

.map-area {
  min-height: 500px;
  border-radius: var(--radius-lg);
  overflow: hidden;
  border: 1px solid var(--color-border);
}

.map-container {
  width: 100%;
  height: 100%;
  min-height: 500px;
}

.lot-list {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  padding: 0.5rem;
  max-height: 600px;
  overflow-y: auto;
}

.lot-list-item {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem;
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  cursor: pointer;
  transition: all 0.15s;
  text-align: left;
  width: 100%;
  color: var(--color-text);
}

.lot-list-item:hover {
  border-color: var(--color-primary);
}

.lot-list-item.selected {
  border-color: var(--color-primary);
  background: rgba(0, 71, 255, 0.06);
}

.lot-status-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  flex-shrink: 0;
}

.lot-list-info {
  flex: 1;
  min-width: 0;
}

.lot-list-name {
  display: block;
  font-weight: 600;
  font-size: 0.875rem;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.lot-list-district {
  display: block;
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.lot-list-resource-badge {
  display: inline-block;
  margin-top: 0.25rem;
  font-size: 0.7rem;
  font-weight: 600;
  color: var(--color-primary);
  background: rgba(139, 92, 246, 0.1);
  border: 1px solid rgba(139, 92, 246, 0.25);
  border-radius: 999px;
  padding: 0.05rem 0.4rem;
}

.lot-for-sale-badge {
  display: inline-block;
  margin-top: 0.25rem;
  font-size: 0.7rem;
  font-weight: 600;
  color: #10b981;
  background: rgba(16, 185, 129, 0.1);
  border: 1px solid rgba(16, 185, 129, 0.3);
  border-radius: 999px;
  padding: 0.05rem 0.4rem;
}

.lot-list-meta {
  text-align: right;
  flex-shrink: 0;
}

.lot-list-price {
  display: block;
  font-weight: 600;
  font-size: 0.875rem;
  color: var(--color-secondary);
}

.lot-list-status {
  display: block;
  font-size: 0.6875rem;
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.lot-list-status.available {
  color: var(--color-secondary);
}

.lot-list-status.owned {
  color: var(--color-text-secondary);
}

.lot-list-status.yours {
  color: var(--color-primary);
}

.detail-panel {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: 1.5rem;
  align-self: start;
  position: sticky;
  top: 80px;
}

.empty-panel {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 200px;
}

.select-prompt {
  color: var(--color-text-secondary);
  font-size: 0.875rem;
  text-align: center;
}

.empty-state {
  padding: 1rem;
  color: var(--color-text-secondary);
  text-align: center;
}

@media (max-width: 1024px) {
  .city-content {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 768px) {
  .city-content {
    min-height: calc(100vh - 9.5rem);
  }

  .city-content.has-selection {
    padding-bottom: min(68vh, 42rem);
  }

  .map-area,
  .map-container {
    min-height: calc(100vh - 14rem);
  }
}
</style>

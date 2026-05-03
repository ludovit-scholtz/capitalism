<template>
  <div class="city-map-view container">
    <div class="page-header">
      <div>
        <button class="btn btn-secondary btn-sm" @click="router.push('/dashboard')">← {{ t('cityMap.backToDashboard') }}</button>
        <h1 v-if="city">🗺️ {{ city.name }} — {{ t('cityMap.title') }}</h1>
        <p class="subtitle">{{ t('cityMap.subtitle') }}</p>
      </div>
      <div class="header-controls">
        <div class="view-toggle">
          <button class="toggle-btn" :class="{ active: viewMode === 'map' }" @click="viewMode = 'map'">🗺️ {{ t('cityMap.mapView') }}</button>
          <button class="toggle-btn" :class="{ active: viewMode === 'list' }" @click="viewMode = 'list'">📋 {{ t('cityMap.listView') }}</button>
        </div>
        <div class="filter-toggle">
          <button class="toggle-btn" :class="{ active: !showAvailableOnly }" @click="showAvailableOnly = false">{{ t('cityMap.filterAll') }}</button>
          <button class="toggle-btn" :class="{ active: showAvailableOnly }" @click="showAvailableOnly = true">{{ t('cityMap.filterAvailable') }}</button>
        </div>
        <span class="lot-count">{{ t('cityMap.lotCount', { count: filteredLots.length }) }}</span>
      </div>
    </div>

    <div v-if="loading" class="loading">{{ t('common.loading') }}</div>

    <div v-else-if="error" class="error-message" role="alert">
      {{ error }}
      <button class="btn btn-secondary" @click="fetchData()">{{ t('common.tryAgain') }}</button>
    </div>

    <template v-else-if="city">
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
          @purchase-complete="handlePurchaseComplete"
          @lot-refreshed="handleLotRefreshed"
        />

        <aside v-else class="detail-panel empty-panel">
          <p class="select-prompt">{{ t('cityMap.selectLot') }}</p>
        </aside>
      </div>

      <CityMediaHousesSection :media-houses="cityMediaHouses" :loading="mediaHousesLoading" />
      <CityPowerPlanningSection :city-weather="cityWeather" :city-power-balance="cityPowerBalance" />
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { formatMoney } from '@/lib/currencyFormat'
import { getLotStatus as lotStatusFromOwnership, getLotMarkerColor as markerColorFromStatus } from '@/lib/cityMapHelpers'
import { getActiveCompany } from '@/lib/accountContext'
import CityLotDetailPanel from '@/components/cityMap/CityLotDetailPanel.vue'
import CityMediaHousesSection from '@/components/cityMap/CityMediaHousesSection.vue'
import CityPowerPlanningSection from '@/components/cityMap/CityPowerPlanningSection.vue'
import type { City, BuildingLot, Company, PurchaseLotResult, CityMediaHouseInfo, CityWeatherForecast, CityPowerBalance } from '@/types'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'

const { t, locale } = useI18n()
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const { selectedCityId } = storeToRefs(auth)

const cityId = computed(() => route.params.id as string)
const highlightedBuildingId = computed(() => (typeof route.query.building === 'string' ? route.query.building : null))

const loading = ref(true)
const error = ref<string | null>(null)
const city = ref<City | null>(null)
const lots = ref<BuildingLot[]>([])
const companies = ref<Company[]>([])
const selectedLot = ref<BuildingLot | null>(null)
const showAvailableOnly = ref(false)
const viewMode = ref<'map' | 'list'>('map')

const cityWeather = ref<CityWeatherForecast | null>(null)
const cityPowerBalance = ref<CityPowerBalance | null>(null)

const mapContainer = ref<HTMLDivElement | null>(null)
let map: L.Map | null = null
let markers: L.Marker[] = []

const cityMediaHouses = ref<CityMediaHouseInfo[]>([])
const mediaHousesLoading = ref(false)

const filteredLots = computed(() => {
  if (showAvailableOnly.value) {
    return lots.value.filter((lot) => !lot.ownerCompanyId)
  }
  return lots.value
})

const activeCompany = computed(() => getActiveCompany(auth.player, companies.value))
const isCompanyAccountActive = computed(() => auth.player?.activeAccountType === 'COMPANY' && !!activeCompany.value)

function getLotStatus(lot: BuildingLot): 'available' | 'owned' | 'yours' {
  return lotStatusFromOwnership(
    lot.ownerCompanyId,
    companies.value.map((c) => c.id),
  )
}

function getLotMarkerColor(lot: BuildingLot): string {
  return markerColorFromStatus(getLotStatus(lot))
}

function formatCurrency(value: number): string {
  return formatMoney(value, city.value?.currencyCode ?? 'EUR', locale.value)
}

async function fetchData() {
  loading.value = true
  error.value = null
  try {
    if (auth.isAuthenticated && !auth.player) {
      await auth.fetchMe()
    }

    const [cityData, lotsData, companiesData] = await Promise.all([
      gqlRequest<{ city: City }>(
        `query GetCity($id: UUID!) {
          city(id: $id) {
            id name countryCode latitude longitude population currencyCode
            resources { resourceType { id name slug category } abundance }
          }
        }`,
        { id: cityId.value },
      ),
      gqlRequest<{ cityLots: BuildingLot[] }>(
        `query CityLots($cityId: UUID!) {
          cityLots(cityId: $cityId) {
            id cityId name description district latitude longitude
            populationIndex basePrice price suitableTypes
            ownerCompanyId buildingId
            ownerCompany { id name }
            building { id name type isUnderConstruction constructionCompletesAtTick constructionCost isForSale askingPrice }
            resourceType { id name slug }
            materialQuality materialQuantity
          }
        }`,
        { cityId: cityId.value },
      ),
      auth.isAuthenticated ? gqlRequest<{ myCompanies: Company[] }>(`{ myCompanies { id name cash foundedAtUtc buildings { id } } }`) : Promise.resolve({ myCompanies: [] as Company[] }),
    ])

    city.value = cityData.city
    lots.value = lotsData.cityLots
    companies.value = companiesData.myCompanies
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load city data'
  } finally {
    loading.value = false
  }
}

async function fetchMediaHouses() {
  if (!cityId.value) return
  mediaHousesLoading.value = true
  try {
    const data = await gqlRequest<{ cityMediaHouses: CityMediaHouseInfo[] }>(
      `query CityMediaHouses($cityId: UUID!) {
        cityMediaHouses(cityId: $cityId) {
          id name cityName mediaType effectivenessMultiplier ownerCompanyName
          powerStatus isUnderConstruction contentRanking contentValue contentBudgetPerTick isGovernmentOwned
        }
      }`,
      { cityId: cityId.value },
    )
    cityMediaHouses.value = data.cityMediaHouses ?? []
  } catch {
    cityMediaHouses.value = []
  } finally {
    mediaHousesLoading.value = false
  }
}

async function fetchWeatherForecast() {
  if (!cityId.value) return
  try {
    const data = await gqlRequest<{ cityWeatherForecast: CityWeatherForecast | null }>(
      `query CityWeatherForecast($cityId: UUID!) {
        cityWeatherForecast(cityId: $cityId) {
          cityId currentWindPercent currentSolarPercent
          forecast { tick windPercent solarPercent }
        }
      }`,
      { cityId: cityId.value },
    )
    cityWeather.value = data.cityWeatherForecast ?? null
  } catch {
    cityWeather.value = null
  }
}

async function fetchCityPowerBalance() {
  if (!cityId.value) return
  try {
    const data = await gqlRequest<{ cityPowerBalance: CityPowerBalance }>(
      `query CityPowerBalance($cityId: UUID!) {
        cityPowerBalance(cityId: $cityId) {
          cityId totalSupplyMw totalDemandMw reserveMw reservePercent status
          powerPlantCount consumerBuildingCount
        }
      }`,
      { cityId: cityId.value },
    )
    cityPowerBalance.value = data.cityPowerBalance ?? null
  } catch {
    cityPowerBalance.value = null
  }
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
  if (!mapContainer.value || !city.value) return

  map = L.map(mapContainer.value, {
    center: [city.value.latitude, city.value.longitude],
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

  for (const lot of filteredLots.value) {
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

  if (filteredLots.value.length > 0) {
    const bounds = L.latLngBounds(filteredLots.value.map((lot) => [lot.latitude, lot.longitude] as [number, number]))
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
  const buildingId = highlightedBuildingId.value
  if (!buildingId) return

  const matchingLot = lots.value.find((lot) => lot.buildingId === buildingId)
  if (!matchingLot) return

  if (selectedLot.value?.id === matchingLot.id) {
    if (map) {
      map.panTo([matchingLot.latitude, matchingLot.longitude])
    }
    return
  }

  selectLot(matchingLot)
}

function handlePurchaseComplete(result: PurchaseLotResult) {
  const lotIdx = lots.value.findIndex((lot) => lot.id === result.lot.id)
  if (lotIdx >= 0) {
    lots.value[lotIdx] = result.lot
  }
  selectedLot.value = result.lot

  const companyIdx = companies.value.findIndex((company) => company.id === result.company.id)
  if (companyIdx >= 0) {
    companies.value[companyIdx]!.cash = result.company.cash
  }

  updateMarkers()
}

function handleLotRefreshed(lot: BuildingLot) {
  const lotIdx = lots.value.findIndex((item) => item.id === lot.id)
  if (lotIdx >= 0) {
    lots.value[lotIdx] = lot
  }
  selectedLot.value = lot
  updateMarkers()
}

watch(filteredLots, () => {
  if (map) {
    updateMarkers()
  }
})

watch(
  () => [highlightedBuildingId.value, lots.value.map((lot) => `${lot.id}:${lot.buildingId ?? ''}`).join('|')],
  () => {
    if (highlightedBuildingId.value) {
      selectRequestedBuildingLot()
    }
  },
)

watch(selectedCityId, (nextCityId) => {
  if (!nextCityId || nextCityId === cityId.value) {
    return
  }
  router.push({ name: 'city-map', params: { id: nextCityId } })
})

watch(cityId, async () => {
  if (selectedCityId.value !== cityId.value) {
    auth.switchCity(cityId.value)
  }

  selectedLot.value = null
  cityWeather.value = null
  cityPowerBalance.value = null
  viewMode.value = 'map'

  if (map) {
    map.remove()
    map = null
  }

  await fetchData()
  void fetchMediaHouses()
  void fetchWeatherForecast()
  void fetchCityPowerBalance()

  if (!error.value) {
    await nextTick()
    initMap()
  }
})

onMounted(async () => {
  if (selectedCityId.value !== cityId.value) {
    auth.switchCity(cityId.value)
  }

  await fetchData()
  void fetchMediaHouses()
  void fetchWeatherForecast()
  void fetchCityPowerBalance()

  await nextTick()
  if (viewMode.value === 'map') {
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

watch(viewMode, async (mode) => {
  if (mode === 'map') {
    await nextTick()
    if (!map) {
      initMap()
    } else {
      map.invalidateSize()
    }
  }
})
</script>

<style scoped>
.city-map-view {
  padding: 1.5rem 1rem;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
  margin-bottom: 1.5rem;
  flex-wrap: wrap;
}

.page-header h1 {
  font-size: 1.5rem;
  margin: 0.5rem 0 0.25rem;
}

.subtitle {
  color: var(--color-text-secondary);
  font-size: 0.875rem;
  margin: 0;
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

.loading {
  padding: 2rem;
  text-align: center;
  color: var(--color-text-secondary);
}

.error-message {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
  margin-bottom: 1rem;
}

@media (max-width: 1024px) {
  .city-content {
    grid-template-columns: 1fr;
  }

  .detail-panel {
    position: static;
  }
}
</style>

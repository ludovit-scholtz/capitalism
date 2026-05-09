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
        <button class="toggle-btn resource-layer-toggle" :class="{ active: showResourceLayer }" @click="showResourceLayer = !showResourceLayer">
          ⛏️ {{ t('cityMap.resourceLayer') }}
        </button>
        <span class="lot-count">{{ t('cityMap.lotCount', { count: filteredLots.length }) }}</span>
      </div>
    </div>

    <div v-if="loading" class="loading">{{ t('common.loading') }}</div>

    <div v-else-if="error" class="error-message" role="alert">
      {{ error }}
      <button class="btn btn-secondary" @click="fetchData()">{{ t('common.tryAgain') }}</button>
    </div>

    <CityMapContent
      v-else-if="city"
      :city="city"
      :filtered-lots="filteredLots"
      :lots="lots"
      :companies="companies"
      :view-mode="viewMode"
      :city-weather="cityWeather"
      :city-power-balance="cityPowerBalance"
      :city-economic-report="cityEconomicReport"
      :economic-report-loading="economicReportLoading"
      :city-media-houses="cityMediaHouses"
      :media-houses-loading="mediaHousesLoading"
      :highlighted-building-id="highlightedBuildingId"
      :city-id="cityId"
      :show-resource-layer="showResourceLayer"
      @purchase-complete="handlePurchaseComplete"
      @lot-refreshed="handleLotRefreshed"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, nextTick, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import CityMapContent from '@/components/cityMap/CityMapContent.vue'
import type { City, BuildingLot, Company, PurchaseLotResult, CityMediaHouseInfo, CityWeatherForecast, CityPowerBalance, CityEconomicReportResult } from '@/types'

const { t } = useI18n()
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
const showAvailableOnly = ref(false)
const showResourceLayer = ref(false)
const viewMode = ref<'map' | 'list'>('map')

const cityWeather = ref<CityWeatherForecast | null>(null)
const cityPowerBalance = ref<CityPowerBalance | null>(null)
const cityEconomicReport = ref<CityEconomicReportResult | null>(null)
const economicReportLoading = ref(false)

const cityMediaHouses = ref<CityMediaHouseInfo[]>([])
const mediaHousesLoading = ref(false)

const filteredLots = computed(() => {
  if (showAvailableOnly.value) {
    return lots.value.filter((lot) => !lot.ownerCompanyId)
  }
  return lots.value
})

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
             building { id name type isUnderConstruction constructionCompletesAtTick constructionCost isForSale askingPrice destroyedAtUtc }
             resourceType { id name slug }
             materialQuality materialQuantity originalMaterialQuantity
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

async function fetchCityEconomicReport() {
  if (!cityId.value) return
  economicReportLoading.value = true
  try {
    const data = await gqlRequest<{ getCityEconomicReport: CityEconomicReportResult }>(
      `query GetCityEconomicReport($cityId: UUID!) {
        getCityEconomicReport(cityId: $cityId) {
          latest {
            id cityId taxCycleEnd totalSalaries totalPublicRevenue
            activeCompanies totalPowerConsumption totalPowerSupply
            averageProductQuality economicIndex computedAtUtc
          }
          history {
            id cityId taxCycleEnd totalSalaries totalPublicRevenue
            activeCompanies totalPowerConsumption totalPowerSupply
            averageProductQuality economicIndex computedAtUtc
          }
        }
      }`,
      { cityId: cityId.value },
    )
    cityEconomicReport.value = data.getCityEconomicReport ?? null
  } catch {
    cityEconomicReport.value = null
  } finally {
    economicReportLoading.value = false
  }
}

function handlePurchaseComplete(result: PurchaseLotResult) {
  const lotIdx = lots.value.findIndex((lot) => lot.id === result.lot.id)
  if (lotIdx >= 0) {
    lots.value[lotIdx] = result.lot
  }

  const companyIdx = companies.value.findIndex((company) => company.id === result.company.id)
  if (companyIdx >= 0) {
    companies.value[companyIdx]!.cash = result.company.cash
  }
}

function handleLotRefreshed(lot: BuildingLot) {
  const lotIdx = lots.value.findIndex((item) => item.id === lot.id)
  if (lotIdx >= 0) {
    lots.value[lotIdx] = lot
  }
}

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

  cityWeather.value = null
  cityPowerBalance.value = null
  viewMode.value = 'map'

  await fetchData()
  void fetchMediaHouses()
  void fetchWeatherForecast()
  void fetchCityPowerBalance()
  void fetchCityEconomicReport()
})

onMounted(async () => {
  if (selectedCityId.value !== cityId.value) {
    auth.switchCity(cityId.value)
  }

  await fetchData()
  void fetchMediaHouses()
  void fetchWeatherForecast()
  void fetchCityPowerBalance()
  void fetchCityEconomicReport()

  await nextTick()
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

.loading {
  text-align: center;
  padding: 3rem 1rem;
  color: var(--color-text-secondary);
}

.error-message {
  background: rgba(239, 68, 68, 0.1);
  color: var(--color-danger);
  padding: 1rem;
  border-radius: var(--radius-md);
  display: flex;
  gap: 1rem;
  align-items: center;
}
</style>

<template src="./CityMapView.template.html"></template>

<script setup lang="ts">
/* oxlint-disable no-unused-vars */
/* eslint-disable @typescript-eslint/no-unused-vars */
// Split-file SFC: script symbols are consumed by CityMapView.template.html.
 
 
import { ref, computed, onMounted, onUnmounted, nextTick, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { useGameStateStore } from '@/stores/gameState'
import { gqlRequest, GraphQLError } from '@/lib/graphql'
import { formatTickDuration } from '@/lib/gameTime'
import { formatMoney } from '@/lib/currencyFormat'
import {
  getLotStatus as lotStatusFromOwnership,
  getLotMarkerColor as markerColorFromStatus,
  formatPopulationIndex,
  populationIndexClass,
  canPurchaseLot as isPurchasable,
  canSubmitPurchaseForm as isFormSubmittable,
  constructionCostForType,
  constructionTicksForType,
  constructionTicksRemaining as computeConstructionTicksRemaining,
} from '@/lib/cityMapHelpers'
import { getActiveCompany } from '@/lib/accountContext'
import type { City, BuildingLot, Company, PurchaseLotResult, CityMediaHouseInfo, CityWeatherForecast, CityPowerBalance } from '@/types'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'

const { t, locale } = useI18n()
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const { selectedCityId } = storeToRefs(auth)
const gameStateStore = useGameStateStore()

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

// Purchase form state
const purchaseMode = ref(false)
const selectedBuildingType = ref('')
const selectedPowerPlantType = ref('')
const buildingName = ref('')
const selectedMediaType = ref('')
const purchasing = ref(false)
const purchaseError = ref<string | null>(null)
const purchaseSuccess = ref<string | null>(null)
const justPurchasedBuildingId = ref<string | null>(null)
const justPurchasedBuildingType = ref<string | null>(null)
const justPurchasedIsUnderConstruction = ref(false)
const justPurchasedConstructionCompletesAtTick = ref<number | null>(null)

// Weather forecast and power balance for the current city
const cityWeather = ref<CityWeatherForecast | null>(null)
const cityPowerBalance = ref<CityPowerBalance | null>(null)

// Power plant type options with MW output
const POWER_PLANT_TYPES = [
  { type: 'COAL',    labelKey: 'powerGrid.plantTypes.COAL',    mw: 50,  descKey: 'powerPlant.coalDescription' },
  { type: 'GAS',     labelKey: 'powerGrid.plantTypes.GAS',     mw: 40,  descKey: 'powerPlant.gasDescription' },
  { type: 'SOLAR',   labelKey: 'powerGrid.plantTypes.SOLAR',   mw: 20,  descKey: 'powerPlant.solarDescription' },
  { type: 'WIND',    labelKey: 'powerGrid.plantTypes.WIND',    mw: 25,  descKey: 'powerPlant.windDescription' },
  { type: 'NUCLEAR', labelKey: 'powerGrid.plantTypes.NUCLEAR', mw: 200, descKey: 'powerPlant.nuclearDescription' },
]

// Map reference
const mapContainer = ref<HTMLDivElement | null>(null)
let map: L.Map | null = null

// City media houses
const cityMediaHouses = ref<CityMediaHouseInfo[]>([])
const mediaHousesLoading = ref(false)
let markers: L.Marker[] = []

const filteredLots = computed(() => {
  if (showAvailableOnly.value) {
    return lots.value.filter((lot) => !lot.ownerCompanyId)
  }
  return lots.value
})

const suitableTypesForLot = computed(() => {
  if (!selectedLot.value) return []
  return selectedLot.value.suitableTypes.split(',').map((s) => s.trim())
})

const isOwnedByPlayer = computed(() => {
  if (!selectedLot.value) return false
  return companies.value.some((c) => c.id === selectedLot.value?.ownerCompanyId)
})

const activeCompany = computed(() => getActiveCompany(auth.player, companies.value))
const isCompanyAccountActive = computed(() => auth.player?.activeAccountType === 'COMPANY' && !!activeCompany.value)
const isOwnedByActiveCompany = computed(() => !!selectedLot.value?.ownerCompanyId && selectedLot.value.ownerCompanyId === activeCompany.value?.id)
const isOwnedByDifferentControlledCompany = computed(() => isOwnedByPlayer.value && !!selectedLot.value?.ownerCompanyId && selectedLot.value.ownerCompanyId !== activeCompany.value?.id)

const canPurchase = computed(() => (selectedLot.value ? isCompanyAccountActive.value && isPurchasable(auth.isAuthenticated, companies.value.length, selectedLot.value.ownerCompanyId) : false))

const canSubmitPurchase = computed(() => {
  const baseValid = isFormSubmittable(selectedBuildingType.value, buildingName.value, activeCompany.value?.id ?? '', purchasing.value)
  // Media houses require a channel type selection.
  if (selectedBuildingType.value === 'MEDIA_HOUSE' && !selectedMediaType.value) return false
  // Power plants require a plant type selection.
  if (selectedBuildingType.value === 'POWER_PLANT' && !selectedPowerPlantType.value) return false
  return baseValid
})

const selectedCompany = computed(() => activeCompany.value)

const cashAfterPurchase = computed(() => {
  if (!selectedCompany.value || !selectedLot.value) return null
  const constructionCost = selectedBuildingType.value ? constructionCostForType(selectedBuildingType.value) : 0
  return selectedCompany.value.cash - selectedLot.value.price - constructionCost
})

/** Returns remaining construction ticks for the current building, using the live tick from the game state store. */
function constructionTicksRemaining(completesAtTick: number | null): number {
  const currentTick = gameStateStore.gameState?.currentTick ?? 0
  return computeConstructionTicksRemaining(completesAtTick, currentTick)
}

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

function formatBuildingType(type: string): string {
  const key = `buildings.types.${type}`
  const translated = t(key)
  if (translated !== key) return translated
  return type.replace(/_/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase())
}

function populationIndexLabel(value: number): string {
  if (value >= 1.8) return t('cityMap.populationIndexVeryHigh')
  if (value >= 1.3) return t('cityMap.populationIndexHigh')
  if (value >= 0.9) return t('cityMap.populationIndexMedium')
  return t('cityMap.populationIndexLow')
}

/**
 * Returns a short strategic recommendation label for the lot based on its
 * population index and resource data. This implements the ROADMAP requirement:
 * "include a simple recommendation label such as 'strong for retail demand,'
 * 'balanced starter location,' or 'resource-oriented.'"
 */
function strategicRecommendation(lot: BuildingLot): { key: string; cssClass: string } {
  const suitable = lot.suitableTypes.split(',').map((s) => s.trim())
  const hasMine = suitable.includes('MINE')
  const hasRetail = suitable.includes('SALES_SHOP')
  const hasFactory = suitable.includes('FACTORY')

  if (hasMine && lot.resourceType) {
    return { key: 'recommendationResourceOriented', cssClass: 'rec-resource' }
  }
  if (hasRetail && lot.populationIndex >= 1.3) {
    return { key: 'recommendationStrongRetail', cssClass: 'rec-retail' }
  }
  if (hasFactory && lot.populationIndex < 0.9) {
    return { key: 'recommendationIndustrialEfficiency', cssClass: 'rec-industrial' }
  }
  return { key: 'recommendationBalancedStarter', cssClass: 'rec-balanced' }
}

function materialQualityLabel(quality: number): string {
  if (quality >= 0.8) return t('cityMap.rawMaterialQualityExcellent')
  if (quality >= 0.6) return t('cityMap.rawMaterialQualityGood')
  if (quality >= 0.4) return t('cityMap.rawMaterialQualityFair')
  return t('cityMap.rawMaterialQualityPoor')
}

function materialQualityClass(quality: number): string {
  if (quality >= 0.8) return 'quality-excellent'
  if (quality >= 0.6) return 'quality-good'
  if (quality >= 0.4) return 'quality-fair'
  return 'quality-poor'
}

function placementGuidanceKey(buildingType: string): string {
  const map: Record<string, string> = {
    SALES_SHOP: 'placementGuidanceSalesShop',
    COMMERCIAL: 'placementGuidanceCommercial',
    FACTORY: 'placementGuidanceFactory',
    MINE: 'placementGuidanceMine',
    APARTMENT: 'placementGuidanceApartment',
    RESEARCH_DEVELOPMENT: 'placementGuidanceResearchDevelopment',
    POWER_PLANT: 'placementGuidancePowerPlant',
    BANK: 'placementGuidanceBank',
    EXCHANGE: 'placementGuidanceExchange',
    MEDIA_HOUSE: 'placementGuidanceMediaHouse',
  }
  return map[buildingType] ?? 'placementGuidanceGeneric'
}

function postPurchaseBodyKey(buildingType: string): string {
  const map: Record<string, string> = {
    FACTORY: 'postPurchaseBodyFactory',
    MINE: 'postPurchaseBodyMine',
    SALES_SHOP: 'postPurchaseBodySalesShop',
    RESEARCH_DEVELOPMENT: 'postPurchaseBodyResearchDevelopment',
    APARTMENT: 'postPurchaseBodyApartment',
    COMMERCIAL: 'postPurchaseBodyCommercial',
    MEDIA_HOUSE: 'postPurchaseBodyMediaHouse',
    BANK: 'postPurchaseBodyBank',
    EXCHANGE: 'postPurchaseBodyExchange',
    POWER_PLANT: 'postPurchaseBodyPowerPlant',
  }
  return map[buildingType] ?? 'postPurchaseBody'
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
            id name countryCode latitude longitude population
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
            building { id name type isUnderConstruction constructionCompletesAtTick constructionCost }
            resourceType { id name slug }
            materialQuality materialQuantity
          }
        }`,
        { cityId: cityId.value },
      ),
      auth.isAuthenticated
        ? gqlRequest<{ myCompanies: Company[] }>(`{ myCompanies { id name cash foundedAtUtc buildings { id } } }`)
        : Promise.resolve({ myCompanies: [] as Company[] }),
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

  // Clear existing markers
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

  // Fit bounds if we have lots
  if (filteredLots.value.length > 0) {
    const bounds = L.latLngBounds(filteredLots.value.map((lot) => [lot.latitude, lot.longitude] as [number, number]))
    map.fitBounds(bounds.pad(0.15))
  }
}

function selectLot(lot: BuildingLot) {
  selectedLot.value = lot
  purchaseMode.value = false
  purchaseError.value = null
  purchaseSuccess.value = null
  justPurchasedBuildingId.value = null
  justPurchasedBuildingType.value = null
  justPurchasedIsUnderConstruction.value = false
  justPurchasedConstructionCompletesAtTick.value = null
  selectedBuildingType.value = ''
  buildingName.value = ''
  selectedMediaType.value = ''
  selectedPowerPlantType.value = ''

  // Update markers to show selection
  updateMarkers()

  // Pan map to selected lot
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

function startPurchase() {
  purchaseMode.value = true
  purchaseError.value = null
  purchaseSuccess.value = null
}

async function confirmPurchase() {
  if (!selectedLot.value || !canSubmitPurchase.value || !activeCompany.value) return

  purchasing.value = true
  purchaseError.value = null

  try {
    const data = await gqlRequest<{ purchaseLot: PurchaseLotResult }>(
      `mutation PurchaseLot($input: PurchaseLotInput!) {
        purchaseLot(input: $input) {
          lot {
            id cityId name description district latitude longitude price suitableTypes
            ownerCompanyId buildingId
            ownerCompany { id name }
            building { id name type isUnderConstruction constructionCompletesAtTick constructionCost }
          }
          building { id name type isUnderConstruction constructionCompletesAtTick constructionCost }
          company { id name cash }
        }
      }`,
      {
        input: {
          companyId: activeCompany.value.id,
          lotId: selectedLot.value.id,
          buildingType: selectedBuildingType.value,
          buildingName: buildingName.value.trim() || null,
          mediaType: selectedBuildingType.value === 'MEDIA_HOUSE' ? selectedMediaType.value || null : null,
          powerPlantType: selectedBuildingType.value === 'POWER_PLANT' ? selectedPowerPlantType.value || null : null,
        },
      },
    )

    // Update the lot in our local state
    const idx = lots.value.findIndex((l) => l.id === data.purchaseLot.lot.id)
    if (idx >= 0) {
      lots.value[idx] = data.purchaseLot.lot
    }
    selectedLot.value = data.purchaseLot.lot

    // Update company cash
    const companyIdx = companies.value.findIndex((c) => c.id === data.purchaseLot.company.id)
    if (companyIdx >= 0) {
      companies.value[companyIdx]!.cash = data.purchaseLot.company.cash
    }

    purchaseSuccess.value = t('cityMap.purchaseSuccess')
    justPurchasedBuildingId.value = data.purchaseLot.building.id
    justPurchasedBuildingType.value = data.purchaseLot.building.type
    justPurchasedIsUnderConstruction.value = data.purchaseLot.building.isUnderConstruction ?? false
    justPurchasedConstructionCompletesAtTick.value = data.purchaseLot.building.constructionCompletesAtTick ?? null
    purchaseMode.value = false
    updateMarkers()
  } catch (e: unknown) {
    if (e instanceof GraphQLError) {
      if (e.code === 'LOT_ALREADY_OWNED') {
        // Stale lot: another player claimed this lot after the player opened the form.
        // Re-fetch just this single lot so the UI reflects new ownership immediately
        // without fetching the full city list.
        purchaseError.value = t('cityMap.purchaseErrorAlreadyOwned')
        purchaseMode.value = false
        try {
          const refreshedLot = await gqlRequest<{ lot: BuildingLot | null }>(
            `query GetLot($id: UUID!) {
              lot(id: $id) {
                id cityId name description district latitude longitude price suitableTypes
                ownerCompanyId buildingId
                ownerCompany { id name }
                building { id name type isUnderConstruction constructionCompletesAtTick constructionCost }
              }
            }`,
            { id: selectedLot.value?.id },
          )
          if (refreshedLot.lot) {
            const idx = lots.value.findIndex((l) => l.id === refreshedLot.lot!.id)
            if (idx >= 0) lots.value[idx] = refreshedLot.lot
            selectedLot.value = refreshedLot.lot
            updateMarkers()
          }
        } catch {
          // Silently ignore refresh errors; the stale-lot error message is already shown
        }
      } else if (e.code === 'INSUFFICIENT_FUNDS') {
        purchaseError.value = t('cityMap.purchaseErrorInsufficientFunds')
      } else if (e.code === 'UNSUITABLE_BUILDING_TYPE') {
        purchaseError.value = t('cityMap.purchaseErrorUnsuitable')
      } else {
        purchaseError.value = e.message
      }
    } else {
      purchaseError.value = e instanceof Error ? e.message : t('cityMap.purchaseError')
    }
  } finally {
    purchasing.value = false
  }
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

// Reload data and reinitialize map when city changes via the picker or back navigation.
// fetchData() handles its own error state (sets error.value), so no extra try-catch needed here.
watch(cityId, async () => {
  if (selectedCityId.value !== cityId.value) {
    auth.switchCity(cityId.value)
  }
  selectedLot.value = null
  purchaseMode.value = false
  purchaseError.value = null
  purchaseSuccess.value = null
  justPurchasedBuildingId.value = null
  justPurchasedBuildingType.value = null
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

// Fix blank-map regression: v-show keeps the container in the DOM so we can always
// call invalidateSize() on the existing Leaflet instance without re-initializing.
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

<style scoped src="./CityMapView.styles.css"></style>
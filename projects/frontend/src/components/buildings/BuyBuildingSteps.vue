<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { useAuthStore } from '@/stores/auth'
import { formatMoney } from '@/lib/currencyFormat'
import { nearestBuildingsForLot, syncSelectedLotId } from '@/lib/buyBuildingMap'
import type { BuildingLot, City, CurrencyBalance, PurchaseLotResult } from '@/types'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'

interface BuyBuildingPlayerBuilding {
  id: string
  cityId: string
  name: string
  type: string
  latitude: number
  longitude: number
}

const props = defineProps<{
  cities: City[]
  companyId: string
  selectedType: string
}>()

const emit = defineEmits<{
  purchased: [buildingId: string, buildingType: string]
}>()

const { t, locale } = useI18n()
const auth = useAuthStore()
const { selectedCityId } = storeToRefs(auth)

// ── State ──────────────────────────────────────────────────────────────────────

const lotsLoading = ref(false)
const error = ref<string | null>(null)
const availableLots = ref<BuildingLot[]>([])
const selectedLotId = ref('')
const selectedMediaType = ref('')
const buildingName = ref('')
const submitting = ref(false)
const depositRatePercent = ref<number>(3)
const lendingRatePercent = ref<number>(8)
const selectedResourceFilter = ref<string>('')
const playerBalances = ref<CurrencyBalance[]>([])
const companyBankAccounts = ref<Array<{ id: string; currencyCode: string; balance: number; companyId: string | null; ownerType: string }>>([])
const playerBuildings = ref<BuyBuildingPlayerBuilding[]>([])
const hoveredLotId = ref('')
const showMapOnMobile = ref(false)
const mapFullscreen = ref(false)
const mapContainer = ref<HTMLDivElement | null>(null)

let lotMap: L.Map | null = null
let lotMarkers: L.Marker[] = []
let playerBuildingMarkers: L.Marker[] = []
let nearestDistanceLines: L.Polyline[] = []

// ── Mutations ──────────────────────────────────────────────────────────────────

const SET_BANK_RATES_MUTATION = `
  mutation SetBankRates($input: SetBankRatesInput!) {
    setBankRates(input: $input) {
      bankBuildingId
      depositInterestRatePercent
      lendingInterestRatePercent
    }
  }
`

const INITIATE_BASE_DEPOSIT_MUTATION = `
  mutation InitiateBaseDeposit($bankBuildingId: UUID!) {
    initiateBaseDeposit(bankBuildingId: $bankBuildingId) {
      bankBuildingId
      baseCapitalDeposited
      totalDeposits
      depositInterestRatePercent
      lendingInterestRatePercent
    }
  }
`

const PROPERTY_DEFAULT_AREA_SQM: Record<string, number> = {
  APARTMENT: 1800,
  COMMERCIAL: 1400,
}

// ── Computeds ──────────────────────────────────────────────────────────────────

const selectedCompany = computed(() => auth.player?.companies.find((c) => c.id === props.companyId) ?? null)

const selectedLot = computed<BuildingLot | null>(() => availableLots.value.find((l) => l.id === selectedLotId.value) ?? null)

const isPropertyTypeSelected = computed(() => props.selectedType === 'APARTMENT' || props.selectedType === 'COMMERCIAL')

const selectedPropertyAreaSqm = computed<number | null>(() => (isPropertyTypeSelected.value ? (PROPERTY_DEFAULT_AREA_SQM[props.selectedType] ?? null) : null))

const selectedCityObj = computed(() => props.cities.find((c) => c.id === selectedCityId.value) ?? null)

const selectedCityCurrencyCode = computed<string>(() => selectedCityObj.value?.currencyCode?.toUpperCase() ?? 'EUR')

const bankBaseCapitalRequired = computed<number>(() => {
  const cc = selectedCityCurrencyCode.value
  switch (cc) {
    case 'CZK':
      return 240_000_000
    case 'GBP':
      return 8_600_000
    case 'CNY':
      return 72_000_000
    case 'INR':
      return 835_000_000
    default:
      return 10_000_000
  }
})

const companyBankBalanceInCityCurrency = computed<number>(() => {
  if (!selectedCompany.value) return 0
  const cc = selectedCityCurrencyCode.value
  return companyBankAccounts.value.filter((a) => a.ownerType === 'COMPANY' && a.companyId === selectedCompany.value!.id && a.currencyCode.toUpperCase() === cc).reduce((sum, a) => sum + a.balance, 0)
})

const companyHasBankCapital = computed<boolean>(() => !!selectedCompany.value && companyBankBalanceInCityCurrency.value >= bankBaseCapitalRequired.value)

const bankCapitalInsufficientMessage = computed<string>(() => t('buildings.bankCapitalInsufficient', { amount: formatCurrency(bankBaseCapitalRequired.value) }))

const CONSTRUCTION_COSTS: Record<string, number> = {
  MINE: 5_000,
  FACTORY: 15_000,
  SALES_SHOP: 8_000,
  RESEARCH_DEVELOPMENT: 25_000,
  APARTMENT: 40_000,
  COMMERCIAL: 20_000,
  MEDIA_HOUSE: 30_000,
  BANK: 50_000,
  EXCHANGE: 60_000,
  POWER_PLANT: 80_000,
}

const selectedLotTotalCost = computed<number>(() => {
  if (!selectedLot.value || !props.selectedType) return 0
  return selectedLot.value.price + (CONSTRUCTION_COSTS[props.selectedType] ?? 10_000)
})

const availableLocalBalance = computed<number>(() => {
  const cc = selectedCityCurrencyCode.value
  if (cc === 'EUR') return 0
  if (selectedCompany.value) {
    return companyBankAccounts.value.filter((a) => a.ownerType === 'COMPANY' && a.companyId === selectedCompany.value!.id && a.currencyCode.toUpperCase() === cc).reduce((sum, a) => sum + a.balance, 0)
  }
  return playerBalances.value.find((b) => b.currencyCode === cc)?.balance ?? 0
})

const fundingGapType = computed<'missing_account' | 'insufficient_funds' | null>(() => {
  const cc = selectedCityCurrencyCode.value
  if (cc === 'EUR') return null
  const balance = availableLocalBalance.value
  if (balance <= 0) return 'missing_account'
  if (selectedLot.value && balance < selectedLotTotalCost.value) return 'insufficient_funds'
  return null
})

const hasFundingGap = computed<boolean>(() => fundingGapType.value !== null)

const canSubmit = computed(() => {
  if (!props.selectedType || !selectedCityId.value || !selectedLot.value) return false
  if (props.selectedType === 'MEDIA_HOUSE' && !selectedMediaType.value) return false
  return true
})

const availableMineResources = computed(() => {
  const seen = new Set<string>()
  const result: Array<{ slug: string; name: string }> = []
  for (const lot of availableLots.value) {
    if (lot.resourceType && !seen.has(lot.resourceType.slug)) {
      seen.add(lot.resourceType.slug)
      result.push({ slug: lot.resourceType.slug, name: lot.resourceType.name })
    }
  }
  return result.sort((a, b) => a.name.localeCompare(b.name))
})

const filteredLots = computed(() => {
  if (!selectedResourceFilter.value || props.selectedType !== 'MINE') return availableLots.value
  return availableLots.value.filter((lot) => lot.resourceType?.slug === selectedResourceFilter.value)
})

const selectedOrHoveredLot = computed(
  () =>
    filteredLots.value.find((lot) => lot.id === selectedLotId.value)
    ?? filteredLots.value.find((lot) => lot.id === hoveredLotId.value)
    ?? null,
)

const cityPlayerBuildings = computed(() =>
  playerBuildings.value.filter((building) => building.cityId === selectedCityId.value),
)

const nearestBuildings = computed(() =>
  nearestBuildingsForLot(selectedOrHoveredLot.value, cityPlayerBuildings.value, 3),
)

// ── Lifecycle ──────────────────────────────────────────────────────────────────

onMounted(async () => {
  try {
    const [balancesData, accountsData, companiesData] = await Promise.all([
      gqlRequest<{ playerCurrencyBalances: CurrencyBalance[] }>('{ playerCurrencyBalances { currencyCode currencySymbol balance } }'),
      gqlRequest<{ myBankAccounts: Array<{ id: string; currencyCode: string; balance: number; companyId: string | null; ownerType: string }> }>('{ myBankAccounts { id currencyCode balance companyId ownerType } }'),
      gqlRequest<{ myCompanies: Array<{ buildings: BuyBuildingPlayerBuilding[] }> }>(`{
        myCompanies {
          buildings {
            id
            cityId
            name
            type
            latitude
            longitude
          }
        }
      }`),
    ])
    playerBalances.value = balancesData.playerCurrencyBalances ?? []
    companyBankAccounts.value = accountsData.myBankAccounts ?? []
    playerBuildings.value = (companiesData.myCompanies ?? []).flatMap((company) => company.buildings ?? [])
  } catch {
    // Non-fatal: financial data may be missing; displayed values fall back to 0
  }

  await nextTick()
  ensureLotMap()
})

onUnmounted(() => {
  destroyLotMap()
})

watch(
  [selectedCityId, () => props.selectedType],
  async ([cityId, buildingType]) => {
    selectedLotId.value = ''
    availableLots.value = []
    selectedResourceFilter.value = ''

    if (buildingType !== 'MEDIA_HOUSE') {
      selectedMediaType.value = ''
    }

    if (!cityId || !buildingType) {
      return
    }

    lotsLoading.value = true
    error.value = null

    try {
      const data = await gqlRequest<{ cityLots: BuildingLot[] }>(
        `query BuyBuildingLots($cityId: UUID!) {
          cityLots(cityId: $cityId) {
            id cityId name description district latitude longitude populationIndex basePrice price suitableTypes
            ownerCompanyId buildingId
            resourceType { id name slug }
            materialQuality materialQuantity
          }
        }`,
        { cityId },
      )

      availableLots.value = data.cityLots.filter((lot) => {
        const supportedTypes = lot.suitableTypes.split(',').map((item) => item.trim())
        if (lot.ownerCompanyId || !supportedTypes.includes(buildingType)) {
          return false
        }
        if (buildingType === 'MINE') {
          return lot.resourceType != null && lot.materialQuality != null && lot.materialQuantity != null
        }
        return true
      })
      selectedLotId.value = syncSelectedLotId(selectedLotId.value, availableLots.value)
      await nextTick()
      ensureLotMap()
      updateMapMarkers()
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : t('cityMap.purchaseError')
    } finally {
      lotsLoading.value = false
    }
  },
  { immediate: true },
)

watch(filteredLots, async (lots) => {
  const synced = syncSelectedLotId(selectedLotId.value, lots)
  if (synced !== selectedLotId.value) {
    selectedLotId.value = synced
  }
  await nextTick()
  ensureLotMap()
  updateMapMarkers()
})

watch(
  () => selectedLotId.value,
  () => {
    if (!lotMap) return
    updateMapMarkers()
  },
)

watch(cityPlayerBuildings, () => {
  if (!lotMap) return
  updateMapMarkers()
})

watch(
  () => [showMapOnMobile.value, mapFullscreen.value],
  async ([mobileVisible]) => {
    if (!mobileVisible) return
    await nextTick()
    if (!lotMap) {
      ensureLotMap()
      return
    }
    lotMap.invalidateSize()
  },
)

// ── Helpers ────────────────────────────────────────────────────────────────────

function formatCurrency(value: number) {
  return formatMoney(value, selectedCityObj.value?.currencyCode ?? 'EUR', locale.value)
}

function formatPopulationIndex(value: number) {
  return value.toFixed(2)
}

function formatSqm(value: number) {
  return `${value.toLocaleString(locale.value)} m²`
}

function districtLabel(district: string) {
  const key = `cityMap.districts.${district}`
  const translated = t(key)
  return translated === key ? district : translated
}

function buyBuildingMaterialQualityClass(quality: number): string {
  if (quality >= 0.8) return 'bg-emerald-500/15 text-emerald-600'
  if (quality >= 0.6) return 'bg-blue-500/15 text-blue-600'
  if (quality >= 0.4) return 'bg-amber-500/15 text-amber-600'
  return 'bg-red-500/15 text-red-600'
}

function buyBuildingMaterialQualityLabel(quality: number): string {
  if (quality >= 0.8) return t('cityMap.rawMaterialQualityExcellent')
  if (quality >= 0.6) return t('cityMap.rawMaterialQualityGood')
  if (quality >= 0.4) return t('cityMap.rawMaterialQualityFair')
  return t('cityMap.rawMaterialQualityPoor')
}

function formatDistanceKm(distanceKm: number): string {
  return `${distanceKm.toFixed(1)} km`
}

function lotMarkerColor(lot: BuildingLot): string {
  if (selectedLotId.value === lot.id) return '#f59e0b'
  return '#22c55e'
}

function createLotMarkerIcon(color: string, isSelected: boolean): L.DivIcon {
  const size = isSelected ? 20 : 14
  const border = isSelected ? '3px solid #fff' : '2px solid rgba(255,255,255,0.9)'
  return L.divIcon({
    className: `buy-lot-marker ${isSelected ? 'selected' : 'available'}`,
    html: `<div style="width:${size}px;height:${size}px;background:${color};border-radius:50%;border:${border};box-shadow:0 2px 8px rgba(0,0,0,0.35);"></div>`,
    iconSize: [size + 8, size + 8],
    iconAnchor: [(size + 8) / 2, (size + 8) / 2],
  })
}

function createPlayerBuildingMarkerIcon(buildingType: string): L.DivIcon {
  const buildingIcon = t(`buildings.typeIcons.${buildingType}`)
  const iconText = buildingIcon.startsWith('buildings.typeIcons.') ? '🏢' : buildingIcon
  return L.divIcon({
    className: 'buy-player-building-marker',
    html: `<div class="buy-player-marker-dot"><span>${iconText}</span></div>`,
    iconSize: [28, 28],
    iconAnchor: [14, 14],
  })
}

function clearMapLayers() {
  lotMarkers.forEach((marker) => marker.remove())
  playerBuildingMarkers.forEach((marker) => marker.remove())
  nearestDistanceLines.forEach((line) => line.remove())
  lotMarkers = []
  playerBuildingMarkers = []
  nearestDistanceLines = []
}

function drawNearestDistanceLines() {
  if (!lotMap || !selectedOrHoveredLot.value) return

  nearestDistanceLines.forEach((line) => line.remove())
  nearestDistanceLines = []

  for (const nearby of nearestBuildings.value) {
    const line = L.polyline(
      [
        [selectedOrHoveredLot.value.latitude, selectedOrHoveredLot.value.longitude],
        [nearby.latitude, nearby.longitude],
      ],
      {
        color: '#60a5fa',
        weight: 2,
        dashArray: '5, 5',
        opacity: 0.8,
      },
    ).addTo(lotMap)
    nearestDistanceLines.push(line)
  }
}

function fitMapToVisibleMarkers() {
  if (!lotMap || filteredLots.value.length === 0) return

  const allPoints: Array<[number, number]> = [
    ...filteredLots.value.map((lot) => [lot.latitude, lot.longitude] as [number, number]),
    ...cityPlayerBuildings.value.map((building) => [building.latitude, building.longitude] as [number, number]),
  ]
  const bounds = L.latLngBounds(allPoints)
  lotMap.fitBounds(bounds.pad(0.15), { animate: false })
}

function updateMapMarkers() {
  if (!lotMap) return

  clearMapLayers()

  for (const lot of filteredLots.value) {
    const marker = L.marker([lot.latitude, lot.longitude], {
      icon: createLotMarkerIcon(lotMarkerColor(lot), selectedLotId.value === lot.id),
    }).addTo(lotMap)

    marker.bindTooltip(`${lot.name}\n${formatCurrency(lot.price)}`)
    marker.on('click', () => {
      selectedLotId.value = lot.id
      showMapOnMobile.value = true
    })
    marker.on('mouseover', () => {
      hoveredLotId.value = lot.id
      drawNearestDistanceLines()
    })
    marker.on('mouseout', () => {
      hoveredLotId.value = ''
      drawNearestDistanceLines()
    })
    lotMarkers.push(marker)
  }

  for (const building of cityPlayerBuildings.value) {
    const marker = L.marker([building.latitude, building.longitude], {
      icon: createPlayerBuildingMarkerIcon(building.type),
    }).addTo(lotMap)
    marker.bindTooltip(`${building.name}\n${t(`buildings.types.${building.type}`)}`)
    playerBuildingMarkers.push(marker)
  }

  drawNearestDistanceLines()
  fitMapToVisibleMarkers()
}

function ensureLotMap() {
  if (lotMap || !mapContainer.value || filteredLots.value.length === 0) return

  const firstLot = filteredLots.value[0]
  if (!firstLot) return

  lotMap = L.map(mapContainer.value, {
    center: [firstLot.latitude, firstLot.longitude],
    zoom: 13,
    zoomControl: true,
    zoomAnimation: false,
    fadeAnimation: false,
    markerZoomAnimation: false,
  })

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; OpenStreetMap contributors',
    maxZoom: 19,
  }).addTo(lotMap)

  updateMapMarkers()
}

function destroyLotMap() {
  clearMapLayers()
  if (!lotMap) return
  lotMap.stop()
  lotMap.off()
  lotMap.remove()
  lotMap = null
}

function selectLotFromList(lotId: string) {
  selectedLotId.value = lotId
  if (!lotMap) return
  const lot = filteredLots.value.find((candidate) => candidate.id === lotId)
  if (!lot) return
  lotMap.panTo([lot.latitude, lot.longitude], { animate: false })
}

async function buyBuilding() {
  if (!canSubmit.value || !selectedCompany.value || !selectedLot.value) return

  if (props.selectedType === 'BANK' && !companyHasBankCapital.value) {
    error.value = bankCapitalInsufficientMessage.value
    return
  }

  submitting.value = true
  error.value = null

  try {
    const data = await gqlRequest<{ purchaseLot: PurchaseLotResult }>(
      `mutation PurchaseLot($input: PurchaseLotInput!) {
        purchaseLot(input: $input) {
          building { id name type level }
        }
      }`,
      {
        input: {
          companyId: selectedCompany.value.id,
          lotId: selectedLot.value.id,
          buildingType: props.selectedType,
          buildingName: buildingName.value.trim() || null,
          mediaType: props.selectedType === 'MEDIA_HOUSE' ? selectedMediaType.value || null : null,
        },
      },
    )

    const buildingId = data.purchaseLot.building.id

    if (props.selectedType === 'BANK') {
      try {
        await gqlRequest(INITIATE_BASE_DEPOSIT_MUTATION, { bankBuildingId: buildingId })
      } catch {
        // Non-fatal
      }
      try {
        await gqlRequest(SET_BANK_RATES_MUTATION, {
          input: {
            bankBuildingId: buildingId,
            depositInterestRatePercent: depositRatePercent.value,
            lendingInterestRatePercent: lendingRatePercent.value,
          },
        })
      } catch {
        // Non-fatal
      }
    }

    emit('purchased', buildingId, props.selectedType)
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : t('cityMap.purchaseError')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="flex flex-col gap-4">
    <!-- Step 2: Name, bank config, funding -->
    <div v-if="selectedType" class="mb-8 flex flex-col gap-4">
      <!-- Building name -->
      <div class="flex flex-col gap-1.5">
        <label for="buildingName" class="text-sm font-semibold">
          {{ t('buildings.buildingName') }}
          <span class="font-normal text-muted ml-1">({{ t('common.optional') }})</span>
        </label>
        <input
          id="buildingName"
          v-model="buildingName"
          type="text"
          maxlength="200"
          :placeholder="t('buildings.buildingNamePlaceholder')"
          class="w-full px-4 py-3 border-2 border-divider rounded-lg bg-page text-body text-base placeholder:text-muted focus:outline-none focus:border-brand focus:shadow-[0_0_0_3px_rgba(0,71,255,0.15)] transition-colors"
        />
      </div>

      <!-- Media house channel type -->
      <div v-if="selectedType === 'MEDIA_HOUSE'" class="flex flex-col gap-1.5">
        <label for="mediaType" class="text-sm font-semibold">{{ t('cityMap.mediaType') }}</label>
        <select
          id="mediaType"
          v-model="selectedMediaType"
          class="w-full px-4 py-3 border-2 border-divider rounded-lg bg-page text-body text-base focus:outline-none focus:border-brand focus:shadow-[0_0_0_3px_rgba(0,71,255,0.15)] transition-colors"
          required
        >
          <option value="">{{ t('cityMap.selectMediaType') }}</option>
          <option value="NEWSPAPER">📰 {{ t('cityMap.mediaTypespaper') }} (×1.0)</option>
          <option value="RADIO">📻 {{ t('cityMap.mediaTypeRadio') }} (×1.5)</option>
          <option value="TV">📺 {{ t('cityMap.mediaTypeTv') }} (×2.0)</option>
        </select>
        <p class="text-xs text-muted m-0">{{ t('cityMap.mediaTypeHint') }}</p>
        <div class="mt-2 grid gap-2">
          <div class="rounded-lg border border-divider bg-surface p-3">
            <p class="mb-1.5 text-xs font-bold text-foreground">📰 {{ t('cityMap.mediaTypespaper') }}</p>
            <p class="text-xs text-muted">{{ t('cityMap.mediaTypeGuidespaper') }}</p>
          </div>
          <div class="rounded-lg border border-divider bg-surface p-3">
            <p class="mb-1.5 text-xs font-bold text-foreground">📻 {{ t('cityMap.mediaTypeRadio') }}</p>
            <p class="text-xs text-muted">{{ t('cityMap.mediaTypeGuideRadio') }}</p>
          </div>
          <div class="rounded-lg border border-divider bg-surface p-3">
            <p class="mb-1.5 text-xs font-bold text-foreground">📺 {{ t('cityMap.mediaTypeTv') }}</p>
            <p class="text-xs text-muted">{{ t('cityMap.mediaTypeGuideTV') }}</p>
          </div>
          <div class="rounded-lg border border-amber-300/50 bg-amber-500/10 p-3">
            <p class="mb-1 text-xs font-bold text-amber-700 dark:text-amber-400">💡 {{ t('cityMap.mediaTypeStrategyTitle') }}</p>
            <p class="text-xs text-muted">{{ t('cityMap.mediaTypeStrategyBody') }}</p>
          </div>
        </div>
      </div>

      <div class="flex items-start gap-2 px-4 py-3 border border-divider rounded-lg bg-page">
        <span class="text-sm">📍</span>
        <div class="flex flex-col gap-0.5">
          <span class="text-xs text-muted">{{ t('buildings.selectCity') }}</span>
          <strong class="text-sm">{{ selectedCityObj?.name ?? t('common.selectCity') }}</strong>
        </div>
      </div>

      <!-- Funding gap alert -->
      <div
        v-if="selectedCityId && hasFundingGap"
        class="funding-guidance flex gap-3 p-4 bg-[rgba(255,159,67,0.08)] border border-[var(--color-warning,#ff9f43)] rounded-xl mt-1"
        role="alert"
        aria-live="polite"
      >
        <span class="text-xl flex-shrink-0" aria-hidden="true">⚠️</span>
        <div class="flex-1 flex flex-col gap-1.5">
          <strong class="text-[0.95rem] font-bold text-[var(--color-warning,#ff9f43)]">
            <template v-if="fundingGapType === 'missing_account'">{{ t('buildings.fundingGapTitleMissing', { currency: selectedCityCurrencyCode }) }}</template>
            <template v-else>{{ t('buildings.fundingGapTitleInsufficient', { currency: selectedCityCurrencyCode }) }}</template>
          </strong>
          <p class="text-sm text-muted m-0">
            <template v-if="fundingGapType === 'missing_account'">{{ t('buildings.fundingGapBodyMissing', { currency: selectedCityCurrencyCode }) }}</template>
            <template v-else>{{ t('buildings.fundingGapBodyInsufficient', { currency: selectedCityCurrencyCode }) }}</template>
          </p>
          <div v-if="fundingGapType === 'insufficient_funds'" class="flex flex-col gap-1 mt-1 p-3 bg-white/[0.04] rounded-md text-sm">
            <div class="amount-required flex justify-between items-center">
              <span class="text-muted">{{ t('buildings.fundingGapRequired') }}</span>
              <strong>{{ formatCurrency(selectedLotTotalCost) }}</strong>
            </div>
            <div class="amount-available flex justify-between items-center">
              <span class="text-muted">{{ t('buildings.fundingGapAvailable') }}</span>
              <strong class="text-[var(--color-warning,#ff9f43)]">{{ formatCurrency(availableLocalBalance) }}</strong>
            </div>
            <div class="amount-shortfall flex justify-between items-center border-t border-divider pt-1 mt-0.5">
              <span class="text-muted">{{ t('buildings.fundingGapShortfall') }}</span>
              <strong class="text-bad">{{ formatCurrency(selectedLotTotalCost - availableLocalBalance) }}</strong>
            </div>
          </div>
          <div class="flex flex-wrap gap-2 mt-1">
            <RouterLink
              to="/forex"
              class="btn-guidance-primary inline-flex items-center px-4 py-1.5 bg-brand text-white rounded text-sm font-semibold no-underline hover:opacity-90 transition-opacity"
            >
              {{ t('buildings.fundingGapGoToForex') }}
            </RouterLink>
            <RouterLink
              v-if="selectedCompany"
              :to="`/bank-statement/${selectedCompany.id}`"
              class="btn-guidance-secondary inline-flex items-center px-4 py-1.5 border border-divider text-muted rounded text-sm font-semibold no-underline hover:bg-card transition-colors"
            >
              {{ t('buildings.fundingGapViewStatement') }}
            </RouterLink>
          </div>
        </div>
      </div>

      <!-- Company balance banner -->
      <div v-if="selectedCompany" class="flex justify-between items-center mt-1 px-4 py-3 border border-divider rounded-lg bg-page">
        <span class="font-medium">{{ selectedCompany.name }}</span>
        <strong class="text-good">{{ formatCurrency(companyBankBalanceInCityCurrency) }}</strong>
      </div>

      <!-- BANK: setup info -->
      <div v-if="selectedType === 'BANK'" class="flex gap-4 mt-1 p-5 bg-[rgba(59,130,246,0.08)] border border-[rgba(59,130,246,0.25)] rounded-lg">
        <span class="text-3xl flex-shrink-0">🏦</span>
        <div class="flex-1">
          <h3 class="text-[0.9375rem] font-bold mb-1.5">{{ t('buildings.bankSetupTitle') }}</h3>
          <p class="text-sm text-muted mb-2">{{ t('buildings.bankSetupDescription') }}</p>
          <ul class="m-0 pl-5 text-[0.8125rem] text-muted flex flex-col gap-1 list-disc">
            <li>{{ t('buildings.bankSetupStep1') }}</li>
            <li>{{ t('buildings.bankSetupStep2') }}</li>
            <li>{{ t('buildings.bankSetupStep3') }}</li>
          </ul>
        </div>
      </div>

      <!-- BANK: capital requirements check -->
      <div
        v-if="selectedType === 'BANK'"
        class="flex items-start gap-3 mt-1 px-5 py-3.5 border rounded-lg"
        :class="companyHasBankCapital ? 'capital-ok bg-[rgba(16,185,129,0.07)] border-[rgba(16,185,129,0.3)]' : 'capital-warn bg-[rgba(248,113,113,0.07)] border-[rgba(248,113,113,0.3)]'"
      >
        <span class="text-xl flex-shrink-0" aria-hidden="true">{{ companyHasBankCapital ? '✅' : '⚠️' }}</span>
        <div class="flex flex-col gap-1">
          <span class="text-[0.8125rem] text-muted">{{ t('buildings.bankCapitalRequirement') }}:</span>
          <strong class="text-base font-bold">{{ formatCurrency(bankBaseCapitalRequired) }}</strong>
          <span v-if="companyHasBankCapital" class="capital-status-ok text-[0.8125rem] text-[#10b981]">{{ t('buildings.bankCapitalSufficient') }}</span>
          <span v-else class="capital-status-warn text-[0.8125rem] text-bad font-semibold">{{ bankCapitalInsufficientMessage }}</span>
        </div>
      </div>

      <!-- BANK: interest rate config -->
      <div v-if="selectedType === 'BANK'" class="mt-1 p-5 bg-[rgba(59,130,246,0.05)] border border-[rgba(59,130,246,0.2)] rounded-lg">
        <h4 class="text-[0.9375rem] font-bold mb-3.5">{{ t('buildings.bankSetupRatesTitle') }}</h4>
        <div class="grid grid-cols-2 max-sm:grid-cols-1 gap-4">
          <div class="flex flex-col gap-1">
            <label for="depositRatePercent" class="text-sm font-semibold">{{ t('buildings.bankDepositRateLabel') }}</label>
            <p class="text-xs text-muted mb-1 m-0">{{ t('buildings.bankDepositRateHint') }}</p>
            <input
              id="depositRatePercent"
              v-model.number="depositRatePercent"
              type="number"
              min="0"
              max="100"
              step="0.1"
              class="px-3 py-2 border border-divider rounded bg-card text-body text-[0.9rem] w-full max-w-[200px] focus:outline-none focus:border-brand transition-colors"
            />
          </div>
          <div class="flex flex-col gap-1">
            <label for="lendingRatePercent" class="text-sm font-semibold">{{ t('buildings.bankLendingRateLabel') }}</label>
            <p class="text-xs text-muted mb-1 m-0">{{ t('buildings.bankLendingRateHint') }}</p>
            <input
              id="lendingRatePercent"
              v-model.number="lendingRatePercent"
              type="number"
              min="0.1"
              max="200"
              step="0.1"
              class="px-3 py-2 border border-divider rounded bg-card text-body text-[0.9rem] w-full max-w-[200px] focus:outline-none focus:border-brand transition-colors"
            />
          </div>
        </div>
      </div>
    </div>

    <!-- Step 3: Land selection -->
    <div v-if="selectedType && selectedCityId" class="mb-8">
      <div class="flex items-center justify-between gap-4 mb-3">
        <h2 class="text-lg font-semibold">{{ t('buildings.selectLand') }}</h2>
        <span v-if="lotsLoading" class="text-[0.8125rem] text-muted">{{ t('common.loading') }}</span>
      </div>

      <div v-if="!lotsLoading && availableLots.length === 0" class="mt-4 p-4 border border-divider rounded-lg bg-page text-muted text-sm">
        {{ t('buildings.noAvailableLand') }}
      </div>

      <!-- Mine resource filter -->
      <div v-if="selectedType === 'MINE' && availableMineResources.length > 1 && !lotsLoading && availableLots.length > 0" class="mine-resource-filter mb-4">
        <span class="text-xs font-semibold text-muted mr-2">{{ t('buildings.filterByResource') }}:</span>
        <div class="flex flex-wrap gap-2 mt-1.5">
          <button
            class="resource-filter-btn px-3 py-1 rounded-full border text-xs font-semibold transition-all"
            :class="!selectedResourceFilter ? 'border-brand bg-brand/10 text-brand' : 'border-divider text-muted hover:border-brand hover:text-brand'"
            @click="selectedResourceFilter = ''"
          >
            {{ t('common.all') }}
          </button>
          <button
            v-for="res in availableMineResources"
            :key="res.slug"
            class="resource-filter-btn px-3 py-1 rounded-full border text-xs font-semibold transition-all"
            :class="selectedResourceFilter === res.slug ? 'border-brand bg-brand/10 text-brand' : 'border-divider text-muted hover:border-brand hover:text-brand'"
            @click="selectedResourceFilter = res.slug"
          >
            ⛏ {{ res.name }}
          </button>
        </div>
      </div>

      <div v-else-if="!lotsLoading && filteredLots.length === 0 && availableLots.length > 0" class="mt-4 p-4 border border-divider rounded-lg bg-page text-muted text-sm">
        {{ t('buildings.noLotsForResource') }}
      </div>

      <div v-if="filteredLots.length > 0" class="buy-building-map-panel mb-4 rounded-lg border border-divider bg-page p-3">
        <div class="mb-3 flex items-center justify-between gap-2">
          <div class="flex flex-col">
            <strong class="text-sm">{{ t('buildings.landMapTitle') }}</strong>
            <span class="text-xs text-muted">{{ t('buildings.landMapHint') }}</span>
          </div>
          <div class="flex items-center gap-2">
            <button
              class="buy-building-map-toggle btn btn-sm border border-divider bg-card px-2 py-1 text-xs md:hidden"
              @click="showMapOnMobile = !showMapOnMobile"
            >
              {{ showMapOnMobile ? t('buildings.hideMap') : t('buildings.showMap') }}
            </button>
            <button class="btn btn-sm border border-divider bg-card px-2 py-1 text-xs" @click="mapFullscreen = !mapFullscreen">
              {{ mapFullscreen ? t('buildings.exitFullscreenMap') : t('buildings.fullscreenMap') }}
            </button>
          </div>
        </div>
        <div
          class="buy-building-map"
          :class="{
            'hidden md:block': !showMapOnMobile,
            fullscreen: mapFullscreen,
          }"
        >
          <div ref="mapContainer" class="buy-building-map-canvas" />
        </div>
        <div class="mt-3 flex flex-wrap gap-3 text-xs text-muted">
          <span class="inline-flex items-center gap-1"><span class="legend-dot lot"></span>{{ t('buildings.landMapLegendLots') }}</span>
          <span class="inline-flex items-center gap-1"><span class="legend-dot selected"></span>{{ t('buildings.landMapLegendSelectedLot') }}</span>
          <span class="inline-flex items-center gap-1"><span class="legend-dot building"></span>{{ t('buildings.landMapLegendYourBuildings') }}</span>
        </div>
      </div>

      <!-- Lot grid -->
      <div v-if="filteredLots.length > 0" class="grid gap-3 [grid-template-columns:repeat(auto-fill,minmax(220px,1fr))]">
        <button
          v-for="lot in filteredLots"
          :key="lot.id"
          class="lot-card flex flex-col gap-2 p-4 border-2 border-divider rounded-lg bg-page text-body text-left cursor-pointer transition-all duration-200 hover:border-brand hover:-translate-y-px focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand"
          :class="selectedLotId === lot.id ? 'selected border-brand ring-1 ring-brand bg-[rgba(0,71,255,0.08)]' : ''"
          @mouseenter="hoveredLotId = lot.id"
          @mouseleave="hoveredLotId = ''"
          @click="selectLotFromList(lot.id)"
        >
          <div class="flex justify-between items-baseline gap-4">
            <span class="font-bold">{{ lot.name }}</span>
            <span class="text-sm font-bold text-good shrink-0">{{ formatCurrency(lot.price) }}</span>
          </div>
          <span class="text-[0.8125rem] text-muted">{{ districtLabel(lot.district) }}</span>
          <span v-if="selectedType === 'MINE' && lot.materialQuality != null" class="text-[0.8125rem] text-muted">
            {{ t('cityMap.rawMaterialQuality') }}: {{ Math.round(lot.materialQuality * 100) }}%
          </span>
          <span v-if="selectedType === 'MINE' && lot.materialQuantity != null" class="text-[0.8125rem] text-muted">
            {{ t('cityMap.rawMaterialQuantity') }}: {{ lot.materialQuantity.toLocaleString(locale) }} {{ t('cityMap.rawMaterialQuantityUnit') }}
          </span>
          <span v-if="selectedType !== 'MINE'" class="text-[0.8125rem] text-muted">
            {{ t('buildings.populationIndex') }}: {{ formatPopulationIndex(lot.populationIndex) }}
          </span>
          <span class="text-[0.8125rem] text-muted">{{ t('buildings.appraisedValue') }}: {{ formatCurrency(lot.basePrice) }}</span>
          <span v-if="selectedPropertyAreaSqm != null" class="text-[0.8125rem] text-muted">{{ t('buildings.propertySize') }}: {{ formatSqm(selectedPropertyAreaSqm) }}</span>
          <span
            v-if="lot.resourceType"
            class="buy-building-resource-badge inline-block self-start rounded-full border border-violet-500/25 bg-violet-500/10 px-2 py-0.5 text-[0.7rem] font-semibold text-brand cursor-help"
            data-testid="buy-building-resource-badge"
            :title="t('cityMap.resourcePremiumTooltip')"
          >
            ⛏ {{ lot.resourceType.name }}
          </span>
        </button>
      </div>

      <!-- Selected lot summary -->
      <div v-if="selectedLot" class="flex flex-col gap-2 mt-4 p-4 border border-divider rounded-lg bg-page">
        <div>
          <span class="text-[0.8125rem] text-muted mr-1.5">{{ t('buildings.selectedLand') }}</span>
          <strong>{{ selectedLot.name }}</strong>
        </div>
        <div class="flex flex-wrap gap-4 text-sm text-muted">
          <span>{{ districtLabel(selectedLot.district) }}</span>
          <span>
            {{ t('buildings.askingPrice') }}: <strong class="text-body">{{ formatCurrency(selectedLot.price) }}</strong>
            <span
              v-if="selectedLot.resourceType && selectedLot.price > selectedLot.basePrice"
              class="buy-building-resource-premium-badge ml-1 inline-block rounded px-1 py-0.5 text-[0.7rem] font-semibold text-violet-700 bg-violet-500/15 align-middle cursor-help"
              :title="t('cityMap.resourcePremiumTooltip')"
            >{{ t('cityMap.resourcePremium') }}</span>
          </span>
          <span v-if="selectedType === 'MINE' && selectedLot.materialQuality != null">
            {{ t('cityMap.rawMaterialQuality') }}: <strong class="text-body">{{ Math.round(selectedLot.materialQuality * 100) }}%</strong>
          </span>
          <span v-if="selectedType === 'MINE' && selectedLot.materialQuantity != null">
            {{ t('cityMap.rawMaterialQuantity') }}: <strong class="text-body">{{ selectedLot.materialQuantity.toLocaleString(locale) }} {{ t('cityMap.rawMaterialQuantityUnit') }}</strong>
          </span>
          <span v-if="selectedType !== 'MINE'">{{ t('buildings.populationIndex') }}: {{ formatPopulationIndex(selectedLot.populationIndex) }}</span>
          <span v-if="selectedPropertyAreaSqm != null">
            {{ t('buildings.propertySize') }}: <strong class="text-body">{{ formatSqm(selectedPropertyAreaSqm) }}</strong>
          </span>
        </div>
        <div v-if="nearestBuildings.length > 0" class="buy-building-nearest rounded-md border border-divider bg-surface p-3">
          <strong class="text-xs">{{ t('buildings.landMapNearestTitle') }}</strong>
          <ul class="mt-2 flex flex-col gap-1.5 m-0 p-0 list-none">
            <li v-for="building in nearestBuildings" :key="building.id" class="text-xs text-muted">
              <span class="font-semibold text-body">{{ building.name }}</span>
              <span class="mx-1">·</span>
              <span>{{ t(`buildings.types.${building.type}`) }}</span>
              <span class="mx-1">·</span>
              <span>{{ formatDistanceKm(building.distanceKm) }}</span>
            </li>
          </ul>
        </div>
        <!-- Mining deposit investment summary -->
        <div
          v-if="selectedType === 'MINE' && selectedLot.resourceType"
          class="buy-building-mining-summary mt-2 rounded-md border border-violet-500/20 bg-violet-500/10 p-3.5"
          data-testid="buy-building-mining-summary"
        >
          <h4 class="buy-building-mining-summary-title m-0 mb-2.5 text-sm font-semibold text-violet-700">⛏ {{ t('cityMap.miningDepositSummaryTitle') }}</h4>
          <div class="buy-building-mining-summary-grid mb-2.5 grid grid-cols-2 gap-x-4 gap-y-2">
            <div class="buy-building-mining-summary-item flex flex-col gap-0.5">
              <span class="text-[0.8125rem] text-muted">{{ t('cityMap.rawMaterialResource') }}</span>
              <strong class="text-sm">{{ selectedLot.resourceType.name }}</strong>
            </div>
            <div v-if="selectedLot.materialQuality != null" class="buy-building-mining-summary-item flex flex-col gap-0.5">
              <span class="text-[0.8125rem] text-muted">{{ t('cityMap.rawMaterialQuality') }}</span>
              <span class="buy-building-quality-badge inline-block rounded-full px-1.5 py-0.5 text-xs font-semibold" :class="buyBuildingMaterialQualityClass(selectedLot.materialQuality)">
                {{ buyBuildingMaterialQualityLabel(selectedLot.materialQuality) }} ({{ Math.round(selectedLot.materialQuality * 100) }}%)
              </span>
            </div>
            <div v-if="selectedLot.materialQuantity != null" class="buy-building-mining-summary-item flex flex-col gap-0.5">
              <span class="text-[0.8125rem] text-muted">{{ t('cityMap.rawMaterialQuantity') }}</span>
              <span class="text-sm">{{ selectedLot.materialQuantity.toLocaleString(locale) }} {{ t('cityMap.rawMaterialQuantityUnit') }}</span>
            </div>
            <div class="buy-building-mining-summary-item flex flex-col gap-0.5">
              <span class="text-[0.8125rem] text-muted">{{ t('buildings.appraisedValue') }}</span>
              <span class="text-sm">{{ formatCurrency(selectedLot.basePrice) }}</span>
            </div>
            <div v-if="selectedLot.price > selectedLot.basePrice" class="buy-building-mining-summary-item flex flex-col gap-0.5">
              <span class="text-[0.8125rem] text-muted">{{ t('buildings.resourceDepositPremium') }}</span>
              <span class="text-sm font-semibold text-good">+ {{ formatCurrency(selectedLot.price - selectedLot.basePrice) }}</span>
            </div>
          </div>
          <p class="buy-building-mining-hint m-0 text-xs leading-[1.45] text-muted">{{ t('cityMap.miningInvestmentHint') }}</p>
        </div>
      </div>

      <!-- Error from lot operations -->
      <p v-if="error" class="text-bad text-sm mt-3">{{ error }}</p>

      <!-- CTA -->
      <div class="flex justify-end mt-6">
        <button
          class="btn btn-primary px-8 py-3 text-base font-semibold rounded-lg disabled:opacity-50 disabled:cursor-not-allowed"
          :disabled="!canSubmit || submitting || (selectedType === 'BANK' && !companyHasBankCapital) || hasFundingGap"
          @click="buyBuilding"
        >
          {{ submitting ? t('common.loading') : t('buildings.buyNow') }}
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.buy-building-map {
  min-height: 320px;
}

.buy-building-map-canvas {
  min-height: 320px;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  overflow: hidden;
}

.buy-building-map.fullscreen {
  position: fixed;
  inset: 1rem;
  z-index: 60;
  background: var(--color-bg);
  border-radius: var(--radius-lg);
  padding: 0.5rem;
  min-height: auto;
}

.buy-building-map.fullscreen .buy-building-map-canvas {
  height: calc(100vh - 3rem);
  min-height: calc(100vh - 3rem);
}

.legend-dot {
  width: 0.625rem;
  height: 0.625rem;
  border-radius: 999px;
  display: inline-block;
}

.legend-dot.lot {
  background: #22c55e;
}

.legend-dot.selected {
  background: #f59e0b;
}

.legend-dot.building {
  background: #2563eb;
}

:deep(.buy-player-marker-dot) {
  width: 26px;
  height: 26px;
  border-radius: 999px;
  border: 2px solid #fff;
  background: #2563eb;
  display: grid;
  place-items: center;
  color: #fff;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.35);
  font-size: 12px;
}
</style>

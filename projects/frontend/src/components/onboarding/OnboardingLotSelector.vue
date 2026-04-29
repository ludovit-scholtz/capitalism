<script setup lang="ts">
/* oxlint-disable no-unused-vars */

import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { formatMoney } from '@/lib/currencyFormat'
import type { BuildingLot, City } from '@/types'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'

const props = withDefaults(
  defineProps<{
    lots: BuildingLot[]
    selectedLotId: string
    requiredBuildingType: string
    moneyAvailable: number
    recommendedLotIds?: string[]
    city: City | null
  }>(),
  {
    recommendedLotIds: () => [],
  },
)

const emit = defineEmits<{
  'update:selectedLotId': [value: string]
}>()

const { t, locale } = useI18n()

const mapContainer = ref<HTMLDivElement | null>(null)
const viewMode = ref<'map' | 'list'>('map')
const showAvailableOnly = ref(false)

let map: L.Map | null = null
let markers: L.Marker[] = []

const filteredLots = computed(() => {
  if (showAvailableOnly.value) {
    return props.lots.filter((lot) => !lot.ownerCompanyId)
  }

  return props.lots
})

const selectedLot = computed(() => props.lots.find((lot) => lot.id === props.selectedLotId) ?? null)

function formatCurrency(value: number): string {
  return formatMoney(value, props.city?.currencyCode ?? 'EUR', locale.value)
}

function formatBuildingType(type: string): string {
  const key = `buildings.types.${type}`
  const translated = t(key)
  if (translated !== key) {
    return translated
  }

  return type.replace(/_/g, ' ').replace(/\b\w/g, (char) => char.toUpperCase())
}

function calculateDistance(lot: BuildingLot, city: City): number {
  const dLat = lot.latitude - city.latitude
  const dLon = lot.longitude - city.longitude
  return Math.sqrt(dLat * dLat + dLon * dLon) * 111 // approximate km
}

function calculateDeliveryCost(distance: number): number {
  return Math.round(distance * 10) // $10 per km
}

function formatDistance(distance: number): string {
  return `${distance.toFixed(1)} km`
}

function isRecommended(lot: BuildingLot): boolean {
  return props.recommendedLotIds.includes(lot.id)
}

function isAffordable(lot: BuildingLot): boolean {
  return props.moneyAvailable >= lot.price
}

function getLotStatus(lot: BuildingLot): 'available' | 'owned' | 'selected' {
  if (selectedLot.value?.id === lot.id) {
    return 'selected'
  }

  return lot.ownerCompanyId ? 'owned' : 'available'
}

function getMarkerColor(lot: BuildingLot): string {
  const status = getLotStatus(lot)
  if (status === 'selected') return '#0047FF'
  if (lot.ownerCompanyId) return '#6B7280'
  if (isRecommended(lot) && isAffordable(lot)) return '#00C853'
  if (isAffordable(lot)) return '#FF6D00'
  return '#8B949E'
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

function selectLot(lotId: string): void {
  emit('update:selectedLotId', lotId)
}

function updateMarkers(): void {
  if (!map) return

  markers.forEach((marker) => marker.remove())
  markers = []

  for (const lot of filteredLots.value) {
    const isSelected = selectedLot.value?.id === lot.id
    const marker = L.marker([lot.latitude, lot.longitude], {
      icon: createMarkerIcon(getMarkerColor(lot), isSelected),
    }).addTo(map)

    marker.bindTooltip(lot.name, {
      direction: 'top',
      offset: [0, -10],
    })

    marker.on('click', () => {
      selectLot(lot.id)
    })

    markers.push(marker)
  }

  if (filteredLots.value.length > 0) {
    const bounds = L.latLngBounds(filteredLots.value.map((lot) => [lot.latitude, lot.longitude] as [number, number]))
    map.fitBounds(bounds.pad(0.15), { animate: false })
  }
}

function destroyMap(): void {
  markers.forEach((marker) => marker.remove())
  markers = []

  if (!map) return

  map.stop()
  map.off()
  map.remove()
  map = null
}

function initMap(): void {
  if (!mapContainer.value || map || filteredLots.value.length === 0) return

  const fallbackLot = filteredLots.value[0]
  if (!fallbackLot) return

  map = L.map(mapContainer.value, {
    center: [fallbackLot.latitude, fallbackLot.longitude],
    zoom: 14,
    zoomControl: true,
    zoomAnimation: false,
    fadeAnimation: false,
    markerZoomAnimation: false,
  })

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; OpenStreetMap contributors',
    maxZoom: 19,
  }).addTo(map)

  updateMarkers()
}

watch(filteredLots, () => {
  if (map) {
    updateMarkers()
  }
})

watch(
  () => props.selectedLotId,
  () => {
    if (map) {
      updateMarkers()
      if (selectedLot.value) {
        map.panTo([selectedLot.value.latitude, selectedLot.value.longitude], { animate: false })
      }
    }
  },
)

watch(viewMode, async (mode) => {
  if (mode === 'map') {
    await nextTick()
    if (!map) {
      initMap()
    } else {
      map.invalidateSize()
    }
    return
  }

  destroyMap()
})

onMounted(async () => {
  await nextTick()
  if (viewMode.value === 'map') {
    initMap()
  }
})

onUnmounted(() => {
  destroyMap()
})
</script>

<template>
  <section class="lot-selector">
    <div class="selector-toolbar">
      <div class="selector-stats">
        <strong>{{ t('cityMap.lotCount', { count: filteredLots.length }) }}</strong>
        <span>{{ t('onboarding.mapLegend') }}</span>
      </div>

      <div class="toolbar-actions">
        <div class="view-toggle" role="group" :aria-label="t('onboarding.mapViewLabel')">
          <button class="toggle-btn" :class="{ active: viewMode === 'map' }" @click="viewMode = 'map'">­čŚ║´ŞĆ {{ t('cityMap.mapView') }}</button>
          <button class="toggle-btn" :class="{ active: viewMode === 'list' }" @click="viewMode = 'list'">Ôś░ {{ t('cityMap.listView') }}</button>
        </div>
        <button class="toggle-btn" :class="{ active: showAvailableOnly }" @click="showAvailableOnly = !showAvailableOnly">
          {{ t('cityMap.filterAvailable') }}
        </button>
      </div>
    </div>

    <div class="selector-layout">
      <div class="visual-panel">
        <div v-if="viewMode === 'map'" ref="mapContainer" class="map-panel" />
        <div v-else class="list-panel">
          <button v-for="lot in filteredLots" :key="lot.id" class="lot-list-item" :class="{ selected: selectedLotId === lot.id }" @click="selectLot(lot.id)">
            <div class="lot-list-header">
              <strong>{{ lot.name }}</strong>
              <span class="lot-price">{{ formatCurrency(lot.price) }}</span>
            </div>
            <div class="lot-list-meta">
              <span>{{ t('cityMap.district') }}: {{ t(`cityMap.districts.${lot.district}`) }}</span>
              <span>{{ lot.suitableTypes.split(',').map(formatBuildingType).join(', ') }}</span>
            </div>
            <div class="lot-badges">
              <span v-if="isRecommended(lot)" class="badge recommended">{{ t('onboarding.recommendedLot') }}</span>
              <span v-if="!isAffordable(lot)" class="badge muted">{{ t('onboarding.needsMoreCash') }}</span>
              <span v-else class="badge affordable">{{ t('onboarding.affordableNow') }}</span>
              <span v-if="lot.ownerCompanyId" class="badge muted">{{ t('cityMap.owned') }}</span>
            </div>
          </button>
        </div>
      </div>

      <aside class="detail-panel">
        <template v-if="selectedLot">
          <div class="detail-header">
            <h3>{{ selectedLot.name }}</h3>
            <span v-if="isRecommended(selectedLot)" class="badge recommended">
              {{ t('onboarding.recommendedLot') }}
            </span>
          </div>

          <p class="detail-description">{{ selectedLot.description }}</p>

          <dl class="detail-grid">
            <div>
              <dt>{{ t('cityMap.district') }}</dt>
              <dd>{{ t(`cityMap.districts.${selectedLot.district}`) }}</dd>
            </div>
            <div v-if="city">
              <dt>{{ t('cityMap.deliveryDistance') }}</dt>
              <dd>{{ formatDistance(calculateDistance(selectedLot, city)) }}</dd>
            </div>
            <div>
              <dt>{{ t('cityMap.price') }}</dt>
              <dd>{{ formatCurrency(selectedLot.price) }}</dd>
            </div>
            <div v-if="city">
              <dt>{{ t('cityMap.priceWithDelivery') }}</dt>
              <dd>{{ formatCurrency(selectedLot.price + calculateDeliveryCost(calculateDistance(selectedLot, city))) }}</dd>
            </div>
            <div>
              <dt>{{ t('cityMap.suitableFor') }}</dt>
              <dd>{{ selectedLot.suitableTypes.split(',').map(formatBuildingType).join(', ') }}</dd>
            </div>
            <div>
              <dt>{{ t('onboarding.cashAfterPurchase') }}</dt>
              <dd :class="{ danger: !isAffordable(selectedLot) }">
                {{ formatCurrency(Math.max(props.moneyAvailable - selectedLot.price, 0)) }}
              </dd>
            </div>
          </dl>

          <p v-if="selectedLot.ownerCompanyId" class="status-message" role="status">
            {{ t('onboarding.lotUnavailableBody') }}
          </p>
          <p v-else-if="!isAffordable(selectedLot)" class="status-message warning" role="status">
            {{ t('onboarding.needsMoreCash') }}
          </p>
          <p v-else class="status-message success" role="status">
            {{ t('onboarding.readyToPurchase', { type: formatBuildingType(requiredBuildingType) }) }}
          </p>
        </template>

        <p v-else class="empty-selection">{{ t('cityMap.selectLot') }}</p>
      </aside>
    </div>
  </section>
</template>

<style scoped src="./OnboardingLotSelector.styles.css"></style>

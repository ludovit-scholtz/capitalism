<template>
  <div class="world-map-page">
    <header class="world-map-header">
      <h1>{{ t('worldMap.title') }}</h1>
      <p>{{ t('worldMap.subtitle') }}</p>
    </header>

    <div v-if="loading" class="world-map-state">{{ t('common.loading') }}</div>
    <div v-else-if="error" class="world-map-state world-map-error" role="alert">
      <p>{{ error }}</p>
      <button class="btn btn-secondary btn-sm" type="button" @click="fetchWorldMap">
        {{ t('common.tryAgain') }}
      </button>
    </div>

    <div v-else class="world-map-layout">
      <div class="world-map-panel">
        <div class="world-map-legend">
          <span><i class="dot dot--mine"></i> {{ t('worldMap.legendMine') }}</span>
          <span><i class="dot dot--available"></i> {{ t('worldMap.legendAvailable') }}</span>
        </div>
        <div ref="mapEl" class="world-map-canvas" />
      </div>

      <aside class="city-sidebar">
        <h2>{{ t('worldMap.cityListTitle') }}</h2>
        <div class="city-list">
          <button
            v-for="city in cities"
            :key="city.id"
            class="city-item"
            :class="{ 'city-item--active': selectedCity?.id === city.id }"
            type="button"
            @click="selectCity(city)"
          >
            <span>{{ city.name }}</span>
            <small>{{ city.currencyCode }}</small>
          </button>
        </div>

        <section v-if="selectedCity" class="city-detail">
          <h3>{{ selectedCity.name }}</h3>
          <p>{{ t('worldMap.population') }}: {{ selectedCity.population.toLocaleString() }}</p>
          <p>{{ t('worldMap.currency') }}: {{ selectedCity.currencyCode }}</p>
          <p>{{ t('worldMap.availableLand') }}: {{ selectedCity.availableLandPlots ?? 0 }}</p>
          <p>{{ t('worldMap.competition') }}: {{ selectedCity.activeCompanyCount ?? 0 }}</p>
          <p v-if="selectedCity.topResourceName">{{ t('worldMap.featuredResource') }}: {{ selectedCity.topResourceName }}</p>

          <button
            v-if="selectedCity.isUnlocked"
            class="btn btn-primary btn-sm"
            type="button"
            @click="goToCity(selectedCity)"
          >
            {{ t('worldMap.goToCity') }}
          </button>
          <button
            v-else
            class="btn btn-secondary btn-sm"
            type="button"
            @click="showExpansionModal = true"
          >
            {{ t('worldMap.startExpanding') }}
          </button>
        </section>
      </aside>
    </div>

    <div v-if="showExpansionModal && selectedCity && !selectedCity.isUnlocked" class="expansion-modal-backdrop" role="dialog" aria-modal="true">
      <div class="expansion-modal">
        <h2>{{ t('worldMap.expandTitle', { city: selectedCity.name }) }}</h2>
        <p>{{ t('cityExpansion.mapLockedBody', { city: selectedCity.name, amount: formatCurrency(selectedCity.requiredNetWorth ?? 0, selectedCity.currencyCode) }) }}</p>
        <div class="world-map-progress" aria-hidden="true">
          <span class="world-map-progress__fill" :style="{ width: `${selectedCity.progressPercent ?? 0}%` }"></span>
        </div>
        <p class="world-map-progress__label">{{ t('cityExpansion.progressLabel', { percent: selectedCity.progressPercent ?? 0 }) }}</p>
        <ul>
          <li>{{ t('worldMap.currency') }}: {{ selectedCity.currencyCode }}</li>
          <li>{{ t('worldMap.population') }}: {{ selectedCity.population.toLocaleString() }}</li>
          <li>{{ t('cityExpansion.currentNetWorth') }}: {{ formatCurrency(selectedCity.currentNetWorth ?? 0, selectedCity.currencyCode) }}</li>
          <li>{{ t('cityExpansion.requiredNetWorth') }}: {{ formatCurrency(selectedCity.requiredNetWorth ?? 0, selectedCity.currencyCode) }}</li>
          <li v-if="selectedCity.topResourceName">{{ t('worldMap.featuredResource') }}: {{ selectedCity.topResourceName }}</li>
        </ul>
        <div class="expansion-actions">
          <button class="btn btn-primary btn-sm" type="button" @click="startExpanding">
            {{ t('cityExpansion.growToUnlockCta') }}
          </button>
          <button class="btn btn-secondary btn-sm" type="button" @click="showExpansionModal = false">
            {{ t('common.close') }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import { gqlRequest } from '@/lib/graphql'
import { useAuthStore } from '@/stores/auth'
import { formatMoney } from '@/lib/currencyFormat'

interface ExpansionCity {
  id: string
  name: string
  countryCode: string
  currencyCode: string
  latitude: number
  longitude: number
  population: number
  isUnlocked: boolean
  availableLandPlots?: number
  activeCompanyCount?: number
  topResourceName?: string | null
  requiredNetWorth?: number
  currentNetWorth?: number
  progressPercent?: number
  estimatedTicksToUnlock?: number | null
}

const { t, locale } = useI18n()
const router = useRouter()
const auth = useAuthStore()

const loading = ref(true)
const error = ref<string | null>(null)
const cities = ref<ExpansionCity[]>([])
const selectedCityId = ref<string | null>(null)
const showExpansionModal = ref(false)
const mapEl = ref<HTMLElement | null>(null)

const selectedCity = computed(() => cities.value.find((city) => city.id === selectedCityId.value) ?? null)

let map: L.Map | null = null
const markersById = new Map<string, L.Marker>()

function markerSizeForPopulation(population: number): number {
  const minSize = 14
  const maxSize = 30
  const ratio = Math.max(0, Math.min(1, (population - 300_000) / 5_000_000))
  return Math.round(minSize + ratio * (maxSize - minSize))
}

function createMap() {
  if (!mapEl.value) return
  if (map) {
    map.remove()
    map = null
    markersById.clear()
  }

  map = L.map(mapEl.value, { zoomControl: true, scrollWheelZoom: true }).setView([48.15, 17.1], 4)
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; OpenStreetMap contributors',
  }).addTo(map)

  for (const city of cities.value) {
    const size = markerSizeForPopulation(city.population)
    const marker = L.marker([city.latitude, city.longitude], {
      icon: L.divIcon({
        className: `world-city-marker ${city.isUnlocked ? 'world-city-marker--mine' : 'world-city-marker--available'}`,
        html: `<span style="width:${size}px;height:${size}px;"></span>`,
      }),
    }).addTo(map)

    marker.bindTooltip(`${city.name} · ${city.currencyCode}`)
    marker.on('click', () => selectCity(city))
    markersById.set(city.id, marker)
  }
}

function selectCity(city: ExpansionCity) {
  selectedCityId.value = city.id
  showExpansionModal.value = !city.isUnlocked
  if (map) {
    map.flyTo([city.latitude, city.longitude], Math.max(map.getZoom(), 5), { duration: 0.5 })
  }
}

async function goToCity(city: ExpansionCity) {
  auth.switchCity(city.id)
  showExpansionModal.value = false
  await router.push(`/city/${city.id}`)
}

async function startExpanding() {
  showExpansionModal.value = false
  if (!auth.isAuthenticated) {
    await router.push('/login')
    return
  }

  const companyId = auth.player?.activeCompanyId ?? auth.player?.companies?.[0]?.id
  if (companyId) {
    await router.push(`/ledger/${companyId}`)
  } else {
    await router.push('/dashboard')
  }
}

function formatCurrency(amount: number, currencyCode: string): string {
  return formatMoney(amount, currencyCode, locale.value)
}

async function fetchWorldMap() {
  loading.value = true
  error.value = null
  try {
    if (auth.isAuthenticated && !auth.player) {
      await auth.fetchMe()
    }

    let response: { getCities?: ExpansionCity[]; cities?: ExpansionCity[] } | null = null
    try {
      response = await gqlRequest<{ getCities?: ExpansionCity[] }>(
        `
          query WorldMapCities {
            getCities {
              id
              name
              countryCode
              currencyCode
              latitude
              longitude
              population
              isUnlocked
              availableLandPlots
              activeCompanyCount
              topResourceName
              requiredNetWorth
              currentNetWorth
              progressPercent
              estimatedTicksToUnlock
            }
          }
        `,
      )
    } catch {
      response = null
    }

    if (!response?.getCities) {
      response = await gqlRequest<{ cities: ExpansionCity[] }>(
        `
          query WorldMapCitiesFallback {
            cities {
              id
              name
              countryCode
              currencyCode
              latitude
              longitude
              population
            }
          }
        `,
      )
    }

    const rawCities = response.getCities ?? response.cities ?? []
    const fallbackCities = response.cities ?? []

    const unlockedIds = new Set(
      (auth.player?.companies ?? []).flatMap((company) => company.buildings.map((building) => building.cityId)),
    )

    const normalizedCities = rawCities.length > 0 ? rawCities : fallbackCities
    cities.value = normalizedCities.map((city) => ({
      ...city,
      isUnlocked: city.isUnlocked || unlockedIds.has(city.id),
      availableLandPlots: city.availableLandPlots ?? 0,
      activeCompanyCount: city.activeCompanyCount ?? 0,
    }))

    selectedCityId.value = cities.value[0]?.id ?? null
    await nextTick()
    createMap()
  } catch {
    error.value = t('worldMap.loadFailed')
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  void fetchWorldMap()
})

onBeforeUnmount(() => {
  map?.remove()
})
</script>

<style scoped>
.world-map-page {
  max-width: 1440px;
  margin: 0 auto;
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.world-map-header h1 {
  margin: 0;
}

.world-map-header p {
  color: var(--color-text-secondary);
  margin-top: 0.4rem;
}

.world-map-layout {
  display: grid;
  grid-template-columns: minmax(0, 3fr) minmax(260px, 1fr);
  gap: 1rem;
}

.world-map-panel {
  border: 1px solid var(--color-divider);
  border-radius: 12px;
  background: var(--color-card);
  overflow: hidden;
}

.world-map-legend {
  display: flex;
  gap: 1rem;
  padding: 0.75rem 1rem;
  border-bottom: 1px solid var(--color-divider);
  color: var(--color-text-secondary);
  font-size: 0.85rem;
}

.dot {
  width: 10px;
  height: 10px;
  border-radius: 999px;
  display: inline-block;
  margin-right: 0.35rem;
}

.dot--mine {
  background: var(--color-primary);
}

.dot--available {
  background: #94a3b8;
}

.world-map-canvas {
  min-height: 560px;
}

:deep(.world-city-marker span) {
  border-radius: 999px;
  border: 2px solid rgba(0, 0, 0, 0.4);
  display: block;
}

:deep(.world-city-marker--mine span) {
  background: var(--color-primary);
}

:deep(.world-city-marker--available span) {
  background: #94a3b8;
}

.city-sidebar {
  border: 1px solid var(--color-divider);
  border-radius: 12px;
  background: var(--color-card);
  padding: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.city-sidebar h2 {
  margin: 0;
  font-size: 1rem;
}

.city-list {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  max-height: 260px;
  overflow-y: auto;
}

.city-item {
  border: 1px solid var(--color-divider);
  background: var(--color-surface);
  color: var(--color-text-primary);
  border-radius: 8px;
  padding: 0.5rem 0.75rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  cursor: pointer;
}

.city-item--active {
  border-color: var(--color-primary);
}

.city-detail {
  border-top: 1px solid var(--color-divider);
  padding-top: 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
}

.city-detail h3,
.city-detail p {
  margin: 0;
}

.world-map-state {
  border: 1px solid var(--color-divider);
  border-radius: 12px;
  padding: 2rem;
  text-align: center;
}

.world-map-error {
  color: var(--color-text-danger, #ef4444);
}

.expansion-modal-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.55);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1rem;
  z-index: 200;
}

.expansion-modal {
  width: min(520px, 100%);
  background: var(--color-card);
  border: 1px solid var(--color-divider);
  border-radius: 12px;
  padding: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.expansion-modal h2,
.expansion-modal p,
.expansion-modal ul {
  margin: 0;
}

.expansion-actions {
  display: flex;
  gap: 0.5rem;
  justify-content: flex-end;
}

.world-map-progress {
  width: 100%;
  height: 0.75rem;
  border-radius: 999px;
  background: rgba(148, 163, 184, 0.18);
  overflow: hidden;
}

.world-map-progress__fill {
  display: block;
  height: 100%;
  background: linear-gradient(90deg, var(--color-primary), var(--color-secondary));
}

.world-map-progress__label {
  margin: 0;
  color: var(--color-text-secondary);
  font-size: 0.82rem;
}

@media (max-width: 960px) {
  .world-map-layout {
    grid-template-columns: 1fr;
  }

  .world-map-canvas {
    min-height: 420px;
  }
}
</style>

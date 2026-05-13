<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { useTickRefresh } from '@/composables/useTickRefresh'

interface City {
  id: string
  name: string
  currencyCode: string
}

interface EnergyListing {
  listingId: string
  buildingId: string
  buildingName: string
  companyId: string
  companyName: string
  cityId: string
  plantType: string
  pricePerKwhLocal: number
  capacityKw: number
  availableKw: number
  createdAtTick: number
}

interface PowerPlantBuilding {
  id: string
  name: string
  cityId: string
  cityName: string
  cityCode: string
}

const { t } = useI18n()
const auth = useAuthStore()
const { player } = storeToRefs(auth)

const loading = ref(true)
const error = ref<string | null>(null)
const cities = ref<City[]>([])
const listings = ref<EnergyListing[]>([])
const myBuildings = ref<PowerPlantBuilding[]>([])
const selectedCityId = ref<string>('all')

// Listing modal state
const showListModal = ref(false)
const listBuildingId = ref('')
const listPrice = ref<number | string>('')
const listCapacity = ref<number | string>('')
const listSubmitting = ref(false)
const listError = ref<string | null>(null)

// Cancel state
const cancellingId = ref<string | null>(null)

const CITIES_QUERY = `{ cities { id name currencyCode } }`

const ENERGY_MARKET_QUERY = (cityId: string) => `{
  energyMarket(cityId: "${cityId}") {
    listingId buildingId buildingName companyId companyName cityId
    plantType pricePerKwhLocal capacityKw availableKw createdAtTick
  }
}`

const MY_PLANTS_QUERY = `{
  myCompanies {
    id
    buildings {
      id name type cityId
      city { id name currencyCode }
    }
  }
}`

const LIST_MUTATION = `mutation ListEnergyForSale($buildingId: UUID!, $pricePerKwhLocal: Decimal!, $capacityKw: Decimal!) {
  listEnergyForSale(input: { buildingId: $buildingId, pricePerKwhLocal: $pricePerKwhLocal, capacityKw: $capacityKw }) {
    listingId buildingId companyId cityId plantType pricePerKwhLocal capacityKw availableKw createdAtTick
  }
}`

const CANCEL_MUTATION = `mutation CancelEnergyListing($listingId: UUID!) {
  cancelEnergyListing(input: { listingId: $listingId })
}`

const filteredListings = computed(() => {
  if (selectedCityId.value === 'all') return listings.value
  return listings.value.filter((l) => l.cityId === selectedCityId.value)
})

const cityById = computed(() => {
  const m: Record<string, City> = {}
  for (const c of cities.value) m[c.id] = c
  return m
})

const myOwnedBuildingIds = computed(() => new Set(myBuildings.value.map((b) => b.id)))

function isOwnListing(listing: EnergyListing) {
  return myOwnedBuildingIds.value.has(listing.buildingId)
}

function plantTypeLabel(type: string) {
  const map: Record<string, string> = {
    COAL: t('energyMarket.plantCoal'),
    GAS: t('energyMarket.plantGas'),
    NUCLEAR: t('energyMarket.plantNuclear'),
    SOLAR: t('energyMarket.plantSolar'),
    WIND: t('energyMarket.plantWind'),
  }
  return map[type] ?? type
}

function plantTypeBadgeClass(type: string) {
  const map: Record<string, string> = {
    COAL: 'badge-coal',
    GAS: 'badge-gas',
    NUCLEAR: 'badge-nuclear',
    SOLAR: 'badge-solar',
    WIND: 'badge-wind',
  }
  return map[type] ?? 'badge-coal'
}

async function loadData(isRefresh = false) {
  if (!isRefresh) loading.value = true
  error.value = null
  try {
    const cityData = await gqlRequest<{ cities: City[] }>(CITIES_QUERY)
    cities.value = cityData.cities ?? []

    const allListings: EnergyListing[] = []
    for (const city of cities.value) {
      try {
        const res = await gqlRequest<{ energyMarket: EnergyListing[] }>(
          ENERGY_MARKET_QUERY(city.id),
        )
        allListings.push(...(res.energyMarket ?? []))
      } catch {
        // ignore per-city errors
      }
    }
    listings.value = allListings

    if (player.value) {
      try {
        const me = await gqlRequest<{
          myCompanies: Array<{
            id: string
            buildings: Array<{
              id: string
              name: string
              type: string
              cityId: string
              city: { id: string; name: string; currencyCode: string }
            }>
          }>
        }>(MY_PLANTS_QUERY)
        myBuildings.value = (me.myCompanies ?? []).flatMap((c) =>
          (c.buildings ?? [])
            .filter((b) => b.type === 'POWER_PLANT')
            .map((b) => ({
              id: b.id,
              name: b.name,
              cityId: b.cityId,
              cityName: b.city?.name ?? '',
              cityCode: b.city?.currencyCode ?? '',
            })),
        )
      } catch {
        // ignore
      }
    }
  } catch {
    error.value = t('energyMarket.loadFailed')
  } finally {
    loading.value = false
  }
}

async function cancelListing(listingId: string) {
  if (!confirm(t('energyMarket.cancelConfirm'))) return
  cancellingId.value = listingId
  try {
    await gqlRequest(CANCEL_MUTATION, { listingId })
    await loadData(true)
  } catch {
    alert(t('energyMarket.cancelFailed'))
  } finally {
    cancellingId.value = null
  }
}

async function submitListing() {
  listError.value = null
  const priceVal = parseFloat(String(listPrice.value))
  const capVal = parseFloat(String(listCapacity.value))
  if (!listBuildingId.value || isNaN(priceVal) || priceVal <= 0 || isNaN(capVal) || capVal <= 0) {
    listError.value = 'Please fill in all fields with valid values.'
    return
  }
  listSubmitting.value = true
  try {
    await gqlRequest(LIST_MUTATION, {
      buildingId: listBuildingId.value,
      pricePerKwhLocal: priceVal,
      capacityKw: capVal,
    })
    showListModal.value = false
    listPrice.value = ''
    listCapacity.value = ''
    listBuildingId.value = ''
    await loadData(true)
  } catch {
    listError.value = t('energyMarket.listFailed')
  } finally {
    listSubmitting.value = false
  }
}

onMounted(() => loadData())
useTickRefresh(() => loadData(true))
</script>

<template>
  <div class="energy-market-view">
    <div class="page-header">
      <p class="eyebrow">{{ t('energyMarket.eyebrow') }}</p>
      <h1>{{ t('energyMarket.title') }}</h1>
      <p class="subtitle">{{ t('energyMarket.subtitle') }}</p>
    </div>

    <div v-if="loading" class="loading-state">
      <span class="spinner" />
    </div>

    <div v-else-if="error" class="error-state">{{ error }}</div>

    <template v-else>
      <!-- Controls bar -->
      <div class="controls-bar">
        <div class="city-filter">
          <label for="city-filter-select">{{ t('energyMarket.filterByCity') }}</label>
          <select id="city-filter-select" v-model="selectedCityId" class="city-select">
            <option value="all">{{ t('energyMarket.allCities') }}</option>
            <option v-for="city in cities" :key="city.id" :value="city.id">{{ city.name }}</option>
          </select>
        </div>

        <button
          v-if="player && myBuildings.length > 0"
          class="btn-primary list-btn"
          @click="showListModal = true"
        >
          ⚡ {{ t('energyMarket.listSurplus') }}
        </button>
      </div>

      <!-- Energy crisis banner -->
      <div
        v-if="filteredListings.length === 0"
        class="energy-crisis-banner"
      >
        <div class="crisis-content">
          <h2>{{ t('energyMarket.energyCrisis') }}</h2>
          <p>{{ t('energyMarket.energyCrisisDesc') }}</p>
          <router-link to="/city/all" class="btn-secondary">
            {{ t('energyMarket.buildPowerPlant') }}
          </router-link>
        </div>
      </div>

      <!-- Listings table -->
      <div v-else class="listings-section">
        <table class="listings-table">
          <thead>
            <tr>
              <th>{{ t('energyMarket.colCity') }}</th>
              <th>{{ t('energyMarket.colSeller') }}</th>
              <th>{{ t('energyMarket.colPlantType') }}</th>
              <th class="num-col">{{ t('energyMarket.colPrice') }}</th>
              <th class="num-col">{{ t('energyMarket.colCapacity') }}</th>
              <th class="num-col">{{ t('energyMarket.colAvailable') }}</th>
              <th>{{ t('energyMarket.colActions') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="listing in filteredListings" :key="listing.listingId">
              <td>{{ cityById[listing.cityId]?.name ?? listing.cityId }}</td>
              <td>
                {{ listing.companyName }}
                <span v-if="isOwnListing(listing)" class="own-badge">{{
                  t('energyMarket.ownListing')
                }}</span>
              </td>
              <td>
                <span :class="['plant-badge', plantTypeBadgeClass(listing.plantType)]">
                  {{ plantTypeLabel(listing.plantType) }}
                </span>
              </td>
              <td class="num-col mono">
                {{ listing.pricePerKwhLocal.toFixed(4) }}
                {{ cityById[listing.cityId]?.currencyCode ?? '' }}
              </td>
              <td class="num-col mono">{{ listing.capacityKw.toFixed(1) }}</td>
              <td class="num-col mono">{{ listing.availableKw.toFixed(1) }}</td>
              <td>
                <button
                  v-if="isOwnListing(listing)"
                  class="btn-danger btn-sm"
                  :disabled="cancellingId === listing.listingId"
                  @click="cancelListing(listing.listingId)"
                >
                  {{ cancellingId === listing.listingId ? '…' : t('energyMarket.cancelListing') }}
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>

    <!-- List Surplus Modal -->
    <div v-if="showListModal" class="modal-overlay" @click.self="showListModal = false">
      <div class="modal-box">
        <h2>{{ t('energyMarket.listTitle') }}</h2>
        <div v-if="listError" class="modal-error">{{ listError }}</div>

        <div class="form-field">
          <label>{{ t('energyMarket.listBuilding') }}</label>
          <select v-model="listBuildingId" class="form-select">
            <option value="">— select —</option>
            <option v-for="b in myBuildings" :key="b.id" :value="b.id">
              {{ b.name }} ({{ b.cityName }})
            </option>
          </select>
        </div>

        <div class="form-field">
          <label>{{ t('energyMarket.listPriceLabel') }}</label>
          <input
            v-model="listPrice"
            type="number"
            min="0.0001"
            step="0.0001"
            class="form-input"
            placeholder="e.g. 0.05"
          />
        </div>

        <div class="form-field">
          <label>{{ t('energyMarket.listCapacityLabel') }}</label>
          <input
            v-model="listCapacity"
            type="number"
            min="1"
            step="1"
            class="form-input"
            placeholder="e.g. 500"
          />
        </div>

        <div class="modal-actions">
          <button class="btn-secondary" @click="showListModal = false">Cancel</button>
          <button class="btn-primary" :disabled="listSubmitting" @click="submitListing">
            {{ listSubmitting ? '…' : t('energyMarket.listSubmit') }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.energy-market-view {
  max-width: 1100px;
  margin: 0 auto;
  padding: var(--space-6) var(--space-4);
}

.page-header {
  margin-bottom: var(--space-6);
}

.page-header .eyebrow {
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: var(--color-accent);
  margin-bottom: var(--space-1);
}

.page-header h1 {
  font-size: 1.75rem;
  font-weight: 700;
  color: var(--color-text-primary);
  margin: 0 0 var(--space-1);
}

.page-header .subtitle {
  color: var(--color-text-secondary);
  font-size: 0.9rem;
}

.loading-state {
  display: flex;
  justify-content: center;
  padding: var(--space-12);
}

.spinner {
  width: 32px;
  height: 32px;
  border: 3px solid var(--color-border);
  border-top-color: var(--color-accent);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.error-state {
  color: var(--color-danger);
  padding: var(--space-4);
  background: var(--color-danger-soft);
  border-radius: var(--radius-md);
}

.controls-bar {
  display: flex;
  align-items: center;
  gap: var(--space-4);
  margin-bottom: var(--space-5);
  flex-wrap: wrap;
}

.city-filter {
  display: flex;
  align-items: center;
  gap: var(--space-2);
}

.city-filter label {
  font-size: 0.85rem;
  color: var(--color-text-secondary);
}

.city-select {
  padding: var(--space-2) var(--space-3);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  color: var(--color-text-primary);
  font-size: 0.875rem;
}

.list-btn {
  margin-left: auto;
}

.energy-crisis-banner {
  background: linear-gradient(135deg, #1a0a00, #2d1800);
  border: 1px solid #f97316;
  border-radius: var(--radius-lg);
  padding: var(--space-8) var(--space-6);
  text-align: center;
  margin-top: var(--space-4);
}

.crisis-content h2 {
  font-size: 1.5rem;
  font-weight: 700;
  color: #f97316;
  margin-bottom: var(--space-2);
}

.crisis-content p {
  color: var(--color-text-secondary);
  margin-bottom: var(--space-4);
}

.listings-section {
  overflow-x: auto;
}

.listings-table {
  width: 100%;
  border-collapse: collapse;
  font-family: ui-monospace, 'Cascadia Code', 'Source Code Pro', monospace;
  font-size: 0.875rem;
}

.listings-table th {
  text-align: left;
  padding: var(--space-3) var(--space-4);
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--color-text-secondary);
  background: var(--color-surface);
  border-bottom: 1px solid var(--color-border);
}

.listings-table td {
  padding: var(--space-3) var(--space-4);
  border-bottom: 1px solid var(--color-border-soft);
  vertical-align: middle;
}

.listings-table tr:hover td {
  background: var(--color-hover);
}

.num-col {
  text-align: right;
}

.mono {
  font-family: ui-monospace, 'Cascadia Code', monospace;
}

.own-badge {
  display: inline-block;
  font-size: 0.65rem;
  background: var(--color-accent-soft);
  color: var(--color-accent);
  border-radius: var(--radius-sm);
  padding: 1px 5px;
  margin-left: 4px;
  font-family: sans-serif;
  vertical-align: middle;
}

.plant-badge {
  display: inline-block;
  padding: 2px 8px;
  border-radius: var(--radius-sm);
  font-size: 0.75rem;
  font-family: sans-serif;
  font-weight: 600;
}

.badge-coal {
  background: #4a3728;
  color: #d4a574;
}
.badge-gas {
  background: #1a3a4a;
  color: #74c4d4;
}
.badge-nuclear {
  background: #2a3a1a;
  color: #b4d474;
}
.badge-solar {
  background: #3a3a1a;
  color: #e4d474;
}
.badge-wind {
  background: #1a2a3a;
  color: #74a4d4;
}

.btn-primary {
  padding: var(--space-2) var(--space-4);
  background: var(--color-accent);
  color: #fff;
  border: none;
  border-radius: var(--radius-sm);
  cursor: pointer;
  font-size: 0.875rem;
  font-weight: 600;
}
.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-secondary {
  padding: var(--space-2) var(--space-4);
  background: var(--color-surface);
  color: var(--color-text-primary);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  cursor: pointer;
  font-size: 0.875rem;
}

.btn-danger {
  padding: var(--space-1) var(--space-3);
  background: var(--color-danger-soft);
  color: var(--color-danger);
  border: 1px solid var(--color-danger);
  border-radius: var(--radius-sm);
  cursor: pointer;
  font-size: 0.8rem;
}
.btn-danger:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-sm {
  font-size: 0.75rem;
  padding: 2px var(--space-2);
}

/* Modal */
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.6);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-box {
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: var(--space-6);
  min-width: 360px;
  max-width: 480px;
  width: 100%;
}

.modal-box h2 {
  margin: 0 0 var(--space-4);
  font-size: 1.1rem;
  font-weight: 700;
}

.modal-error {
  color: var(--color-danger);
  font-size: 0.875rem;
  margin-bottom: var(--space-3);
}

.form-field {
  margin-bottom: var(--space-4);
}

.form-field label {
  display: block;
  font-size: 0.8rem;
  color: var(--color-text-secondary);
  margin-bottom: var(--space-1);
}

.form-select,
.form-input {
  width: 100%;
  padding: var(--space-2) var(--space-3);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  color: var(--color-text-primary);
  font-size: 0.875rem;
  box-sizing: border-box;
}

.modal-actions {
  display: flex;
  gap: var(--space-3);
  justify-content: flex-end;
  margin-top: var(--space-4);
}
</style>

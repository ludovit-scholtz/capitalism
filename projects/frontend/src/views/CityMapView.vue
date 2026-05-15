<template>
  <div class="city-map-view container">
    <div class="page-header">
      <div class="header-main">
        <button class="btn btn-secondary btn-sm" @click="router.push('/dashboard')">← {{ t('cityMap.backToDashboard') }}</button>
        <h1 v-if="city">🏙️ {{ city.name }} — {{ t('cityMap.title') }}</h1>
        <p class="subtitle">{{ t('cityMap.subtitle') }}</p>
      </div>
      <div v-if="city" class="city-context">
        <span class="context-chip">{{ city.currencyCode }}</span>
        <span class="context-chip">{{ city.population.toLocaleString() }}</span>
      </div>
    </div>

    <nav class="city-tabs" aria-label="City tabs">
      <RouterLink
        v-for="tab in tabs"
        :key="tab.name"
        :to="{ name: tab.name, params: { cityId } }"
        class="city-tab"
        :class="{ 'city-tab--active': activeTab === tab.key }"
      >
        <span>{{ tab.icon }}</span>
        <span>{{ t(tab.labelKey) }}</span>
      </RouterLink>
    </nav>

    <div v-if="loading" class="loading">{{ t('common.loading') }}</div>

    <div v-else-if="error" class="error-message" role="alert">
      {{ error }}
      <button class="btn btn-secondary" @click="fetchAll()">{{ t('common.tryAgain') }}</button>
    </div>

    <section v-else-if="city && isCityLocked" class="city-lock-shell">
      <div class="city-lock-backdrop" aria-hidden="true">
        <div class="city-lock-backdrop__panel">
          <p>{{ t('cityExpansion.mapBackdropTitle', { city: city.name }) }}</p>
          <div class="city-lock-backdrop__chips">
            <span class="context-chip">{{ city.currencyCode }}</span>
            <span class="context-chip">{{ city.population.toLocaleString() }}</span>
          </div>
        </div>
      </div>

      <div class="city-lock-overlay">
        <CountryFlag :country-code="city.countryCode" size="lg" :title="city.countryCode" class="city-lock-overlay__flag" />
        <p class="city-lock-overlay__eyebrow">{{ t('cityExpansion.lockedBadge') }}</p>
        <h2 class="city-lock-overlay__title">{{ city.name }}</h2>
        <p class="city-lock-overlay__copy">
          {{ t('cityExpansion.mapLockedBody', { city: city.name, amount: formatUnlockAmount(cityUnlockStatus?.requiredNetWorth ?? 0, cityUnlockStatus?.currency ?? city.currencyCode) }) }}
        </p>

        <div class="city-lock-overlay__progress" aria-hidden="true">
          <span class="city-lock-overlay__progress-fill" :style="{ width: `${cityUnlockProgress}%` }"></span>
        </div>
        <p class="city-lock-overlay__progress-label">{{ t('cityExpansion.progressLabel', { percent: cityUnlockProgress }) }}</p>

        <dl class="city-lock-overlay__metrics">
          <div>
            <dt>{{ t('cityExpansion.currentNetWorth') }}</dt>
            <dd>{{ formatUnlockAmount(cityUnlockStatus?.currentNetWorth ?? 0, cityUnlockStatus?.currency ?? city.currencyCode) }}</dd>
          </div>
          <div>
            <dt>{{ t('cityExpansion.requiredNetWorth') }}</dt>
            <dd>{{ formatUnlockAmount(cityUnlockStatus?.requiredNetWorth ?? 0, cityUnlockStatus?.currency ?? city.currencyCode) }}</dd>
          </div>
          <div>
            <dt>{{ t('cityExpansion.estimatedTicks') }}</dt>
            <dd>
              {{
                cityUnlockStatus?.estimatedTicksToUnlock != null
                  ? t('cityExpansion.estimatedTicksValue', { ticks: formatEstimatedTicksLabel(cityUnlockStatus.estimatedTicksToUnlock, locale) })
                  : t('cityExpansion.estimateUnavailable')
              }}
            </dd>
          </div>
        </dl>

        <button class="btn btn-primary city-lock-overlay__cta" @click="goToExpansionProgress">
          {{ t('cityExpansion.growToUnlockCta') }}
        </button>
      </div>
    </section>

    <RouterView v-else-if="city" v-slot="{ Component }">
      <component
        :is="Component"
        :city="city"
        :city-id="cityId"
        :lots="lots"
        :companies="companies"
        :npc-companies="npcCompanies"
        :city-weather="cityWeather"
        :city-power-balance="cityPowerBalance"
        :city-economic-report="cityEconomicReport"
        :economic-report-loading="economicReportLoading"
        :city-media-houses="cityMediaHouses"
        :media-houses-loading="mediaHousesLoading"
        :highlighted-building-id="highlightedBuildingId"
        :economy-cycle="economyCycle"
        :active-market-events="activeMarketEvents"
        :economic-history="economicHistory"
        @purchase-complete="handlePurchaseComplete"
        @lot-refreshed="handleLotRefreshed"
      />
    </RouterView>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import CountryFlag from '@/components/common/CountryFlag.vue'
import { computeCityUnlockProgress, formatEstimatedTicksLabel } from '@/lib/cityExpansion'
import { formatMoney } from '@/lib/currencyFormat'
import type { City, CityUnlockStatus, BuildingLot, Company, PurchaseLotResult, CityMediaHouseInfo, CityWeatherForecast, CityPowerBalance, CityEconomicReportResult, EconomicCycleView, MarketEventView, EconomicCycleHistoryPoint, NpcCompanySummary } from '@/types'

const { t, locale } = useI18n()
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const { selectedCityId } = storeToRefs(auth)

const tabs = [
  { key: 'overview', name: 'city-map', icon: '📍', labelKey: 'cityMap.tabs.overview' },
  { key: 'economy', name: 'city-economy', icon: '📈', labelKey: 'cityMap.tabs.economy' },
  { key: 'buildings', name: 'city-buildings', icon: '🏗️', labelKey: 'cityMap.tabs.buildings' },
  { key: 'market', name: 'city-market', icon: '🛒', labelKey: 'cityMap.tabs.market' },
  { key: 'competitors', name: 'city-competitors', icon: '🏁', labelKey: 'cityMap.tabs.competitors' },
] as const

const cityId = computed(() => route.params.cityId as string)
const activeTab = computed(() =>
  route.name === 'city-economy'
    ? 'economy'
    : route.name === 'city-buildings'
      ? 'buildings'
      : route.name === 'city-market'
        ? 'market'
        : route.name === 'city-competitors'
          ? 'competitors'
          : 'overview',
)
const highlightedBuildingId = computed(() => (typeof route.query.building === 'string' ? route.query.building : null))

const loading = ref(true)
const error = ref<string | null>(null)
const city = ref<City | null>(null)
const cityUnlockStatus = ref<CityUnlockStatus | null>(null)
const lots = ref<BuildingLot[]>([])
const companies = ref<Company[]>([])
const npcCompanies = ref<NpcCompanySummary[]>([])

const cityWeather = ref<CityWeatherForecast | null>(null)
const cityPowerBalance = ref<CityPowerBalance | null>(null)
const cityEconomicReport = ref<CityEconomicReportResult | null>(null)
const economicReportLoading = ref(false)

const cityMediaHouses = ref<CityMediaHouseInfo[]>([])
const mediaHousesLoading = ref(false)

const economyCycle = ref<EconomicCycleView | null>(null)
const activeMarketEvents = ref<MarketEventView[]>([])
const economicHistory = ref<EconomicCycleHistoryPoint[]>([])
const activeCompanyId = computed(() => auth.player?.activeCompanyId ?? auth.player?.companies?.[0]?.id ?? null)
const isCityLocked = computed(() => auth.isAuthenticated && cityUnlockStatus.value != null && !cityUnlockStatus.value.isUnlocked)
const cityUnlockProgress = computed(() =>
  cityUnlockStatus.value
    ? computeCityUnlockProgress(cityUnlockStatus.value)
    : 0,
)

function formatUnlockAmount(amount: number, currencyCode: string): string {
  return formatMoney(amount, currencyCode, locale.value)
}

async function fetchCityUnlockStatus() {
  if (!auth.isAuthenticated) {
    cityUnlockStatus.value = null
    return
  }

  try {
    const data = await gqlRequest<{ cityUnlockStatus: CityUnlockStatus | null }>(
      `query CityUnlockStatus($cityId: UUID!) {
        cityUnlockStatus(cityId: $cityId) {
          cityId
          cityName
          countryCode
          isUnlocked
          requiredNetWorth
          currentNetWorth
          currency
          progressPercent
          estimatedTicksToUnlock
          companyId
        }
      }`,
      { cityId: cityId.value },
    )
    cityUnlockStatus.value = data.cityUnlockStatus ?? null
  } catch {
    cityUnlockStatus.value = null
  }
}

async function fetchData() {
  loading.value = true
  error.value = null
  try {
    if (auth.isAuthenticated && !auth.player) {
      await auth.fetchMe()
    }

    const [cityData, companiesData, npcData] = await Promise.all([
      gqlRequest<{ city: City }>(
        `query GetCity($id: UUID!) {
          city(id: $id) {
            id name countryCode latitude longitude population currencyCode
            resources { resourceType { id name slug category } abundance }
          }
        }`,
        { id: cityId.value },
      ),
      auth.isAuthenticated ? gqlRequest<{ myCompanies: Company[] }>(`{ myCompanies { id name cash foundedAtUtc buildings { id } } }`) : Promise.resolve({ myCompanies: [] as Company[] }),
      gqlRequest<{ npcCompanies: NpcCompanySummary[] }>(
        `query NpcCompanies($cityId: UUID) {
          npcCompanies(cityId: $cityId) {
            id
            companyId
            name
            archetype
            difficultyLevel
            homeCityId
            homeCityName
            isActive
            createdAtUtc
            buildingCount
          }
        }`,
        { cityId: cityId.value },
      ),
    ])

    city.value = cityData.city
    companies.value = companiesData.myCompanies
    npcCompanies.value = npcData.npcCompanies ?? []

    await fetchCityUnlockStatus()
    if (isCityLocked.value) {
      lots.value = []
      cityWeather.value = null
      cityPowerBalance.value = null
      cityEconomicReport.value = null
      cityMediaHouses.value = []
      economyCycle.value = null
      activeMarketEvents.value = []
      economicHistory.value = []
      return
    }

    const lotsData = await gqlRequest<{ cityLots: BuildingLot[] }>(
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
    )

    lots.value = lotsData.cityLots
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

async function fetchEconomyData() {
  if (!cityId.value) return
  try {
    const data = await gqlRequest<{
      getCurrentEconomicCycle: EconomicCycleView | null
      getActiveMarketEvents: MarketEventView[]
      getEconomicHistory: EconomicCycleHistoryPoint[]
    }>(
      `query CityEconomy($cityId: UUID!) {
        getCurrentEconomicCycle {
          id phase phaseStartedTick expectedDurationTicks intensityFactor phaseEndTick ticksRemaining
        }
        getActiveMarketEvents(cityId: $cityId) {
          id eventType title description magnitudeMultiplier startsAtTick expiresAtTick ticksRemaining
          affectedResourceTypeId affectedResourceName affectedResourceSlug
          affectedCityId affectedCityName
        }
        getEconomicHistory(last: 365) {
          tick phase intensityFactor
        }
      }`,
      { cityId: cityId.value },
    )

    economyCycle.value = data.getCurrentEconomicCycle
    activeMarketEvents.value = data.getActiveMarketEvents ?? []
    economicHistory.value = data.getEconomicHistory ?? []
  } catch {
    economyCycle.value = null
    activeMarketEvents.value = []
    economicHistory.value = []
  }
}

async function fetchAll() {
  await fetchData()
  if (isCityLocked.value) {
    return
  }

  await Promise.all([fetchMediaHouses(), fetchWeatherForecast(), fetchCityPowerBalance(), fetchCityEconomicReport(), fetchEconomyData()])
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

async function goToExpansionProgress() {
  const targetCompanyId = cityUnlockStatus.value?.companyId ?? activeCompanyId.value
  if (targetCompanyId) {
    await router.push(`/ledger/${targetCompanyId}`)
    return
  }

  await router.push('/dashboard')
}

watch(cityId, async () => {
  if (selectedCityId.value !== cityId.value) {
    auth.switchCity(cityId.value)
  }

  cityWeather.value = null
  cityPowerBalance.value = null
  cityEconomicReport.value = null
  cityMediaHouses.value = []
  npcCompanies.value = []
  economyCycle.value = null
  activeMarketEvents.value = []
  economicHistory.value = []

  await fetchAll()
})

onMounted(async () => {
  if (selectedCityId.value !== cityId.value) {
    auth.switchCity(cityId.value)
  }

  await fetchAll()
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
  margin-bottom: 0.75rem;
  flex-wrap: wrap;
}

.header-main h1 {
  font-size: 1.5rem;
  margin: 0.5rem 0 0.25rem;
}

.subtitle {
  color: var(--color-text-secondary);
  font-size: 0.875rem;
  margin: 0;
}

.city-context {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.context-chip {
  background: var(--color-card);
  border: 1px solid var(--color-border);
  border-radius: 999px;
  padding: 0.2rem 0.7rem;
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.city-tabs {
  display: flex;
  gap: 0.5rem;
  flex-wrap: nowrap;
  overflow-x: auto;
  padding-bottom: 0.5rem;
  margin-bottom: 1rem;
  position: sticky;
  top: 0.5rem;
  z-index: 4;
  background: var(--color-bg);
}

.city-tab {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  padding: 0.45rem 0.85rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  color: var(--color-text-secondary);
  text-decoration: none;
  white-space: nowrap;
  transition: all 0.15s;
}

.city-tab:hover {
  border-color: var(--color-primary);
  color: var(--color-text);
}

.city-tab--active {
  border-color: var(--color-primary);
  color: var(--color-primary);
  background: rgba(0, 71, 255, 0.08);
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

.city-lock-shell {
  position: relative;
  min-height: 28rem;
  overflow: hidden;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  background: linear-gradient(135deg, rgba(15, 23, 42, 0.92), rgba(30, 41, 59, 0.82));
}

.city-lock-backdrop {
  position: absolute;
  inset: 0;
  padding: 2rem;
  filter: blur(12px);
  opacity: 0.55;
}

.city-lock-backdrop__panel {
  height: 100%;
  border: 1px solid rgba(255, 255, 255, 0.12);
  border-radius: var(--radius-lg);
  background: rgba(255, 255, 255, 0.06);
  padding: 1.5rem;
}

.city-lock-backdrop__chips {
  margin-top: 1rem;
  display: flex;
  gap: 0.75rem;
}

.city-lock-overlay {
  position: relative;
  z-index: 1;
  margin: 2rem auto;
  max-width: 38rem;
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 1.25rem;
  background: rgba(15, 23, 42, 0.72);
  backdrop-filter: blur(18px);
  padding: 2rem;
  color: #f8fafc;
  box-shadow: 0 24px 80px rgba(15, 23, 42, 0.35);
}

.city-lock-overlay__flag {
  margin-bottom: 0.75rem;
}

.city-lock-overlay__eyebrow {
  margin: 0 0 0.35rem;
  font-size: 0.75rem;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: #fbbf24;
}

.city-lock-overlay__title {
  margin: 0;
  font-size: 1.75rem;
}

.city-lock-overlay__copy {
  margin: 0.9rem 0 1rem;
  color: rgba(248, 250, 252, 0.86);
}

.city-lock-overlay__progress {
  width: 100%;
  height: 0.8rem;
  border-radius: 999px;
  background: rgba(148, 163, 184, 0.18);
  overflow: hidden;
}

.city-lock-overlay__progress-fill {
  display: block;
  height: 100%;
  background: linear-gradient(90deg, var(--color-primary), #22c55e);
}

.city-lock-overlay__progress-label {
  margin: 0.55rem 0 1rem;
  color: rgba(248, 250, 252, 0.76);
  font-size: 0.84rem;
}

.city-lock-overlay__metrics {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr));
  gap: 1rem;
  margin: 0 0 1.25rem;
}

.city-lock-overlay__metrics dt {
  margin-bottom: 0.25rem;
  font-size: 0.72rem;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: rgba(248, 250, 252, 0.68);
}

.city-lock-overlay__metrics dd {
  margin: 0;
  font-size: 0.95rem;
  font-weight: 600;
}

.city-lock-overlay__cta {
  width: 100%;
  justify-content: center;
}
</style>

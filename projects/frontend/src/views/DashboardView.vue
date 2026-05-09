<script setup lang="ts">
import { ref, onMounted, computed, onUnmounted } from 'vue'
import { storeToRefs } from 'pinia'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { useGameStateStore } from '@/stores/gameState'
import { gqlRequest } from '@/lib/graphql'
import { useTickRefresh } from '@/composables/useTickRefresh'
import { useTickCountdown } from '@/composables/useTickCountdown'
import { useScrollPreservation } from '@/composables/useScrollPreservation'
import { useFirstTimeUserGates } from '@/composables/useFirstTimeUserGates'
import { deepEqual } from '@/lib/utils'
import { getActiveCompany } from '@/lib/accountContext'
import { formatInGameTime } from '@/lib/gameTime'
import TutorialTooltip from '@/components/ui/TutorialTooltip.vue'
import DashboardMainContent from '@/components/dashboard/DashboardMainContent.vue'
import type {
  Company,
  GameState,
  ScheduledActionSummary,
  CityPowerBalance,
  CompanyLedgerSummary,
  City,
  BuildingUnitOperationalStatus,
  EconomicCycleView,
  MarketEventView,
  EconomicCycleHistoryPoint,
} from '@/types'

// Module-level cache for city names - cities are static and never change during a session.
const _cityNamesCache: Record<string, string> = {}
// Module-level cache for city currencies - cities are static and never change during a session.
const _cityCurrenciesCache: Record<string, string> = {}

const { t, locale } = useI18n()
const router = useRouter()
const auth = useAuthStore()
const gameStateStore = useGameStateStore()
const { gameState } = storeToRefs(gameStateStore)

const companies = ref<Company[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const pendingActions = ref<ScheduledActionSummary[]>([])
const pendingActionsLoading = ref(false)
const cityPowerBalances = ref<Record<string, CityPowerBalance>>({})
const companyLedgers = ref<Record<string, CompanyLedgerSummary>>({})
const ledgerLoading = ref(false)
const cityNames = ref<Record<string, string>>({})
const cityCurrencies = ref<Record<string, string>>({})
const economicCycle = ref<EconomicCycleView | null>(null)
const activeMarketEvents = ref<MarketEventView[]>([])
const economicHistory = ref<EconomicCycleHistoryPoint[]>([])
/** Map from buildingId -> per-unit operational statuses for supply-chain live status display. */
const buildingUnitStatuses = ref<Record<string, BuildingUnitOperationalStatus[]>>({})
/** Map from buildingId -> aggregated financial totals (revenue, costs, profit). */
const buildingFinancials = ref<Record<string, { totalSales: number; totalCosts: number; totalProfit: number }>>({})
const buildingFinancialsLoading = ref(false)

const { tickCountdown, startTickCountdown, stopTickCountdown } = useTickCountdown(gameState)
const { saveScrollPosition, restoreScrollPosition } = useScrollPreservation()

// ── First-time user tutorial tooltip ────────────────────────────────────────
const {
  showDashboardTooltip,
  hydrateFromBackend,
  dismissDashboardTooltip,
} = useFirstTimeUserGates()

// Delay tooltip 1 s so the page renders before showing the overlay
const dashboardTooltipReady = ref(false)

const activeCompany = computed(() => getActiveCompany(auth.player, companies.value))
const formattedGameTime = computed(() => (gameState.value?.currentGameTimeUtc ? formatInGameTime(gameState.value.currentGameTimeUtc, locale.value) : ''))

async function loadDashboardData() {
  const companiesData = await gqlRequest<{ myCompanies: Company[] }>(
    `{ myCompanies {
      id name cash foundedAtUtc
      buildings { id name type level cityId powerStatus isAdvertisingActive lotMaterialQuantity lotOriginalMaterialQuantity destroyedAtUtc hasDefaultedCollateralLoan units { id unitType gridX gridY level } }
    } }`,
  )

  if (!deepEqual(companies.value, companiesData.myCompanies)) {
    companies.value = companiesData.myCompanies
  }
}

function applyCityMaps(cities: City[]) {
  const nameMap: Record<string, string> = {}
  const currencyMap: Record<string, string> = {}
  for (const city of cities) {
    nameMap[city.id] = city.name
    _cityNamesCache[city.id] = city.name
    currencyMap[city.id] = city.currencyCode
    _cityCurrenciesCache[city.id] = city.currencyCode
  }
  cityNames.value = nameMap
  cityCurrencies.value = currencyMap
}

async function refreshCompanyDerivedData() {
  const companiesForDerived = activeCompany.value ? [activeCompany.value] : []
  const cityIds = [...new Set(companiesForDerived.flatMap((company) => company.buildings.map((building) => building.cityId)))]
  const companyIds = companiesForDerived.map((company) => company.id)
  const buildingIds = companiesForDerived.flatMap((company) => company.buildings.map((building) => building.id))

  await Promise.all([loadCityPowerBalances(cityIds), loadCityNames(), loadLedgers(companyIds), loadBuildingUnitStatuses(buildingIds), loadBuildingFinancials(buildingIds)])
}

async function loadEconomyData() {
  try {
    const data = await gqlRequest<{
      getCurrentEconomicCycle: EconomicCycleView | null
      getActiveMarketEvents: MarketEventView[]
      getEconomicHistory: EconomicCycleHistoryPoint[]
    }>(`{
      getCurrentEconomicCycle {
        id phase phaseStartedTick expectedDurationTicks intensityFactor phaseEndTick ticksRemaining
      }
      getActiveMarketEvents {
        id eventType title description magnitudeMultiplier startsAtTick expiresAtTick ticksRemaining
        affectedResourceTypeId affectedResourceName affectedResourceSlug
        affectedCityId affectedCityName
      }
      getEconomicHistory(last: 365) {
        tick phase intensityFactor
      }
    }`)

    economicCycle.value = data.getCurrentEconomicCycle
    activeMarketEvents.value = data.getActiveMarketEvents ?? []
    economicHistory.value = data.getEconomicHistory ?? []
  } catch {
    economicCycle.value = null
    activeMarketEvents.value = []
    economicHistory.value = []
  }
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    router.push('/login')
    return
  }

  const _loadStart = performance.now()

  try {
    if (!auth.player) {
      await auth.fetchMe()
    }
    if (auth.player && !auth.player.onboardingCompletedAtUtc) {
      router.push('/onboarding')
      return
    }
    const initialData = await gqlRequest<{
      myCompanies: Company[]
      gameState: GameState
      myPendingActions: ScheduledActionSummary[]
      cities: City[]
      getCurrentEconomicCycle: EconomicCycleView | null
      getActiveMarketEvents: MarketEventView[]
      getEconomicHistory: EconomicCycleHistoryPoint[]
      tutorialProgress: Array<{ milestone: string; isCompleted: boolean; completedAtUtc: string | null }>
    }>(
      `{
        myCompanies {
          id name cash foundedAtUtc
          buildings { id name type level cityId powerStatus isAdvertisingActive lotMaterialQuantity lotOriginalMaterialQuantity destroyedAtUtc hasDefaultedCollateralLoan units { id unitType gridX gridY level } }
        }
        gameState {
          currentTick lastTickAtUtc tickIntervalSeconds taxCycleTicks taxRate
          currentGameYear currentGameTimeUtc ticksPerDay ticksPerYear
          nextTaxTick nextTaxGameTimeUtc nextTaxGameYear
        }
        myPendingActions {
          id actionType buildingId buildingName buildingType
          submittedAtUtc submittedAtTick appliesAtTick ticksRemaining totalTicksRequired
        }
        cities {
          id
          name
          currencyCode
        }
        getCurrentEconomicCycle {
          id phase phaseStartedTick expectedDurationTicks intensityFactor phaseEndTick ticksRemaining
        }
        getActiveMarketEvents {
          id eventType title description magnitudeMultiplier startsAtTick expiresAtTick ticksRemaining
          affectedResourceTypeId affectedResourceName affectedResourceSlug
          affectedCityId affectedCityName
        }
        getEconomicHistory(last: 365) {
          tick phase intensityFactor
        }
        tutorialProgress {
          milestone
          isCompleted
          completedAtUtc
        }
      }`,
    )

    companies.value = initialData.myCompanies
    pendingActions.value = initialData.myPendingActions
    gameState.value = initialData.gameState
    applyCityMaps(initialData.cities)
    economicCycle.value = initialData.getCurrentEconomicCycle
    activeMarketEvents.value = initialData.getActiveMarketEvents ?? []
    economicHistory.value = initialData.getEconomicHistory ?? []
    startTickCountdown()

    // Hydrate tooltip dismissed state from backend progress
    await hydrateFromBackend(initialData.tutorialProgress ?? [])

    // Show dashboard immediately after critical payload arrives.
    loading.value = false

    // Performance monitoring: log time from navigation start to first interactive render.
    const _loadMs = Math.round(performance.now() - _loadStart)
    console.debug(`[Dashboard] Initial load: ${_loadMs}ms`)

    // Delay tooltip 1 s so the initial render is complete before showing overlay
    setTimeout(() => {
      dashboardTooltipReady.value = true
    }, 1000)

    // Non-critical derived widgets hydrate in the background.
    void refreshCompanyDerivedData()
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load dashboard'
  } finally {
    if (error.value) {
      loading.value = false
    }
  }
})

useTickRefresh(async () => {
  if (!auth.isAuthenticated) {
    return
  }

  const scrollPos = saveScrollPosition()
  await Promise.all([loadDashboardData(), loadPendingActions()])
  await loadEconomyData()
  startTickCountdown()
  // Refresh ledger, unit statuses, and building financials on tick but keep loading state quiet (non-critical).
  const companiesForDerived = activeCompany.value ? [activeCompany.value] : []
  const companyIds = companiesForDerived.map((c) => c.id)
  const buildingIds = companiesForDerived.flatMap((c) => c.buildings.map((b) => b.id))
  await Promise.all([loadLedgers(companyIds, true), loadBuildingUnitStatuses(buildingIds), loadBuildingFinancials(buildingIds, true)])
  await restoreScrollPosition(scrollPos)
})

onUnmounted(stopTickCountdown)

async function loadCityPowerBalances(cityIds: string[]) {
  if (cityIds.length === 0) return
  // Load balances best-effort in parallel; failures are non-critical.
  const results = await Promise.allSettled(
    cityIds.map((cityId) =>
      gqlRequest<{ cityPowerBalance: CityPowerBalance }>(
        `query CityPower($cityId: UUID!) {
          cityPowerBalance(cityId: $cityId) {
            cityId totalSupplyMw totalDemandMw reserveMw reservePercent status
            powerPlantCount consumerBuildingCount
            powerPlants { buildingId buildingName plantType outputMw powerStatus }
          }
        }`,
        { cityId },
      ),
    ),
  )
  for (const result of results) {
    if (result.status === 'fulfilled') {
      const balance = result.value.cityPowerBalance
      cityPowerBalances.value[balance.cityId] = balance
    }
  }
}

async function loadPendingActions() {
  pendingActionsLoading.value = true
  try {
    const data = await gqlRequest<{ myPendingActions: ScheduledActionSummary[] }>(
      `{ myPendingActions {
        id actionType buildingId buildingName buildingType
        submittedAtUtc submittedAtTick appliesAtTick ticksRemaining totalTicksRequired
      } }`,
    )
    if (!deepEqual(pendingActions.value, data.myPendingActions)) {
      pendingActions.value = data.myPendingActions
    }
  } catch {
    // best-effort - pending actions list is non-critical
  } finally {
    pendingActionsLoading.value = false
  }
}

async function loadCityNames() {
  // Cities are static - serve from module-level cache after first successful load.
  if (Object.keys(_cityNamesCache).length > 0) {
    if (!deepEqual(cityNames.value, _cityNamesCache)) {
      cityNames.value = { ..._cityNamesCache }
    }
    if (!deepEqual(cityCurrencies.value, _cityCurrenciesCache)) {
      cityCurrencies.value = { ..._cityCurrenciesCache }
    }
    return
  }
  try {
    const data = await gqlRequest<{ cities: City[] }>('{ cities { id name currencyCode } }')
    applyCityMaps(data.cities)
  } catch {
    // best-effort - city names are non-critical
  }
}

async function loadLedgers(companyIds: string[], isRefresh = false) {
  if (companyIds.length === 0) return
  if (!isRefresh) ledgerLoading.value = true
  try {
    const results = await Promise.allSettled(
      companyIds.map((companyId) =>
        gqlRequest<{ companyLedger: CompanyLedgerSummary | null }>(
          `query CompanyLedger($companyId: UUID!) {
            companyLedger(companyId: $companyId) {
              companyId companyName gameYear isCurrentGameYear currentCash
              totalRevenue totalPurchasingCosts totalLaborCosts totalEnergyCosts
              totalMarketingCosts totalOtherCosts totalTaxPaid netIncome cashFromOperations
              primaryCurrencyCode
            }
          }`,
          { companyId },
        ),
      ),
    )
    for (const result of results) {
      if (result.status === 'fulfilled' && result.value.companyLedger) {
        const ledger = result.value.companyLedger
        companyLedgers.value[ledger.companyId] = ledger
      }
    }
  } catch {
    // best-effort - ledger data is non-critical
  } finally {
    if (!isRefresh) ledgerLoading.value = false
  }
}

async function loadBuildingUnitStatuses(buildingIds: string[]) {
  if (buildingIds.length === 0) return
  try {
    const results = await Promise.allSettled(
      buildingIds.map((buildingId) =>
        gqlRequest<{ buildingUnitOperationalStatuses: BuildingUnitOperationalStatus[] }>(
          `query BuildingUnitOperationalStatuses($buildingId: UUID!) {
            buildingUnitOperationalStatuses(buildingId: $buildingId) {
              buildingUnitId status blockedCode blockedReason idleTicks
            }
          }`,
          { buildingId },
        ).then((data) => ({ buildingId, statuses: data.buildingUnitOperationalStatuses })),
      ),
    )
    for (const result of results) {
      if (result.status === 'fulfilled') {
        buildingUnitStatuses.value[result.value.buildingId] = result.value.statuses
      }
    }
  } catch {
    // best-effort - unit status is non-critical
  }
}

async function loadBuildingFinancials(buildingIds: string[], isRefresh = false) {
  if (buildingIds.length === 0) return
  if (!isRefresh) buildingFinancialsLoading.value = true
  try {
    const results = await Promise.allSettled(
      buildingIds.map((buildingId) =>
        gqlRequest<{ buildingFinancialTimeline: { totalSales: number; totalCosts: number; totalProfit: number } }>(
          `query BuildingHeaderFinancials($buildingId: UUID!) {
            buildingFinancialTimeline(buildingId: $buildingId) {
              totalSales totalCosts totalProfit
            }
          }`,
          { buildingId },
        ).then((data) => ({ buildingId, totals: data.buildingFinancialTimeline })),
      ),
    )
    for (const result of results) {
      if (result.status === 'fulfilled') {
        buildingFinancials.value[result.value.buildingId] = result.value.totals
      }
    }
  } catch {
    // best-effort - financial summary is non-critical
  } finally {
    if (!isRefresh) buildingFinancialsLoading.value = false
  }
}

// ── Child event handlers ──────────────────────────────────────────────────────

async function handleRefreshDashboard() {
  await loadDashboardData()
  await Promise.all([refreshCompanyDerivedData(), loadPendingActions(), loadEconomyData()])
}

function handleCompanyLaunched() {
  // Parent refreshes data; actual redirect is handled by the child component
  void loadDashboardData()
  void refreshCompanyDerivedData()
}
</script>

<template>
  <div class="container py-8 px-4 relative">
    <!-- Dashboard first-visit contextual tooltip overlay -->
    <TutorialTooltip
      v-if="dashboardTooltipReady && showDashboardTooltip"
      milestone="TOOLTIP_DASHBOARD_SHOWN"
      :title="t('tutorial.tooltips.dashboardOverlay.title')"
      :description="t('tutorial.tooltips.dashboardOverlay.body')"
      position="bottom"
      @dismiss="dismissDashboardTooltip"
    />
    <!-- Dashboard header: title + tick clock -->
    <div class="flex justify-between items-start mb-8 flex-wrap gap-4">
      <div>
        <h1 class="text-[1.75rem] font-bold mb-1">{{ t('dashboard.title') }}</h1>
        <div v-if="auth.player" class="flex items-center gap-3">
          <span class="font-semibold text-[0.9375rem]">{{ auth.player.displayName }}</span>
          <span class="text-muted text-[0.8125rem]">{{ auth.player.email }}</span>
        </div>
      </div>
      <div
        v-if="gameState"
        class="tick-clock-widget flex flex-col items-end gap-1 px-4 py-2.5 rounded-lg bg-white/[0.04] border border-divider min-w-[9rem] text-right"
        :aria-label="t('tickClock.sectionTitle')"
        :title="t('tickClock.currentTick', { tick: gameState.currentTick })"
      >
        <span class="tick-clock-label text-xs text-muted font-medium tracking-wide">{{ t('tickClock.currentTime', { time: formattedGameTime }) }}</span>
        <span v-if="tickCountdown" class="tick-clock-countdown text-[0.9375rem] font-bold text-brand tabular-nums" role="timer">{{ tickCountdown }}</span>
      </div>
    </div>

    <div v-if="loading" class="text-center py-12 text-muted">{{ t('common.loading') }}</div>

    <div v-else-if="error" class="error-message flex items-center gap-4 p-4 bg-[rgba(248,113,113,0.1)] text-bad rounded" role="alert">
      {{ error }}
      <button class="btn btn-secondary" @click="router.go(0)">{{ t('common.tryAgain') }}</button>
    </div>

    <DashboardMainContent
      v-else
      :companies="companies"
      :company-ledgers="companyLedgers"
      :ledger-loading="ledgerLoading"
      :pending-actions="pendingActions"
      :pending-actions-loading="pendingActionsLoading"
      :game-state="gameState"
      :city-power-balances="cityPowerBalances"
      :city-names="cityNames"
      :city-currencies="cityCurrencies"
      :building-financials="buildingFinancials"
      :building-financials-loading="buildingFinancialsLoading"
      :building-unit-statuses="buildingUnitStatuses"
      :economic-cycle="economicCycle"
      :active-market-events="activeMarketEvents"
      :economic-history="economicHistory"
      @refresh-dashboard="handleRefreshDashboard"
      @company-launched="handleCompanyLaunched"
    />
  </div>
</template>

<script setup lang="ts">
/* eslint-disable @typescript-eslint/no-unused-vars */
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
import { deepEqual } from '@/lib/utils'
import { getActiveCompany } from '@/lib/accountContext'
import { formatInGameTime } from '@/lib/gameTime'
import PendingActionsTimeline from '@/components/dashboard/PendingActionsTimeline.vue'
import SupplyChainPanel from '@/components/dashboard/SupplyChainPanel.vue'
import FinancialSummaryCard from '@/components/dashboard/FinancialSummaryCard.vue'
import StarterGuidance from '@/components/dashboard/StarterGuidance.vue'
import DashboardChatPanel from '@/components/dashboard/DashboardChatPanel.vue'
import DashboardTabNav from '@/components/dashboard/DashboardTabNav.vue'
import BuildingHeaderFinancials from '@/components/buildings/BuildingHeaderFinancials.vue'
import type { Company, GameState, ScheduledActionSummary, CityPowerBalance, CompanyLedgerSummary, City, BuildingUnitOperationalStatus } from '@/types'

// Module-level cache for city names — cities are static and never change during a session.
const _cityNamesCache: Record<string, string> = {}
// Module-level cache for city currencies — cities are static and never change during a session.
const _cityCurrenciesCache: Record<string, string> = {}

const { t, locale } = useI18n()
const router = useRouter()
const auth = useAuthStore()
const gameStateStore = useGameStateStore()
const { gameState } = storeToRefs(gameStateStore)
const { selectedCityId } = storeToRefs(auth)

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
/** Map from buildingId → per-unit operational statuses for supply-chain live status display. */
const buildingUnitStatuses = ref<Record<string, BuildingUnitOperationalStatus[]>>({})
/** Map from buildingId → aggregated financial totals (revenue, costs, profit). */
const buildingFinancials = ref<Record<string, { totalSales: number; totalCosts: number; totalProfit: number }>>({})
const buildingFinancialsLoading = ref(false)
const createCompanyName = ref('')
const createCompanyLoading = ref(false)
const createCompanyError = ref<string | null>(null)
const createCompanyMessage = ref<string | null>(null)
const masterPortalUrl = import.meta.env.VITE_MASTER_WEB_URL || 'http://localhost:5174'

/** Active dashboard tab. Persisted in sessionStorage so navigation preserves state. */
const _savedTab = typeof sessionStorage !== 'undefined' ? sessionStorage.getItem('dashboard_tab') : null
const activeTab = ref<'overview' | 'buildings' | 'activity' | 'chat' | 'pro'>((_savedTab as 'overview' | 'buildings' | 'activity' | 'chat' | 'pro') || 'overview')
function setActiveTab(tab: 'overview' | 'buildings' | 'activity' | 'chat' | 'pro') {
  activeTab.value = tab
  if (typeof sessionStorage !== 'undefined') sessionStorage.setItem('dashboard_tab', tab)
}

/** Active personal-account tab. Persisted in sessionStorage so navigation preserves state. */
const _savedPersonTab = typeof sessionStorage !== 'undefined' ? sessionStorage.getItem('person_account_tab') : null
const personAccountTab = ref<'overview' | 'create-company' | 'ledger'>((_savedPersonTab as 'overview' | 'create-company' | 'ledger') || 'overview')
function setPersonAccountTab(tab: string) {
  personAccountTab.value = tab as 'overview' | 'create-company' | 'ledger'
  if (typeof sessionStorage !== 'undefined') sessionStorage.setItem('person_account_tab', tab)
}
const personAccountTabs = computed(() => [
  { key: 'overview', label: t('dashboard.personTabOverview') },
  { key: 'create-company', label: t('dashboard.personTabCreateCompany') },
  { key: 'ledger', label: t('dashboard.personTabLedger') },
])

const { tickCountdown, startTickCountdown, stopTickCountdown } = useTickCountdown(gameState)
const { saveScrollPosition, restoreScrollPosition } = useScrollPreservation()

const activeCompany = computed(() => getActiveCompany(auth.player, companies.value))
const isPersonAccount = computed(() => auth.player?.activeAccountType !== 'COMPANY' || !activeCompany.value)
const visibleCompanies = computed(() => (activeCompany.value ? [activeCompany.value] : []))
const formattedGameTime = computed(() => (gameState.value?.currentGameTimeUtc ? formatInGameTime(gameState.value.currentGameTimeUtc, locale.value) : ''))

const filteredBuildingsByCity = computed(() => {
  if (!activeCompany.value || !selectedCityId.value) {
    return activeCompany.value?.buildings ?? []
  }
  return activeCompany.value.buildings.filter((b) => b.cityId === selectedCityId.value)
})

function tabsForCompany(company: Company) {
  return [
    { key: 'overview', label: t('dashboard.tabOverview') },
    { key: 'buildings', label: t('dashboard.tabBuildings'), badge: company.buildings.length },
    { key: 'activity', label: t('dashboard.tabActivity') },
    { key: 'chat', label: t('dashboard.tabChat') },
    { key: 'pro', label: t('dashboard.tabPro') },
  ]
}
const buildingTypeIcons: Record<string, string> = {
  MINE: '⛏️',
  FACTORY: '🏭',
  SALES_SHOP: '🏪',
  RESEARCH_DEVELOPMENT: '🔬',
  APARTMENT: '🏢',
  COMMERCIAL: '🏛️',
  MEDIA_HOUSE: '📺',
  BANK: '🏦',
  EXCHANGE: '📊',
  POWER_PLANT: '⚡',
}

function getBuildingIcon(type: string): string {
  return buildingTypeIcons[type] || '🏗️'
}

function formatBuildingType(type: string): string {
  return type.replace(/_/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase())
}

/** Returns the power status label for a building's powerStatus field. */
function getBuildingPowerLabel(powerStatus: string): string {
  const key = `powerGrid.buildingStatus.${powerStatus}` as Parameters<typeof t>[0]
  return t(key)
}

/** Returns the CSS class for a power status badge. */
function powerStatusClass(status: string): string {
  const base = 'power-badge text-[0.625rem] font-bold px-1.5 py-0.5 rounded whitespace-nowrap'
  if (status === 'CONSTRAINED') return `${base} power-badge--constrained bg-amber-400/20 text-amber-400`
  if (status === 'OFFLINE') return `${base} power-badge--offline bg-red-400/15 text-[var(--color-danger)]`
  return `${base} power-badge--powered bg-green-500/15 text-[var(--color-secondary)]`
}

/** Returns the CSS class for the city power balance status. */
function powerBalanceClass(status: string): string {
  const base = 'power-balance flex items-center gap-2 px-3 py-2 rounded text-[0.8125rem] flex-wrap border'
  if (status === 'CRITICAL') return `${base} power-balance--critical bg-red-400/10 border-red-400/25 text-[var(--color-danger)]`
  if (status === 'CONSTRAINED') return `${base} power-balance--constrained bg-amber-400/15 border-amber-400/30 text-amber-400`
  return `${base} power-balance--balanced bg-green-500/10 border-green-500/25 text-[var(--color-secondary)]`
}

async function loadDashboardData() {
  const companiesData = await gqlRequest<{ myCompanies: Company[] }>(
    `{ myCompanies {
      id name cash foundedAtUtc
      buildings { id name type level cityId powerStatus units { id unitType gridX gridY level } }
    } }`,
  )

  if (!deepEqual(companies.value, companiesData.myCompanies)) {
    companies.value = companiesData.myCompanies
  }
}

async function refreshCompanyDerivedData() {
  const cityIds = [...new Set(companies.value.flatMap((company) => company.buildings.map((building) => building.cityId)))]
  const companyIds = companies.value.map((company) => company.id)
  const buildingIds = companies.value.flatMap((company) => company.buildings.map((building) => building.id))

  await Promise.all([loadCityPowerBalances(cityIds), loadCityNames(), loadLedgers(companyIds), loadBuildingUnitStatuses(buildingIds), loadBuildingFinancials(buildingIds)])
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    router.push('/login')
    return
  }

  try {
    await auth.fetchMe()
    if (auth.player && !auth.player.onboardingCompletedAtUtc) {
      router.push('/onboarding')
      return
    }
    const [companiesData, gameStateData] = await Promise.all([
      gqlRequest<{ myCompanies: Company[] }>(
        `{ myCompanies {
          id name cash foundedAtUtc
          buildings { id name type level cityId powerStatus units { id unitType gridX gridY level } }
        } }`,
      ),
      gqlRequest<{ gameState: GameState }>(
        '{ gameState { currentTick lastTickAtUtc tickIntervalSeconds taxCycleTicks taxRate currentGameYear currentGameTimeUtc ticksPerDay ticksPerYear nextTaxTick nextTaxGameTimeUtc nextTaxGameYear } }',
      ),
    ])
    companies.value = companiesData.myCompanies
    gameState.value = gameStateData.gameState
    startTickCountdown()

    // Load city power balances for each unique city that has buildings.
    await refreshCompanyDerivedData()

    await loadPendingActions()
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load dashboard'
  } finally {
    loading.value = false
  }
})

useTickRefresh(async () => {
  if (!auth.isAuthenticated) {
    return
  }

  const scrollPos = saveScrollPosition()
  await Promise.all([loadDashboardData(), loadPendingActions()])
  startTickCountdown()
  // Refresh ledger, unit statuses, and building financials on tick but keep loading state quiet (non-critical).
  const companyIds = companies.value.map((c) => c.id)
  const buildingIds = companies.value.flatMap((c) => c.buildings.map((b) => b.id))
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
    // best-effort — pending actions list is non-critical
  } finally {
    pendingActionsLoading.value = false
  }
}

async function loadCityNames() {
  // Cities are static — serve from module-level cache after first successful load.
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
    const nameMap: Record<string, string> = {}
    const currencyMap: Record<string, string> = {}
    for (const city of data.cities) {
      nameMap[city.id] = city.name
      _cityNamesCache[city.id] = city.name
      currencyMap[city.id] = city.currencyCode
      _cityCurrenciesCache[city.id] = city.currencyCode
    }
    cityNames.value = nameMap
    cityCurrencies.value = currencyMap
  } catch {
    // best-effort — city names are non-critical
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
    // best-effort — ledger data is non-critical
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
    // best-effort — unit status is non-critical
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
    // best-effort — financial summary is non-critical
  } finally {
    if (!isRefresh) buildingFinancialsLoading.value = false
  }
}

function formatCurrency(value: number, currencyCode = 'EUR'): string {
  return new Intl.NumberFormat(locale.value, {
    style: 'currency',
    currency: currencyCode,
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(value)
}

function formatDateTime(value: string): string {
  return new Intl.DateTimeFormat(locale.value, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}

async function createCompany() {
  const trimmedName = createCompanyName.value.trim()
  if (!trimmedName) {
    createCompanyError.value = t('dashboard.companyNameRequired')
    createCompanyMessage.value = null
    return
  }

  createCompanyLoading.value = true
  createCompanyError.value = null
  createCompanyMessage.value = null

  try {
    await gqlRequest(
      `mutation CreateCompany($input: CreateCompanyInput!) {
        createCompany(input: $input) {
          id
        }
      }`,
      {
        input: {
          name: trimmedName,
        },
      },
    )

    createCompanyName.value = ''
    createCompanyMessage.value = t('dashboard.createCompanySuccess', { company: trimmedName })

    await auth.fetchMe()
    await loadDashboardData()
    await Promise.all([refreshCompanyDerivedData(), loadPendingActions()])
  } catch (e: unknown) {
    createCompanyError.value = e instanceof Error ? e.message : t('dashboard.createCompanyFailed')
  } finally {
    createCompanyLoading.value = false
  }
}
</script>

<template src="./DashboardView.template.html"></template>

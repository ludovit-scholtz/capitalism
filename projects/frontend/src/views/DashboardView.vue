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

// Module-level cache for city names - cities are static and never change during a session.
const _cityNamesCache: Record<string, string> = {}
// Module-level cache for city currencies - cities are static and never change during a session.
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
/** Map from buildingId -> per-unit operational statuses for supply-chain live status display. */
const buildingUnitStatuses = ref<Record<string, BuildingUnitOperationalStatus[]>>({})
/** Map from buildingId -> aggregated financial totals (revenue, costs, profit). */
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
  SALES_SHOP: '🏬',
  RESEARCH_DEVELOPMENT: '🧪',
  APARTMENT: '🏠',
  COMMERCIAL: '🏢',
  MEDIA_HOUSE: '📰',
  BANK: '🏦',
  EXCHANGE: '📈',
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

<template>
  <div class="container py-8 px-4">
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

    <template v-else>
      <!-- Person account mode (no company selected) -->
      <section v-if="isPersonAccount" class="person-account-panel mt-6 p-6 border border-divider rounded-xl bg-card">
        <div class="mb-1">
          <p class="text-xs font-bold tracking-widest uppercase text-muted m-0 mb-1.5">{{ t('dashboard.personModeEyebrow') }}</p>
          <h2 class="text-xl font-bold">{{ t('dashboard.personModeTitle') }}</h2>
        </div>

        <!-- Personal-account tab navigation -->
        <DashboardTabNav :tabs="personAccountTabs" :model-value="personAccountTab" class="mt-1" @update:model-value="setPersonAccountTab" />

        <!-- ÔöÇÔöÇ Overview tab ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ -->
        <div v-show="personAccountTab === 'overview'" class="pt-5" role="tabpanel" :aria-label="t('dashboard.personTabOverview')">
          <p class="text-muted mb-4">{{ companies.length === 0 ? t('dashboard.personModeNoCompanies') : t('dashboard.personModeBody') }}</p>

          <div class="grid gap-3 [grid-template-columns:repeat(auto-fit,minmax(180px,1fr))] mb-5">
            <article class="person-metric-card flex flex-col gap-1.5 p-4 border border-divider rounded-lg bg-card-raised">
              <span class="text-xs text-muted uppercase tracking-wide">{{ t('dashboard.personalCash') }}</span>
              <strong class="text-xl">{{ formatCurrency(auth.player?.personalCash ?? 0, 'EUR') }}</strong>
            </article>
            <article class="person-metric-card flex flex-col gap-1.5 p-4 border border-divider rounded-lg bg-card-raised">
              <span class="text-xs text-muted uppercase tracking-wide">{{ t('dashboard.controlledCompanies') }}</span>
              <strong class="text-xl">{{ companies.length }}</strong>
            </article>
          </div>

          <div v-if="companies.length > 0" class="mt-4">
            <h3 class="text-[0.9375rem] font-bold mb-3">{{ t('dashboard.controlledCompaniesTitle') }}</h3>
            <div class="grid gap-3 [grid-template-columns:repeat(auto-fit,minmax(220px,1fr))]">
              <article v-for="company in companies" :key="company.id" class="flex flex-col gap-1 p-4 border border-divider rounded-lg bg-page">
                <strong>{{ company.name }}</strong>
                <span>{{ formatCurrency(company.cash, companyLedgers[company.id]?.primaryCurrencyCode ?? 'EUR') }}</span>
                <small class="text-muted">{{ t('dashboard.switchCompanyHint') }}</small>
              </article>
            </div>
          </div>
        </div>

        <!-- ÔöÇÔöÇ Create company tab ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ -->
        <div v-show="personAccountTab === 'create-company'" class="pt-5" role="tabpanel" :aria-label="t('dashboard.personTabCreateCompany')">
          <div class="flex flex-col gap-3.5">
            <div>
              <h3 class="text-[0.9375rem] font-bold mb-1">{{ t('dashboard.createCompanyTitle') }}</h3>
              <p class="text-muted m-0">{{ t('dashboard.createCompanyBody') }}</p>
            </div>

            <form class="flex flex-col gap-3" @submit.prevent="createCompany">
              <label class="flex flex-col gap-1.5">
                <span class="text-sm font-semibold">{{ t('dashboard.companyNameLabel') }}</span>
                <input
                  v-model="createCompanyName"
                  type="text"
                  maxlength="200"
                  :placeholder="t('dashboard.companyNamePlaceholder')"
                  class="px-3.5 py-3 border border-divider rounded bg-page text-body focus:outline-none focus:border-brand transition-colors"
                />
              </label>
              <div class="flex flex-wrap gap-3">
                <button class="btn btn-primary" type="submit" :disabled="createCompanyLoading">{{ createCompanyLoading ? t('common.loading') : t('dashboard.createCompany') }}</button>
                <RouterLink to="/encyclopedia" class="btn btn-secondary">{{ t('dashboard.browseEncyclopedia') }}</RouterLink>
              </div>
            </form>

            <p v-if="createCompanyMessage" class="m-0 p-3 rounded-lg bg-[rgba(34,197,94,0.12)] text-good" role="status">{{ createCompanyMessage }}</p>
            <p v-if="createCompanyError" class="m-0 p-3 rounded-lg bg-[rgba(248,113,113,0.12)] text-bad" role="alert">{{ createCompanyError }}</p>
          </div>
        </div>

        <!-- ÔöÇÔöÇ Ledger tab ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ -->
        <div v-show="personAccountTab === 'ledger'" class="pt-5" role="tabpanel" :aria-label="t('dashboard.personTabLedger')">
          <p class="text-muted mb-5">{{ t('dashboard.personLedgerTabBody') }}</p>

          <div class="grid gap-3 [grid-template-columns:repeat(auto-fit,minmax(180px,1fr))] mb-5">
            <article class="person-metric-card flex flex-col gap-1.5 p-4 border border-divider rounded-lg bg-card-raised">
              <span class="text-xs text-muted uppercase tracking-wide">{{ t('dashboard.personalCash') }}</span>
              <strong class="text-xl">{{ formatCurrency(auth.player?.personalCash ?? 0, 'EUR') }}</strong>
            </article>
          </div>

          <div class="person-account-ledger-link mb-5">
            <RouterLink to="/personal-ledger" class="btn btn-primary inline-flex items-center gap-1.5"> 📒 {{ t('dashboard.viewPersonalLedger') }} </RouterLink>
          </div>
        </div>
      </section>

      <!-- Company mode: tabbed dashboard -->
      <div v-if="visibleCompanies.length > 0" class="companies-section flex flex-col gap-6">
        <div v-for="company in visibleCompanies" :key="company.id" class="company-card bg-card border border-divider rounded-xl p-6">
          <!-- Always-visible company bar -->
          <div class="company-header flex justify-between items-start max-sm:flex-col max-sm:gap-4 mb-0">
            <div>
              <h2 class="text-[1.25rem] font-bold mb-2">{{ company.name }}</h2>
              <div class="flex gap-6 flex-wrap">
                <span class="flex flex-col gap-0.5">
                  <span class="text-[0.6875rem] uppercase tracking-wide text-muted font-semibold">{{ t('dashboard.cash') }}</span>
                  <span class="cash text-[1.25rem] font-bold text-good">{{ formatCurrency(company.cash, companyLedgers[company.id]?.primaryCurrencyCode ?? 'EUR') }}</span>
                </span>
                <span class="flex flex-col gap-0.5">
                  <span class="text-[0.6875rem] uppercase tracking-wide text-muted font-semibold">{{ t('dashboard.buildings') }}</span>
                  <span class="text-[1.25rem] font-bold">{{ company.buildings.length }}</span>
                </span>
                <span v-if="company.buildings.length > 0 && cityNames[company.buildings[0]?.cityId ?? '']" class="flex flex-col gap-0.5">
                  <span class="meta-label text-[0.6875rem] uppercase tracking-wide text-muted font-semibold">{{ t('dashboard.city') }}</span>
                  <span class="city-name font-medium text-[0.9375rem]">📍 {{ cityNames[company.buildings[0]?.cityId ?? ''] }}</span>
                </span>
              </div>
            </div>
            <div class="company-actions flex flex-wrap gap-2 items-start">
              <RouterLink :to="`/buy-building/${company.id}`" class="btn btn-primary"> {{ t('dashboard.buyBuilding') }} </RouterLink>
              <RouterLink v-if="company.buildings.length > 0 && company.buildings[0]" :to="`/city/${company.buildings[0].cityId}`" class="btn btn-secondary">
                🗺️ {{ t('nav.cityMap') }}
              </RouterLink>
              <RouterLink :to="`/ledger/${company.id}`" class="btn btn-ghost"> 📒 {{ t('dashboard.viewLedger') }} </RouterLink>
              <RouterLink :to="`/company/${company.id}/settings`" class="btn btn-ghost"> ⚙️ {{ t('dashboard.companySettings') }} </RouterLink>
            </div>
          </div>

          <!-- Section tab navigation -->
          <DashboardTabNav :tabs="tabsForCompany(company)" :model-value="activeTab" @update:model-value="setActiveTab($event as 'chat' | 'buildings' | 'overview' | 'activity' | 'pro')" />

          <!-- ÔöÇÔöÇ Overview tab ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ -->
          <div v-show="activeTab === 'overview'" class="tab-panel pt-5" role="tabpanel" aria-label="Overview">
            <!-- Financial summary and guidance -->
            <div class="grid grid-cols-2 max-[700px]:grid-cols-1 gap-3 mb-4">
              <FinancialSummaryCard :ledger="companyLedgers[company.id] ?? null" :loading="ledgerLoading" />
              <StarterGuidance :company="company" :revenue="companyLedgers[company.id]?.totalRevenue ?? 0" :net-income="companyLedgers[company.id]?.netIncome ?? 0" />
            </div>
          </div>

          <!-- ÔöÇÔöÇ Buildings tab ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ -->
          <div v-show="activeTab === 'buildings'" class="tab-panel pt-5" role="tabpanel" aria-label="Buildings">
            <div v-if="filteredBuildingsByCity.length === 0" class="no-buildings text-center p-6 text-muted flex flex-col items-center gap-4">
              <p v-if="selectedCityId">{{ t('dashboard.noBuildingsInCity') }}</p>
              <p v-else>{{ t('dashboard.noBuildings') }}</p>
              <RouterLink :to="`/buy-building/${company.id}`" class="btn btn-primary"> {{ t('dashboard.buyBuilding') }} </RouterLink>
            </div>

            <div v-else class="buildings-grid grid gap-5 [grid-template-columns:repeat(auto-fill,minmax(260px,1fr))]">
              <div v-for="building in filteredBuildingsByCity" :key="building.id" class="building-card-wrapper flex flex-col mb-1">
                <RouterLink
                  :to="building.type === 'BANK' ? `/bank/${building.id}` : `/building/${building.id}`"
                  class="building-card flex items-center gap-3 p-4 bg-page border border-divider rounded-t-lg border-b-0 no-underline text-body transition-all duration-200 hover:border-brand hover:bg-[rgba(0,71,255,0.04)] hover:-translate-y-px"
                >
                  <div class="text-[1.75rem] flex-shrink-0">{{ getBuildingIcon(building.type) }}</div>
                  <div class="flex-1 flex flex-col gap-0.5">
                    <span class="building-name font-semibold text-[0.9375rem]">{{ building.name }}</span>
                    <span class="building-type-label text-xs text-muted">{{ formatBuildingType(building.type) }}</span>
                  </div>
                  <div class="flex flex-col items-end gap-0.5">
                    <span class="bg-brand text-white px-2 py-0.5 rounded text-[0.6875rem] font-bold">Lv.{{ building.level }}</span>
                    <span class="text-[0.6875rem] text-muted">{{ building.units.length }} units</span>
                    <span v-if="building.powerStatus && building.powerStatus !== 'POWERED'" :class="powerStatusClass(building.powerStatus)" :aria-label="getBuildingPowerLabel(building.powerStatus)">
                      {{ building.powerStatus === 'OFFLINE' ? '❌' : '⚡' }} {{ getBuildingPowerLabel(building.powerStatus) }}
                    </span>
                  </div>
                </RouterLink>
                <BuildingHeaderFinancials
                  :revenue="buildingFinancials[building.id]?.totalSales ?? null"
                  :costs="buildingFinancials[building.id]?.totalCosts ?? null"
                  :profit="buildingFinancials[building.id]?.totalProfit ?? null"
                  :loading="buildingFinancialsLoading && !buildingFinancials[building.id]"
                  :currency-code="cityCurrencies[building.cityId] ?? 'EUR'"
                />
                <SupplyChainPanel v-if="building.units.length > 0" :units="building.units" :statuses="buildingUnitStatuses[building.id]" class="mt-3" />
              </div>
            </div>

            <!-- City power summary -->
            <div v-if="filteredBuildingsByCity.length > 0 && filteredBuildingsByCity[0]" class="mt-3 flex flex-wrap gap-2">
              <template v-for="cityId in [...new Set(filteredBuildingsByCity.map((b) => b.cityId))]" :key="cityId">
                <div v-if="cityPowerBalances[cityId] && cityPowerBalances[cityId].powerPlantCount > 0" :class="powerBalanceClass(cityPowerBalances[cityId].status)" :aria-label="t('powerGrid.title')">
                  <span class="flex-shrink-0">⚡</span>
                  <span class="font-semibold">{{ t('powerGrid.powerCardTitle') }}</span>
                  <span v-if="cityPowerBalances[cityId].status === 'BALANCED'">
                    {{ cityPowerBalances[cityId].totalSupplyMw }} / {{ cityPowerBalances[cityId].totalDemandMw }} {{ t('powerGrid.unit') }}
                  </span>
                  <span v-else> {{ cityPowerBalances[cityId].status === 'CRITICAL' ? t('powerGrid.criticalWarning') : t('powerGrid.shortageWarning') }} </span>
                  <RouterLink :to="`/city/${cityId}`" class="underline text-xs ml-auto">{{ t('powerGrid.viewDetails') }}</RouterLink>
                </div>
                <div
                  v-else
                  class="power-balance power-balance--legacy flex items-center gap-2 px-3 py-2 rounded text-sm border border-[var(--color-border)] bg-[var(--color-bg-secondary)] text-[var(--color-text-secondary)]"
                >
                  <span>⚡</span>
                  <span class="font-semibold">{{ t('powerGrid.powerCardTitle') }}</span>
                  <span>{{ t('powerGrid.powerCardNoPower') }}</span>
                </div>
              </template>
            </div>
          </div>

          <!-- ÔöÇÔöÇ Activity tab ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ -->
          <div v-show="activeTab === 'activity'" class="tab-panel pt-5" role="tabpanel" aria-label="Activity">
            <PendingActionsTimeline :actions="pendingActions" :loading="pendingActionsLoading" :current-tick="gameState?.currentTick ?? null" />
          </div>

          <!-- ÔöÇÔöÇ Chat tab ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ -->
          <div v-show="activeTab === 'chat'" class="tab-panel pt-5" role="tabpanel" aria-label="Chat">
            <DashboardChatPanel />
          </div>

          <!-- ÔöÇÔöÇ Pro tab ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ -->
          <div v-show="activeTab === 'pro'" class="tab-panel pt-5" role="tabpanel" :aria-label="t('dashboard.tabPro')">
            <section class="pro-tab-panel p-6 border border-[rgba(255,109,0,0.35)] rounded-xl bg-gradient-to-br from-[rgba(255,109,0,0.08)] to-[rgba(0,71,255,0.07)]" aria-labelledby="pro-tab-title">
              <!-- Status header -->
              <div class="flex justify-between items-start gap-4 mb-3 max-sm:flex-col">
                <div>
                  <span class="inline-block mb-1.5 text-xs font-bold tracking-widest uppercase text-[var(--color-tertiary)]">{{ t('proAccess.eyebrow') }}</span>
                  <h2 id="pro-tab-title" class="m-0 text-[1.35rem] font-bold">{{ t('proAccess.title') }}</h2>
                </div>
                <span
                  class="startup-pack-status px-3 py-1.5 rounded-full text-xs font-bold tracking-wide uppercase"
                  :class="auth.isProSubscriber ? 'bg-[rgba(0,200,83,0.18)] text-[var(--color-secondary)]' : 'bg-[rgba(248,113,113,0.12)] text-bad'"
                >
                  {{ auth.isProSubscriber ? t('proAccess.activeBadge') : t('proAccess.inactiveBadge') }}
                </span>
              </div>

              <!-- Subscription state message -->
              <p class="text-muted mb-6">
                <template v-if="auth.isProSubscriber && auth.player?.proSubscriptionEndsAtUtc">
                  {{ t('proAccess.activeBody', { date: formatDateTime(auth.player.proSubscriptionEndsAtUtc) }) }}
                </template>
                <template v-else> {{ t('proAccess.inactiveBody') }} </template>
              </p>

              <!-- Benefit cards -->
              <h3 class="text-base font-bold mb-3.5">{{ t('dashboard.proBenefitsHeading') }}</h3>
              <div class="grid gap-3.5 [grid-template-columns:repeat(auto-fit,minmax(220px,1fr))] mb-6">
                <article
                  v-for="(benefit, i) in [
                    { icon: '🏭', title: t('dashboard.proBenefitProducts'), body: t('dashboard.proBenefitProductsBody') },
                    { icon: '📊', title: t('dashboard.proBenefitAdvanced'), body: t('dashboard.proBenefitAdvancedBody') },
                    { icon: '🚀', title: t('dashboard.proBenefitUnlock'), body: t('dashboard.proBenefitUnlockBody') },
                    { icon: '⚡', title: t('dashboard.proBenefitPriority'), body: t('dashboard.proBenefitPriorityBody') },
                  ]"
                  :key="i"
                  class="flex gap-3.5 items-start p-4 rounded-lg bg-[rgba(13,17,23,0.32)] border border-[rgba(48,54,61,0.8)]"
                >
                  <span class="text-2xl flex-shrink-0" aria-hidden="true">{{ benefit.icon }}</span>
                  <div>
                    <strong class="block text-[0.9rem] mb-1">{{ benefit.title }}</strong>
                    <p class="m-0 text-[0.8125rem] text-muted leading-relaxed">{{ benefit.body }}</p>
                  </div>
                </article>
              </div>

              <!-- Portal CTA -->
              <div class="flex items-center gap-4 flex-wrap max-sm:flex-col max-sm:items-start">
                <p class="m-0 flex-1 text-muted text-sm">{{ t('proAccess.manageBody') }}</p>
                <a class="btn btn-primary" :href="masterPortalUrl" target="_blank" rel="noreferrer"> {{ t('proAccess.openPortal') }} </a>
              </div>
            </section>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { storeToRefs } from 'pinia'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { getActiveCompany } from '@/lib/accountContext'
import PendingActionsTimeline from '@/components/dashboard/PendingActionsTimeline.vue'
import SupplyChainPanel from '@/components/dashboard/SupplyChainPanel.vue'
import FinancialSummaryCard from '@/components/dashboard/FinancialSummaryCard.vue'
import StarterGuidance from '@/components/dashboard/StarterGuidance.vue'
import DashboardChatPanel from '@/components/dashboard/DashboardChatPanel.vue'
import DashboardPersonalSettingsPanel from '@/components/dashboard/DashboardPersonalSettingsPanel.vue'
import DashboardTabNav from '@/components/dashboard/DashboardTabNav.vue'
import BuildingHeaderFinancials from '@/components/buildings/BuildingHeaderFinancials.vue'
import NewCompanyModal from '@/components/dashboard/NewCompanyModal.vue'
import { computeMiningEfficiencyFactor } from '@/lib/miningScarcity'
import { resolveMasterWebUrl } from '@/lib/runtimeGraphqlUrl'
import type {
  Company,
  GameState,
  ScheduledActionSummary,
  CityPowerBalance,
  CompanyLedgerSummary,
  City,
  BuildingUnitOperationalStatus,
} from '@/types'

const { t, locale } = useI18n()
const router = useRouter()
const auth = useAuthStore()
const { selectedCityId } = storeToRefs(auth)

const props = defineProps<{
  companies: Company[]
  companyLedgers: Record<string, CompanyLedgerSummary>
  ledgerLoading: boolean
  pendingActions: ScheduledActionSummary[]
  pendingActionsLoading: boolean
  gameState: GameState | null
  cityPowerBalances: Record<string, CityPowerBalance>
  cityNames: Record<string, string>
  cityCurrencies: Record<string, string>
  buildingFinancials: Record<string, { totalSales: number; totalCosts: number; totalProfit: number }>
  buildingFinancialsLoading: boolean
  buildingUnitStatuses: Record<string, BuildingUnitOperationalStatus[]>
}>()

const emit = defineEmits<{
  (e: 'refresh-dashboard'): void
  (e: 'company-launched', companyId: string): void
}>()

const masterPortalUrl = resolveMasterWebUrl(import.meta.env.VITE_MASTER_WEB_URL)

// ── Tab state ─────────────────────────────────────────────────────────────────

const _savedTab = typeof sessionStorage !== 'undefined' ? sessionStorage.getItem('dashboard_tab') : null
const activeTab = ref<'overview' | 'buildings' | 'activity' | 'chat' | 'pro'>((_savedTab as 'overview' | 'buildings' | 'activity' | 'chat' | 'pro') || 'overview')
function setActiveTab(tab: 'overview' | 'buildings' | 'activity' | 'chat' | 'pro') {
  activeTab.value = tab
  if (typeof sessionStorage !== 'undefined') sessionStorage.setItem('dashboard_tab', tab)
}

const _savedPersonTab = typeof sessionStorage !== 'undefined' ? sessionStorage.getItem('person_account_tab') : null
const personAccountTab = ref<'overview' | 'create-company' | 'ledger' | 'settings'>((_savedPersonTab as 'overview' | 'create-company' | 'ledger' | 'settings') || 'overview')
function setPersonAccountTab(tab: string) {
  personAccountTab.value = tab as 'overview' | 'create-company' | 'ledger' | 'settings'
  if (typeof sessionStorage !== 'undefined') sessionStorage.setItem('person_account_tab', tab)
}
const personAccountTabs = computed(() => [
  { key: 'overview', label: t('dashboard.personTabOverview') },
  { key: 'create-company', label: t('dashboard.personTabCreateCompany') },
  { key: 'ledger', label: t('dashboard.personTabLedger') },
  { key: 'settings', label: t('dashboard.personTabSettings') },
])

// ── Computed helpers ──────────────────────────────────────────────────────────

const activeCompany = computed(() => getActiveCompany(auth.player, props.companies))
const isPersonAccount = computed(() => auth.player?.activeAccountType !== 'COMPANY' || !activeCompany.value)
const visibleCompanies = computed(() => (activeCompany.value ? [activeCompany.value] : []))

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

function getBuildingPowerLabel(powerStatus: string): string {
  const key = `powerGrid.buildingStatus.${powerStatus}` as Parameters<typeof t>[0]
  return t(key)
}

function powerStatusClass(status: string): string {
  const base = 'power-badge text-[0.625rem] font-bold px-1.5 py-0.5 rounded whitespace-nowrap'
  if (status === 'CONSTRAINED') return `${base} power-badge--constrained bg-amber-400/20 text-amber-400`
  if (status === 'OFFLINE') return `${base} power-badge--offline bg-red-400/15 text-[var(--color-danger)]`
  return `${base} power-badge--powered bg-green-500/15 text-[var(--color-secondary)]`
}

function powerBalanceClass(status: string): string {
  const base = 'power-balance flex items-center gap-2 px-3 py-2 rounded text-[0.8125rem] flex-wrap border'
  if (status === 'CRITICAL') return `${base} power-balance--critical bg-red-400/10 border-red-400/25 text-[var(--color-danger)]`
  if (status === 'CONSTRAINED') return `${base} power-balance--constrained bg-amber-400/15 border-amber-400/30 text-amber-400`
  return `${base} power-balance--balanced bg-green-500/10 border-green-500/25 text-[var(--color-secondary)]`
}

function miningEfficiencyPercent(building: Company['buildings'][number]): number | null {
  if (building.lotOriginalMaterialQuantity == null || building.lotOriginalMaterialQuantity <= 0) return null
  if (building.lotMaterialQuantity == null) return null
  return Math.round(computeMiningEfficiencyFactor(building.lotMaterialQuantity, building.lotOriginalMaterialQuantity) * 100)
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

// ── Create company (first company) ───────────────────────────────────────────

const createCompanyName = ref('')
const createCompanyLoading = ref(false)
const createCompanyError = ref<string | null>(null)
const createCompanyMessage = ref<string | null>(null)

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
    emit('refresh-dashboard')
  } catch (e: unknown) {
    createCompanyError.value = e instanceof Error ? e.message : t('dashboard.createCompanyFailed')
  } finally {
    createCompanyLoading.value = false
  }
}

// ── Additional Company IPO ────────────────────────────────────────────────────

interface AdditionalCompanyPrerequisites {
  allRequirementsMet: boolean
  companyCount: number
  underMaxCap: boolean
  hasExistingCompany: boolean
  companyAgeTicks: number
  companyAgeRequirementMet: boolean
  ticksUntilAgeRequirementMet: number
  netIncomeInWindow: number
  profitabilityRequirementMet: boolean
  personalBalanceUsd: number
  balanceRequirementMet: boolean
}

const additionalCompanyPrerequisites = ref<AdditionalCompanyPrerequisites | null>(null)
const showNewCompanyModal = ref(false)
const newCompanySuccessMessage = ref<string | null>(null)
const dashboardCities = ref<City[]>([])

async function loadAdditionalCompanyPrerequisites() {
  if (!auth.isAuthenticated) return
  try {
    const data = await gqlRequest<{ additionalCompanyPrerequisites: AdditionalCompanyPrerequisites }>(
      `{
        additionalCompanyPrerequisites {
          allRequirementsMet companyCount underMaxCap hasExistingCompany
          companyAgeTicks companyAgeRequirementMet ticksUntilAgeRequirementMet
          netIncomeInWindow profitabilityRequirementMet
          personalBalanceUsd balanceRequirementMet
        }
        cities { id name currencyCode }
      }`,
    )
    additionalCompanyPrerequisites.value = data.additionalCompanyPrerequisites
    if ((data as { additionalCompanyPrerequisites: AdditionalCompanyPrerequisites; cities?: City[] }).cities?.length) {
      dashboardCities.value = (data as { additionalCompanyPrerequisites: AdditionalCompanyPrerequisites; cities: City[] }).cities
    }
  } catch {
    // non-critical – silently ignore
  }
}

function onNewCompanyLaunched(companyId: string, companyName: string) {
  newCompanySuccessMessage.value = t('dashboard.newCompanySuccess', { name: companyName })
  void auth.fetchMe()
  void loadAdditionalCompanyPrerequisites()
  emit('company-launched', companyId)
  setTimeout(() => {
    router.push(`/onboarding?companyId=${companyId}`)
  }, 1800)
}

onMounted(() => {
  if (auth.isAuthenticated) {
    void loadAdditionalCompanyPrerequisites()
  }
})
</script>

<template>
  <!-- Person account mode (no company selected) -->
  <section v-if="isPersonAccount" class="person-account-panel mt-6 p-6 border border-divider rounded-xl bg-card">
    <div class="mb-1">
      <p class="text-xs font-bold tracking-widest uppercase text-muted m-0 mb-1.5">{{ t('dashboard.personModeEyebrow') }}</p>
      <h2 class="text-xl font-bold">{{ t('dashboard.personModeTitle') }}</h2>
    </div>

    <!-- Personal-account tab navigation -->
    <DashboardTabNav :tabs="personAccountTabs" :model-value="personAccountTab" class="mt-1" @update:model-value="setPersonAccountTab" />

    <!-- ── Overview tab ──────────────────────────────────────────────────── -->
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

      <!-- ── Launch New Company CTA ────────────────────────────────────────── -->
      <div
        v-if="additionalCompanyPrerequisites"
        class="launch-new-company-panel mt-6 p-5 border rounded-xl"
        :class="additionalCompanyPrerequisites.allRequirementsMet ? 'border-brand/40 bg-brand/5' : 'border-divider bg-card-raised'"
      >
        <div class="flex items-start justify-between gap-4 flex-wrap">
          <div>
            <h3 class="text-[0.9375rem] font-bold mb-1">{{ t('dashboard.launchNewCompany') }}</h3>
            <p class="text-sm text-muted m-0">{{ t('dashboard.launchNewCompanySubtitle') }}</p>
          </div>
          <button
            class="btn launch-new-company-btn"
            :class="additionalCompanyPrerequisites.allRequirementsMet ? 'btn-primary' : 'btn-secondary opacity-60 cursor-not-allowed'"
            :disabled="!additionalCompanyPrerequisites.allRequirementsMet"
            :title="additionalCompanyPrerequisites.allRequirementsMet ? '' : t('dashboard.launchNewCompanyLocked')"
            @click="additionalCompanyPrerequisites.allRequirementsMet && (showNewCompanyModal = true)"
          >
            {{ t('dashboard.launchNewCompanyBtn') }}
          </button>
        </div>

        <!-- Prerequisites checklist shown when NOT all met -->
        <ul v-if="!additionalCompanyPrerequisites.allRequirementsMet" class="nc-prereq-list mt-4 flex flex-col gap-1.5 text-sm list-none p-0 m-0">
          <li :class="additionalCompanyPrerequisites.companyAgeRequirementMet ? 'text-good' : 'text-muted'">
            {{
              additionalCompanyPrerequisites.companyAgeRequirementMet
                ? t('dashboard.prereqCompanyAgeMet')
                : t('dashboard.prereqCompanyAgeNotMet', { ticks: additionalCompanyPrerequisites.ticksUntilAgeRequirementMet })
            }}
          </li>
          <li :class="additionalCompanyPrerequisites.profitabilityRequirementMet ? 'text-good' : 'text-muted'">
            {{ additionalCompanyPrerequisites.profitabilityRequirementMet ? t('dashboard.prereqProfitabilityMet') : t('dashboard.prereqProfitabilityNotMet') }}
          </li>
          <li :class="additionalCompanyPrerequisites.balanceRequirementMet ? 'text-good' : 'text-muted'">
            {{
              additionalCompanyPrerequisites.balanceRequirementMet
                ? t('dashboard.prereqBalanceMet')
                : t('dashboard.prereqBalanceNotMet', { required: '200,000', current: Math.floor(additionalCompanyPrerequisites.personalBalanceUsd).toLocaleString() })
            }}
          </li>
          <li :class="additionalCompanyPrerequisites.underMaxCap ? 'text-good' : 'text-bad'">
            {{ additionalCompanyPrerequisites.underMaxCap ? t('dashboard.prereqMaxCapMet') : t('dashboard.prereqMaxCapNotMet') }}
          </li>
        </ul>

        <!-- Success message after launch -->
        <p v-if="newCompanySuccessMessage" class="mt-3 p-3 rounded-lg bg-[rgba(34,197,94,0.12)] text-good text-sm" role="status">
          {{ newCompanySuccessMessage }}
        </p>
      </div>
    </div>

    <!-- ── Create company tab ─────────────────────────────────────────────── -->
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

    <!-- ── Ledger tab ─────────────────────────────────────────────────────── -->
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

    <!-- ── Settings tab ───────────────────────────────────────────────────── -->
    <div v-show="personAccountTab === 'settings'" class="pt-5" role="tabpanel" :aria-label="t('dashboard.personTabSettings')">
      <DashboardPersonalSettingsPanel @saved="emit('refresh-dashboard')" />
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
          <RouterLink v-if="company.buildings.length > 0 && company.buildings[0]" :to="`/city/${company.buildings[0].cityId}`" class="btn btn-secondary"> 🗺️ {{ t('nav.cityMap') }} </RouterLink>
          <RouterLink :to="`/ledger/${company.id}`" class="btn btn-ghost"> 📒 {{ t('dashboard.viewLedger') }} </RouterLink>
          <RouterLink :to="`/company/${company.id}/contracts`" class="btn btn-ghost"> 🏛️ {{ t('dashboard.viewContracts') }} </RouterLink>
          <RouterLink :to="`/company/${company.id}/research`" class="btn btn-ghost"> 🔬 {{ t('research.dashboard.navLink') }} </RouterLink>
          <RouterLink :to="`/company/${company.id}/settings`" class="btn btn-ghost"> ⚙️ {{ t('dashboard.companySettings') }} </RouterLink>
        </div>
      </div>

      <!-- Section tab navigation -->
      <DashboardTabNav :tabs="tabsForCompany(company)" :model-value="activeTab" @update:model-value="setActiveTab($event as 'chat' | 'buildings' | 'overview' | 'activity' | 'pro')" />

      <!-- ── Overview tab ──────────────────────────────────────────────────── -->
      <div v-show="activeTab === 'overview'" class="tab-panel pt-5" role="tabpanel" aria-label="Overview">
        <!-- Financial summary and guidance -->
        <div class="grid grid-cols-2 max-[700px]:grid-cols-1 gap-3 mb-4">
          <FinancialSummaryCard :ledger="companyLedgers[company.id] ?? null" :loading="ledgerLoading" />
          <StarterGuidance :company="company" :revenue="companyLedgers[company.id]?.totalRevenue ?? 0" :net-income="companyLedgers[company.id]?.netIncome ?? 0" />
        </div>
        <div class="flex flex-wrap gap-2">
          <RouterLink :to="selectedCityId ? { name: 'market-intelligence', query: { city: selectedCityId } } : { name: 'market-intelligence' }" class="btn btn-secondary">
            📊 {{ t('dashboard.openMarketIntelligence') }}
          </RouterLink>
        </div>
      </div>

      <!-- ── Buildings tab ─────────────────────────────────────────────────── -->
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
                <span
                  v-if="building.type === 'MEDIA_HOUSE' && building.isAdvertisingActive"
                  class="advertising-active-badge inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[0.6rem] font-bold bg-emerald-500/20 text-emerald-700 dark:text-emerald-300 border border-emerald-400/30"
                >
                  📺 {{ t('dashboard.advertisingActive') }}
                </span>
                <!-- Destroyed badge -->
                <span
                  v-if="building.destroyedAtUtc"
                  class="destroyed-badge inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[0.6rem] font-bold bg-red-500/20 text-red-700 dark:text-red-400 border border-red-400/30"
                  :title="t('buildingDetail.destroyedHint')"
                >
                  ☠️ {{ t('buildingDetail.destroyedBadge') }}
                </span>
                <!-- Loan default badge -->
                <span
                  v-if="building.hasDefaultedCollateralLoan && !building.destroyedAtUtc"
                  class="loan-default-badge inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[0.6rem] font-bold bg-red-500/20 text-red-700 dark:text-red-400 border border-red-400/30"
                  :title="t('buildingDetail.loanDefaultHint')"
                >
                  ⚠️ {{ t('buildingDetail.loanDefaultBadge') }}
                </span>
                <span v-if="building.powerStatus && building.powerStatus !== 'POWERED'" :class="powerStatusClass(building.powerStatus)" :aria-label="getBuildingPowerLabel(building.powerStatus)">
                  {{ building.powerStatus === 'OFFLINE' ? '❌' : '⚡' }} {{ getBuildingPowerLabel(building.powerStatus) }}
                </span>
                <!-- Depletion warning badge for mine buildings when efficiency falls below 60% -->
                <span
                  v-if="building.type === 'MINE' && miningEfficiencyPercent(building) != null && miningEfficiencyPercent(building)! < 60"
                  class="depletion-risk-badge inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[0.6rem] font-bold border"
                  :class="
                    building.lotMaterialQuantity != null && building.lotMaterialQuantity <= 0
                      ? 'bg-red-500/20 text-red-700 dark:text-red-400 border-red-400/30'
                      : miningEfficiencyPercent(building)! < 30
                        ? 'bg-red-500/20 text-red-700 dark:text-red-400 border-red-400/30'
                        : 'bg-amber-500/20 text-amber-700 dark:text-amber-400 border-amber-400/30'
                  "
                  :title="t('mining.dashboardBadgeTooltip', { percent: Math.round(((building.lotMaterialQuantity ?? 0) / (building.lotOriginalMaterialQuantity ?? 1)) * 100) })"
                  :aria-label="t('mining.depletionRisk')"
                >
                  {{
                    building.lotMaterialQuantity != null && building.lotMaterialQuantity <= 0
                      ? `🔴 ${t('mining.depleted')}`
                      : miningEfficiencyPercent(building)! < 30
                        ? `🔴 ${t('mining.criticalDepletionRisk')}`
                        : `⚠️ ${t('mining.depletionRisk')}`
                  }}
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

      <!-- ── Activity tab ──────────────────────────────────────────────────── -->
      <div v-show="activeTab === 'activity'" class="tab-panel pt-5" role="tabpanel" aria-label="Activity">
        <PendingActionsTimeline :actions="pendingActions" :loading="pendingActionsLoading" :current-tick="gameState?.currentTick ?? null" />
      </div>

      <!-- ── Chat tab ──────────────────────────────────────────────────────── -->
      <div v-show="activeTab === 'chat'" class="tab-panel pt-5" role="tabpanel" aria-label="Chat">
        <DashboardChatPanel />
      </div>

      <!-- ── Pro tab ───────────────────────────────────────────────────────── -->
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

  <!-- Launch New Company Modal -->
  <NewCompanyModal :open="showNewCompanyModal" :cities="dashboardCities" :prerequisites="additionalCompanyPrerequisites" @close="showNewCompanyModal = false" @launched="onNewCompanyLaunched" />
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { useRouter, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { useAuthStore } from '@/stores/auth'
import { formatMoney } from '@/lib/currencyFormat'
import type { City, BuildingLot, Company, PurchaseLotResult, CurrencyBalance } from '@/types'

const { t, locale } = useI18n()
const router = useRouter()
const route = useRoute()
const auth = useAuthStore()
const { selectedCityId } = storeToRefs(auth)

const companyId = computed(() => route.params.companyId as string)

const loading = ref(false)
const lotsLoading = ref(false)
const error = ref<string | null>(null)
const cities = ref<City[]>([])
const availableLots = ref<BuildingLot[]>([])
const selectedLotId = ref('')
const selectedType = ref('')
const selectedMediaType = ref('')
const buildingName = ref('')
const submitting = ref(false)
// Bank setup fields
const depositRatePercent = ref<number>(3)
const lendingRatePercent = ref<number>(8)
// Resource filter for mine lots
const selectedResourceFilter = ref<string>('')
// Funding guidance
const playerBalances = ref<CurrencyBalance[]>([])
// Company bank accounts (for bank capital check)
const companyBankAccounts = ref<Array<{ id: string; currencyCode: string; balance: number; companyId: string | null; ownerType: string }>>([])

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

const buildingTypes = ['MINE', 'FACTORY', 'SALES_SHOP', 'RESEARCH_DEVELOPMENT', 'APARTMENT', 'COMMERCIAL', 'MEDIA_HOUSE', 'BANK', 'EXCHANGE', 'POWER_PLANT']
const PROPERTY_DEFAULT_AREA_SQM: Record<string, number> = {
  APARTMENT: 1800,
  COMMERCIAL: 1400,
}

const selectedCompany = computed<Company | null>(() => {
  return auth.player?.companies.find((company) => company.id === companyId.value) ?? null
})

const selectedLot = computed<BuildingLot | null>(() => {
  return availableLots.value.find((lot) => lot.id === selectedLotId.value) ?? null
})

const isPropertyTypeSelected = computed(() => selectedType.value === 'APARTMENT' || selectedType.value === 'COMMERCIAL')

const selectedPropertyAreaSqm = computed<number | null>(() => {
  if (!isPropertyTypeSelected.value) return null
  return PROPERTY_DEFAULT_AREA_SQM[selectedType.value] ?? null
})

const selectedCityObj = computed(() => cities.value.find((c) => c.id === selectedCityId.value) ?? null)

/** Base capital requirement for the selected city's currency (mirrors backend logic). */
const bankBaseCapitalRequired = computed<number>(() => {
  const cc = selectedCityObj.value?.currencyCode?.toUpperCase() ?? 'EUR'
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

/** Sum of all company bank-account balances in the city currency. */
const companyBankBalanceInCityCurrency = computed<number>(() => {
  if (!selectedCompany.value) return 0
  const cc = selectedCityCurrencyCode.value
  return companyBankAccounts.value.filter((a) => a.ownerType === 'COMPANY' && a.companyId === selectedCompany.value!.id && a.currencyCode.toUpperCase() === cc).reduce((sum, a) => sum + a.balance, 0)
})

const companyHasBankCapital = computed<boolean>(() => {
  if (!selectedCompany.value) return false
  return companyBankBalanceInCityCurrency.value >= bankBaseCapitalRequired.value
})

const bankCapitalInsufficientMessage = computed<string>(() =>
  t('buildings.bankCapitalInsufficient', {
    amount: formatCurrency(bankBaseCapitalRequired.value),
  }),
)

/** City currency code for the selected city (EUR if none selected). */
const selectedCityCurrencyCode = computed<string>(() => selectedCityObj.value?.currencyCode?.toUpperCase() ?? 'EUR')

/** Construction cost by building type, mirroring GameConstants.ConstructionCost on the backend. */
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

/** Total cost for the current selection: lot asking price + construction cost. */
const selectedLotTotalCost = computed<number>(() => {
  if (!selectedLot.value || !selectedType.value) return 0
  const construction = CONSTRUCTION_COSTS[selectedType.value] ?? 10_000
  return selectedLot.value.price + construction
})

/** Company's total balance in the destination city currency across all bank accounts. */
const availableLocalBalance = computed<number>(() => {
  const cc = selectedCityCurrencyCode.value
  if (cc === 'EUR') return 0
  // Use company bank accounts (already fetched)
  if (selectedCompany.value) {
    return companyBankAccounts.value.filter((a) => a.ownerType === 'COMPANY' && a.companyId === selectedCompany.value!.id && a.currencyCode.toUpperCase() === cc).reduce((sum, a) => sum + a.balance, 0)
  }
  // Fallback to player-level balances if no company selected
  return playerBalances.value.find((b) => b.currencyCode === cc)?.balance ?? 0
})

/**
 * Funding gap type:
 *  'missing_account' ÔÇô player has no balance record (or zero) for the destination currency
 *  'insufficient_funds' ÔÇô player has some balance but not enough for the selected lot total
 *  null ÔÇô no gap (EUR city, or player is sufficiently funded)
 */
const fundingGapType = computed<'missing_account' | 'insufficient_funds' | null>(() => {
  const cc = selectedCityCurrencyCode.value
  if (cc === 'EUR') return null
  const balance = availableLocalBalance.value
  if (balance <= 0) return 'missing_account'
  // Only check lot-total when a lot is selected; otherwise flag missing account when zero
  if (selectedLot.value && balance < selectedLotTotalCost.value) return 'insufficient_funds'
  return null
})

/** True when any funding gap is present (either missing account or insufficient balance). */
const hasFundingGap = computed<boolean>(() => fundingGapType.value !== null)

const canSubmit = computed(() => {
  if (!selectedType.value || !selectedCityId.value || !selectedLot.value) return false
  if (selectedType.value === 'MEDIA_HOUSE' && !selectedMediaType.value) return false
  return true
})

onMounted(async () => {
  if (!auth.isAuthenticated) {
    router.push('/login')
    return
  }

  if (!auth.player) {
    await auth.fetchMe()
  }

  loading.value = true
  try {
    const [citiesData, balancesData, accountsData] = await Promise.all([
      gqlRequest<{ cities: City[] }>('{ cities { id name countryCode currencyCode population } }'),
      gqlRequest<{ playerCurrencyBalances: CurrencyBalance[] }>('{ playerCurrencyBalances { currencyCode currencySymbol balance } }'),
      gqlRequest<{ myBankAccounts: Array<{ id: string; currencyCode: string; balance: number; companyId: string | null; ownerType: string }> }>(
        '{ myBankAccounts { id currencyCode balance companyId ownerType } }',
      ),
    ])
    cities.value = citiesData.cities
    playerBalances.value = balancesData.playerCurrencyBalances ?? []
    companyBankAccounts.value = accountsData.myBankAccounts ?? []

    if (!selectedCompany.value) {
      error.value = t('cityMap.noCompany')
    }
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load cities'
  } finally {
    loading.value = false
  }

  // Pre-select building type if passed as query param (e.g. from "Acquire a Bank" button)
  const typeParam = route.query.type as string | undefined
  if (typeParam && buildingTypes.includes(typeParam)) {
    selectedType.value = typeParam
  }

  if (!selectedCityId.value && cities.value.length > 0) {
    const cityUsage = new Map<string, number>()
    for (const company of auth.player?.companies ?? []) {
      for (const building of company.buildings ?? []) {
        cityUsage.set(building.cityId, (cityUsage.get(building.cityId) ?? 0) + 1)
      }
    }
    let preferredCityId: string | null = null
    let preferredCount = -1
    for (const [cityId, count] of cityUsage.entries()) {
      if (count > preferredCount) {
        preferredCount = count
        preferredCityId = cityId
      }
    }

    const preferredCity = preferredCityId ? cities.value.find((c) => c.id === preferredCityId) : null
    const fallbackCity = preferredCity ?? cities.value[0]
    if (fallbackCity) {
      auth.switchCity(fallbackCity.id)
    }
  }
})

watch([selectedCityId, selectedType], async ([cityId, buildingType]) => {
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
      return !lot.ownerCompanyId && supportedTypes.includes(buildingType)
    })
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : t('cityMap.purchaseError')
  } finally {
    lotsLoading.value = false
  }
})

/** Unique resource types available in the current city for mine lots. */
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

/** Lots after applying optional resource type filter. */
const filteredLots = computed(() => {
  if (!selectedResourceFilter.value || selectedType.value !== 'MINE') return availableLots.value
  return availableLots.value.filter((lot) => lot.resourceType?.slug === selectedResourceFilter.value)
})

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

async function buyBuilding() {
  if (!canSubmit.value || !selectedCompany.value || !selectedLot.value) return

  // Block bank purchase when company lacks sufficient capital
  if (selectedType.value === 'BANK' && !companyHasBankCapital.value) {
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
          buildingType: selectedType.value,
          buildingName: buildingName.value.trim() || null,
          mediaType: selectedType.value === 'MEDIA_HOUSE' ? selectedMediaType.value || null : null,
        },
      },
    )

    const buildingId = data.purchaseLot.building.id

    if (selectedType.value === 'BANK') {
      // Activate the bank with the base capital deposit
      try {
        await gqlRequest(INITIATE_BASE_DEPOSIT_MUTATION, {
          bankBuildingId: buildingId,
        })
      } catch {
        // Non-fatal: bank was purchased; base deposit can be made from the bank management page
      }

      // Set the configured interest rates
      try {
        await gqlRequest(SET_BANK_RATES_MUTATION, {
          input: {
            bankBuildingId: buildingId,
            depositInterestRatePercent: depositRatePercent.value,
            lendingInterestRatePercent: lendingRatePercent.value,
          },
        })
      } catch {
        // Non-fatal: rates can be configured on the bank management page
      }
    }

    router.push(selectedType.value === 'BANK' ? `/bank/${buildingId}` : `/building/${buildingId}`)
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : t('cityMap.purchaseError')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="container py-8 px-4 max-w-4xl">
    <!-- Back nav -->
    <div class="mb-6">
      <RouterLink to="/dashboard" class="inline-flex items-center gap-1.5 text-sm text-muted hover:text-brand no-underline transition-colors">
        <span>←</span> {{ t('buildingDetail.backToDashboard') }}
      </RouterLink>
    </div>

    <!-- Main card -->
    <div class="bg-card border border-divider rounded-xl p-8">
      <h1 class="text-2xl font-bold mb-6">{{ t('buildings.title') }}</h1>

      <!-- Error alert -->
      <div v-if="error" class="flex items-start gap-3 p-3 mb-5 bg-[rgba(248,113,113,0.1)] border border-[rgba(248,113,113,0.3)] text-bad rounded-lg text-sm" role="alert">{{ error }}</div>

      <!-- Loading -->
      <div v-if="loading" class="text-center py-8 text-muted">{{ t('common.loading') }}</div>

      <template v-else>
        <!-- Step 1: Building type (hidden when ?type= is pre-selected in URL) -->
        <div v-if="!route.query.type || !buildingTypes.includes(String(route.query.type))" class="mb-8">
          <h2 class="text-lg font-semibold mb-3">{{ t('buildings.selectType') }}</h2>
          <div class="grid gap-3 [grid-template-columns:repeat(auto-fill,minmax(180px,1fr))]">
            <button
              v-for="bType in buildingTypes"
              :key="bType"
              class="type-card flex flex-col items-center gap-1.5 py-5 px-3 border-2 border-divider rounded-lg bg-page cursor-pointer transition-all duration-200 text-body text-center hover:border-brand hover:-translate-y-0.5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand"
              :class="selectedType === bType ? 'selected border-brand bg-[rgba(0,71,255,0.08)] ring-1 ring-brand' : ''"
              @click="selectedType = bType"
            >
              <span class="text-3xl leading-none">{{ t(`buildings.typeIcons.${bType}`) }}</span>
              <span class="font-bold text-sm">{{ t(`buildings.types.${bType}`) }}</span>
              <span class="text-[0.6875rem] text-muted leading-snug">{{ t(`buildings.typeDescriptions.${bType}`) }}</span>
            </button>
          </div>
        </div>

        <!-- Step 2: Name (city comes from navbar context switcher) -->
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

            <!-- Strategy guide cards -->
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
                <template v-if="fundingGapType === 'missing_account'"> {{ t('buildings.fundingGapTitleMissing', { currency: selectedCityCurrencyCode }) }} </template>
                <template v-else> {{ t('buildings.fundingGapTitleInsufficient', { currency: selectedCityCurrencyCode }) }} </template>
              </strong>
              <p class="text-sm text-muted m-0">
                <template v-if="fundingGapType === 'missing_account'"> {{ t('buildings.fundingGapBodyMissing', { currency: selectedCityCurrencyCode }) }} </template>
                <template v-else> {{ t('buildings.fundingGapBodyInsufficient', { currency: selectedCityCurrencyCode }) }} </template>
              </p>
              <!-- Amount breakdown (insufficient funds only) -->
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
              <!-- CTA buttons -->
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

          <!-- Company balance banner (shows bank account balance in selected city currency, or all-currency summary) -->
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

        <!-- Step 3: Land selection (shown after type + city are chosen) -->
        <div v-if="selectedType && selectedCityId" class="mb-8">
          <div class="flex items-center justify-between gap-4 mb-3">
            <h2 class="text-lg font-semibold">{{ t('buildings.selectLand') }}</h2>
            <span v-if="lotsLoading" class="text-[0.8125rem] text-muted">{{ t('common.loading') }}</span>
          </div>

          <!-- Empty state -->
          <div v-if="!lotsLoading && availableLots.length === 0" class="mt-4 p-4 border border-divider rounded-lg bg-page text-muted text-sm">{{ t('buildings.noAvailableLand') }}</div>

          <!-- Mine resource filter (only when MINE type selected and multiple resources available) -->
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

          <!-- Lot grid -->
          <div v-else-if="!lotsLoading && filteredLots.length === 0 && availableLots.length > 0" class="mt-4 p-4 border border-divider rounded-lg bg-page text-muted text-sm">
            {{ t('buildings.noLotsForResource') }}
          </div>

          <div v-if="filteredLots.length > 0" class="grid gap-3 [grid-template-columns:repeat(auto-fill,minmax(220px,1fr))]">
            <button
              v-for="lot in filteredLots"
              :key="lot.id"
              class="lot-card flex flex-col gap-2 p-4 border-2 border-divider rounded-lg bg-page text-body text-left cursor-pointer transition-all duration-200 hover:border-brand hover:-translate-y-px focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand"
              :class="selectedLotId === lot.id ? 'selected border-brand ring-1 ring-brand bg-[rgba(0,71,255,0.08)]' : ''"
              @click="selectedLotId = lot.id"
            >
              <div class="flex justify-between items-baseline gap-4">
                <span class="font-bold">{{ lot.name }}</span>
                <span class="text-sm font-bold text-good shrink-0">{{ formatCurrency(lot.price) }}</span>
              </div>
              <span class="text-[0.8125rem] text-muted">{{ districtLabel(lot.district) }}</span>
              <span class="text-[0.8125rem] text-muted"> {{ t('buildings.populationIndex') }}: {{ formatPopulationIndex(lot.populationIndex) }} </span>
              <span class="text-[0.8125rem] text-muted"> {{ t('buildings.appraisedValue') }}: {{ formatCurrency(lot.basePrice) }} </span>
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
              <span
                >{{ t('buildings.askingPrice') }}: <strong class="text-body">{{ formatCurrency(selectedLot.price) }}</strong>
                <span
                  v-if="selectedLot.resourceType && selectedLot.price > selectedLot.basePrice"
                  class="buy-building-resource-premium-badge ml-1 inline-block rounded px-1 py-0.5 text-[0.7rem] font-semibold text-violet-700 bg-violet-500/15 align-middle cursor-help"
                  :title="t('cityMap.resourcePremiumTooltip')"
                  >{{ t('cityMap.resourcePremium') }}</span
                >
              </span>
              <span>{{ t('buildings.populationIndex') }}: {{ formatPopulationIndex(selectedLot.populationIndex) }}</span>
              <span v-if="selectedPropertyAreaSqm != null"
                >{{ t('buildings.propertySize') }}: <strong class="text-body">{{ formatSqm(selectedPropertyAreaSqm) }}</strong></span
              >
            </div>
            <!-- Mining deposit investment summary (shown when MINE selected and lot has resource) -->
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
      </template>
    </div>
  </div>
</template>

<style scoped>
.buy-building-resource-badge {
  display: inline-block;
  font-size: 0.7rem;
  font-weight: 600;
  color: var(--color-primary);
  background: rgba(139, 92, 246, 0.1);
  border: 1px solid rgba(139, 92, 246, 0.25);
  border-radius: 999px;
  padding: 0.1rem 0.45rem;
  cursor: help;
}

.buy-building-resource-premium-badge {
  display: inline-block;
  font-size: 0.7rem;
  font-weight: 600;
  color: #7c3aed;
  background: rgba(139, 92, 246, 0.12);
  border-radius: var(--radius-sm, 4px);
  padding: 0.1rem 0.3rem;
  vertical-align: middle;
  cursor: help;
}

.buy-building-mining-summary {
  background: rgba(139, 92, 246, 0.06);
  border: 1px solid rgba(139, 92, 246, 0.2);
  border-radius: var(--radius-md, 8px);
  padding: 0.875rem;
  margin-top: 0.5rem;
}

.buy-building-mining-summary-title {
  font-size: 0.875rem;
  font-weight: 600;
  margin: 0 0 0.625rem 0;
  color: #7c3aed;
}

.buy-building-mining-summary-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.5rem 1rem;
  margin-bottom: 0.625rem;
}

.buy-building-mining-summary-item {
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
}

.buy-building-mining-hint {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
  margin: 0;
  line-height: 1.45;
}

.buy-building-quality-badge {
  font-size: 0.75rem;
  font-weight: 600;
  padding: 0.1rem 0.375rem;
  border-radius: 999px;
  display: inline-block;
}

.buy-building-quality-badge.quality-excellent {
  background: rgba(34, 197, 94, 0.15);
  color: #16a34a;
}

.buy-building-quality-badge.quality-good {
  background: rgba(59, 130, 246, 0.15);
  color: #2563eb;
}

.buy-building-quality-badge.quality-fair {
  background: rgba(234, 179, 8, 0.15);
  color: #ca8a04;
}

.buy-building-quality-badge.quality-poor {
  background: rgba(239, 68, 68, 0.15);
  color: #dc2626;
}
</style>

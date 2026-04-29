<template src="./BuyBuildingView.template.html"></template>

<script setup lang="ts">
/* oxlint-disable no-unused-vars */
/* eslint-disable @typescript-eslint/no-unused-vars */
// Split-file SFC: script symbols are consumed by BuyBuildingView.template.html.
 
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
 *  'missing_account' – player has no balance record (or zero) for the destination currency
 *  'insufficient_funds' – player has some balance but not enough for the selected lot total
 *  null – no gap (EUR city, or player is sufficiently funded)
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
  if (quality >= 0.8) return 'quality-excellent'
  if (quality >= 0.6) return 'quality-good'
  if (quality >= 0.4) return 'quality-fair'
  return 'quality-poor'
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

<style scoped src="./BuyBuildingView.styles.css"></style>
<script setup lang="ts">
/* eslint-disable @typescript-eslint/no-unused-vars */
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useTickRefresh } from '@/composables/useTickRefresh'
import { useScrollPreservation } from '@/composables/useScrollPreservation'
import { gqlRequest } from '@/lib/graphql'
import { formatInGameTime, formatGameTickTime } from '@/lib/gameTime'
import { formatMoney } from '@/lib/currencyFormat'
import CurrencyAmount from '@/components/numbers/CurrencyAmount.vue'
import type { CompanyLedgerSummary, LedgerEntryResult } from '@/types'

const { t, locale } = useI18n()
const route = useRoute()
const router = useRouter()
const { saveScrollPosition, restoreScrollPosition } = useScrollPreservation()

const companyId = computed(() => route.params.companyId as string)
const ledger = ref<CompanyLedgerSummary | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)
const drillCategory = ref<string | null>(null)
const drillEntries = ref<LedgerEntryResult[]>([])
const drillLoading = ref(false)
const selectedGameYear = ref<number | null>(null)
const selectedResolvedGameYear = computed(() => selectedGameYear.value ?? ledger.value?.gameYear ?? null)

const LEDGER_QUERY = `
  query GetCompanyLedger($companyId: UUID!, $gameYear: Int) {
    companyLedger(companyId: $companyId, gameYear: $gameYear) {
      companyId companyName gameYear isCurrentGameYear currentCash
      primaryCurrencyCode primaryCurrencySymbol hasMixedCurrencies
      totalRevenue totalMediaHouseIncome totalPurchasingCosts totalShippingCosts totalLaborCosts totalEnergyCosts totalMarketingCosts totalTaxPaid totalOtherCosts taxableIncome estimatedIncomeTax netIncome
      totalDepositInterestReceived totalDepositInterestPaid totalLoanInterestIncome totalLoanInterestExpense
      propertyValue propertyAppreciation buildingValue inventoryValue totalDepositsPlaced totalAssets totalPropertyPurchases
      totalStockPurchaseCashOut totalStockSaleCashIn cashFromOperations cashFromInvestments cashFromBanking firstRecordedTick lastRecordedTick
      incomeTaxDueAtTick incomeTaxDueGameTimeUtc incomeTaxDueGameYear isIncomeTaxSettled
      history {
        gameYear isCurrentGameYear totalRevenue totalLaborCosts totalEnergyCosts netIncome totalTaxPaid taxableIncome estimatedIncomeTax firstRecordedTick lastRecordedTick
      }
      buildingSummaries { buildingId buildingName buildingType revenue costs currencyCode currencySymbol }
    }
  }
`

const DRILL_QUERY = `
  query GetLedgerDrillDown($companyId: UUID!, $category: String!, $gameYear: Int) {
    ledgerDrillDown(companyId: $companyId, category: $category, gameYear: $gameYear) {
      id category description amount recordedAtTick
      buildingId buildingName buildingType buildingUnitId
      productTypeId productName resourceTypeId resourceName
      currencyCode currencySymbol
    }
  }
`

async function fetchLedger(isRefresh = false) {
  if (!isRefresh) loading.value = true
  error.value = null
  try {
    const data = await gqlRequest<{ companyLedger: CompanyLedgerSummary | null }>(LEDGER_QUERY, {
      companyId: companyId.value,
      gameYear: selectedGameYear.value,
    })
    if (!data.companyLedger) {
      error.value = t('ledger.notFound')
      return
    }
    ledger.value = data.companyLedger
  } catch (e) {
    error.value = e instanceof Error ? e.message : t('ledger.loadFailed')
  } finally {
    if (!isRefresh) loading.value = false
  }
}

async function loadDrillEntries(category: string, isRefresh = false) {
  if (!isRefresh) drillLoading.value = true
  drillEntries.value = []
  try {
    const data = await gqlRequest<{ ledgerDrillDown: LedgerEntryResult[] }>(DRILL_QUERY, {
      companyId: companyId.value,
      category,
      gameYear: selectedResolvedGameYear.value,
    })
    drillEntries.value = data.ledgerDrillDown
  } catch {
    drillEntries.value = []
  } finally {
    if (!isRefresh) drillLoading.value = false
  }
}

async function toggleDrill(category: string) {
  if (drillCategory.value === category) {
    drillCategory.value = null
    drillEntries.value = []
    return
  }
  drillCategory.value = category
  await loadDrillEntries(category)
}

async function selectGameYear(gameYear: number | null) {
  selectedGameYear.value = gameYear
  drillCategory.value = null
  drillEntries.value = []
  await fetchLedger()
}

function formatAmount(amount: number, currencyCode?: string): string {
  const code = getEffectiveCurrencyCode(currencyCode)
  if (amount == null || !isFinite(amount) || isNaN(amount)) return '—'
  return formatMoney(amount, code, locale.value)
}

function getEffectiveCurrencyCode(currencyCode?: string | null): string {
  return currencyCode ?? ledger.value?.primaryCurrencyCode ?? 'EUR'
}

function showInlineCurrencyBadge(entryCurrencyCode?: string | null): boolean {
  return (entryCurrencyCode ?? 'EUR') !== (ledger.value?.primaryCurrencyCode ?? 'EUR')
}

function amountClass(amount: number): string {
  return amount >= 0 ? 'amount-positive' : 'amount-negative'
}

function formatGameTime(value: string): string {
  return formatInGameTime(value, locale.value)
}

onMounted(fetchLedger)

useTickRefresh(async () => {
  if (selectedGameYear.value !== null && !ledger.value?.isCurrentGameYear) {
    return
  }

  const scrollPos = saveScrollPosition()
  await fetchLedger(true)
  if (drillCategory.value) {
    await loadDrillEntries(drillCategory.value, true)
  }
  await restoreScrollPosition(scrollPos)
})
</script>

<template src="./LedgerView.template.html"></template>

<style scoped src="./LedgerView.styles.css"></style>

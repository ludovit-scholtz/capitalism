<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useTickRefresh } from '@/composables/useTickRefresh'
import { useScrollPreservation } from '@/composables/useScrollPreservation'
import { gqlRequest } from '@/lib/graphql'
import type { CompanyLedgerSummary, LedgerEntryResult } from '@/types'
import LedgerMainContent from '@/components/ledger/LedgerMainContent.vue'

const { t } = useI18n()
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
      eventTag eventDescription
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

async function handleDrillToggle(category: string | null) {
  if (category === null) {
    drillCategory.value = null
    drillEntries.value = []
    return
  }
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

<template>
  <div class="ledger-view container">
    <div class="ledger-header">
      <button class="btn btn-ghost" @click="router.push('/dashboard')">← {{ t('common.back') }}</button>
      <div>
        <p class="ledger-eyebrow">{{ t('ledger.eyebrow') }}</p>
        <h1 class="ledger-title">{{ ledger?.companyName ?? t('ledger.title') }}</h1>
      </div>
    </div>

    <div v-if="loading" class="state-box">
      <span class="state-icon">⏳</span>
      <p>{{ t('common.loading') }}</p>
    </div>

    <div v-else-if="error" class="state-box state-error">
      <span class="state-icon">⚠️</span>
      <p>{{ error }}</p>
      <button class="btn btn-secondary" @click="() => fetchLedger()">{{ t('common.tryAgain') }}</button>
    </div>

    <LedgerMainContent
      v-else-if="ledger"
      :ledger="ledger"
      :drill-category="drillCategory"
      :drill-entries="drillEntries"
      :drill-loading="drillLoading"
      :selected-resolved-game-year="selectedResolvedGameYear"
      @drill-toggle="handleDrillToggle"
      @select-game-year="selectGameYear"
    />
  </div>
</template>

<style scoped>
.ledger-header {
  display: flex;
  align-items: center;
  gap: 1rem;
  margin-bottom: 2rem;
}
.ledger-eyebrow {
  font-size: 0.8125rem;
  font-weight: 600;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--color-accent);
  margin: 0 0 0.25rem;
}
.ledger-title {
  font-size: 1.75rem;
  margin: 0;
}
.state-box {
  text-align: center;
  padding: 3rem 1rem;
  color: var(--color-text-secondary);
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1rem;
}
.state-error {
  color: var(--color-danger, #ef4444);
}
.state-icon {
  font-size: 2.5rem;
}
</style>

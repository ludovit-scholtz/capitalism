<script setup lang="ts">
 
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
  if (amount == null || !isFinite(amount) || isNaN(amount)) return 'ÔÇö'
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

<template>
  <div class="ledger-view container">
    <div class="ledger-header">
      <button class="btn btn-ghost" @click="router.push('/dashboard')">ÔćÉ {{ t('common.back') }}</button>
      <div>
        <p class="ledger-eyebrow">{{ t('ledger.eyebrow') }}</p>
        <h1 class="ledger-title">{{ ledger?.companyName ?? t('ledger.title') }}</h1>
      </div>
    </div>

    <div v-if="loading" class="state-box">
      <span class="state-icon">ÔĆ│</span>
      <p>{{ t('common.loading') }}</p>
    </div>

    <div v-else-if="error" class="state-box state-error">
      <span class="state-icon">ÔÜá´ŞĆ</span>
      <p>{{ error }}</p>
      <button class="btn btn-secondary" @click="() => fetchLedger()">{{ t('common.tryAgain') }}</button>
    </div>

    <div v-else-if="ledger" class="ledger-content">
      <div class="kpi-row">
        <div class="kpi-card">
          <span class="kpi-label">{{ t('ledger.gameYear') }}</span>
          <span class="kpi-value">{{ t('ledger.gameYearLabel', { year: ledger.gameYear }) }}</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">{{ t('ledger.cash') }}</span>
          <span class="kpi-value" :class="amountClass(ledger.currentCash)">
            <CurrencyAmount :amount="ledger.currentCash" :currency="ledger.primaryCurrencyCode" />
          </span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">{{ t('ledger.netIncome') }}</span>
          <span class="kpi-value" :class="amountClass(ledger.netIncome)">
            <CurrencyAmount :amount="ledger.netIncome" :currency="ledger.primaryCurrencyCode" />
          </span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">{{ t('ledger.taxableIncome') }}</span>
          <span class="kpi-value" :class="amountClass(ledger.taxableIncome)">
            <CurrencyAmount :amount="ledger.taxableIncome" :currency="ledger.primaryCurrencyCode" />
          </span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">{{ t('ledger.estimatedIncomeTax') }}</span>
          <span class="kpi-value amount-negative">
            <CurrencyAmount :amount="-ledger.estimatedIncomeTax" :currency="ledger.primaryCurrencyCode" />
          </span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">{{ t('ledger.totalAssets') }}</span>
          <span class="kpi-value">
            <CurrencyAmount :amount="ledger.totalAssets" :currency="ledger.primaryCurrencyCode" />
          </span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">{{ t('ledger.currency') }}</span>
          <span class="kpi-value">
            <span class="currency-badge">{{ ledger.primaryCurrencyCode }}</span>
            <span v-if="ledger.hasMixedCurrencies" class="currency-mixed-hint">{{ t('ledger.mixedCurrencies') }}</span>
          </span>
        </div>
      </div>

      <div v-if="ledger.lastRecordedTick === 0" class="info-banner">
        <span>­čôŐ</span>
        <span>{{ t('ledger.noHistoryYet') }}</span>
      </div>
      <p v-else class="tick-range-note">
        {{
          t('ledger.dataRange', {
            fromTime: formatGameTickTime(ledger.firstRecordedTick, locale),
            toTime: formatGameTickTime(ledger.lastRecordedTick, locale),
          })
        }}
      </p>

      <div class="year-meta-row">
        <div class="statement-card meta-card">
          <h2 class="statement-title">­čžż {{ t('ledger.incomeTaxSchedule') }}</h2>
          <p class="meta-copy">
            {{
              ledger.isIncomeTaxSettled
                ? t('ledger.incomeTaxSettledAtTick', { time: formatGameTime(ledger.incomeTaxDueGameTimeUtc) })
                : t('ledger.incomeTaxDueAtTick', { time: formatGameTime(ledger.incomeTaxDueGameTimeUtc) })
            }}
          </p>
          <p class="meta-copy">{{ t('ledger.incomeTaxDueYear', { year: ledger.incomeTaxDueGameYear }) }}</p>
        </div>

        <div class="statement-card meta-card">
          <h2 class="statement-title">­čŚé´ŞĆ {{ t('ledger.historyTitle') }}</h2>
          <div class="history-buttons">
            <button
              v-for="yearItem in ledger.history"
              :key="yearItem.gameYear"
              type="button"
              class="history-button"
              :class="{ active: yearItem.gameYear === selectedResolvedGameYear }"
              @click="selectGameYear(yearItem.isCurrentGameYear ? null : yearItem.gameYear)"
            >
              <span>{{ t('ledger.gameYearShort', { year: yearItem.gameYear }) }}</span>
              <span :class="amountClass(yearItem.netIncome)">
                <CurrencyAmount :amount="yearItem.netIncome" :currency="ledger.primaryCurrencyCode" />
              </span>
            </button>
          </div>
        </div>
      </div>

      <div v-if="!ledger.isCurrentGameYear" class="info-banner historical-note">
        <span>­čĽ░´ŞĆ</span>
        <span>{{ t('ledger.historicalYearNote') }}</span>
      </div>

      <div class="statements-grid">
        <div class="statement-card">
          <h2 class="statement-title">­čôł {{ t('ledger.incomeStatement') }}</h2>
          <div class="statement-rows">
            <div class="statement-row">
              <span class="row-label">{{ t('ledger.revenue') }}</span>
              <span class="amount-positive">{{ formatAmount(ledger.totalRevenue) }}</span>
              <button class="drill-btn" :class="{ active: drillCategory === 'REVENUE' }" :aria-label="t('ledger.drillDown') + ': ' + t('ledger.revenue')" @click="toggleDrill('REVENUE')">
                {{ drillCategory === 'REVENUE' ? 'Ôľ▓' : 'Ôľ╝' }}
              </button>
            </div>
            <div v-if="(ledger.totalMediaHouseIncome ?? 0) > 0" class="statement-row media-house-income-row">
              <span class="row-label">­čô║ {{ t('ledger.mediaHouseIncome') }}</span>
              <span class="amount-positive">{{ formatAmount(ledger.totalMediaHouseIncome ?? 0) }}</span>
              <button
                class="drill-btn"
                :class="{ active: drillCategory === 'MEDIA_HOUSE_INCOME' }"
                :aria-label="t('ledger.drillDown') + ': ' + t('ledger.mediaHouseIncome')"
                @click="toggleDrill('MEDIA_HOUSE_INCOME')"
              >
                {{ drillCategory === 'MEDIA_HOUSE_INCOME' ? 'Ôľ▓' : 'Ôľ╝' }}
              </button>
            </div>
            <div v-if="(ledger.totalDepositInterestReceived ?? 0) > 0" class="statement-row">
              <span class="row-label">{{ t('ledger.depositInterestReceived') }}</span>
              <span class="amount-positive">{{ formatAmount(ledger.totalDepositInterestReceived ?? 0) }}</span>
              <button
                class="drill-btn"
                :class="{ active: drillCategory === 'DEPOSIT_INTEREST_RECEIVED' }"
                :aria-label="t('ledger.drillDown') + ': ' + t('ledger.depositInterestReceived')"
                @click="toggleDrill('DEPOSIT_INTEREST_RECEIVED')"
              >
                {{ drillCategory === 'DEPOSIT_INTEREST_RECEIVED' ? 'Ôľ▓' : 'Ôľ╝' }}
              </button>
            </div>
            <div v-if="(ledger.totalLoanInterestIncome ?? 0) > 0" class="statement-row">
              <span class="row-label">{{ t('ledger.loanInterestIncome') }}</span>
              <span class="amount-positive">{{ formatAmount(ledger.totalLoanInterestIncome ?? 0) }}</span>
              <button
                class="drill-btn"
                :class="{ active: drillCategory === 'LOAN_INTEREST_INCOME' }"
                :aria-label="t('ledger.drillDown') + ': ' + t('ledger.loanInterestIncome')"
                @click="toggleDrill('LOAN_INTEREST_INCOME')"
              >
                {{ drillCategory === 'LOAN_INTEREST_INCOME' ? 'Ôľ▓' : 'Ôľ╝' }}
              </button>
            </div>
            <div class="statement-row cost-row">
              <span class="row-label">{{ t('ledger.purchasingCosts') }}</span>
              <span class="amount-negative">{{ formatAmount(-ledger.totalPurchasingCosts) }}</span>
              <button
                class="drill-btn"
                :class="{ active: drillCategory === 'PURCHASING_COST' }"
                :aria-label="t('ledger.drillDown') + ': ' + t('ledger.purchasingCosts')"
                @click="toggleDrill('PURCHASING_COST')"
              >
                {{ drillCategory === 'PURCHASING_COST' ? 'Ôľ▓' : 'Ôľ╝' }}
              </button>
            </div>
            <div v-if="ledger.totalShippingCosts > 0" class="statement-row cost-row">
              <span class="row-label">{{ t('ledger.shippingCosts') }}</span>
              <span class="amount-negative">{{ formatAmount(-ledger.totalShippingCosts) }}</span>
              <button
                class="drill-btn"
                :class="{ active: drillCategory === 'SHIPPING_COST' }"
                :aria-label="t('ledger.drillDown') + ': ' + t('ledger.shippingCosts')"
                @click="toggleDrill('SHIPPING_COST')"
              >
                {{ drillCategory === 'SHIPPING_COST' ? 'Ôľ▓' : 'Ôľ╝' }}
              </button>
            </div>
            <div v-if="ledger.totalLaborCosts > 0" class="statement-row cost-row">
              <span class="row-label">{{ t('ledger.laborCosts') }}</span>
              <span class="amount-negative">{{ formatAmount(-ledger.totalLaborCosts) }}</span>
              <button class="drill-btn" :class="{ active: drillCategory === 'LABOR_COST' }" :aria-label="t('ledger.drillDown') + ': ' + t('ledger.laborCosts')" @click="toggleDrill('LABOR_COST')">
                {{ drillCategory === 'LABOR_COST' ? 'Ôľ▓' : 'Ôľ╝' }}
              </button>
            </div>
            <div v-if="ledger.totalEnergyCosts > 0" class="statement-row cost-row">
              <span class="row-label">{{ t('ledger.energyCosts') }}</span>
              <span class="amount-negative">{{ formatAmount(-ledger.totalEnergyCosts) }}</span>
              <button class="drill-btn" :class="{ active: drillCategory === 'ENERGY_COST' }" :aria-label="t('ledger.drillDown') + ': ' + t('ledger.energyCosts')" @click="toggleDrill('ENERGY_COST')">
                {{ drillCategory === 'ENERGY_COST' ? 'Ôľ▓' : 'Ôľ╝' }}
              </button>
            </div>
            <div v-if="ledger.totalMarketingCosts > 0" class="statement-row cost-row">
              <span class="row-label">{{ t('ledger.marketingCosts') }}</span>
              <span class="amount-negative">{{ formatAmount(-ledger.totalMarketingCosts) }}</span>
              <button class="drill-btn" :class="{ active: drillCategory === 'MARKETING' }" :aria-label="t('ledger.drillDown') + ': ' + t('ledger.marketingCosts')" @click="toggleDrill('MARKETING')">
                {{ drillCategory === 'MARKETING' ? 'Ôľ▓' : 'Ôľ╝' }}
              </button>
            </div>
            <div v-if="(ledger.totalDepositInterestPaid ?? 0) > 0" class="statement-row cost-row">
              <span class="row-label">{{ t('ledger.depositInterestPaid') }}</span>
              <span class="amount-negative">{{ formatAmount(-(ledger.totalDepositInterestPaid ?? 0)) }}</span>
              <button
                class="drill-btn"
                :class="{ active: drillCategory === 'DEPOSIT_INTEREST_PAID' }"
                :aria-label="t('ledger.drillDown') + ': ' + t('ledger.depositInterestPaid')"
                @click="toggleDrill('DEPOSIT_INTEREST_PAID')"
              >
                {{ drillCategory === 'DEPOSIT_INTEREST_PAID' ? 'Ôľ▓' : 'Ôľ╝' }}
              </button>
            </div>
            <div v-if="(ledger.totalLoanInterestExpense ?? 0) > 0" class="statement-row cost-row">
              <span class="row-label">{{ t('ledger.loanInterestExpense') }}</span>
              <span class="amount-negative">{{ formatAmount(-(ledger.totalLoanInterestExpense ?? 0)) }}</span>
              <button
                class="drill-btn"
                :class="{ active: drillCategory === 'LOAN_INTEREST_EXPENSE' }"
                :aria-label="t('ledger.drillDown') + ': ' + t('ledger.loanInterestExpense')"
                @click="toggleDrill('LOAN_INTEREST_EXPENSE')"
              >
                {{ drillCategory === 'LOAN_INTEREST_EXPENSE' ? 'Ôľ▓' : 'Ôľ╝' }}
              </button>
            </div>
            <div v-if="ledger.totalTaxPaid > 0" class="statement-row cost-row">
              <span class="row-label">{{ t('ledger.taxPaid') }}</span>
              <span class="amount-negative">{{ formatAmount(-ledger.totalTaxPaid) }}</span>
              <button class="drill-btn" :class="{ active: drillCategory === 'TAX' }" :aria-label="t('ledger.drillDown') + ': ' + t('ledger.taxPaid')" @click="toggleDrill('TAX')">
                {{ drillCategory === 'TAX' ? 'Ôľ▓' : 'Ôľ╝' }}
              </button>
            </div>
            <div class="statement-row total-row">
              <span class="row-label">{{ t('ledger.netIncome') }}</span>
              <span :class="amountClass(ledger.netIncome)">{{ formatAmount(ledger.netIncome) }}</span>
            </div>
          </div>
        </div>

        <div class="statement-card">
          <h2 class="statement-title">­čôŐ {{ t('ledger.balanceSheet') }}</h2>
          <div class="statement-rows">
            <div class="statement-row">
              <span class="row-label">{{ t('ledger.cash') }}</span>
              <span>{{ formatAmount(ledger.currentCash) }}</span>
            </div>
            <div class="statement-row">
              <span class="row-label">{{ t('ledger.propertyValue') }}</span>
              <span>{{ formatAmount(ledger.propertyValue) }}</span>
              <button
                class="drill-btn"
                :class="{ active: drillCategory === 'PROPERTY_PURCHASE' }"
                :aria-label="t('ledger.drillDown') + ': ' + t('ledger.propertyValue')"
                @click="toggleDrill('PROPERTY_PURCHASE')"
              >
                {{ drillCategory === 'PROPERTY_PURCHASE' ? 'Ôľ▓' : 'Ôľ╝' }}
              </button>
            </div>
            <div class="statement-row">
              <span class="row-label">{{ t('ledger.propertyAppreciation') }}</span>
              <span :class="amountClass(ledger.propertyAppreciation)">{{ formatAmount(ledger.propertyAppreciation) }}</span>
            </div>
            <div class="statement-row">
              <span class="row-label">{{ t('ledger.buildingValue') }}</span>
              <span>{{ formatAmount(ledger.buildingValue) }}</span>
              <button
                class="drill-btn"
                :class="{ active: drillCategory === 'BUILDING_VALUE' }"
                :aria-label="t('ledger.drillDown') + ': ' + t('ledger.buildingValue')"
                @click="toggleDrill('BUILDING_VALUE')"
              >
                {{ drillCategory === 'BUILDING_VALUE' ? 'Ôľ▓' : 'Ôľ╝' }}
              </button>
            </div>
            <div class="statement-row">
              <span class="row-label">{{ t('ledger.inventoryValue') }}</span>
              <span>{{ formatAmount(ledger.inventoryValue) }}</span>
              <button
                class="drill-btn"
                :class="{ active: drillCategory === 'INVENTORY_VALUE' }"
                :aria-label="t('ledger.drillDown') + ': ' + t('ledger.inventoryValue')"
                @click="toggleDrill('INVENTORY_VALUE')"
              >
                {{ drillCategory === 'INVENTORY_VALUE' ? 'Ôľ▓' : 'Ôľ╝' }}
              </button>
            </div>
            <div v-if="(ledger.totalDepositsPlaced ?? 0) > 0" class="statement-row">
              <span class="row-label">{{ t('ledger.depositsPlaced') }}</span>
              <span>{{ formatAmount(ledger.totalDepositsPlaced ?? 0) }}</span>
              <button
                class="drill-btn"
                :class="{ active: drillCategory === 'DEPOSIT_MADE' }"
                :aria-label="t('ledger.drillDown') + ': ' + t('ledger.depositsPlaced')"
                @click="toggleDrill('DEPOSIT_MADE')"
              >
                {{ drillCategory === 'DEPOSIT_MADE' ? 'Ôľ▓' : 'Ôľ╝' }}
              </button>
            </div>
            <div class="statement-row total-row">
              <span class="row-label">{{ t('ledger.totalAssets') }}</span>
              <span>{{ formatAmount(ledger.totalAssets) }}</span>
            </div>
          </div>
        </div>

        <div class="statement-card">
          <h2 class="statement-title">­čĺÁ {{ t('ledger.cashFlow') }}</h2>
          <div class="statement-rows">
            <div class="statement-row">
              <span class="row-label">{{ t('ledger.cashFromOperations') }}</span>
              <span :class="amountClass(ledger.cashFromOperations)">{{ formatAmount(ledger.cashFromOperations) }}</span>
            </div>
            <div class="statement-row">
              <span class="row-label">{{ t('ledger.cashFromInvestments') }}</span>
              <span :class="amountClass(ledger.cashFromInvestments)">{{ formatAmount(ledger.cashFromInvestments) }}</span>
            </div>
            <div v-if="(ledger.cashFromBanking ?? 0) !== 0" class="statement-row">
              <span class="row-label">{{ t('ledger.cashFromBanking') }}</span>
              <span :class="amountClass(ledger.cashFromBanking ?? 0)">{{ formatAmount(ledger.cashFromBanking ?? 0) }}</span>
              <button
                class="drill-btn"
                :class="{ active: drillCategory === 'DEPOSIT_MADE' }"
                :aria-label="t('ledger.drillDown') + ': ' + t('ledger.depositsPlaced')"
                @click="toggleDrill('DEPOSIT_MADE')"
              >
                {{ drillCategory === 'DEPOSIT_MADE' ? 'Ôľ▓' : 'Ôľ╝' }}
              </button>
            </div>
            <div v-if="ledger.totalStockPurchaseCashOut > 0" class="statement-row">
              <span class="row-label">{{ t('ledger.stockPurchases') }}</span>
              <span class="amount-negative">{{ formatAmount(-ledger.totalStockPurchaseCashOut) }}</span>
              <button
                class="drill-btn"
                :class="{ active: drillCategory === 'STOCK_PURCHASE' }"
                :aria-label="t('ledger.drillDown') + ': ' + t('ledger.stockPurchases')"
                @click="toggleDrill('STOCK_PURCHASE')"
              >
                {{ drillCategory === 'STOCK_PURCHASE' ? 'Ôľ▓' : 'Ôľ╝' }}
              </button>
            </div>
            <div v-if="ledger.totalStockSaleCashIn > 0" class="statement-row">
              <span class="row-label">{{ t('ledger.stockSales') }}</span>
              <span class="amount-positive">{{ formatAmount(ledger.totalStockSaleCashIn) }}</span>
              <button class="drill-btn" :class="{ active: drillCategory === 'STOCK_SALE' }" :aria-label="t('ledger.drillDown') + ': ' + t('ledger.stockSales')" @click="toggleDrill('STOCK_SALE')">
                {{ drillCategory === 'STOCK_SALE' ? 'Ôľ▓' : 'Ôľ╝' }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <div v-if="drillCategory" class="drill-panel">
        <div class="drill-header">
          <h3>{{ t('ledger.drillDown') }}: {{ t(`ledger.category.${drillCategory}`) }}</h3>
          <button
            class="btn btn-ghost btn-sm"
            @click="
              () => {
                drillCategory = null
                drillEntries = []
              }
            "
          >
            ÔťĽ {{ t('common.close') }}
          </button>
        </div>
        <div v-if="drillLoading" class="state-box-sm">{{ t('common.loading') }}</div>
        <div v-else-if="drillEntries.length === 0" class="state-box-sm">
          {{ t('ledger.noEntries') }}
        </div>
        <div v-else class="drill-table-wrapper">
          <table class="drill-table">
            <thead>
              <tr>
                <th>{{ t('ledger.description') }}</th>
                <th>{{ t('ledger.amount') }}</th>
                <th>{{ t('ledger.tick') }}</th>
                <th>{{ t('ledger.building') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="entry in drillEntries" :key="entry.id">
                <td>{{ entry.productName ?? entry.resourceName ?? entry.description }}</td>
                <td :class="amountClass(entry.amount)">
                  {{ formatAmount(entry.amount, entry.currencyCode) }}
                  <span v-if="showInlineCurrencyBadge(entry.currencyCode)" class="currency-badge currency-badge-inline">{{ entry.currencyCode }}</span>
                </td>
                <td>{{ entry.recordedAtTick }}</td>
                <td>
                  <RouterLink v-if="entry.buildingId" :to="entry.buildingType === 'BANK' ? `/bank/${entry.buildingId}` : `/building/${entry.buildingId}`" class="link-btn">
                    {{ entry.buildingName ?? t('ledger.viewBuilding') }}
                  </RouterLink>
                  <span v-else>ÔÇö</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div v-if="ledger.buildingSummaries.length > 0" class="buildings-card">
        <h2 class="statement-title">­čĆş {{ t('ledger.buildingsPerformance') }}</h2>
        <div class="buildings-table-wrapper">
          <table class="buildings-table">
            <thead>
              <tr>
                <th>{{ t('ledger.buildingName') }}</th>
                <th>{{ t('ledger.buildingType') }}</th>
                <th>{{ t('ledger.currency') }}</th>
                <th>{{ t('ledger.revenue') }}</th>
                <th>{{ t('ledger.costs') }}</th>
                <th>{{ t('ledger.profit') }}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="b in ledger.buildingSummaries" :key="b.buildingId">
                <td>{{ b.buildingName }}</td>
                <td>{{ b.buildingType }}</td>
                <td>
                  <span class="currency-badge">{{ b.currencyCode }}</span>
                </td>
                <td class="amount-positive">{{ formatAmount(b.revenue, b.currencyCode) }}</td>
                <td class="amount-negative">{{ formatAmount(-b.costs, b.currencyCode) }}</td>
                <td :class="amountClass(b.revenue - b.costs)">
                  {{ formatAmount(b.revenue - b.costs, b.currencyCode) }}
                </td>
                <td>
                  <RouterLink :to="b.buildingType === 'BANK' ? `/bank/${b.buildingId}` : `/building/${b.buildingId}`" class="btn btn-ghost btn-sm">
                    {{ t('ledger.manage') }}
                  </RouterLink>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  </div>


</template>

<style scoped src="./LedgerView.styles.css"></style>


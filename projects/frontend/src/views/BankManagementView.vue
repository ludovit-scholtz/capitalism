<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { useTickRefresh } from '@/composables/useTickRefresh'
import { useScrollPreservation } from '@/composables/useScrollPreservation'
import { useGameStateStore } from '@/stores/gameState'
import { deepEqual } from '@/lib/utils'
import { getActiveCompany } from '@/lib/accountContext'
import UiStateLoading from '@/components/ui/UiStateLoading.vue'
import UiStateError from '@/components/ui/UiStateError.vue'
import type {
  LoanSummary,
  BankDepositSummary,
  BankInfoSummary,
  Company,
  PlayerBankAccountSummary,
  BankDepositRateHistorySummary,
} from '@/types'
import BankCustomerView from '@/components/bank/BankCustomerView.vue'
import BankManagementTabContent from '@/components/bank/BankManagementTabContent.vue'
import {
  BANK_LOANS_QUERY,
  BANK_INFO_QUERY,
  BANK_DEPOSITS_QUERY,
  SET_BANK_RATES_MUTATION,
  MY_COMPANIES_QUERY,
  MY_BANK_ACCOUNTS_QUERY,
  MY_DEPOSITS_QUERY,
  MY_LOANS_QUERY,
  INITIATE_BASE_DEPOSIT_MUTATION,
  UPDATE_BANK_DEPOSIT_RATE_MUTATION,
  BANK_DEPOSIT_RATE_HISTORY_QUERY,
} from '@/lib/bankManagementQueries'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const gameStateStore = useGameStateStore()
const { saveScrollPosition, restoreScrollPosition } = useScrollPreservation()

const bankBuildingId = computed(() => route.params.buildingId as string)

const loading = ref(true)
const error = ref<string | null>(null)
const issuedLoans = ref<LoanSummary[]>([])
const bankDeposits = ref<BankDepositSummary[]>([])
const bankInfo = ref<BankInfoSummary | null>(null)
const userCompanies = ref<Company[]>([])

// Ownership detection uses the navbar-selected company and bank owner company id.
const isOwner = computed(() => {
  if (!auth.isAuthenticated) return false
  const selectedCompany = getActiveCompany(auth.player, userCompanies.value)
  const bankOwnerCompanyId = bankInfo.value?.lenderCompanyId
  if (!selectedCompany || !bankOwnerCompanyId) return false
  return selectedCompany.id === bankOwnerCompanyId
})

// Customer-specific state
const myDepositsHere = ref<BankDepositSummary[]>([])
// My active loans at this bank (customer view)
const myLoansHere = ref<LoanSummary[]>([])
// Operating bank accounts matching the bank's city currency (customer view)
const myOperatingAccountsHere = ref<PlayerBankAccountSummary[]>([])

// Rate configuration form
const showRatesForm = ref(false)
const ratesForm = ref({ depositInterestRatePercent: 3, lendingInterestRatePercent: 8 })
const ratesLoading = ref(false)
const ratesError = ref<string | null>(null)
const ratesSuccess = ref(false)

// Base capital deposit (activation)
const baseDepositLoading = ref(false)
const baseDepositError = ref<string | null>(null)
const baseDepositSuccess = ref(false)

// Dynamic deposit rate management
const depositRateHistory = ref<BankDepositRateHistorySummary[]>([])
const depositRateLoading = ref(false)
const depositRateError = ref<string | null>(null)
const depositRateSuccess = ref(false)

async function loadData(isRefresh = false) {
  if (!isRefresh) {
    loading.value = true
  }
  error.value = null
  try {
    // Always load bank info (public) and user companies (for ownership check)
    const [infoResult, companiesResult] = await Promise.all([
      gqlRequest<{ bankInfo: BankInfoSummary }>(BANK_INFO_QUERY, {
        id: bankBuildingId.value,
      }),
      auth.isAuthenticated ? gqlRequest<{ me: { companies: Company[] } }>(MY_COMPANIES_QUERY) : Promise.resolve({ me: { companies: [] } }),
    ])
    bankInfo.value = infoResult.bankInfo ?? null
    userCompanies.value = companiesResult.me?.companies ?? []

    const selectedCompany = getActiveCompany(auth.player, userCompanies.value)
    const ownerDetected = !!selectedCompany && selectedCompany.id === bankInfo.value?.lenderCompanyId

    if (ownerDetected) {
      // Owner view: load full management data
      const [loansResult, depositsResult, historyResult] = await Promise.all([
        gqlRequest<{ bankLoans: LoanSummary[] }>(BANK_LOANS_QUERY, {
          bankBuildingId: bankBuildingId.value,
        }),
        gqlRequest<{ bankDeposits: BankDepositSummary[] }>(BANK_DEPOSITS_QUERY, {
          id: bankBuildingId.value,
        }),
        gqlRequest<{ bankDepositRateHistory: BankDepositRateHistorySummary[] }>(
          BANK_DEPOSIT_RATE_HISTORY_QUERY,
          { bankBuildingId: bankBuildingId.value },
        ),
      ])
      const loans = loansResult.bankLoans ?? []
      if (!deepEqual(issuedLoans.value, loans)) {
        issuedLoans.value = loans
      }
      const deposits = depositsResult.bankDeposits ?? []
      if (!deepEqual(bankDeposits.value, deposits)) {
        bankDeposits.value = deposits
      }
      const history = historyResult.bankDepositRateHistory ?? []
      if (!deepEqual(depositRateHistory.value, history)) {
        depositRateHistory.value = history
      }
      if (bankInfo.value && !showRatesForm.value) {
        ratesForm.value.depositInterestRatePercent = bankInfo.value.depositInterestRatePercent
        ratesForm.value.lendingInterestRatePercent = bankInfo.value.lendingInterestRatePercent
      }
    } else {
      // Customer view: load deposit relationship, active loans, and operating accounts at this bank.
      const [depositsResult, myLoansResult, accountsResult] = await Promise.all([
        auth.isAuthenticated ? gqlRequest<{ myDeposits: BankDepositSummary[] }>(MY_DEPOSITS_QUERY) : Promise.resolve({ myDeposits: [] }),
        auth.isAuthenticated ? gqlRequest<{ myLoans: LoanSummary[] }>(MY_LOANS_QUERY) : Promise.resolve({ myLoans: [] }),
        auth.isAuthenticated ? gqlRequest<{ myBankAccounts: PlayerBankAccountSummary[] }>(MY_BANK_ACCOUNTS_QUERY) : Promise.resolve({ myBankAccounts: [] }),
      ])
      const myDeposits = (depositsResult.myDeposits ?? []).filter((d) => d.bankBuildingId === bankBuildingId.value)
      if (!deepEqual(myDepositsHere.value, myDeposits)) {
        myDepositsHere.value = myDeposits
      }
      const loansHere = (myLoansResult.myLoans ?? []).filter((l: LoanSummary) => l.bankBuildingId === bankBuildingId.value)
      if (!deepEqual(myLoansHere.value, loansHere)) {
        myLoansHere.value = loansHere
      }
      // Operating accounts for the active context (company/person) matching this bank's city currency
      const bankCurrency = bankInfo.value?.cityCurrencyCode
      const activeComp = getActiveCompany(auth.player, userCompanies.value)
      const operatingAccounts = (accountsResult.myBankAccounts ?? []).filter((a) => {
        if (!bankCurrency || a.currencyCode.toUpperCase() !== bankCurrency.toUpperCase() || a.bankBuildingId !== bankBuildingId.value) {
          return false
        }

        if (auth.player?.activeAccountType === 'COMPANY') {
          return a.ownerType === 'COMPANY' && a.companyId === activeComp?.id
        }

        return a.ownerType === 'PERSON'
      })
      if (!deepEqual(myOperatingAccountsHere.value, operatingAccounts)) {
        myOperatingAccountsHere.value = operatingAccounts
      }
    }
  } catch (err) {
    error.value = err instanceof Error ? err.message : String(err)
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  auth.initFromStorage()
  if (auth.isAuthenticated && !auth.player) {
    try {
      await auth.fetchMe()
    } catch {
      // loadData will surface any user-visible auth/data errors.
    }
  }

  await loadData()
})

useTickRefresh(async () => {
  const scrollPos = saveScrollPosition()
  await loadData(true)
  await restoreScrollPosition(scrollPos)
})

const overdueLoans = computed(() => issuedLoans.value.filter((l) => l.status !== 'ACTIVE' && l.status !== 'REPAID'))

/** The active city currency code — from bankInfo when available, fallback to EUR. */
const cityCurrency = computed(() => bankInfo.value?.cityCurrencyCode ?? 'EUR')


const totalIssuedCapacity = computed(() => issuedLoans.value.filter((l) => l.status === 'ACTIVE' || l.status === 'OVERDUE').reduce((sum, l) => sum + l.remainingPrincipal, 0))

const expectedMonthlyIncome = computed(() => issuedLoans.value.filter((l) => l.status === 'ACTIVE' || l.status === 'OVERDUE').reduce((sum, l) => sum + l.paymentAmount, 0))

async function saveRates() {
  if (!bankBuildingId.value) return
  ratesLoading.value = true
  ratesError.value = null
  ratesSuccess.value = false
  try {
    const result = await gqlRequest<{ setBankRates: BankInfoSummary }>(SET_BANK_RATES_MUTATION, {
      input: {
        bankBuildingId: bankBuildingId.value,
        depositInterestRatePercent: ratesForm.value.depositInterestRatePercent,
        lendingInterestRatePercent: ratesForm.value.lendingInterestRatePercent,
      },
    })
    bankInfo.value = result.setBankRates
    ratesSuccess.value = true
    showRatesForm.value = false
  } catch (err) {
    ratesError.value = err instanceof Error ? err.message : String(err)
  } finally {
    ratesLoading.value = false
  }
}

async function submitBaseDeposit() {
  if (!bankBuildingId.value) return
  baseDepositLoading.value = true
  baseDepositError.value = null
  baseDepositSuccess.value = false
  try {
    const result = await gqlRequest<{ initiateBaseDeposit: BankInfoSummary }>(INITIATE_BASE_DEPOSIT_MUTATION, { bankBuildingId: bankBuildingId.value })
    bankInfo.value = result.initiateBaseDeposit
    baseDepositSuccess.value = true
    await loadData(true)
  } catch (err) {
    baseDepositError.value = err instanceof Error ? err.message : String(err)
  } finally {
    baseDepositLoading.value = false
  }
}

const activeCompany = computed(() => getActiveCompany(auth.player, userCompanies.value))
const isCompanyAccountActive = computed(() => auth.player?.activeAccountType === 'COMPANY' && !!activeCompany.value)
const isPersonalAccountActive = computed(() => auth.player?.activeAccountType === 'PERSON')

async function saveDepositRate(newRate: number) {
  if (!bankBuildingId.value) return
  depositRateLoading.value = true
  depositRateError.value = null
  depositRateSuccess.value = false
  try {
    const result = await gqlRequest<{ updateBankDepositRate: BankInfoSummary }>(
      UPDATE_BANK_DEPOSIT_RATE_MUTATION,
      { input: { bankBuildingId: bankBuildingId.value, newRatePercent: newRate } },
    )
    bankInfo.value = result.updateBankDepositRate
    depositRateSuccess.value = true
    // Reload history to show the new pending entry
    const histResult = await gqlRequest<{ bankDepositRateHistory: BankDepositRateHistorySummary[] }>(
      BANK_DEPOSIT_RATE_HISTORY_QUERY,
      { bankBuildingId: bankBuildingId.value },
    )
    depositRateHistory.value = histResult.bankDepositRateHistory ?? []
  } catch (err) {
    depositRateError.value = err instanceof Error ? err.message : String(err)
  } finally {
    depositRateLoading.value = false
  }
}
</script>

<template>
  <main class="bank-management-view container mx-auto px-4 pb-16 pt-6 sm:px-6 lg:px-8 lg:pb-20 lg:pt-8">
    <div class="page-header mb-10 flex flex-col gap-3 lg:mb-12">
      <!-- Show different titles based on ownership -->
      <template v-if="!loading && isOwner">
        <h1 class="page-title text-4xl font-black tracking-tight text-body">{{ t('bank.configureBank') }}</h1>
        <p class="page-subtitle text-sm text-muted sm:text-base">{{ t('bank.lending') }}</p>
      </template>
      <template v-else-if="!loading">
        <div class="customer-nav">
          <button class="btn-back" @click="router.push('/banking')">← {{ t('bank.backToMarketplace') }}</button>
        </div>
        <h1 class="page-title text-4xl font-black tracking-tight text-body">{{ bankInfo?.bankBuildingName ?? t('bank.customerView') }}</h1>
        <p class="page-subtitle flex flex-wrap items-center gap-2 text-sm text-muted sm:text-base">
          {{ bankInfo?.lenderCompanyName }} • {{ bankInfo?.cityName }}
          <span v-if="bankInfo?.cityCurrencyCode" class="currency-badge">{{ bankInfo.cityCurrencyCode }}</span>
        </p>
      </template>
      <template v-else>
        <h1 class="page-title text-4xl font-black tracking-tight text-body">{{ t('bank.customerView') }}</h1>
      </template>
    </div>

    <UiStateLoading v-if="loading" :label="t('common.loading')" />

    <UiStateError v-else-if="error" :message="error" :retry-label="t('common.retry')" @retry="loadData" />

    <template v-else>
      <!-- ── OWNER VIEW ─────────────────────────────────────────────── -->
      <template v-if="isOwner">
        <BankManagementTabContent
          :bank-info="bankInfo!"
          :bank-deposits="bankDeposits"
          :issued-loans="issuedLoans"
          :total-issued-capacity="totalIssuedCapacity"
          :expected-monthly-income="expectedMonthlyIncome"
          :overdue-loans="overdueLoans"
          :city-currency="cityCurrency"
          :show-rates-form="showRatesForm"
          :rates-form="ratesForm"
          :rates-loading="ratesLoading"
          :rates-error="ratesError"
          :rates-success="ratesSuccess"
          :base-deposit-loading="baseDepositLoading"
          :base-deposit-error="baseDepositError"
          :base-deposit-success="baseDepositSuccess"
          :deposit-rate-history="depositRateHistory"
          :deposit-rate-loading="depositRateLoading"
          :deposit-rate-error="depositRateError"
          :deposit-rate-success="depositRateSuccess"
          :current-tick="gameStateStore.gameState?.currentTick ?? 0"
          @update:show-rates-form="showRatesForm = $event"
          @update:rates-form="ratesForm = $event"
          @save-rates="saveRates"
          @submit-base-deposit="submitBaseDeposit"
          @save-deposit-rate="saveDepositRate"
        />
      </template>

      <!-- Customer View -->
      <template v-else>
        <BankCustomerView
          :bank-info="bankInfo"
          :bank-building-id="bankBuildingId"
          :is-authenticated="auth.isAuthenticated"
          :active-company="activeCompany"
          :is-company-account-active="isCompanyAccountActive"
          :is-personal-account-active="isPersonalAccountActive"
          :my-operating-accounts-here="myOperatingAccountsHere"
          :my-loans-here="myLoansHere"
          :my-deposits-here="myDepositsHere"
          :current-tick="gameStateStore.gameState?.currentTick ?? 0"
          @data-changed="loadData(true)"
        />
      </template>
    </template>
  </main>
</template>

<style scoped>
.bank-management-view {
  margin: 0 auto;
}

.page-header {
  margin-bottom: var(--spacing-xl);
}

.page-title {
  font-size: 1.8rem;
  font-weight: 700;
}

.page-subtitle {
  color: var(--color-text-secondary);
}

/* Customer view styles */
.customer-nav {
  margin-bottom: 0.5rem;
}

.btn-back {
  background: none;
  border: none;
  color: var(--color-text-secondary);
  cursor: pointer;
  font-size: 0.875rem;
  padding: 0;
  text-decoration: underline;
}

.btn-back:hover {
  color: var(--color-text-primary);
}

.currency-badge {
  display: inline-block;
  font-size: 0.7rem;
  font-weight: 700;
  letter-spacing: 0.05em;
  padding: 0.1em 0.45em;
  border-radius: var(--radius-sm, 4px);
  background: var(--color-accent-alpha, rgba(59, 130, 246, 0.15));
  color: var(--color-accent, #3b82f6);
  vertical-align: middle;
}
</style>
